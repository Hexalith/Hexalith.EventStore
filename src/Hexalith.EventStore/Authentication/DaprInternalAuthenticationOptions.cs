using Microsoft.AspNetCore.Authentication;

namespace Hexalith.EventStore.Authentication;

/// <summary>
/// Options for the DaprInternal authentication scheme. Trusts the <c>dapr-caller-app-id</c>
/// header that DAPR sidecars attach to service-invocation requests. Only app-ids listed in
/// <see cref="AllowedCallers"/> are authenticated. Outside Development the configured
/// DAPR app-channel token must also match. Production still requires mTLS, a deny-by-default
/// sidecar ACL, and network isolation for the application port.
/// </summary>
public sealed class DaprInternalAuthenticationOptions : AuthenticationSchemeOptions {
    public const string SchemeName = "DaprInternal";

    public const string CallerHeaderName = "dapr-caller-app-id";

    public IList<string> AllowedCallers { get; init; } = [];
}
