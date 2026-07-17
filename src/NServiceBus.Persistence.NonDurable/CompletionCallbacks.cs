namespace NServiceBus.Persistence.NonDurable;

using System;
using System.Collections.Generic;

sealed class CompletionCallbacks
{
    public void Add(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (callbacksExecuted)
        {
            callback();
            return;
        }

        callbacks.Add(callback);
    }

    public void Run()
    {
        if (callbacksExecuted)
        {
            return;
        }

        callbacksExecuted = true;

        foreach (var callback in callbacks)
        {
            callback();
        }

        callbacks.Clear();
    }

    readonly List<Action> callbacks = [];
    bool callbacksExecuted;
}
