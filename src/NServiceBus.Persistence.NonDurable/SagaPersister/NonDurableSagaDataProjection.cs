namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Threading;
using Extensibility;
using SagaPersister;

static class NonDurableSagaDataProjection
{
    public static TSagaData? GetSagaData<TSagaData>(
        NonDurableStorage storage,
        INonDurableSagaLockingSession lockingSession,
        IReadOnlyContextBag context,
        Func<TSagaData, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        GetSagaData<TSagaData, Func<TSagaData, bool>>(
            storage,
            lockingSession,
            context,
            predicate,
            static (sagaData, predicate) => predicate(sagaData),
            cancellationToken);

    public static TSagaData? GetSagaData<TSagaData, TState>(
        NonDurableStorage storage,
        INonDurableSagaLockingSession lockingSession,
        IReadOnlyContextBag context,
        TState state,
        Func<TSagaData, TState, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(lockingSession);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(predicate);

        if (context is not ContextBag contextBag)
        {
            throw new InvalidOperationException("The context must be a mutable ContextBag.");
        }

        foreach (var (sagaId, entry) in storage.Sagas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.SagaDataType != typeof(TSagaData))
            {
                continue;
            }

            var sagaData = (TSagaData)entry.GetSagaCopy();
            if (!predicate(sagaData, state))
            {
                continue;
            }

            if (!entry.UsesPessimisticConcurrency)
            {
                NonDurableSagaPersister.SetEntry(contextBag, sagaId, entry);
                return sagaData;
            }

            var lockedSagaData = SagaReadLocking.ReadCurrent<TSagaData>(
                storage.Sagas,
                lockingSession,
                sagaId,
                entry,
                liveEntry =>
                {
                    // First copy of the data may already be stale since the lock was acquired.
                    var currentSagaData = (TSagaData)liveEntry.GetSagaCopy();
                    return predicate(currentSagaData, state) ? currentSagaData : null;
                },
                (capturedSagaId, capturedEntry) => NonDurableSagaPersister.SetEntry(contextBag, capturedSagaId, capturedEntry),
                cancellationToken);

            if (lockedSagaData is not null)
            {
                return lockedSagaData;
            }
        }

        return null;
    }
}