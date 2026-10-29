using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Hexalith.EventStore.Extensions;

/// <summary>Separates verified internal traffic from unauthenticated traffic without relaxing either quota.</summary>
internal static class EventStoreRateLimitPartition {
    /// <summary>Gets a bounded callback quota key only for a mapped actor and a configured, verified channel token.</summary>
    /// <param name="context">The routed request, before authorization middleware runs.</param>
    /// <returns>A non-reversible actor identity key, or <see langword="null"/> for ordinary traffic.</returns>
    internal static string? TrustedActorKey(HttpContext context)
    {
        if (!IsActorEndpoint(context.GetEndpoint())
            || context.Request.RouteValues["actorTypeName"] is not string actorType
            || string.IsNullOrWhiteSpace(actorType)
            || context.Request.RouteValues["actorId"] is not string actorId
            || string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        // Rate limiting precedes endpoint authorization, so verify the configured secret directly rather
        // than trusting the current principal or waiting for the endpoint's channel policy to authenticate.
        // A legacy unverified channel claim (never emitted any more) must never mint an actor partition.
        IConfiguration configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        if (DaprAppChannelToken.Verify(context.Request, configuration[DaprAppChannelToken.ConfigurationKey], isDevelopment: false)
            != DaprAppChannelTokenStatus.Valid)
        {
            return null;
        }

        // A length prefix keeps type/id pairs unambiguous. Hashing avoids exposing raw actor IDs in quota logs.
        byte[] identity = Encoding.UTF8.GetBytes(FormattableString.Invariant($"{actorType.Length}:{actorType}{actorId}"));
        return "sidecar:actor:" + Convert.ToHexStringLower(SHA256.HashData(identity));
    }

    private static bool IsActorEndpoint(Endpoint? endpoint)
    {
        if (endpoint?.Metadata.GetMetadata<EventStoreActorCallbackEndpointMetadata>() is not null)
            return true;

        // Custom Gateway hosts may call Dapr's mapping directly. Require its actual mapped delegate,
        // exact route and verb; a matching public path or caller-supplied route values are insufficient.
        Type daprMapper = typeof(ActorsEndpointRouteBuilderExtensions);
        Type? declaringType = endpoint?.RequestDelegate?.Method.DeclaringType;
        if (endpoint is not RouteEndpoint route
            || declaringType?.Assembly != daprMapper.Assembly
            || declaringType.DeclaringType != daprMapper)
            return false;

        string? verb = route.RoutePattern.RawText?.TrimStart('/') switch
        {
            "actors/{actorTypeName}/{actorId}" => "DELETE",
            "actors/{actorTypeName}/{actorId}/method/{methodName}" => "PUT",
            "actors/{actorTypeName}/{actorId}/method/remind/{reminderName}" => "PUT",
            "actors/{actorTypeName}/{actorId}/method/timer/{timerName}" => "PUT",
            _ => null,
        };
        IReadOnlyList<string>? methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        return verb is not null && methods is { Count: 1 } && string.Equals(methods[0], verb, StringComparison.Ordinal);
    }

    public static string? TrustedInternalKey(ClaimsPrincipal? principal) {
        if (principal is null) {
            return null;
        }

        foreach (ClaimsIdentity identity in principal.Identities) {
            if (!identity.IsAuthenticated) {
                continue;
            }

            if (string.Equals(identity.AuthenticationType, DaprInternalAuthenticationOptions.SchemeName, StringComparison.Ordinal)) {
                string? caller = identity.FindFirst(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)?.Value;
                string? subject = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(caller) && (subject is null || string.Equals(subject, "workload:" + caller, StringComparison.Ordinal))) {
                    return "workload:" + caller;
                }
            }

            if (string.Equals(identity.AuthenticationType, EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme, StringComparison.Ordinal)
                && string.Equals(identity.FindFirst(EventStoreWorkloadAuthenticationDefaults.ChannelClaimType)?.Value,
                    DaprSidecarChannelAuthenticationHandler.VerifiedChannel, StringComparison.Ordinal)) {
                return "sidecar:verified";
            }
        }

        return null;
    }

    public static string? TrustedTenantKey(ClaimsPrincipal? principal) {
        if (principal is null) {
            return null;
        }

        foreach (ClaimsIdentity identity in principal.Identities) {
            if (!identity.IsAuthenticated) {
                continue;
            }

            if (string.Equals(identity.AuthenticationType, DaprInternalAuthenticationOptions.SchemeName, StringComparison.Ordinal)) {
                string? boundTenant = identity.FindFirst(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType)?.Value;
                if (!string.IsNullOrWhiteSpace(boundTenant)) {
                    return boundTenant;
                }
            }
        }

        return null;
    }
}
