namespace NServiceBus
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Features;

    class NonDurableTransactionalStorageFeature : Feature
    {
        public NonDurableTransactionalStorageFeature()
        {
            DependsOn<SynchronizedStorage>();
        }
        protected override void Setup(FeatureConfigurationContext context) =>
            context.Services.AddScoped<ICompletableSynchronizedStorageSession, NonDurableSynchronizedStorageSession>();
    }
}