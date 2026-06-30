namespace NServiceBus.Persistence.NonDurable.SagaPersister;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

class SagaEntry(IContainSagaData sagaData, CorrelationId correlationId, int version, JsonSerializerOptions serializerOptions)
{
    public CorrelationId CorrelationId { get; } = correlationId;

    public int Version { get; } = version;

    public Type SagaDataType { get; } = sagaData.GetType();

    public IContainSagaData GetSagaCopy() => (IContainSagaData)Deserialize(serializedSagaData, SagaDataType, serializerOptions);

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
    static string SerializeWithReflection(object value, Type runtimeType, JsonSerializerOptions options) => JsonSerializer.Serialize(value, runtimeType, options);

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
    static object DeserializeWithReflection(string json, Type runtimeType, JsonSerializerOptions options) => JsonSerializer.Deserialize(json, runtimeType, options)!;

    readonly string serializedSagaData = Serialize(sagaData, sagaData.GetType(), serializerOptions);
}