using NServiceBus.Sagas;

namespace NServiceBus.Features
{
    using Microsoft.Extensions.DependencyInjection;

    class NonDurableSagaPersistence : Feature
    {
        internal NonDurableSagaPersistence()
        {
            DependsOn<Sagas>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<ISagaPersister, NonDurableSagaPersister>();
        }
    }
}