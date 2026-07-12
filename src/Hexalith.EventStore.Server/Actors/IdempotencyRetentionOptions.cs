namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Configures application-level retention for terminal idempotency records.
/// </summary>
public sealed record IdempotencyRetentionOptions
{
    /// <summary>The default terminal retention in seconds (24 hours).</summary>
    public const int DefaultTerminalRetentionSeconds = 86_400;

    /// <summary>Gets the terminal record retention in seconds.</summary>
    public int TerminalRetentionSeconds { get; init; } = DefaultTerminalRetentionSeconds;
}
