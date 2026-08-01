using System.Security.Cryptography;
using System.Text;

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
    public void Resolve_AdapterMetadataChangesAfterRegistration_UsesValidatedSnapshot()
    {
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"demo\"}");
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());
        TrustedIdempotencyDescriptor expected = new IdempotencyIntentAdapterRegistry(
            [CreateAdapter("CreateFolderCommand", "{\"name\":\"demo\"}")],
            new CanonicalIdempotencyIntentEncoder()).Resolve(CreateCommand("CreateFolderCommand"));
        adapter.AdapterId.Returns("changed-adapter");
        adapter.OperationId.Returns("changed-operation");
        adapter.DescriptorVersion.Returns(2);
        adapter.RetentionTier.Returns(IdempotencyReplayRetentionTier.Commit);

        TrustedIdempotencyDescriptor actual = registry.Resolve(CreateCommand("CreateFolderCommand"));

        actual.AdapterId.ShouldBe("folders");
        actual.OperationId.ShouldBe("create-folder");
        actual.DescriptorVersion.ShouldBe(1);
        actual.RetentionTier.ShouldBe(IdempotencyReplayRetentionTier.Mutation);
        actual.CanonicalIntent.ShouldBe(expected.CanonicalIntent);
    }

    [Fact]
    public async Task Resolve_DifferentCanonicalTargets_ProducesDifferentIntentDigestsAsync()
    {
        TrustedIdempotencyDescriptor first = new IdempotencyIntentAdapterRegistry(
            [CreateAdapter("CreateFolderCommand", "{\"name\":\"demo\"}", "folders/folder-1")],
            new CanonicalIdempotencyIntentEncoder()).Resolve(CreateCommand("CreateFolderCommand"));
        TrustedIdempotencyDescriptor second = new IdempotencyIntentAdapterRegistry(
            [CreateAdapter("CreateFolderCommand", "{\"name\":\"demo\"}", "folders/folder-2")],
            new CanonicalIdempotencyIntentEncoder()).Resolve(CreateCommand("CreateFolderCommand"));
        using var keyProvider = new StaticIdempotencyDigestKeyProvider(
            "v1",
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["v1"] = Enumerable.Repeat((byte)0x2A, 32).ToArray(),
            },
            []);
        var protector = new IdempotencyKeyProtector(keyProvider);

        IdempotencyProtectedIdentitySet firstIdentity = await protector
            .ProtectAsync("tenant-a", "opaque-key", first)
            .ConfigureAwait(true);
        IdempotencyProtectedIdentitySet secondIdentity = await protector
            .ProtectAsync("tenant-a", "opaque-key", second)
            .ConfigureAwait(true);

        first.CanonicalIntent.ShouldNotBe(second.CanonicalIntent);
        firstIdentity.Active.KeyDigest.ShouldBe(secondIdentity.Active.KeyDigest);
        firstIdentity.Active.IntentDigest.ShouldNotBe(secondIdentity.Active.IntentDigest);
        CryptographicOperations.ZeroMemory(first.CanonicalIntent);
        CryptographicOperations.ZeroMemory(second.CanonicalIntent);
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

    [Fact]
    public void Resolve_CanonicalPayloadExceedsBoundAfterSerialization_FailsClosed()
    {
        string semanticJson = $"{{\"value\":\"{new string('é', 20_000)}\"}}";
        IIdempotencyIntentAdapter adapter = CreateAdapter("CreateFolderCommand", semanticJson);
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Resolve(CreateCommand("CreateFolderCommand")));

        exception.Message.ShouldBe(
            "Trusted canonical intent payload exceeds the supported size after canonicalization.");
    }

    [Fact]
    public void Resolve_AdapterControlledMetadataFieldExceedsBound_FailsClosed()
    {
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"demo\"}",
            new string('é', 2_049));
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Resolve(CreateCommand("CreateFolderCommand")));

        exception.Message.ShouldBe("Trusted canonical intent canonical target exceeds the supported size.");
    }

    [Fact]
    public void Resolve_SemanticOptionFieldExceedsBound_FailsClosed()
    {
        IReadOnlyDictionary<string, string> options = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = new string('é', 2_049),
        };
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"demo\"}",
            semanticOptions: options);
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Resolve(CreateCommand("CreateFolderCommand")));

        exception.Message.ShouldBe("Trusted canonical intent semantic option value exceeds the supported size.");
    }

    [Fact]
    public void Resolve_SemanticOptionsExceedTotalBound_FailsClosed()
    {
        IReadOnlyDictionary<string, string> options = Enumerable.Range(0, 16)
            .ToDictionary(
                static index => $"option-{index:D2}",
                static _ => new string('o', 4_090),
                StringComparer.Ordinal);
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            "{\"name\":\"demo\"}",
            semanticOptions: options);
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Resolve(CreateCommand("CreateFolderCommand")));

        exception.Message.ShouldBe("Trusted canonical intent semantic options exceed the supported size.");
    }

    [Fact]
    public void Resolve_CompleteCanonicalIntentExceedsBound_FailsClosed()
    {
        IReadOnlyDictionary<string, string> options = Enumerable.Range(0, 15)
            .ToDictionary(
                static index => $"option-{index:D2}",
                static _ => new string('o', 4_000),
                StringComparer.Ordinal);
        IIdempotencyIntentAdapter adapter = CreateAdapter(
            "CreateFolderCommand",
            $"{{\"value\":\"{new string('p', 59_000)}\"}}",
            new string('t', 3_000),
            options,
            new string('v', 3_000),
            new string('d', 3_000),
            new string('c', 3_000));
        var registry = new IdempotencyIntentAdapterRegistry(
            [adapter],
            new CanonicalIdempotencyIntentEncoder());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Resolve(CreateCommand("CreateFolderCommand")));

        exception.Message.ShouldBe("Trusted canonical intent exceeds the supported size.");
    }

    private static IIdempotencyIntentAdapter CreateAdapter(
        string commandType,
        string semanticJson,
        string canonicalTarget = "folders/folder-1",
        IReadOnlyDictionary<string, string>? semanticOptions = null,
        string policyVersion = "policy-v1",
        string delegatedTaskScope = "task-scope",
        string credentialScope = "credential-scope")
    {
        IIdempotencyIntentAdapter adapter = Substitute.For<IIdempotencyIntentAdapter>();
        adapter.CommandType.Returns(commandType);
        adapter.AdapterId.Returns("folders");
        adapter.OperationId.Returns("create-folder");
        adapter.DescriptorVersion.Returns(1);
        adapter.RetentionTier.Returns(IdempotencyReplayRetentionTier.Mutation);
        adapter.CreateIntent(Arg.Any<SubmitCommand>()).Returns(
            new IdempotencyCanonicalIntent(
                canonicalTarget,
                Encoding.UTF8.GetBytes(semanticJson),
                semanticOptions
                    ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["mode"] = "strict" },
                policyVersion,
                delegatedTaskScope,
                credentialScope));
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
