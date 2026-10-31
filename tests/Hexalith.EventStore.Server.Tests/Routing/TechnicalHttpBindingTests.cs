using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises complete versioned technical membership independently of the real MVC/HTTP qualification suite.</summary>
public sealed class TechnicalHttpBindingTests
{
    [Theory]
    [InlineData("null-receiver")]
    [InlineData("null-endpoint")]
    [InlineData("duplicate-receiver")]
    [InlineData("duplicate-action")]
    [InlineData("unknown-peer")]
    [InlineData("wrong-version")]
    [InlineData("missing-actions")]
    [InlineData("missing-methods")]
    [InlineData("null-grant")]
    [InlineData("null-grant-methods")]
    [InlineData("duplicate-grant")]
    public void MalformedMembershipHasNamedInventoryRefusal(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        var endpoint = Endpoint();
        var receiver = new RouteCatalogTechnicalHttpReceiver("eventstore", RouteCatalogTechnicalHttpPolicy.FenceVersion, [endpoint]);
        RouteCatalogTechnicalHttpReceiver[] receivers = scenario switch
        {
            "null-receiver" => [null!],
            "null-endpoint" => [receiver with { Endpoints = [null!] }],
            "duplicate-receiver" => [receiver, receiver],
            "duplicate-action" => [receiver with { Endpoints = [endpoint, endpoint] }],
            "unknown-peer" => [receiver with { AppId = "not-a-required-peer" }],
            "wrong-version" => [receiver with { FenceVersion = FiniteRouteCatalogReceiver.Version }],
            "missing-actions" => [receiver with { Endpoints = [] }],
            "null-grant" => [receiver with { Invocations = [null!] }],
            "null-grant-methods" => [receiver with { Invocations = [new("ui-proxy", "/api/v1/admin", null!)] }],
            "duplicate-grant" => [receiver with { Invocations = [new("ui-proxy", "/api/v1/admin", ["POST"]), new("ui-proxy", "/api/v1/admin", ["POST"])] }],
            _ => [receiver with { Endpoints = [endpoint with { Methods = null! }] }],
        };
        var activation = fixture.Candidate() with { TechnicalHttpReceivers = receivers };
        Should.Throw<RouteCatalogUnavailableException>(() => RouteCatalogActivationBinding.ComputeDigest(activation)).Category.ShouldBe(RouteCatalogRefusalReason.Inventory);
    }

