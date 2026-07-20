namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

[SuppressMessage(
    "Reliability",
    "CA2213:Disposable fields should be disposed",
    Justification = "The semaphore is owned by the saga lineage and intentionally becomes unreachable when the saga and any sessions holding the lock are no longer referenced.")]
sealed class SagaLockState
{
    public Guid Identity { get; } = Guid.NewGuid();

    public bool TryAcquire(TimeSpan timeout, CancellationToken cancellationToken = default) => semaphore.Wait(timeout, cancellationToken);

    public void Release() => semaphore.Release();

    // SemaphoreSlim does not allocate an OS wait handle unless AvailableWaitHandle is used.
    readonly SemaphoreSlim semaphore = new(1, 1);
}
