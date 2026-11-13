namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Detects a second serialization of a mutable input during one pure hash computation.</summary>
internal sealed class CanonicalHashChangingPreimage
{
    private int _reads;

    /// <summary>Gets a different value on every serialization observation.</summary>
    public int Value => ++_reads;

    /// <summary>Gets the number of actual serializer observations.</summary>
    internal int Reads => _reads;
}
