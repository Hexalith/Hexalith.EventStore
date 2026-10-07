namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Receiver-side settings of one workload-assertion scheme, bound per scheme name.
/// </summary>
/// <remarks>
/// Issuer, signing keys, algorithms, clock skew, and lifetime validation always come from the shared JWT
/// contract (<c>Authentication:JwtBearer</c>); these settings only narrow the accepted audience, callers, and
/// assertion lifetime.
/// </remarks>
public sealed class WorkloadAuthenticationOptions
{
    /// <summary>Gets the default maximum assertion lifetime, measured as <c>exp</c> minus <c>iat</c>.</summary>
    public const int DefaultMaximumLifetimeSeconds = 300;

    /// <summary>Gets the largest lifetime an operator may configure.</summary>
    public const int MaximumConfigurableLifetimeSeconds = 900;

    /// <summary>Gets or sets the audience this receiver accepts, normally its own Dapr application id.</summary>
    public string? Audience { get; set; }

    /// <summary>Gets or sets the workload identities (<c>azp</c>) this receiver accepts.</summary>
    public IList<string> AllowedCallers { get; set; } = [];

    /// <summary>Gets or sets the maximum accepted assertion lifetime in seconds.</summary>
    public int MaximumLifetimeSeconds { get; set; } = DefaultMaximumLifetimeSeconds;

    /// <summary>
    /// Gets the support-safe failure of the settings the scheme needs to authenticate callers, or
    /// <see langword="null"/> when they are usable.
    /// </summary>
    /// <returns>The failure description without configured values.</returns>
    public string? GetConfigurationFailure()
    {
        string? validationFailure = GetValidationFailure();
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        return AllowedCallers is null
            || AllowedCallers.Count == 0
            || AllowedCallers.Any(static caller => string.IsNullOrWhiteSpace(caller))
                ? "AllowedCallers must contain at least one non-blank workload identity."
                : null;
    }

    /// <summary>
    /// Gets the support-safe failure of the settings needed to validate an assertion for this audience, or
    /// <see langword="null"/> when they are usable. Callers are checked separately.
    /// </summary>
    /// <returns>The failure description without configured values.</returns>
    public string? GetValidationFailure()
    {
        if (string.IsNullOrWhiteSpace(Audience))
        {
            return "Audience must be configured.";
        }

        return MaximumLifetimeSeconds is <= 0 or > MaximumConfigurableLifetimeSeconds
            ? $"MaximumLifetimeSeconds must be between 1 and {MaximumConfigurableLifetimeSeconds}."
            : null;
    }
}
