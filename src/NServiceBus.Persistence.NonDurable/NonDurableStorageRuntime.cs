namespace NServiceBus.Persistence.NonDurable;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

static class NonDurableStorageRuntime
{
    public static NonDurableStorage SharedStorage { get; } = new();

    public static void Configure(IServiceCollection services, NonDurablePersistenceOptions persistenceOptions)
    {
        ArgumentNullException.ThrowIfNull(persistenceOptions, nameof(persistenceOptions));

        var storage = persistenceOptions.Storage
            ?? (persistenceOptions.TimeProvider != TimeProvider.System
                ? new NonDurableStorage(new NonDurableStorageOptions { TimeProvider = persistenceOptions.TimeProvider })
                : null)
            ?? SharedStorage;
        services.TryAddSingleton(storage);
    }
}