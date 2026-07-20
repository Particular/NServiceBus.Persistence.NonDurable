namespace NServiceBus.Persistence.NonDurable.Tests
{
    using System;
    using System.Threading;
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
                Id = Guid.NewGuid(),
                SomeId = "Original"
            };
            var persister = new NonDurableSagaPersister();
            var insertSession = new NonDurableSynchronizedStorageSession();
            await insertSession.Open(new ContextBag());
            await persister.Save(saga, SagaMetadataHelper.GetMetadata<TestSaga>(saga), insertSession, new ContextBag());
            await insertSession.CompleteAsync();

            saga.SomeId = "Changed";

            var returnedSaga1 =
                await persister.Get<TestSagaData>(saga.Id, new NonDurableSynchronizedStorageSession(), new ContextBag());
            var returnedSaga2 = await persister.Get<TestSagaData>("SomeId", "Original",
                new NonDurableSynchronizedStorageSession(), new ContextBag());
            Assert.Multiple(() =>
            {
                Assert.That(returnedSaga1, Is.Not.SameAs(returnedSaga2));
                Assert.That(saga, Is.Not.SameAs(returnedSaga1));
                Assert.That(returnedSaga1.SomeId, Is.EqualTo("Original"));
                Assert.That(returnedSaga2.SomeId, Is.EqualTo("Original"));
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
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] was modified by another process."));
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
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] was modified by another process."));
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
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] was modified by another process."));
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
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] was modified by another process."));

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
                        $"NonDurableSagaPersister concurrency violation: saga entity Id[{saga.Id}] was modified by another process."));
        }

        [Test]
        public async Task Pessimistic_get_waits_for_current_holder_and_reads_latest_data()
        {
            var saga = new TestSagaData
            {
                Id = Guid.NewGuid(),
                SomeId = "Original"
            };

            var (persister, options, storage) = CreatePessimisticPersister();
            await SaveSaga(persister, options, storage, saga);

            var firstSession = new NonDurableSynchronizedStorageSession(storage);
            await firstSession.Open(new ContextBag());
            var firstContext = new ContextBag();

            var secondSession = new NonDurableSynchronizedStorageSession(storage);
            await secondSession.Open(new ContextBag());
            var secondContext = new ContextBag();

            try
            {
                var firstSaga = await persister.Get<TestSagaData>(saga.Id, firstSession, firstContext);

                var secondGetTask = Task.Run(async () =>
                    await persister.Get<TestSagaData>(saga.Id, secondSession, secondContext));

                Assert.That(await Task.WhenAny(secondGetTask, Task.Delay(200)), Is.Not.SameAs(secondGetTask));

                firstSaga.SomeId = "Updated";
                await persister.Update(firstSaga, firstSession, firstContext);
                await firstSession.CompleteAsync();

                var secondSaga = await secondGetTask;
                Assert.That(secondSaga.SomeId, Is.EqualTo("Updated"));

                await secondSession.CompleteAsync();
            }
            finally
            {
                firstSession.Dispose();
                secondSession.Dispose();
            }
        }

        [Test]
        public async Task Pessimistic_get_times_out_when_lock_is_held_too_long()
        {
            var saga = new TestSagaData
            {
                Id = Guid.NewGuid(),
                SomeId = "TimedOut"
            };

            var options = new NonDurableSagaOptions
            {
                ConcurrencyMode = NonDurableSagaConcurrencyMode.Pessimistic,
                PessimisticLockTimeout = TimeSpan.FromMilliseconds(200)
            };
            var storage = new NonDurableStorage();
            var persister = new NonDurableSagaPersister(storage, options);
            await SaveSaga(persister, options, storage, saga);

            var firstSession = new NonDurableSynchronizedStorageSession(storage);
            await firstSession.Open(new ContextBag());
            var firstContext = new ContextBag();

            var waitingSession = new NonDurableSynchronizedStorageSession(storage, options);
            await waitingSession.Open(new ContextBag());

            try
            {
                await persister.Get<TestSagaData>(saga.Id, firstSession, firstContext);

                Assert.That(
                    async () => await persister.Get<TestSagaData>(saga.Id, waitingSession, new ContextBag(), CancellationToken.None),
                    Throws.InstanceOf<TimeoutException>().And.Message.Contains("timed out"));
            }
            finally
            {
                firstSession.Dispose();
                waitingSession.Dispose();
            }
        }

        [Test]
        public async Task Shared_storage_allows_mixed_saga_concurrency_modes()
        {
            var storage = new NonDurableStorage();
            var pessimisticOptions = new NonDurableSagaOptions
            {
                ConcurrencyMode = NonDurableSagaConcurrencyMode.Pessimistic
            };
            var optimisticOptions = new NonDurableSagaOptions
            {
                ConcurrencyMode = NonDurableSagaConcurrencyMode.Optimistic
            };

            var pessimisticPersister = new NonDurableSagaPersister(storage, pessimisticOptions);
            var optimisticPersister = new NonDurableSagaPersister(storage, optimisticOptions);

            var pessimisticSaga = new TestSagaData
            {
                Id = Guid.NewGuid(),
                SomeId = "Pessimistic"
            };
            var optimisticSaga = new TestSagaData
            {
                Id = Guid.NewGuid(),
                SomeId = "Optimistic"
            };

            await SaveSaga(pessimisticPersister, pessimisticOptions, storage, pessimisticSaga);
            await SaveSaga(optimisticPersister, optimisticOptions, storage, optimisticSaga);

            var pessimisticRead = await optimisticPersister.Get<TestSagaData>(
                pessimisticSaga.Id,
                new NonDurableSynchronizedStorageSession(storage, optimisticOptions),
                new ContextBag());

            var optimisticRead = await pessimisticPersister.Get<TestSagaData>(
                optimisticSaga.Id,
                new NonDurableSynchronizedStorageSession(storage, pessimisticOptions),
                new ContextBag());

            Assert.Multiple(() =>
            {
                Assert.That(pessimisticRead.SomeId, Is.EqualTo("Pessimistic"));
                Assert.That(optimisticRead.SomeId, Is.EqualTo("Optimistic"));
            });
        }

        static async Task SaveSaga(NonDurableSagaPersister persister, NonDurableSagaOptions options, NonDurableStorage storage, TestSagaData saga)
        {
            var session = new NonDurableSynchronizedStorageSession(storage, options);
            await session.Open(new ContextBag());

            try
            {
                await persister.Save(saga, SagaMetadataHelper.GetMetadata<TestSaga>(saga), session, new ContextBag());
                await session.CompleteAsync();
            }
            finally
            {
                session.Dispose();
            }
        }

        static (NonDurableSagaPersister Persister, NonDurableSagaOptions Options, NonDurableStorage Storage) CreatePessimisticPersister()
        {
            var options = new NonDurableSagaOptions
            {
                ConcurrencyMode = NonDurableSagaConcurrencyMode.Pessimistic,
                PessimisticLockTimeout = TimeSpan.FromSeconds(5)
            };
            var storage = new NonDurableStorage();
            var persister = new NonDurableSagaPersister(storage, options);
            return (persister, options, storage);
        }
    }
}