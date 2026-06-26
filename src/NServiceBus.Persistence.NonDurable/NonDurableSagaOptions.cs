namespace NServiceBus;

using System.Text.Json;

/// <summary>
/// Options for configuring saga persistence behavior.
/// </summary>
public sealed class NonDurableSagaOptions
{
    /// <summary>
    /// Gets or sets the <see cref="JsonSerializerOptions"/> used to persist saga data.
    /// </summary>
    /// <remarks>
    /// Saga data is the only persistence state that is JSON-serialized; outbox and subscription storage are unaffected
    /// by this setting.
    /// </remarks>
    public JsonSerializerOptions JsonSerializerOptions { get; init; } = new();
}
