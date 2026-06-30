namespace NServiceBus.Testing;

using System;
using System.Threading;
using Extensibility;
using Persistence;
using Persistence.NonDurable;
using Persistence.NonDurable.SagaPersister;

/// <summary>
/// A fake implementation of the NonDurable synchronized storage session for testing purposes.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="TestableNonDurableSynchronizedStorageSession" /> using the specified storage instance.
/// </remarks>
public class TestableNonDurableSynchronizedStorageSession(NonDurableStorage storage) : ISynchronizedStorageSession, INonDurableStorageSession
{
    /// <summary>
    /// Initializes a new instance of <see cref="TestableNonDurableSynchronizedStorageSession" /> using a new <see cref="NonDurableStorage" /> instance.
    /// </summary>
    public TestableNonDurableSynchronizedStorageSession() : this(new NonDurableStorage())
    {
    }

    /// <summary>
    /// Adds saga data to the test session storage.
    /// </summary>
    public void AddSaga<TSagaData>(TSagaData sagaData)
        where TSagaData : class, IContainSagaData
    {
        ArgumentNullException.ThrowIfNull(sagaData);

        var noCorrelationId = new CorrelationId(typeof(object), string.Empty, new object());
        storage.Sagas[sagaData.Id] = new SagaEntry(sagaData, noCorrelationId, version: 1, new NonDurableSagaOptions().JsonSerializerOptions);
    }

    /// <inheritdoc />
    public TSagaData? GetSagaData<TSagaData>(IReadOnlyContextBag context, Func<TSagaData, bool> predicate, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        NonDurableSagaDataProjection.GetSagaData(storage, context, predicate, cancellationToken);
}