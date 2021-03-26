namespace NServiceBus.Persistence.NonDurable.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Configuration.AdvancedExtensibility;
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

                    await session.Send(new PlaceOrder(), options);

                    // send a duplicate to be discarded since we use the same message id
                    await session.Send(new PlaceOrder(), options);

                    // delay and send the same message as we now expect the outbox to have cleared after the delay
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await session.Send(new PlaceOrder(), options);

                    // terminate the test
                    await session.SendLocal(new PlaceOrder { Terminate = true });
                }))
                .WithEndpoint<DownstreamEndpoint>()
                .Done(c => c.Done)
                .Run();

            Assert.AreEqual(3, context.MessagesReceivedByOutboxEndpoint, "Outbox endpoint should get 3 messages (1 discarded)");
            Assert.AreEqual(2, context.MessagesReceivedByDownstreamEndpoint, "Downstream endpoint should get 2 messages");
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

            class SendOrderAcknowledgementHandler : IHandleMessages<SendOrderAcknowledgement>
            {
                public SendOrderAcknowledgementHandler(Context context)
                {
                    testContext = context;
                }

                public Task Handle(SendOrderAcknowledgement message, IMessageHandlerContext context)
                {
                    if (!message.Terminate)
                    {
                        testContext.MessagesReceivedByDownstreamEndpoint++;
                    }
                    else
                    {
                        testContext.Done = true;
                    }

                    return Task.FromResult(0);
                }

                readonly Context testContext;
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
                    b.GetSettings().Set("Outbox.NonDurableTimeToCheckForDuplicateEntries", TimeSpan.FromMilliseconds(100));
                    b.ConfigureRouting()
                        .RouteToEndpoint(typeof(SendOrderAcknowledgement), typeof(DownstreamEndpoint));
                });
            }

            class PlaceOrderHandler : IHandleMessages<PlaceOrder>
            {
                public PlaceOrderHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(PlaceOrder message, IMessageHandlerContext context)
                {
                    testContext.MessagesReceivedByOutboxEndpoint++;
                    return context.Send(new SendOrderAcknowledgement
                    {
                        Terminate = message.Terminate
                    });
                }

                readonly Context testContext;
            }
        }

        public class PlaceOrder : IMessage
        {
            public bool Terminate { get; set; }
        }

        public class Terminate : IMessage
        {

        }

        public class SendOrderAcknowledgement : IMessage
        {
            public bool Terminate { get; set; }
        }
    }
}