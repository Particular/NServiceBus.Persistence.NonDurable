namespace NServiceBus.Persistence.NonDurable.AcceptanceTests;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public partial class When_persisting_saga_data_with_custom_json_options : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_serialize_and_deserialize_saga_data_using_custom_json_serializer_options()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointThatHostsASaga>(
                b => b.When(session => session.SendLocal(new StartSaga
                {
                    CorrelationId = Guid.NewGuid()
                })))
            .Run();

        Assert.That(context.LoadedSagaData, Is.Not.Null);
        Assert.That(context.LoadedSagaData.Value, Is.EqualTo("Hello from trimming-friendly path"));
    }

    public class Context : ScenarioContext
    {
        public SagaDataWithCustomOptions LoadedSagaData { get; set; }
        public bool SagaDataLoaded { get; set; }
    }

    public class EndpointThatHostsASaga : EndpointConfigurationBuilder
    {
        public EndpointThatHostsASaga()
        {
            var jsonOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = SagaDataTypeContext.Default
            };

            EndpointSetup<DefaultServer>(c =>
            {
                c.UseNonDurablePersistence(new NonDurablePersistenceOptions
                {
                    Saga = new SagaOptions
                    {
                        JsonSerializerOptions = jsonOptions
                    }
                });
            });
        }

        [Saga]
        public class CustomOptionsSaga(Context testContext) : Saga<SagaDataWithCustomOptions>,
            IAmStartedByMessages<StartSaga>,
            IHandleMessages<LoadTheSagaAgain>
        {
            public Task Handle(StartSaga message, IMessageHandlerContext context)
            {
                Data.Value = "Hello from trimming-friendly path";
                return context.SendLocal(new LoadTheSagaAgain
                {
                    DataId = Data.CorrelationId
                });
            }

            public Task Handle(LoadTheSagaAgain message, IMessageHandlerContext context)
            {
                testContext.LoadedSagaData = Data;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaDataWithCustomOptions> mapper) =>
                mapper.MapSaga(saga => saga.CorrelationId)
                    .ToMessage<StartSaga>(m => m.CorrelationId)
                    .ToMessage<LoadTheSagaAgain>(m => m.DataId);
        }
    }

    public class SagaDataWithCustomOptions : ContainSagaData
    {
        public Guid CorrelationId { get; set; }
        public string Value { get; set; }
    }

    [JsonSerializable(typeof(SagaDataWithCustomOptions))]
    public partial class SagaDataTypeContext : JsonSerializerContext;

    public class StartSaga : IMessage
    {
        public Guid CorrelationId { get; set; }
    }

    public class LoadTheSagaAgain : IMessage
    {
        public Guid DataId { get; set; }
    }
}