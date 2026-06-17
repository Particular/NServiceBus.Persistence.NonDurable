namespace NServiceBus.Features;

using System;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Outbox;
using NServiceBus.Persistence.NonDurable;

sealed class NonDurableOutboxPersistence : Feature
{
    public NonDurableOutboxPersistence()
    {
        DependsOn<Outbox>();
        DependsOn<NonDurableTransactionalStorageFeature>();

        Enable<NonDurableTransactionalStorageFeature>();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var configuredStorage = context.Settings.GetOrDefault<NonDurableStorage>(NonDurableStorageRuntime.StorageKey);
        NonDurableStorageRuntime.Configure(context.Services, configuredStorage);

        var endpointName = context.Settings.EndpointName();
        context.Services.AddSingleton(sp => new NonDurableOutboxStorage(endpointName, sp.GetRequiredService<NonDurableStorage>()));
        context.Services.AddSingleton<IOutboxStorage>(sp => sp.GetRequiredService<NonDurableOutboxStorage>());

        var timeToKeepDeduplicationEntries = context.Settings.Get<TimeSpan>("Outbox.TimeToKeepDeduplicationEntries");
        context.RegisterStartupTask(sp => new OutboxCleaner(sp.GetRequiredService<NonDurableOutboxStorage>(), timeToKeepDeduplicationEntries));
    }
}