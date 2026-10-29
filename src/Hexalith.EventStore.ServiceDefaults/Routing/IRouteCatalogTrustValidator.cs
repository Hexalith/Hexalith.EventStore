using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Owner-supplied trust boundary; production instances require an independently authenticated owner binding.</summary>
public interface IRouteCatalogTrustValidator
{
    /// <summary>Verifies the complete lifecycle binding, including predecessor and exact host set.</summary>
    Task ValidateAsync(RouteCatalogActivation activation, string bindingDigest, CancellationToken cancellationToken);
}
