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
        Transaction?.Commit();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Transaction is { } tx)
        {
            tx.DisposeTrackedActivities();
            Transaction = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}