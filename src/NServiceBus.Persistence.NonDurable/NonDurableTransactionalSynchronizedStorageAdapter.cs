namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Outbox;
    using Persistence;
    using Transport;

    class NonDurableTransactionalSynchronizedStorageAdapter : ISynchronizedStorageAdapter
    {
        public Task<CompletableSynchronizedStorageSession> TryAdapt(OutboxTransaction transaction, ContextBag context)
        {
            if (transaction is NonDurableOutboxTransaction inMemOutboxTransaction)
            {
                CompletableSynchronizedStorageSession session = new NonDurableSynchronizedStorageSession(inMemOutboxTransaction.Transaction);
                return Task.FromResult(session);
            }
            return EmptyTask;
        }

        public Task<CompletableSynchronizedStorageSession> TryAdapt(TransportTransaction transportTransaction, ContextBag context)
        {
            if (transportTransaction.TryGet(out Transaction ambientTransaction))
            {
                var transaction = new NonDurableTransaction();
                CompletableSynchronizedStorageSession session = new NonDurableSynchronizedStorageSession(transaction);
                ambientTransaction.EnlistVolatile(new EnlistmentNotification2(transaction), EnlistmentOptions.None);
                return Task.FromResult(session);
            }
            return EmptyTask;
        }

        static readonly Task<CompletableSynchronizedStorageSession> EmptyTask = Task.FromResult<CompletableSynchronizedStorageSession>(null);

        class EnlistmentNotification2 : IEnlistmentNotification
        {
            public EnlistmentNotification2(NonDurableTransaction transaction)
            {
                this.transaction = transaction;
            }

            public void Prepare(PreparingEnlistment preparingEnlistment)
            {
                try
                {
                    transaction.Commit();
                    preparingEnlistment.Prepared();
                }
                catch (Exception ex)
                {
                    preparingEnlistment.ForceRollback(ex);
                }
            }

            public void Commit(Enlistment enlistment)
            {
                enlistment.Done();
            }

            public void Rollback(Enlistment enlistment)
            {
                transaction.Rollback();
                enlistment.Done();
            }

            public void InDoubt(Enlistment enlistment)
            {
                enlistment.Done();
            }

            readonly NonDurableTransaction transaction;
        }
    }
}