using System.Collections;
using System.Reflection;

using Dapr.Client;

using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogMetadataCacheTests
{
    [Fact]
    public async Task OperationalMetadataCacheEvictsEveryObsoleteCommittedEpoch()
    {
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate();
        long epoch = 1;
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(
            new(candidate.CanonicalCatalog, candidate.RootDigest, epoch), () => Task.CompletedTask));
        var registry = new DaprDomainQueryHandlerRegistry(authority, Substitute.For<DaprClient>(), Options.Create(new CommandStatusOptions()), NullLogger<DaprDomainQueryHandlerRegistry>.Instance);
        var cache = (ICollection)typeof(DaprDomainQueryHandlerRegistry).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry)!;
        for (; epoch <= 20; epoch++)
        {
            (await registry.SupportsQueryAsync("tenants", "get-tenant")).ShouldBeTrue();
            (await registry.SupportsQueryAsync("counter", "get-counter-status")).ShouldBeFalse();
            cache.Count.ShouldBe(2);
        }
    }
}
