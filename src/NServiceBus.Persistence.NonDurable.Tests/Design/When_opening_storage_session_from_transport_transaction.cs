namespace NServiceBus.Persistence.NonDurable.Tests.Design;

using System.Transactions;
using System.Threading.Tasks;
using NUnit.Framework;
using Transport;

[TestFixture]
public class When_opening_storage_session_from_transport_transaction
{
    [Test]
    public async Task Should_use_dedicated_transaction_key_when_type_keyed_transaction_also_exists()
    {
        using var coordinator = new CommittableTransaction();
        using var unrelated = new CommittableTransaction();
        var transportTransaction = new TransportTransaction();
        transportTransaction.Set<Transaction>(unrelated);
        transportTransaction.Set(NonDurableTransactionKeys.Transaction, coordinator);
        var session = new NonDurableSynchronizedStorageSession();
        var applied = false;

        var opened = await session.TryOpen(transportTransaction, out _);
        session.Enlist(new object(), _ => applied = true);
        coordinator.Commit();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(opened, Is.True);
            Assert.That(applied, Is.True);
        }
    }

    [Test]
    public async Task Should_ignore_type_keyed_transaction()
    {
        using var transaction = new CommittableTransaction();
        var transportTransaction = new TransportTransaction();
        transportTransaction.Set<Transaction>(transaction);
        var session = new NonDurableSynchronizedStorageSession();

        var opened = await session.TryOpen(transportTransaction, out _);

        Assert.That(opened, Is.False);
    }
}
