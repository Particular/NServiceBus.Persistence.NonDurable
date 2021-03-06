namespace NServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Persistence;

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

        public Task CompleteAsync(CancellationToken cancellationToken = default)
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