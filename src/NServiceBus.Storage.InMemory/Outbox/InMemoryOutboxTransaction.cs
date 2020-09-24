namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Janitor;
    using Outbox;

    [SkipWeaving]
    class InMemoryOutboxTransaction : OutboxTransaction
    {
        public InMemoryOutboxTransaction()
        {
            Transaction = new InMemoryTransaction2();
        }

        public InMemoryTransaction2 Transaction { get; private set; }

        public void Dispose()
        {
            Transaction = null;
        }

        public Task Commit()
        {
            Transaction.Commit();
            return Task.CompletedTask;
        }

        public void Enlist(Action action)
        {
            Transaction.Enlist(action);
        }
    }
}