using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogAuthorityCoordinateTests
{
    [Theory]
    [InlineData("unchanged")]
    [InlineData("state-key")]
    [InlineData("component")]
    [InlineData("endpoint")]
    [InlineData("component-document")]
    public async Task PreviouslyCommittedAuthorityRefusesEffectiveCellDrift(string field)
    {
        var fixture = new RouteCatalogTestFixture(); var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        var before = (await fixture.Store.ReadAsync(default)).State;
        var original = fixture.Store.Coordinates;
        var changed = field switch
        {
            "state-key" => original with { StateKey = "another-previously-valid-key" },
            "component" => original with { StateStore = "another-previously-valid-component" },
            "endpoint" => original with { HttpEndpoint = "http://127.0.0.1:3599/" },
            _ => original,
        };
        fixture.Store.Coordinates = changed;
        for (int index = 0; index < fixture.Observations.Length; index++)
            fixture.Observations[index] = fixture.Observations[index] with
            {
                AuthorityCoordinates = changed,
                TopologyDocuments = field == "component-document"
                    ? [Component("wrong-component", fixture.Hosts.Select(h => h.AppId).ToArray()), .. fixture.Observations[index].TopologyDocuments.Skip(1)]
                    : field == "component" ? [Component(changed.StateStore, fixture.Hosts.Select(h => h.AppId).ToArray()), .. fixture.Observations[index].TopologyDocuments.Skip(1)]
                    : fixture.Observations[index].TopologyDocuments,
            };
        if (field == "unchanged") (await fixture.Authorities[0].ReadbackAsync()).Serving.ShouldBeTrue();
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadbackAsync());
        var after = (await fixture.Store.ReadAsync(default)).State;
        after.Committed!.RootDigest.ShouldBe(before.Committed!.RootDigest); after.Epoch.ShouldBe(before.Epoch); after.Pending.ShouldBeNull(); after.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachExecutableTargetRequiresItsActualCompiledDeclaration(bool missing)
    {
        var fixture = new RouteCatalogTestFixture(); var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        int sample = Array.FindIndex(fixture.Hosts, h => h.AppId == "sample");
        if (missing) fixture.Observations[sample] = fixture.Observations[sample] with { Declarations = [] };
        var identities = fixture.Observations.Select(ObservedRouteCatalogHost.Identity).ToArray();
        var candidate = fixture.Candidate(2, first.RootDigest) with { RequiredHosts = identities };
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        await fixture.Authorities[0].PrepareAsync(candidate);
        var receiver = fixture.CreateAuthority(identities[sample]);
        if (missing) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => receiver.ReadyAsync(candidate.AttemptId));
        else await receiver.ReadyAsync(candidate.AttemptId);
        (await fixture.Store.ReadAsync(default)).State.Committed!.RootDigest.ShouldBe(first.RootDigest);
    }

    [Fact]
    public void EffectiveStateTransportAndObservationUseTheSameNormalizedRuntimeCoordinates()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DAPR_HTTP_ENDPOINT"] = "http://LOCALHOST:3500", ["DAPR_HTTP_PORT"] = "9999" }).Build();
        var options = Options.Create(new RouteCatalogHostOptions { StateStore = "selected-component", StateKey = "selected-key" });
        var store = new DaprRouteCatalogStateStore(Substitute.For<IHttpClientFactory>(), configuration, options);
        store.Coordinates.ShouldBe(new("selected-component", "selected-key", "http://localhost:3500/"));
        store.Coordinates.ShouldBe(RouteCatalogStateCoordinates.Capture(configuration, options.Value));
        var fixture = new RouteCatalogTestFixture();
        Should.Throw<RouteCatalogUnavailableException>(() => ObservedRouteCatalogHost.Identity(fixture.Observations[0] with { AuthorityCoordinates = null }));
    }

    [Theory]
    [InlineData("state-key")]
    [InlineData("component")]
    [InlineData("endpoint")]
    public async Task SeparatelyValidCellsCannotReuseTheOldHostIdentity(string field)
    {
        var first = new RouteCatalogTestFixture(); await first.ActivateAsync(first.Candidate());
        var original = first.Store.Coordinates;
        var coordinates = field switch
        {
            "state-key" => original with { StateKey = "another-valid-cell" },
            "component" => original with { StateStore = "another-valid-component" },
            _ => original with { HttpEndpoint = "http://127.0.0.1:3599/" },
        };
        var second = new RouteCatalogTestFixture(coordinates); await second.ActivateAsync(second.Candidate());
        (await second.Authorities[0].ReadbackAsync()).Serving.ShouldBeTrue();
        second.Hosts[0].ShouldNotBe(first.Hosts[0]);
        var options = Options.Create(new RouteCatalogHostOptions { Identity = first.Hosts[0], Profile = "Development", DevelopmentFixture = true });
        var moved = new RouteCatalogAuthority(second.Store, Substitute.For<IRouteCatalogTrustValidator>(), options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(second.Observations[0])));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => moved.ReadbackAsync());
        (await first.Authorities[0].ReadbackAsync()).Serving.ShouldBeTrue();
        (await second.Authorities[0].ReadbackAsync()).Serving.ShouldBeTrue();
    }

    private static byte[] Component(string name, string[] appIds) => JsonSerializer.SerializeToUtf8Bytes(new
    { kind = "Component", metadata = new { name }, spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = appIds });
}
