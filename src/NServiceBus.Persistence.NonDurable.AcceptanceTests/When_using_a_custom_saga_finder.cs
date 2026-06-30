namespace NServiceBus.Persistence.NonDurable.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Extensibility;
using NServiceBus.Persistence;
using NServiceBus.Sagas;
using NUnit.Framework;

public class When_using_a_custom_saga_finder : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_route_message_without_correlation_property_via_finder()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithCustomFinderSaga>(b =>
            {
                b.Services(services => services.AddSingleton<ServerTaskIndex>());
                b.When(session => session.SendLocal(new StartProcessExecution
                {
                    ProcessExecutionId = Guid.NewGuid()
                }));
            })
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.SagaIdFromFollowUp, Is.EqualTo(context.SagaIdFromStart));
            Assert.That(context.ServerTaskIdMatched, Is.True);
        });
    }

    // A custom finder that correlates on a non-correlation property cannot query the
    // in-memory saga dictionary through the synchronized storage session yet, so it keeps a
    // ServerTaskId -> saga id index and delegates to the persister's by-id Get (which captures
    // the entry for the optimistic-concurrency check). A projection API that removes the need
    // for this parallel index is tracked as a follow-up.

    public class Context : ScenarioContext
    {
        public Guid SagaIdFromStart { get; set; }
        public Guid SagaIdFromFollowUp { get; set; }
        public bool ServerTaskIdMatched { get; set; }
    }

    public class ServerTaskIndex
    {
        public ConcurrentDictionary<Guid, Guid> ServerTaskIdToSagaId { get; } = new();
    }

    public class EndpointWithCustomFinderSaga : EndpointConfigurationBuilder
    {
        public EndpointWithCustomFinderSaga() => EndpointSetup<DefaultServer>();

        public class ProcessExecutionSaga(Context testContext, ServerTaskIndex index) : Saga<ProcessExecutionSagaData>,
            IAmStartedByMessages<StartProcessExecution>,
            IHandleMessages<ContinueWithServerTask>
        {
            public Task Handle(StartProcessExecution message, IMessageHandlerContext context)
            {
                Data.ProcessExecutionId = message.ProcessExecutionId;
                Data.ServerTaskId = Guid.NewGuid();

                index.ServerTaskIdToSagaId[Data.ServerTaskId] = Data.Id;
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

    public class ProcessExecutionSagaFinder(ISagaPersister persister, ServerTaskIndex index)
        : ISagaFinder<ProcessExecutionSagaData, ContinueWithServerTask>
    {
        public async Task<ProcessExecutionSagaData> FindBy(ContinueWithServerTask message,
            ISynchronizedStorageSession storageSession, IReadOnlyContextBag context,
            CancellationToken cancellationToken = default)
        {
            if (!index.ServerTaskIdToSagaId.TryGetValue(message.ServerTaskId, out var sagaId))
            {
                return null;
            }

            return await persister.Get<ProcessExecutionSagaData>(sagaId, storageSession, (ContextBag)context, cancellationToken);
        }
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