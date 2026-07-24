namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

static class SagaReadLocking
{
    public static async ValueTask<TSagaData?> ReadCurrent<TSagaData>(
        ConcurrentDictionary<Guid, SagaEntry> sagas,
        INonDurableSagaLockingSession lockingSession,
        Func<SagaReadCandidate?> resolveCandidate,
        Func<SagaEntry, TSagaData?> tryRead,
        Action<Guid, SagaEntry> captureEntry,
        bool retryOnReadMiss = false,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        ArgumentNullException.ThrowIfNull(sagas);
        ArgumentNullException.ThrowIfNull(lockingSession);
        ArgumentNullException.ThrowIfNull(resolveCandidate);
        ArgumentNullException.ThrowIfNull(tryRead);
        ArgumentNullException.ThrowIfNull(captureEntry);

        var startedAt = Stopwatch.GetTimestamp();

        while (resolveCandidate() is { } candidate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = candidate.Entry;

            if (!entry.UsesPessimisticConcurrency)
            {
                if (!entry.IsCompletionPending && tryRead(entry) is { } optimisticSagaData)
                {
                    captureEntry(candidate.SagaId, entry);
                    return optimisticSagaData;
                }

                return null;
            }

            var remaining = lockingSession.PessimisticLockTimeout - Stopwatch.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                throw new NonDurableSagaLockTimeoutException(candidate.SagaId, lockingSession.PessimisticLockTimeout);
            }

            var lockAcquired = await lockingSession.TryAcquireSagaLock(candidate.SagaId, entry, remaining, cancellationToken).ConfigureAwait(false);
            var retainLock = false;

            try
            {
                if (sagas.TryGetValue(candidate.SagaId, out var currentEntry)
                    && entry.HasSameLockIdentity(currentEntry)
                    && !currentEntry.IsCompletionPending)
                {
                    if (tryRead(currentEntry) is { } sagaData)
                    {
                        captureEntry(candidate.SagaId, currentEntry);
                        retainLock = true;
                        return sagaData;
                    }

                    if (!retryOnReadMiss)
                    {
                        return null;
                    }
                }
            }
            finally
            {
                if (lockAcquired && !retainLock)
                {
                    lockingSession.ReleaseSagaLock(entry);
                }
            }
        }

        return null;
    }

    public readonly record struct SagaReadCandidate(Guid SagaId, SagaEntry Entry);
}