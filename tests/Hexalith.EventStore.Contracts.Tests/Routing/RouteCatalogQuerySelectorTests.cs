using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

public sealed class RouteCatalogQuerySelectorTests
{
    [Fact]
    public void OptionalMetadataPreservesOriginalConstructorAndDeconstructSignatures()
    {
        Type[] signature = [typeof(string), typeof(string), typeof(IReadOnlyList<string>), typeof(string),
            typeof(string), typeof(IReadOnlyList<string>), typeof(string), typeof(bool)];
        typeof(RouteExecutionDeclaration).GetConstructor(signature).ShouldNotBeNull();
        typeof(RouteExecutionDeclaration).GetMethod("Deconstruct", signature.Select(type => type.MakeByRefType()).ToArray()).ShouldNotBeNull();
        typeof(RouteExecutionDeclaration).GetConstructors().ShouldHaveSingleItem().GetParameters().Length.ShouldBe(8);
        Query(null).Execution!.CompatibilityProjectionActorTypes.ShouldBeNull();
    }

    [Fact]
    public void AbsentSelectorsPreserveExistingIndependentCanonicalGoldens()
    {
        foreach (string fixture in new[] { "CanonicalCatalog.json", "V2CanonicalCatalog.json" })
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Routing", "Fixtures", fixture));
            var catalog = RouteCatalogCodec.Decode(bytes);
            RouteCatalogCodec.Encode(catalog).ShouldBe(bytes);
            Encoding.UTF8.GetString(bytes).ShouldNotContain("compatibilityProjectionActorTypes");
            foreach (var declaration in catalog.Declarations)
                declaration.Execution?.CompatibilityProjectionActorTypes.ShouldBeNull();
        }
    }

    [Fact]
    public void OwnerSelectorsRoundTripCanonicallyAndBindDeclarationFacetAndRoot()
    {
        var declaration = Query(["LegacyWidgetQueryActor", "AnotherWidgetQueryActor"]);
        var catalog = Catalog(declaration);
        // Independent SHA-256 of the canonical JSON declaration preimage (Python json/hashlib).
        RouteCatalogCodec.ComputeDeclarationDigest(declaration).ShouldBe("675ba80053808cd4d09886bdae9ed516d76f544ecffd651f3d528060f35e6305");
        byte[] bytes = RouteCatalogCodec.Encode(catalog);
        var decoded = RouteCatalogCodec.Decode(bytes);
        decoded.Declarations[0].Execution!.CompatibilityProjectionActorTypes.ShouldBe(["AnotherWidgetQueryActor", "LegacyWidgetQueryActor"]);
        RouteCatalogCodec.Encode(Catalog(Query(["AnotherWidgetQueryActor", "LegacyWidgetQueryActor"]))).ShouldBe(bytes);
        var changed = Query(["ForeignWidgetQueryActor"]);
        var changedCatalog = Catalog(changed);
        RouteCatalogCodec.ComputeDeclarationDigest(changed).ShouldNotBe(RouteCatalogCodec.ComputeDeclarationDigest(declaration));
        changedCatalog.RouteFacetDigest.ShouldNotBe(catalog.RouteFacetDigest);
        RouteCatalogCodec.ComputeRootDigest(changedCatalog).ShouldNotBe(RouteCatalogCodec.ComputeRootDigest(catalog));
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Seal(catalog with { Declarations = [changed] }))
            .Diagnostics.Single().Code.ShouldBe("declaration-digest-mismatch");
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(RouteCatalogCodec.Encode(changedCatalog), RouteCatalogCodec.ComputeRootDigest(catalog)))
            .Diagnostics.Single().Code.ShouldBe("root-digest-mismatch");
    }

    [Theory]
    [InlineData("empty", "invalid-compatibility-selector-count")]
    [InlineData("too-many", "invalid-compatibility-selector-count")]
    [InlineData("duplicate", "duplicate-compatibility-selector")]
    [InlineData("null", "invalid-compatibility-selector")]
    [InlineData("blank", "invalid-compatibility-selector")]
    [InlineData("whitespace", "invalid-compatibility-selector")]
    [InlineData("wildcard", "invalid-compatibility-selector")]
    [InlineData("colon", "invalid-compatibility-selector")]
    [InlineData("long", "invalid-compatibility-selector")]
    [InlineData("unicode", "invalid-compatibility-selector")]
    public void InvalidFiniteSelectorsRefuseBesideValidControl(string scenario, string reason)
    {
        RouteCatalogValidator.ValidateDeclaration(Query([new string('A', 64), "Valid.Actor_Type-1+Nested"]));
        IReadOnlyList<string> invalid = scenario switch
        {
            "empty" => [],
            "too-many" => Enumerable.Range(0, 17).Select(index => "Actor" + index).ToArray(),
            "duplicate" => ["Actor", "Actor"],
            "null" => [null!],
            "blank" => [""],
            "whitespace" => [" Actor"],
            "wildcard" => ["Actor*"],
            "colon" => ["Actor:type"],
            "long" => [new string('A', 65)],
            _ => ["Äctor"],
        };
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogValidator.ValidateDeclaration(Query(invalid)))
            .Diagnostics.Single().Code.ShouldBe(reason);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("projection")]
    [InlineData("computed")]
    [InlineData("actor")]
    public void CompatibilityAssertionsCannotEnrollOtherExecutionKinds(string kind)
    {
        var query = Query(["LegacyWidgetQueryActor"]);
        var invalid = kind switch
        {
            "command" => query with { Kind = "command", ProjectionType = null, MessageIdVersion = 1, Operation = EventStoreOperationIdentifiers.DomainServiceProcess },
            "projection" => query with { Kind = "projection", MessageType = null, Operation = EventStoreOperationIdentifiers.DomainServiceProject },
            "computed" => query with { ProjectionType = null, Execution = query.Execution! with { QueryKind = "computed" } },
            _ => query with { Operation = EventStoreOperationIdentifiers.ProjectionQuery, Execution = query.Execution! with { TargetKind = "projection-actor", ActorType = "ProjectionActor" } },
        };
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogValidator.ValidateDeclaration(invalid))
            .Diagnostics.Single().Code.ShouldBe("invalid-compatibility-selector-target");
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogValidator.ValidateDeclaration(query with
        { Execution = query.Execution! with { ActorType = "LegacyWidgetQueryActor" } }))
            .Diagnostics.Single().Code.ShouldBe("invalid-actor-type");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("LegacyWidgetQueryActor", true)]
    [InlineData("legacywidgetqueryactor", false)]
    [InlineData("ForeignQueryActor", false)]
    [InlineData("ProjectionActor", false)]
    [InlineData("Sample.WidgetQuery", false)]
    [InlineData("get-widget", false)]
    [InlineData("*", false)]
    public void SuppliedSelectorRequiresItsExactCommittedEnrollment(string? selector, bool matches)
    {
        var query = Query(["LegacyWidgetQueryActor"]);
        RouteCatalogQuerySelectorPolicy.Matches(query, selector).ShouldBe(matches);
        if (selector is not null) RouteCatalogQuerySelectorPolicy.Matches(Query(null), selector).ShouldBeFalse();
        RouteCatalogQuerySelectorPolicy.Matches(Query(null), null).ShouldBeTrue();
        var actor = Query(null) with { Operation = EventStoreOperationIdentifiers.ProjectionQuery,
            Execution = Query(null).Execution! with { TargetKind = "projection-actor", ActorType = "ProjectionActor" } };
        RouteCatalogValidator.ValidateDeclaration(actor);
        RouteCatalogQuerySelectorPolicy.Matches(actor, "ProjectionActor").ShouldBeTrue();
        RouteCatalogQuerySelectorPolicy.Matches(actor, "LegacyWidgetQueryActor").ShouldBeFalse();
    }

    [Fact]
    public void NoncanonicalSelectorOrderAndExplicitNullRefuseReadback()
    {
        string canonical = Encoding.UTF8.GetString(RouteCatalogCodec.Encode(Catalog(Query(["ActorA", "ActorB"]))));
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(Encoding.UTF8.GetBytes(
            canonical.Replace("[\"ActorA\",\"ActorB\"]", "[\"ActorB\",\"ActorA\"]", StringComparison.Ordinal))))
            .Diagnostics.Single().Code.ShouldBe("noncanonical-encoding");
        string absent = Encoding.UTF8.GetString(RouteCatalogCodec.Encode(Catalog(Query(null))));
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(Encoding.UTF8.GetBytes(
            absent.Replace("\"actorType\":null", "\"actorType\":null,\"compatibilityProjectionActorTypes\":null", StringComparison.Ordinal))))
            .Diagnostics.Single().Code.ShouldBe("noncanonical-encoding");
    }

    private static RouteContractDeclaration Query(IReadOnlyList<string>? selectors) => new("widget-query", "query", "widget", "get-widget", "widgets", "v1", null,
        "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new("Sample.WidgetQuery", "exact", ["get-widget"], "domain-service", "projection", []) { CompatibilityProjectionActorTypes = selectors });

    private static RouteCatalogEnvelope Catalog(RouteContractDeclaration query) => RouteCatalogCodec.Seal(new(2, 1, "Development", false, [query],
        [new(query.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(query), "widget-owner", "query", "v1", query.CredentialKind, query.Operation, false, "tenant-a")], [], "", ""));
}
