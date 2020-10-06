namespace NServiceBus
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Features;

    class NonDurableTransactionalStorageFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<ISynchronizedStorage, NonDurableSynchronizedStorage>();
            context.Services.AddSingleton<ISynchronizedStorageAdapter, NonDurableTransactionalSynchronizedStorageAdapter>();
        }
    }
}