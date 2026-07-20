namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Configures trusted tenant-scoped idempotency admission.</summary>
public sealed record IdempotencyAdmissionOptions
{
    /// <summary>Gets a value indicating whether trusted canonical admission is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the active digest-key version used for new admissions.</summary>
    public string ActiveDigestKeyVersion { get; init; } = string.Empty;

    /// <summary>Gets base64-encoded digest master keys by version.</summary>
    public Dictionary<string, string> DigestKeys { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Gets operation policies keyed as <c>{adapterId}:{operationId}</c>.</summary>
    public Dictionary<string, IdempotencyAdmissionOperationOptions> Operations { get; init; } = new(StringComparer.Ordinal);
}
