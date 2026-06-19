namespace NServiceBus.AcceptanceTests.Sagas;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_saga_rollback_with_transport_transaction : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_rollback_saga_changes_on_handler_failure()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new StartSaga { Id = "test" })))
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Attempts, Is.EqualTo(2), "should have retried once");
            Assert.That(context.RetrySagaValue, Is.EqualTo(0), "saga value should be rolled back to 0 on retry");
        }
    }

    class Context : ScenarioContext
    {
        public int Attempts { get; set; }
        public int RetrySagaValue { get; set; }
    }

    class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint()
        {
            EndpointSetup<DefaultServer>(config =>
            {
                var recoverability = config.Recoverability();
                recoverability.Immediate(settings => settings.NumberOfRetries(1));
            });
        }
    }

    class SagaData : ContainSagaData
    {
        public string CorrelationId { get; set; }
        public int Value { get; set; }
    }

    class MySaga : Saga<SagaData>, IAmStartedByMessages<StartSaga>
    {
        Context testContext;

        public MySaga(Context context) => testContext = context;

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper) =>
            mapper.MapSaga(s => s.CorrelationId)
                  .ToMessage<StartSaga>(m => m.Id);

        public Task Handle(StartSaga message, IMessageHandlerContext context)
        {
            testContext.Attempts++;

            if (testContext.Attempts == 1)
            {
                Data.Value = 42;
                throw new Exception("Boom on first attempt");
            }

            testContext.RetrySagaValue = Data.Value;
            Data.Value = 100;
            testContext.MarkAsCompleted();
            return Task.CompletedTask;
        }
    }

    class StartSaga : IMessage
    {
        public string Id { get; set; }
    }
}