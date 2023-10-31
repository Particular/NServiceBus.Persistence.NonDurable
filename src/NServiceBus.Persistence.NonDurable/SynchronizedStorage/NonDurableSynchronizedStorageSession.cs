namespace NServiceBus
{
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
        public NonDurableTransaction Transaction { get; private set; }

        public void Dispose()
        {
            Transaction = null;
        }

        public ValueTask<bool> TryOpen(IOutboxTransaction transaction, ContextBag context, CancellationToken cancellationToken = new CancellationToken())
        {
            if (transaction is NonDurableOutboxTransaction inMemOutboxTransaction)
            {
                Transaction = inMemOutboxTransaction.Transaction;
                return new ValueTask<bool>(true);
            }
            return new ValueTask<bool>(false);
        }

        public ValueTask<bool> TryOpen(TransportTransaction transportTransaction, ContextBag context, CancellationToken cancellationToken = new CancellationToken()) => TryOpen(transportTransaction, out _, cancellationToken);

        internal ValueTask<bool> TryOpen(TransportTransaction transportTransaction, out EnlistmentNotification enlistmentNotification, CancellationToken cancellationToken = new CancellationToken())
        {
            if (transportTransaction.TryGet(out Transaction ambientTransaction))
            {
                Transaction = new NonDurableTransaction();
                ownsNonDurableTransaction = true;
                enlistedInAmbientTransaction = true;
                enlistmentNotification = new EnlistmentNotification(Transaction);
                ambientTransaction.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
                return new ValueTask<bool>(true);
            }

            enlistmentNotification = null;
            return new ValueTask<bool>(false);
        }

        public Task Open(ContextBag contextBag, CancellationToken cancellationToken = new CancellationToken())
        {
            Transaction = new NonDurableTransaction();
            ownsNonDurableTransaction = true;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            if (ownsNonDurableTransaction && !enlistedInAmbientTransaction)
            {
                Transaction.Commit();
            }

            return Task.CompletedTask;
        }

        public void Enlist(Action action, Action rollbackAction) => Transaction.Enlist(action, rollbackAction);

        bool ownsNonDurableTransaction;
        bool enlistedInAmbientTransaction;

        internal class EnlistmentNotification : IEnlistmentNotification
        {
            public TaskCompletionSource TransactionCompletionSource { get; private set; } = new TaskCompletionSource();

            public EnlistmentNotification(NonDurableTransaction transaction) => this.transaction = transaction;

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

            readonly NonDurableTransaction transaction;
        }
    }
}