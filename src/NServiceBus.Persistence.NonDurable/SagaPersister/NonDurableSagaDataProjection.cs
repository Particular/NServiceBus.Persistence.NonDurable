namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using SagaPersister;

static class NonDurableSagaDataProjection
{
    public static Task<TSagaData?> FindSagaData<TSagaData>(
        NonDurableStorage storage,
        INonDurableSagaLockingSession lockingSession,
        IReadOnlyContextBag context,
        Func<TSagaData, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData =>
        FindSagaData<TSagaData, Func<TSagaData, bool>>(
            storage,
            lockingSession,
            context,
            predicate,
            static (sagaData, predicate) => predicate(sagaData),
            cancellationToken);

    public static async Task<TSagaData?> FindSagaData<TSagaData, TState>(
        NonDurableStorage storage,
        INonDurableSagaLockingSession lockingSession,
        IReadOnlyContextBag context,
        TState state,
        Func<TSagaData, TState, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(lockingSession);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(predicate);

        if (context is not ContextBag contextBag)
        {
            throw new InvalidOperationException("The context must be a mutable ContextBag.");
        }

        var readState = new ProjectionReadState<TSagaData, TState>(storage.Sagas, contextBag, state, predicate, cancellationToken);

        return await SagaReadLocking.ReadCurrent(
            storage.Sagas,
            lockingSession,
            readState,
            static readState =>
            {
                foreach (var (sagaId, entry) in readState.Sagas)
                {
                    readState.CancellationToken.ThrowIfCancellationRequested();

                    if (entry.IsCompletionPending || entry.SagaDataType != typeof(TSagaData))
                    {
                        continue;
                    }

                    var sagaData = (TSagaData)entry.GetSagaCopy();
                    if (readState.Predicate(sagaData, readState.State))
                    {
                        return new(sagaId, entry);
                    }
                }

                return null;
            },
            static (liveEntry, readState) =>
            {
                var currentSagaData = (TSagaData)liveEntry.GetSagaCopy();
                return readState.Predicate(currentSagaData, readState.State) ? currentSagaData : null;
            },
            static (capturedSagaId, capturedEntry, readState) => NonDurableSagaPersister.SetEntry(readState.Context, capturedSagaId, capturedEntry),
            retryOnReadMiss: true,
            cancellationToken).ConfigureAwait(false);
    }

    readonly record struct ProjectionReadState<TSagaData, TState>(
        ConcurrentDictionary<Guid, SagaEntry> Sagas,
        ContextBag Context,
        TState State,
        Func<TSagaData, TState, bool> Predicate,
        CancellationToken CancellationToken)
        where TSagaData : class, IContainSagaData;
}