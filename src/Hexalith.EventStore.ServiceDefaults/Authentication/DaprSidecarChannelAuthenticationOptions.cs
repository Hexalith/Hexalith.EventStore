using Microsoft.AspNetCore.Authentication;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Options of the scheme that authenticates only the Dapr application channel.
/// </summary>
/// <remarks>
/// This scheme is reserved for deliveries the sidecar itself originates (pub/sub delivery, subscription
/// discovery, actor runtime calls), where no caller credential exists. It grants no workload, tenant, or
/// administrator identity.
/// </remarks>
public sealed class DaprSidecarChannelAuthenticationOptions : AuthenticationSchemeOptions;
