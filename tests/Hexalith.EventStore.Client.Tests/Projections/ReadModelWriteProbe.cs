namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>A serializable value that signals when a fake store is about to write.</summary>
/// <param name="read">Supplies the value during serialization.</param>
internal sealed class ReadModelWriteProbe(Func<int> read)
{
    /// <summary>Gets the value to persist.</summary>
    public int Value => read();
}
