namespace NServiceBus.Features
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Used to configure non durable saga persistence.
    /// </summary>
    public class NonDurableSagaPersistence : Feature
    {
        internal NonDurableSagaPersistence()
        {
            DependsOn<Sagas>();
        }

        /// <summary>
        /// See <see cref="Feature.Setup" />.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<NonDurableSagaPersister>();
        }
    }
}