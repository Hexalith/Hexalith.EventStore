namespace P1R.Counter;

/// <summary>Disposable event-fold state; public setter permits snapshot rehydration.</summary>
public sealed class CounterState
{
    /// <summary>Gets or sets the fixture count restored from a snapshot.</summary>
    public int Count { get; set; }

    /// <summary>Applies one persisted increment.</summary>
    public void Apply(CounterIncremented value) => Count++;
}
