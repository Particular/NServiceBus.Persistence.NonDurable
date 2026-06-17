namespace NServiceBus.Persistence.NonDurable;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

static class NonDurableStorageRuntime
{
    public static NonDurableStorage SharedStorage { get; } = new();

    public static void Configure(IServiceCollection services, NonDurablePersistenceOptions? persistenceOptions = null)
    {
        var storage = persistenceOptions?.Storage
            ?? (persistenceOptions?.TimeProvider is not null
                ? new NonDurableStorage(new NonDurableStorageOptions { TimeProvider = persistenceOptions.TimeProvider })
                : null)
            ?? SharedStorage;

        services.TryAddSingleton(storage);
    }
}