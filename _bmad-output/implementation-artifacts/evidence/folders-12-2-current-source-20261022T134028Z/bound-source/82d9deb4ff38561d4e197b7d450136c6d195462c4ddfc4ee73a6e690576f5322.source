using System.Diagnostics.CodeAnalysis;

namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// Builds the absolute command-status <c>Location</c> URL for a generated command controller's
/// <c>202 Accepted</c> response, or reports that none can be built (fail-closed).
/// </summary>
public interface ICommandStatusLocationBuilder {
    /// <summary>
    /// Tries to build the absolute command-status <c>Location</c> URL for the supplied tracking key.
    /// </summary>
    /// <param name="statusKey">The gateway-owned command-status tracking key.</param>
    /// <param name="location">
    /// When this method returns <see langword="true"/>, the absolute command-status URL; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when a gateway status base is configured; otherwise <see langword="false"/>.</returns>
    bool TryBuild(string statusKey, [NotNullWhen(true)] out string? location);
}
