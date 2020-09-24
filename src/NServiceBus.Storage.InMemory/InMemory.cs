namespace NServiceBus
{
    using Features;
    using Persistence;

    /// <summary>
    /// Used to enable InMemory persistence.
    /// </summary>
    public class InMemoryPersistence : PersistenceDefinition
    {
        internal InMemoryPersistence()
        {
            Supports<StorageType.Sagas>(s =>
            {
                s.EnableFeatureByDefault<InMemorySagaPersistence2>();
                s.EnableFeatureByDefault<InMemoryTransactionalStorageFeature2>();
            });

            Supports<StorageType.Timeouts>(s => s.EnableFeatureByDefault<InMemoryTimeoutPersistence>());
            Supports<StorageType.Subscriptions>(s => s.EnableFeatureByDefault<InMemorySubscriptionPersistence2>());
            Supports<StorageType.Outbox>(s => { s.EnableFeatureByDefault<InMemoryOutboxPersistence2>();
                s.EnableFeatureByDefault<InMemoryTransactionalStorageFeature2>();

            });
        }
    }
}