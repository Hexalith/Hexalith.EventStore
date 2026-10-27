namespace PublishedConsumer;

/// <summary>Folds the disposable Counter aggregate using SDK replay.</summary>
public sealed class CounterState
{
    /// <summary>Gets or sets the reconstructed Counter value.</summary>
    public int Count { get; set; }
    /// <summary>Gets or sets the observed Apply invocation count.</summary>
    public static int ApplyCalls { get; set; }

    /// <summary>Applies one admitted increment and records the invocation.</summary>
    public void Apply(CounterIncremented value)
    {
        ApplyCalls++;
        Count += value.Amount;
    }
}
