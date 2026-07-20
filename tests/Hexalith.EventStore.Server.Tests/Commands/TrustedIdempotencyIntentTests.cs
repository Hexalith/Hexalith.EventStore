using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class TrustedIdempotencyIntentTests
{
    [Fact]
    public void Resolve_UnknownCommandType_FailsClosed()
    {
        var registry = new IdempotencyIntentAdapterRegistry(
            [],
            new CanonicalIdempotencyIntentEncoder());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Resolve(CreateCommand("UnknownCommand")));

        exception.Message.ShouldBe("No trusted idempotency adapter is registered for the command type.");
    }

    [Fact]
    public void Resolve_EquivalentPayloadsWithDifferentPropertyOrder_ProducesIdenticalCanonicalBytes()
    {
        IIdempotencyIntentAdapter firstAdapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"demo\",\"options\":{\"b\":2,\"a\":1}}");
        IIdempotencyIntentAdapter secondAdapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"options\":{\"a\":1,\"b\":2},\"name\":\"demo\"}");

        TrustedIdempotencyDescriptor first = new IdempotencyIntentAdapterRegistry(
            [firstAdapter],
            new CanonicalIdempotencyIntentEncoder()).Resolve(CreateCommand("CreateFolderCommand"));
        TrustedIdempotencyDescriptor second = new IdempotencyIntentAdapterRegistry(
            [secondAdapter],
            new CanonicalIdempotencyIntentEncoder()).Resolve(CreateCommand("CreateFolderCommand"));

        first.CanonicalIntent.ShouldBe(second.CanonicalIntent);
        first.AdapterId.ShouldBe("folders");
        first.OperationId.ShouldBe("create-folder");
        first.DescriptorVersion.ShouldBe(1);
        first.RetentionTier.ShouldBe(IdempotencyReplayRetentionTier.Mutation);
    }

    [Fact]
    public void Resolve_DuplicateSemanticJsonProperty_FailsBeforeAdmission()
    {
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"first\",\"name\":\"second\"}");
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Resolve(CreateCommand("CreateFolderCommand")));

        exception.Message.ShouldBe("Trusted canonical intent contains a duplicate JSON property.");
    }

    [Fact]
    public void Resolve_TransportOnlyChanges_DoNotChangeCanonicalIntent()
    {
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"demo\"}");
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());

        TrustedIdempotencyDescriptor first = registry.Resolve(CreateCommand(
            "CreateFolderCommand",
            messageId: "01J00000000000000000000000",
            correlationId: "trace-first"));
        TrustedIdempotencyDescriptor retry = registry.Resolve(CreateCommand(
            "CreateFolderCommand",
            messageId: "01J11111111111111111111111",
            correlationId: "trace-retry"));

        retry.CanonicalIntent.ShouldBe(first.CanonicalIntent);
    }

    [Fact]
    public void Resolve_InvalidRegisteredDescriptorVersion_FailsClosed()
    {
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"demo\"}");
        adapter.DescriptorVersion.Returns(0);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => new IdempotencyIntentAdapterRegistry(
                [adapter],
                new CanonicalIdempotencyIntentEncoder()));

        exception.Message.ShouldBe("A trusted idempotency adapter registration is invalid.");
    }

    [Fact]
    public void Resolve_MissingPolicyVersion_FailsClosed()
    {
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"demo\"}");
        adapter.CreateIntent(Arg.Any<SubmitCommand>()).Returns(
            new IdempotencyCanonicalIntent(
                "folders/folder-1",
                "{}"u8.ToArray(),
                null,
                string.Empty,
                null,
                null));
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Resolve(CreateCommand("CreateFolderCommand")));

        exception.Message.ShouldBe("The trusted canonical intent is incomplete.");
    }

    private static IIdempotencyIntentAdapter CreateAdapter(string commandType, string semanticJson)
    {
        IIdempotencyIntentAdapter adapter = Substitute.For<IIdempotencyIntentAdapter>();
        adapter.CommandType.Returns(commandType);
        adapter.AdapterId.Returns("folders");
        adapter.OperationId.Returns("create-folder");
        adapter.DescriptorVersion.Returns(1);
        adapter.RetentionTier.Returns(IdempotencyReplayRetentionTier.Mutation);
        adapter.CreateIntent(Arg.Any<SubmitCommand>()).Returns(
            new IdempotencyCanonicalIntent(
                "folders/folder-1",
                System.Text.Encoding.UTF8.GetBytes(semanticJson),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["mode"] = "strict" },
                "policy-v1",
                "task-scope",
                "credential-scope"));
        return adapter;
    }

    private static SubmitCommand CreateCommand(
        string commandType,
        string messageId = "01J00000000000000000000000",
        string correlationId = "01J00000000000000000000000")
        => new(
            messageId,
            "tenant-a",
            "folders",
            "folder-1",
            commandType,
            "{}"u8.ToArray(),
            correlationId,
            "user-1",
            IdempotencyKey: "opaque-key");
}
