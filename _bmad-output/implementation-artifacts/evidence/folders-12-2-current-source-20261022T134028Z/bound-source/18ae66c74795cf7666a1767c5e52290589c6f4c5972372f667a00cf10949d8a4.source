using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>A source-qualified page or a closed unavailable/conflict outcome.</summary>
/// <param name="Page">The verified page, absent on uncertainty.</param>
/// <param name="FailureReason">The safe failure category.</param>
public sealed record SourcePublicationReadResult(SourcePublicationPage? Page, string? FailureReason)
{
    /// <summary>Gets whether a complete declared cut and usable page were released.</summary>
    public bool IsAvailable => Page is not null && FailureReason is null;
}
