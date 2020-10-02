using NServiceBus.Timeout.Core;

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
            var nonDurableTimeoutPersister = new NonDurableTimeoutPersister(() => DateTime.UtcNow);
            
            context.Services.AddSingleton<IQueryTimeouts>(nonDurableTimeoutPersister);
            context.Services.AddSingleton<IPersistTimeouts>(nonDurableTimeoutPersister);
        }
    }
}