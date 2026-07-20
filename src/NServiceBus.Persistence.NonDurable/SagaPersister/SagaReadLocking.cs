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
        SagaEntry entry,
        Func<SagaEntry, TSagaData?> tryRead,
        Action<Guid, SagaEntry> captureEntry,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        ArgumentNullException.ThrowIfNull(sagas);
        ArgumentNullException.ThrowIfNull(lockingSession);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(tryRead);
        ArgumentNullException.ThrowIfNull(captureEntry);

        if (!entry.UsesPessimisticConcurrency)
        {
            if (tryRead(entry) is { } sagaData)
            {
                captureEntry(sagaId, entry);
                return sagaData;
            }

            return null;
        }

        var lockAcquired = lockingSession.TryAcquireSagaLock(sagaId, entry, cancellationToken);

        try
        {
            if (sagas.TryGetValue(sagaId, out var currentEntry)
                && entry.HasSameLockIdentity(currentEntry)
                && tryRead(currentEntry) is { } sagaData)
            {
                captureEntry(sagaId, currentEntry);
                return sagaData;
            }
        }
        catch
        {
            if (lockAcquired)
            {
                lockingSession.ReleaseSagaLock(entry);
            }

            throw;
        }

        if (lockAcquired)
        {
            lockingSession.ReleaseSagaLock(entry);
        }

        return null;
    }
}
