namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Explicit private-run owner sidecar coordinates; never inferred from the caller's token.</summary>
public sealed class DevelopmentActorStateOwner
{
    /// <summary>Gets or sets the actual selected owner app identity.</summary>
    public string AppId { get; set; } = "";
    /// <summary>Gets or sets the actual owner Dapr HTTP endpoint.</summary>
    public string HttpEndpoint { get; set; } = "";
    /// <summary>Gets or sets that owner's distinct run-private API credential.</summary>
    public string ApiToken { get; set; } = "";
}
