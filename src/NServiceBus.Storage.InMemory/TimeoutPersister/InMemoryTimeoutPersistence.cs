namespace NServiceBus.Features
{
    using System;

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
        protected internal override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent(_ => new InMemoryTimeoutPersister(() => DateTime.UtcNow), DependencyLifecycle.SingleInstance);
        }
    }
}