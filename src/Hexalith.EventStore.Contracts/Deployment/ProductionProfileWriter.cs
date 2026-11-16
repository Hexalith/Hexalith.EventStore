namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Declared writer and governed erasure binding for an actor component.</summary>
/// <param name="Id">The required id.</param>
/// <param name="AppId">The required app id.</param>
/// <param name="ComponentId">The required component id.</param>
/// <param name="ImplementationDigest">The required implementation digest.</param>
/// <param name="ErasureContractDigest">The required erasure contract digest.</param>
public sealed record ProductionProfileWriter(
    string Id,
    string AppId,
    string ComponentId,
    string ImplementationDigest,
    string ErasureContractDigest);
