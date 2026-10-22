namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Identifies the approved source for the idempotency digest key-ring.</summary>
public enum IdempotencyDigestKeySource
{
    /// <summary>Base64 key material from application configuration for local development only.</summary>
    Configuration,

    /// <summary>A runtime-required versioned map retrieved through the Dapr secret API.</summary>
    DaprSecret,
}
