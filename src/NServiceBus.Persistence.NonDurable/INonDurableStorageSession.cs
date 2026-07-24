namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Particular.Obsoletes;

/// <summary>
/// Provides access to NonDurable persistence synchronized storage operations.
/// </summary>
public interface INonDurableStorageSession
{
    /// <summary>
    /// Finds saga data asynchronously by querying the NonDurable in-memory saga store and captures the selected entry for concurrency checks.
    /// </summary>
    /// <typeparam name="TSagaData">The saga data type to query.</typeparam>
    /// <param name="context">The current context bag.</param>
    /// <param name="predicate">The predicate used to select the saga data.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The first saga data instance that matches the predicate, or <c>null</c> when no match is found.</returns>
    Task<TSagaData?> FindSagaData<TSagaData>(
        IReadOnlyContextBag context,
        Func<TSagaData, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData;

    /// <summary>
    /// Finds saga data asynchronously by querying the NonDurable in-memory saga store and captures the selected entry for concurrency checks.
    /// </summary>
    /// <typeparam name="TSagaData">The saga data type to query.</typeparam>
    /// <typeparam name="TState">The type of the state passed to the predicate.</typeparam>
    /// <param name="context">The current context bag.</param>
    /// <param name="state">The state passed to the predicate.</param>
    /// <param name="predicate">The predicate used to select the saga data.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The first saga data instance that matches the predicate, or <c>null</c> when no match is found.</returns>
    Task<TSagaData?> FindSagaData<TSagaData, TState>(
        IReadOnlyContextBag context,
        TState state,
        Func<TSagaData, TState, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData;

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
    [ObsoleteMetadata(
        Message = "GetSagaData performs synchronous-over-asynchronous locking",
        ReplacementTypeOrMember = "FindSagaData",
        RemoveInVersion = "5",
        TreatAsErrorFromVersion = "4")]
    [Obsolete("GetSagaData performs synchronous-over-asynchronous locking. Use 'FindSagaData' instead. Will be treated as an error from version 4.0.0. Will be removed in version 5.0.0.", false)]
    TSagaData? GetSagaData<TSagaData>(
        IReadOnlyContextBag context,
        Func<TSagaData, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData;

    /// <summary>
    /// Finds saga data by querying the NonDurable in-memory saga store and captures the selected entry for optimistic concurrency checks.
    /// The query is evaluated against a moment-in-time snapshot of the underlying storage as it is enumerated.
    /// Saga entries added or removed concurrently with the scan may or may not be included in that snapshot.
    /// Returned saga data is a copy of the stored entry.
    /// </summary>
    /// <typeparam name="TSagaData">The saga data type to query.</typeparam>
    /// <typeparam name="TState">The type of the state passed to the predicate.</typeparam>
    /// <param name="context">The current context bag.</param>
    /// <param name="state">The state passed to the predicate.</param>
    /// <param name="predicate">The predicate used to select the saga data.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The first saga data instance that matches the predicate, or <c>null</c> when no match is found.</returns>
    [ObsoleteMetadata(
        Message = "GetSagaData performs synchronous-over-asynchronous locking",
        ReplacementTypeOrMember = "FindSagaData",
        RemoveInVersion = "5",
        TreatAsErrorFromVersion = "4")]
    [Obsolete("GetSagaData performs synchronous-over-asynchronous locking. Use 'FindSagaData' instead. Will be treated as an error from version 4.0.0. Will be removed in version 5.0.0.", false)]
    TSagaData? GetSagaData<TSagaData, TState>(
        IReadOnlyContextBag context,
        TState state,
        Func<TSagaData, TState, bool> predicate,
        CancellationToken cancellationToken = default)
        where TSagaData : class, IContainSagaData;
}