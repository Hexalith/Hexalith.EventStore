using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public class ReadModelBatchFingerprintTests {
    public sealed record Doc(int N, string Text);

    private static ReadModelBatchScope Scope() =>
        new("statestore", "tenant-1", "counter", "agg-1", "counterView", "01J0BATCHULID0000000000000");

    private static ReadModelBatch SampleBatch() =>
        new(
            Scope(),
            [
                ReadModelBatchOperation.Write("detail:1", new Doc(5, "hi"), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Delete("index:old", ReadModelBatchConcurrency.IdempotentAbsent),
            ]);

    [Theory]
    [InlineData("{\"b\":1,\"a\":2}", "{\"a\":2,\"b\":1}")]
    [InlineData("{\"b\":1,\"a\":{\"d\":4,\"c\":3},\"arr\":[3,1,2]}", "{\"a\":{\"c\":3,\"d\":4},\"arr\":[3,1,2],\"b\":1}")]
    [InlineData("{ \"z\" : \"x\" }", "{\"z\":\"x\"}")]
    public void Canonicalize_SortsObjectKeysOrdinally_PreservesArrayOrder(string input, string expected) {
        byte[] canonical = ReadModelBatchCanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(input));

        Encoding.UTF8.GetString(canonical).ShouldBe(expected);
    }

    [Fact]
    public void Canonicalize_IsIndependentOfPropertyOrder() {
        byte[] a = ReadModelBatchCanonicalJson.Canonicalize(Encoding.UTF8.GetBytes("{\"x\":1,\"y\":2}"));
        byte[] b = ReadModelBatchCanonicalJson.Canonicalize(Encoding.UTF8.GetBytes("{\"y\":2,\"x\":1}"));

        a.ShouldBe(b);
    }

    [Fact]
    public void BuildCanonicalManifest_GoldenVector_IsFrozen() {
        ReadModelBatch batch = SampleBatch();
        string writeValue = Convert.ToBase64String(
            ReadModelBatchCanonicalJson.Serialize(new Doc(5, "hi")).Span);
        // The writer escapes '+' in the nested-type FullName as +; match that exactly.
        string type = System.Text.Json.JsonEncodedText.Encode(typeof(Doc).FullName!).ToString();

        string expected =
            "{\"v\":1,\"scope\":{\"store\":\"statestore\",\"tenant\":\"tenant-1\",\"domain\":\"counter\","
            + "\"aggregate\":\"agg-1\",\"projection\":\"counterView\",\"batch\":\"01J0BATCHULID0000000000000\"},"
            + "\"ops\":["
            + "{\"ord\":0,\"key\":\"detail:1\",\"kind\":\"w\",\"type\":\"" + type + "\",\"cmode\":\"Unconditional\",\"etag\":\"\",\"val\":\"" + writeValue + "\"},"
            + "{\"ord\":1,\"key\":\"index:old\",\"kind\":\"d\",\"type\":\"\",\"cmode\":\"IdempotentAbsent\",\"etag\":\"\",\"val\":\"\"}"
            + "]}";

        string manifest = Encoding.UTF8.GetString(ReadModelBatchFingerprint.BuildCanonicalManifest(batch));

        manifest.ShouldBe(expected);
    }

    // A fully-literal golden fingerprint: the expected value is a hardcoded constant, NOT derived from the
    // production serializer/canonicalizer (unlike the manifest golden above, whose value bytes flow through
    // ReadModelBatchCanonicalJson). Any silent change to value serialization, canonicalization, or
    // fingerprint material moves the computed fingerprint away from this frozen constant and fails here — a
    // fingerprint drift otherwise silently regresses idempotency (stored terminal receipts stop matching, so
    // a completed-retry re-applies the batch or returns a spurious identity conflict).
    private const string GoldenFingerprint = "v1:7swKRZpMEEyvUykZ1t_DjwFYzAYcVYCj1FxyNrRcLj4";

    [Fact]
    public void Compute_FrozenGoldenFingerprint_DetectsAnySerializationDrift() =>
        ReadModelBatchFingerprint.Compute(SampleBatch()).ShouldBe(GoldenFingerprint);

    [Fact]
    public void Compute_MatchesIndependentSha256OfManifest() {
        ReadModelBatch batch = SampleBatch();
        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
        string expected = "v1:" + Convert.ToBase64String(SHA256.HashData(manifest))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        ReadModelBatchFingerprint.Compute(batch).ShouldBe(expected);
    }

    [Fact]
    public void Compute_IsDeterministicAcrossRebuilds() =>
        ReadModelBatchFingerprint.Compute(SampleBatch()).ShouldBe(ReadModelBatchFingerprint.Compute(SampleBatch()));

    [Fact]
    public void Compute_ChangesWhenOperationOrderChanges() {
        ReadModelBatch ordered = new(
            Scope(),
            [
                ReadModelBatchOperation.Write("a", new Doc(1, "x"), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write("b", new Doc(2, "y"), ReadModelBatchConcurrency.LastWrite),
            ]);
        ReadModelBatch swapped = new(
            Scope(),
            [
                ReadModelBatchOperation.Write("b", new Doc(2, "y"), ReadModelBatchConcurrency.LastWrite),
                ReadModelBatchOperation.Write("a", new Doc(1, "x"), ReadModelBatchConcurrency.LastWrite),
            ]);

        ReadModelBatchFingerprint.Compute(ordered).ShouldNotBe(ReadModelBatchFingerprint.Compute(swapped));
    }

    [Fact]
    public void Compute_ChangesWhenValueOrConcurrencyChanges() {
        string baseline = ReadModelBatchFingerprint.Compute(new(
            Scope(),
            [ReadModelBatchOperation.Write("a", new Doc(1, "x"), ReadModelBatchConcurrency.LastWrite)]));
        string differentValue = ReadModelBatchFingerprint.Compute(new(
            Scope(),
            [ReadModelBatchOperation.Write("a", new Doc(2, "x"), ReadModelBatchConcurrency.LastWrite)]));
        string differentConcurrency = ReadModelBatchFingerprint.Compute(new(
            Scope(),
            [ReadModelBatchOperation.Write("a", new Doc(1, "x"), ReadModelBatchConcurrency.CreateOnly)]));

        differentValue.ShouldNotBe(baseline);
        differentConcurrency.ShouldNotBe(baseline);
    }

    [Fact]
    public void ScopeHash_ChangesWithAnyComponent() {
        string baseline = Scope().ComputeScopeHash();
        string differentTenant = (Scope() with { TenantId = "tenant-2" }).ComputeScopeHash();
        string differentBatch = (Scope() with { BatchId = "other" }).ComputeScopeHash();

        differentTenant.ShouldNotBe(baseline);
        differentBatch.ShouldNotBe(baseline);
        baseline.ShouldBe(Scope().ComputeScopeHash());
    }
}
