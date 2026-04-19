using Microsoft.AspNetCore.Authentication;

namespace Hexalith.EventStore.Authentication;

/// <summary>
/// Options for the DaprInternal authentication scheme. Trusts the <c>dapr-caller-app-id</c>
/// header that DAPR sidecars attach to service-invocation requests. Only app-ids listed in
/// <see cref="AllowedCallers"/> are authenticated; everything else is left for the JWT scheme
/// to handle. In production on Kubernetes, DAPR mTLS + SPIFFE prevents header spoofing; in
/// self-hosted mode the trust boundary is the DAPR sidecar process on localhost.
/// </summary>
public sealed class DaprInternalAuthenticationOptions : AuthenticationSchemeOptions {
    public const string SchemeName = "DaprInternal";

    public const string CallerHeaderName = "dapr-caller-app-id";

    public IList<string> AllowedCallers { get; init; } = [];
}
