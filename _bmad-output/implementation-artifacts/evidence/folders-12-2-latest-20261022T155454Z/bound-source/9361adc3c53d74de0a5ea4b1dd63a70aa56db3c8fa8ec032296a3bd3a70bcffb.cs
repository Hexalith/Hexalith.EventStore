namespace Hexalith.EventStore.Server.Commands;

/// <summary>Identifies durable evidence that prevents digest-key retirement.</summary>
public enum IdempotencyDigestKeyReferenceKind
{
    /// <summary>A live admission record.</summary>
    Record,

    /// <summary>An expired consumed-key tombstone.</summary>
    Tombstone,

    /// <summary>A tenant-directory alias or promotion entry.</summary>
    DirectoryAlias,

    /// <summary>A legacy-migration inventory entry.</summary>
    MigrationEntry,

    /// <summary>A tenant legal-hold record.</summary>
    LegalHold,
}
