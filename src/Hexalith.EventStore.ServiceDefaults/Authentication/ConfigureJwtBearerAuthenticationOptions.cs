using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Configures the default bearer scheme from shared JWT authentication options.
/// </summary>
public sealed class ConfigureJwtBearerAuthenticationOptions(
    IOptions<JwtBearerAuthenticationOptions> authenticationOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    /// <inheritdoc />
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            JwtBearerAuthenticationContract.Configure(options, authenticationOptions.Value);
        }
    }

    /// <inheritdoc />
    public void Configure(JwtBearerOptions options)
        => Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
