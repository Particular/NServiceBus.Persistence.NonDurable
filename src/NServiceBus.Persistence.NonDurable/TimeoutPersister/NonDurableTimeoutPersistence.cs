namespace NServiceBus.Features
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    class NonDurableTimeoutPersistence : Feature
    {
        internal NonDurableTimeoutPersistence()
        {
            DependsOn<TimeoutManager>();
        }
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton(_ => new NonDurableTimeoutPersister(() => DateTime.UtcNow));
        }
    }
}