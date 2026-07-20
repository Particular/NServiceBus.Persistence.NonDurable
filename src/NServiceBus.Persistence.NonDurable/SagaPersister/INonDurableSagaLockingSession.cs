namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Threading;

interface INonDurableSagaLockingSession
{
    bool TryAcquireSagaLock(Guid sagaId, SagaEntry entry, CancellationToken cancellationToken = default);

    void ReleaseSagaLock(SagaEntry entry);
}
