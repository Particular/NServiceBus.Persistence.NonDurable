namespace NServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Outbox;

    class NonDurableOutboxTransaction : IOutboxTransaction
    {
        public NonDurableOutboxTransaction() => Transaction = new NonDurableTransaction();

        public NonDurableTransaction Transaction { get; private set; }

        public void Dispose() => Transaction = null;

        public ValueTask DisposeAsync()
        {
            Transaction = null;

            return ValueTask.CompletedTask;
        }

        public Task Commit(CancellationToken cancellationToken = default)
        {
            Transaction.Commit();
            return Task.CompletedTask;
        }

        public void Enlist(Action action) => Transaction.Enlist(action);
    }
}