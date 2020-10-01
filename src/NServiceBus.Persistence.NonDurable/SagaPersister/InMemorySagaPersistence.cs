namespace NServiceBus.Features
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Used to configure in memory saga persistence.
    /// </summary>
    public class InMemorySagaPersistence2 : Feature
    {
        internal InMemorySagaPersistence2()
        {
            DependsOn<Sagas>();
        }

        /// <summary>
        /// See <see cref="Feature.Setup" />.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<InMemorySagaPersister>();
        }
    }
}