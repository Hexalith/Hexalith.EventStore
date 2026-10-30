using System.Security.Claims;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Evaluates the owner's explicit eligibility using exactly one authenticated surface claim.</summary>
public static class RouteCatalogSurfacePolicy
{
    /// <summary>The signed surface claim. Headers, roles and endpoint classifications never supply this value.</summary>
    public const string ClaimType = "eventstore:surface";

    /// <summary>Returns whether this principal can invoke a public operation. Origin proof is supplied only by an existing verified gateway boundary.</summary>
    public static bool Allows(RouteOperationContract operation, ClaimsPrincipal principal, bool verifiedOriginatingProof = false)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(principal);
        if (operation.Ingress != "public") return false;
        return Surface(principal) switch
        {
            "ui" => operation.Eligibility is "agent-eligible" or "UI-only" or "confirmation-required",
            "agent" => operation.Eligibility == "agent-eligible",
            "service" => operation.Eligibility == "agent-eligible"
                || operation.Eligibility == "confirmation-required" && verifiedOriginatingProof,
            _ => false,
        };
    }

    /// <summary>Reads exactly one authenticated closed-vocabulary surface claim, rejecting every missing or ambiguous representation.</summary>
    public static string? Surface(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var claims = principal.Identities.SelectMany(identity => identity.Claims.Where(c => c.Type == ClaimType)
            .Select(claim => (identity.IsAuthenticated, claim.Value))).ToArray();
        return claims.Length == 1 && claims[0].IsAuthenticated && claims[0].Value is "ui" or "agent" or "service"
            ? claims[0].Value : null;
    }
}
