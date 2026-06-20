namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Extensibility;
using Outbox;
using Persistence;
using Transport;

class NonDurableSynchronizedStorageSession : ICompletableSynchronizedStorageSession
{
    public NonDurableStorageTransaction? Transaction { get; private set; }

    public void Dispose()
    {
        if (Transaction is null)
        {
            return;
        }

        Transaction = null;
    }

    public ValueTask DisposeAsync()
    {
        if (Transaction is null)
        {
            return default;
        }

        Transaction = null;
        return default;
    }

    public ValueTask<bool> TryOpen(IOutboxTransaction transaction, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        if (transaction is NonDurableOutboxTransaction nonDurableOutboxTransaction)
        {
            Transaction = nonDurableOutboxTransaction.Transaction;
            ownsTransaction = false;
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
        if (transportTransaction.TryGet(NonDurableTransactionKeys.Transaction, out Transaction? ambientTransaction) && ambientTransaction is not null)
        {
            Transaction = new NonDurableStorageTransaction();
            ownsTransaction = true;
            enlistedInAmbientTransaction = true;
            enlistmentNotification = new EnlistmentNotification(Transaction);
            ambientTransaction.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
            return new ValueTask<bool>(true);
        }

        enlistmentNotification = null;
        return new ValueTask<bool>(false);
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
            Transaction.Commit();
        }

        return Task.CompletedTask;
    }

    public void Enlist<TState>(TState state, Action<TState> apply, Action<TState>? rollback = null)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(Transaction);
        Transaction.Enlist(state, apply, rollback);
    }

    bool ownsTransaction;
    bool enlistedInAmbientTransaction;

    internal class EnlistmentNotification : IEnlistmentNotification
    {
        public TaskCompletionSource TransactionCompletionSource { get; } = new TaskCompletionSource();

        public EnlistmentNotification(NonDurableStorageTransaction transaction) =>
            this.transaction = transaction;

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
            enlistment.Done();
            TransactionCompletionSource.SetResult();
        }

        public void Rollback(Enlistment enlistment)
        {
            transaction.Rollback();
            enlistment.Done();
            TransactionCompletionSource.SetResult();
        }

        public void InDoubt(Enlistment enlistment) => enlistment.Done();

        readonly NonDurableStorageTransaction transaction;
    }
}
