namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>The provider boundary explicitly observed before a connection is broken.</summary>
internal enum PostgresCommitBoundary
{
    /// <summary>COMMIT has not been forwarded to PostgreSQL.</summary>
    BeforeCommit,

    /// <summary>PostgreSQL completed COMMIT, but its acknowledgement is withheld.</summary>
    CommitAcknowledgement,
}
