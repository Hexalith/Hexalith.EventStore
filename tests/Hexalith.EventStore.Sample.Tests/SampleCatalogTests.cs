using System.Text;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Projections;
using Shouldly;

namespace Hexalith.EventStore.Sample.Tests;

public class SampleCatalogTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("boolean")]
    public void TypedAdapterRequiresCanonicalStringTargetDescriptor(string change)
    {
        CommandIntentDeclaration valid = SampleCatalog.Commands.Single(command => command.Route.Execution!.HandlerType == typeof(IncrementCounter).FullName);
        var adapter = new DeclaredIdempotencyIntentAdapter<IncrementCounter>(valid, _ => new Dictionary<string, object?>());
        adapter.CreateIntent(new(adapter.CommandType, "tenant-a", "counter", "counter-1", "{}"u8.ToArray(), null))
            .CanonicalTarget.ShouldBe("tenant-a:counter:counter-1");
        IdempotencyDescriptorField[] fields = change == "missing" ? [new("value", "string")] : [new("target", "boolean")];
        CommandIntentDeclaration invalid = valid with { DescriptorSchema = valid.DescriptorSchema with { Fields = fields } };
        Should.Throw<ArgumentException>(() => new DeclaredIdempotencyIntentAdapter<IncrementCounter>(invalid, _ => new Dictionary<string, object?>()));
    }

    [Fact]
    public void AllFivePoliciesBindTargetAndIgnoreTransportAndIgnoredPayloadIdentifiers()
    {
        SampleCatalog.Commands.Count.ShouldBe(5);
        foreach (IDeclaredIdempotencyIntentAdapter adapter in SampleCatalog.CreateAdapters())
        {
            var command = new IdempotencyIntentCommand(adapter.CommandType, "tenant-a", adapter.Declaration.Route.Domain, "target-1",
                Encoding.UTF8.GetBytes("{}"), null);
            IdempotencyCanonicalIntent intent = adapter.CreateIntent(command);
            Encoding.UTF8.GetString(intent.SemanticPayload).ShouldBe("{}");
            intent.CanonicalTarget.ShouldBe($"tenant-a:{command.Domain}:target-1");
            adapter.RetentionTier.ShouldBe(IdempotencyReplayRetentionTier.Mutation);
            adapter.Declaration.Route.MessageIdVersion.ShouldBe(1);
            adapter.CreateIntent(command with { Payload = Encoding.UTF8.GetBytes("{\"counterId\":\"ignored-other-id\",\"text\":\"ignored\"}"), Extensions = new Dictionary<string, string> { ["subject"] = "spoof" } }).SemanticPayload.ShouldBe(intent.SemanticPayload);
            adapter.CreateIntent(command with { AggregateId = "target-2" }).CanonicalTarget.ShouldNotBe(intent.CanonicalTarget);
            adapter.CreateIntent(command with { CommandType = "Namespace." + adapter.Declaration.Route.Execution!.Aliases.Last() + ", Assembly" }).SemanticPayload.ShouldBe(intent.SemanticPayload);
            Should.Throw<InvalidOperationException>(() => adapter.CreateIntent(command with { Domain = "wrong-domain" }));
        }
    }

    [Fact]
    public void ActualProjectionMatchesOnlyDeclaredCounterOutput()
    {
        ProjectionResponse response = new CounterProjection().Project(new("tenant-a", "counter", "counter-1", []));
        response.ProjectionType.ShouldBe(SampleCatalog.Projection.ProjectionType);
        SampleCatalog.Projection.Execution!.ProjectionOutputs.ShouldBe([response.ProjectionType]);
        SampleCatalog.Routes.ShouldNotContain(route => route.Domain == "greeting" && route.Kind == "projection");
        SampleCatalog.Commands.Single(c => c.Route.Domain == "greeting").Route.MessageType.ShouldBe("SendGreeting");
    }
}
