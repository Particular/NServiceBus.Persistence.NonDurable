namespace NServiceBus.Features
{
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Microsoft.Extensions.DependencyInjection;

    sealed class NonDurableSubscriptionPersistence : Feature
    {
        public NonDurableSubscriptionPersistence() => DependsOn("NServiceBus.Features.MessageDrivenSubscriptions");

        protected override void Setup(FeatureConfigurationContext context) => context.Services.AddSingleton<ISubscriptionStorage, NonDurableSubscriptionStorage>();
    }
}