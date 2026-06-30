namespace NServiceBus;

using Features;
using Microsoft.Extensions.DependencyInjection;
using Persistence;
using Persistence.NonDurable;

sealed class NonDurableTransactionalStorageFeature : Feature
{
    public NonDurableTransactionalStorageFeature() => DependsOn<SynchronizedStorage>();

    protected override void Setup(FeatureConfigurationContext context)
    {
        NonDurablePersistenceOptions persistenceOptions = context.Settings.Get<NonDurablePersistenceOptions>();
        NonDurableStorageRuntime.Configure(context.Services, persistenceOptions);

        context.Services.AddScoped<ICompletableSynchronizedStorageSession>(sp => new NonDurableSynchronizedStorageSession(sp.GetRequiredService<NonDurableStorage>()));
    }
}