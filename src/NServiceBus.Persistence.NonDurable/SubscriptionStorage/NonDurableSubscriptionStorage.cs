namespace NServiceBus.Persistence.NonDurable.SubscriptionStorage;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class NonDurableSubscriptionStorage(NonDurableStorage storage) : ISubscriptionStorage
{
    public NonDurableSubscriptionStorage() : this(new NonDurableStorage())
    {
    }

    public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default)
    {
        using var activity = NonDurablePersistenceTracing.StartSubscriptionSubscribe(subscriber, messageType);
        var subscribers = storage.GetOrAdd(messageType, static _ => new ConcurrentDictionary<string, Subscriber>(StringComparer.OrdinalIgnoreCase));
        subscribers[subscriber.TransportAddress] = subscriber;
        NonDurablePersistenceTracing.MarkSuccess(activity);
        return Task.CompletedTask;
    }

    public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default)
    {
        using var activity = NonDurablePersistenceTracing.StartSubscriptionUnsubscribe(subscriber, messageType);
        if (storage.TryGetValue(messageType, out var subscribers))
        {
            subscribers.TryRemove(subscriber.TransportAddress, out _);
        }
        NonDurablePersistenceTracing.MarkSuccess(activity);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context, CancellationToken cancellationToken = default)
    {
        var messageTypesList = messageTypes as IList<MessageType> ?? messageTypes.ToList();
        using var activity = NonDurablePersistenceTracing.StartGetSubscribers(messageTypesList.Count);
        Dictionary<(string TransportAddress, string Endpoint), Subscriber> subscribers = [];

        foreach (var messageType in messageTypesList)
        {
            if (!storage.TryGetValue(messageType, out var subscriptions))
            {
                continue;
            }

            foreach (var subscriber in subscriptions.Values)
            {
                subscribers[(subscriber.TransportAddress, subscriber.Endpoint)] = subscriber;
            }
        }

        NonDurablePersistenceTracing.AddResolvedSubscribersEvent(activity, subscribers.Count);
        NonDurablePersistenceTracing.MarkSuccess(activity);
        return Task.FromResult<IEnumerable<Subscriber>>(subscribers.Values);
    }

    readonly ConcurrentDictionary<MessageType, ConcurrentDictionary<string, Subscriber>> storage = storage.Subscriptions;
}