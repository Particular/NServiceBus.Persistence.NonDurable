namespace NServiceBus.Features
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Outbox;

    class NonDurableOutboxPersistence : Feature
    {
        public NonDurableOutboxPersistence()
        {
            DependsOn<Outbox>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var outboxStorage = new NonDurableOutboxStorage();
            context.Services.AddSingleton(typeof(IOutboxStorage), outboxStorage);

            var timeSpan = context.Settings.Get<TimeSpan>(TimeToKeepDeduplicationEntries);

            context.RegisterStartupTask(new OutboxCleaner(outboxStorage, timeSpan));
        }

        public const string TimeToKeepDeduplicationEntries = "Outbox.TimeToKeepDeduplicationEntries";

        class OutboxCleaner : FeatureStartupTask
        {
            public OutboxCleaner(NonDurableOutboxStorage storage, TimeSpan timeToKeepDeduplicationData)
            {
                this.timeToKeepDeduplicationData = timeToKeepDeduplicationData;
                nonDurableOutboxStorage = storage;
            }

            protected override Task OnStart(IMessageSession session)
            {
                cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session)
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    cleanupTimer.Dispose(waitHandle);

                    // TODO: Use async synchronization primitive
                    waitHandle.WaitOne();
                }
                return Task.CompletedTask;
            }

            void PerformCleanup(object state)
            {
                nonDurableOutboxStorage.RemoveEntriesOlderThan(DateTime.UtcNow - timeToKeepDeduplicationData);
            }

            readonly NonDurableOutboxStorage nonDurableOutboxStorage;
            readonly TimeSpan timeToKeepDeduplicationData;

            Timer cleanupTimer;
        }
    }
}