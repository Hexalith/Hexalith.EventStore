namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Dual-principal identity extracted from the gateway JWT for a single query submission.
/// Distinguishes the original end-user actor from the authenticated calling workload, and
/// carries delegation, scope, and audience evidence when present on the token.
/// </summary>
/// <param name="OriginalActorId">The original end-user actor identifier, sourced from the <c>sub</c> claim.</param>
/// <param name="AuthenticatedWorkloadId">The authenticated calling workload identifier, sourced from <c>azp</c>, <c>client_id</c>, or the first <c>aud</c> entry.</param>
/// <param name="IsDelegated">Whether the token carries delegation evidence (an RFC 8693 <c>act</c> claim, or <c>azp</c> diverging from <c>client_id</c>).</param>
/// <param name="Scopes">The whitespace-split <c>scope</c>/<c>scp</c> claim values, when present.</param>
/// <param name="Audience">The token's <c>aud</c> claim values, when present.</param>
public sealed record DualPrincipalIdentity(
    string OriginalActorId,
    string? AuthenticatedWorkloadId,
    bool IsDelegated,
    IReadOnlyList<string>? Scopes,
    IReadOnlyList<string>? Audience);
