namespace NServiceBus.Persistence.NonDurable.PersistenceTests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using System.Transactions;
    using NServiceBus.PersistenceTesting.Sagas;
    using NServiceBus.PersistenceTesting;
    using NServiceBus.Transport;

    public class When_completing_a_saga_with_an_escalated_DTC_transaction : SagaPersisterTests
    {
        [Test]
        public async Task Should_rollback_completed_saga_when_the_dtc_transaction_is_aborted()
        {
            configuration.RequiresDtcSupport();

            var startingSagaData = new TestSagaData
            {
                SomeId = Guid.NewGuid().ToString(),
                LastUpdatedBy = "Unchanged"
            };
            await SaveSaga(startingSagaData);

            // This enlistment notifier emulates a participating DTC transaction that fails to commit.
            var enlistmentNotifier = new EnlistmentNotifier(abortTransaction: true);
            Transaction transaction = null;
            NonDurableSynchronizedStorageSession.EnlistmentNotification enlistmentNotification = null;

            Assert.That(async () =>
            {
                using var tx = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

                Transaction.Current.EnlistDurable(enlistmentNotifier.Id, enlistmentNotifier, EnlistmentOptions.None);
                transaction = Transaction.Current;

                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(Transaction.Current);

                using var session = configuration.CreateStorageSession();
                var contextBag = configuration.GetContextBagForSagaStorage();

                await ((NonDurableSynchronizedStorageSession)session).TryOpen(transportTransaction, out enlistmentNotification);

                var sagaData = await configuration.SagaStorage.Get<TestSagaData>(startingSagaData.Id, session, contextBag);
                await configuration.SagaStorage.Complete(sagaData, session, contextBag);

                await session.CompleteAsync();

                // When the enlistmentNotifier forces a rollback, the persister should also rollback with the rest of the DTC transaction.
                tx.Complete();
            }, Throws.Exception.TypeOf<TransactionAbortedException>());

            await enlistmentNotification.TransactionCompletionSource.Task;

            var unchangedSagaData = await GetById<TestSagaData>(startingSagaData.Id);

            Assert.That(unchangedSagaData, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(unchangedSagaData.Id, Is.EqualTo(startingSagaData.Id));
                Assert.That(unchangedSagaData.SomeId, Is.EqualTo(startingSagaData.SomeId));
                Assert.That(unchangedSagaData.LastUpdatedBy, Is.EqualTo(startingSagaData.LastUpdatedBy));
            });
        }

        public class TestSaga : Saga<TestSagaData>, IAmStartedByMessages<StartMessage>
        {
            public Task Handle(StartMessage message, IMessageHandlerContext context) => throw new NotImplementedException();

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TestSagaData> mapper) => mapper.MapSaga(s => s.SomeId).ToMessage<StartMessage>(msg => msg.SomeId);
        }

        public class TestSagaData : ContainSagaData
        {
            public string SomeId { get; set; }

            public string LastUpdatedBy { get; set; }
        }

        public class StartMessage
        {
            public string SomeId { get; set; }
        }

        class EnlistmentNotifier(bool abortTransaction) : IEnlistmentNotification
        {
            public TaskCompletionSource CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool RollbackWasCalled { get; private set; }

            public bool CommitWasCalled { get; private set; }

            public void Prepare(PreparingEnlistment preparingEnlistment)
            {
                if (!abortTransaction)
                {
                    preparingEnlistment.Prepared();
                }
                else
                {
                    preparingEnlistment.ForceRollback();
                }
            }

            public void Commit(Enlistment enlistment)
            {
                CommitWasCalled = true;
                CompletionSource.SetResult();
                enlistment.Done();
            }

            public void Rollback(Enlistment enlistment)
            {
                RollbackWasCalled = true;
                CompletionSource.SetResult();
                enlistment.Done();
            }

            public void InDoubt(Enlistment enlistment)
            {
                CompletionSource.SetResult();
                enlistment.Done();
            }

            public readonly Guid Id = Guid.NewGuid();
        }

        public When_completing_a_saga_with_an_escalated_DTC_transaction(TestVariant param) : base(param)
        {
        }
    }
}