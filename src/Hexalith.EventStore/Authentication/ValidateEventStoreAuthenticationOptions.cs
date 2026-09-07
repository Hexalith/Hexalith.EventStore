using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Authentication;

/// <summary>
/// Validates EventStore JWT authentication configuration at startup.
/// </summary>
public sealed class ValidateEventStoreAuthenticationOptions(
    IHostEnvironment environment,
    ILogger<ValidateEventStoreAuthenticationOptions>? logger = null)
    : IValidateOptions<EventStoreAuthenticationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EventStoreAuthenticationOptions options)
        => JwtBearerAuthenticationContract.Validate(
            options,
            environment,
            "Authentication:JwtBearer",
            logger);
}
