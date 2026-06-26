namespace NServiceBus;

using System;
using System.Collections.Concurrent;
using Persistence.NonDurable;
using Persistence.NonDurable.SagaPersister;
using Unicast.Subscriptions;
using Unicast.Subscriptions.MessageDrivenSubscriptions;

/// <summary>
/// Shared in-memory persistence runtime for development and testing scenarios.
/// </summary>
public class NonDurableStorage
{
    /// <summary>
    /// Initializes a new instance of <see cref="NonDurableStorage"/> with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the non-durable persistence storage. When <c>null</c>, <see cref="TimeProvider.System"/> is used.</param>
    public NonDurableStorage(NonDurableStorageOptions? options = null) => TimeProvider = options?.TimeProvider ?? TimeProvider.System;

    /// <summary>
    /// The <see cref="TimeProvider"/> used for outbox entry expiry and timestamps.
    /// </summary>
    public TimeProvider TimeProvider { get; }

    internal ConcurrentDictionary<Guid, SagaEntry> Sagas { get; } = new();

    internal ConcurrentDictionary<CorrelationId, Guid> SagaCorrelationIds { get; } = new();

    internal ConcurrentDictionary<string, StoredOutboxMessage> OutboxMessages { get; } = new(StringComparer.Ordinal);

    internal ConcurrentDictionary<MessageType, ConcurrentDictionary<string, Subscriber>> Subscriptions { get; } = new();
}