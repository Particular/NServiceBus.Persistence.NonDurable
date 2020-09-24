namespace NServiceBus
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;

    class InMemoryTransactionalStorageFeature2 : Feature
    {
        /// <summary>
        /// Called when the features is activated.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<InMemorySynchronizedStorage2>();
            context.Services.AddSingleton<InMemoryTransactionalSynchronizedStorageAdapter2>();
        }
    }
}