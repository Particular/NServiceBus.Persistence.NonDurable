namespace NServiceBus
{
    using Features;
    using Persistence;

    /// <summary>
    /// Used to enable NonDurable persistence.
    /// </summary>
    public class NonDurablePersistence : PersistenceDefinition, IPersistenceDefinitionFactory<NonDurablePersistence>
    {
        NonDurablePersistence()
        {
            Supports<StorageType.Sagas, NonDurableSagaPersistence>();
            Supports<StorageType.Subscriptions, NonDurableSubscriptionPersistence>();
            Supports<StorageType.Outbox, NonDurableOutboxPersistence>();
        }

        static NonDurablePersistence IPersistenceDefinitionFactory<NonDurablePersistence>.Create() => new();
    }
}