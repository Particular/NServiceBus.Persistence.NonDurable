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
            Enable<NonDurableTransactionalStorageFeature>();

            DependsOn<Outbox>();
            DependsOn<NonDurableTransactionalStorageFeature>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var outboxStorage = new NonDurableOutboxStorage();
            context.Services.AddSingleton(typeof(IOutboxStorage), outboxStorage);

            var deduplicationPeriod = context.Settings.Get<TimeSpan>(TimeToKeepDeduplicationEntries);
            if (!context.Settings.TryGet(IntervalToCheckForDuplicateEntries, out TimeSpan cleanupInterval))
            {
                cleanupInterval = TimeSpan.FromMinutes(1);
            }

            context.RegisterStartupTask(new OutboxCleaner(outboxStorage, deduplicationPeriod, cleanupInterval));
        }

        public const string TimeToKeepDeduplicationEntries = "Outbox.TimeToKeepDeduplicationEntries";
        public const string IntervalToCheckForDuplicateEntries = "Outbox.NonDurableTimeToCheckForDuplicateEntries";

        class OutboxCleaner : FeatureStartupTask
        {
            public OutboxCleaner(NonDurableOutboxStorage storage, TimeSpan timeToKeepDeduplicationData, TimeSpan intervalToCheckForDuplicates)
            {
                this.timeToKeepDeduplicationData = timeToKeepDeduplicationData;
                this.intervalToCheckForDuplicates = intervalToCheckForDuplicates;
                nonDurableOutboxStorage = storage;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                cleanupTimer = new Timer(PerformCleanup, null, intervalToCheckForDuplicates, intervalToCheckForDuplicates);
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
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
            readonly TimeSpan intervalToCheckForDuplicates;

            Timer cleanupTimer;
        }
    }
}