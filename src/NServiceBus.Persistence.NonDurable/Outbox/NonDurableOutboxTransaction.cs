namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Outbox;

    class NonDurableOutboxTransaction : OutboxTransaction
    {
        public NonDurableOutboxTransaction()
        {
            Transaction = new NonDurableTransaction();
        }

        public NonDurableTransaction Transaction { get; private set; }

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