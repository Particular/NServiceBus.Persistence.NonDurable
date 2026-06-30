namespace NServiceBus.Persistence.NonDurable.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Extensibility;
using NServiceBus.Persistence;
using NServiceBus.Sagas;
using NUnit.Framework;

public class When_using_the_synchronized_storage_session_to_find_saga_data : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_route_message_without_correlation_property_via_projection()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithProjectionFinderSaga>(b =>
                b.When(session => session.SendLocal(new StartProcessExecution { ProcessExecutionId = Guid.NewGuid() })))
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.SagaIdFromFollowUp, Is.EqualTo(context.SagaIdFromStart));
            Assert.That(context.ServerTaskIdMatched, Is.True);
        }
    }

    public class Context : ScenarioContext
    {
        public Guid SagaIdFromStart { get; set; }
        public Guid SagaIdFromFollowUp { get; set; }
        public bool ServerTaskIdMatched { get; set; }
    }

    public class EndpointWithProjectionFinderSaga : EndpointConfigurationBuilder
    {
        public EndpointWithProjectionFinderSaga() => EndpointSetup<DefaultServer>();

        [Saga]
        public class ProcessExecutionSaga(Context testContext) : Saga<ProcessExecutionSagaData>,
            IAmStartedByMessages<StartProcessExecution>,
            IHandleMessages<ContinueWithServerTask>
        {
            public Task Handle(StartProcessExecution message, IMessageHandlerContext context)
            {
                Data.ProcessExecutionId = message.ProcessExecutionId;
                Data.ServerTaskId = Guid.NewGuid();

                testContext.SagaIdFromStart = Data.Id;

                return context.SendLocal(new ContinueWithServerTask
                {
                    ServerTaskId = Data.ServerTaskId
                });
            }

            public Task Handle(ContinueWithServerTask message, IMessageHandlerContext context)
            {
                testContext.SagaIdFromFollowUp = Data.Id;
                testContext.ServerTaskIdMatched = Data.ServerTaskId == message.ServerTaskId;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<ProcessExecutionSagaData> mapper)
            {
                mapper.MapSaga(saga => saga.ProcessExecutionId)
                    .ToMessage<StartProcessExecution>(m => m.ProcessExecutionId);

                mapper.ConfigureFinderMapping<ContinueWithServerTask, ProcessExecutionSagaFinder>();
            }
        }
    }

    public class ProcessExecutionSagaFinder : ISagaFinder<ProcessExecutionSagaData, ContinueWithServerTask>
    {
        public Task<ProcessExecutionSagaData> FindBy(ContinueWithServerTask message,
            ISynchronizedStorageSession storageSession, IReadOnlyContextBag context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(storageSession.NonDurablePersistenceSession().GetSagaData<ProcessExecutionSagaData, Guid>(
                context,
                message.ServerTaskId,
                static (sagaData, serverTaskId) => sagaData.ServerTaskId == serverTaskId,
                cancellationToken));
    }

    public class ProcessExecutionSagaData : ContainSagaData
    {
        public Guid ProcessExecutionId { get; set; }
        public Guid ServerTaskId { get; set; }
    }

    public class StartProcessExecution : IMessage
    {
        public Guid ProcessExecutionId { get; set; }
    }

    public class ContinueWithServerTask : IMessage
    {
        public Guid ServerTaskId { get; set; }
    }
}