    [Fact]
    public void NewSnapshotDeeplyFreezesMembershipWithoutChangingLegacyConstructor()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        string[] methods = ["POST"], policies = ["AdminFull", "EventStoreHuman"];
        string[] grantMethods = ["POST"];
        RouteCatalogDeclaredInvocation[] grants = [new("ui-proxy-outside-authority", "/api/v1/admin", grantMethods)];
        RouteCatalogTechnicalHttpEndpoint[] endpoints = [Endpoint() with { Methods = methods, Policies = policies }];
        RouteCatalogTechnicalHttpReceiver[] receivers = [new("eventstore", RouteCatalogTechnicalHttpPolicy.FenceVersion, endpoints) { Invocations = grants }];
        var activation = fixture.Candidate() with { TechnicalHttpReceivers = receivers };
        string digest = RouteCatalogActivationBinding.ComputeDigest(activation);
        var snapshot = new CommittedRouteCatalogSnapshot(activation, 1, "eventstore", fixture.Store.Coordinates);
        methods[0] = "DELETE"; policies[0] = "replaced"; endpoints[0] = Endpoint("/replaced"); receivers[0] = receivers[0] with { AppId = "replaced" };
        grantMethods[0] = "DELETE"; grants[0] = new("replaced", "/foreign", ["DELETE"]);
        var frozen = snapshot.TechnicalHttpReceivers.ShouldHaveSingleItem();
        frozen.AppId.ShouldBe("eventstore"); frozen.Endpoints.ShouldHaveSingleItem().Methods.ShouldBe(["POST"]);
        frozen.Endpoints[0].Policies.ShouldBe(["AdminFull", "EventStoreHuman"]);
        frozen.Invocations!.ShouldHaveSingleItem().CallerAppId.ShouldBe("ui-proxy-outside-authority");
        frozen.Invocations[0].HttpMethods.ShouldBe(["POST"]);
        Should.Throw<NotSupportedException>(() => ((IList<string>)frozen.Invocations[0].HttpMethods)[0] = "DELETE");
        snapshot.ActivationBindingDigest.ShouldBe(digest);
        Should.Throw<NotSupportedException>(() => ((IList<string>)frozen.Endpoints[0].Methods)[0] = "DELETE");
        Should.Throw<NotSupportedException>(() => ((IList<RouteCatalogTechnicalHttpEndpoint>)frozen.Endpoints)[0] = Endpoint());
        var legacy = new CommittedRouteCatalogSnapshot(activation.CanonicalCatalog, activation.RootDigest, 1, null, null, "eventstore");
        legacy.TechnicalHttpReceivers.ShouldBeEmpty(); legacy.ActivationBindingDigest.ShouldBeNull();
    }

    [Theory]
    [InlineData("same", true)]
    [InlineData("missing-receiver", false)]
    [InlineData("missing-action", false)]
    [InlineData("extra-action", false)]
    [InlineData("changed-schema", false)]
    [InlineData("changed-verb", false)]
    [InlineData("extra-peer", false)]
    public async Task EachReceiverIndependentlyChecksItsCompleteInstalledInventory(string scenario, bool admitted)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        int local = Array.FindIndex(fixture.Observations, observation => observation.AppId == "eventstore");
        var endpoints = new[] { Endpoint(), Endpoint("/api/v1/admin/tenants/{tenantId}/users", "GET") };
        var actual = fixture.Observations[local] with
        {
            ReceiverSurface = new(RouteCatalogTechnicalHttpPolicy.FenceVersion, endpoints.Select(endpoint => new RouteCatalogReceiverEndpoint(
                endpoint.Pattern, endpoint.Methods, RouteCatalogTechnicalHttpPolicy.Operation, endpoint.Policies, false, true, true)).ToArray()) { TechnicalHttp = endpoints },
        };
        var receiver = new RouteCatalogTechnicalHttpReceiver(actual.AppId, RouteCatalogTechnicalHttpPolicy.FenceVersion, endpoints);
        RouteCatalogTechnicalHttpReceiver[] proposed = scenario switch
        {
            "missing-receiver" => [],
            "missing-action" => [receiver with { Endpoints = [endpoints[0]] }],
            "extra-action" => [receiver with { Endpoints = [.. endpoints, Endpoint("/api/v1/admin/other")] }],
            "changed-schema" => [receiver with { Endpoints = [endpoints[0] with { RequestDigest = new string('b', 64) }, endpoints[1]] }],
            "changed-verb" => [receiver with { Endpoints = [endpoints[0] with { Methods = ["PUT"] }, endpoints[1]] }],
            "extra-peer" => [receiver, receiver with { AppId = fixture.Observations.First(observation => observation.AppId != actual.AppId).AppId }],
            _ => [receiver],
        };
        var candidate = fixture.Candidate() with { TechnicalHttpReceivers = proposed };
        var observer = new ObservedRouteCatalogHost(_ => Task.FromResult(actual));
        if (scenario == "extra-peer") observer = new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations.First(observation => observation.AppId != actual.AppId)));
        if (admitted) (await observer.ObserveAsync(candidate, fixture.Store.Coordinates, TestContext.Current.CancellationToken)).ShouldBe(ObservedRouteCatalogHost.Identity(actual));
        else (await Should.ThrowAsync<RouteCatalogUnavailableException>(() => observer.ObserveAsync(candidate, fixture.Store.Coordinates, TestContext.Current.CancellationToken)))
            .Category.ShouldBe(RouteCatalogRefusalReason.Inventory);
    }

    [Fact]
    public void NonemptyMembershipChangesTheOwnerBindingAndCoversEveryDescriptor()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true); var original = fixture.Candidate();
        var receiver = new RouteCatalogTechnicalHttpReceiver("eventstore", RouteCatalogTechnicalHttpPolicy.FenceVersion, [Endpoint()]);
        var versioned = original with { TechnicalHttpReceivers = [receiver] };
        string digest = RouteCatalogActivationBinding.ComputeDigest(versioned);
        digest.ShouldNotBe(RouteCatalogActivationBinding.ComputeDigest(original));
        digest.ShouldNotBe(RouteCatalogActivationBinding.ComputeDigest(versioned with
        { TechnicalHttpReceivers = [receiver with { Endpoints = [receiver.Endpoints[0] with { FilterDigest = new string('b', 64) }] }] }));
    }

    private static RouteCatalogTechnicalHttpEndpoint Endpoint(string pattern = "/api/v1/admin/tenants/{tenantId}/users", string method = "POST")
        => new(pattern, [method], "synthetic-sdk-action-identity", new string('a', 64), new string('a', 64),
            ["AdminFull", "EventStoreHuman"], new string('a', 64), new string('a', 64));
}
