using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Host.Authentication;

/// <summary>
/// Configures <see cref="JwtBearerOptions"/> for Admin.Server host.
/// </summary>
public class ConfigureJwtBearerOptions(IOptions<AdminServerAuthenticationOptions> authOptions) : IConfigureNamedOptions<JwtBearerOptions> {
    public void Configure(string? name, JwtBearerOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (name != JwtBearerDefaults.AuthenticationScheme) {
            return;
        }

        AdminServerAuthenticationOptions authConfig = authOptions.Value;

        JwtBearerAuthenticationContract.Configure(options, authConfig);
    }

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
