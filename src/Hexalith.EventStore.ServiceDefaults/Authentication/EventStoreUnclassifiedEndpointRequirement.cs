using Microsoft.AspNetCore.Authorization;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Intentionally has no handler: an unclassified endpoint can never be authorized by credentials.</summary>
internal sealed class EventStoreUnclassifiedEndpointRequirement : IAuthorizationRequirement;
