namespace NServiceBus.Testing;

using System;
using System.Collections.Generic;
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
public class TestableNonDurableSynchronizedStorageSession(NonDurableStorage storage, NonDurableSagaOptions sagaOptions) : ISynchronizedStorageSession, INonDurableStorageSession, INonDurableSagaLockingSession
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
    public TestableNonDurableSynchronizedStorageSession(NonDurableSagaOptions sagaOptions) : this(CreateStorage(sagaOptions), sagaOptions)
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
        NonDurableSagaDataProjection.GetSagaData(storage, this, context, predicate, cancellationToken);

    /// <inheritdoc />
    public TSagaData? GetSagaData<TSagaData, TState>(IReadOnlyContextBag context, TState state, Func<TSagaData, TState, bool> predicate, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        NonDurableSagaDataProjection.GetSagaData(storage, this, context, state, predicate, cancellationToken);

    bool INonDurableSagaLockingSession.UsesPessimisticSagaConcurrency => storage.SagaConcurrencyMode == NonDurableSagaConcurrencyMode.Pessimistic;

    bool INonDurableSagaLockingSession.TryAcquireSagaLock(Guid sagaId, CancellationToken cancellationToken)
    {
        if (storage.SagaConcurrencyMode != NonDurableSagaConcurrencyMode.Pessimistic || acquiredSagaLocks.ContainsKey(sagaId))
        {
            return false;
        }

        var lockLease = storage.SagaLocks.Acquire(lockOwnerId, sagaId, cancellationToken);
        acquiredSagaLocks.Add(sagaId, lockLease);
        return true;
    }

    void INonDurableSagaLockingSession.ReleaseSagaLock(Guid sagaId)
    {
        if (acquiredSagaLocks.Remove(sagaId, out var lockLease))
        {
            lockLease.Dispose();
        }
    }

    readonly NonDurableStorage storage = storage ?? throw new ArgumentNullException(nameof(storage));
    readonly NonDurableSagaOptions sagaOptions = sagaOptions ?? throw new ArgumentNullException(nameof(sagaOptions));
    readonly Dictionary<Guid, IDisposable> acquiredSagaLocks = [];
    readonly Guid lockOwnerId = Guid.NewGuid();

    static NonDurableStorage CreateStorage(NonDurableSagaOptions sagaOptions)
    {
        ArgumentNullException.ThrowIfNull(sagaOptions);

        return new NonDurableStorage(new NonDurableStorageOptions
        {
            SagaConcurrencyMode = sagaOptions.ConcurrencyMode
        });
    }
}