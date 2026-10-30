using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Contracts.Tests.Routing;

public class RouteCatalogV3Tests
{
    [Theory]
    [InlineData("missing-operation")]
    [InlineData("missing-eligibility")]
    [InlineData("missing-schema")]
    [InlineData("duplicate-operation")]
    [InlineData("conflicting-operation")]
    [InlineData("duplicate-declaration")]
    public void IncompleteOrAmbiguousOwnerInputsRefuse(string fault)
    {
        var input = Input(8);
        var changed = input.Operations[0] with { Eligibility = "UI-only" };
        changed = changed with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(changed) };
        input = fault switch
        {
            "missing-operation" => input with { Operations = input.Operations.Skip(1).ToArray() },
            "missing-eligibility" => input with { Operations = [input.Operations[0] with { Eligibility = "" }, .. input.Operations.Skip(1)] },
            "missing-schema" => input with { Operations = [input.Operations[0] with { RequestSchema = default }, .. input.Operations.Skip(1)] },
            "duplicate-operation" => input with { Operations = [.. input.Operations, input.Operations[0]] },
            "conflicting-operation" => input with { Operations = [.. input.Operations, changed] },
            _ => input with { Declarations = [.. input.Declarations, input.Declarations[0]] },
        };
        Should.Throw<RouteCatalogValidationException>(() => VersionedRouteCatalogBuilder.Build(input));
    }

    [Fact]
    public void SuppliedCommittedRootMustMatchTheExactHistoryBytes()
    {
        var previous = VersionedRouteCatalogBuilder.Build(Input(8));
        Should.Throw<RouteCatalogValidationException>(() => VersionedRouteCatalogBuilder.Build(Input(9) with
        { CommittedPredecessor = RouteCatalogCodec.Encode(previous), CommittedPredecessorRootDigest = new string('0', 64) }));
    }
    [Theory]
    [InlineData("CanonicalCatalog.json")]
    [InlineData("V2CanonicalCatalog.json")]
    public void HistoricalGoldenBytesRemainExact(string fixture)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Routing", "Fixtures", fixture));
        RouteCatalogCodec.Encode(RouteCatalogCodec.Decode(bytes)).ShouldBe(bytes);
    }

    [Fact]
    public void SchemaThreeIsCanonicalAndOperationDigestIsIndependentOfDeclaration()
    {
        var input = Input(8);
        var first = VersionedRouteCatalogBuilder.Build(input);
        var second = VersionedRouteCatalogBuilder.Build(input with { Operations = input.Operations.Reverse().ToArray(), Bindings = input.Bindings.Reverse().ToArray() });
        RouteCatalogCodec.Encode(first).ShouldBe(RouteCatalogCodec.Encode(second));
        RouteCatalogCodec.Decode(RouteCatalogCodec.Encode(first)).SchemaVersion.ShouldBe(3);
        first.Routes.All(r => r.OperationContractDigest != r.DeclarationDigest).ShouldBeTrue();
    }

    [Theory]
    [InlineData("eligibility")]
    [InlineData("schema")]
    [InlineData("digest")]
    public void SuppliedOwnerTamperingIsRejectedBeforeGeneration(string change)
    {
        var input = Input(8);
        var operation = input.Operations[0];
        operation = change switch
        {
            "eligibility" => operation with { Eligibility = "UI-only" },
            "schema" => operation with { RequestSchema = JsonSerializer.SerializeToElement(new { type = "object", title = "changed" }) },
            _ => operation with { Digest = new string('0', 64) },
        };
        Should.Throw<RouteCatalogValidationException>(() => VersionedRouteCatalogBuilder.Build(input with { Operations = [operation, .. input.Operations.Skip(1)] }));
    }

    [Fact]
    public void RevisedAndRemovedPoliciesSurviveWithOriginalGenerationsDigestsAndKeys()
    {
        var first = VersionedRouteCatalogBuilder.Build(Input(8));
        var nextInput = Input(9) with { CommittedPredecessor = RouteCatalogCodec.Encode(first), CommittedPredecessorRootDigest = RouteCatalogCodec.ComputeRootDigest(first) };
        string removed = first.Declarations.First(d => d.Kind == "command").RouteId;
        string revised = first.Declarations.First(d => d.RouteId != removed).RouteId;
        nextInput = nextInput with
        {
            Declarations = nextInput.Declarations.Where(d => d.RouteId != removed).ToArray(),
            Bindings = nextInput.Bindings.Where(b => b.RouteId != removed).ToArray(),
            Idempotency = nextInput.Idempotency.Where(i => i.RouteId != removed).Select(i => i with { PolicyVersion = i.PolicyVersion + "-revised" })
                .Select(i => i with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(i) }).ToArray(),
            Operations = nextInput.Operations.Where(o => o.RouteId != removed).Select(o => o.RouteId == revised
                ? o with { Eligibility = "UI-only", RequestSchema = JsonSerializer.SerializeToElement(new { type = "object", title = "revised-contract" }) } : o)
                .Select(o => o with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(o) }).ToArray(),
        };
        var second = VersionedRouteCatalogBuilder.Build(nextInput);
        second.Declarations.ShouldNotContain(d => d.RouteId == removed);
        second.Routes.ShouldNotContain(r => r.RouteId == removed);
        second.Operations!.ShouldNotContain(o => o.RouteId == removed);
        second.Idempotency.ShouldNotContain(i => i.RouteId == removed);
        second.Operations!.Single(o => o.RouteId == revised).Digest.ShouldNotBe(first.Operations!.Single(o => o.RouteId == revised).Digest);
        var third = VersionedRouteCatalogBuilder.Build(nextInput with
        {
            Generation = 10, CommittedPredecessor = RouteCatalogCodec.Encode(second),
            CommittedPredecessorRootDigest = RouteCatalogCodec.ComputeRootDigest(second),
            Idempotency = second.Idempotency.Select(i => i with { Generation = 10 })
                .Select(i => i with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(i) }).ToArray(),
        });
        third.RetainedIdempotency!.Count.ShouldBe(first.Idempotency.Count + second.Idempotency.Count);
        foreach (var original in first.Idempotency.Concat(second.Idempotency))
        {
            var retained = third.RetainedIdempotency.Single(r => r.Entry.ContentDigest == original.ContentDigest).Entry;
            retained.Generation.ShouldBe(original.Generation);
            retained.KeyMap.ShouldBe(original.KeyMap);
        }
        third.Idempotency.All(i => i.Generation == 10).ShouldBeTrue();
    }

    [Fact]
    public void TamperedCommittedHistoryIsNeverRepaired()
    {
        var first = VersionedRouteCatalogBuilder.Build(Input(8));
        byte[] bytes = RouteCatalogCodec.Encode(first);
        string content = System.Text.Encoding.UTF8.GetString(bytes).Replace(first.Idempotency[0].ContentDigest, new string('0', 64), StringComparison.Ordinal);
        Should.Throw<RouteCatalogValidationException>(() => VersionedRouteCatalogBuilder.Build(Input(9) with
        { CommittedPredecessor = System.Text.Encoding.UTF8.GetBytes(content), CommittedPredecessorRootDigest = RouteCatalogCodec.ComputeRootDigest(first) }));
    }

    private static RouteCatalogGenerationInput Input(long generation)
    {
        var original = RouteCatalogCodec.Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Routing", "Fixtures", "V2CanonicalCatalog.json")));
        var policies = original.Idempotency.Select(i => i with { Generation = generation }).Select(i => i with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(i) }).ToArray();
        var operations = original.Declarations.Select(d => new RouteOperationContract(d.RouteId, d.ContractVersion,
            d.Kind == "projection" || d.Execution!.RequiresDelegatedContext ? "internal" : "public", "agent-eligible",
            JsonSerializer.SerializeToElement(new { type = "object" }), JsonSerializer.SerializeToElement(new { type = "object" }), string.Empty,
            d.Kind, d.Domain, d.MessageType ?? d.ProjectionType))
            .Select(o => o with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(o) }).ToArray();
        return new(generation, "Development", original.Declarations, operations, original.Routes.Select(r => new RouteCompositionBinding(
            r.RouteId, r.AppId, r.Method, r.ContractVersion, r.IsRuntimeOverride, r.ResolverTenantPattern)).ToArray(), policies);
    }
}
