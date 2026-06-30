namespace NServiceBus;

using System;
using Persistence;
using Persistence.NonDurable;

/// <summary>
/// NonDurable persistence specific extension methods for <see cref="ISynchronizedStorageSession" />.
/// </summary>
public static class NonDurableSynchronizedStorageSessionExtensions
{
    /// <summary>
    /// Retrieves the NonDurable persistence session from the synchronized storage session.
    /// </summary>
    public static INonDurableStorageSession NonDurablePersistenceSession(this ISynchronizedStorageSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session is INonDurableStorageSession nonDurableSession)
        {
            return nonDurableSession;
        }

        throw new Exception($"Cannot access the synchronized storage session. Ensure that 'EndpointConfiguration.UsePersistence<{nameof(NonDurablePersistence)}>()' has been called.");
    }
}
