namespace Hexalith.EventStore.Server.Projections;

/// <summary>Identifies the deployed, stopped implementation of one maintenance writer role.</summary>
/// <param name="Role">The exact inventoried writer role.</param>
/// <param name="Commit">The full canonical source revision deployed to this writer.</param>
/// <param name="PackageIdentity">The exact immutable package identity retained by the operator.</param>
/// <param name="UpgradedAndQuiesced">Whether this writer is upgraded and stopped.</param>
public sealed record ProjectionSourceGuardWriterInventory(string Role, string Commit,
    string PackageIdentity, bool UpgradedAndQuiesced);
