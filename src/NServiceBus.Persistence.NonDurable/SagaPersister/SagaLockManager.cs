namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Collections.Generic;
using System.Threading;

sealed class SagaLockManager
{
    public IDisposable Acquire(Guid ownerId, Guid sagaId, CancellationToken cancellationToken = default)
    {
        SagaLockState lockState;

        lock (syncRoot)
        {
            lockState = GetOrAddLockState(sagaId);

            if (lockState.OwnerId == ownerId)
            {
                lockState.ReentrancyCount++;
                return new SagaLockLease(this, ownerId, sagaId);
            }

            if (lockState.OwnerId is null)
            {
                // Non-blocking consistency check, not a real wait. If this fails, our ownership bookkeeping
                // and the semaphore state have diverged, so throw
                if (!lockState.Semaphore.Wait(0, CancellationToken.None))
                {
                    throw new InvalidOperationException($"Failed to acquire the free saga lock for Id[{sagaId}].");
                }

                lockState.OwnerId = ownerId;
                lockState.ReentrancyCount = 1;
                return new SagaLockLease(this, ownerId, sagaId);
            }

            lockState.WaiterCount++;
            waitingOwners[ownerId] = lockState.OwnerId.Value;

            if (CreatesCycle(ownerId, lockState.OwnerId.Value))
            {
                waitingOwners.Remove(ownerId);
                lockState.WaiterCount--;
                RemoveUnusedLockState(sagaId, lockState);
                throw new NonDurableSagaDeadlockException(sagaId);
            }
        }

        try
        {
            lockState.Semaphore.Wait(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (syncRoot)
            {
                waitingOwners.Remove(ownerId);
                lockState.WaiterCount--;
                RemoveUnusedLockState(sagaId, lockState);
            }

            throw;
        }
        catch
        {
            lock (syncRoot)
            {
                waitingOwners.Remove(ownerId);
                lockState.WaiterCount--;
                RemoveUnusedLockState(sagaId, lockState);
            }

            throw;
        }

        lock (syncRoot)
        {
            waitingOwners.Remove(ownerId);
            lockState.WaiterCount--;

            if (lockState.OwnerId is not null)
            {
                throw new InvalidOperationException($"The saga lock for Id[{sagaId}] was acquired while it was still owned.");
            }

            lockState.OwnerId = ownerId;
            lockState.ReentrancyCount = 1;
            return new SagaLockLease(this, ownerId, sagaId);
        }
    }

    void Release(Guid ownerId, Guid sagaId)
    {
        lock (syncRoot)
        {
            if (!lockStates.TryGetValue(sagaId, out var lockState))
            {
                throw new InvalidOperationException($"The saga lock for Id[{sagaId}] is not registered.");
            }

            if (lockState.OwnerId != ownerId)
            {
                throw new InvalidOperationException($"The saga lock for Id[{sagaId}] is owned by another session.");
            }

            lockState.ReentrancyCount--;

            if (lockState.ReentrancyCount == 0)
            {
                lockState.OwnerId = null;
                lockState.Semaphore.Release();
                RemoveUnusedLockState(sagaId, lockState);
            }
        }
    }

    SagaLockState GetOrAddLockState(Guid sagaId)
    {
        if (lockStates.TryGetValue(sagaId, out var existing))
        {
            return existing;
        }

        var created = new SagaLockState();
        lockStates.Add(sagaId, created);
        return created;
    }

    bool CreatesCycle(Guid waitingOwnerId, Guid blockingOwnerId)
    {
        var visited = new HashSet<Guid> { waitingOwnerId };
        var currentOwnerId = blockingOwnerId;

        while (true)
        {
            if (!visited.Add(currentOwnerId))
            {
                return currentOwnerId == waitingOwnerId;
            }

            if (!waitingOwners.TryGetValue(currentOwnerId, out var nextOwnerId))
            {
                return false;
            }

            currentOwnerId = nextOwnerId;
        }
    }

    void RemoveUnusedLockState(Guid sagaId, SagaLockState lockState)
    {
        if (lockState.OwnerId is null && lockState.ReentrancyCount == 0 && lockState.WaiterCount == 0)
        {
            lockStates.Remove(sagaId);
        }
    }

    readonly object syncRoot = new();
    // Regular dictionaries simpler / cheaper as they're only accessed under a lock on syncRoot
    readonly Dictionary<Guid, SagaLockState> lockStates = [];
    readonly Dictionary<Guid, Guid> waitingOwners = [];

    sealed class SagaLockLease(SagaLockManager manager, Guid ownerId, Guid sagaId) : IDisposable
    {
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            manager.Release(ownerId, sagaId);
        }

        int disposed;
    }

    sealed class SagaLockState
    {
        public Guid? OwnerId { get; set; }

        public int ReentrancyCount { get; set; }

        public int WaiterCount { get; set; }

        public SemaphoreSlim Semaphore { get; } = new(1, 1);
    }
}
