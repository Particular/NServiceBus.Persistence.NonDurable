namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Outbox;
    using Persistence;
    using Transport;

    class InMemoryTransactionalSynchronizedStorageAdapter2 : ISynchronizedStorageAdapter
    {
        public Task<CompletableSynchronizedStorageSession> TryAdapt(OutboxTransaction transaction, ContextBag context)
        {
            if (transaction is InMemoryOutboxTransaction inMemOutboxTransaction)
            {
                CompletableSynchronizedStorageSession session = new InMemorySynchronizedStorageSession2(inMemOutboxTransaction.Transaction);
                return Task.FromResult(session);
            }
            return EmptyTask;
        }

        public Task<CompletableSynchronizedStorageSession> TryAdapt(TransportTransaction transportTransaction, ContextBag context)
        {
            if (transportTransaction.TryGet(out Transaction ambientTransaction))
            {
                var transaction = new InMemoryTransaction2();
                CompletableSynchronizedStorageSession session = new InMemorySynchronizedStorageSession2(transaction);
                ambientTransaction.EnlistVolatile(new EnlistmentNotification2(transaction), EnlistmentOptions.None);
                return Task.FromResult(session);
            }
            return EmptyTask;
        }

        static readonly Task<CompletableSynchronizedStorageSession> EmptyTask = Task.FromResult<CompletableSynchronizedStorageSession>(null);

        class EnlistmentNotification2 : IEnlistmentNotification
        {
            public EnlistmentNotification2(InMemoryTransaction2 transaction)
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

            InMemoryTransaction2 transaction;
        }
    }
}