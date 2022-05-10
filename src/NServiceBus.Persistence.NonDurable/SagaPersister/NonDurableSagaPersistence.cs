namespace NServiceBus.Features
{
    using NServiceBus.Sagas;
    using Microsoft.Extensions.DependencyInjection;

    class NonDurableSagaPersistence : Feature
    {
        public NonDurableSagaPersistence()
        {
            Defaults(s => s.EnableFeatureByDefault<NonDurableTransactionalStorageFeature>());

            DependsOn<Sagas>();
            DependsOn<NonDurableTransactionalStorageFeature>();
        }

        protected override void Setup(FeatureConfigurationContext context) =>
            context.Services.AddSingleton<ISagaPersister, NonDurableSagaPersister>();
    }
}