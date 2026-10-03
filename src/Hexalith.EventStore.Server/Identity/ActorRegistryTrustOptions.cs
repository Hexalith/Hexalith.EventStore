namespace Hexalith.EventStore.Server.Identity;

/// <summary>Explicit narrow registry capabilities; no trusted caller defaults.</summary>
public sealed class ActorRegistryTrustOptions
{
    /// <summary>Gets or sets the sole trusted identity-service workload sources allowed to mutate.</summary>
    public string[] WriterSources { get; set; } = [];

    /// <summary>Gets or sets the configured trusted identity-service sources allowed to inspect.</summary>
    public string[] ReaderSources { get; set; } = [];
}
