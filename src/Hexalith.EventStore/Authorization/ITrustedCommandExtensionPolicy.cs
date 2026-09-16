using System.Security.Claims;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Determines whether one reserved command extension is trusted for an authenticated caller.
/// </summary>
public interface ITrustedCommandExtensionPolicy
{
    /// <summary>
    /// Determines whether the policy accepts the exact reserved extension for the supplied command and caller.
    /// </summary>
    /// <param name="principal">The authenticated command caller.</param>
    /// <param name="command">The submitted command envelope.</param>
    /// <param name="key">The reserved extension key.</param>
    /// <param name="value">The reserved extension value.</param>
    /// <returns><see langword="true"/> when this policy accepts the extension; otherwise, <see langword="false"/>.</returns>
    bool Accepts(ClaimsPrincipal principal, SubmitCommandRequest command, string key, string value);
}
