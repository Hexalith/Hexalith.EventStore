using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Real SDK MVC actions retain the signed public producer's finite default while the composite remains opt-in.</summary>
public sealed class LegacyTechnicalReceiverCompatibilityTests
{
    [Theory]
    [InlineData("exact", 200)]
    [InlineData("unactivated", 503)]
    [InlineData("missing-root", 503)]
    [InlineData("wrong-root", 503)]
    [InlineData("wrong-epoch", 503)]
    [InlineData("duplicate-root", 503)]
    [InlineData("duplicate-epoch", 503)]
    [InlineData("changed-policy", 403)]
    [InlineData("descendant", 403)]
    public async Task DefaultFiniteHttpRequiresActualCommittedCodeVerbAndGenerationBeforeEffects(string scenario, int expected)
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken, legacyFinite: true, activate: scenario != "unactivated",
            invocations: [new("eventstore-admin-server", "/api/v1/admin/storage/snapshot-policy", ["PUT"])]);
        if (scenario != "unactivated")
        {
            var surface = fixture.App.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            surface.FenceVersion.ShouldBe("eventstore-finite-sdk-http-receiver-v2");
            surface.TechnicalHttp.ShouldBeNull();
            var committed = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!;
            committed.TechnicalHttpReceivers.ShouldBeNull();
            if (scenario != "missing-root") fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.RootHeader,
                scenario == "wrong-root" ? new string('0', 64) : committed.RootDigest);
            fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.EpochHeader, scenario == "wrong-epoch" ? "999" : "1");
            if (scenario == "duplicate-root") fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.RootHeader, committed.RootDigest);
            if (scenario == "duplicate-epoch") fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.EpochHeader, "1");
        }
        if (scenario == "changed-policy")
        {
            var action = ((IEndpointRouteBuilder)fixture.App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
                .Single(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "api/v1/admin/storage/snapshot-policy"
                    && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("PUT"));
            action.Metadata.GetOrderedMetadata<IAuthorizeData>().First().Policy = "unowned-policy";
        }
        string path = "/api/v1/admin/storage/snapshot-policy" + (scenario == "descendant" ? "/extra" : string.Empty);
        using var response = await fixture.Client.PutAsJsonAsync(path, new SnapshotPolicySetRequest("tenant-a", "orders", "order", 25), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe((HttpStatusCode)expected);
        fixture.State.ByteStoreContains("admin:storage-snapshot-policies:tenant-a").ShouldBe(expected == 200);
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }
}
