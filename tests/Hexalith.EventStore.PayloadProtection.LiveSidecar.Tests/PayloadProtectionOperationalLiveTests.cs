using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Qualifies actual cumulative transport admission and writer capacity without changing structural schemas.</summary>
public sealed class PayloadProtectionOperationalLiveTests(PayloadProtectionDaprFixture fixture) : IClassFixture<PayloadProtectionDaprFixture>
{
    [Fact]
    public async Task ExactChargedTransactionBoundaryCommitsAndOneByteOverIsRejectedBeforeMutation()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)); var token = timeout.Token;
        await new PayloadProtectionCapabilityProbe(fixture.Store).ProbeAsync(token);
        const int count = 32; int charge = (PayloadProtectionOperationalLimits.TransactionBytes - 1024) / count;
        var entropy = new CryptographicPayloadProtectionEntropy(); var values = new List<WrappedDekV1>(); var mutations = new List<PayloadProtectionStateMutation>();
        var identity = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("tenant-transport-boundary", "qualification", "aggregate-01");
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity); string now = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        for (int i = 0; i < count; i++)
        {
            var value = PayloadProtectionStateCodec.Seal(new WrappedDekV1(identity.TenantId, identity.Domain, identity.AggregateId, scope, entropy.CreateKeyReference(), 1, "Reserved",
                "fixture-backend", "p", "RSA-OAEP-256", "AA", 1, 1, 1, entropy.CreateKeyReference(), now, now, new('0', 64), new('0', 64)));
            int padding = charge - PayloadProtectionOperationalLimits.Charge(PayloadProtectionStateMutation.Put(value));
            value = PayloadProtectionStateCodec.Seal(value with { ProviderKeyVersionId = new('p', 1 + padding) });
            values.Add(value); mutations.Add(PayloadProtectionStateMutation.Put(value));
        }
        (mutations.Sum(PayloadProtectionOperationalLimits.Charge) + 1024).ShouldBe(PayloadProtectionOperationalLimits.TransactionBytes);
        (await fixture.Store.CommitAsync(mutations, token)).ShouldBe(PayloadStateCommitResult.Committed);
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(values[0]), token))!.Value.ShouldBe(values[0]);
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(values[^1]), token))!.Value.ShouldBe(values[^1]);
        var enlarged = PayloadProtectionStateCodec.Seal(values[0] with { ProviderKeyVersionId = values[0].ProviderKeyVersionId + "p" });
        var over = mutations.ToArray(); over[0] = PayloadProtectionStateMutation.Put(enlarged);
        await Should.ThrowAsync<PayloadProtectionFormatException>(() => fixture.Store.CommitAsync(over, token).AsTask());
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(values[0]), token))!.Value.ShouldBe(values[0]);
        // This is primitive transport qualification, not a persisted-event/actor or key-custody claim.
        int completed = 0; int length = PayloadProtectionStateCodec.Encode(values[0]).Length;
        using (var phase = new PayloadOperationBudget(TimeProvider.System, token))
        {
            var failure = await Should.ThrowAsync<PayloadProtectionStateException>(async () =>
            {
                while (true) { await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(values[0]), token); completed++; }
            });
            failure.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        }
        completed.ShouldBe(checked((int)((PayloadProtectionOperationalLimits.ReadBytes - PayloadProtectionStateCodec.MaximumBytes) / length + 1)));
    }

    [Fact]
    public async Task QualifiedWriterPreservesTwoPageIndexesAt257KeysAndRetainedMemberIncarnations()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-operational-writer", token);
        for (int i = 0; i < 257; i++)
        {
            if (i % 32 == 0) { await env.Leases.RenewAsync(env.Identity, token); }
            var context = new PayloadProtectionContext(env.Identity, "qualification-event", PayloadProtectionPayloadKind.Event, checked((ulong)i));
            var write = await env.Reservations.ProtectEventAsync("{\"secret\":\"writer-capacity-canary\"}"u8.ToArray(), ["/secret"], context, token);
            await env.Reservations.AcquireCompletionAsync(write.Completion!, token);
            await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
            CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
        }
        var indexes = new PayloadKeyIndexes(fixture.Store);
        ScopeKeyIndexSnapshot scope = await indexes.ReadScopeAsync(env.Scope, token);
        scope.Head!.Value.EntryCount.ShouldBe(257u); scope.Pages.Count.ShouldBe(2); scope.Pages[1].Value.Entries.Length.ShouldBe(1);
        KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(env.Backend.BackendId), PayloadProtectionStateKeys.Digest(env.Backend.OldVersion), token);
        inventory.Head!.Value.EntryCount.ShouldBe(257ul); inventory.Pages.Count.ShouldBe(2);
        foreach (ScopeIndexEntryV1 entry in scope.Pages.SelectMany(p => p.Value.Entries))
        {
            var key = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(env.Scope, entry.KeyRef, entry.DekVersion), token))!.Value;
            key.State.ShouldBe("Active"); PayloadKeyIndexes.VerifyMembership(key, scope, inventory);
        }
        // Fresh registered incarnations retain their append-only ordinals; no low permanent-member cap is imposed.
        for (int i = 1; i < 257; i++)
        {
            var incarnation = new PayloadProtectionLeaseCoordinator(fixture.Store, env.Roots, env.Profile);
            await incarnation.RegisterAsync(env.Identity, token);
        }
        var members = await new PayloadProtectionCatalogs(fixture.Store).ReadMembersAsync(env.Root, token);
        members.Head.Value.EntryCount.ShouldBe(257u); members.Pages.Count.ShouldBe(2);
        members.Pages[1].Value.Entries.Single().Ordinal.ShouldBe(257u);
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }
}
