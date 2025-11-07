namespace NServiceBus.Features
{
    using NServiceBus.Sagas;
    using Microsoft.Extensions.DependencyInjection;

    sealed class NonDurableSagaPersistence : Feature
    {
        public NonDurableSagaPersistence()
        {
            Enable<NonDurableTransactionalStorageFeature>();

            DependsOn<Sagas>();
            DependsOn<NonDurableTransactionalStorageFeature>();
        }

        protected override void Setup(FeatureConfigurationContext context) =>
            context.Services.AddSingleton<ISagaPersister, NonDurableSagaPersister>();
    }
}