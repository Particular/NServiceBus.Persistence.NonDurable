namespace NServiceBus
{
    using Features;
    using Persistence;

    /// <summary>
    /// Used to enable NonDurable persistence.
    /// </summary>
    public class NonDurablePersistence : PersistenceDefinition
    {
        internal NonDurablePersistence()
        {
            Supports<StorageType.Sagas>(s =>
            {
                s.EnableFeatureByDefault<NonDurableSagaPersistence>();
                s.EnableFeatureByDefault<NonDurableTransactionalStorageFeature>();
            });

            Supports<StorageType.Timeouts>(s => s.EnableFeatureByDefault<NonDurableTimeoutPersistence>());
            Supports<StorageType.Subscriptions>(s => s.EnableFeatureByDefault<NonDurableSubscriptionPersistence>());
            Supports<StorageType.Outbox>(s => { s.EnableFeatureByDefault<NonDurableOutboxPersistence>();
                s.EnableFeatureByDefault<NonDurableTransactionalStorageFeature>();
            });
        }
    }
}