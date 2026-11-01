using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V057/V058 complete scope and KEK membership with atomic generation changes.</summary>
public sealed class KeyIndexTests
{
    private static readonly string Scope = PayloadProtectionStateKeys.ScopeDigest(new("tenant-a", "parties", "party-01"));
    private static readonly string Backend = PayloadProtectionStateKeys.Digest("fixture-backend");
    private static readonly string Provider = PayloadProtectionStateKeys.Digest("fixture:version-01");
    private const string Timestamp = "2026-10-23T00:00:00.0000000Z";

    [Fact]
    public async Task V057_V137_TwoPageIndexesStayGenerationConsistentAndInventoryRemovalRetainsAddresses()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        for (int i = 0; i < 257; i++)
        {
            ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(Scope);
            KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(Backend, Provider);
            string key = i.ToString("D26");
            (await store.CommitAsync([.. indexes.SetScopeEntry(scope, new(key, 1, Provider, "Reserved"), true),
                .. indexes.SetInventoryEntry(inventory, new(Scope, key, 1), true)])).ShouldBe(PayloadStateCommitResult.Committed);
        }
        ScopeKeyIndexSnapshot finalScope = await indexes.ReadScopeAsync(Scope);
        KekKeyIndexSnapshot finalInventory = await indexes.ReadKekAsync(Backend, Provider);
        finalScope.Head!.Value.EntryCount.ShouldBe(257u);
        finalScope.Pages.Count.ShouldBe(2);
        finalScope.Pages.ShouldAllBe(p => p.Value.Generation == finalScope.Head.Value.Generation);
        finalInventory.Head!.Value.EntryCount.ShouldBe(257ul);
        finalInventory.Pages.ShouldAllBe(p => p.Value.Generation == finalInventory.Head.Value.Generation);
        (await store.CommitAsync(indexes.SetInventoryEntry(finalInventory, new(Scope, 256.ToString("D26"), 1), false))).ShouldBe(PayloadStateCommitResult.Committed);
        KekKeyIndexSnapshot removed = await indexes.ReadKekAsync(Backend, Provider);
        removed.Head!.Value.EntryCount.ShouldBe(256ul);
        removed.Pages.Count.ShouldBe(2);
        removed.Pages[1].Value.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task V058_StaleIndexETagAbortsKeyAndBothIndexWritesTogether()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(Scope);
        KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(Backend, Provider);
        WrappedDekV1 key = Key();
        IReadOnlyList<PayloadProtectionStateMutation> mutations = [PayloadProtectionStateMutation.Put(key),
            .. indexes.SetScopeEntry(scope, new(key.KeyRef, 1, Provider, "Reserved"), true),
            .. indexes.SetInventoryEntry(inventory, new(Scope, key.KeyRef, 1), true)];
        await store.CommitAsync([PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(Backend, Provider, 1, 0, 0, Timestamp))]);
        (await store.CommitAsync(mutations)).ShouldBe(PayloadStateCommitResult.Conflict);
        (await store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(key))).ShouldBeNull();
        (await indexes.ReadScopeAsync(Scope)).Head.ShouldBeNull();
        (await indexes.ReadKekAsync(Backend, Provider)).Head!.Value.EntryCount.ShouldBe(0ul);
    }

    [Fact]
    public async Task V057_MembershipRequiresMatchingRecordStateAndExactProviderInBothDirections()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        ScopeKeyIndexSnapshot empty = await indexes.ReadScopeAsync(Scope);
        KekKeyIndexSnapshot missing = await indexes.ReadKekAsync(Backend, Provider);
        WrappedDekV1 key = Key();
        Should.Throw<PayloadProtectionStateException>(() => PayloadKeyIndexes.VerifyMembership(key, empty, missing));
        await store.CommitAsync([.. indexes.SetScopeEntry(empty, new(key.KeyRef, 1, Provider, "Reserved"), true),
            .. indexes.SetInventoryEntry(missing, new(Scope, key.KeyRef, 1), true)]);
        ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(Scope);
        KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(Backend, Provider);
        PayloadKeyIndexes.VerifyMembership(key, scope, inventory);
        Should.Throw<PayloadProtectionStateException>(() => PayloadKeyIndexes.VerifyMembership(key with { State = "Active" }, scope, inventory));
        Should.Throw<PayloadProtectionStateException>(() => PayloadKeyIndexes.VerifyMembership(key with { ProviderKeyVersionId = "fixture:version-02" }, scope, inventory));
        await store.CommitAsync([PayloadProtectionStateMutation.Put(scope.Pages[0].Value with { Generation = 0 }, scope.Pages[0].ETag)]);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => indexes.ReadScopeAsync(Scope).AsTask());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConcurrentMembershipCommitDuringEnumerationReturnsAFreshConsistentSnapshot(bool inventory, bool initiallyAbsent)
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        var scope = await indexes.ReadScopeAsync(Scope);
        var kek = await indexes.ReadKekAsync(Backend, Provider);
        if (!initiallyAbsent)
        {
            await store.CommitAsync([.. indexes.SetScopeEntry(scope, new("01J00000000000000000000001", 1, Provider, "Reserved"), true),
                .. indexes.SetInventoryEntry(kek, new(Scope, "01J00000000000000000000001", 1), true)]);
            scope = await indexes.ReadScopeAsync(Scope);
            kek = await indexes.ReadKekAsync(Backend, Provider);
        }
        string pageKey = inventory ? PayloadProtectionStateKeys.KekPage(Backend, Provider, 0) : PayloadProtectionStateKeys.IndexPage(Scope, 0);
        bool injected = false;
        store.Reading = key =>
        {
            if (key != pageKey) { return; }
            store.Reading = null;
            injected = true;
            store.CommitAsync([.. indexes.SetScopeEntry(scope, new("01J00000000000000000000002", 1, Provider, "Reserved"), true),
                .. indexes.SetInventoryEntry(kek, new(Scope, "01J00000000000000000000002", 1), true)])
                .GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
        };
        if (inventory)
        {
            KekKeyIndexSnapshot result = await indexes.ReadKekAsync(Backend, Provider);
            result.Head!.Value.EntryCount.ShouldBe(initiallyAbsent ? 1ul : 2ul);
            result.Pages.ShouldAllBe(page => page.Value.Generation == result.Head.Value.Generation);
            result.Pages.SelectMany(page => page.Value.Entries).ShouldContain(new KekInventoryEntryV1(Scope, "01J00000000000000000000002", 1));
        }
        else
        {
            ScopeKeyIndexSnapshot result = await indexes.ReadScopeAsync(Scope);
            result.Head!.Value.EntryCount.ShouldBe(initiallyAbsent ? 1u : 2u);
            result.Pages.ShouldAllBe(page => page.Value.Generation == result.Head.Value.Generation);
            result.Pages.SelectMany(page => page.Value.Entries).ShouldContain(new ScopeIndexEntryV1("01J00000000000000000000002", 1, Provider, "Reserved"));
        }
        injected.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PageWithoutAHeadOrWithAnUnchangedWrongGenerationRemainsCorruption(bool inventory)
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        if (inventory) { await store.CommitAsync([PayloadProtectionStateMutation.Put(new KekInventoryPageV1(Backend, Provider, 2, 0, []))]); }
        else { await store.CommitAsync([PayloadProtectionStateMutation.Put(new ScopeIndexPageV1(Scope, 2, 0, []))]); }
        async Task ReadAsync()
        {
            if (inventory) { _ = await indexes.ReadKekAsync(Backend, Provider); }
            else { _ = await indexes.ReadScopeAsync(Scope); }
        }
        (await Should.ThrowAsync<PayloadProtectionStateException>(ReadAsync)).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        if (inventory) { await store.CommitAsync([PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(Backend, Provider, 1, 1, 0, Timestamp))]); }
        else { await store.CommitAsync([PayloadProtectionStateMutation.Put(new ScopeIndexHeadV1(Scope, 1, 1, 0, Timestamp))]); }
        (await Should.ThrowAsync<PayloadProtectionStateException>(ReadAsync)).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
    }

    private static WrappedDekV1 Key() => PayloadProtectionStateCodec.Seal(new("tenant-a", "parties", "party-01", Scope,
        "01J00000000000000000000000", 1, "Reserved", "fixture-backend", "fixture:version-01", "RSA-OAEP-256", "AA", 1, 1, 1,
        "01J00000000000000000000001", Timestamp, Timestamp, new('0', 64), new('0', 64)));
}
