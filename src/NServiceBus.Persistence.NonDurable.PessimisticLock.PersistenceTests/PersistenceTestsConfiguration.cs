namespace NServiceBus.PersistenceTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Outbox;
using NServiceBus.Persistence;
using NServiceBus.Persistence.NonDurable;
using NServiceBus.Sagas;

public partial class PersistenceTestsConfiguration
{
    public bool SupportsDtc => OperatingSystem.IsWindows();

    public bool SupportsOutbox => true;

    public bool SupportsFinders => true;

    public bool SupportsPessimisticConcurrency => true;

    public ISagaIdGenerator SagaIdGenerator { get; private set; }

    public ISagaPersister SagaStorage { get; private set; }

    public Func<ICompletableSynchronizedStorageSession> CreateStorageSession { get; private set; }

    public IOutboxStorage OutboxStorage { get; private set; }

    public Task Configure(CancellationToken cancellationToken = default)
    {
        var storage = new NonDurableStorage();
        var sagaOptions = new NonDurableSagaOptions
        {
            ConcurrencyMode = NonDurableSagaConcurrencyMode.Pessimistic,
            PessimisticLockTimeout = SessionTimeout ?? TimeSpan.FromSeconds(5)
        };

        SagaIdGenerator = new DefaultSagaIdGenerator();
        SagaStorage = new NonDurableSagaPersister(storage, sagaOptions);
        OutboxStorage = new NonDurableOutboxStorage("test-endpoint", storage);
        CreateStorageSession = () => new NonDurableSynchronizedStorageSession(storage, sagaOptions);

        return Task.CompletedTask;
    }

    public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

class DefaultSagaIdGenerator : ISagaIdGenerator
{
    public Guid Generate(SagaIdGeneratorContext context) => Guid.NewGuid();
}
