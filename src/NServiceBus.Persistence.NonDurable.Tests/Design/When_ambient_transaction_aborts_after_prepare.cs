namespace NServiceBus.Persistence.NonDurable.Tests.Design;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Extensibility;
using NUnit.Framework;
using Sagas;

// Validates the DTC two-phase-commit rollback semantics without requiring a real ambient
// TransactionScope (which is Windows-gated in the PersistenceTests suite).
//
// NonDurableSynchronizedStorageSession.EnlistmentNotification drives the storage transaction
// through exactly two calls: Commit() during the Prepare phase, then Rollback() when the
// ambient/DTC transaction is aborted. These tests invoke that same sequence directly against
// the storage transaction and against the real saga persister, so the behavior is covered
// on every platform.
[TestFixture]
public class When_ambient_transaction_aborts_after_prepare
{
    [Test]
    public void Should_undo_operations_applied_during_prepare_when_rolled_back_after_commit()
    {
        var transaction = new NonDurableStorageTransaction();
        var store = new ConcurrentDictionary<string, string>();

        transaction.Enlist(
            new State(store, "key", "committed"),
            static state => state.Store[state.Key] = state.Value,
            static state => RemoveFromStore(state));

        // Prepare phase: apply the staged operations.
        transaction.Commit();
        Assert.That(store["key"], Is.EqualTo("committed"), "operation should be applied during prepare");

        // Abort phase: the ambient/DTC transaction is aborted after a successful prepare.
        transaction.Rollback();
        Assert.That(store.TryGetValue("key", out _), Is.False, "applied operation should be undone on rollback");
    }

    [Test]
    public void Should_run_rollback_callbacks_when_never_committed()
    {
        var transaction = new NonDurableStorageTransaction();
        var rollbackCount = 0;

        transaction.Enlist(rollbackCount, static _ => { }, _ => rollbackCount++);

        transaction.Rollback();

        Assert.That(rollbackCount, Is.EqualTo(1));
    }

    [Test]
    public void Should_not_apply_operations_after_rollback()
    {
        var transaction = new NonDurableStorageTransaction();
        var store = new ConcurrentDictionary<string, string>();

        transaction.Enlist(
            new State(store, "key", "committed"),
            static state => state.Store[state.Key] = state.Value,
            static state => RemoveFromStore(state));

        transaction.Rollback();
        transaction.Commit();

        Assert.That(store.TryGetValue("key", out _), Is.False, "a rolled-back transaction must not be committed later");
    }

    [Test]
    public void Should_be_idempotent_when_rollback_called_more_than_once()
    {
        var transaction = new NonDurableStorageTransaction();
        var rollbackCount = 0;

        transaction.Enlist(rollbackCount, static _ => { }, _ => rollbackCount++);

        transaction.Commit();
        transaction.Rollback();
        transaction.Rollback(); // second call must not re-run callbacks

        Assert.That(rollbackCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Should_undo_operations_when_commit_and_rollback_happen_on_different_threads()
    {
        var transaction = new NonDurableStorageTransaction();
        var store = new ConcurrentDictionary<string, string>();

        transaction.Enlist(
            new State(store, "key", "committed"),
            static state => state.Store[state.Key] = state.Value,
            static state => RemoveFromStore(state));

        await Task.Run(transaction.Commit);
        Assert.That(store["key"], Is.EqualTo("committed"), "operation should be applied during prepare");

        await Task.Run(transaction.Rollback);
        Assert.That(store.TryGetValue("key", out _), Is.False, "applied operation should be undone on rollback");
    }

    [Test]
    public async Task Should_undo_saga_update_when_prepare_applied_then_transaction_aborted()
    {
        var persister = new NonDurableSagaPersister();

        var saga = new Saga { Id = Guid.NewGuid(), SomeId = "x", LastUpdatedBy = "Unchanged" };
        var correlation = new SagaCorrelationProperty(nameof(Saga.SomeId), saga.SomeId);

        // Persist the saga outside the (simulated) DTC scope.
        var saveSession = new NonDurableSynchronizedStorageSession();
        var saveCtx = new ContextBag();
        await saveSession.Open(saveCtx);
        await persister.Save(saga, correlation, saveSession, saveCtx);
        await saveSession.CompleteAsync();

        // Simulate a DTC-participating session and drive it through the exact EnlistmentNotification
        // sequence: Commit() during Prepare, Rollback() when the ambient transaction is aborted.
        var session = new NonDurableSynchronizedStorageSession();
        var ctx = new ContextBag();
        await session.Open(ctx);

        var loaded = await persister.Get<Saga>(saga.Id, session, ctx);
        loaded.LastUpdatedBy = "Changed";
        await persister.Update(loaded, session, ctx);

        // Prepare phase.
        Assert.That(session.Transaction, Is.Not.Null);
        session.Transaction!.Commit();
        Assert.That((await ReadBack(persister, saga.Id)).LastUpdatedBy, Is.EqualTo("Changed"),
            "prepare should apply the saga update");

        // Abort phase: the update applied during prepare must be undone.
        session.Transaction!.Rollback();

        var afterAbort = await ReadBack(persister, saga.Id);
        Assert.That(afterAbort.LastUpdatedBy, Is.EqualTo("Unchanged"),
            "abort should undo the saga update applied during prepare");
    }

    [Test]
    public async Task Should_restore_saga_when_completed_then_transaction_aborted()
    {
        var persister = new NonDurableSagaPersister();

        var saga = new Saga { Id = Guid.NewGuid(), SomeId = "x", LastUpdatedBy = "Unchanged" };
        var correlation = new SagaCorrelationProperty(nameof(Saga.SomeId), saga.SomeId);

        var saveSession = new NonDurableSynchronizedStorageSession();
        var saveCtx = new ContextBag();
        await saveSession.Open(saveCtx);
        await persister.Save(saga, correlation, saveSession, saveCtx);
        await saveSession.CompleteAsync();

        var session = new NonDurableSynchronizedStorageSession();
        var ctx = new ContextBag();
        await session.Open(ctx);

        await persister.Get<Saga>(saga.Id, session, ctx);
        await persister.Complete(saga, session, ctx);

        session.Transaction!.Commit();
        Assert.That(await ReadBack(persister, saga.Id), Is.Null, "prepare should remove the saga");

        session.Transaction!.Rollback();
        var afterAbort = await ReadBack(persister, saga.Id);
        Assert.That(afterAbort, Is.Not.Null, "abort should restore the completed saga");
        Assert.That(afterAbort!.LastUpdatedBy, Is.EqualTo("Unchanged"));
    }

    static async Task<Saga> ReadBack(NonDurableSagaPersister persister, Guid id)
    {
        var s = new NonDurableSynchronizedStorageSession();
        var c = new ContextBag();
        await s.Open(c);
        var d = await persister.Get<Saga>(id, s, c);
        await s.CompleteAsync();
        return d;
    }

    static void RemoveFromStore(State state) => state.Store.TryRemove(state.Key, out _);

    readonly record struct State(ConcurrentDictionary<string, string> Store, string Key, string Value);

    public class Saga : ContainSagaData
    {
        public string SomeId { get; set; }
        public string LastUpdatedBy { get; set; }
    }
}