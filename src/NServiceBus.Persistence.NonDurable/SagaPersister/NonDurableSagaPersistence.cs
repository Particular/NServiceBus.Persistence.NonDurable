namespace NServiceBus.Features;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Persistence.NonDurable;
using NServiceBus.Sagas;

sealed class NonDurableSagaPersistence : Feature
{
    public NonDurableSagaPersistence()
    {
        DependsOn<Sagas>();
        DependsOn<NonDurableTransactionalStorageFeature>();

        Enable<NonDurableTransactionalStorageFeature>();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var persistenceOptions = context.Settings.GetOrDefault<NonDurablePersistenceOptions>();
        NonDurableStorageRuntime.Configure(context.Services, persistenceOptions);

        var serializerOptions = persistenceOptions?.Saga?.JsonSerializerOptions
            ?? new System.Text.Json.JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

        context.Services.AddSingleton(new NonDurableSagaPersisterSettings(serializerOptions));
        context.Services.AddSingleton(sp =>
            new NonDurableSagaPersister(
                sp.GetRequiredService<NonDurableStorage>(),
                sp.GetRequiredService<NonDurableSagaPersisterSettings>()));
        context.Services.AddSingleton<ISagaPersister>(sp => sp.GetRequiredService<NonDurableSagaPersister>());
    }
}