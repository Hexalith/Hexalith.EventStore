using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Exact append-only activation/admission operation receipt; changed identity cannot reuse the operation.</summary>
/// <param name="OperationId">Complete deterministic operation identity.</param>
/// <param name="RequestDigest">Exact owned input fingerprint.</param>
/// <param name="Outcome">Original authenticated durable result.</param>
internal sealed record DeletionConsumptionOperation(string OperationId, string RequestDigest, DeletionConsumptionOutcome Outcome);
