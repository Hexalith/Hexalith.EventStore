using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Explicit local composition facts. Absence is a readiness and dispatch failure.</summary>
public sealed class RouteCatalogHostOptions
{
    /// <summary>Gets or sets the independently observed local host identity.</summary>
    public RouteCatalogHostIdentity? Identity { get; set; }

    /// <summary>Gets or sets the exact profile. Development fixtures never authorize production.</summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>Gets or sets whether an explicitly separate Development fixture is enabled.</summary>
    public bool DevelopmentFixture { get; set; }

    /// <summary>Gets or sets the independently configured exact Dapr recipients of catalog control calls.</summary>
    public IReadOnlyList<string> ControlPlaneAppIds { get; set; } = [];

    /// <summary>Gets or sets explicit v2 invocation intent and owned-coordinate enforcement for technical cohorts.</summary>
    public bool RequireTechnicalHttpBinding { get; set; }

    /// <summary>Gets or sets explicit private Debug/Development reuse of deterministic canonical decoding within one fresh acquisition.</summary>
    public bool ScopedDecodeReuse { get; set; }

    /// <summary>Gets or sets explicit private Debug/Development coordination of local lease completion with admission.</summary>
    /// <remarks>Default false; no lifecycle, remote enrollment, durable fence or fresh validation semantics change.</remarks>
    public bool SerializeLocalLeaseRelease { get; set; }

    /// <summary>Gets or sets explicit private Debug/Development retention of bounded deterministic validated document bytes.</summary>
    /// <remarks>Default false; no state, ETag, host, trust, permission or dispatch result is retained.</remarks>
    public bool RetainValidatedCatalogBytes { get; set; }

    /// <summary>Gets or sets explicit private Debug/Development reuse of this authority's live receiver scope for nested metadata transport.</summary>
    /// <remarks>Default false; independent receivers still acquire fresh authority, and the parent's actual fence and remote enrollment remain owned by its receiver.</remarks>
    public bool ReuseHeldMetadataDispatch { get; set; }

    /// <summary>Gets or sets the owner factory for a separate redirect-disabled invocation primary transport.
    /// Custom transports require this factory; it must preserve their effective security/connection settings,
    /// return a separately owned handler and contain no application delegating handlers.</summary>
    public Func<HttpMessageHandler, HttpMessageHandler>? InvocationTransportFactory { get; set; }

    /// <summary>Gets or sets explicit private Development owner-sidecar read coordinates and owner-specific API credentials.</summary>
    public IReadOnlyList<DevelopmentActorStateOwner> DevelopmentActorStateOwners { get; set; } = [];

    internal DevelopmentActorStateOwnerBoundary? ActorStateOwnerBoundary { get; set; }

    /// <summary>Gets or sets the shared Dapr authority component. Dedicated authorities use keyPrefix name;
    /// legacy none components require an independently isolated backend when callers lack business-store access.</summary>
    public string StateStore { get; set; } = "route-catalog";

    /// <summary>Gets or sets the shared profile-specific state key.</summary>
    public string StateKey { get; set; } = "route-catalog-authority-v1";
}
