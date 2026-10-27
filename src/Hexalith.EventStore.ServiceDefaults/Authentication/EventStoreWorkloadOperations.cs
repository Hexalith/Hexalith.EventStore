using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Operations an EventStore workload assertion can grant.
/// </summary>
public static class EventStoreWorkloadOperations
{
    /// <summary>Process a command through <c>POST /process</c>.</summary>
    public const string DomainServiceProcess = EventStoreOperationIdentifiers.DomainServiceProcess;

    /// <summary>Rebuild aggregate state through <c>POST /replay-state</c>.</summary>
    public const string DomainServiceReplayState = EventStoreOperationIdentifiers.DomainServiceReplayState;

    /// <summary>Execute a query through <c>POST /query</c>.</summary>
    public const string DomainServiceQuery = EventStoreOperationIdentifiers.DomainServiceQuery;

    /// <summary>Dispatch, reconcile, or rebuild projections through the <c>/project</c> family.</summary>
    public const string DomainServiceProject = EventStoreOperationIdentifiers.DomainServiceProject;

    /// <summary>Read the operational catalog through <c>POST /admin/operational-index-metadata</c>.</summary>
    public const string DomainServiceMetadata = EventStoreOperationIdentifiers.DomainServiceMetadata;

    /// <summary>Publish a projection-changed notification to EventStore.</summary>
    public const string ProjectionNotify = EventStoreOperationIdentifiers.ProjectionNotify;

    /// <summary>Submit a target-receipted trusted effect to EventStore.</summary>
    public const string TrustedEffect = EventStoreOperationIdentifiers.TrustedEffect;

    /// <summary>Submit a command through the workload-only gateway.</summary>
    public const string GatewayCommandSubmit = EventStoreOperationIdentifiers.GatewayCommandSubmit;

    /// <summary>Read a command status through the workload-only gateway.</summary>
    public const string GatewayCommandStatus = EventStoreOperationIdentifiers.GatewayCommandStatus;

    /// <summary>Read an event stream through the workload-only gateway.</summary>
    public const string GatewayStreamRead = EventStoreOperationIdentifiers.GatewayStreamRead;

    /// <summary>Resolve one exact owner authority query under a signed requester binding.</summary>
    public const string GatewayOwnerAuthorityQuery = EventStoreOperationIdentifiers.GatewayOwnerAuthorityQuery;
}
