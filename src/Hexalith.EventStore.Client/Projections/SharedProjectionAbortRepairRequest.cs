namespace Hexalith.EventStore.Client.Projections;

/// <summary>Immutable certification to restore bounded selected metadata to its original absence.</summary>
/// <param name="OperationId">The abort owner.</param>
/// <param name="InventoryFingerprint">The exact certified inventory.</param>
/// <param name="Epoch">The reserved abort epoch.</param>
/// <param name="ActiveGeneration">The unchanged selected generation.</param>
/// <param name="CertificateId">The server-verified certificate identity.</param>
/// <param name="Values">The complete metadata observations, including original corrupt evidence.</param>
public sealed record SharedProjectionAbortRepairRequest(string OperationId, string InventoryFingerprint,
    long Epoch, long ActiveGeneration, string CertificateId, SharedProjectionAbortRepairValue[] Values)
{
    /// <summary>The complete unchanged source preconditions retained through every repair restart.</summary>
    public SharedProjectionAbortRepairGuard[] SourceGuards { get; init; } = [];
}
