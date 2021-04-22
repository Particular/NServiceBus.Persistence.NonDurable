namespace NServiceBus
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;

    class NonDurableSynchronizedStorage : ISynchronizedStorage
    {
        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag, CancellationToken cancellationToken = default)
        {
            var session = (CompletableSynchronizedStorageSession)new NonDurableSynchronizedStorageSession();
            return Task.FromResult(session);
        }
    }
}