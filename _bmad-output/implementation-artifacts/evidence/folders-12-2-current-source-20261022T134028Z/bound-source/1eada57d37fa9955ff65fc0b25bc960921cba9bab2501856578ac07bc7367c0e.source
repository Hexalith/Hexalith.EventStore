namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Mutates caller inputs from state construction or Apply while recording effective values.</summary>
internal sealed class LegacyReplayMutationState
{
    /// <summary>Runs the state-construction callback after replay intake.</summary>
    public LegacyReplayMutationState()
    {
        LegacyReplayMutationScope.Current.Constructed++;
        LegacyReplayMutationScope.Current.OnConstruct?.Invoke();
    }

    /// <summary>Gets the reconstructed sum.</summary>
    public int Total { get; private set; }

    /// <summary>Applies the private payload and runs the first-Apply callback.</summary>
    /// <param name="value">The privately read event.</param>
    public void Apply(LegacyReplayMutationEvent value)
    {
        Total += value.Amount;
        LegacyReplayMutationScope scope = LegacyReplayMutationScope.Current;
        scope.Applied.Add(value.Amount);
        if (scope.Applied.Count == 1) { scope.OnFirstApply?.Invoke(); }
    }
}
