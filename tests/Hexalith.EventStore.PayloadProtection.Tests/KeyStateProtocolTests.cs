using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Closed state schemas and independently reproduced key integrity vectors.</summary>
public sealed class KeyStateProtocolTests
{
    private const string Scope = "b7672350411bd42a27dc3684d48b8bc268b5615ecd52252bb3aecb666ec3a467";
    private const string Root = "19845ecc5f96a5c6beca937c86f765ddd29a8909df9a091d5f49b5f454b980f9";
    private const string Operation = "01J00000000000000000000001";
    private const string Timestamp = "2026-10-23T00:00:00.0000000Z";

    [Fact]
    public void ExactScopeAndKeyGrammarMatchIndependentPythonFraming()
    {
        PayloadProtectionStateKeys.ScopeDigest(new("tenant-a", "parties", "party-01")).ShouldBe(Scope);
        PayloadProtectionStateKeys.TenantDomainDigest("tenant-a", "parties").ShouldBe(Root);
        PayloadProtectionStateKeys.MemberPage(Root, 255).ShouldBe($"eventstore:pp:v2:members:{Root}:0000000255");
        PayloadProtectionStateKeys.Capability(Root, 42, Operation).ShouldBe($"eventstore:pp:v2:capability:{Root}:00000000000000000042:{Operation}");
        PayloadProtectionStateKeys.ClearAck(Scope, 42, Operation).ShouldBe($"eventstore:pp:v2:clear-ack:{Scope}:42:{Operation}");
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateKeys.MemberPage(Root, 256));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateKeys.Key(Scope, Operation, 0));
    }

    [Fact]
    public void WrappedFieldHashMatchesIndependentPythonAndIgnoresJsonMemberOrder()
    {
        WrappedDekV1 key = Wrapped();
        key.WrappedDekSha256.ShouldBe("630dcd2966c4336691125448bbb25b4ff412a49c732db2c8abc1b8581bd710dd");
        key.CanonicalFieldsSha256.ShouldBe("272d7afa5ed61ae1084833186cfe73a314d2430bf1ea731ad19bb50ad1a1efe3");
        byte[] bytes = PayloadProtectionStateCodec.Encode(key);
        Encoding.UTF8.GetString(bytes).ShouldStartWith("{\"schemaVersion\":1,\"tenantId\":");
        using JsonDocument json = JsonDocument.Parse(bytes);
        using var reordered = new MemoryStream();
        using (var writer = new Utf8JsonWriter(reordered))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in json.RootElement.EnumerateObject().Reverse()) { property.WriteTo(writer); }
            writer.WriteEndObject();
        }

        PayloadProtectionStateCodec.Decode<WrappedDekV1>(reordered.ToArray()).ShouldBe(key);
    }

    [Theory]
    [InlineData("tenantId", "tenant-b")]
    [InlineData("domain", "other")]
    [InlineData("aggregateId", "party-02")]
    [InlineData("scopeDigest", "09845ecc5f96a5c6beca937c86f765ddd29a8909df9a091d5f49b5f454b980f9")]
    [InlineData("keyRef", "01J00000000000000000000002")]
    [InlineData("state", "Active")]
    [InlineData("backendId", "other-backend")]
    [InlineData("providerKeyVersionId", "fixture:version-02")]
    [InlineData("wrapAlgorithmId", "RSA-OAEP")]
    [InlineData("creatingOperationId", "01J00000000000000000000002")]
    [InlineData("createdAtUtc", "2026-10-22T00:00:00.0000000Z")]
    [InlineData("updatedAtUtc", "2026-10-24T00:00:00.0000000Z")]
    [InlineData("wrappedDek", "AQECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8")]
    public void EveryTextHashFieldMutationFailsWithoutResealing(string field, string value)
    {
        JsonObject json = JsonNode.Parse(PayloadProtectionStateCodec.Encode(Wrapped()))!.AsObject();
        json[field] = value;
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV1>(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData("dekVersion")]
    [InlineData("lifecycleEpoch")]
    [InlineData("fenceApprovalEpoch")]
    [InlineData("highestWrittenEpoch")]
    public void EveryNumericHashFieldMutationFailsWithoutResealing(string field)
    {
        JsonObject json = JsonNode.Parse(PayloadProtectionStateCodec.Encode(Wrapped()))!.AsObject();
        json[field] = 99;
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WrappedDekV1>(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData("\"revision\":0", "\"revision\":0.0")]
    [InlineData("\"revision\":0", "\"revision\":1e0")]
    [InlineData("\"revision\":0", "\"revision\":-1")]
    [InlineData("\"revision\":0", "\"revision\":18446744073709551616")]
    [InlineData("\"revision\":0", "\"revision\":0,\"revision\":0")]
    [InlineData("\"revision\":0", "\"unknown\":0,\"revision\":0")]
    [InlineData("\"revision\":0,", "")]
    [InlineData("\"revision\":0", "\"revision\":null")]
    [InlineData("2026-10-23T00:00:00.0000000Z", "2026-10-23T00:00:00Z")]
    [InlineData("2026-10-23T00:00:00.0000000Z", "2026-10-23T00:00:00.0000000+00:00")]
    public void ClosedSchemaRejectsDuplicateUnknownMissingNullNoncanonicalNumbersAndTimestamps(string before, string after)
    {
        byte[] json = PayloadProtectionStateCodec.Encode(new WorkHeadV1(Root, 0, 0, 0, Timestamp));
        string mutated = Encoding.UTF8.GetString(json).Replace(before, after, StringComparison.Ordinal);
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WorkHeadV1>(Encoding.UTF8.GetBytes(mutated)));
    }

    [Fact]
    public void CatalogBoundsAndPermanentMemberOrdinalsAreValidated()
    {
        PayloadProtectionStateCodec.Encode(new MemberHeadV1(Root, ulong.MaxValue, 256, 65536, Timestamp)).ShouldNotBeEmpty();
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(new MemberHeadV1(Root, 0, 257, 65537, Timestamp)));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(new MemberHeadV1(Root, 0, 1, 0, Timestamp)));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(new MemberPageV1(Root, 1, [new(1, 1, Operation, "Serving")])));
        var entries = Enumerable.Range(0, 257).Select(i => new WorkEntryV1(i.ToString("D26"), Scope)).ToArray();
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(new WorkPageV1(Root, 0, entries)));
        PayloadProtectionStateCodec.Encode(new WorkHeadV1(Root, 1, 1, 0, Timestamp)).ShouldNotBeEmpty(); // Empty retained pages are legal.
    }

    [Fact]
    public void AuditOmitsOptionalMembersAndRejectsUnknownReasons()
    {
        var audit = new PayloadProtectionAuditRecord(Operation, Operation, null, null, "tenant-a", "parties", null,
            "Reconcile", null, null, 1, 2, "fixture-backend", "v2", null, null, 1, 0, "Pending", "ambiguous-outcome", "fixture-actor", Timestamp);
        string json = Encoding.UTF8.GetString(PayloadProtectionStateCodec.Encode(audit));
        json.ShouldNotContain("workflowId"); json.ShouldNotContain(":null");
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(audit with { ReasonCode = "provider-private-canary" }));
        string withNull = json.Replace("\"operationId\":", "\"workflowId\":null,\"operationId\":", StringComparison.Ordinal);
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<PayloadProtectionAuditRecord>(Encoding.UTF8.GetBytes(withNull)));
        audit.ToString().ShouldBe(nameof(PayloadProtectionAuditRecord));
    }

    [Fact]
    public void RootEnrollmentIsBoundedAndCannotDropUnclearedRoots()
    {
        var previous = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var successor = new PayloadProtectionScopeRegistry([new("tenant-b", "parties")]);
        Should.Throw<Hexalith.EventStore.PayloadProtection.PayloadProtectionStateException>(() => previous.ValidateSuccessor(successor, new HashSet<string>()));
        previous.ValidateSuccessor(successor, new HashSet<string> { Root });
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionScopeRegistry([]));
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionScopeRegistry([new("tenant-a", "parties"), new("tenant-a", "parties")]));
        new PayloadProtectionScopeRegistry(Enumerable.Range(0, 4096).Select(i => new PayloadProtectionScope("tenant-" + i, "parties"))).Roots.Count.ShouldBe(4096);
        Should.Throw<PayloadProtectionFormatException>(() => new PayloadProtectionScopeRegistry(Enumerable.Range(0, 4097).Select(i => new PayloadProtectionScope("tenant-" + i, "parties"))));
    }

    [Fact]
    public void DurableValueByteCeilingIsExact()
    {
        WrappedDekV1 key = Wrapped();
        int current = PayloadProtectionStateCodec.Encode(key).Length;
        int providerLength = PayloadProtectionStateCodec.MaximumBytes - current + key.ProviderKeyVersionId.Length;
        WrappedDekV1 exact = PayloadProtectionStateCodec.Seal(key with { ProviderKeyVersionId = new('a', providerLength) });
        PayloadProtectionStateCodec.Encode(exact).Length.ShouldBe(PayloadProtectionStateCodec.MaximumBytes);
        WrappedDekV1 oversized = PayloadProtectionStateCodec.Seal(key with { ProviderKeyVersionId = new('a', providerLength + 1) });
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(oversized));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<WorkHeadV1>(new byte[PayloadProtectionStateCodec.MaximumBytes + 1]));
    }

    [Fact]
    public async Task ExactMaximumRecordProviderIdWorksThroughDigestIndexesAndCache()
    {
        WrappedDekV1 key = Wrapped(); int current = PayloadProtectionStateCodec.Encode(key).Length;
        string provider = new('a', PayloadProtectionStateCodec.MaximumBytes - current + key.ProviderKeyVersionId.Length);
        key = PayloadProtectionStateCodec.Seal(key with { ProviderKeyVersionId = provider });
        PayloadProtectionStateCodec.Encode(key).Length.ShouldBe(65536);
        PayloadProtectionStateKeys.Digest(provider).Length.ShouldBe(64);
        var store = new MemoryPayloadProtectionStateStore(); var indexes = new PayloadKeyIndexes(store);
        string backendDigest = PayloadProtectionStateKeys.Digest(key.BackendId); string providerDigest = PayloadProtectionStateKeys.Digest(provider);
        (await store.CommitAsync([PayloadProtectionStateMutation.Put(key),
            .. indexes.SetScopeEntry(await indexes.ReadScopeAsync(Scope), new(key.KeyRef, 1, providerDigest, key.State), true),
            .. indexes.SetInventoryEntry(await indexes.ReadKekAsync(backendDigest, providerDigest), new(Scope, key.KeyRef, 1), true)])).ShouldBe(PayloadStateCommitResult.Committed);
        PayloadKeyIndexes.VerifyMembership(key, await indexes.ReadScopeAsync(Scope), await indexes.ReadKekAsync(backendDigest, providerDigest));
        await using var cache = new PayloadKeyCache();
        await using var first = await cache.GetAsync(new(Scope, key.KeyRef, 1, 1, provider), _ => ValueTask.FromResult(PayloadKeyResolution.Success(new byte[32])));
        first.Reason.ShouldBeNull();
        await using var hit = await cache.GetAsync(new(Scope, key.KeyRef, 1, 1, provider), _ => throw new InvalidOperationException());
        hit.Reason.ShouldBeNull();
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateKeys.Digest(new('a', 65537)));
    }

    [Fact]
    public void ReconciliationUsesOnlyTheApprovedScopeWideOrPositivePerKeyForms()
    {
        var work = new ReconciliationV1(Operation, Scope, "", 0, "Invalidate", Timestamp, 0, "Pending", "domain-invalidating", Timestamp);
        PayloadProtectionStateCodec.Encode(work).ShouldNotBeEmpty();
        PayloadProtectionStateCodec.Encode(work with { Kind = "Delete" }).ShouldNotBeEmpty();
        PayloadProtectionStateCodec.Encode(work with { KeyRef = Operation, DekVersion = 1 }).ShouldNotBeEmpty();
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(work with { KeyRef = Operation }));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(work with { DekVersion = 1 }));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(work with { Kind = "Create" }));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(work with { Kind = "Activate" }));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(work with { Kind = "Rewrap" }));
    }

    private static WrappedDekV1 Wrapped() => PayloadProtectionStateCodec.Seal(new("tenant-a", "parties", "party-01", Scope,
        "01J00000000000000000000000", 1, "Reserved", "fixture-backend", "fixture:version-01", "RSA-OAEP-256",
        "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8", 1, 2, 3, Operation, Timestamp, Timestamp, new('0', 64), new('0', 64)));
}
