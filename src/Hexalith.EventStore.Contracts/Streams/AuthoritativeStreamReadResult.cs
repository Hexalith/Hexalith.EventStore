namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>A source consistency result; unavailable evidence cannot authorize an action.</summary>
/// <param name="Stream">The verified prefix, only on success.</param>
/// <param name="FailureReason">A value-free internal failure classification.</param>
public sealed record AuthoritativeStreamReadResult(AuthoritativeEventStream? Stream, string? FailureReason)
{
    /// <summary>Gets whether a complete stable source prefix was observed.</summary>
    public bool IsAuthoritative => Stream is not null && FailureReason is null;
}
