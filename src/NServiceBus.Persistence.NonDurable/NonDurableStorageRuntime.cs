namespace NServiceBus.Persistence.NonDurable;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

static class NonDurableStorageRuntime
{
    public static NonDurableStorage SharedStorage { get; } = new();

    public static void Configure(IServiceCollection services, NonDurablePersistenceOptions persistenceOptions)
    {
        var storage = persistenceOptions.Storage ?? SharedStorage;
        services.TryAddSingleton(storage);
    }
}