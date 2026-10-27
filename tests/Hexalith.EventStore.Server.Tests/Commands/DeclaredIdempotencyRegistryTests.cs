using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class DeclaredIdempotencyRegistryTests
{
    [Fact]
    public void DeclaredPolicyVersionIsSnapshottedAndMustMatchAdapterOutput()
    {
        CommandIntentDeclaration declaration = SampleCatalog.Commands[0];
        IDeclaredIdempotencyIntentAdapter adapter = Custom(declaration);
        var registry = new IdempotencyIntentAdapterRegistry([adapter], new());
        byte[] canonical = registry.Resolve(Command(declaration)).CanonicalIntent;
        IdempotencyCanonicalIntent valid = adapter.CreateIntent(new(adapter.CommandType, "tenant-a", declaration.Route.Domain, "counter-1", "{}"u8.ToArray(), null));
        adapter.CreateIntent(Arg.Any<IdempotencyIntentCommand>()).Returns(valid with { PolicyVersion = "changed-policy" });
        Should.Throw<InvalidOperationException>(() => registry.Resolve(Command(declaration))).Message
            .ShouldBe("The trusted canonical intent disagrees with its declared policy version.");
        adapter.Declaration.Returns(declaration with { PolicyVersion = "changed-policy" });
        Should.Throw<InvalidOperationException>(() => registry.Resolve(Command(declaration)));
        adapter.CreateIntent(Arg.Any<IdempotencyIntentCommand>()).Returns(valid);
        registry.Resolve(Command(declaration)).CanonicalIntent.ShouldBe(canonical);
    }

    [Fact]
    public void LegacyAdapterPolicyVersionsRemainAdapterOwned()
    {
        IIdempotencyIntentAdapter legacy = Substitute.For<IIdempotencyIntentAdapter>();
        legacy.CommandType.Returns("legacy-command");
        legacy.AdapterId.Returns("legacy-adapter"); legacy.OperationId.Returns("legacy-operation");
        legacy.DescriptorVersion.Returns(1); legacy.RetentionTier.Returns(IdempotencyReplayRetentionTier.Mutation);
        var intent = new IdempotencyCanonicalIntent("tenant-a:counter:counter-1", "{}"u8.ToArray(), null, "policy-one", null, null);
        legacy.CreateIntent(Arg.Any<IdempotencyIntentCommand>()).Returns(intent);
        var registry = new IdempotencyIntentAdapterRegistry([legacy], new());
        var command = Command(SampleCatalog.Commands[0]) with { CommandType = "legacy-command" };
        byte[] first = registry.Resolve(command).CanonicalIntent;
        legacy.CreateIntent(Arg.Any<IdempotencyIntentCommand>()).Returns(intent with { PolicyVersion = "policy-two" });
        registry.Resolve(command).CanonicalIntent.ShouldNotBe(first);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("command")]
    [InlineData("adapter")]
    [InlineData("operation")]
    [InlineData("descriptor-version")]
    [InlineData("retention")]
    public void CustomRegistrationMustAgreeWithItsCommandDeclaration(string change)
    {
        CommandIntentDeclaration declaration = SampleCatalog.Commands[0];
        IDeclaredIdempotencyIntentAdapter adapter = Custom(declaration);
        _ = new IdempotencyIntentAdapterRegistry([adapter], new());
        CommandIntentDeclaration drift = change switch
        {
            "kind" => declaration with { Route = CounterCatalogDeclarations.Query },
            "command" => declaration with { Route = declaration.Route with { MessageType = "other-command", Execution = null } },
            "adapter" => declaration with { AdapterId = "other-adapter" },
            "operation" => declaration with { OperationId = "other-operation" },
            "descriptor-version" => declaration with { DescriptorSchema = declaration.DescriptorSchema with { Version = 2 } },
            _ => declaration with { RetentionTier = IdempotencyReplayRetentionTier.Commit },
        };
        adapter.Declaration.Returns(drift);
        Should.Throw<InvalidOperationException>(() => new IdempotencyIntentAdapterRegistry([adapter], new()));
    }

    [Fact]
    public void SyntheticScopedKeyCannotSelectAnAdapterAcrossDomains()
    {
        CommandIntentDeclaration declaration = SampleCatalog.Commands[0];
        IDeclaredIdempotencyIntentAdapter adapter = Custom(declaration);
        var registry = new IdempotencyIntentAdapterRegistry([adapter], new());
        var command = Command(declaration);
        registry.Resolve(command).AdapterId.ShouldBe(adapter.AdapterId);
        adapter.ClearReceivedCalls();
        Should.Throw<InvalidOperationException>(() => registry.Resolve(command with
        {
            Domain = "foreign", CommandType = declaration.Route.Domain + "|" + adapter.CommandType,
        }));
        adapter.DidNotReceive().CreateIntent(Arg.Any<IdempotencyIntentCommand>());
    }

    [Fact]
    public void V1ExactDiscriminatorsSupportDistinctEntriesAndRejectOverlaps()
    {
        CommandIntentDeclaration first = SampleCatalog.Commands[0] with { Route = SampleCatalog.Commands[0].Route with { Execution = null } };
        CommandIntentDeclaration second = SampleCatalog.Commands[1] with { Route = SampleCatalog.Commands[1].Route with { Execution = null } };
        var registry = new IdempotencyIntentAdapterRegistry([Custom(first), Custom(second)], new());
        registry.Resolve(Command(first)).AdapterId.ShouldBe(first.AdapterId);
        registry.Resolve(Command(second)).AdapterId.ShouldBe(second.AdapterId);
        Should.Throw<InvalidOperationException>(() => registry.Resolve(Command(first) with { CommandType = "Namespace." + first.Route.MessageType }));
        Should.Throw<InvalidOperationException>(() => new IdempotencyIntentAdapterRegistry([Custom(first), Custom(first)], new()));
        CommandIntentDeclaration aliased = SampleCatalog.Commands[1] with
        {
            Route = SampleCatalog.Commands[1].Route with { Execution = SampleCatalog.Commands[1].Route.Execution! with
            {
                Aliases = [.. SampleCatalog.Commands[1].Route.Execution!.Aliases, first.Route.MessageType!],
            } },
        };
        Should.Throw<InvalidOperationException>(() => new IdempotencyIntentAdapterRegistry([Custom(first), Custom(aliased)], new()));
        Should.Throw<InvalidOperationException>(() => new IdempotencyIntentAdapterRegistry([Custom(aliased), Custom(first)], new()));
    }

    private static SubmitCommand Command(CommandIntentDeclaration declaration) => new(
        "id-1", "tenant-a", declaration.Route.Domain, "counter-1", declaration.Route.MessageType!, "{}"u8.ToArray(), "corr-1", "actor-1");

    private static IDeclaredIdempotencyIntentAdapter Custom(CommandIntentDeclaration declaration)
    {
        IDeclaredIdempotencyIntentAdapter adapter = Substitute.For<IDeclaredIdempotencyIntentAdapter>();
        adapter.Declaration.Returns(declaration);
        adapter.CommandType.Returns(declaration.Route.MessageType!);
        adapter.AdapterId.Returns(declaration.AdapterId);
        adapter.OperationId.Returns(declaration.OperationId);
        adapter.DescriptorVersion.Returns(declaration.DescriptorSchema.Version);
        adapter.RetentionTier.Returns(declaration.RetentionTier);
        adapter.CreateIntent(Arg.Any<IdempotencyIntentCommand>()).Returns(new IdempotencyCanonicalIntent("tenant-a:counter:counter-1", "{}"u8.ToArray(), null, declaration.PolicyVersion, null, null));
        return adapter;
    }
}
