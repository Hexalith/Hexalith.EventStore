using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Admin.Server.Host.Authentication;

/// <summary>
/// Configuration options for Admin.Server host JWT authentication.
/// </summary>
public sealed record AdminServerAuthenticationOptions : JwtBearerAuthenticationOptions;
