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
}
