namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Outbox;

class NonDurableOutboxStorage(string endpointName, NonDurableStorage storage) : IOutboxStorage
{
    public NonDurableOutboxStorage(string endpointName) : this(endpointName, new NonDurableStorage())
    {
    }

    public Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        using var activity = NonDurablePersistenceTracing.StartOutboxGet(messageId);
        var storageKey = OutboxStorageKey(messageId);
        if (!Storage.TryGetValue(storageKey, out var storedMessage))
        {
            NonDurablePersistenceTracing.AddMissEvent(activity);
            NonDurablePersistenceTracing.MarkSuccess(activity);
            return NoOutboxMessageTask!;
        }

        NonDurablePersistenceTracing.AddHitEvent(activity);
        NonDurablePersistenceTracing.MarkSuccess(activity);
        return Task.FromResult(new OutboxMessage(messageId, storedMessage.TransportOperations));
    }

    public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = default)
    {
        using var activity = NonDurablePersistenceTracing.StartOutboxBeginTransaction();
        var transaction = new NonDurableOutboxTransaction();
        NonDurablePersistenceTracing.MarkSuccess(activity);
        return Task.FromResult<IOutboxTransaction>(transaction);
    }

    public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context, CancellationToken cancellationToken = default)
    {
        using var activity = NonDurablePersistenceTracing.StartOutboxStore(message.MessageId, message.TransportOperations.Length);
        var storageKey = OutboxStorageKey(message.MessageId);
        var tx = (NonDurableOutboxTransaction)transaction;
        tx.Enlist(
            new StoreOperationState(Storage, storageKey, message.MessageId, message.TransportOperations.Select(CopyOperation).ToArray()),
            static state =>
            {
                if (!state.Storage.TryAdd(state.StorageKey, new StoredOutboxMessage(state.MessageId, state.TransportOperations)))
                {
                    throw new Exception($"Outbox message with id '{state.MessageId}' is already present in storage.");
                }
            },
            static state => state.Storage.TryRemove(state.StorageKey, out _));
        NonDurablePersistenceTracing.AddStagedEvent(activity);
        NonDurablePersistenceTracing.MarkSuccess(activity);

        return Task.CompletedTask;
    }

    public Task SetAsDispatched(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        using var activity = NonDurablePersistenceTracing.StartOutboxSetAsDispatched(messageId);
        var storageKey = OutboxStorageKey(messageId);
        if (!Storage.TryGetValue(storageKey, out var storedMessage))
        {
            NonDurablePersistenceTracing.AddMissEvent(activity);
            NonDurablePersistenceTracing.MarkSuccess(activity);
            return Task.CompletedTask;
        }

        storedMessage.MarkAsDispatched(DateTime.UtcNow);
        NonDurablePersistenceTracing.AddHitEvent(activity);
        NonDurablePersistenceTracing.AddMarkedDispatchedEvent(activity);
        NonDurablePersistenceTracing.MarkSuccess(activity);
        return Task.CompletedTask;
    }

    public void RemoveEntriesOlderThan(DateTime dateTime)
    {
        foreach (var entry in Storage)
        {
            var storedMessage = entry.Value;
            if (storedMessage.Dispatched && storedMessage.DispatchedAt < dateTime)
            {
                Storage.TryRemove(entry.Key, out _);
            }
        }
    }

    string OutboxStorageKey(string messageId) => $"{endpointName}_{messageId}";

    internal ConcurrentDictionary<string, StoredOutboxMessage> Messages => Storage;

    readonly record struct StoreOperationState(
        ConcurrentDictionary<string, StoredOutboxMessage> Storage,
        string StorageKey,
        string MessageId,
        TransportOperation[] TransportOperations);

    static TransportOperation CopyOperation(TransportOperation operation)
    {
        var headers = operation.Headers != null
            ? new Dictionary<string, string>(operation.Headers)
            : [];

        var options = operation.Options != null
            ? new Transport.DispatchProperties(operation.Options)
            : [];

        var body = operation.Body.IsEmpty
            ? []
            : operation.Body.ToArray();

        return new TransportOperation(operation.MessageId, options, body, headers);
    }

    ConcurrentDictionary<string, StoredOutboxMessage> Storage { get; } = storage.OutboxMessages;
    static readonly Task<OutboxMessage?> NoOutboxMessageTask = Task.FromResult(default(OutboxMessage));
}