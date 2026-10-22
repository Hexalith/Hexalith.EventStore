using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Candidate HKDF/core interoperability and zeroization; no registry/authority/physical backend qualification.</summary>
public sealed class InteractionOccurrenceKeyDerivationTests
{
    private const string Reference = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private static InteractionOccurrenceIdentity Identity() => new(new("tenant-a", "interaction-a", "alias-a"),
        new AggregateIdentity("tenant-a", "conversations", "directory-1"), PayloadProtectionPayloadKind.Event, 1, "Event.Type", "root-v1", "candidate-hkdf-sha256-v1");
    private static byte[] Root() => Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    /// <summary>Independent Python RFC5869 calculation pins the candidate framed domain separator; transferred root buffer is actually zeroed.</summary>
    [Fact]
    public void CandidateHkdfGoldenAndOwnedRootZeroization()
    {
        byte[] root = Root(); var material = InteractionOccurrenceKeyDeriver.Derive(root, Identity(), Reference, TestContext.Current.CancellationToken);
        Convert.ToHexString(material.DataEncryptionKey).ShouldBe("44B3049599FDCBB59988F1AE1F143866E8B903C4A62168EC78FDBD962E4B0472"); root.All(b => b == 0).ShouldBeTrue();
        CryptographicOperations.ZeroMemory(material.DataEncryptionKey);
    }
    /// <summary>Every target/owner/type/kind/sequence/version/reference domain changes the ephemeral key even with the same interaction root.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("interaction")]
    [InlineData("alias")]
    [InlineData("domain")]
    [InlineData("owner")]
    [InlineData("type")]
    [InlineData("sequence")]
    [InlineData("snapshot")]
    [InlineData("root-version")]
    [InlineData("reference")]
    public void OccurrenceSubstitutionNeverReusesDerivedKey(string vector)
    {
        var identity = Identity(); var first = InteractionOccurrenceKeyDeriver.Derive(Root(), identity, Reference, TestContext.Current.CancellationToken);
        var changed = vector switch {
            "tenant" => identity with { Target = identity.Target with { TenantId = "tenant-b" }, Owner = new("tenant-b", "conversations", "directory-1") },
            "interaction" => identity with { Target = identity.Target with { AgentInteractionId = "other" } },
            "alias" => identity with { Target = identity.Target with { TargetProtectionKeyAlias = "other" } },
            "domain" => identity with { Owner = new("tenant-a", "agents", "directory-1") },
            "owner" => identity with { Owner = new("tenant-a", "conversations", "directory-2") },
            "type" => identity with { PayloadTypeId = "Other.Type" }, "sequence" => identity with { Sequence = 2 },
            "snapshot" => identity with { Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = "hx-snapshot-v1:test" },
            "root-version" => identity with { RootKeyVersion = "root-v2" }, _ => identity
        };
        var second = InteractionOccurrenceKeyDeriver.Derive(Root(), changed, vector == "reference" ? "01ARZ3NDEKTSV4RRFFQ69G5FAW" : Reference, TestContext.Current.CancellationToken);
        first.DataEncryptionKey.ShouldNotBe(second.DataEncryptionKey); CryptographicOperations.ZeroMemory(first.DataEncryptionKey); CryptographicOperations.ZeroMemory(second.DataEncryptionKey);
    }
    /// <summary>Repeated field ordinal across two unique references uses different keys/ciphertext; actual existing v2 core decrypts only the exact alias/occurrence.</summary>
    [Fact]
    public async Task RepeatedOrdinalInteroperatesWithUnchangedActualV2Core()
    {
        byte[] payload = "{\"ProtectedContent\":\"synthetic-sensitive\",\"Safe\":1}"u8.ToArray(); var identity = Identity();
        var context = new PayloadProtectionContext(identity.Owner, identity.PayloadTypeId, identity.Kind, identity.Sequence); var core = new PayloadProtectionCore();
        var firstMaterial = InteractionOccurrenceKeyDeriver.Derive(Root(), identity, Reference, TestContext.Current.CancellationToken);
        var first = core.ProtectEvent(payload, new[] { "/ProtectedContent" }, context, () => firstMaterial, cancellationToken: TestContext.Current.CancellationToken);
        firstMaterial.DataEncryptionKey.All(b => b == 0).ShouldBeTrue();
        var secondMaterial = InteractionOccurrenceKeyDeriver.Derive(Root(), identity, "01ARZ3NDEKTSV4RRFFQ69G5FAW", TestContext.Current.CancellationToken);
        var second = core.ProtectEvent(payload, new[] { "/ProtectedContent" }, context, () => secondMaterial, cancellationToken: TestContext.Current.CancellationToken);
        first.PayloadBytes.ShouldNotBe(second.PayloadBytes); System.Text.Encoding.UTF8.GetString(first.PayloadBytes).ShouldNotContain("synthetic-sensitive");
        var read = await core.TryUnprotectEventAsync(first.PayloadBytes, context, (reference, _, token) => ValueTask.FromResult<byte[]?>(InteractionOccurrenceKeyDeriver.Derive(Root(), identity, reference, token).DataEncryptionKey), TestContext.Current.CancellationToken);
        read.IsReadable.ShouldBeTrue(); read.PayloadBytes.ShouldBe(payload);
        var wrong = await core.TryUnprotectEventAsync(first.PayloadBytes, context, (reference, _, token) => ValueTask.FromResult<byte[]?>(InteractionOccurrenceKeyDeriver.Derive(Root(), identity with { Target = identity.Target with { TargetProtectionKeyAlias = "other" } }, reference, token).DataEncryptionKey), TestContext.Current.CancellationToken);
        wrong.IsReadable.ShouldBeFalse(); wrong.PayloadBytes.ShouldBeNull();
    }
    /// <summary>Malformed scope/reference and caller cancellation clear the actual transferred root buffer on every failure.</summary>
    [Theory]
    [InlineData("reference")]
    [InlineData("tenant")]
    [InlineData("profile")]
    [InlineData("cancel")]
    public void FailedDerivationClearsActualRootBuffer(string vector)
    {
        var root = Root(); var identity = Identity(); using var cancellation = new CancellationTokenSource();
        if (vector == "tenant") { identity = identity with { Target = identity.Target with { TenantId = "tenant-b" } }; }
        if (vector == "profile") { identity = identity with { DerivationProfileVersion = "unaccepted" }; }
        if (vector == "cancel") { cancellation.Cancel(); }
        if (vector == "cancel") { var exception = Should.Throw<OperationCanceledException>(() => InteractionOccurrenceKeyDeriver.Derive(root, identity, Reference, cancellation.Token)); exception.CancellationToken.ShouldBe(cancellation.Token); }
        else { Should.Throw<PayloadProtectionFormatException>(() => InteractionOccurrenceKeyDeriver.Derive(root, identity, vector == "reference" ? "bad" : Reference)); }
        root.All(b => b == 0).ShouldBeTrue();
    }
}
