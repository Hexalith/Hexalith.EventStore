namespace Hexalith.EventStore.Client.Projections;

/// <summary>Authenticated, constant-size reference to a complete captured owner identity tree.</summary>
/// <param name="Version">The additive storage schema version.</param>
/// <param name="NamespaceHash">The scope, source, operation and epoch binding.</param>
/// <param name="ScopeHash">The selected projection scope hash.</param>
/// <param name="SourceHash">The original source stream hash.</param>
/// <param name="OperationHash">The original rebuild operation hash.</param>
/// <param name="Epoch">The owning rebuild epoch.</param>
/// <param name="Count">The complete captured event count.</param>
/// <param name="RootDigest">The content-addressed root digest; empty until sealed.</param>
/// <param name="Height">The root level, with zero denoting a leaf.</param>
/// <param name="LastPosition">The last original source position.</param>
public sealed record SharedProjectionCapturedIdentityReference(
    int Version, string NamespaceHash, string ScopeHash, string SourceHash, string OperationHash,
    long Epoch, long Count, string RootDigest, int Height, long LastPosition);
