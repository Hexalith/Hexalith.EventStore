namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>A purpose-limited source result; failure never contains partial history.</summary>
/// <param name="Stream">The complete authenticated partition on success.</param>
/// <param name="FailureReason">A content-free failure classification.</param>
public sealed record RetainedIdentityHistoryReadResult(RetainedIdentityHistoryStream? Stream, string? FailureReason)
{
    /// <summary>Gets whether a complete retained-history observation exists.</summary>
    public bool IsAuthoritative => Stream is not null && FailureReason is null;
}
