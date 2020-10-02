using NServiceBus.Persistence;

namespace NServiceBus
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;

    class NonDurableTransactionalStorageFeature : Feature
    {
        /// <summary>
        /// Called when the features is activated.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<ISynchronizedStorage, NonDurableSynchronizedStorage>();
            context.Services.AddSingleton<ISynchronizedStorageAdapter, NonDurableTransactionalSynchronizedStorageAdapter>();
        }
    }
}