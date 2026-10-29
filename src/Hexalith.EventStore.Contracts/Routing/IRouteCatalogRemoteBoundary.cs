namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Enrolls actual remote execution against the complete active set of durable catalog fences.</summary>
public interface IRouteCatalogRemoteBoundary
{
    /// <summary>Atomically enrolls every active fence, refusing execution if any scope is closed.</summary>
    void Enroll();

    /// <summary>Retains all enrolled fences after ambiguous remote completion.</summary>
    void Retain();

    /// <summary>Completes enrollment without clearing any retained fence.</summary>
    void Complete();
}
