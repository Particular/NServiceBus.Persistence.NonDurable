namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;

sealed class NonDurableSagaDeadlockException(Guid sagaId)
    : Exception($"NonDurableSagaPersister deadlock detected while acquiring saga entity Id {sagaId}.")
{
}
