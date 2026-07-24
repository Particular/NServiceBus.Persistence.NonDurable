namespace NServiceBus.Persistence.NonDurable;

using System;
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

        SagaReadLocking.SagaReadCandidate? ResolveCandidate()
        {
            foreach (var (sagaId, entry) in storage.Sagas)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsCompletionPending || entry.SagaDataType != typeof(TSagaData))
                {
                    continue;
                }

                var sagaData = (TSagaData)entry.GetSagaCopy();
                if (predicate(sagaData, state))
                {
                    return new(sagaId, entry);
                }
            }

            return null;
        }

        return await SagaReadLocking.ReadCurrent(
            storage.Sagas,
            lockingSession,
            ResolveCandidate,
            liveEntry =>
            {
                var currentSagaData = (TSagaData)liveEntry.GetSagaCopy();
                return predicate(currentSagaData, state) ? currentSagaData : null;
            },
            (capturedSagaId, capturedEntry) => NonDurableSagaPersister.SetEntry(contextBag, capturedSagaId, capturedEntry),
            retryOnReadMiss: true,
            cancellationToken).ConfigureAwait(false);
    }
}