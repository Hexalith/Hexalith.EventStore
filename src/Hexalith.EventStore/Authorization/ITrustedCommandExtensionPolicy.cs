using System.Security.Claims;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Determines whether one reserved command extension is trusted for an authenticated caller.
/// </summary>
public interface ITrustedCommandExtensionPolicy
{
    /// <summary>
    /// Determines whether this policy claims responsibility for the reserved extension key on the supplied command
    /// kind.
    /// </summary>
    /// <param name="domain">The target domain name.</param>
    /// <param name="commandType">The command type discriminator.</param>
    /// <param name="key">The reserved extension key.</param>
    /// <returns><see langword="true"/> when this policy claims the extension; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Claim evaluation deliberately excludes both the extension dictionary and its values. The gateway supplies
    /// the claimed value only to the one policy that claims the key, after rejecting missing or overlapping claims.
    /// The default implementation claims nothing so policy implementations compiled against an earlier contract fail
    /// closed until they opt in to value evaluation.
    /// </remarks>
    bool Claims(string domain, string commandType, string key)
    {
        return false;
    }

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
