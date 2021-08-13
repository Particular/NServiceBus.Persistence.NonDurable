namespace NServiceBus
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;

    class NonDurableSynchronizedStorage : ISynchronizedStorage
    {
        public Task<ICompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag, CancellationToken cancellationToken = default) =>
            Task.FromResult<ICompletableSynchronizedStorageSession>(new NonDurableSynchronizedStorageSession());
    }
}