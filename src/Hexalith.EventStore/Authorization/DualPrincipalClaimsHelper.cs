using System.Security.Claims;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Extracts dual-principal identity claims (original actor, authenticated workload, delegation,
/// scopes, and audience) from a <see cref="ClaimsPrincipal"/> at the gateway boundary. Models
/// <see cref="GlobalAdministratorHelper"/>'s claims-extraction approach.
/// </summary>
/// <remarks>
/// Claim mapping approved 2026-07-18 (Story 6.1-P2 Design Notes):
/// <list type="bullet">
/// <item><description><c>OriginalActorId</c> is the caller-supplied <c>sub</c> claim value — the same source as the legacy <c>UserId</c>.</description></item>
/// <item><description><c>AuthenticatedWorkloadId</c> is the <c>azp</c> claim, falling back to <c>client_id</c>, then the first <c>aud</c> entry, when earlier sources are absent.</description></item>
/// <item><description><c>IsDelegated</c> is <c>true</c> when an RFC 8693 <c>act</c> claim is present, or when <c>azp</c> and <c>client_id</c> are both present and differ.</description></item>
/// <item><description><c>Scopes</c> is the whitespace-split <c>scope</c> claim, falling back to <c>scp</c>.</description></item>
/// <item><description><c>Audience</c> is every <c>aud</c> claim value on the principal.</description></item>
/// </list>
/// No OBO/service-account flow exists in this repository today, so workload and actor naturally
/// converge to the same value until a real delegation flow is introduced.
/// <para>
/// <see cref="DualPrincipalIdentity.Scopes"/> and <see cref="DualPrincipalIdentity.Audience"/> are
/// bounded to <see cref="MaxClaimListEntries"/> entries, and each individual entry is further
/// bounded to <see cref="MaxClaimValueLength"/> characters: both are sourced from
/// attacker-influenceable claim values (a caller controls their own token's <c>scope</c>/<c>scp</c>
/// and <c>aud</c> claims) and are threaded onto every <c>QueryEnvelope</c> -- not only opted-in
/// safe-denial routes -- so an unbounded claim list, or a single unbounded claim value, would let
/// any caller inflate every request's serialized size.
/// </para>
/// </remarks>
public static class DualPrincipalClaimsHelper {
    /// <summary>
    /// The maximum number of entries preserved in <see cref="DualPrincipalIdentity.Scopes"/> and
    /// <see cref="DualPrincipalIdentity.Audience"/>. Additional claim-sourced entries beyond this
    /// bound are silently truncated rather than rejected, since the values are read-only routing
    /// metadata, not an authorization decision input requiring a fail-closed error.
    /// </summary>
    private const int MaxClaimListEntries = 64;

    /// <summary>
    /// The maximum length preserved for each individual <see cref="DualPrincipalIdentity.Scopes"/>
    /// or <see cref="DualPrincipalIdentity.Audience"/> entry. <see cref="MaxClaimListEntries"/>
    /// alone only bounds how many entries survive -- a single oversized claim value that never
    /// splits into multiple entries (e.g. one <c>aud</c> claim, or a <c>scope</c> claim with no
    /// internal whitespace) would stay within the 64-entry cap while still being arbitrarily
    /// large. Truncating each entry's length closes that gap.
    /// </summary>
    private const int MaxClaimValueLength = 512;

    private const string ActorizedActorClaimType = "act";
    private const string AudienceClaimType = "aud";
    private const string AuthorizedPartyClaimType = "azp";
    private const string ClientIdClaimType = "client_id";
    private const string ScopeClaimType = "scope";
    private const string ScopeAlternateClaimType = "scp";

    /// <summary>
    /// Extracts the dual-principal identity from the specified principal.
    /// </summary>
    /// <param name="principal">The claims principal to evaluate.</param>
    /// <param name="originalActorId">The already-validated <c>sub</c> claim value (the caller has confirmed this is non-empty).</param>
    /// <returns>The extracted <see cref="DualPrincipalIdentity"/>.</returns>
    public static DualPrincipalIdentity Extract(ClaimsPrincipal principal, string originalActorId) {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalActorId);

        IReadOnlyList<string> audience = ExtractAudience(principal);
        string? authorizedParty = FirstClaimValue(principal, AuthorizedPartyClaimType);
        string? clientId = FirstClaimValue(principal, ClientIdClaimType);
        string? authenticatedWorkloadId = authorizedParty
            ?? clientId
            ?? (audience.Count > 0 ? audience[0] : null);

        bool isDelegated = HasActClaim(principal)
            || (!string.IsNullOrWhiteSpace(authorizedParty)
                && !string.IsNullOrWhiteSpace(clientId)
                && !string.Equals(authorizedParty, clientId, StringComparison.Ordinal));

        return new DualPrincipalIdentity(
            originalActorId,
            authenticatedWorkloadId,
            isDelegated,
            ExtractScopes(principal),
            audience.Count > 0 ? audience : null);
    }

    private static string? FirstClaimValue(ClaimsPrincipal principal, string claimType) {
        foreach (Claim claim in principal.Claims) {
            if (string.Equals(claim.Type, claimType, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(claim.Value)) {
                return claim.Value;
            }
        }

        return null;
    }

    private static bool HasActClaim(ClaimsPrincipal principal) {
        foreach (Claim claim in principal.Claims) {
            if (string.Equals(claim.Type, ActorizedActorClaimType, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(claim.Value)) {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ExtractAudience(ClaimsPrincipal principal) {
        List<string> audience = [];
        foreach (Claim claim in principal.Claims) {
            if (audience.Count >= MaxClaimListEntries) {
                // Bounded: a caller controls their own token's claims, and every aud entry is
                // threaded onto every QueryEnvelope regardless of route -- stop accumulating
                // rather than let an attacker-inflated claim set grow every request's payload.
                break;
            }

            if (string.Equals(claim.Type, AudienceClaimType, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(claim.Value)) {
                audience.Add(TruncateClaimValue(claim.Value));
            }
        }

        return audience;
    }

    private static string TruncateClaimValue(string value) =>
        value.Length > MaxClaimValueLength ? value[..MaxClaimValueLength] : value;

    private static IReadOnlyList<string>? ExtractScopes(ClaimsPrincipal principal) {
        string? scopeClaimValue = FirstClaimValue(principal, ScopeClaimType)
            ?? FirstClaimValue(principal, ScopeAlternateClaimType);

        if (string.IsNullOrWhiteSpace(scopeClaimValue)) {
            return null;
        }

        string[] scopes = scopeClaimValue.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (scopes.Length == 0) {
            return null;
        }

        // Bounded for the same reason as ExtractAudience: the scope claim is caller-controlled
        // and threaded onto every QueryEnvelope.
        string[] bounded = scopes.Length > MaxClaimListEntries
            ? scopes[..MaxClaimListEntries]
            : scopes;

        // Per-entry length bound closes the gap MaxClaimListEntries alone leaves open: a single
        // scope token with no internal whitespace never splits into multiple entries, so it would
        // otherwise stay within the 64-entry cap while still being arbitrarily large.
        for (int i = 0; i < bounded.Length; i++) {
            bounded[i] = TruncateClaimValue(bounded[i]);
        }

        return bounded;
    }
}
