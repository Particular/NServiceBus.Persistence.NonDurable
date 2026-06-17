namespace NServiceBus.Persistence.NonDurable
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Outbox;

    /// <summary>
    /// Contains NonDurableOutbox-related settings extensions.
    /// </summary>
    public static class NonDurableOutboxSettingsExtensions
    {
        /// <summary>
        /// Specifies how long the outbox should keep message data in storage to be able to deduplicate.
        /// </summary>
        /// <param name="settings">The outbox settings.</param>
        /// <param name="time">
        /// Defines the <see cref="TimeSpan"/> which indicates how long the outbox deduplication entries should be kept.
        /// For example, if <code>TimeSpan.FromDays(1)</code> is used, the deduplication entries are kept for no longer than one day.
        /// It is not possible to use a negative or zero TimeSpan value.
        /// </param>
        public static OutboxSettings TimeToKeepDeduplicationData(this OutboxSettings settings, TimeSpan time)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(time, TimeSpan.Zero);

            settings.GetSettings().Set("Outbox.TimeToKeepDeduplicationEntries", time);
            return settings;
        }

        /// <summary>
        /// Specifies the frequency at which the outbox cleanup task runs to remove deduplication entries that are older
        /// than the configured <see cref="TimeToKeepDeduplicationData"/> period.
        /// </summary>
        /// <param name="settings">The outbox settings.</param>
        /// <param name="time">
        /// Defines the <see cref="TimeSpan"/> which indicates how frequently the deduplication data cleanup task should run.
        /// For example, if <code>TimeSpan.FromMinutes(1)</code> is used, the cleanup task runs at most once per minute.
        /// It is not possible to use a negative or zero TimeSpan value. When not specified, the cleanup task runs every minute.
        /// </param>
        public static OutboxSettings FrequencyToRunDeduplicationDataCleanup(this OutboxSettings settings, TimeSpan time)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(time, TimeSpan.Zero);

            settings.GetSettings().Set("Outbox.NonDurableTimeToCheckForDuplicateEntries", time);
            return settings;
        }
    }
}