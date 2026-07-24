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

        if (!entry.TryGetLockState(out var lockState) || acquiredSagaLocks.ContainsKey(lockState.Identity))
        {
            return false;
        }

        if (!await lockState.TryAcquire(timeout, cancellationToken).ConfigureAwait(false))
        {
            throw new NonDurableSagaLockTimeoutException(sagaId, PessimisticLockTimeout);
        }

        acquiredSagaLocks.Add(lockState.Identity, new SagaLockLease(lockState));
        return true;
    }

    public void ReleaseSagaLock(SagaEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.TryGetLockState(out var lockState) && acquiredSagaLocks.Remove(lockState.Identity, out var lockLease))
        {
            lockLease.Dispose();
        }
    }

    public void ReleaseAllSagaLocks()
    {
        if (sagaLocksReleased)
        {
            return;
        }

        foreach (var lockLease in acquiredSagaLocks.Values)
        {
            lockLease.Dispose();
        }

        acquiredSagaLocks.Clear();
        sagaLocksReleased = true;
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