using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Authentication;

/// <summary>
/// Configuration options for EventStore JWT authentication.
/// </summary>
public sealed record EventStoreAuthenticationOptions : JwtBearerAuthenticationOptions;
