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
        if (gatewayStatusBase is null) {
            location = null;
            return false;
        }

        location = gatewayStatusBase.AbsoluteUri.TrimEnd('/') + "/api/v1/commands/status/" + Uri.EscapeDataString(statusKey);
        return true;
    }
}
