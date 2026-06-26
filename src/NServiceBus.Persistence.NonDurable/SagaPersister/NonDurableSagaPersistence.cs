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
        NonDurablePersistenceOptions persistenceOptions = context.Settings.Get<NonDurablePersistenceOptions>();
        NonDurableStorageRuntime.Configure(context.Services, persistenceOptions);


        context.Services.AddSingleton(sp => new NonDurableSagaPersister(sp.GetRequiredService<NonDurableStorage>(), persistenceOptions.Saga));
        context.Services.AddSingleton<ISagaPersister>(sp => sp.GetRequiredService<NonDurableSagaPersister>());
    }
}