// Approved Dapr Kubernetes cryptography supplement: 3489775e15bdc5b7ffad4a67f37c6372a23d50b213095e26bd45e1514ad285bd; sections 5-6.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Closed WrappedDekV2 schema dispatch, HXKF 02 field hash, 2,048-byte bound and mixed-reader refusals; WrappedDekV1 bytes stay unchanged.</summary>
public sealed class WrappedDekV2CodecTests
{
    private const string Address = "pp-keys/kek-v1/key.pem";
    private const string KeyRef = "01J00000000000000000000811";
    private const string Creator = "01J00000000000000000000812";
    private const string Now = "2026-11-10T00:00:00.0000000Z";
    private static readonly string Scope = PayloadProtectionStateKeys.ScopeDigest(new("tenant-a", "parties", "party-01"));
    private static readonly PayloadProtectionAuthorityProfile Kubernetes = PayloadProtectionAuthorityProfile.DaprKubernetesCrypto;

    [Fact]
    public void V2EncodesSchemaVersionTwoFirstWithTheExactV1MemberOrderAndRoundTripsOnlyUnderItsProfile()
    {
        WrappedDekV1 key = V2(Envelope(673));
        key.ShouldBeOfType<WrappedDekV2>(); key.SchemaVersion.ShouldBe(2); key.ToString().ShouldBe(nameof(WrappedDekV2));
        byte[] bytes = PayloadProtectionStateCodec.Encode(key);
        Encoding.UTF8.GetString(bytes).ShouldStartWith("{\"schemaVersion\":2,\"tenantId\":\"tenant-a\",");
        Members(bytes).ShouldBe(Members(PayloadProtectionStateCodec.Encode(V1())));
        PayloadProtectionStateCodec.Decode<WrappedDekV1>(bytes, Kubernetes).ShouldBe(key);
        PayloadProtectionStateCodec.Decode<WrappedDekV2>(bytes, Kubernetes).ShouldBe(key);
        PayloadProtectionStateCodec.Encode(key, Kubernetes).ShouldBe(bytes);
        WrappedDekV1 transitioned = PayloadProtectionStateCodec.Seal(key with { State = "Active", UpdatedAtUtc = "2026-11-10T00:00:01.0000000Z" });
        transitioned.ShouldBeOfType<WrappedDekV2>();
        PayloadProtectionStateCodec.KeyFor(transitioned).ShouldBe(PayloadProtectionStateKeys.Key(Scope, KeyRef, 1));
        PayloadProtectionStateCodec.Decode<WrappedDekV1>(PayloadProtectionStateCodec.Encode(transitioned), Kubernetes).State.ShouldBe("Active");
    }

