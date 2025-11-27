namespace NServiceBus.Persistence.NonDurable.PersistenceTests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using System.Transactions;
    using NServiceBus.PersistenceTesting.Sagas;
    using NServiceBus.PersistenceTesting;
    using NServiceBus.Transport;

    public class When_saving_a_saga_with_an_escalated_DTC_transaction : SagaPersisterTests
    {
        [Test]
        public async Task Should_rollback_new_saga_when_the_dtc_transaction_is_aborted()
        {
            configuration.RequiresDtcSupport();

            // This enlistment notifier emulates a participating DTC transaction that fails to commit.
            var enlistmentNotifier = new EnlistmentNotifier(abortTransaction: true);
            Transaction transaction = null;
            NonDurableSynchronizedStorageSession.EnlistmentNotification enlistmentNotification = null;

            var newSagaData = new TestSagaData() { SomeId = Guid.NewGuid().ToString() };

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
                await SaveSagaWithSession(newSagaData, session, contextBag);

                await session.CompleteAsync();

                // When the enlistmentNotifier forces a rollback, the persister should also rollback with the rest of the DTC transaction.
                tx.Complete();
            }, Throws.Exception.TypeOf<TransactionAbortedException>());

            await enlistmentNotification.TransactionCompletionSource.Task;

            var notFoundSagaData = await GetById<TestSagaData>(newSagaData.Id);

            Assert.That(notFoundSagaData, Is.Null);
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
                enlistment.Done();
                CompletionSource.SetResult();
            }

            public void Rollback(Enlistment enlistment)
            {
                RollbackWasCalled = true;
                enlistment.Done();
                CompletionSource.SetResult();
            }

            public void InDoubt(Enlistment enlistment)
            {
                CompletionSource.SetResult();
                enlistment.Done();
            }

            public readonly Guid Id = Guid.NewGuid();
        }

        public When_saving_a_saga_with_an_escalated_DTC_transaction(TestVariant param) : base(param)
        {
        }
    }
}