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
            Supports<StorageType.Sagas>(s => s.EnableFeatureByDefault<NonDurableSagaPersistence>());
            Supports<StorageType.Subscriptions>(s => s.EnableFeatureByDefault<NonDurableSubscriptionPersistence>());
            Supports<StorageType.Outbox>(s => s.EnableFeatureByDefault<NonDurableOutboxPersistence>());
        }
    }
}