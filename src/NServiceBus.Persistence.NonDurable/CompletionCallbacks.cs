namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Collections.Generic;

sealed class CompletionCallbacks
{
    public void Add<TState>(TState state, Action<TState> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (callbacksExecuted)
        {
            callback(state);
            return;
        }

        (callbacks ??= []).Add(new CompletionCallback<TState>(state, callback));
    }

    public void Run()
    {
        if (callbacksExecuted)
        {
            return;
        }

        callbacksExecuted = true;

        if (callbacks is null)
        {
            return;
        }

        foreach (var callback in callbacks)
        {
            callback.Invoke();
        }

        callbacks.Clear();
    }

    interface ICompletionCallback
    {
        void Invoke();
    }

    sealed class CompletionCallback<TState>(TState state, Action<TState> callback) : ICompletionCallback
    {
        public void Invoke() => callback(state);
    }

    List<ICompletionCallback>? callbacks;
    bool callbacksExecuted;
}