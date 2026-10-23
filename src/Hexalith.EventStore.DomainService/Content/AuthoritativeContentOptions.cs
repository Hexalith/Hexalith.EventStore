namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Bounded Dapr-owned body storage. Neither bodies nor live references have a TTL.</summary>
public sealed class AuthoritativeContentOptions
{
    /// <summary>Gets or sets the Dapr state component.</summary>
    public string StateStoreName { get; set; } = "statestore";

    /// <summary>Gets or sets the maximum exact body length.</summary>
    public int MaximumBodyBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>Gets or sets the maximum retained uploads and publications per scope; capacity fails closed.</summary>
    public int MaximumEntries { get; set; } = 4096;

    /// <summary>Gets or sets the bounded CAS attempt count.</summary>
    public int WriteAttempts { get; set; } = 16;

    /// <summary>Gets or sets the total deadline for one API operation.</summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
