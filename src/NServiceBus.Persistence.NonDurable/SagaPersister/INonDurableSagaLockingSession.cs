namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Threading;
using System.Threading.Tasks;

interface INonDurableSagaLockingSession
{
    TimeSpan PessimisticLockTimeout { get; }

    ValueTask<bool> TryAcquireSagaLock(Guid sagaId, SagaEntry entry, TimeSpan timeout, CancellationToken cancellationToken = default);

    void ReleaseSagaLock(SagaEntry entry);
}