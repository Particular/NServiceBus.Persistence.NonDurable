namespace NServiceBus.Persistence.NonDurable.AcceptanceTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using Configuration.AdvancedExtensibility;
    using NUnit.Framework;

    public class When_setting_timetokeepdeduplicationdata
    {
        const string NServiceBusPersistenceNonDurableUniqueIdHeader = "NServiceBus.Persistence.NonDurable.UniqueId";

        [Test]
        public async Task Should_ignore_duplicates_only_within_time_configured()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<OutboxEndpoint>(b => b.When(async session =>
                {
                    var duplicateMessageId = Guid.NewGuid().ToString();

                    //Following three messages are duplicates in the sense of NServiceBus but carry a header that allows this test to distinguish which copy of the duplicated message has been processed
                    await session.Send(new PlaceOrder(), CreateSendOptions(duplicateMessageId, "1"));

                    // send a duplicate to be discarded since we use the same message id
                    await session.Send(new PlaceOrder(), CreateSendOptions(duplicateMessageId, "2"));

                    // delay and send the same message as we now expect the outbox to have cleared after the delay
                    await Task.Delay(TimeSpan.FromSeconds(6));
                    await session.Send(new PlaceOrder(), CreateSendOptions(duplicateMessageId, "3"));
                }))
                .Done(c => (c.ProcessedIds.TryGetValue("1", out _) || c.ProcessedIds.TryGetValue("2", out _)) && c.ProcessedIds.TryGetValue("3", out _))
                .Run();

            Assert.IsTrue(context.ProcessedIds.ContainsKey("1") || context.ProcessedIds.ContainsKey("2"), "Either copy 1 or 2 should be processed");
            Assert.IsFalse(context.ProcessedIds.ContainsKey("1") && context.ProcessedIds.ContainsKey("2"), "Copy 1 and 2 should not both be processed");
            Assert.IsTrue(context.ProcessedIds.ContainsKey("3"), "Copy 3 should be processed because it is sent after the expiration period");
        }

        /// <summary>
        /// Creates send options for duplicates but adds a header that allows the test to distinguish the messages
        /// </summary>
        /// <returns></returns>
        static SendOptions CreateSendOptions(string messageId, string uniqueId)
        {
            var options = new SendOptions();
            options.SetMessageId(messageId);
            options.RouteToThisEndpoint();
            options.SetHeader(NServiceBusPersistenceNonDurableUniqueIdHeader, uniqueId);
            return options;
        }

        class Context : ScenarioContext
        {
            public ConcurrentDictionary<string, bool> ProcessedIds { get; } = new ConcurrentDictionary<string, bool>();
        }

        class OutboxEndpoint : EndpointConfigurationBuilder
        {
            public OutboxEndpoint()
            {
                EndpointSetup<DefaultServer>(b =>
                {
                    // limit to one to avoid race conditions on dispatch and this allows us to reliably check whether deduplication happens properly
                    b.LimitMessageProcessingConcurrencyTo(1);
                    b.EnableOutbox().TimeToKeepDeduplicationData(TimeSpan.FromSeconds(3));
                    b.GetSettings().Set("Outbox.NonDurableTimeToCheckForDuplicateEntries", TimeSpan.FromMilliseconds(100));
                });
            }

            class PlaceOrderHandler : IHandleMessages<PlaceOrder>
            {
                public PlaceOrderHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(PlaceOrder message, IMessageHandlerContext context)
                {
                    testContext.ProcessedIds[context.MessageHeaders[NServiceBusPersistenceNonDurableUniqueIdHeader]] = true;
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class PlaceOrder : IMessage
        {
        }
    }
}