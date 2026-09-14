using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Covers JSON traversal, selection, and atomic transformation vectors V027 and V034-V040.
/// </summary>
public sealed class JsonTransformTests {
    /// <summary>Verifies the writer rejects every pre-existing reserved wrapper member before encryption.</summary>
    [Theory]
    [InlineData("{\"$pdenc\":\"value\",\"email\":\"a\"}")]
    [InlineData("{\"nested\":{\"$pdenc\":\"value\"},\"email\":\"a\"}")]
    public void ReservedWrapperMember_IsRejectedBeforeProtection(string json) {
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes(json),
            ["/email"],
            TestFixture.Context(),
            TestFixture.Material));
    }

    /// <summary>V027 detects removed, plaintext-substituted, duplicated, and added wrappers before plaintext escapes.</summary>
    [Fact]
    [Trait("Vector", "V027")]
    public async Task V027_EveryWrapperSetChange_FailsAtomicallyAsync() {
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
    public void V034_DuplicateJsonMember_IsRejectedBeforeEncryption() {
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":1,\"email\":2}"),
            ["/email"],
            TestFixture.Context(),
            TestFixture.Material));
    }

    /// <summary>V035 rejects unresolved, scalar-traversal, and out-of-range paths.</summary>
    [Theory]
    [InlineData("/missing")]
    [InlineData("/email/child")]
    [InlineData("/items/2")]
    [Trait("Vector", "V035")]
    public void V035_PathResolutionFailures_AreBounded(string path) {
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":\"a\",\"items\":[0]}"),
            [path],
            TestFixture.Context(),
            TestFixture.Material));
    }

    /// <summary>V036 rejects selected ancestor/descendant overlap independent of input order.</summary>
    [Theory]
    [InlineData("/person", "/person/email")]
    [InlineData("/person/email", "/person")]
    [Trait("Vector", "V036")]
    public void V036_AncestorDescendantSelection_IsRejected(string first, string second) {
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"person\":{\"email\":\"a\"}}"),
            [first, second],
            TestFixture.Context(),
            TestFixture.Material));
    }

    /// <summary>V037 rejects a pointer selected more than once.</summary>
    [Fact]
    [Trait("Vector", "V037")]
    public void V037_RepeatedPointerSelection_IsRejected() {
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            Encoding.UTF8.GetBytes("{\"email\":\"a\"}"),
            ["/email", "/email"],
            TestFixture.Context(),
            TestFixture.Material));
    }

    /// <summary>V038 rejects a serialized cycle-equivalent graph within the fixed depth bound before material creation.</summary>
    [Fact]
    [Trait("Vector", "V038")]
    public void V038_CycleEquivalentOverDepthJson_IsRejected() {
        string json = new string('[', 65) + "0" + new string(']', 65);
        byte[] payload = Encoding.UTF8.GetBytes(json);
        byte[] original = payload.ToArray();
        int materialCalls = 0;

        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionCore().ProtectEvent(
            payload,
            ["/0"],
            TestFixture.Context(),
            () => {
                materialCalls++;
                return TestFixture.Material();
            }));

        materialCalls.ShouldBe(0);
        payload.ShouldBe(original);
    }

    /// <summary>V039 propagates cancellation at the bounded JSON walk checkpoint.</summary>
    [Fact]
    [Trait("Vector", "V039")]
    public void V039_CancellationDuringWalk_Propagates() {
        using var source = new CancellationTokenSource();
        string json = "[" + string.Join(',', Enumerable.Repeat("0", 512)) + "]";
        Should.Throw<OperationCanceledException>(() => BoundedJsonDocument.Parse(
            Encoding.UTF8.GetBytes(json),
            source.Token,
            count => {
                if (count == 256) {
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
    public async Task V040_SelectedValueShapes_RoundTripOrSkipNullAsync(string value, int expectedProtected) {
        byte[] original = Encoding.UTF8.GetBytes("{\"value\":" + value + "}");
        CoreProtectionResult protectedResult = new PayloadProtectionCore().ProtectEvent(
            original,
            ["/value"],
            TestFixture.Context(),
            TestFixture.Material);
        protectedResult.ProtectedPathCount.ShouldBe(expectedProtected);
        Encoding.UTF8.GetBytes("{\"value\":" + value + "}").ShouldBe(original);
        if (expectedProtected == 0) {
            protectedResult.PayloadBytes.ShouldBeSameAs(original);
            return;
        }

        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(protectedResult.PayloadBytes);
        result.IsReadable.ShouldBeTrue();
        using JsonDocument expected = JsonDocument.Parse(original);
        using JsonDocument actual = JsonDocument.Parse(result.PayloadBytes!);
        JsonElement.DeepEquals(expected.RootElement, actual.RootElement).ShouldBeTrue();
    }

    private static async Task AssertWrapperSetMismatchAsync(
        string scenario,
        byte[] payload,
        int expectedResolverCalls) {
        byte[] original = payload.ToArray();
        int resolverCalls = 0;
        CoreUnprotectionResult result = await TestFixture.UnprotectAsync(
            payload,
            keyResolver: (_, _, _) => {
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
