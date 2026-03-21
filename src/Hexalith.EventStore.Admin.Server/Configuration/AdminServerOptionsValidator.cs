using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Configuration;

/// <summary>
/// Validates <see cref="AdminServerOptions"/> at startup to fail fast on misconfiguration.
/// </summary>
public sealed class AdminServerOptionsValidator : IValidateOptions<AdminServerOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AdminServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string>? failures = null;

        if (string.IsNullOrWhiteSpace(options.StateStoreName))
        {
            (failures ??= []).Add($"{nameof(options.StateStoreName)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.CommandApiAppId))
        {
            (failures ??= []).Add($"{nameof(options.CommandApiAppId)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.TenantServiceAppId))
        {
            (failures ??= []).Add($"{nameof(options.TenantServiceAppId)} must not be empty.");
        }

        if (options.MaxTimelineEvents <= 0)
        {
            (failures ??= []).Add($"{nameof(options.MaxTimelineEvents)} must be greater than zero.");
        }

        if (options.ServiceInvocationTimeoutSeconds <= 0)
        {
            (failures ??= []).Add($"{nameof(options.ServiceInvocationTimeoutSeconds)} must be greater than zero.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
