using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;

namespace Hexalith.EventStore.Authentication;

/// <summary>
/// Settings and names of the <c>DaprInternal</c> workload-assertion scheme, bound from <c>Authentication:DaprInternal</c>.
/// </summary>
/// <remarks>
/// <para>
/// An internal caller is authenticated only when the Dapr application-channel token (<c>APP_API_TOKEN</c>) is valid
/// and the request carries one short-lived workload assertion from the trusted JWT issuer, issued for
/// <see cref="DefaultAudience"/> (or the configured <c>Audience</c>), naming an allow-listed caller in its
/// <c>azp</c> claim, and granting the requested operation. The plaintext <see cref="CallerHeaderName"/> header is
/// only cross-checked against the signed caller; it never authenticates a request.
/// </para>
/// <para>
/// The resulting principal carries the workload identity and its granted operations only. It never carries
/// global-administrator, tenant, domain, or permission claims, so an internal workload cannot act as a human
/// administrator.
/// </para>
/// </remarks>
public sealed class DaprInternalAuthenticationOptions : AuthenticationSchemeOptions {
    /// <summary>Gets the scheme name.</summary>
    public const string SchemeName = "DaprInternal";

    /// <summary>Gets the sidecar caller-attribution header; it is never trusted on its own.</summary>
    public const string CallerHeaderName = EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName;

    /// <summary>Gets the default workload audience of this EventStore host.</summary>
    public const string DefaultAudience = "eventstore";

    /// <summary>Gets the policy requiring an internal workload granted the trusted-effect operation.</summary>
    public const string TrustedEffectPolicy = SchemeName + ":" + EventStoreWorkloadOperations.TrustedEffect;

    /// <summary>Gets or sets the internal workload identities allowed to call this host.</summary>
    public IList<string> AllowedCallers { get; init; } = [];
}
