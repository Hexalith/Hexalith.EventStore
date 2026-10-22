namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Trusted replay result; erasure substitution never makes an unreadable external read readable.</summary>
/// <param name="Outcome">The trusted replay bytes or bounded unreadable result.</param>
/// <param name="HasErasedContent">Whether a positively erased field was replaced by <see cref="Erased"/>.</param>
public sealed record PayloadReplayResult(PayloadUnprotectionOutcome Outcome, bool HasErasedContent = false);
