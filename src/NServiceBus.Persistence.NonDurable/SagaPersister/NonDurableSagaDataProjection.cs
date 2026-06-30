namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Threading;
using Extensibility;

static class NonDurableSagaDataProjection
{
    public static TSagaData? GetSagaData<TSagaData>(
        NonDurableStorage storage,
        IReadOnlyContextBag context,
        Func<TSagaData, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(predicate);

        if (context is not ContextBag contextBag)
        {
            throw new InvalidOperationException("The context must be a mutable ContextBag.");
        }

        foreach (var (sagaId, entry) in storage.Sagas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.SagaDataType != typeof(TSagaData))
            {
                continue;
            }

            var sagaData = (TSagaData)entry.GetSagaCopy();
            if (!predicate(sagaData))
            {
                continue;
            }

            NonDurableSagaPersister.SetEntry(contextBag, sagaId, entry);
            return sagaData;
        }

        return null;
    }
}