using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Covers JSON traversal, selection, and atomic transformation vectors V027 and V034-V040.
/// </summary>
public sealed class JsonTransformTests
{
    /// <summary>Verifies the writer rejects every pre-existing reserved wrapper member before encryption.</summary>
    [Theory]
    [InlineData("{\"$pdenc\":\"value\",\"email\":\"a\"}")]
    [InlineData("{\"nested\":{\"$pdenc\":\"value\"},\"email\":\"a\"}")]
    [InlineData("{\"\\u0024pdenc\":\"value\",\"email\":\"a\"}")]
    public void ReservedWrapperMember_IsRejectedBeforeProtection(string json)
    {
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes(json),
            ["/email"],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));
        materialCalls.ShouldBe(0);
    }

    /// <summary>Verifies snapshot protection rejects reserved markers without requesting material.</summary>
    [Fact]
    public void SnapshotReservedWrapperMember_IsRejectedBeforeMaterialCreation()
    {
        int materialCalls = 0;

        Should.Throw<PayloadProtectionFormatException>(() => TestFixture.ProtectSnapshot(
            "{\"nested\":{\"$pdenc\":\"value\"}}"u8.ToArray(),
            materialFactory: () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));

        materialCalls.ShouldBe(0);
    }

    /// <summary>Verifies only a literal unescaped wrapper name is accepted by the event reader.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObfuscatedWrapperName_IsRejectedBeforeLookupAsync(bool besideValidWrapper)
    {
        byte[] payload = besideValidWrapper
            ? Encoding.UTF8.GetBytes(
                "{\"email\":{\"$pdenc\":\"" + TestFixture.EnvelopeBase64Url
                + "\"},\"\\u0024pdenc\":\"decoy\"}")
            : Encoding.UTF8.GetBytes(
                "{\"email\":{\"\\u0024pdenc\":\"" + TestFixture.EnvelopeBase64Url + "\"},\"name\":\"Alice\"}");
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            payload,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }

    /// <summary>Verifies every forbidden carrier shape is rejected before key resolution.</summary>
    [Theory]
    [InlineData("extra-member")]
    [InlineData("non-string")]
    [InlineData("escaped-value")]
    public async Task ForbiddenCarrierShape_IsRejectedBeforeLookupAsync(string shape)
    {
        string carrier = TestFixture.EnvelopeBase64Url;
        string wrapper = shape switch
        {
            "extra-member" => "{\"$pdenc\":\"" + carrier + "\",\"extra\":0}",
            "non-string" => "{\"$pdenc\":0}",
            _ => "{\"$pdenc\":\"\\u0053" + carrier[1..] + "\"}",
        };
        byte[] payload = Encoding.UTF8.GetBytes("{\"email\":" + wrapper + ",\"name\":\"Alice\"}");
        int resolverCalls = 0;

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            payload,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(0);
    }

    /// <summary>V027 detects removed, plaintext-substituted, duplicated, and added wrappers before plaintext escapes.</summary>
    [Fact]
    [Trait("Vector", "V027")]
    public async Task V027_EveryWrapperSetChange_FailsAtomicallyAsync()
    {
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":\"alice@example.com\",\"name\":\"Alice\"}"),
            ["/email", "/name"],
            TestFixture.Context(),
            TestFixture.Material);
        JsonObject baseline = JsonNode.Parse(protectedResult.PayloadBytes)!.AsObject();

        JsonObject removed = baseline.DeepClone().AsObject();
        removed.Remove("name").ShouldBeTrue();
        JsonObject substituted = baseline.DeepClone().AsObject();
        substituted["name"] = "Alice";
        string duplicateJson = "{\"email\":" + baseline["email"]!.ToJsonString()
            + ",\"email\":" + baseline["email"]!.ToJsonString() + "}";
        JsonObject duplicatedAtNewPath = baseline.DeepClone().AsObject();
        duplicatedAtNewPath["copy"] = baseline["email"]!.DeepClone();

        ProtectedPathManifest expandedManifest = ProtectedPathManifestCodec.Create(["/email", "/name", "/z"]);
        byte[] addedAad = TestFixture.Aad(path: "/z", ordinal: 2, commitment: expandedManifest.Commitment);
        PayloadProtectionEnvelope addedEnvelope = PayloadCryptography.Encrypt(
            "0"u8,
            addedAad,
            TestFixture.Dek(),
            TestFixture.KeyReference,
            1,
            2);
        JsonObject added = baseline.DeepClone().AsObject();
        added["z"] = new JsonObject { ["$pdenc"] = Base64UrlCodec.Encode(EnvelopeCodec.Write(addedEnvelope)) };

        await AssertWrapperSetMismatchAsync("removed", Encoding.UTF8.GetBytes(removed.ToJsonString()), expectedResolverCalls: 1);
        await AssertWrapperSetMismatchAsync("plaintext-substituted", Encoding.UTF8.GetBytes(substituted.ToJsonString()), expectedResolverCalls: 1);
        await AssertWrapperSetMismatchAsync("duplicate", Encoding.UTF8.GetBytes(duplicateJson), expectedResolverCalls: 0);
        await AssertWrapperSetMismatchAsync(
            "duplicate-at-new-path",
            Encoding.UTF8.GetBytes(duplicatedAtNewPath.ToJsonString()),
            expectedResolverCalls: 0);
        await AssertWrapperSetMismatchAsync("added", Encoding.UTF8.GetBytes(added.ToJsonString()), expectedResolverCalls: 1);
    }

    /// <summary>V034 rejects ordinal-equal duplicate JSON member names.</summary>
    [Fact]
    [Trait("Vector", "V034")]
    public void V034_DuplicateJsonMember_IsRejectedBeforeEncryption()
    {
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":1,\"email\":2}"),
            ["/email"],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));
        materialCalls.ShouldBe(0);
    }

    /// <summary>V035 rejects unresolved, scalar-traversal, and out-of-range paths.</summary>
    [Theory]
    [InlineData("/missing")]
    [InlineData("/email/child")]
    [InlineData("/items/2")]
    [Trait("Vector", "V035")]
    public void V035_PathResolutionFailures_AreBounded(string path)
    {
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":\"a\",\"items\":[0]}"),
            [path],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));
        materialCalls.ShouldBe(0);
    }

    /// <summary>V036 rejects selected ancestor/descendant overlap independent of input order.</summary>
    [Theory]
    [InlineData("/person", "/person/email")]
    [InlineData("/person/email", "/person")]
    [Trait("Vector", "V036")]
    public void V036_AncestorDescendantSelection_IsRejected(string first, string second)
    {
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"person\":{\"email\":\"a\"}}"),
            [first, second],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));
        materialCalls.ShouldBe(0);
    }

    /// <summary>V036 catches an ancestor even when an ordinary sibling sorts between it and its descendant.</summary>
    [Fact]
    [Trait("Vector", "V036")]
    public void V036_InterposedSiblingCannotHideAncestorOverlap()
    {
        int materialCalls = 0;

        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            "{\"a\":{\"b\":1},\"a-foo\":2}"u8.ToArray(),
            ["/a", "/a-foo", "/a/b"],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));

        materialCalls.ShouldBe(0);
    }

    /// <summary>V031 round-trips selected names containing RFC 6901 tilde and slash escapes.</summary>
    [Theory]
    [InlineData("a~b", "/a~0b")]
    [InlineData("a/b", "/a~1b")]
    [Trait("Vector", "V031")]
    public async Task V031_EscapedMemberName_RoundTripsThroughCompleteCoreAsync(string memberName, string path)
    {
        byte[] original = Encoding.UTF8.GetBytes("{\"" + memberName + "\":{\"value\":1}}");
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            original,
            [path],
            TestFixture.Context(),
            TestFixture.Material);

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(protectedResult.PayloadBytes);

        result.IsReadable.ShouldBeTrue();
        result.PayloadBytes.ShouldBe(original);
    }

    /// <summary>Verifies decoded-equivalent duplicate member spellings fail locally on writer and reader paths.</summary>
    [Theory]
    [InlineData("protect")]
    [InlineData("read")]
    public async Task DecodedEquivalentDuplicateNames_AreRejectedBeforeExternalWorkAsync(string operation)
    {
        byte[] payload = operation == "protect"
            ? "{\"email\":1,\"\\u0065mail\":2}"u8.ToArray()
            : Encoding.UTF8.GetBytes(
                "{\"email\":{\"$pdenc\":\"" + TestFixture.EnvelopeBase64Url
                + "\"},\"\\u0065mail\":{\"$pdenc\":\"" + TestFixture.EnvelopeBase64Url + "\"}}");
        int externalCalls = 0;

        if (operation == "protect")
        {
            Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
                payload,
                ["/email"],
                TestFixture.Context(),
                () =>
                {
                    externalCalls++;
                    return TestFixture.Material();
                }));
        }
        else
        {
            CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
                payload,
                keyResolver: (_, _, _) =>
                {
                    externalCalls++;
                    return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
                });
            result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }

        externalCalls.ShouldBe(0);
    }

    /// <summary>V037 rejects a pointer selected more than once.</summary>
    [Fact]
    [Trait("Vector", "V037")]
    public void V037_RepeatedPointerSelection_IsRejected()
    {
        int materialCalls = 0;
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":\"a\"}"),
            ["/email", "/email"],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));
        materialCalls.ShouldBe(0);
    }

    /// <summary>Verifies authenticated event and snapshot plaintext cannot introduce reserved markers.</summary>
    [Theory]
    [InlineData("event", false)]
    [InlineData("event", true)]
    [InlineData("snapshot", false)]
    [InlineData("snapshot", true)]
    public async Task Readers_RejectAuthenticatedReservedMarkersAsync(string payloadKind, bool escaped)
    {
        byte[] plaintext = Encoding.UTF8.GetBytes(
            escaped ? "{\"\\u0024pdenc\":\"nested\"}" : "{\"$pdenc\":\"nested\"}");
        int resolverCalls = 0;
        CoreUnprotectionResult result;
        if (payloadKind == "event")
        {
            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(["/email"]);
            byte[] aad = TestFixture.Aad(commitment: manifest.Commitment);
            PayloadProtectionEnvelope envelope = PayloadCryptography.Encrypt(
                plaintext,
                aad,
                TestFixture.Dek(),
                TestFixture.KeyReference,
                1,
                0);
            result = await TestFixture.UnprotectAsync(
                TestFixture.WrapperPayloadBytes(Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope))),
                keyResolver: (_, _, _) =>
                {
                    resolverCalls++;
                    return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
                });
        }
        else
        {
            PayloadProtectionContext context = TestFixture.SnapshotContext();
            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create([string.Empty], snapshot: true);
            byte[] aad = AadCodec.Write(context, string.Empty, TestFixture.KeyReference, 1, 0, manifest.Commitment);
            PayloadProtectionEnvelope envelope = PayloadCryptography.Encrypt(
                plaintext,
                aad,
                TestFixture.Dek(),
                TestFixture.KeyReference,
                1,
                0);
            var protectedSnapshot = new ProtectedSnapshotPayloadV2(
                "json+pdenc-v2",
                context.PayloadTypeId,
                Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope)));
            result = await TestFixture.UnprotectSnapshotAsync(
                protectedSnapshot,
                keyResolver: (_, _, _) =>
                {
                    resolverCalls++;
                    return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
                });
        }

        result.PayloadBytes.ShouldBeNull();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        resolverCalls.ShouldBe(1);
    }

    /// <summary>V038 rejects a serialized cycle-equivalent graph within the fixed depth bound before material creation.</summary>
    [Fact]
    [Trait("Vector", "V038")]
    public void V038_CycleEquivalentOverDepthJson_IsRejected()
    {
        string json = new string('[', 65) + "0" + new string(']', 65);
        byte[] payload = Encoding.UTF8.GetBytes(json);
        byte[] original = [.. payload];
        int materialCalls = 0;

        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            payload,
            ["/0"],
            TestFixture.Context(),
            () =>
            {
                materialCalls++;
                return TestFixture.Material();
            }));

        materialCalls.ShouldBe(0);
        payload.ShouldBe(original);
    }

    /// <summary>V039 propagates cancellation at the bounded JSON walk checkpoint.</summary>
    [Fact]
    [Trait("Vector", "V039")]
    public void V039_CancellationDuringWalk_Propagates()
    {
        using var source = new CancellationTokenSource();
        string json = "[" + string.Join(',', Enumerable.Repeat("0", 512)) + "]";
        Should.Throw<OperationCanceledException>(() => BoundedJsonDocument.Parse(
            Encoding.UTF8.GetBytes(json),
            source.Token,
            count =>
            {
                if (count == 256)
                {
                    source.Cancel();
                }
            }));
    }

    /// <summary>V040 skips null and protects every non-null JSON value shape.</summary>
    [Theory]
    [InlineData("null", 0)]
    [InlineData("\"\"", 1)]
    [InlineData("[]", 1)]
    [InlineData("{}", 1)]
    [InlineData("42", 1)]
    [InlineData("{\"nested\":true}", 1)]
    [InlineData("[1,2]", 1)]
    [Trait("Vector", "V040")]
    public async Task V040_SelectedValueShapes_RoundTripOrSkipNullAsync(string value, int expectedProtected)
    {
        byte[] original = Encoding.UTF8.GetBytes("{\"value\":" + value + "}");
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            original,
            ["/value"],
            TestFixture.Context(),
            TestFixture.Material);
        protectedResult.ProtectedPathCount.ShouldBe(expectedProtected);
        Encoding.UTF8.GetBytes("{\"value\":" + value + "}").ShouldBe(original);
        if (expectedProtected == 0)
        {
            protectedResult.PayloadBytes.ShouldNotBeSameAs(original);
            protectedResult.PayloadBytes.ShouldBe(original);
            return;
        }

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(protectedResult.PayloadBytes);
        result.IsReadable.ShouldBeTrue();
        using JsonDocument expected = JsonDocument.Parse(original);
        using JsonDocument actual = JsonDocument.Parse(result.PayloadBytes!);
        JsonElement.DeepEquals(expected.RootElement, actual.RootElement).ShouldBeTrue();
    }

    /// <summary>V040 restores alternate valid raw token spellings byte for byte.</summary>
    [Theory]
    [InlineData("{\"value\": [ 1 , 2 ] }")]
    [InlineData("{\"value\":\"\\u0061\"}")]
    [InlineData("{\"value\":1.00e+02}")]
    [Trait("Vector", "V040")]
    public async Task V040_AlternateRawTokens_RoundTripExactBytesAsync(string json)
    {
        byte[] original = Encoding.UTF8.GetBytes(json);
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            original,
            ["/value"],
            TestFixture.Context(),
            TestFixture.Material);

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(protectedResult.PayloadBytes);

        result.IsReadable.ShouldBeTrue();
        result.PayloadBytes.ShouldBe(original);
    }

    private static async Task AssertWrapperSetMismatchAsync(
        string scenario,
        byte[] payload,
        int expectedResolverCalls)
    {
        byte[] original = [.. payload];
        int resolverCalls = 0;
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            payload,
            keyResolver: (_, _, _) =>
            {
                resolverCalls++;
                return ValueTask.FromResult<byte[]?>(TestFixture.Dek());
            });
        result.IsReadable.ShouldBeFalse(scenario);
        result.PayloadBytes.ShouldBeNull(scenario);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch, scenario);
        resolverCalls.ShouldBe(expectedResolverCalls, scenario);
        payload.ShouldBe(original, scenario);
    }
}
