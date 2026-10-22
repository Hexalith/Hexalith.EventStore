namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Shared bounds for source inspection and its base64-encoded authenticated wire certificate.</summary>
public static class RetainedIdentityHistoryLimits
{
    /// <summary>Maximum number of original source positions certified by one observation.</summary>
    public const int MaxSourcePositions = 10_000;

    /// <summary>Maximum sealed source or decrypted attribution payload bytes.</summary>
    public const int MaxPayloadBytes = 16 * 1024 * 1024;

    /// <summary>Maximum serialized certificate bytes, including base64 and metadata framing.</summary>
    public const int MaxResponseBytes = 32 * 1024 * 1024;

    /// <summary>Maximum persisted contract-name length admitted by a source grant.</summary>
    public const int MaxContractNameLength = 1024;
}
