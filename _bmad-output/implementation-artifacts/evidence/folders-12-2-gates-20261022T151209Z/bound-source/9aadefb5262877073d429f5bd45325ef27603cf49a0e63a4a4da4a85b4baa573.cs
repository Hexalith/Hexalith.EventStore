using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Host.Authentication;

/// <summary>
/// Validates Admin.Server host authentication configuration at startup.
/// </summary>
public sealed class ValidateAdminServerAuthenticationOptions(
    IHostEnvironment environment,
    ILogger<ValidateAdminServerAuthenticationOptions>? logger = null)
    : IValidateOptions<AdminServerAuthenticationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AdminServerAuthenticationOptions options)
        => JwtBearerAuthenticationContract.Validate(
            options,
            environment,
            "Authentication:JwtBearer",
            logger);
}
