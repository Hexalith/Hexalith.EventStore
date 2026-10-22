namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Append-only registration/release state including every post-start hold attempt.</summary>
/// <param name="HoldId">Exact HoldId.</param>
/// <param name="Scope">Exact Scope.</param>
/// <param name="DispositionVersion">Exact DispositionVersion.</param>
/// <param name="Active">Exact Active.</param>
/// <param name="PostStartAttempt">Exact PostStartAttempt.</param>
/// <param name="ReleaseReceiptId">Exact ReleaseReceiptId.</param>
public sealed record GovernanceHoldState(string HoldId, GovernanceScopeV1 Scope, string DispositionVersion, bool Active, bool PostStartAttempt, string ReleaseReceiptId);
