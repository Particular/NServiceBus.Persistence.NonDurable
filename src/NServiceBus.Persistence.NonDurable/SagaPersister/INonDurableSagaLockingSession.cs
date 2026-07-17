namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Threading;

interface INonDurableSagaLockingSession
{
    bool UsesPessimisticSagaConcurrency { get; }

    bool TryAcquireSagaLock(Guid sagaId, CancellationToken cancellationToken = default);

    void ReleaseSagaLock(Guid sagaId);
}
