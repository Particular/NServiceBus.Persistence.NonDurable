namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Collections.Generic;
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
            RunCompletionCallbacks();
        }

        return Task.CompletedTask;
    }

    internal void OnCompleted(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (completionCallbacksExecuted)
        {
            callback();
            return;
        }

        completionCallbacks.Add(callback);
    }

    public void Dispose()
    {
        if (Transaction is { } tx)
        {
            tx.DisposeTrackedActivities();
            RunCompletionCallbacks();
            Transaction = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    void RunCompletionCallbacks()
    {
        if (completionCallbacksExecuted)
        {
            return;
        }

        completionCallbacksExecuted = true;

        foreach (var callback in completionCallbacks)
        {
            callback();
        }

        completionCallbacks.Clear();
    }

    readonly List<Action> completionCallbacks = [];
    bool completionCallbacksExecuted;
}