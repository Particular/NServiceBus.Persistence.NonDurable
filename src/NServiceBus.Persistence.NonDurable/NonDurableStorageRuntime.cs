namespace NServiceBus.Persistence.NonDurable;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

static class NonDurableStorageRuntime
{
    public static NonDurableStorage SharedOptimisticStorage { get; } = new();
    public static NonDurableStorage SharedPessimisticStorage { get; } = new(new NonDurableStorageOptions
    {
        SagaConcurrencyMode = NonDurableSagaConcurrencyMode.Pessimistic
    });

    public static void Configure(IServiceCollection services, NonDurablePersistenceOptions persistenceOptions)
    {
        ArgumentNullException.ThrowIfNull(persistenceOptions, nameof(persistenceOptions));

        var storage = persistenceOptions.Storage
            ?? (persistenceOptions.TimeProvider != TimeProvider.System
                ? new NonDurableStorage(new NonDurableStorageOptions
                {
                    TimeProvider = persistenceOptions.TimeProvider,
                    SagaConcurrencyMode = persistenceOptions.Saga.ConcurrencyMode
                })
                : null)
            ?? GetSharedStorage(persistenceOptions.Saga.ConcurrencyMode);
        services.TryAddSingleton(storage);
    }

    static NonDurableStorage GetSharedStorage(NonDurableSagaConcurrencyMode concurrencyMode) =>
        concurrencyMode switch
        {
            NonDurableSagaConcurrencyMode.Optimistic => SharedOptimisticStorage,
            NonDurableSagaConcurrencyMode.Pessimistic => SharedPessimisticStorage,
            _ => throw new ArgumentOutOfRangeException(nameof(concurrencyMode), concurrencyMode, "Unsupported saga concurrency mode.")
        };
}