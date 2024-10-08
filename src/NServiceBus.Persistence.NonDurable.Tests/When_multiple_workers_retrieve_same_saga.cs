﻿namespace NServiceBus.Persistence.NonDurable.Tests
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;

    [TestFixture]
    class When_multiple_workers_retrieve_same_saga
    {
        [Test]
        public async Task Persister_returns_different_instance_of_saga_data()
        {
            var saga = new TestSagaData
            {
                Id = Guid.NewGuid()
            };
            var persister = new NonDurableSagaPersister();
            var insertSession = new NonDurableSynchronizedStorageSession();
            await insertSession.Open(new ContextBag());
            await persister.Save(saga, SagaMetadataHelper.GetMetadata<TestSaga>(saga), insertSession, new ContextBag());
            await insertSession.CompleteAsync();

            var returnedSaga1 =
                await persister.Get<TestSagaData>(saga.Id, new NonDurableSynchronizedStorageSession(), new ContextBag());
            var returnedSaga2 = await persister.Get<TestSagaData>("Id", saga.Id,
                new NonDurableSynchronizedStorageSession(), new ContextBag());
            Assert.Multiple(() =>
            {
                Assert.That(returnedSaga1, Is.Not.SameAs(returnedSaga2));
                Assert.That(saga, Is.Not.SameAs(returnedSaga1));
            });
            Assert.That(saga, Is.Not.SameAs(returnedSaga2));
        }

        [Test]
        public async Task Save_fails_when_data_changes_between_read_and_update()
        {
            var sagaId = Guid.NewGuid();
            var saga = new TestSagaData
            {
                Id = sagaId,
                SomeId = sagaId.ToString()
            };
            var persister = new NonDurableSagaPersister();
            var insertSession = new NonDurableSynchronizedStorageSession();
            await insertSession.Open(new ContextBag());
            await persister.Save(saga, SagaMetadataHelper.GetMetadata<TestSaga>(saga), insertSession, new ContextBag());
            await insertSession.CompleteAsync();

            var winningContext = new ContextBag();
            var losingContext = new ContextBag();
            var returnedSaga1 = await Task.Run(() =>
                persister.Get<TestSagaData>(saga.Id, new NonDurableSynchronizedStorageSession(), winningContext));
            var returnedSaga2 = await persister.Get<TestSagaData>("SomeId", sagaId.ToString(),
                new NonDurableSynchronizedStorageSession(), losingContext);

            var winningSaveSession = new NonDurableSynchronizedStorageSession();
            await winningSaveSession.Open(new ContextBag());
            var losingSaveSession = new NonDurableSynchronizedStorageSession();
            await losingSaveSession.Open(new ContextBag());

            await persister.Update(returnedSaga1, winningSaveSession, winningContext);
            await persister.Update(returnedSaga2, losingSaveSession, losingContext);

            await winningSaveSession.CompleteAsync();

            Assert.That(async () => await losingSaveSession.CompleteAsync(),
                Throws.InstanceOf<Exception>().And.Message
                    .StartsWith(
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] already saved."));
        }

        [Test]
        public async Task Save_fails_when_data_changes_between_read_and_update_on_same_thread()
        {
            var sagaId = Guid.NewGuid();
            var saga = new TestSagaData
            {
                Id = sagaId,
                SomeId = sagaId.ToString()
            };
            var persister = new NonDurableSagaPersister();
            var insertSession = new NonDurableSynchronizedStorageSession();
            await insertSession.Open(new ContextBag());
            await persister.Save(saga, SagaMetadataHelper.GetMetadata<TestSaga>(saga), insertSession, new ContextBag());
            await insertSession.CompleteAsync();

            var winningContext = new ContextBag();
            var record =
                await persister.Get<TestSagaData>(saga.Id, new NonDurableSynchronizedStorageSession(), winningContext);
            var losingContext = new ContextBag();
            var staleRecord = await persister.Get<TestSagaData>("SomeId", sagaId.ToString(),
                new NonDurableSynchronizedStorageSession(), losingContext);

            var winningSaveSession = new NonDurableSynchronizedStorageSession();
            await winningSaveSession.Open(new ContextBag());
            var losingSaveSession = new NonDurableSynchronizedStorageSession();
            await losingSaveSession.Open(new ContextBag());

            await persister.Update(record, winningSaveSession, winningContext);
            await persister.Update(staleRecord, losingSaveSession, losingContext);

            await winningSaveSession.CompleteAsync();

            Assert.That(async () => await losingSaveSession.CompleteAsync(),
                Throws.InstanceOf<Exception>().And.Message
                    .StartsWith(
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] already saved."));
        }

        [Test]
        public async Task Save_fails_when_writing_same_data_twice()
        {
            var saga = new TestSagaData
            {
                Id = Guid.NewGuid()
            };
            var persister = new NonDurableSagaPersister();
            var insertSession = new NonDurableSynchronizedStorageSession();
            await insertSession.Open(new ContextBag());
            await persister.Save(saga, SagaMetadataHelper.GetMetadata<TestSaga>(saga), insertSession, new ContextBag());
            await insertSession.CompleteAsync();

            var retrievingContext = new ContextBag();
            var returnedSaga1 =
                await persister.Get<TestSagaData>(saga.Id, new NonDurableSynchronizedStorageSession(), retrievingContext);

            var winningSaveSession = new NonDurableSynchronizedStorageSession();
            await winningSaveSession.Open(new ContextBag());
            var losingSaveSession = new NonDurableSynchronizedStorageSession();
            await losingSaveSession.Open(new ContextBag());

            await persister.Update(returnedSaga1, winningSaveSession, retrievingContext);
            await persister.Update(returnedSaga1, losingSaveSession, retrievingContext);

            await winningSaveSession.CompleteAsync();

            Assert.That(async () => await losingSaveSession.CompleteAsync(),
                Throws.InstanceOf<Exception>().And.Message
                    .StartsWith(
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] already saved."));
        }

        [Test]
        public async Task Save_process_is_repeatable()
        {
            var sagaId = Guid.NewGuid();
            var saga = new TestSagaData
            {
                Id = sagaId,
                SomeId = sagaId.ToString()
            };
            var persister = new NonDurableSagaPersister();
            var insertSession = new NonDurableSynchronizedStorageSession();
            await insertSession.Open(new ContextBag());
            await persister.Save(saga, SagaMetadataHelper.GetMetadata<TestSaga>(saga), insertSession, new ContextBag());
            await insertSession.CompleteAsync();

            var winningSessionContext = new ContextBag();
            var returnedSaga1 = await Task.Run(() =>
                persister.Get<TestSagaData>(saga.Id, new NonDurableSynchronizedStorageSession(), winningSessionContext));

            var losingSessionContext = new ContextBag();
            var returnedSaga2 = await persister.Get<TestSagaData>("SomeId", sagaId.ToString(),
                new NonDurableSynchronizedStorageSession(), losingSessionContext);

            var winningSaveSession = new NonDurableSynchronizedStorageSession();
            await winningSaveSession.Open(new ContextBag());
            var losingSaveSession = new NonDurableSynchronizedStorageSession();
            await losingSaveSession.Open(new ContextBag());

            await persister.Update(returnedSaga1, winningSaveSession, winningSessionContext);
            await persister.Update(returnedSaga2, losingSaveSession, losingSessionContext);

            await winningSaveSession.CompleteAsync();
            Assert.That(async () => await losingSaveSession.CompleteAsync(),
                Throws.InstanceOf<Exception>().And.Message
                    .StartsWith(
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] already saved."));

            losingSessionContext = new ContextBag();
            var returnedSaga3 = await Task.Run(() => persister.Get<TestSagaData>("SomeId", sagaId.ToString(),
                new NonDurableSynchronizedStorageSession(), losingSessionContext));

            winningSessionContext = new ContextBag();
            var returnedSaga4 = await persister.Get<TestSagaData>(saga.Id, new NonDurableSynchronizedStorageSession(),
                winningSessionContext);

            winningSaveSession = new NonDurableSynchronizedStorageSession();
            await winningSaveSession.Open(new ContextBag());
            losingSaveSession = new NonDurableSynchronizedStorageSession();
            await losingSaveSession.Open(new ContextBag());

            await persister.Update(returnedSaga4, winningSaveSession, winningSessionContext);
            await persister.Update(returnedSaga3, losingSaveSession, losingSessionContext);

            await winningSaveSession.CompleteAsync();

            Assert.That(async () => await losingSaveSession.CompleteAsync(),
                Throws.InstanceOf<Exception>().And.Message
                    .StartsWith(
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] already saved."));
        }
    }
}