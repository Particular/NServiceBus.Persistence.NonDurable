namespace NServiceBus;

using System.Text.Json;

/// <summary>
/// Options for configuring saga persistence behavior.
/// </summary>
public sealed class SagaOptions
{
    /// <summary>
    /// Gets or sets the <see cref="JsonSerializerOptions"/> used to persist saga data, or <c>null</c> to use sensible defaults.
    /// </summary>
    /// <remarks>
    /// Saga data is the only persistence state that is JSON-serialized; outbox and subscription storage are unaffected
    /// by this setting.
    /// </remarks>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }
}
