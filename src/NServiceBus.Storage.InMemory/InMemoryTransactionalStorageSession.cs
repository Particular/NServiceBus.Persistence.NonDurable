namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Janitor;
    using Persistence;

    [SkipWeaving]
    class InMemorySynchronizedStorageSession2 : CompletableSynchronizedStorageSession
    {
        public InMemorySynchronizedStorageSession2(InMemoryTransaction2 transaction)
        {
            Transaction = transaction;
        }

        public InMemorySynchronizedStorageSession2()
            : this(new InMemoryTransaction2())
        {
            ownsTransaction = true;
        }

        public InMemoryTransaction2 Transaction { get; private set; }

        public void Dispose()
        {
            Transaction = null;
        }

        public Task CompleteAsync()
        {
            if (ownsTransaction)
            {
                Transaction.Commit();
            }
            return Task.CompletedTask;
        }

        public void Enlist(Action action)
        {
            Transaction.Enlist(action);
        }

        bool ownsTransaction;
    }
}