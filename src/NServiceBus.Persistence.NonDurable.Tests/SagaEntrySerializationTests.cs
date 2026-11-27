namespace NServiceBus.Persistence.NonDurable.Tests;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;
using SagaPersister;

[TestFixture]
partial class SagaEntrySerializationTests
{
    [Test]
    public void Should_reuse_metadata_from_a_type_info_resolver_once_serialization_made_the_options_read_only()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = SagaEntryTestsJsonContext.Default };
        _ = CreateEntry(options);

        var first = SagaEntry.ResolveTypeInfo(typeof(ResolvedSagaData), options);
        var second = SagaEntry.ResolveTypeInfo(typeof(ResolvedSagaData), options);

        Assert.That(second, Is.Not.Null.And.SameAs(first));
    }

    [Test]
    public void Should_reuse_reflection_metadata_once_serialization_made_the_options_read_only()
    {
        var options = new JsonSerializerOptions();
        Assert.That(SagaEntry.ResolveTypeInfo(typeof(ResolvedSagaData), options), Is.Null, "Options without a resolver use reflection serialization");
        _ = CreateEntry(options);

        var first = SagaEntry.ResolveTypeInfo(typeof(ResolvedSagaData), options);
        var second = SagaEntry.ResolveTypeInfo(typeof(ResolvedSagaData), options);

        Assert.That(second, Is.Not.Null.And.SameAs(first));
    }

    [Test]
    public void Should_leave_types_the_resolver_omits_to_reflection_serialization() =>
        Assert.That(SagaEntry.ResolveTypeInfo(typeof(ResolvedSagaData), UnrelatedJsonContext.Default.Options), Is.Null);

    static SagaEntry CreateEntry(JsonSerializerOptions options) =>
        new(new ResolvedSagaData { Id = Guid.NewGuid() }, default, version: 1, NonDurableSagaConcurrencyMode.Optimistic, options);

    public class ResolvedSagaData : ContainSagaData
    {
        public int Value { get; set; }
    }

    [JsonSerializable(typeof(ResolvedSagaData))]
    partial class SagaEntryTestsJsonContext : JsonSerializerContext;

    [JsonSerializable(typeof(int))]
    partial class UnrelatedJsonContext : JsonSerializerContext;
}
