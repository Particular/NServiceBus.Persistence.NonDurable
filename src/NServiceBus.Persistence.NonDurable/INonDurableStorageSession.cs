namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Threading;
using Extensibility;

/// <summary>
/// Provides access to NonDurable persistence synchronized storage operations.
/// </summary>
public interface INonDurableStorageSession
{
    /// <summary>
    /// Finds saga data by querying the NonDurable in-memory saga store and captures the selected entry for optimistic concurrency checks.
    /// The query is evaluated against a moment-in-time snapshot of the underlying storage as it is enumerated.
    /// Saga entries added or removed concurrently with the scan may or may not be included in that snapshot.
    /// Returned saga data is a copy of the stored entry.
    /// </summary>
    /// <typeparam name="TSagaData">The saga data type to query.</typeparam>
    /// <param name="context">The current context bag.</param>
    /// <param name="predicate">The predicate used to select the saga data.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The first saga data instance that matches the predicate, or <c>null</c> when no match is found.</returns>
    TSagaData? GetSagaData<TSagaData>(
        IReadOnlyContextBag context,
        Func<TSagaData, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData;
}