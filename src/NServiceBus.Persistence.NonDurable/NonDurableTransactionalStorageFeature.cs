﻿namespace NServiceBus
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Features;

    class NonDurableTransactionalStorageFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddScoped<ICompletableSynchronizedStorageSession, NonDurableSynchronizedStorageSession>();
        }
    }
}