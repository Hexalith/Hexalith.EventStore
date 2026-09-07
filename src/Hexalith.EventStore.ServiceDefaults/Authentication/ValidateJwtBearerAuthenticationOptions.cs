using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Validates shared JWT bearer options during host startup.
/// </summary>
public sealed class ValidateJwtBearerAuthenticationOptions(
    IHostEnvironment environment,
    ILogger<ValidateJwtBearerAuthenticationOptions>? logger = null)
    : IValidateOptions<JwtBearerAuthenticationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, JwtBearerAuthenticationOptions options)
        => JwtBearerAuthenticationContract.Validate(
            options,
            environment,
            "Authentication:JwtBearer",
            logger);
}
