namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Configures trusted tenant-scoped idempotency admission.</summary>
public sealed record IdempotencyAdmissionOptions
{
    /// <summary>Gets or sets whether upgrade deployments fail closed unless every presented key is inventoried.</summary>
    public bool RequireLegacyInventory { get; init; }

    /// <summary>Gets a value indicating whether trusted canonical admission is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the active digest-key version used for new admissions.</summary>
    public string ActiveDigestKeyVersion { get; init; } = string.Empty;

    /// <summary>Gets retained reader versions in deterministic lookup order.</summary>
    public List<string> ReaderDigestKeyVersions { get; init; } = [];

    /// <summary>Gets the approved key-material source.</summary>
    public IdempotencyDigestKeySource DigestKeySource { get; init; }

    /// <summary>Gets the Dapr secret-store component name for production key retrieval.</summary>
    public string DigestKeySecretStoreName { get; init; } = string.Empty;

    /// <summary>Gets the logical Dapr secret map name for production key retrieval.</summary>
    public string DigestKeySecretName { get; init; } = string.Empty;

    /// <summary>Gets the required non-secret secret-map generation marker.</summary>
    public string DigestKeySecretGeneration { get; init; } = string.Empty;

    /// <summary>Gets base64-encoded local-development digest keys by version.</summary>
    public Dictionary<string, string> DigestKeys { get; init; } = new(StringComparer.Ordinal);
}
