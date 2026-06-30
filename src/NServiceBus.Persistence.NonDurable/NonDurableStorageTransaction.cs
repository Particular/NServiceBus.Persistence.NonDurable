namespace NServiceBus;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

class NonDurableStorageTransaction
{
    public void Enlist<TState>(TState state, Action<TState> apply, Action<TState>? rollback = null, Activity? activity = null)
    {
        ArgumentNullException.ThrowIfNull(apply);
        var operation = new TransactionOperation<TState>(state, apply, rollback);
        enlistedOperations.Add(operation);
        if (operation.CanRollback)
        {
            rollbackOperations.Add(operation);
        }

        // The activity is passed explicitly by the persister (the span owner), never
        // captured from Activity.Current — user code can replace Activity.Current via
        // AsyncLocal, so relying on it would capture the wrong span.
        if (activity is not null)
        {
            enlistedActivities.Add(activity);
        }

        tracingActivity ??= activity;
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
            CompleteEnlistedActivities(success: true);
        }
        catch (Exception ex)
        {
            RollbackAppliedOperations();

            NonDurablePersistenceTracing.AddTransactionRolledBackEvent(tracingActivity, operationCount);
            CompleteEnlistedActivities(success: false, exception: ex);
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
        CompleteEnlistedActivities(success: false);
        enlistedOperations.Clear();
    }

    // Called by NonDurableSynchronizedStorageSession.Dispose / NonDurableOutboxTransaction.Dispose
    // when the transaction was abandoned without Commit or Rollback (e.g. handler failure in the
    // non-DTC path). Disposes tracked activities to avoid leaks. In the DTC path, the ambient
    // transaction drives Commit/Rollback via EnlistmentNotification, so this is not called for
    // enlisted-in-ambient sessions (the session checks enlistedInAmbientTransaction before calling).
    internal void DisposeTrackedActivities()
    {
        if (enlistedActivities.Count == 0)
        {
            return;
        }

        // Transaction was abandoned without commit — mark activities as error and dispose.
        foreach (var activity in enlistedActivities)
        {
            NonDurablePersistenceTracing.MarkError(activity, "Transaction abandoned without commit");
            activity.Dispose();
        }

        enlistedActivities.Clear();
    }

    void CompleteEnlistedActivities(bool success, Exception? exception = null)
    {
        if (enlistedActivities.Count == 0)
        {
            return;
        }

        foreach (var activity in enlistedActivities)
        {
            if (success)
            {
                NonDurablePersistenceTracing.MarkSuccess(activity);
            }
            else if (exception is not null)
            {
                // The exception escaped the span boundary — Commit rethrows after this.
                NonDurablePersistenceTracing.MarkError(activity, exception, exceptionEscaped: true);
            }
            else
            {
                NonDurablePersistenceTracing.MarkError(activity, "Transaction rolled back");
            }

            activity.Dispose();
        }

        enlistedActivities.Clear();
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
    readonly List<Activity> enlistedActivities = [];
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