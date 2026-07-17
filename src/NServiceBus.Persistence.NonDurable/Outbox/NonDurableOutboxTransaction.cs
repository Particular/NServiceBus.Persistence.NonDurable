namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Outbox;

class NonDurableOutboxTransaction : IOutboxTransaction
{
    public NonDurableStorageTransaction? Transaction { get; private set; } = new();

    public void Enlist<TState>(TState state, Action<TState> apply, Action<TState>? rollback = null, Activity? activity = null)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(Transaction);

        Transaction.Enlist(state, apply, rollback, activity);
    }

    public Task Commit(CancellationToken cancellationToken = default)
    {
        try
        {
            Transaction?.Commit();
        }
        finally
        {
            completionCallbacks.Run();
        }

        return Task.CompletedTask;
    }

    internal void OnCompleted(Action callback)
    {
        completionCallbacks.Add(callback);
    }

    public void Dispose()
    {
        if (Transaction is { } tx)
        {
            tx.DisposeTrackedActivities();
            completionCallbacks.Run();
            Transaction = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    readonly CompletionCallbacks completionCallbacks = new();
}