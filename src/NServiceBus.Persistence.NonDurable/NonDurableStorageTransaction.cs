namespace NServiceBus;

using System;
using System.Collections.Generic;

class NonDurableStorageTransaction
{
    public void Enlist<TState>(TState state, Action<TState> apply, Action<TState>? rollback = null)
    {
        ArgumentNullException.ThrowIfNull(apply);
        enlistedOperations.Add(new TransactionOperation<TState>(state, apply, rollback));
        NonDurablePersistenceTracing.AddTransactionEnlistedEvent(typeof(TState).Name);
    }

    public void Commit()
    {
        var appliedOperations = new Stack<ITransactionOperation>();
        var operationCount = enlistedOperations.Count;
        var committed = false;

        try
        {
            foreach (var operation in enlistedOperations)
            {
                operation.Apply();

                if (operation.CanRollback)
                {
                    appliedOperations.Push(operation);
                }
            }

            committed = true;
        }
        catch
        {
            while (appliedOperations.TryPop(out var operation))
            {
                operation.Rollback();
            }

            NonDurablePersistenceTracing.AddTransactionRolledBackEvent(operationCount);
            throw;
        }
        finally
        {
            if (committed)
            {
                NonDurablePersistenceTracing.AddTransactionCommittedEvent(operationCount);
            }
            enlistedOperations.Clear();
        }
    }

    public void Rollback()
    {
        NonDurablePersistenceTracing.AddTransactionRolledBackEvent(enlistedOperations.Count);
        enlistedOperations.Clear();
    }

    readonly List<ITransactionOperation> enlistedOperations = [];

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