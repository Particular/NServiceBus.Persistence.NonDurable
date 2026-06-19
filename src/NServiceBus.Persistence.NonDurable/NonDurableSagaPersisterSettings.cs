namespace NServiceBus.Persistence.NonDurable;

using System.Text.Json;

/// <summary>
/// Holds settings for the NonDurableSagaPersister to support constructor injection.
/// </summary>
public class NonDurableSagaPersisterSettings(JsonSerializerOptions serializerOptions)
{
    /// <summary>
    /// The JSON serializer options used for saga data serialization.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; } = serializerOptions;
}