namespace NServiceBus.Persistence.NonDurable.Tests;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Extensibility;
using NServiceBus.Outbox;
using NServiceBus.Sagas;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using NServiceBus.Persistence.NonDurable.SubscriptionStorage;
using NUnit.Framework;

[TestFixture]
public class When_emitting_persistence_spans
{
    [Test]
    public async Task Should_create_saga_spans_and_transaction_events()
    {
        var persister = new NonDurableSagaPersister();
        using var listener = new TestingActivityListener(NonDurablePersistenceTracing.ActivitySourceName);
        using var root = StartRootActivity();
        var session = new NonDurableSynchronizedStorageSession();
        var context = new ContextBag();
        var sagaData = new TestSagaData { Id = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), Value = "first" };
        var correlation = new SagaCorrelationProperty(nameof(TestSagaData.CorrelationId), sagaData.CorrelationId);

        await session.Open(context);
        await persister.Save(sagaData, correlation, session, context);
        await session.CompleteAsync();
        var loadedSaga = await persister.Get<TestSagaData>(sagaData.Id, session, context);

        Assert.That(loadedSaga, Is.Not.Null);

        sagaData.Value = "second";
        await session.Open(context);
        await persister.Get<TestSagaData>(sagaData.Id, session, context);
        await persister.Update(sagaData, session, context);
        await session.CompleteAsync();

        await session.Open(context);
        await persister.Get<TestSagaData>(sagaData.Id, session, context);
        await persister.Complete(sagaData, session, context);
        await session.CompleteAsync();
        root.Stop();

        var activities = listener.CompletedFrom(NonDurablePersistenceTracing.ActivitySourceName);

        using (Assert.EnterMultipleScope())
        {
            // Span names are under test (part of public API)
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaSaveActivityName), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaGetByIdActivityName && Equals(a.GetTagItem("nservicebus.persistence.result"), "hit")), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaUpdateActivityName), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaCompleteActivityName), Is.True);

            // Parent/child linkage — spans should be children of the root activity
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaSaveActivityName && a.ParentId == root.Id), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaGetByIdActivityName && a.ParentId == root.Id), Is.True);

            // Status deferred to transaction commit (option b) — should be Ok after commit
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaSaveActivityName && a.Status == ActivityStatusCode.Ok), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaUpdateActivityName && a.Status == ActivityStatusCode.Ok), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaCompleteActivityName && a.Status == ActivityStatusCode.Ok), Is.True);

            // Transaction lifecycle events
            Assert.That(activities.Any(a => a.Events.Any(e => e.Name == "nondurable.persistence.transaction.enlisted")), Is.True);
            Assert.That(activities.Any(a => a.Events.Any(e => e.Name == "nondurable.persistence.transaction.committed")), Is.True);
        }
    }

    [Test]
    public async Task Should_create_outbox_spans()
    {
        var storage = new NonDurableStorage();
        var outbox = new NonDurableOutboxStorage("test-endpoint", storage);
        using var listener = new TestingActivityListener(NonDurablePersistenceTracing.ActivitySourceName);
        using var root = StartRootActivity();
        var context = new ContextBag();
        await using var transaction = await outbox.BeginTransaction(context);
        var outboxMessage = new OutboxMessage("message-id", []);

        await outbox.Store(outboxMessage, transaction, context);
        await transaction.Commit();
        await outbox.Get("message-id", context);
        await outbox.SetAsDispatched("message-id", context);
        root.Stop();

        var activities = listener.CompletedFrom(NonDurablePersistenceTracing.ActivitySourceName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.OutboxBeginTransactionActivityName), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.OutboxStoreActivityName), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.OutboxGetActivityName && Equals(a.GetTagItem("nservicebus.persistence.result"), "hit")), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.OutboxSetAsDispatchedActivityName && a.Events.Any(e => e.Name == "nondurable.persistence.marked_dispatched")), Is.True);

            // Store span status deferred to outbox transaction commit
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.OutboxStoreActivityName && a.Status == ActivityStatusCode.Ok), Is.True);
        }
    }

    [Test]
    public async Task Should_create_subscription_spans()
    {
        var storage = new NonDurableStorage();
        var subscriptionStorage = new NonDurableSubscriptionStorage(storage);
        using var listener = new TestingActivityListener(NonDurablePersistenceTracing.ActivitySourceName);
        using var root = StartRootActivity();
        var context = new ContextBag();
        var messageType = new MessageType(typeof(TestEvent));
        var subscriber = new Subscriber("endpoint-a", "EndpointA");

        await subscriptionStorage.Subscribe(subscriber, messageType, context);
        var subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage([messageType], context);
        await subscriptionStorage.Unsubscribe(subscriber, messageType, context);
        root.Stop();

        var activities = listener.CompletedFrom(NonDurablePersistenceTracing.ActivitySourceName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SubscriptionSubscribeActivityName), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SubscriptionGetSubscribersActivityName && a.Events.Any(e => e.Name == "nondurable.persistence.resolved_subscribers")), Is.True);
            Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SubscriptionUnsubscribeActivityName), Is.True);
            Assert.That(subscribers.Single().TransportAddress, Is.EqualTo("endpoint-a"));
        }
    }

    [Test]
    public void Should_emit_rollback_event_when_storage_transaction_fails()
    {
        var transaction = new NonDurableStorageTransaction();
        using var root = StartRootActivity();

        // The activity is passed explicitly to Enlist (no Activity.Current reliance).
        transaction.Enlist(new object(), static _ => { }, activity: root);
        transaction.Enlist(new InvalidOperationException("boom"), static state => throw state);

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());

        root.Stop();

        Assert.That(root.Events.Any(e => e.Name == "nondurable.persistence.transaction.rolled_back"), Is.True);
    }

    [Test]
    public async Task Should_mark_enlisted_span_as_error_when_transaction_abandoned()
    {
        var persister = new NonDurableSagaPersister();
        using var listener = new TestingActivityListener(NonDurablePersistenceTracing.ActivitySourceName);
        using var root = StartRootActivity();
        var session = new NonDurableSynchronizedStorageSession();
        var context = new ContextBag();
        var sagaData = new TestSagaData { Id = Guid.NewGuid(), Value = "first" };

        // Open + Save enlists an activity, but do NOT call CompleteAsync.
        // Disposing the session should dispose the activity with Error status.
        await session.Open(context);
        await persister.Save(sagaData, SagaCorrelationProperty.None, session, context);
        session.Dispose();
        root.Stop();

        var activities = listener.CompletedFrom(NonDurablePersistenceTracing.ActivitySourceName);

        Assert.That(activities.Any(a => a.OperationName == NonDurablePersistenceTracing.SagaSaveActivityName
                                        && a.Status == ActivityStatusCode.Error), Is.True);
    }

    static Activity StartRootActivity()
    {
        var activity = new Activity("root");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        return activity;
    }

    public class TestSagaData : ContainSagaData
    {
        public Guid CorrelationId { get; set; }

        public string Value { get; set; }
    }

    public class TestEvent : IEvent;
}