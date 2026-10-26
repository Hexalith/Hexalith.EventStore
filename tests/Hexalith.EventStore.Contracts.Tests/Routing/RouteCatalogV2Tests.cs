using System.Text;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

public class RouteCatalogV2Tests
{
    [Fact]
    public void ActorAndComputedQueriesRoundTripWithoutInventingProjectionTargets()
    {
        RouteCatalogEnvelope catalog = Catalog();
        byte[] bytes = RouteCatalogCodec.Encode(catalog);
        RouteCatalogCodec.Encode(RouteCatalogCodec.Decode(bytes)).ShouldBe(bytes);
        catalog.Declarations.Single(d => d.RouteId == "computed").ProjectionType.ShouldBeNull();
        catalog.Routes.Single(r => r.RouteId == "counter-query").Method.ShouldBe("QueryAsync");
        RouteCatalogCodec.ComputeRootDigest(catalog).ShouldBe(RouteCatalogCodec.ComputeRootDigest(Catalog()));
    }

    [Theory]
    [InlineData("unsupported-alias")]
    [InlineData("duplicate-alias")]
    [InlineData("wrong-target")]
    [InlineData("computed-actor")]
    [InlineData("unknown-execution-field")]
    [InlineData("missing-execution-field")]
    [InlineData("v1-extension")]
    public void RejectsInvalidV2BesideValidControl(string scenario)
    {
        RouteCatalogEnvelope valid = Catalog();
        byte[] bytes = RouteCatalogCodec.Encode(valid);
        RouteCatalogCodec.Decode(bytes);
        if (scenario is "unknown-execution-field" or "missing-execution-field")
        {
            string json = Encoding.UTF8.GetString(bytes);
            json = scenario == "unknown-execution-field" ? json.Replace("\"aliasPolicy\":", "\"secret\":\"never-print\",\"aliasPolicy\":", StringComparison.Ordinal)
                : json.Replace(",\"targetKind\":\"domain-service\"", "", StringComparison.Ordinal);
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Decode(Encoding.UTF8.GetBytes(json))).Message.ShouldNotContain("never-print");
            return;
        }
        RouteContractDeclaration first = valid.Declarations[0];
        RouteExecutionDeclaration execution = first.Execution!;
        RouteCatalogEnvelope invalid = scenario switch
        {
            "v1-extension" => valid with { SchemaVersion = 1 },
            _ => valid with { Declarations = [first with { Execution = scenario switch
            {
                "unsupported-alias" => execution with { AliasPolicy = "wildcard" },
                "duplicate-alias" => execution with { Aliases = [.. execution.Aliases, execution.Aliases[0]] },
                "wrong-target" => execution with { TargetKind = "unknown" },
                _ => execution with { TargetKind = "projection-actor", QueryKind = "computed" },
            } }, .. valid.Declarations.Skip(1)] },
        };
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Seal(invalid));
    }

    [Theory]
    [InlineData("IncrementCounter", true)]
    [InlineData("increment-counter", true)]
    [InlineData("Arbitrary.Namespace.IncrementCounter, Arbitrary.Assembly", true)]
    [InlineData("Unknown", false)]
    [InlineData("IncrementCounter , Assembly", false)]
    [InlineData("incrementcounter", false)]
    public void AliasProtocolPreservesActualLookupWithoutUnboundedHandlers(string wire, bool matches)
    {
        var declaration = new RouteContractDeclaration("increment", "command", "counter", "increment-counter", null, "v1", 1,
            "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new("Sample.IncrementCounter", "clr-short-name-v1", ["increment-counter", "IncrementCounter"], "domain-service", null, []));
        CommandDiscriminatorCompatibility.Matches(declaration, wire).ShouldBe(matches);
    }

    [Fact]
    public void NormalizedCommandAliasesCannotOverlapExactQueryAliases()
    {
        RouteCatalogEnvelope fixture = RouteCatalogCodec.Decode(RouteCatalogCodecTests.FixtureBytes());
        RouteContractDeclaration command = fixture.Declarations[0] with
        {
            Execution = new("Namespace.Foo", "clr-short-name-v1", ["create-counter", "Foo"], "domain-service", null, []),
        };
        RouteCatalogEnvelope Join(string queryType)
        {
            RouteContractDeclaration query = new("query", "query", "counter", queryType, null, "v1", null, "workload",
                EventStoreOperationIdentifiers.DomainServiceQuery, null, [], new("Namespace.Query", "exact", [queryType], "domain-service", "computed", []));
            RouteContractDeclaration[] declarations = [command, query];
            return RouteCatalogCodec.Seal(new(2, fixture.Generation, "Development", false, declarations,
                declarations.Select(d => new RouteCatalogEntry(d.RouteId, fixture.Generation, RouteCatalogCodec.ComputeDeclarationDigest(d), "sample",
                    d.Kind == "query" ? "query" : "process", "v1", d.CredentialKind, d.Operation, false, "*")).ToArray(), [fixture.Idempotency[0]], "", ""));
        }
        RouteCatalogCodec.Decode(RouteCatalogCodec.Encode(Join("Namespace.Bar")));
        Should.Throw<RouteCatalogValidationException>(() => Join("Namespace.Foo")).Diagnostics.Single().Code.ShouldBe("ambiguous-handler-alias");
    }

    [Fact]
    public void ResolverScopeIsRequiredAndBoundByFacetAndRootDigests()
    {
        RouteCatalogEnvelope original = Catalog();
        RouteCatalogEnvelope changed = RouteCatalogCodec.Seal(original with
        {
            Routes = [original.Routes[0] with { ResolverTenantPattern = "system" }, .. original.Routes.Skip(1)],
        });
        changed.RouteFacetDigest.ShouldNotBe(original.RouteFacetDigest);
        RouteCatalogCodec.ComputeRootDigest(changed).ShouldNotBe(RouteCatalogCodec.ComputeRootDigest(original));
        foreach (string? invalid in new string?[] { null, "tenant-*", "platform:tenants", "SYSTEM", "" })
        {
            Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.Seal(original with
            {
                Routes = [original.Routes[0] with { ResolverTenantPattern = invalid }, .. original.Routes.Skip(1)],
            }));
        }
    }

    [Theory]
    [InlineData("missing-actor", "invalid-actor-type")]
    [InlineData("query-clr-policy", "unsupported-alias-policy")]
    [InlineData("missing-primary", "missing-primary-alias")]
    [InlineData("invalid-query-kind", "invalid-query-kind")]
    [InlineData("computed-actor", "actor-requires-projection")]
    [InlineData("unexpected-output", "unexpected-projection-outputs")]
    [InlineData("missing-output", "missing-or-invalid-list")]
    [InlineData("duplicate-output", "duplicate-projection-output")]
    [InlineData("invalid-alias", "missing-or-invalid-discriminator")]
    [InlineData("unbounded-handler", "execution-requires-exact-handler")]
    [InlineData("missing-handler", "missing-or-invalid-discriminator")]
    public void NewExecutionRejectionsHavePassingControls(string scenario, string reason)
    {
        RouteContractDeclaration query = Catalog().Declarations[0];
        RouteContractDeclaration projection = new("projection", "projection", "counter", null, "counter", "v1", null,
            "workload", EventStoreOperationIdentifiers.DomainServiceProject, null, [],
            new("Counter.Handler", "exact", ["counter"], "domain-service", null, ["counter"]));
        RouteCatalogValidator.ValidateDeclaration(query);
        RouteCatalogValidator.ValidateDeclaration(projection);
        var invalid = scenario switch
        {
            "missing-actor" => query with { Operation = EventStoreOperationIdentifiers.ProjectionQuery, Execution = query.Execution! with { TargetKind = "projection-actor" } },
            "query-clr-policy" => query with { Execution = query.Execution! with { AliasPolicy = "clr-short-name-v1" } },
            "missing-primary" => query with { Execution = query.Execution! with { Aliases = ["Other"] } },
            "invalid-query-kind" => query with { ProjectionType = "counter", Execution = query.Execution! with { QueryKind = null } },
            "computed-actor" => query with { Operation = EventStoreOperationIdentifiers.ProjectionQuery, Execution = query.Execution! with { TargetKind = "projection-actor", ActorType = "ProjectionActor" } },
            "unexpected-output" => query with { Execution = query.Execution! with { ProjectionOutputs = ["counter"] } },
            "missing-output" => projection with { Execution = projection.Execution! with { ProjectionOutputs = [] } },
            "duplicate-output" => projection with { Execution = projection.Execution! with { ProjectionOutputs = ["counter", "counter"] } },
            "invalid-alias" => query with { Execution = query.Execution! with { Aliases = ["bad alias"] } },
            "unbounded-handler" => query with { MessageType = "*", FallbackTypes = ["some-query"] },
            _ => query with { Execution = query.Execution! with { HandlerType = "" } },
        };
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogValidator.ValidateDeclaration(invalid)).Diagnostics.Single().Code.ShouldBe(reason);
    }

    private static RouteCatalogEnvelope Catalog()
    {
        RouteContractDeclaration[] declarations = [
            new("computed", "query", "tenants", "Namespace.Resolve.v1", null, "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
                new("Namespace.Resolve", "exact", ["Namespace.Resolve.v1"], "domain-service", "computed", [])),
            new("counter-query", "query", "counter", "get-counter-status", "counter", "v1", null, "workload", EventStoreOperationIdentifiers.ProjectionQuery, null, [],
                new("Counter.Query", "exact", ["get-counter-status"], "projection-actor", "projection", [], ActorType: "ProjectionActor"))];
        return RouteCatalogCodec.Seal(new(2, 1, "Development", false, declarations,
            declarations.Select(d => new RouteCatalogEntry(d.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(d), d.Domain == "counter" ? "eventstore" : "tenants",
                d.Domain == "counter" ? "QueryAsync" : "query", "v1", d.CredentialKind, d.Operation, false, "*")).ToArray(), [], "", ""));
    }
}
