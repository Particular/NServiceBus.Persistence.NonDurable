namespace NServiceBus;

using System;
using System.Text.Json;
using Configuration.AdvancedExtensibility;

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
    /// Configures the endpoint to use non-durable persistence with the specified storage options.
    /// </summary>
    /// <param name="configuration">The endpoint configuration.</param>
    /// <param name="options">The persistence options for configuring the non-durable persistence.</param>
    public static PersistenceExtensions<NonDurablePersistence> UseNonDurablePersistence(
        this EndpointConfiguration configuration, NonDurablePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        var persistenceExtensions = configuration.UsePersistence<NonDurablePersistence>();
        persistenceExtensions.GetSettings().Set(options);
        return persistenceExtensions;
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
}