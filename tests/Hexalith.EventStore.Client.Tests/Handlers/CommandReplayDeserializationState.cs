using Hexalith.EventStore.Client.Tests.Aggregates;

namespace Hexalith.EventStore.Client.Tests.Handlers;

/// <summary>Records the values reconstructed after all admitted event converters finish.</summary>
internal sealed class CommandReplayDeserializationState
{
    /// <summary>Records working state construction after complete event preparation.</summary>
    public CommandReplayDeserializationState() => LegacyReplayMutationScope.Current.Constructed++;

    /// <summary>Gets the reconstructed sum.</summary>
    public int Total { get; private set; }

    /// <summary>Applies an event whose converter may attempt to mutate later caller input.</summary>
    /// <param name="value">The value deserialized from the privately admitted payload.</param>
    public void Apply(CommandReplayDeserializationEvent value)
    {
        Total += value.Amount;
        LegacyReplayMutationScope.Current.Applied.Add(value.Amount);
    }
}
