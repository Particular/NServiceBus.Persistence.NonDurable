namespace NServiceBus.Features
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Used to configure non durable timeout persistence.
    /// </summary>
    public class NonDurableTimeoutPersistence : Feature
    {
        internal NonDurableTimeoutPersistence()
        {
            DependsOn<TimeoutManager>();
        }

        /// <summary>
        /// See <see cref="Feature.Setup" />.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton(_ => new NonDurableTimeoutPersister(() => DateTime.UtcNow));
        }
    }
}