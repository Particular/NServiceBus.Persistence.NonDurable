namespace NServiceBus;

using Persistence;

/// <summary>
/// Used to enable NonDurable persistence.
/// </summary>
public class NonDurablePersistence : PersistenceDefinition, IPersistenceDefinitionFactory<NonDurablePersistence>
{
    NonDurablePersistence()
    {
        Defaults(s => s.SetDefault(new NonDurablePersistenceOptions()));
        Supports<StorageType.Sagas, Features.NonDurableSagaPersistence>();
        Supports<StorageType.Subscriptions, Features.NonDurableSubscriptionPersistence>();
        Supports<StorageType.Outbox, Features.NonDurableOutboxPersistence>();
    }

    static NonDurablePersistence IPersistenceDefinitionFactory<NonDurablePersistence>.Create() => new();
}