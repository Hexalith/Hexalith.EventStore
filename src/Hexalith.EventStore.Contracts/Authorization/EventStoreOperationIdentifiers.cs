namespace Hexalith.EventStore.Contracts.Authorization;

/// <summary>
/// Contracts-owned operation identifiers. Authentication and assertion handling remain in ServiceDefaults.
/// </summary>
public static class EventStoreOperationIdentifiers
{
    /// <summary>Read the persisted committed catalog through a protected Dapr generation readback.</summary>
    public const string RouteCatalogRead = "eventstore:route-catalog-read";
    /// <summary>Coordinate an independently trusted catalog activation through the protected host lifecycle API.</summary>
    public const string RouteCatalogActivate = "eventstore:route-catalog-activate";
    /// <summary>Execute a projection query through ProjectionActor.QueryAsync; catalog metadata only.</summary>
    public const string ProjectionQuery = "projection:query";

    /// <summary>Process a command through <c>POST /process</c>.</summary>
    public const string DomainServiceProcess = "domain-service:process";

    /// <summary>Rebuild aggregate state through <c>POST /replay-state</c>.</summary>
    public const string DomainServiceReplayState = "domain-service:replay-state";

    /// <summary>Execute a query through <c>POST /query</c>.</summary>
    public const string DomainServiceQuery = "domain-service:query";

    /// <summary>Dispatch, reconcile, or rebuild projections through the <c>/project</c> family.</summary>
    public const string DomainServiceProject = "domain-service:project";

    /// <summary>Read the operational catalog through <c>POST /admin/operational-index-metadata</c>.</summary>
    public const string DomainServiceMetadata = "domain-service:metadata";

    /// <summary>Publish a projection-changed notification to EventStore.</summary>
    public const string ProjectionNotify = "projection:notify";

    /// <summary>Submit a target-receipted trusted effect to EventStore.</summary>
    public const string TrustedEffect = "eventstore:trusted-effect";

    /// <summary>Submit a command through the workload-only gateway.</summary>
    public const string GatewayCommandSubmit = "eventstore:command-submit";

    /// <summary>Read a command status through the workload-only gateway.</summary>
    public const string GatewayCommandStatus = "eventstore:command-status";

    /// <summary>Read an event stream through the workload-only gateway.</summary>
    public const string GatewayStreamRead = "eventstore:stream-read";

    /// <summary>Resolve one exact owner authority query under a signed requester binding.</summary>
    public const string GatewayOwnerAuthorityQuery = "eventstore:owner-authority-query";
}
