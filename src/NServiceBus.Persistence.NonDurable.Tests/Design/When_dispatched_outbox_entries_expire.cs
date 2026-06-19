namespace NServiceBus.Persistence.NonDurable.Tests;

using System;
using System.Threading.Tasks;
using NServiceBus.Outbox;
using NUnit.Framework;

[TestFixture]
public class When_dispatched_outbox_entries_expire
{
    [Test]
    public async Task Should_expire_from_dispatch_time()
    {
        var storage = new NonDurableOutboxStorage("test-endpoint", new NonDurableStorage());
        var message = new OutboxMessage("message-id",
        [
            new TransportOperation("operation-id", [], new byte[] { 1 }, [])
        ]);

        await using (var transaction = (NonDurableOutboxTransaction)await storage.BeginTransaction(new(), TestContext.CurrentContext.CancellationToken))
        {
            await storage.Store(message, transaction, new(), TestContext.CurrentContext.CancellationToken);
            await transaction.Commit(TestContext.CurrentContext.CancellationToken);
        }

        storage.Messages[$"test-endpoint_{message.MessageId}"].StoredAt = DateTime.UtcNow.AddDays(-1);

        await storage.SetAsDispatched(message.MessageId, new(), TestContext.CurrentContext.CancellationToken);

        storage.RemoveEntriesOlderThan(DateTime.UtcNow.AddMinutes(-5));

        var persisted = await storage.Get(message.MessageId, new(), TestContext.CurrentContext.CancellationToken);
        Assert.That(persisted, Is.Not.Null);
    }
}