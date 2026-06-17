namespace NServiceBus;

using System;
using System.Text.Json;
using Configuration.AdvancedExtensibility;
using Persistence.NonDurable;

/// <summary>
/// Extension methods for configuring non-durable persistence.
/// </summary>
public static class NonDurablePersistenceConfigurationExtensions
{
    /// <summary>
    /// Configures the endpoint to use non-durable persistence.
    /// </summary>
    public static PersistenceExtensions<NonDurablePersistence> UseNonDurablePersistence(
        this EndpointConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.UsePersistence<NonDurablePersistence>();
    }

    /// <summary>
    /// Configures the <see cref="JsonSerializerOptions"/> used for serializing saga data.
    /// </summary>
    /// <param name="persistenceExtensions">The persistence extensions to extend.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to use.</param>
    public static void SerializerOptions(this PersistenceExtensions<NonDurablePersistence> persistenceExtensions, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(persistenceExtensions);
        ArgumentNullException.ThrowIfNull(options);

        persistenceExtensions.GetSettings().Set(Features.NonDurableSagaPersistence.SerializerOptionsKey, options);
    }

    /// <summary>
    /// Configures the <see cref="NonDurableStorage"/> runtime used by the non-durable persistence.
    /// </summary>
    public static void Storage(this PersistenceExtensions<NonDurablePersistence> persistenceExtensions, NonDurableStorage storage)
    {
        ArgumentNullException.ThrowIfNull(persistenceExtensions);
        ArgumentNullException.ThrowIfNull(storage);

        persistenceExtensions.GetSettings().Set(NonDurableStorageRuntime.StorageKey, storage);
    }
}