namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Credential-free coordinates of an independently selected owner sidecar in one private Development run.</summary>
public sealed record DevelopmentActorStateOwnerObservation(string AppId, string HttpEndpoint, bool ApiTokenConfigured);
