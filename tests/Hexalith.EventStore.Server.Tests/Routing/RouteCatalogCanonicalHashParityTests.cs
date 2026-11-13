using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks that reuse follows original validation and exactly one complete immutable serialization.</summary>
public sealed class RouteCatalogCanonicalHashParityTests
{
    /// <summary>Purpose, version, nested ordering, duplicate fields and numeric lexicals retain their original digests.</summary>
    [Fact]
    public void CompleteSerializedPreimagesKeepEveryCanonicalDigest()
    {
        RouteOperationContract operation = CanonicalHashTestInputs.Operation();
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        string[] schemas = ["{\"z\":1,\"a\":{\"b\":2,\"a\":3}}", "{\"a\":{\"a\":3,\"b\":2},\"z\":1}",
            "{\"same\":1,\"same\":2}", "{\"same\":2,\"same\":1}", "{\"n\":1}", "{\"n\":1.0}", "{\"n\":1e0}"];
        foreach (string raw in schemas)
        {
            using var document = JsonDocument.Parse(raw);
            RouteOperationContract changed = operation with { RequestSchema = document.RootElement };
            string expected = CanonicalHashTestInputs.Uncached(() => RouteCatalogCodec.ComputeOperationContractDigest(changed));
            RouteCatalogCodec.ComputeOperationContractDigest(changed).ShouldBe(expected);
            RouteCatalogCodec.ComputeOperationContractDigest(changed).ShouldBe(expected);
        }
        object[] purposes = [new { purpose = "a", schemaVersion = 1, payload = "same" },
            new { purpose = "b", schemaVersion = 1, payload = "same" }, new { purpose = "a", schemaVersion = 2, payload = "same" }];
        string[] distinct = purposes.Select(value =>
        {
            string expected = CanonicalHashTestInputs.Uncached(() => CanonicalHashTestInputs.Hash(value));
            string actual = CanonicalHashTestInputs.Hash(value);
            actual.ShouldBe(expected);
            return actual;
        }).ToArray();
        distinct.Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
        hashes.Hits.ShouldBeGreaterThan(0);
        bytes.Count.ShouldBe(0);
        scope.CacheHits.ShouldBe(0);
    }

    /// <summary>Every changed operation scalar or schema is freshly serialized, including a return to prior exact input.</summary>
    [Fact]
    public void OperationChangesAndReversionsCannotReuseAnotherPreimage()
    {
        RouteOperationContract operation = CanonicalHashTestInputs.Operation();
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        using var request = JsonDocument.Parse("{\"type\":\"object\",\"description\":\"changed-request\"}");
        using var result = JsonDocument.Parse("{\"type\":\"object\",\"description\":\"changed-result\"}");
        RouteOperationContract[] changes = [operation, operation with { RouteId = "changed-route" }, operation with { ContractVersion = "v2" },
            operation with { Kind = operation.Kind == "query" ? "command" : "query" }, operation with { Domain = "other" },
            operation with { MessageType = "OtherMessage" }, operation with { Ingress = operation.Ingress == "public" ? "internal" : "public" },
            operation with { Eligibility = "confirmation-required" }, operation with { RequestSchema = request.RootElement },
            operation with { ResultSchema = result.RootElement }, operation];
        foreach (RouteOperationContract changed in changes)
        {
            string expected = CanonicalHashTestInputs.Uncached(() => RouteCatalogCodec.ComputeOperationContractDigest(changed));
            RouteCatalogCodec.ComputeOperationContractDigest(changed).ShouldBe(expected);
        }
        hashes.Hits.ShouldBeGreaterThan(0);
    }

    /// <summary>Mutable descriptor lists are still read, validated and normalized on every computation.</summary>
    [Fact]
    public void MutableDescriptorMetadataKeepsFreshValidationAndReversion()
    {
        IdempotencyDescriptorSchema original = CanonicalHashTestInputs.Descriptor();
        var fields = original.Fields.ToList();
        var descriptor = original with { Fields = fields };
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        string first = CanonicalHashTestInputs.Uncached(() => RouteCatalogCodec.ComputeDescriptorDigest(descriptor));
        RouteCatalogCodec.ComputeDescriptorDigest(descriptor).ShouldBe(first);
        fields[0] = fields[0] with { Name = "changed-field" };
        string changed = CanonicalHashTestInputs.Uncached(() => RouteCatalogCodec.ComputeDescriptorDigest(descriptor));
        changed.ShouldNotBe(first);
        RouteCatalogCodec.ComputeDescriptorDigest(descriptor).ShouldBe(changed);
        fields[0] = original.Fields[0];
        RouteCatalogCodec.ComputeDescriptorDigest(descriptor).ShouldBe(first);
        fields[0] = fields[0] with { Type = "invalid" };
        var expected = Should.Throw<RouteCatalogValidationException>(() => CanonicalHashTestInputs.Uncached(() => RouteCatalogCodec.ComputeDescriptorDigest(descriptor)));
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ComputeDescriptorDigest(descriptor)).Diagnostics.ShouldBe(expected.Diagnostics);
    }

    /// <summary>Scalar validation still precedes schema access, and disposed schemas never become cached truth.</summary>
    [Fact]
    public void InvalidScalarsAndDisposedSchemasKeepOriginalErrorOrder()
    {
        RouteOperationContract operation = CanonicalHashTestInputs.Operation();
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        RouteCatalogCodec.ComputeOperationContractDigest(operation);
        int retained = hashes.Count;
        using var document = JsonDocument.Parse("{}");
        var disposed = operation with { RequestSchema = document.RootElement };
        document.Dispose();
        Should.Throw<ObjectDisposedException>(() => RouteCatalogCodec.ComputeOperationContractDigest(disposed));
        var invalid = disposed with { RouteId = "" };
        var expected = Should.Throw<RouteCatalogValidationException>(() => CanonicalHashTestInputs.Uncached(() => RouteCatalogCodec.ComputeOperationContractDigest(invalid)));
        Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ComputeOperationContractDigest(invalid)).Diagnostics.ShouldBe(expected.Diagnostics);
        hashes.Count.ShouldBe(retained);
    }

    /// <summary>A miss computes from its single serializer snapshot rather than observing mutable getters twice.</summary>
    [Fact]
    public void MissAndHitObserveTheOriginalSerializerExactlyOnce()
    {
        var uncached = new CanonicalHashChangingPreimage();
        string expected = CanonicalHashTestInputs.Uncached(() => CanonicalHashTestInputs.Hash(uncached));
        uncached.Reads.ShouldBe(1);
        using var bytes = new RouteCatalogValidatedBytesMemo();
        using var hashes = new RouteCatalogCanonicalHashMemo();
        using var scope = RouteCatalogDecodeScope.EnterWithCanonicalHashMemo(bytes, hashes);
        var miss = new CanonicalHashChangingPreimage();
        CanonicalHashTestInputs.Hash(miss).ShouldBe(expected);
        miss.Reads.ShouldBe(1);
        var hit = new CanonicalHashChangingPreimage();
        CanonicalHashTestInputs.Hash(hit).ShouldBe(expected);
        hit.Reads.ShouldBe(1);
        hashes.Count.ShouldBe(1);
        hashes.Hits.ShouldBe(1);
    }
}
