// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 8-10, 13-15; lifecycle amendment sections 1-8.
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>V046-V047 observable conditional-create races regenerate a wholly fresh reference and DEK before AES.</summary>
public sealed class ReservationCollisionTests
{
    [Fact]
    public async Task V046_V047_ConditionalCreateLoserTerminalizesItsIntentAndUsesFreshMaterial()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var keys = new List<string>();
        var deks = new List<byte[]>();
        fixture.Backend.Wrap = async (operationId, bytes, token) =>
        {
            OperationV1 intent = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operationId), token))!.Value;
            keys.Add(intent.KeyRef); deks.Add(bytes.ToArray());
            if (keys.Count == 1)
            {
                fixture.Store.BeforeCommit = () =>
                {
                    var indexes = new PayloadKeyIndexes(fixture.Store, fixture.Clock);
                    ScopeKeyIndexSnapshot scope = indexes.ReadScopeAsync(fixture.Scope).GetAwaiter().GetResult();
                    KekKeyIndexSnapshot inventory = indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(fixture.Backend.BackendId), PayloadProtectionStateKeys.Digest("fixture:version-01")).GetAwaiter().GetResult();
                    string now = PayloadProtectionStateCodec.Timestamp(fixture.Clock.GetUtcNow());
                    WrappedDekV1 winner = PayloadProtectionStateCodec.Seal(new("tenant-a", "parties", "party-01", fixture.Scope, intent.KeyRef, 1, "Reserved",
                        fixture.Backend.BackendId, "fixture:version-01", "RSA-OAEP-256", "AA", 1, 7, 1, "01J00000000000000000000400", now, now, new('0', 64), new('0', 64)));
                    fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(winner), .. indexes.SetScopeEntry(scope, new(intent.KeyRef, 1, inventory.ProviderDigest, "Reserved"), true),
                        .. indexes.SetInventoryEntry(inventory, new(fixture.Scope, intent.KeyRef, 1), true)]).GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
                };
            }
            return new(PayloadBackendFailure.None, new byte[32], "fixture:version-01");
        };
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        keys.Count.ShouldBe(2); keys[0].ShouldNotBe(keys[1]);
        CryptographicOperations.FixedTimeEquals(deks[0], deks[1]).ShouldBeFalse();
        write.Completion!.KeyReference.ShouldBe(keys[1]);
        WorkCatalogSnapshot work = await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root);
        work.Head.Value.EntryCount.ShouldBe(1u);
        work.Pages.SelectMany(p => p.Value.Entries).Single().OperationId.ShouldBe(write.Completion.OperationId);
        fixture.Store.Audits.Single(a => a.OperationId != write.Completion.OperationId).Result.ShouldBe("Failed");
        fixture.Store.Audits.Single(a => a.OperationId != write.Completion.OperationId).ReasonCode.ShouldBe("consistency-mismatch");
        foreach (byte[] dek in deks) { CryptographicOperations.ZeroMemory(dek); }
    }
}
