namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact independently authorized hold request/release and acknowledged release receipt; policy is never selected by this owner.</summary>
/// <param name="HoldId">Exact HoldId.</param>
/// <param name="Scope">Exact Scope.</param>
/// <param name="DispositionVersion">Exact DispositionVersion.</param>
/// <param name="ReleaseReceiptId">Exact ReleaseReceiptId.</param>
public sealed record GovernanceHoldCommand(string HoldId, GovernanceScopeV1 Scope, string DispositionVersion, string ReleaseReceiptId);
