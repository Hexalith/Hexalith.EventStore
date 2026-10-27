#if SELECTED
namespace PublishedConsumer;

/// <summary>Applies the current event schema only after SDK batch admission.</summary>
public sealed class EvolutionState
{
    public int Count { get; set; }
    public void Apply(EvolvedIncremented value) => Count += value.Value;
}
#endif
