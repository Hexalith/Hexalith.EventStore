namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Retained module package, image or composed extension identity.</summary>
/// <param name="Id">The required id.</param>
/// <param name="Kind">The required kind.</param>
/// <param name="Version">The required version.</param>
/// <param name="Digest">The required digest.</param>
public sealed record ProductionProfileArtifact(
    string Id,
    string Kind,
    string Version,
    string Digest);
