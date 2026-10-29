using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Isolates direct endpoint wire-contract tests, which intentionally bypass the HTTP auth/routing pipeline.</summary>
internal sealed class DomainRouteCatalogIsolatedServices(IServiceProvider inner) : IServiceProvider
{
    private readonly DomainRouteCatalogIsolatedGuard _guard = new();
    public object? GetService(Type serviceType) => serviceType == typeof(IDomainRouteCatalogGuard) ? _guard : inner.GetService(serviceType);
}
