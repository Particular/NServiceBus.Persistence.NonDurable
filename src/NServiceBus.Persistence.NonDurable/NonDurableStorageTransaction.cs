namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

class NonDurableStorageTransaction
{
    public void Enlist<TState>(TState state, Action<TState> apply, Action<TState>? rollback = null)
    {
        ArgumentNullException.ThrowIfNull(apply);
        var operation = new TransactionOperation<TState>(state, apply, rollback);
        enlistedOperations.Add(operation);
        if (operation.CanRollback)
        {
            rollbackOperations.Add(operation);
        }

        tracingActivity ??= Activity.Current;
        NonDurablePersistenceTracing.AddTransactionEnlistedEvent(tracingActivity, typeof(TState).Name);
    }

    public void Commit()
    {
        if (Volatile.Read(ref committed) != 0 || Volatile.Read(ref rollbackWasCalled) != 0)
        {
            return;
        }

        var operationCount = enlistedOperations.Count;

        try
        {
            foreach (var operation in enlistedOperations)
            {
                operation.Apply();

                appliedOperations.Push(operation);
            }

            Volatile.Write(ref committed, 1);
            NonDurablePersistenceTracing.AddTransactionCommittedEvent(tracingActivity, operationCount);
        }
        catch
        {
            RollbackAppliedOperations();

            NonDurablePersistenceTracing.AddTransactionRolledBackEvent(tracingActivity, operationCount);
            throw;
        }
    }

    public void Rollback()
    {
        if (Interlocked.Exchange(ref rollbackWasCalled, 1) != 0)
        {
            return;
        }

        var operationCount = enlistedOperations.Count;

        foreach (var operation in rollbackOperations)
        {
            operation.Rollback();
        }

        NonDurablePersistenceTracing.AddTransactionRolledBackEvent(tracingActivity, operationCount);
        enlistedOperations.Clear();
    }

    void RollbackAppliedOperations()
    {
        while (appliedOperations.TryPop(out var operation))
        {
            operation.Rollback();
        }
    }

    readonly List<ITransactionOperation> enlistedOperations = [];
    readonly List<ITransactionOperation> rollbackOperations = [];
    readonly Stack<ITransactionOperation> appliedOperations = [];
    Activity? tracingActivity;
    int committed;
    int rollbackWasCalled;

    interface ITransactionOperation
    {
        bool CanRollback { get; }
        void Apply();
        void Rollback();
    }

    sealed class TransactionOperation<TState>(TState state, Action<TState> apply, Action<TState>? rollback) : ITransactionOperation
    {
        public bool CanRollback => rollback is not null;

        public void Apply() => apply(state);

        public void Rollback()
        {
            if (rollback is not null)
            {
                rollback(state);
            }
        }
    }
}