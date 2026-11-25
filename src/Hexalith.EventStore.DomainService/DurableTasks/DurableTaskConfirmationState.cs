namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Retained single-use confirmation state for one admission scope.</summary>
/// <param name="TokenSha256">The hash of the opaque token.</param>
/// <param name="RequestSha256">The normalized intent hash.</param>
/// <param name="BindingSha256">The authoritative interactive evidence hash.</param>
/// <param name="ExpiresAt">The exclusive expiry time.</param>
/// <param name="ConsumedBy">The committed task ID that consumed the artifact.</param>
internal sealed record DurableTaskConfirmationState(
    string TokenSha256,
    string RequestSha256,
    string BindingSha256,
    DateTimeOffset ExpiresAt,
    string? ConsumedBy = null);
