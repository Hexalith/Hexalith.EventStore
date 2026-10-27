#if SELECTED
namespace PublishedConsumer;

/// <summary>Persists applied event state and its exact positive global watermark together.</summary>
public sealed class CounterReadModel
{
    public int Count { get; set; }
    public long Sequence { get; set; }
    public long Watermark { get; set; }
}
#endif
