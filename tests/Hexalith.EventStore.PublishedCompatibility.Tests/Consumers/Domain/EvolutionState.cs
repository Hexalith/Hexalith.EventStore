#if SELECTED
namespace PublishedConsumer;

/// <summary>Applies the current event schema only after SDK batch admission.</summary>
public sealed class EvolutionState
{
    /// <summary>Gets or sets the value reconstructed from current-schema events.</summary>
    public int Count { get; set; }
    /// <summary>Applies one admitted current-schema event.</summary>
    public void Apply(EvolvedIncremented value) => Count += value.Value;
}
#endif
