namespace NServiceBus;

/// <summary>
/// Controls how NonDurable saga persistence coordinates concurrent access to saga data.
/// </summary>
public enum NonDurableSagaConcurrencyMode
{
    /// <summary>
    /// Loads saga data without taking a lock and detects conflicts when changes are committed.
    /// </summary>
    Optimistic = 0,

    /// <summary>
    /// Takes an exclusive per-saga lock when existing saga data is loaded and holds it until the storage session completes.
    /// Conflicts are still validated when changes are committed.
    /// </summary>
    Pessimistic = 1
}
