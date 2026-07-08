using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// Default <see cref="ICommandStatusLocationBuilder"/> that composes an absolute gateway status URL from
/// <see cref="CommandStatusLocationOptions.GatewayStatusBase"/> and fails closed when no base is configured.
/// </summary>
/// <param name="options">The command-status Location options.</param>
internal sealed class CommandStatusLocationBuilder(IOptions<CommandStatusLocationOptions> options) : ICommandStatusLocationBuilder {
    /// <inheritdoc/>
    public bool TryBuild(string statusKey, [NotNullWhen(true)] out string? location) {
        ArgumentException.ThrowIfNullOrWhiteSpace(statusKey);

        Uri? gatewayStatusBase = options.Value.GatewayStatusBase;

        // Fail closed if the configured base is absent or not an absolute URI. Guards the public settable
        // CommandStatusLocationOptions.GatewayStatusBase against a value wired directly via services.Configure
        // (bypassing the validating AddEventStoreCommandStatusLocation helper): calling AbsoluteUri on a
        // relative Uri would throw at request time and turn an already-submitted command's 202 into a 500.
        if (gatewayStatusBase is null || !gatewayStatusBase.IsAbsoluteUri) {
            location = null;
            return false;
        }

        location = gatewayStatusBase.AbsoluteUri.TrimEnd('/') + "/api/v1/commands/status/" + Uri.EscapeDataString(statusKey);
        return true;
    }
}
