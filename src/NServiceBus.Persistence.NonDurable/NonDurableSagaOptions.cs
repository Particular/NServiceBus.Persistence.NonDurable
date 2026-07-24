namespace NServiceBus;

using System;
using System.Text.Json;

/// <summary>
/// Options for configuring saga persistence behavior.
/// </summary>
public sealed class NonDurableSagaOptions
{
    /// <summary>
    /// Gets or sets how saga persistence coordinates concurrent access when a saga is created.
    /// Existing saga lineages retain the mode they were created with until completion.
    /// </summary>
    public NonDurableSagaConcurrencyMode ConcurrencyMode { get; init; } = NonDurableSagaConcurrencyMode.Optimistic;

    /// <summary>
    /// Gets or sets how long pessimistic saga reads wait to acquire a lock before failing.
    /// </summary>
    /// <remarks>
    /// This timeout applies to the current endpoint's synchronized storage session when it reads a pessimistic saga lineage.
    /// It must be greater than zero. The default is 30 seconds.
    /// </remarks>
    public TimeSpan PessimisticLockTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the <see cref="JsonSerializerOptions"/> used to persist saga data.
    /// </summary>
    /// <remarks>
    /// Saga data is the only persistence state that is JSON-serialized; outbox and subscription storage are unaffected
    /// by this setting.
    /// </remarks>
    public JsonSerializerOptions JsonSerializerOptions { get; init; } = new();
}