    [Fact]
    public void FieldHashUsesItsOwnHeaderWithTheV1FieldIdentifiersWhileV1BytesStayExactlyAsBefore()
    {
        byte[] envelope = Envelope(700);
        WrappedDekV1 v2 = V2(envelope);
        v2.WrappedDekSha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(envelope)));
        v2.CanonicalFieldsSha256.ShouldBe(IndependentFieldsHash(0x02, v2));
        WrappedDekV1 v1 = V1();
        v1.CanonicalFieldsSha256.ShouldBe(IndependentFieldsHash(0x01, v1));
        IndependentFieldsHash(0x01, v2).ShouldNotBe(v2.CanonicalFieldsSha256);
        string expected = "{\"schemaVersion\":1,\"tenantId\":\"tenant-a\",\"domain\":\"parties\",\"aggregateId\":\"party-01\",\"scopeDigest\":\"" + Scope
            + "\",\"keyRef\":\"" + KeyRef + "\",\"dekVersion\":1,\"state\":\"Reserved\",\"backendId\":\"lifecycle-test\",\"providerKeyVersionId\":\"fixture:version-01\","
            + "\"wrapAlgorithmId\":\"RSA-OAEP-256\",\"wrappedDek\":\"" + Base64UrlCodec.Encode(new byte[384]) + "\",\"lifecycleEpoch\":1,\"fenceApprovalEpoch\":7,"
            + "\"highestWrittenEpoch\":1,\"creatingOperationId\":\"" + Creator + "\",\"createdAtUtc\":\"" + Now + "\",\"updatedAtUtc\":\"" + Now + "\","
            + "\"canonicalFieldsSha256\":\"" + IndependentFieldsHash(0x01, v1) + "\",\"wrappedDekSha256\":\"" + Convert.ToHexStringLower(SHA256.HashData(new byte[384])) + "\"}";
        Encoding.UTF8.GetString(PayloadProtectionStateCodec.Encode(v1)).ShouldBe(expected);
        PayloadProtectionStateCodec.Decode<WrappedDekV1>(Encoding.UTF8.GetBytes(expected)).ShouldBeOfType<WrappedDekV1>();
    }

    [Fact]
    public void MixedReadersFailClosedAndNoRecordIsConvertedOrRestamped()
    {
        byte[] v2 = PayloadProtectionStateCodec.Encode(V2(Envelope(673)));
        byte[] v1 = PayloadProtectionStateCodec.Encode(V1());
        foreach (PayloadProtectionAuthorityProfile? predecessor in new[] { null, PayloadProtectionAuthorityProfile.Predecessor, PayloadProtectionAuthorityProfile.PlatformCustody })
        {
            Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV1>(v2, predecessor));
            Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV2>(v2, predecessor));
            Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(V2(Envelope(673)), predecessor ?? PayloadProtectionAuthorityProfile.Predecessor));
        }
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV1>(v1, Kubernetes));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV2>(v1, PayloadProtectionAuthorityProfile.Predecessor));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(V1(), Kubernetes));
        // Relabeling either schema's bytes cannot select the other schema.
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV1>(Relabel(v2, 2, 1), PayloadProtectionAuthorityProfile.Predecessor));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV1>(Relabel(v1, 1, 2), Kubernetes));
    }

    [Fact]
    public async Task StateBoundaryRefusesTheOtherSchemaWithTypedOutcomesBeforeAnyTransaction()
    {
        string key = PayloadProtectionStateKeys.Key(Scope, KeyRef, 1);
        foreach ((PayloadProtectionAuthorityProfile authority, WrappedDekV1 foreign) in new[] { (Kubernetes, V1()), (PayloadProtectionAuthorityProfile.Predecessor, V2(Envelope(673))) })
        {
            DaprClient client = Substitute.For<DaprClient>();
            var store = new DaprPayloadProtectionStateStore(client, authorityProfile: authority);
            store.ConfirmQualification();
            ReadOnlyMemory<byte> bytes = PayloadProtectionStateCodec.Encode(foreign);
            client.GetByteStateAndETagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
                .Returns((bytes, "etag"));
            (await Should.ThrowAsync<PayloadProtectionStateException>(async () => await store.ReadAsync<WrappedDekV1>(key))).Reason
                .ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
            await Should.ThrowAsync<PayloadProtectionFormatException>(() => store.CommitAsync([PayloadProtectionStateMutation.Put(foreign)]).AsTask());
            await client.DidNotReceiveWithAnyArgs().ExecuteStateTransactionAsync(default!, default!, default!, default);
        }
    }

    [Theory]
    [InlineData("envelope-2048", true)]
    [InlineData("envelope-2049", false)]
    [InlineData("envelope-unprefixed", false)]
    [InlineData("envelope-prefix-without-newline", false)]
    [InlineData("algorithm-rsa", false)]
    [InlineData("backend-other", false)]
    [InlineData("address-opaque", false)]
    [InlineData("address-uppercase-namespace", false)]
    public void V2ClosedValuesAndEnvelopeBoundAreExact(string variant, bool valid)
    {
        WrappedDekV1 key = variant switch
        {
            "envelope-2048" => V2(Envelope(2048)),
            "envelope-unprefixed" => Draft(Base64UrlCodec.Encode(new byte[673])),
            "envelope-prefix-without-newline" => Draft(Base64UrlCodec.Encode("dapr.io/enc/v1xxxxxxxx"u8)),
            "algorithm-rsa" => V2(Envelope(673)) with { WrapAlgorithmId = "RSA-OAEP-256" },
            "backend-other" => V2(Envelope(673)) with { BackendId = "platform-custody-rsa-software-v1" },
            "address-opaque" => V2(Envelope(673)) with { ProviderKeyVersionId = "fixture:version-01" },
            "address-uppercase-namespace" => V2(Envelope(673)) with { ProviderKeyVersionId = "PP-keys/kek-v1/key.pem" },
            _ => Draft(Base64UrlCodec.Encode(Envelope(2049))),
        };
        if (valid)
        {
            PayloadProtectionStateCodec.Decode<WrappedDekV1>(PayloadProtectionStateCodec.Encode(key), Kubernetes).ShouldBe(key);
            return;
        }

        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(PayloadProtectionStateCodec.Seal(key)));
    }

    [Fact]
    public void V1KeepsItsRsaAlgorithmAnd512ByteBound()
    {
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Seal(V1() with { WrappedDek = Base64UrlCodec.Encode(new byte[513]) }));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(PayloadProtectionStateCodec.Seal(V1() with { WrapAlgorithmId = WrappedDekV2.ClosedWrapAlgorithmId })));
        PayloadProtectionStateCodec.Encode(PayloadProtectionStateCodec.Seal(V1() with { WrappedDek = Base64UrlCodec.Encode(new byte[512]) })).ShouldNotBeEmpty();
    }

    [Fact]
    public void TamperedHashesOrEnvelopeBytesAreRejectedWhileLegalMemberReorderingIsAccepted()
    {
        WrappedDekV1 key = V2(Envelope(673));
        string json = Encoding.UTF8.GetString(PayloadProtectionStateCodec.Encode(key));
        string hash = key.CanonicalFieldsSha256;
        string tamperedHash = json.Replace(hash, (hash[0] == '0' ? "1" : "0") + hash[1..], StringComparison.Ordinal);
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV1>(Encoding.UTF8.GetBytes(tamperedHash), Kubernetes));
        byte[] flipped = Base64UrlCodec.Decode(key.WrappedDek); flipped[^1] ^= 1;
        string tamperedEnvelope = json.Replace(key.WrappedDek, Base64UrlCodec.Encode(flipped), StringComparison.Ordinal);
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV1>(Encoding.UTF8.GetBytes(tamperedEnvelope), Kubernetes));
        string reordered = "{" + json[(json.IndexOf(',', StringComparison.Ordinal) + 1)..^1] + ",\"schemaVersion\":2}";
        PayloadProtectionStateCodec.Decode<WrappedDekV1>(Encoding.UTF8.GetBytes(reordered), Kubernetes).ShouldBe(key);
    }

    [Fact]
    public void TheReaderCapabilityTokenExistsOnlyUnderTheDaprKubernetesAuthorityWhereItIsMandatory()
    {
        string root = PayloadProtectionStateKeys.TenantDomainDigest("tenant-a", "parties");
        CapabilityLeaseV1 Lease(PayloadProtectionAuthorityProfile authority, params string[] formats)
            => new(root, 1, Creator, new('b', 40), new('c', 64), authority.Digest, "fixture-backend", formats, ["V2"], Now, "2026-11-10T00:01:00.0000000Z");
        PayloadProtectionStateCodec.Encode(Lease(Kubernetes, "json", "json+pdenc-v2", "wrapped-dek-v2"), Kubernetes).ShouldNotBeEmpty();
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(Lease(Kubernetes, "json", "json+pdenc-v2"), Kubernetes));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(Lease(Kubernetes, "json", "wrapped-dek-v2"), Kubernetes));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(Lease(Kubernetes, "json", "json+pdenc-v2", "wrapped-dek-v3"), Kubernetes));
        PayloadProtectionAuthorityProfile predecessor = PayloadProtectionAuthorityProfile.Predecessor;
        PayloadProtectionStateCodec.Encode(Lease(predecessor, "json", "json+pdenc-v2"), predecessor).ShouldNotBeEmpty();
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(Lease(predecessor, "json", "json+pdenc-v2", "wrapped-dek-v2"), predecessor));
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var store = new MemoryPayloadProtectionStateStore();
        Should.Throw<PayloadProtectionStateException>(() => new PayloadProtectionLeaseCoordinator(store, roots,
            new(new('b', 40), new('c', 64), "fixture-backend", ["json", "json+pdenc-v2"], ["V2"]), authorityProfile: Kubernetes));
        Should.Throw<PayloadProtectionStateException>(() => new PayloadProtectionLeaseCoordinator(store, roots,
            new(new('b', 40), new('c', 64), "fixture-backend", ["json", "json+pdenc-v2", "wrapped-dek-v2"], ["V2"])));
        new PayloadProtectionLeaseCoordinator(store, roots, new(new('b', 40), new('c', 64), "fixture-backend", ["json", "json+pdenc-v2", "wrapped-dek-v2"], ["V2"]),
            authorityProfile: Kubernetes).AuthorityProfile.ShouldBeSameAs(Kubernetes);
    }

    private static WrappedDekV1 V1() => PayloadProtectionStateCodec.Seal(PayloadProtectionStateCodec.CreateKey(1, "tenant-a", "parties", "party-01", Scope, KeyRef, 1,
        "Reserved", "lifecycle-test", "fixture:version-01", "RSA-OAEP-256", Base64UrlCodec.Encode(new byte[384]), 1, 7, 1, Creator, Now, Now));
    private static WrappedDekV1 V2(byte[] envelope) => PayloadProtectionStateCodec.Seal(Draft(Base64UrlCodec.Encode(envelope)));
    private static WrappedDekV1 Draft(string wrapped) => PayloadProtectionStateCodec.CreateKey(2, "tenant-a", "parties", "party-01", Scope, KeyRef, 1,
        "Reserved", WrappedDekV2.ClosedBackendId, Address, WrappedDekV2.ClosedWrapAlgorithmId, wrapped, 1, 7, 1, Creator, Now, Now);
    private static byte[] Envelope(int length)
    {
        byte[] envelope = RandomNumberGenerator.GetBytes(length);
        WrappedDekV2.SchemePrefix.CopyTo(envelope);
        return envelope;
    }

    private static string[] Members(byte[] json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
    }

    private static byte[] Relabel(byte[] json, int from, int to) => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(json)
        .Replace($"{{\"schemaVersion\":{from},", $"{{\"schemaVersion\":{to},", StringComparison.Ordinal));

    // Independent reimplementation of lifecycle amendment section 5 framing with a caller-selected schema header byte.
    private static string IndependentFieldsHash(byte schema, WrappedDekV1 key)
    {
        var preimage = new List<byte> { 0x48, 0x58, 0x4B, 0x46, schema, 0x11, 0x00, 0x00 };
        void Field(byte id, byte type, byte[] value)
        {
            preimage.Add(id); preimage.Add(type);
            byte[] length = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length, (uint)value.Length);
            preimage.AddRange(length); preimage.AddRange(value);
        }
        byte[] U32(uint value) { byte[] b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, value); return b; }
        byte[] U64(ulong value) { byte[] b = new byte[8]; BinaryPrimitives.WriteUInt64BigEndian(b, value); return b; }
        byte[] Text(string value) => Encoding.UTF8.GetBytes(value);
        Field(1, 1, Text(key.TenantId)); Field(2, 1, Text(key.Domain)); Field(3, 1, Text(key.AggregateId));
        Field(4, 4, Convert.FromHexString(key.ScopeDigest)); Field(5, 1, Text(key.KeyRef)); Field(6, 2, U32(key.DekVersion));
        Field(7, 1, Text(key.State)); Field(8, 1, Text(key.BackendId)); Field(9, 1, Text(key.ProviderKeyVersionId)); Field(10, 1, Text(key.WrapAlgorithmId));
        Field(12, 3, U64(key.LifecycleEpoch)); Field(13, 3, U64(key.FenceApprovalEpoch)); Field(14, 3, U64(key.HighestWrittenEpoch));
        Field(15, 1, Text(key.CreatingOperationId)); Field(16, 1, Text(key.CreatedAtUtc)); Field(17, 1, Text(key.UpdatedAtUtc));
        Field(19, 4, Convert.FromHexString(key.WrappedDekSha256));
        return Convert.ToHexStringLower(SHA256.HashData([.. preimage]));
    }
}
