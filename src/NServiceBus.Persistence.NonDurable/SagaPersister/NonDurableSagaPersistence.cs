namespace NServiceBus.Features
{
    using NServiceBus.Sagas;
    using Microsoft.Extensions.DependencyInjection;

    class NonDurableSagaPersistence : Feature
    {
        public NonDurableSagaPersistence()
        {
            DependsOn<Sagas>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<ISagaPersister, NonDurableSagaPersister>();
        }
    }
}