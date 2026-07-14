namespace Hexalith.EventStore.Server.Projections;

/// <summary>Store-global proof that all projection delivery writers crossed the maintenance boundary.</summary>
internal sealed record ProjectionDeliveryWriterProtocol {
    /// <summary>The current marker schema.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The current delivery writer protocol.</summary>
    public const int CurrentWriterProtocolVersion = 2;

    /// <summary>Initializes a validated v2 cutover marker.</summary>
    public ProjectionDeliveryWriterProtocol(
        int schemaVersion,
        int writerProtocolVersion,
        string cutoverCommit,
        DateTimeOffset activatedAt) {
        if (schemaVersion != CurrentSchemaVersion || writerProtocolVersion != CurrentWriterProtocolVersion) {
            throw new ArgumentException("Only the current projection delivery writer protocol can be activated.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(cutoverCommit);
        SchemaVersion = schemaVersion;
        WriterProtocolVersion = writerProtocolVersion;
        CutoverCommit = cutoverCommit;
        ActivatedAt = activatedAt;
    }

    /// <summary>Gets the marker schema version.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>Gets the activated writer protocol version.</summary>
    public int WriterProtocolVersion { get; init; }

    /// <summary>Gets the exact repository commit deployed at cutover.</summary>
    public string CutoverCommit { get; init; }

    /// <summary>Gets the UTC activation time.</summary>
    public DateTimeOffset ActivatedAt { get; init; }

    /// <summary>Returns whether this marker is the exact supported v2 protocol.</summary>
    public bool IsCurrent => SchemaVersion == CurrentSchemaVersion
        && WriterProtocolVersion == CurrentWriterProtocolVersion
        && !string.IsNullOrWhiteSpace(CutoverCommit);
}
