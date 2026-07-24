namespace NServiceBus.Persistence.NonDurable.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Extensibility;
using NUnit.Framework;
using SagaPersister;
using Testing;
using Transport;

[TestFixture]
class PessimisticSagaLockingTests
{
    [Test]
    public void Update_preserves_lock_identity_while_recreation_changes_it()
    {
        var options = CreateOptions();
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "original" };
        var correlationId = new CorrelationId(typeof(TestSagaData), nameof(TestSagaData.SomeId), saga.SomeId);
        var original = new SagaEntry(saga, correlationId, version: 1, NonDurableSagaConcurrencyMode.Pessimistic, options.JsonSerializerOptions);

        saga.SomeId = "updated";
        var updated = original.UpdateTo(saga, options.JsonSerializerOptions);
        var recreated = new SagaEntry(saga, correlationId, version: 1, NonDurableSagaConcurrencyMode.Pessimistic, options.JsonSerializerOptions);

        Assert.Multiple(() =>
        {
            Assert.That(original.HasSameLockIdentity(updated), Is.True);
            Assert.That(original.HasSameLockIdentity(recreated), Is.False);
        });
    }

    [Test]
    public async Task Old_generation_waiter_retries_against_recreated_saga()
    {
        var (persister, options, storage) = CreatePersister();
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "original" };
        await SaveSaga(persister, options, storage, saga);

        using var holder = await OpenSession(storage, options);
        var holderContext = new ContextBag();
        await persister.Get<TestSagaData>(saga.Id, holder, holderContext);

        using var waiter = await OpenSession(storage, options);
        var waitingRead = persister.Get<TestSagaData>(saga.Id, waiter, new ContextBag());
        Assert.That(waitingRead.IsCompleted, Is.False, "contended reads must wait asynchronously");

        var oldEntry = storage.Sagas[saga.Id];
        var recreatedSaga = new TestSagaData { Id = saga.Id, SomeId = "recreated" };
        storage.Sagas[saga.Id] = new SagaEntry(
            recreatedSaga,
            oldEntry.CorrelationId,
            version: 1,
            NonDurableSagaConcurrencyMode.Pessimistic,
            options.JsonSerializerOptions);

        holder.Dispose();

        var loaded = await waitingRead;
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.SomeId, Is.EqualTo("recreated"));
    }

    [Test]
    public async Task Correlation_waiter_re_resolves_after_lineage_changes()
    {
        var (persister, options, storage) = CreatePersister();
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "correlated" };
        await SaveSaga(persister, options, storage, saga);

        using var holder = await OpenSession(storage, options);
        await persister.Get<TestSagaData>(saga.Id, holder, new ContextBag());

        using var waiter = await OpenSession(storage, options);
        var waitingRead = persister.Get<TestSagaData>(nameof(TestSagaData.SomeId), saga.SomeId, waiter, new ContextBag());
        Assert.That(waitingRead.IsCompleted, Is.False, "contended reads must wait asynchronously");

        var oldEntry = storage.Sagas[saga.Id];
        var recreatedSaga = new TestSagaData { Id = saga.Id, SomeId = saga.SomeId };
        storage.Sagas[saga.Id] = new SagaEntry(
            recreatedSaga,
            oldEntry.CorrelationId,
            version: 1,
            NonDurableSagaConcurrencyMode.Pessimistic,
            options.JsonSerializerOptions);

        holder.Dispose();

        var loaded = await waitingRead;
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.Id, Is.EqualTo(saga.Id));
    }

    [Test]
    public async Task Contended_read_honors_cancellation_without_releasing_the_holder()
    {
        var (persister, options, storage) = CreatePersister(TimeSpan.FromSeconds(30));
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "cancel" };
        await SaveSaga(persister, options, storage, saga);

        using var holder = await OpenSession(storage, options);
        await persister.Get<TestSagaData>(saga.Id, holder, new ContextBag());

        using var waiter = await OpenSession(storage, options);
        using var cancellation = new CancellationTokenSource();
        var waitingRead = persister.Get<TestSagaData>(saga.Id, waiter, new ContextBag(), cancellation.Token);
        Assert.That(waitingRead.IsCompleted, Is.False, "contended reads must not synchronously block the caller");

        cancellation.Cancel();

        Assert.That(async () => await waitingRead, Throws.InstanceOf<OperationCanceledException>());

        holder.Dispose();
        using var verifier = await OpenSession(storage, options);
        Assert.That(await persister.Get<TestSagaData>(saga.Id, verifier, new ContextBag()), Is.Not.Null,
            "cancelling a waiter must not release or corrupt the holder's lock");
    }

    [Test]
    public async Task Multiple_lock_cycle_times_out_and_a_surviving_session_can_recover()
    {
        var (persister, options, storage) = CreatePersister(TimeSpan.FromMilliseconds(200));
        var firstSaga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "first" };
        var secondSaga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "second" };
        await SaveSaga(persister, options, storage, firstSaga);
        await SaveSaga(persister, options, storage, secondSaga);

        using var firstSession = await OpenSession(storage, options);
        using var secondSession = await OpenSession(storage, options);
        await persister.Get<TestSagaData>(firstSaga.Id, firstSession, new ContextBag());
        await persister.Get<TestSagaData>(secondSaga.Id, secondSession, new ContextBag());

        var firstWait = persister.Get<TestSagaData>(secondSaga.Id, firstSession, new ContextBag());
        var secondWait = persister.Get<TestSagaData>(firstSaga.Id, secondSession, new ContextBag());

        Assert.Multiple(() =>
        {
            Assert.That(firstWait.IsCompleted, Is.False);
            Assert.That(secondWait.IsCompleted, Is.False);
        });
        Assert.That(async () => await firstWait, Throws.InstanceOf<TimeoutException>());
        Assert.That(async () => await secondWait, Throws.InstanceOf<TimeoutException>());

        firstSession.Dispose();

        var recovered = await persister.Get<TestSagaData>(firstSaga.Id, secondSession, new ContextBag());
        Assert.That(recovered, Is.Not.Null);
    }

    [Test]
    public async Task Disposing_owned_session_releases_lock()
    {
        var (persister, options, storage) = CreatePersister(TimeSpan.FromMilliseconds(200));
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "dispose" };
        await SaveSaga(persister, options, storage, saga);

        var holder = await OpenSession(storage, options);
        await persister.Get<TestSagaData>(saga.Id, holder, new ContextBag());
        holder.Dispose();

        await AssertCanRead(persister, storage, options, saga.Id);
    }

    [Test]
    public async Task Failed_commit_releases_lock()
    {
        var (persister, options, storage) = CreatePersister(TimeSpan.FromMilliseconds(200));
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "failed-commit" };
        await SaveSaga(persister, options, storage, saga);

        using var holder = await OpenSession(storage, options);
        await persister.Get<TestSagaData>(saga.Id, holder, new ContextBag());
        holder.Enlist(new object(), static _ => throw new InvalidOperationException("commit failed"));

        Assert.That(async () => await holder.CompleteAsync(), Throws.InstanceOf<InvalidOperationException>());

        await AssertCanRead(persister, storage, options, saga.Id);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Outbox_outcome_releases_lock(bool commit)
    {
        var (persister, options, storage) = CreatePersister(TimeSpan.FromMilliseconds(200));
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = commit ? "outbox-commit" : "outbox-dispose" };
        await SaveSaga(persister, options, storage, saga);

        using var outboxTransaction = new NonDurableOutboxTransaction();
        using var holder = new NonDurableSynchronizedStorageSession(storage, options);
        Assert.That(await holder.TryOpen(outboxTransaction, new ContextBag()), Is.True);
        await persister.Get<TestSagaData>(saga.Id, holder, new ContextBag());

        if (commit)
        {
            await outboxTransaction.Commit();
        }
        else
        {
            outboxTransaction.Dispose();
        }

        await AssertCanRead(persister, storage, options, saga.Id);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Ambient_transaction_outcome_releases_lock(bool commit)
    {
        var (persister, options, storage) = CreatePersister(TimeSpan.FromMilliseconds(200));
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = commit ? "ambient-commit" : "ambient-rollback" };
        await SaveSaga(persister, options, storage, saga);

        using var transaction = new CommittableTransaction();
        var transportTransaction = new TransportTransaction();
        transportTransaction.Set<Transaction>(transaction);
        using var holder = new NonDurableSynchronizedStorageSession(storage, options);
        Assert.That(await holder.TryOpen(transportTransaction, out _), Is.True);
        await persister.Get<TestSagaData>(saga.Id, holder, new ContextBag());

        if (commit)
        {
            transaction.Commit();
        }
        else
        {
            transaction.Rollback();
        }

        await AssertCanRead(persister, storage, options, saga.Id);
    }

    [Test]
    public void Disposing_testable_session_releases_projection_lock()
    {
        var options = CreateOptions(TimeSpan.FromMilliseconds(200));
        var storage = new NonDurableStorage();
        var saga = new TestSagaData { Id = Guid.NewGuid(), SomeId = "testable" };

        using (var first = new TestableNonDurableSynchronizedStorageSession(storage, options))
        {
            first.AddSaga(saga);
            Assert.That(first.GetSagaData<TestSagaData>(new ContextBag(), static data => data.SomeId == "testable"), Is.Not.Null);
        }

        using var second = new TestableNonDurableSynchronizedStorageSession(storage, options);
        Assert.That(second.GetSagaData<TestSagaData>(new ContextBag(), static data => data.SomeId == "testable"), Is.Not.Null);
    }

    static async Task AssertCanRead(
        NonDurableSagaPersister persister,
        NonDurableStorage storage,
        NonDurableSagaOptions options,
        Guid sagaId)
    {
        using var verifier = await OpenSession(storage, options);
        Assert.That(await persister.Get<TestSagaData>(sagaId, verifier, new ContextBag()), Is.Not.Null);
        await verifier.CompleteAsync();
    }

    static async Task SaveSaga(NonDurableSagaPersister persister, NonDurableSagaOptions options, NonDurableStorage storage, TestSagaData saga)
    {
        using var session = await OpenSession(storage, options);
        await persister.Save(saga, SagaMetadataHelper.GetMetadata<TestSaga>(saga), session, new ContextBag());
        await session.CompleteAsync();
    }

    static async Task<NonDurableSynchronizedStorageSession> OpenSession(NonDurableStorage storage, NonDurableSagaOptions options)
    {
        var session = new NonDurableSynchronizedStorageSession(storage, options);
        await session.Open(new ContextBag());
        return session;
    }

    static (NonDurableSagaPersister Persister, NonDurableSagaOptions Options, NonDurableStorage Storage) CreatePersister(TimeSpan? timeout = null)
    {
        var options = CreateOptions(timeout);
        var storage = new NonDurableStorage();
        return (new NonDurableSagaPersister(storage, options), options, storage);
    }

    static NonDurableSagaOptions CreateOptions(TimeSpan? timeout = null) => new()
    {
        ConcurrencyMode = NonDurableSagaConcurrencyMode.Pessimistic,
        PessimisticLockTimeout = timeout ?? TimeSpan.FromSeconds(5)
    };
}
