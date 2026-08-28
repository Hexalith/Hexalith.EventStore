using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Operations.Security;

/// <summary>
/// Validates the Dapr application-channel token deployment contract.
/// </summary>
internal static class DaprAppChannelSecurity
{
    internal const string ConfigurationKey = "APP_API_TOKEN";

    internal static string? ValidateConfiguration(IHostEnvironment environment, string? token)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "The Dapr application-channel token must be configured outside Development.");
        }

        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    /// <summary>
    /// Determines whether a request path is guarded by the application-channel token.
    /// </summary>
    /// <remarks>
    /// The platform health endpoints are probed by the orchestrator and by the Dapr sidecar's own app health
    /// check, neither of which carries the app token. Guarding them would leave the workload permanently
    /// unhealthy in exactly the non-Development environments where the token is mandatory. They expose no
    /// retained item, identity, or payload, so leaving them open costs nothing the operator boundary protects.
    /// </remarks>
    internal static bool RequiresToken(PathString path)
        => !path.StartsWithSegments("/health")
            && !path.StartsWithSegments("/alive")
            && !path.StartsWithSegments("/ready");
}
