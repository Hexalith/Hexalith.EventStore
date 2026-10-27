using System.Text;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Client.Routing;
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
    [InlineData(false)]
    [InlineData(true)]
    public void SubjectFlagMustExactlyAgreeWithTheHashedDescriptor(bool requiresSubject)
    {
        CommandIntentDeclaration template = SampleCatalog.Commands.Single(command => command.Route.Execution!.HandlerType == typeof(IncrementCounter).FullName);
        CommandIntentDeclaration valid = template with
        {
            RequiresAuthenticatedSubject = requiresSubject,
            DescriptorSchema = template.DescriptorSchema with
            {
                Fields = requiresSubject ? [new("target", "string"), new("acting-subject", "string")] : [new("target", "string")],
            },
        };
        var adapter = new DeclaredIdempotencyIntentAdapter<IncrementCounter>(valid, _ => new Dictionary<string, object?>());
        var command = new IdempotencyIntentCommand(adapter.CommandType, "tenant-a", "counter", "counter-1", "{}"u8.ToArray(), null) { AuthenticatedSubject = "trusted-actor" };
        Encoding.UTF8.GetString(adapter.CreateIntent(command).SemanticPayload).ShouldBe(requiresSubject ? "{\"acting-subject\":\"trusted-actor\"}" : "{}");
        string digest = RouteCatalogCodec.ComputeDescriptorDigest(valid.DescriptorSchema);
        DevelopmentIdempotencyBindings.Bind(valid, 1).DescriptorDigest.ShouldBe(digest);
        CommandIntentDeclaration toggled = valid with { RequiresAuthenticatedSubject = !requiresSubject };
        RouteCatalogCodec.ComputeDescriptorDigest(toggled.DescriptorSchema).ShouldBe(digest);
        Should.Throw<ArgumentException>(() => new DeclaredIdempotencyIntentAdapter<IncrementCounter>(toggled, _ => new Dictionary<string, object?>()));
        Should.Throw<ArgumentException>(() => DevelopmentIdempotencyBindings.Bind(toggled, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReservedSubjectDescriptorMustBeAString(bool requiresSubject)
    {
        CommandIntentDeclaration template = SampleCatalog.Commands.Single(command => command.Route.Execution!.HandlerType == typeof(IncrementCounter).FullName);
        CommandIntentDeclaration invalid = template with
        {
            RequiresAuthenticatedSubject = requiresSubject,
            DescriptorSchema = template.DescriptorSchema with { Fields = [new("target", "string"), new("acting-subject", "boolean")] },
        };
        Should.Throw<ArgumentException>(() => new DeclaredIdempotencyIntentAdapter<IncrementCounter>(invalid, _ => new Dictionary<string, object?> { ["acting-subject"] = true }));
        Should.Throw<ArgumentException>(() => DevelopmentIdempotencyBindings.Bind(invalid, 1));
    }

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

    [Theory]
    [InlineData("missing")]
    [InlineData("boolean")]
    public void AuthenticatedSubjectRequiresItsStringDescriptorAtConstruction(string change)
    {
        CommandIntentDeclaration template = SampleCatalog.Commands.Single(command => command.Route.Execution!.HandlerType == typeof(IncrementCounter).FullName);
        CommandIntentDeclaration valid = template with
        {
            RequiresAuthenticatedSubject = true,
            DescriptorSchema = template.DescriptorSchema with { Fields = [new("target", "string"), new("acting-subject", "string")] },
        };
        var adapter = new DeclaredIdempotencyIntentAdapter<IncrementCounter>(valid, _ => new Dictionary<string, object?>());
        var command = new IdempotencyIntentCommand(adapter.CommandType, "tenant-a", "counter", "counter-1", "{}"u8.ToArray(), null) { AuthenticatedSubject = "trusted-actor" };
        Encoding.UTF8.GetString(adapter.CreateIntent(command).SemanticPayload).ShouldBe("{\"acting-subject\":\"trusted-actor\"}");
        CommandIntentDeclaration invalid = valid with
        {
            DescriptorSchema = valid.DescriptorSchema with
            {
                Fields = change == "missing" ? [new("target", "string")] : [new("target", "string"), new("acting-subject", "boolean")],
            },
        };
        Should.Throw<ArgumentException>(() => new DeclaredIdempotencyIntentAdapter<IncrementCounter>(invalid, _ => new Dictionary<string, object?>()));
    }

    [Fact]
    public void TypedAdapterSnapshotsAllNestedOwnerCollections()
    {
        CommandIntentDeclaration template = SampleCatalog.Commands.Single(command => command.Route.Execution!.HandlerType == typeof(IncrementCounter).FullName);
        var aliases = template.Route.Execution!.Aliases.ToList();
        var fallbacks = new List<string>();
        var outputs = new List<string>();
        var fields = template.DescriptorSchema.Fields.ToList();
        CommandIntentDeclaration mutable = template with
        {
            Route = template.Route with { FallbackTypes = fallbacks, Execution = template.Route.Execution with { Aliases = aliases, ProjectionOutputs = outputs } },
            DescriptorSchema = template.DescriptorSchema with { Fields = fields },
        };
        var adapter = new DeclaredIdempotencyIntentAdapter<IncrementCounter>(mutable, _ => new Dictionary<string, object?>());
        string routeDigest = RouteCatalogCodec.ComputeDeclarationDigest(adapter.Declaration.Route);
        string schemaDigest = RouteCatalogCodec.ComputeDescriptorDigest(adapter.Declaration.DescriptorSchema);
        aliases.Clear(); aliases.Add("Bogus"); fallbacks.Add("Bogus"); outputs.Add("invented-output"); fields.Clear(); fields.Add(new("invented", "boolean"));
        RouteCatalogCodec.ComputeDeclarationDigest(adapter.Declaration.Route).ShouldBe(routeDigest);
        RouteCatalogCodec.ComputeDescriptorDigest(adapter.Declaration.DescriptorSchema).ShouldBe(schemaDigest);
        var command = new IdempotencyIntentCommand("IncrementCounter", "tenant-a", "counter", "counter-1", "{}"u8.ToArray(), null);
        Encoding.UTF8.GetString(adapter.CreateIntent(command).SemanticPayload).ShouldBe("{}");
        Should.Throw<InvalidOperationException>(() => adapter.CreateIntent(command with { CommandType = "Bogus" }));
        Should.Throw<NotSupportedException>(() => ((IList<string>)adapter.Declaration.Route.Execution!.Aliases).Add("Bogus"));
        Should.Throw<NotSupportedException>(() => ((IList<IdempotencyDescriptorField>)adapter.Declaration.DescriptorSchema.Fields).Clear());
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
