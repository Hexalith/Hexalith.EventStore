namespace PublishedConsumer;

/// <summary>Folds the disposable Counter aggregate using SDK replay.</summary>
public sealed class CounterState
{
    public int Count { get; set; }
    public static int ApplyCalls { get; set; }

    public void Apply(CounterIncremented value)
    {
        ApplyCalls++;
        Count += value.Amount;
    }
}
