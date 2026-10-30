using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Independently captured local executable and loaded topology facts. Factories must read actual registrations and retained loaded inputs.</summary>
/// <param name="HostId">Logical host.</param>
/// <param name="AppId">Actual Dapr app ID.</param>
/// <param name="Declarations">Declarations from compiled owners of the actual local registrations or generated contracts.</param>
/// <param name="Targets">Actual configured executable targets, never targets copied from the proposed candidate.</param>
/// <param name="RequiredHostIds">Exact host inventory from the observed topology.</param>
/// <param name="TopologyDocuments">Retained actual component/ACL/sidecar-argument inputs; no credential material.</param>
/// <param name="AuthenticationProfile">Observed shared validation settings without secrets or key material.</param>
/// <param name="AdmissionPolicies">Installed adapters, enabled policy and available key generations of this local consumer, captured without a candidate.</param>
/// <param name="ProjectionCapabilities">Fingerprints and handler inventories from actual local named projection registrations.</param>
/// <param name="AuxiliaryOperations">Actual local endpoint methods and their effective canonical operation policies by domain.</param>
/// <param name="AuthorityCoordinates">Independently selected effective authority state component, key and local sidecar endpoint.</param>
public sealed record RouteCatalogHostObservation(string HostId, string AppId,
    IReadOnlyList<RouteContractDeclaration> Declarations, IReadOnlyList<RouteCatalogEntry> Targets,
    IReadOnlyList<string> RequiredHostIds, IReadOnlyList<byte[]> TopologyDocuments, byte[] AuthenticationProfile,
    IReadOnlyList<IdempotencyCatalogEntry>? AdmissionPolicies = null,
    IReadOnlyList<RouteCatalogProjectionCapability>? ProjectionCapabilities = null,
    IReadOnlyList<RouteCatalogAuxiliaryOperation>? AuxiliaryOperations = null,
    RouteCatalogStateCoordinates? AuthorityCoordinates = null,
    RouteCatalogReceiverSurface? ReceiverSurface = null,
    IReadOnlyList<RouteOperationContract>? Operations = null);
