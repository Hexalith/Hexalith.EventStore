#if SELECTED
namespace PublishedConsumer;

/// <summary>Persists applied event state and its exact positive global watermark together.</summary>
public sealed class CounterReadModel
{
    /// <summary>Gets or sets the count derived from applied projection events.</summary>
    public int Count { get; set; }
    /// <summary>Gets or sets the last applied aggregate sequence.</summary>
    public long Sequence { get; set; }
    /// <summary>Gets or sets the highest positive global position durably applied to this model.</summary>
    public long Watermark { get; set; }
}
#endif
