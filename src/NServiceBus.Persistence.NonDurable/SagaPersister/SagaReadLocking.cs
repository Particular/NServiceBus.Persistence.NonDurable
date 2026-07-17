namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Collections.Concurrent;
using System.Threading;

static class SagaReadLocking
{
    public static TSagaData? ReadCurrent<TSagaData>(
        ConcurrentDictionary<Guid, SagaEntry> sagas,
        INonDurableSagaLockingSession lockingSession,
        Guid sagaId,
        Func<SagaEntry, TSagaData?> tryRead,
        Action<Guid, SagaEntry> captureEntry,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        ArgumentNullException.ThrowIfNull(sagas);
        ArgumentNullException.ThrowIfNull(lockingSession);
        ArgumentNullException.ThrowIfNull(tryRead);
        ArgumentNullException.ThrowIfNull(captureEntry);

        var lockAcquired = lockingSession.TryAcquireSagaLock(sagaId, cancellationToken);

        try
        {
            if (sagas.TryGetValue(sagaId, out var entry) && tryRead(entry) is { } sagaData)
            {
                captureEntry(sagaId, entry);
                return sagaData;
            }
        }
        catch
        {
            if (lockAcquired)
            {
                lockingSession.ReleaseSagaLock(sagaId);
            }

            throw;
        }

        if (lockAcquired)
        {
            lockingSession.ReleaseSagaLock(sagaId);
        }

        return null;
    }
}
