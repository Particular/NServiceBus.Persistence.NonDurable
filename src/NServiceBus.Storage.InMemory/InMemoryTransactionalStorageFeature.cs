namespace NServiceBus
{
    using Features;

    class InMemoryTransactionalStorageFeature2 : Feature
    {
        /// <summary>
        /// Called when the features is activated.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<InMemorySynchronizedStorage2>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<InMemoryTransactionalSynchronizedStorageAdapter>(DependencyLifecycle.SingleInstance);
        }
    }
}