namespace NServiceBus.Features
{
    using Microsoft.Extensions.DependencyInjection;

    class NonDurableSubscriptionPersistence : Feature
    {
        internal NonDurableSubscriptionPersistence()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            DependsOn<MessageDrivenSubscriptions>();
#pragma warning restore CS0618 // Type or member is obsolete
        }
        
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton(new NonDurableSubscriptionStorage());
        }
    }
}