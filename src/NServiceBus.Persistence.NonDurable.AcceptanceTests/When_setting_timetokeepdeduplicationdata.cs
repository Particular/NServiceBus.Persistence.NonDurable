using NServiceBus.Configuration.AdvancedExtensibility;

namespace NServiceBus.Persistence.NonDurable.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_setting_timetokeepdeduplicationdata
    {
        [Test]
        public async Task Should_ignore_duplicates_only_within_time_configured()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<OutboxEndpoint>(b => b.When(async session =>
                {
                    var duplicateMessageId = Guid.NewGuid().ToString();

                    var options = new SendOptions();
                    options.SetMessageId(duplicateMessageId);
                    options.RouteToThisEndpoint();

                    var order = new PlaceOrder {Id = 1};

                    await session.Send(order, options);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await session.Send(order, options);

                    // delay and send the same message as we expect the outbox to have cleared after the delay
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await session.Send(order, options);

                    // end
                    var terminateOptions = new SendOptions();
                    terminateOptions.RouteToThisEndpoint();
                    await session.Send(new PlaceOrder {Id = 500, Terminate = true}, terminateOptions);
                }))
                .WithEndpoint<DownstreamEndpoint>()
                .Done(c => c.Done)
                .Run();

            Assert.AreEqual(2, context.MessagesReceivedByDownstreamEndpoint);
            Assert.AreEqual(3, context.MessagesReceivedByOutboxEndpoint);
        }

        public class Context : ScenarioContext
        {
            public int MessagesReceivedByDownstreamEndpoint { get; set; }
            public bool Done { get; set; }
            public int MessagesReceivedByOutboxEndpoint { get; set; }
        }

        public class DownstreamEndpoint : EndpointConfigurationBuilder
        {
            public DownstreamEndpoint()
            {
                EndpointSetup<DefaultServer>(b =>
                {
                    b.LimitMessageProcessingConcurrencyTo(1);
                });
            }

            private class SendOrderAcknowledgementHandler : IHandleMessages<SendOrderAcknowledgement>
            {
                private readonly Context testContext;

                public SendOrderAcknowledgementHandler(Context context)
                {
                    testContext = context;
                }

                public Task Handle(SendOrderAcknowledgement message, IMessageHandlerContext context)
                {
                    if (!message.Terminate)
                        testContext.MessagesReceivedByDownstreamEndpoint++;
                    else
                        testContext.Done = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class OutboxEndpoint : EndpointConfigurationBuilder
        {
            public OutboxEndpoint()
            {
                EndpointSetup<DefaultServer>(b =>
                {
                    // limit to one to avoid race conditions on dispatch and this allows us to reliably check whether deduplication happens properly
                    b.LimitMessageProcessingConcurrencyTo(1);
                    b.EnableOutbox().TimeToKeepDeduplicationData(TimeSpan.FromSeconds(3));
                    b.GetSettings().Set("Outbox.NonDurableTimeToCheckForDuplicateEntries", TimeSpan.FromSeconds(1));
                    b.ConfigureTransport().Routing()
                        .RouteToEndpoint(typeof(SendOrderAcknowledgement), typeof(DownstreamEndpoint));
                });
            }

            private class PlaceOrderHandler : IHandleMessages<PlaceOrder>
            {
                private readonly Context testContext;

                public PlaceOrderHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(PlaceOrder message, IMessageHandlerContext context)
                {
                    testContext.MessagesReceivedByOutboxEndpoint++;
                    return context.Send(new SendOrderAcknowledgement
                    {
                        Id = message.Id,
                        Terminate = message.Terminate
                    });
                }
            }
        }

        public class PlaceOrder : ICommand
        {
            public int Id { get; set; }
            public bool Terminate { get; set; }
        }

        public class Terminate : ICommand
        {

        }

        public class SendOrderAcknowledgement : IMessage
        {
            public int Id { get; set; }
            public bool Terminate { get; set; }
        }
    }
}