using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Callback descriptors cannot create arbitrary paths, verbs or legacy activation authority.</summary>
public sealed class DaprCallbackActivationBindingTests
{
    [Theory]
    [InlineData("schema2")]
    [InlineData("path")]
    [InlineData("operation")]
    [InlineData("verb")]
    [InlineData("kind")]
    [InlineData("digest")]
    [InlineData("mixed-native")]
    public void IncompatibleOrMutatedCallbackMetadataRefusesBeforeActivation(string change)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = CatalogSchemaThreeFixture.Create(fixture.Envelope);
        var callback = new RouteCatalogDaprCallbackTransport("subscribe", "GET", RouteCatalogTestFixture.Hash("owner"),
            RouteCatalogTestFixture.Hash("framework"), RouteCatalogTestFixture.Hash("inventory"));
        var auxiliary = new RouteCatalogAuxiliaryOperation("eventstore", "dapr/subscribe", "sdk-dapr-callback:subscribe", "gateway") { DaprCallback = callback };
        var candidate = fixture.Candidate() with { CanonicalCatalog = RouteCatalogCodec.Encode(envelope), RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope), AuxiliaryOperations = [auxiliary] };
        RouteCatalogActivationBinding.ComputeDigest(candidate).Length.ShouldBe(64);
        auxiliary = change switch
        {
            "path" => auxiliary with { Method = "arbitrary/callback" },
            "operation" => auxiliary with { Operation = "arbitrary-operation" },
            "verb" => auxiliary with { DaprCallback = callback with { Verbs = "POST" } },
            "kind" => auxiliary with { DaprCallback = callback with { Kind = "arbitrary" } },
            "digest" => auxiliary with { DaprCallback = callback with { FrameworkDigest = "not-a-digest" } },
            "mixed-native" => auxiliary with { NativeActor = new("config", "GET", callback.CodeDigest, callback.FrameworkDigest, callback.InventoryDigest) },
            _ => auxiliary,
        };
        candidate = candidate with { AuxiliaryOperations = [auxiliary] };
        if (change == "schema2") candidate = candidate with { CanonicalCatalog = fixture.Candidate().CanonicalCatalog, RootDigest = fixture.Candidate().RootDigest };
        Should.Throw<RouteCatalogUnavailableException>(() => RouteCatalogActivationBinding.ComputeDigest(candidate));
    }
}
