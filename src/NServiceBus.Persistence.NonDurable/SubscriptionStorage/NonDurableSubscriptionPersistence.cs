namespace NServiceBus.Features;

using Microsoft.Extensions.DependencyInjection;
using Persistence.NonDurable;
using Persistence.NonDurable.SubscriptionStorage;
using Unicast.Subscriptions.MessageDrivenSubscriptions;

sealed class NonDurableSubscriptionPersistence : Feature
{
    public NonDurableSubscriptionPersistence() => DependsOn("NServiceBus.Features.MessageDrivenSubscriptions");

    protected override void Setup(FeatureConfigurationContext context)
    {
        var configuredStorage = context.Settings.GetOrDefault<NonDurableStorage>(NonDurableStorageRuntime.StorageKey);
        NonDurableStorageRuntime.Configure(context.Services, configuredStorage);
        context.Services.AddSingleton(sp => new NonDurableSubscriptionStorage(sp.GetRequiredService<NonDurableStorage>()));
        context.Services.AddSingleton<ISubscriptionStorage>(sp => sp.GetRequiredService<NonDurableSubscriptionStorage>());
    }
}