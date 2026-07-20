namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Extensibility;
using Outbox;
using Persistence;
using SagaPersister;
using Transport;

class NonDurableSynchronizedStorageSession : ICompletableSynchronizedStorageSession, INonDurableStorageSession, INonDurableSagaLockingSession
{
    public NonDurableSynchronizedStorageSession() : this(NonDurableStorageRuntime.SharedStorage, new NonDurableSagaOptions())
    {
    }

    public NonDurableSynchronizedStorageSession(NonDurableStorage storage) : this(storage, new NonDurableSagaOptions())
    {
    }

    public NonDurableSynchronizedStorageSession(NonDurableStorage storage, NonDurableSagaOptions sagaOptions)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        ArgumentNullException.ThrowIfNull(sagaOptions);
        sagaLockingSession = new SagaLockingSessionState(sagaOptions.PessimisticLockTimeout);
        completionCallbacks.Add(sagaLockingSession.ReleaseAllSagaLocks);
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
                completionCallbacks.Run();
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
            nonDurableOutboxTransaction.OnCompleted(completionCallbacks.Run);
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
        enlistmentNotification = new EnlistmentNotification(Transaction, completionCallbacks.Run);
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
                completionCallbacks.Run();
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

    bool INonDurableSagaLockingSession.TryAcquireSagaLock(Guid sagaId, SagaEntry entry, CancellationToken cancellationToken) =>
        sagaLockingSession.TryAcquireSagaLock(sagaId, entry, cancellationToken);

    void INonDurableSagaLockingSession.ReleaseSagaLock(SagaEntry entry) => sagaLockingSession.ReleaseSagaLock(entry);

    bool ownsTransaction;
    bool enlistedInAmbientTransaction;
    readonly NonDurableStorage storage;
    readonly SagaLockingSessionState sagaLockingSession;
    readonly CompletionCallbacks completionCallbacks = new();

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
