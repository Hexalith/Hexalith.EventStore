using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual SDK boundary actor/persisted-state manager with synthetic independent atomic-writer/namespace/terminal/preservation receipts; no production writer qualification.</summary>
internal sealed class DirectoryMigrationBoundaryFixture
{
    internal InMemoryStateManager Backend { get; } = new();
    internal IDirectoryMigrationBoundaryAuthority Authority { get; } = Substitute.For<IDirectoryMigrationBoundaryAuthority>();
    internal DirectoryMigrationBoundaryActor Actor { get; }
    internal long Anchor { get; set; }
    internal string AnchorDigest { get; set; } = Digest(new DirectoryEpochLedger("tenant-a", 0, null, null, [], [], [], [], []));
    internal bool JournalAvailable { get; set; } = true;
    internal DirectoryMigrationBoundaryFixture()
    {
        Authority.AuthorizeOperationAsync("tenant-a", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyInstallationAsync(Arg.Any<DirectoryEpochInstallation>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyRepairAsync(Arg.Any<DirectoryRepairBoundary>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyDrainAsync(Arg.Any<DirectoryRepairDrainReceipt>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyActivationAsync(Arg.Any<DirectoryEpochActivation>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.ValidateStateAsync("tenant-a", Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.Arg<long>() == Anchor && call.ArgAt<string>(2) == AnchorDigest);
        var admissions = new Dictionary<string, string>(StringComparer.Ordinal);
        Authority.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>(); string exact = JsonSerializer.Serialize(t);
            if (admissions.TryGetValue(t.TargetDigest, out var admitted)) { return admitted == exact; }
            if (t.ScopeId != DirectoryMigrationBoundaryActor.GetActorId("tenant-a") + "|directory-migration-boundary-candidate-v1" || t.ExpectedRevision != Anchor || t.TargetRevision != Anchor + 1 || t.PredecessorDigest != AnchorDigest) { return false; }
            admissions.Add(t.TargetDigest, exact); return true;
        });
        Authority.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>(); return admissions.TryGetValue(t.TargetDigest, out var admitted) && admitted == JsonSerializer.Serialize(t)
                ? Authority.RecordTransitionAsync(t, call.Arg<CancellationToken>()) : Task.FromResult(false);
        });
        var transitions = new Dictionary<string, AnchoredStateTransition>(StringComparer.Ordinal);
        Authority.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>();
            if (!JournalAvailable) { return false; }
            if (t.ScopeId != DirectoryMigrationBoundaryActor.GetActorId("tenant-a") + "|directory-migration-boundary-candidate-v1") { return false; }
            if (transitions.TryGetValue(t.TargetDigest, out var original)) { return JsonSerializer.Serialize(original) == JsonSerializer.Serialize(t); }
            if (t.ExpectedRevision != Anchor || t.TargetRevision != Anchor + 1 || t.PredecessorDigest != AnchorDigest) { return false; }
            Anchor++; AnchorDigest = t.TargetDigest; transitions.Add(t.TargetDigest, t with { TargetBytes = t.TargetBytes.ToArray() }); return true;
        });
        Authority.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>(); return transitions.TryGetValue(t.TargetDigest, out var original) && JsonSerializer.Serialize(original) == JsonSerializer.Serialize(t);
        });
        Authority.RecordRevisionAsync("tenant-a", Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<long>(1) != Anchor || call.ArgAt<long>(2) != Anchor + 1) { return false; }
            Anchor++; AnchorDigest = call.ArgAt<string>(3); return true;
        }); Actor = Create(Backend, Authority);
    }
    internal static DirectoryMigrationBoundaryActor Create(IActorStateManager backend, IDirectoryMigrationBoundaryAuthority? authority = null)
    {
        var actor = new DirectoryMigrationBoundaryActor(ActorHost.CreateForTest<DirectoryMigrationBoundaryActor>(new ActorTestOptions { ActorId = new(DirectoryMigrationBoundaryActor.GetActorId("tenant-a")) }), authority);
        typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, backend); return actor;
    }
    internal DirectoryEpochLedger Persisted => (DirectoryEpochLedger)Backend.CommittedState.Single().Value;
    internal static DirectoryEpochInstallation Installation(string epoch = "epoch-1", long expected = 0) => new("tenant-a", "install-" + epoch, epoch, expected, "independent-legacy-revoked", "independent-all-writers-enforced");
    internal static DirectoryRepairCohortItem Item(string operation = "original-1") => new(new AggregateIdentity("tenant-a", "agents", "directory-a"), operation,
        DirectoryWriteKind.LeaseCommit, "conversation-a", "original-permit", "original-capability", 3, "Committed");
    internal static DirectoryRepairBoundary Repair(long expected = 1) => new("tenant-a", "repair-1", "epoch-1", expected, "complete-post-fence-namespace", "independent-atomic-repair-revocation", [Item(), Item("original-2")]);
    internal static DirectoryRepairDrainReceipt Drain(DirectoryRepairCohortItem item, long expected) => new("tenant-a", "drain-" + item.OperationId, "repair-1", expected, item, "Settled", "authenticated-original-result");
    internal static DirectoryEpochActivation Activation(long expected) => new("tenant-a", "activate-1", "repair-1", expected, Installation("epoch-2", expected), "atomic-old-epoch-fence-bridge-revoked", "full-guard-history-preserved");
    internal static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    internal static IActorStateManager Faulting<T>(InMemoryStateManager backend, int failSave, bool committed)
    {
        var manager = Substitute.For<IActorStateManager>(); int saves = 0;
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<T>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryGetStateAsync<T>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<T>(), Arg.Any<CancellationToken>()).Returns(call => backend.SetStateAsync(call.Arg<string>(), call.Arg<T>(), call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<AnchoredStateTransition>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryGetStateAsync<AnchoredStateTransition>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => backend.SetStateAsync(call.Arg<string>(), call.Arg<AnchoredStateTransition>(), call.Arg<CancellationToken>()));
        manager.TryRemoveStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryRemoveStateAsync(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call => {
            if (++saves != failSave) { await backend.SaveStateAsync(call.Arg<CancellationToken>()); return; }
            if (committed) { await backend.SaveStateAsync(call.Arg<CancellationToken>()); }
            throw new HttpRequestException("Controlled exact pending/main save failure or lost acknowledgement.");
        }); return manager;
    }
}
