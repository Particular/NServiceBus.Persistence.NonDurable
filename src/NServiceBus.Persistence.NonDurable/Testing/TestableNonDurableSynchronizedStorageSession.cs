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
/// Initializes a new instance of <see cref="TestableNonDurableSynchronizedStorageSession" /> using the specified storage instance and saga options.
/// </remarks>
public class TestableNonDurableSynchronizedStorageSession(NonDurableStorage storage, NonDurableSagaOptions sagaOptions) : ISynchronizedStorageSession, INonDurableStorageSession
{
    /// <summary>
    /// Initializes a new instance of <see cref="TestableNonDurableSynchronizedStorageSession" /> using a new <see cref="NonDurableStorage" /> instance and default saga options.
    /// </summary>
    public TestableNonDurableSynchronizedStorageSession() : this(new NonDurableStorage(), new NonDurableSagaOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TestableNonDurableSynchronizedStorageSession" /> using a new <see cref="NonDurableStorage" /> instance and the specified saga options.
    /// </summary>
    public TestableNonDurableSynchronizedStorageSession(NonDurableSagaOptions sagaOptions) : this(new NonDurableStorage(), sagaOptions)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TestableNonDurableSynchronizedStorageSession" /> using the specified storage instance and default saga options.
    /// </summary>
    public TestableNonDurableSynchronizedStorageSession(NonDurableStorage storage) : this(storage, new NonDurableSagaOptions())
    {
    }

    /// <summary>
    /// Adds saga data to the test session storage.
    /// </summary>
    public void AddSaga(IContainSagaData sagaData)
    {
        ArgumentNullException.ThrowIfNull(sagaData);

        var noCorrelationId = new CorrelationId(typeof(object), string.Empty, new object());
        storage.Sagas[sagaData.Id] = new SagaEntry(sagaData, noCorrelationId, version: 1, sagaOptions.JsonSerializerOptions);
    }

    /// <inheritdoc />
    public TSagaData? GetSagaData<TSagaData>(IReadOnlyContextBag context, Func<TSagaData, bool> predicate, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        NonDurableSagaDataProjection.GetSagaData(storage, context, predicate, cancellationToken);

    /// <inheritdoc />
    public TSagaData? GetSagaData<TSagaData, TState>(IReadOnlyContextBag context, TState state, Func<TSagaData, TState, bool> predicate, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        NonDurableSagaDataProjection.GetSagaData(storage, context, state, predicate, cancellationToken);

    readonly NonDurableStorage storage = storage ?? throw new ArgumentNullException(nameof(storage));
    readonly NonDurableSagaOptions sagaOptions = sagaOptions ?? throw new ArgumentNullException(nameof(sagaOptions));
}