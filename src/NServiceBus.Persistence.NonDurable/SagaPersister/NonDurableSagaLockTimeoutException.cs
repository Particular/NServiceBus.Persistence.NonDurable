namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;

sealed class NonDurableSagaLockTimeoutException(Guid sagaId, TimeSpan timeout)
    : TimeoutException($"NonDurableSagaPersister timed out after waiting {timeout} to acquire saga entity Id {sagaId}.")
{
}
