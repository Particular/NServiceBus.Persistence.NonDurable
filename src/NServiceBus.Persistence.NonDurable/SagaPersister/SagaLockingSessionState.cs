namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Collections.Generic;
using System.Threading;

sealed class SagaLockingSessionState(NonDurableStorage storage) : INonDurableSagaLockingSession
{
    public bool UsesPessimisticSagaConcurrency => storage.SagaConcurrencyMode == NonDurableSagaConcurrencyMode.Pessimistic;

    public bool TryAcquireSagaLock(Guid sagaId, CancellationToken cancellationToken = default)
    {
        if (storage.SagaConcurrencyMode != NonDurableSagaConcurrencyMode.Pessimistic || acquiredSagaLocks.ContainsKey(sagaId))
        {
            return false;
        }

        var lockLease = storage.SagaLocks.Acquire(lockOwnerId, sagaId, cancellationToken);
        acquiredSagaLocks.Add(sagaId, lockLease);
        return true;
    }

    public void ReleaseSagaLock(Guid sagaId)
    {
        if (acquiredSagaLocks.Remove(sagaId, out var lockLease))
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
    // Session-local, only used by a single message flow.
    readonly Dictionary<Guid, IDisposable> acquiredSagaLocks = [];
    readonly Guid lockOwnerId = Guid.NewGuid();
}
