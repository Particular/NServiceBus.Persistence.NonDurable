namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Persistence;
using SagaPersister;
using Sagas;

class NonDurableSagaPersister : ISagaPersister
{
    public NonDurableSagaPersister(NonDurableStorage storage, NonDurableSagaOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.PessimisticLockTimeout, TimeSpan.Zero);

        sagas = storage.Sagas;
        byCorrelationId = storage.SagaCorrelationIds;
    }

    // Simplified constructor for testing purposes, which creates a new NonDurableStorage instance.
    public NonDurableSagaPersister()
    {
        options = new NonDurableSagaOptions();
        var storage = new NonDurableStorage();
        sagas = storage.Sagas;
        byCorrelationId = storage.SagaCorrelationIds;
    }

    public Task Save(IContainSagaData sagaData, SagaCorrelationProperty correlationProperty, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
    {
        // Disposal must be deferred until the transaction completes or is disposed
        var activity = NonDurablePersistenceTracing.StartSagaSave(sagaData.Id);
        try
        {
            var correlationId = correlationProperty != SagaCorrelationProperty.None
                ? new CorrelationId(sagaData.GetType(), correlationProperty)
                : NoCorrelationId;
            var entry = new SagaEntry(sagaData, correlationId, version: 1, options.ConcurrencyMode, options.JsonSerializerOptions);

            ((NonDurableSynchronizedStorageSession)session).Enlist(
                new SaveOperationState(sagas, byCorrelationId, sagaData.Id, correlationId, entry),
                static state =>
                {
                    if (!state.CorrelationId.Equals(NoCorrelationId)
                        && !state.ByCorrelationId.TryAdd(state.CorrelationId, state.SagaId))
                    {
                        throw new InvalidOperationException($"The saga with the correlation id already exists");
                    }

                    if (!state.Sagas.TryAdd(state.SagaId, state.Entry))
                    {
                        if (!state.CorrelationId.Equals(NoCorrelationId))
                        {
                            state.ByCorrelationId.TryRemove(new KeyValuePair<CorrelationId, Guid>(state.CorrelationId, state.SagaId));
                        }

                        throw new Exception("A saga with this identifier already exists. This should never happen as saga identifiers are meant to be unique.");
                    }
                },
                static state =>
                {
                    state.Sagas.TryRemove(new KeyValuePair<Guid, SagaEntry>(state.SagaId, state.Entry));

                    if (!state.CorrelationId.Equals(NoCorrelationId))
                    {
                        state.ByCorrelationId.TryRemove(new KeyValuePair<CorrelationId, Guid>(state.CorrelationId, state.SagaId));
                    }
                },
                activity);
            NonDurablePersistenceTracing.AddStagedEvent(activity);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            NonDurablePersistenceTracing.MarkError(activity, ex, exceptionEscaped: true);
            activity?.Dispose();
            throw;
        }
    }

    public async Task<TSagaData> Get<TSagaData>(Guid sagaId, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        using var activity = NonDurablePersistenceTracing.StartSagaGetById(sagaId);
        var sagaData = await SagaReadLocking.ReadCurrent(
            sagas,
            (INonDurableSagaLockingSession)session,
            () => sagas.TryGetValue(sagaId, out var entry)
                ? new(sagaId, entry)
                : null,
            static currentEntry => (TSagaData)currentEntry.GetSagaCopy(),
            (capturedSagaId, capturedEntry) => SetEntry(context, capturedSagaId, capturedEntry),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (sagaData is not null)
        {
            NonDurablePersistenceTracing.AddHitEvent(activity);
            NonDurablePersistenceTracing.MarkSuccess(activity);
            return sagaData;
        }

        NonDurablePersistenceTracing.AddMissEvent(activity);
        NonDurablePersistenceTracing.MarkSuccess(activity);
        return default!;
    }

    public async Task<TSagaData> Get<TSagaData>(string propertyName, object propertyValue, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        using var activity = NonDurablePersistenceTracing.StartSagaGetByProperty(typeof(TSagaData), propertyName, propertyValue);
        var key = new CorrelationId(typeof(TSagaData), propertyName, propertyValue);

        var sagaData = await SagaReadLocking.ReadCurrent(
            sagas,
            (INonDurableSagaLockingSession)session,
            () => byCorrelationId.TryGetValue(key, out var id) && sagas.TryGetValue(id, out var entry)
                ? new(id, entry)
                : null,
            currentEntry => currentEntry.CorrelationId.Equals(key)
                ? (TSagaData)currentEntry.GetSagaCopy()
                : null,
            (capturedSagaId, capturedEntry) => SetEntry(context, capturedSagaId, capturedEntry),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (sagaData is not null)
        {
            NonDurablePersistenceTracing.AddHitEvent(activity);
            NonDurablePersistenceTracing.MarkSuccess(activity);
            return sagaData;
        }

        NonDurablePersistenceTracing.AddMissEvent(activity);
        NonDurablePersistenceTracing.MarkSuccess(activity);
        return default!;
    }

    public Task Update(IContainSagaData sagaData, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
    {
        // Disposal must be deferred until the transaction completes or is disposed
        var activity = NonDurablePersistenceTracing.StartSagaUpdate(sagaData.Id);
        try
        {
            var entry = GetEntry(context, sagaData.Id);
            var updatedEntry = entry.UpdateTo(sagaData, options.JsonSerializerOptions);

            ((NonDurableSynchronizedStorageSession)session).Enlist(
                new UpdateOperationState(sagas, sagaData.Id, entry, updatedEntry),
                static state =>
                {
                    if (!state.Sagas.TryUpdate(state.SagaId, state.UpdatedEntry, state.Entry))
                    {
                        throw new Exception($"NonDurableSagaPersister concurrency violation: saga entity Id[{state.SagaId}] was modified by another process.");
                    }
                },
                static state =>
                {
                    // Restore the original entry by reading the live value and swapping it back.
                    // Comparing against the live value (rather than the captured updated entry) keeps
                    // rollback correct under DTC two-phase commit, where the prepare and rollback
                    // phases are driven by the distributed transaction coordinator on a separate thread.
                    if (state.Sagas.TryGetValue(state.SagaId, out var currentEntry))
                    {
                        state.Sagas.TryUpdate(state.SagaId, state.Entry, currentEntry);
                    }
                },
                activity);
            NonDurablePersistenceTracing.AddStagedEvent(activity);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            NonDurablePersistenceTracing.MarkError(activity, ex, exceptionEscaped: true);
            activity?.Dispose();
            throw;
        }
    }

    public Task Complete(IContainSagaData sagaData, ISynchronizedStorageSession session, ContextBag context, CancellationToken cancellationToken = default)
    {
        // Disposal must be deferred until the transaction completes or is disposed
        var activity = NonDurablePersistenceTracing.StartSagaComplete(sagaData.Id);
        try
        {
            var entry = GetEntry(context, sagaData.Id);
            var completionPendingEntry = entry.MarkCompletionPending();
            var synchronizedStorageSession = (NonDurableSynchronizedStorageSession)session;
            var operationState = new CompleteOperationState(sagas, byCorrelationId, sagaData.Id, entry, completionPendingEntry);

            synchronizedStorageSession.Enlist(
                operationState,
                static state =>
                {
                    if (!state.Sagas.TryUpdate(state.SagaId, state.CompletionPendingEntry, state.Entry))
                    {
                        throw new Exception("Saga can't be completed as it was updated by another process.");
                    }
                },
                static state => state.Sagas.TryUpdate(state.SagaId, state.Entry, state.CompletionPendingEntry),
                activity);

            // The completion marker keeps the saga ID, correlation ID and lock lineage reserved
            // through ambient prepare. A committed transaction leaves the marker in place for this
            // callback to remove; rollback restores the original entry before this callback runs.
            synchronizedStorageSession.OnCompleted(() =>
            {
                if (operationState.Sagas.TryRemove(new KeyValuePair<Guid, SagaEntry>(operationState.SagaId, operationState.CompletionPendingEntry))
                    && !operationState.Entry.CorrelationId.Equals(NoCorrelationId))
                {
                    operationState.ByCorrelationId.TryRemove(new KeyValuePair<CorrelationId, Guid>(operationState.Entry.CorrelationId, operationState.SagaId));
                }
            });
            NonDurablePersistenceTracing.AddStagedEvent(activity);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            NonDurablePersistenceTracing.MarkError(activity, ex, exceptionEscaped: true);
            activity?.Dispose();
            throw;
        }
    }

    internal static void SetEntry(ContextBag context, Guid sagaId, SagaEntry value)
    {
        if (!context.TryGet(ContextKey, out Dictionary<Guid, SagaEntry>? entries))
        {
            entries = [];
            context.Set(ContextKey, entries);
        }
        entries[sagaId] = value;
    }

    SagaEntry GetEntry(ContextBag context, Guid sagaDataId)
    {
        if (context.TryGet(ContextKey, out Dictionary<Guid, SagaEntry>? entries) && entries.TryGetValue(sagaDataId, out var entry))
        {
            return entry;
        }

        // Custom finders may return saga data that was not loaded via Get, so no entry was
        // captured in the context. Fall back to the current live entry so the optimistic-
        // concurrency compare still resolves against committed state
        if (sagas.TryGetValue(sagaDataId, out var liveEntry) && !liveEntry.IsCompletionPending)
        {
            return liveEntry;
        }

        throw new Exception("The saga should be retrieved with Get method before it's updated.");
    }

    readonly NonDurableSagaOptions options;
    readonly ConcurrentDictionary<Guid, SagaEntry> sagas;
    readonly ConcurrentDictionary<CorrelationId, Guid> byCorrelationId;

    readonly record struct SaveOperationState(
        ConcurrentDictionary<Guid, SagaEntry> Sagas,
        ConcurrentDictionary<CorrelationId, Guid> ByCorrelationId,
        Guid SagaId,
        CorrelationId CorrelationId,
        SagaEntry Entry);

    readonly record struct UpdateOperationState(
        ConcurrentDictionary<Guid, SagaEntry> Sagas,
        Guid SagaId,
        SagaEntry Entry,
        SagaEntry UpdatedEntry);

    readonly record struct CompleteOperationState(
        ConcurrentDictionary<Guid, SagaEntry> Sagas,
        ConcurrentDictionary<CorrelationId, Guid> ByCorrelationId,
        Guid SagaId,
        SagaEntry Entry,
        SagaEntry CompletionPendingEntry);

    const string ContextKey = "NServiceBus.NonDurableSagaPersistence.Sagas";
    static readonly CorrelationId NoCorrelationId = new CorrelationId(typeof(object), "", new object());
}