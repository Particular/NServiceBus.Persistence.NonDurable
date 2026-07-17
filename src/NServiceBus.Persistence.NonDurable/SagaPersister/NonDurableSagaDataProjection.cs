namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Threading;
using Extensibility;
using SagaPersister;

static class NonDurableSagaDataProjection
{
    public static TSagaData? GetSagaData<TSagaData>(
        NonDurableStorage storage,
        INonDurableSagaLockingSession session,
        IReadOnlyContextBag context,
        Func<TSagaData, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        GetSagaData<TSagaData, Func<TSagaData, bool>>(
            storage,
            session,
            context,
            predicate,
            static (sagaData, predicate) => predicate(sagaData),
            cancellationToken);

    public static TSagaData? GetSagaData<TSagaData, TState>(
        NonDurableStorage storage,
        INonDurableSagaLockingSession session,
        IReadOnlyContextBag context,
        TState state,
        Func<TSagaData, TState, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(session);
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

            var lockAcquired = session.TryAcquireSagaLock(sagaId, cancellationToken);
            try
            {
                if (!session.UsesPessimisticSagaConcurrency)
                {
                    NonDurableSagaPersister.SetEntry(contextBag, sagaId, entry);
                    return sagaData;
                }

                if (!storage.Sagas.TryGetValue(sagaId, out var liveEntry) || liveEntry.SagaDataType != typeof(TSagaData))
                {
                    if (lockAcquired)
                    {
                        session.ReleaseSagaLock(sagaId);
                    }

                    continue;
                }

                var lockedSagaData = (TSagaData)liveEntry.GetSagaCopy();
                if (!predicate(lockedSagaData, state))
                {
                    if (lockAcquired)
                    {
                        session.ReleaseSagaLock(sagaId);
                    }

                    continue;
                }

                NonDurableSagaPersister.SetEntry(contextBag, sagaId, liveEntry);
                return lockedSagaData;
            }
            catch
            {
                if (lockAcquired)
                {
                    session.ReleaseSagaLock(sagaId);
                }

                throw;
            }
        }

        return null;
    }
}