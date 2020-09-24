namespace NServiceBus.Features
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Used to configure in memory timeout persistence.
    /// </summary>
    public class InMemoryTimeoutPersistence2 : Feature
    {
        internal InMemoryTimeoutPersistence2()
        {
            DependsOn<TimeoutManager>();
        }

        /// <summary>
        /// See <see cref="Feature.Setup" />.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton(_ => new InMemoryTimeoutPersister(() => DateTime.UtcNow));
        }
    }
}