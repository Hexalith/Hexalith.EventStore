using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class IdempotencyDigestKeyRingTests
{
    [Fact]
    public async Task Protector_ActiveAndRetainedKeys_DeriveDistinctOrderedAliases()
    {
        var provider = new StaticIdempotencyDigestKeyProvider(
            "v2",
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                ["v2"] = Encoding.UTF8.GetBytes("abcdef0123456789abcdef0123456789"),
            },
            ["v1"]);
        var protector = new IdempotencyKeyProtector(provider);
        var descriptor = new TrustedIdempotencyDescriptor(
            "folders",
            "CreateFolder",
            1,
            Encoding.UTF8.GetBytes("canonical-intent"),
            IdempotencyReplayRetentionTier.Mutation);

        IdempotencyProtectedIdentitySet protectedSet = await protector.ProtectAsync(
            "tenant-a",
            "opaque-key",
            descriptor);

        protectedSet.Active.DigestKeyVersion.ShouldBe("v2");
        protectedSet.Aliases.Select(alias => alias.DigestKeyVersion).ShouldBe(["v2", "v1"]);
        protectedSet.Aliases.Select(alias => alias.ActorId).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
    }

    [Fact]
    public void Retire_ReferencedReaderVersion_IsRefusedUntilReferencesAreGone()
    {
        using var ring = new IdempotencyDigestKeyRing(
            "v2",
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["v1"] = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"),
                ["v2"] = Encoding.UTF8.GetBytes("abcdef0123456789abcdef0123456789"),
            },
            ["v1"]);
        var references = new[]
        {
            new IdempotencyDigestKeyReference("v1", IdempotencyDigestKeyReferenceKind.Tombstone, 1),
        };

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => ring.Retire("v1", references));

        exception.Message.ShouldBe("The idempotency digest key version is still referenced and cannot be retired.");
        using IdempotencyDigestKeyRing retired = ring.Retire("v1", []);
        retired.ReaderVersions.ShouldBeEmpty();
        retired.Versions.ShouldBe(["v2"]);
    }

    [Fact]
    public void Retire_ActiveWriterVersion_IsAlwaysRefused()
    {
        using var ring = new IdempotencyDigestKeyRing(
            "v2",
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["v2"] = Encoding.UTF8.GetBytes("abcdef0123456789abcdef0123456789"),
            },
            []);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => ring.Retire("v2", []));

        exception.Message.ShouldBe("The active idempotency digest key version cannot be retired.");
    }

    [Fact]
    public async Task DaprSecretProvider_ValidGeneration_ResolvesOnlyActiveAndReaders()
    {
        DaprClient client = Substitute.For<DaprClient>();
        IdempotencyAdmissionOptions options = SecretOptions();
        _ = client.GetSecretAsync(
                options.DigestKeySecretStoreName,
                options.DigestKeySecretName,
                null,
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["generation"] = options.DigestKeySecretGeneration,
                ["v1"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef")),
                ["v2"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("abcdef0123456789abcdef0123456789")),
                ["retired"] = "must-not-be-read",
            });
        var provider = new DaprSecretIdempotencyDigestKeyProvider(client, Options.Create(options));

        using IdempotencyDigestKeyRing ring = await provider.GetKeyRingAsync();

        ring.Versions.ShouldBe(["v2", "v1"]);
        _ = client.Received(1).GetSecretAsync(
            options.DigestKeySecretStoreName,
            options.DigestKeySecretName,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DaprSecretProvider_ProviderFailure_DoesNotDiscloseProviderMessage()
    {
        const string secretSentinel = "provider-secret-must-not-leak";
        DaprClient client = Substitute.For<DaprClient>();
        IdempotencyAdmissionOptions options = SecretOptions();
        _ = client.GetSecretAsync(
                options.DigestKeySecretStoreName,
                options.DigestKeySecretName,
                null,
                Arg.Any<CancellationToken>())
            .Returns<Task<Dictionary<string, string>>>(_ => throw new InvalidOperationException(secretSentinel));
        var provider = new DaprSecretIdempotencyDigestKeyProvider(client, Options.Create(options));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await provider.GetKeyRingAsync());

        exception.ToString().ShouldNotContain(secretSentinel);
        exception.Message.ShouldBe("The secret-backed idempotency digest key ring is unavailable.");
    }

    [Fact]
    public void OptionsValidator_SecretSource_RequiresMetadataAndForbidsInlineKeys()
    {
        IdempotencyAdmissionOptions options = SecretOptions() with
        {
            DigestKeys = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["v2"] = "inline-secret",
            },
        };

        ValidateOptionsResult result = new ValidateIdempotencyAdmissionOptions().Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OptionsValidator_SecretSource_RejectsReservedGenerationVersion(bool active)
    {
        IdempotencyAdmissionOptions options = SecretOptions() with
        {
            ActiveDigestKeyVersion = active ? "generation" : "v2",
            ReaderDigestKeyVersions = active ? ["v1"] : ["generation"],
        };

        ValidateOptionsResult result = new ValidateIdempotencyAdmissionOptions().Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("reserved");
    }

    private static IdempotencyAdmissionOptions SecretOptions()
        => new()
        {
            Enabled = true,
            ActiveDigestKeyVersion = "v2",
            ReaderDigestKeyVersions = ["v1"],
            DigestKeySource = IdempotencyDigestKeySource.DaprSecret,
            DigestKeySecretStoreName = "openbao",
            DigestKeySecretName = "eventstore-idempotency-digest-keys",
            DigestKeySecretGeneration = "generation-2",
        };
}
