namespace Hexalith.EventStore.Testing.Integration;

/// <summary>Consumes explicitly declared domain-service and sidecar endpoints supplied by Platform.</summary>
public abstract class DaprDomainServiceTestFixtureBase : AspireTopologyFixtureBase
{
    /// <summary>Gets the exact domain-service HTTP binding.</summary>
    protected abstract TestEnvironmentEndpoint DomainService { get; }

    /// <summary>Gets the exact Dapr HTTP binding.</summary>
    protected abstract TestEnvironmentEndpoint Sidecar { get; }

    /// <inheritdoc/>
    protected override IReadOnlyList<TestEnvironmentEndpoint> Resources => [DomainService, Sidecar];

    /// <summary>Gets the supplied Dapr endpoint without inferring a local port.</summary>
    public string DaprHttpEndpoint => Environment.GetEndpoint(Sidecar.ResourceId, Sidecar.ModuleId, Sidecar.EndpointName).AbsoluteUri.TrimEnd('/');

    /// <summary>Gets the supplied domain-service endpoint.</summary>
    public string AppEndpoint => Environment.GetEndpoint(DomainService.ResourceId, DomainService.ModuleId, DomainService.EndpointName).AbsoluteUri.TrimEnd('/');

    /// <summary>Gets the bounded domain-service client.</summary>
    public HttpClient DomainClient => Client(DomainService.ResourceId);
}
