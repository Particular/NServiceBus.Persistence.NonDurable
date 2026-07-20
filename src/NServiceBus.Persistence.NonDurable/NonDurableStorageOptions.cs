namespace NServiceBus;

using System;

/// <summary>
/// Options for configuring the non-durable persistence storage.
/// </summary>
public sealed class NonDurableStorageOptions
{
    /// <summary>
    /// Gets or sets the <see cref="TimeProvider"/> used for outbox entry expiry and timestamps.
    /// When set to <c>null</c>, <see cref="TimeProvider.System"/> is used.
    /// </summary>
    public TimeProvider? TimeProvider { get; init; }
}
