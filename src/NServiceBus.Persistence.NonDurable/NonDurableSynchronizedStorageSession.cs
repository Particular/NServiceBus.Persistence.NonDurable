namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Janitor;
    using Persistence;

    [SkipWeaving]
    class NonDurableSynchronizedStorageSession : CompletableSynchronizedStorageSession
    {
        public NonDurableSynchronizedStorageSession(NonDurableTransaction transaction)
        {
            Transaction = transaction;
        }

        public NonDurableSynchronizedStorageSession()
            : this(new NonDurableTransaction())
        {
            ownsTransaction = true;
        }

        public NonDurableTransaction Transaction { get; private set; }

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