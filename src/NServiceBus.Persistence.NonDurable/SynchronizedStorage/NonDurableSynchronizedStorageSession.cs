namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Extensibility;
using Outbox;
using Persistence;
using SagaPersister;
using Transport;

class NonDurableSynchronizedStorageSession(NonDurableStorage storage) : ICompletableSynchronizedStorageSession, INonDurableStorageSession, INonDurableSagaLockingSession
{
    public NonDurableSynchronizedStorageSession() : this(NonDurableStorageRuntime.SharedOptimisticStorage)
    {
    }

    public NonDurableStorageTransaction? Transaction { get; private set; }

    public void Dispose()
    {
        if (Transaction is { } tx)
        {
            // In the DTC path, the ambient transaction drives commit/rollback via
            // EnlistmentNotification — do not dispose activities here; they will be
            // disposed when the ambient transaction commits/rolls back. When we own
            // the transaction, and it wasn't committed, dispose tracked activities to
            // avoid leaks (e.g. handler failure in the non-DTC path).
            if (ownsTransaction && !enlistedInAmbientTransaction)
            {
                tx.DisposeTrackedActivities();
                ReleaseSagaLocks();
            }

            Transaction = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    public ValueTask<bool> TryOpen(IOutboxTransaction transaction, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        if (transaction is NonDurableOutboxTransaction nonDurableOutboxTransaction)
        {
            Transaction = nonDurableOutboxTransaction.Transaction;
            ownsTransaction = false;
            nonDurableOutboxTransaction.OnCompleted(ReleaseSagaLocks);
            return new ValueTask<bool>(true);
        }

        return new ValueTask<bool>(false);
    }

    public ValueTask<bool> TryOpen(TransportTransaction transportTransaction, ContextBag context,
        CancellationToken cancellationToken = default) =>
        TryOpen(transportTransaction, out _, cancellationToken);

    internal ValueTask<bool> TryOpen(TransportTransaction transportTransaction,
        out EnlistmentNotification? enlistmentNotification,
        CancellationToken cancellationToken = default)
    {
        // The dedicated key is the private contract with NServiceBus.Transport.NonDurable.
        // The type-based key is the standard contract honored by the shared persistence
        // test suite (and other transports), which publishes the ambient Transaction via
        // transportTransaction.Set(Transaction.Current). Check the dedicated key first so a
        // NonDurable transport transaction wins when both are present, then fall back to the
        // type-based key.
        if (!transportTransaction.TryGet(NonDurableTransactionKeys.Transaction, out Transaction? ambientTransaction)
            && !transportTransaction.TryGet(out ambientTransaction))
        {
            enlistmentNotification = null;
            return new ValueTask<bool>(false);
        }

        if (ambientTransaction is null)
        {
            enlistmentNotification = null;
            return new ValueTask<bool>(false);
        }

        Transaction = new NonDurableStorageTransaction();
        ownsTransaction = true;
        enlistedInAmbientTransaction = true;
        enlistmentNotification = new EnlistmentNotification(Transaction, ReleaseSagaLocks);
        ambientTransaction.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
        return new ValueTask<bool>(true);
    }

    public Task Open(ContextBag context, CancellationToken cancellationToken = default)
    {
        ownsTransaction = true;
        Transaction = new NonDurableStorageTransaction();
        return Task.CompletedTask;
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (ownsTransaction && !enlistedInAmbientTransaction && Transaction is not null)
        {
            try
            {
                Transaction.Commit();
            }
            finally
            {
                ReleaseSagaLocks();
            }
        }

        return Task.CompletedTask;
    }

    public void Enlist<TState>(TState state, Action<TState> apply, Action<TState>? rollback = null, Activity? activity = null)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(Transaction);
        Transaction.Enlist(state, apply, rollback, activity);
    }

    public TSagaData? GetSagaData<TSagaData>(IReadOnlyContextBag context, Func<TSagaData, bool> predicate, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        NonDurableSagaDataProjection.GetSagaData(storage, this, context, predicate, cancellationToken);

    public TSagaData? GetSagaData<TSagaData, TState>(IReadOnlyContextBag context, TState state, Func<TSagaData, TState, bool> predicate, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        NonDurableSagaDataProjection.GetSagaData(storage, this, context, state, predicate, cancellationToken);

    bool INonDurableSagaLockingSession.UsesPessimisticSagaConcurrency => storage.SagaConcurrencyMode == NonDurableSagaConcurrencyMode.Pessimistic;

    bool INonDurableSagaLockingSession.TryAcquireSagaLock(Guid sagaId, CancellationToken cancellationToken)
    {
        if (storage.SagaConcurrencyMode != NonDurableSagaConcurrencyMode.Pessimistic || acquiredSagaLocks.ContainsKey(sagaId))
        {
            return false;
        }

        var lockLease = storage.SagaLocks.Acquire(lockOwnerId, sagaId, cancellationToken);
        acquiredSagaLocks.Add(sagaId, lockLease);
        return true;
    }

    void INonDurableSagaLockingSession.ReleaseSagaLock(Guid sagaId)
    {
        if (acquiredSagaLocks.Remove(sagaId, out var lockLease))
        {
            lockLease.Dispose();
        }
    }

    void ReleaseSagaLocks()
    {
        if (sagaLocksReleased)
        {
            return;
        }

        foreach (var lockLease in acquiredSagaLocks.Values)
        {
            lockLease.Dispose();
        }

        acquiredSagaLocks.Clear();
        sagaLocksReleased = true;
    }

    bool ownsTransaction;
    bool enlistedInAmbientTransaction;
    bool sagaLocksReleased;
    // Session-local, only used used by a single message flow
    readonly Dictionary<Guid, IDisposable> acquiredSagaLocks = [];
    readonly Guid lockOwnerId = Guid.NewGuid();
    readonly NonDurableStorage storage = storage ?? throw new ArgumentNullException(nameof(storage));

    internal class EnlistmentNotification(NonDurableStorageTransaction transaction, Action releaseSagaLocks) : IEnlistmentNotification
    {
        public TaskCompletionSource TransactionCompletionSource { get; } = new();

        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            try
            {
                transaction.Commit();
                preparingEnlistment.Prepared();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                preparingEnlistment.ForceRollback(ex);
            }
        }

        public void Commit(Enlistment enlistment)
        {
            releaseSagaLocks();
            enlistment.Done();
            TransactionCompletionSource.SetResult();
        }

        public void Rollback(Enlistment enlistment)
        {
            transaction.Rollback();
            releaseSagaLocks();
            enlistment.Done();
            TransactionCompletionSource.SetResult();
        }

        public void InDoubt(Enlistment enlistment)
        {
            releaseSagaLocks();
            enlistment.Done();
        }
    }
}
