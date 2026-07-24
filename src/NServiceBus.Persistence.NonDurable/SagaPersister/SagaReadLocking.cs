namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

static class SagaReadLocking
{
    public static async ValueTask<TSagaData?> ReadCurrent<TSagaData, TState>(
        ConcurrentDictionary<Guid, SagaEntry> sagas,
        INonDurableSagaLockingSession lockingSession,
        TState state,
        Func<TState, SagaReadCandidate?> resolveCandidate,
        Func<SagaEntry, TState, TSagaData?> tryRead,
        Action<Guid, SagaEntry, TState> captureEntry,
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

        while (resolveCandidate(state) is { } candidate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = candidate.Entry;

            if (!entry.UsesPessimisticConcurrency)
            {
                if (!entry.IsCompletionPending && tryRead(entry, state) is { } optimisticSagaData)
                {
                    captureEntry(candidate.SagaId, entry, state);
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
                    if (tryRead(currentEntry, state) is { } sagaData)
                    {
                        captureEntry(candidate.SagaId, currentEntry, state);
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