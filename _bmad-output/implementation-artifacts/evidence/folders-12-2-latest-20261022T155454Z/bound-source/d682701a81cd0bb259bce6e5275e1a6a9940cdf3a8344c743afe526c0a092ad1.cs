using Dapr.Actors;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Private durable technical metadata seam; no target write is authorized by reading it.</summary>
public interface IDirectoryMigrationBoundaryActor : IActor
{
    /// <summary>Retains independently qualified initial all-writer epoch/revocation proof.</summary>
    Task<DirectoryBoundaryOutcome> InstallAsync(DirectoryEpochInstallation installation);
    /// <summary>Retains an independently authenticated atomically fenced finite repair cohort.</summary>
    Task<DirectoryBoundaryOutcome> RepairAsync(DirectoryRepairBoundary boundary);
    /// <summary>Retains exact original persisted terminal work, never new authority.</summary>
    Task<DirectoryBoundaryOutcome> RecordDrainAsync(DirectoryRepairDrainReceipt receipt);
    /// <summary>Activates only a fully drained cohort with qualified atomic predecessor revocation and history preservation.</summary>
    Task<DirectoryBoundaryOutcome> ActivateAsync(DirectoryEpochActivation activation);
    /// <summary>Reads metadata only under independent current exact lookup authority.</summary>
    Task<DirectoryEpochLedger?> ReadAsync(string tenantId);
    /// <summary>Authenticates original operation/request digest before immutable result release.</summary>
    Task<DirectoryBoundaryOutcome?> LookupAsync(string tenantId, string operationId, string requestDigest);
}
