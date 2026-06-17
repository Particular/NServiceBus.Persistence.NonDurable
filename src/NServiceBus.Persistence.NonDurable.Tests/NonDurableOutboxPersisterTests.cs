namespace NServiceBus.Persistence.NonDurable.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;
    using NUnit.Framework;

    [TestFixture]
    class NonDurableOutboxPersisterTests
    {
        [Test]
        public async Task Should_clear_operations_on_dispatched_messages()
        {
            var storage = new NonDurableOutboxStorage("test-endpoint");

            var messageId = "myId";

            var messageToStore = new OutboxMessage(messageId, new[] { new TransportOperation("x", null, null, null) });
            using (var transaction = await storage.BeginTransaction(new ContextBag()))
            {
                await storage.Store(messageToStore, transaction, new ContextBag());

                await transaction.Commit();
            }

            await storage.SetAsDispatched(messageId, new ContextBag());

            var message = await storage.Get(messageId, new ContextBag());

            Assert.That(message.TransportOperations.Any(), Is.False);
        }

        [Test]
        public async Task Should_not_remove_non_dispatched_messages()
        {
            var storage = new NonDurableOutboxStorage("test-endpoint");

            var messageId = "myId";

            var messageToStore = new OutboxMessage(messageId, new[] { new TransportOperation("x", null, null, null) });

            using (var transaction = await storage.BeginTransaction(new ContextBag()))
            {
                await storage.Store(messageToStore, transaction, new ContextBag());

                await transaction.Commit();
            }

            storage.RemoveEntriesOlderThan(DateTime.UtcNow);

            var message = await storage.Get(messageId, new ContextBag());
            Assert.That(message, Is.Not.Null);
        }

        [Test]
        public async Task Should_clear_dispatched_messages_after_given_expiry()
        {
            var storage = new NonDurableOutboxStorage("test-endpoint");

            var messageId = "myId";

            var messageToStore = new OutboxMessage(messageId, new[] { new TransportOperation("x", null, null, null) });
            using (var transaction = await storage.BeginTransaction(new ContextBag()))
            {
                await storage.Store(messageToStore, transaction, new ContextBag());

                await transaction.Commit();
            }

            await storage.SetAsDispatched(messageId, new ContextBag());

            var beforeExpiry = DateTime.UtcNow.AddMinutes(-1);

            // Should not remove entries dispatched after the expiry threshold
            storage.RemoveEntriesOlderThan(beforeExpiry);

            var message = await storage.Get(messageId, new ContextBag());
            Assert.That(message, Is.Not.Null);

            var afterExpiry = DateTime.UtcNow.AddMinutes(1);

            // Should remove entries dispatched before the expiry threshold
            storage.RemoveEntriesOlderThan(afterExpiry);

            message = await storage.Get(messageId, new ContextBag());
            Assert.That(message, Is.Null);
        }

        [Test]
        public async Task Should_not_store_when_transaction_not_committed()
        {
            var storage = new NonDurableOutboxStorage("test-endpoint");

            var messageId = "myId";

            var contextBag = new ContextBag();
            using (var transaction = await storage.BeginTransaction(contextBag))
            {
                var messageToStore = new OutboxMessage(messageId, new[] { new TransportOperation("x", null, null, null) });
                await storage.Store(messageToStore, transaction, contextBag);

                // do not commit
            }

            var message = await storage.Get(messageId, new ContextBag());
            Assert.That(message, Is.Null);
        }

        [Test]
        public async Task Should_store_when_transaction_committed()
        {
            var storage = new NonDurableOutboxStorage("test-endpoint");

            var messageId = "myId";

            var contextBag = new ContextBag();
            using (var transaction = await storage.BeginTransaction(contextBag))
            {
                var messageToStore = new OutboxMessage(messageId, new[] { new TransportOperation("x", null, null, null) });
                await storage.Store(messageToStore, transaction, contextBag);

                await transaction.Commit();
            }

            var message = await storage.Get(messageId, new ContextBag());
            Assert.That(message, Is.Not.Null);
        }
    }
}