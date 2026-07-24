namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

sealed class SagaLockingSessionState(TimeSpan pessimisticLockTimeout) : INonDurableSagaLockingSession
{
    public TimeSpan PessimisticLockTimeout { get; } = pessimisticLockTimeout > TimeSpan.Zero
        ? pessimisticLockTimeout
        : throw new ArgumentOutOfRangeException(nameof(pessimisticLockTimeout), pessimisticLockTimeout, "Pessimistic lock timeout must be greater than zero.");

    public async ValueTask<bool> TryAcquireSagaLock(Guid sagaId, SagaEntry entry, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        SagaLockState? lockState;
        lock (acquiredSagaLocks)
        {
            ObjectDisposedException.ThrowIf(sagaLocksReleased, this);

            if (!entry.TryGetLockState(out lockState) || acquiredSagaLocks.ContainsKey(lockState.Identity))
            {
                return false;
            }
        }

        if (!await lockState.TryAcquire(timeout, cancellationToken).ConfigureAwait(false))
        {
            throw new NonDurableSagaLockTimeoutException(sagaId, PessimisticLockTimeout);
        }

        lock (acquiredSagaLocks)
        {
            if (sagaLocksReleased)
            {
                lockState.Release();
                ObjectDisposedException.ThrowIf(true, this);
            }

            acquiredSagaLocks.Add(lockState.Identity, new SagaLockLease(lockState));
        }

        return true;
    }

    public void ReleaseSagaLock(SagaEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        IDisposable? lockLease = null;
        lock (acquiredSagaLocks)
        {
            if (entry.TryGetLockState(out var lockState))
            {
                acquiredSagaLocks.Remove(lockState.Identity, out lockLease);
            }
        }

        lockLease?.Dispose();
    }

    public void ReleaseAllSagaLocks()
    {
        IDisposable[] lockLeases;
        lock (acquiredSagaLocks)
        {
            if (sagaLocksReleased)
            {
                return;
            }

            sagaLocksReleased = true;
            lockLeases = [.. acquiredSagaLocks.Values];
            acquiredSagaLocks.Clear();
        }

        foreach (var lockLease in lockLeases)
        {
            lockLease.Dispose();
        }
    }

    bool sagaLocksReleased;
    readonly Dictionary<Guid, IDisposable> acquiredSagaLocks = [];

    sealed class SagaLockLease(SagaLockState lockState) : IDisposable
    {
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            lockState.Release();
        }

        int disposed;
    }
}