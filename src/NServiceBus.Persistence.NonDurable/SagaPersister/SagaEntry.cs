namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Represents a stored saga entry with versioning for optimistic concurrency control.
/// Uses System.Text.Json for deep copying to ensure AOT/trimming compatibility.
/// </summary>
class SagaEntry(IContainSagaData sagaData, CorrelationId correlationId, int version, JsonSerializerOptions serializerOptions)
{
    public CorrelationId CorrelationId { get; } = correlationId;

    public int Version { get; } = version;

    /// <summary>
    /// Creates a deep copy of the saga data using System.Text.Json serialization.
    /// This approach is AOT and trimming compatible.
    /// </summary>
    public IContainSagaData GetSagaCopy()
    {
        var type = sagaData.GetType();
        var json = Serialize(sagaData, type, serializerOptions);
        return (IContainSagaData)Deserialize(json, type, serializerOptions)!;
    }

    public SagaEntry UpdateTo(IContainSagaData newSagaData, JsonSerializerOptions newSerializerOptions)
        => new(newSagaData, CorrelationId, Version + 1, newSerializerOptions);

    static string Serialize(object value, Type runtimeType, JsonSerializerOptions options)
    {
        var typeInfo = ResolveTypeInfo(runtimeType, options);
        return typeInfo is not null ? JsonSerializer.Serialize(value, typeInfo) : SerializeWithReflection(value, runtimeType, options);
    }

    static object Deserialize(string json, Type runtimeType, JsonSerializerOptions options)
    {
        var typeInfo = ResolveTypeInfo(runtimeType, options);
        return typeInfo is not null ? JsonSerializer.Deserialize(json, typeInfo)! : DeserializeWithReflection(json, runtimeType, options);
    }

    static JsonTypeInfo? ResolveTypeInfo(Type runtimeType, JsonSerializerOptions options)
    {
        var typeInfo = options.TypeInfoResolver?.GetTypeInfo(runtimeType, options);
        if (typeInfo is not null)
        {
            return typeInfo;
        }

        return JsonSerializer.IsReflectionEnabledByDefault ? null : throw new InvalidOperationException($"No JSON metadata was found for '{runtimeType.FullName}'.");
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
    static string SerializeWithReflection(object value, Type runtimeType, JsonSerializerOptions options)
        => JsonSerializer.Serialize(value, runtimeType, options);

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
    static object DeserializeWithReflection(string json, Type runtimeType, JsonSerializerOptions options)
        => JsonSerializer.Deserialize(json, runtimeType, options)!;
}