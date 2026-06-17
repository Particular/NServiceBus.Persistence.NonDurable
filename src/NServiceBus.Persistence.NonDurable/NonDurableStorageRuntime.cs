namespace NServiceBus.Persistence.NonDurable;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

static class NonDurableStorageRuntime
{
    public const string StorageKey = "NonDurablePersistence.Storage";

    public static NonDurableStorage SharedStorage { get; } = new();

    public static void Configure(IServiceCollection services, NonDurableStorage? configuredStorage)
        => services.TryAddSingleton(configuredStorage ?? SharedStorage);
}