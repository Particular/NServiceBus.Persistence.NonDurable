namespace NServiceBus;

using System;

/// <summary>
/// Options for configuring the non-durable persistence.
/// </summary>
public sealed class NonDurablePersistenceOptions
{
    /// <summary>
    /// Gets the optional <see cref="NonDurableStorage" /> to use when dependency injection does not provide one.
    /// </summary>
    /// <remarks>
    /// When multiple endpoints need to share persistence state in-memory, they should share the same storage instance.
    /// Storage resolution is optional and uses the following precedence: a <see cref="NonDurableStorage" /> resolved from
    /// dependency injection, then the storage provided here, and finally the shared storage.
    /// For testing, omit the storage parameter and the shared storage will be used unless dependency injection supplies one.
    /// </remarks>
    public NonDurableStorage? Storage { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="TimeProvider"/> used for outbox entry expiry and timestamps.
    /// When set to <c>null</c>, <see cref="TimeProvider.System"/> is used.
    /// </summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>
    /// Gets or sets the saga persistence options. When set to a non-null value, custom saga configuration is applied.
    /// </summary>
    /// <remarks>
    /// Saga data is the only persistence state that is JSON-serialized; outbox and subscription storage are unaffected
    /// by these settings.
    /// </remarks>
    public SagaOptions? Saga { get; set; }
}