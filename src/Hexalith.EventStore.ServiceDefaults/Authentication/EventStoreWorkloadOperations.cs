namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Operations an EventStore workload assertion can grant.
/// </summary>
public static class EventStoreWorkloadOperations
{
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
}
