namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Distribution result, with explicit positive authentication of exact retained ciphertext when content stays sealed.</summary>
/// <param name="Outcome">The exact authenticated sealed bytes or legacy readable bytes.</param>
/// <param name="IsSealed">Whether the provider authenticated original committed ciphertext without releasing plaintext.</param>
public sealed record PayloadDistributionResult(PayloadUnprotectionOutcome Outcome, bool IsSealed = false);
