using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual candidate registry/state manager; synthetic independent epoch/antirollback/root/writer authority.</summary>
internal sealed class InteractionOccurrenceFixture
{
    internal InMemoryStateManager Backend { get; } = new();
    internal IInteractionOccurrenceAuthority Authority { get; } = Substitute.For<IInteractionOccurrenceAuthority>();
    internal long Head { get; set; }
    internal string Epoch { get; set; } = "epoch-1";
    internal string AnchorDigest { get; set; } = Digest(new("tenant-a", "epoch-1", 0, []));
    internal InteractionOccurrenceRegistryActor Actor { get; }
    internal bool JournalAvailable { get; set; } = true;
    internal InteractionOccurrenceFixture()
    {
        Authority.AuthorizeOperationAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.GetInstalledEpochAsync("tenant-a", Arg.Any<CancellationToken>()).Returns(_ => Epoch);
        Authority.ValidateStateAsync("tenant-a", Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<string>(0) == "tenant-a" && call.ArgAt<string>(1) == Epoch && call.Arg<long>() == Head && call.ArgAt<string>(3) == AnchorDigest);
        Authority.AuthorizeReservationAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifySealedAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyWriterAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        var admissions = new Dictionary<string, string>(StringComparer.Ordinal);
        Authority.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>(); string exact = JsonSerializer.Serialize(t);
            if (admissions.TryGetValue(t.TargetDigest, out var admitted)) { return admitted == exact; }
            if (t.ScopeId != InteractionOccurrenceRegistryActor.GetActorId("tenant-a") + "|candidate-interaction-occurrences-v1" || t.ExpectedRevision != Head || t.TargetRevision != Head + 1 || t.PredecessorDigest != AnchorDigest) { return false; }
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
            if (t.ScopeId != InteractionOccurrenceRegistryActor.GetActorId("tenant-a") + "|candidate-interaction-occurrences-v1") { return false; }
            if (transitions.TryGetValue(t.TargetDigest, out var original)) { return JsonSerializer.Serialize(original) == JsonSerializer.Serialize(t); }
            if (t.ExpectedRevision != Head || t.TargetRevision != Head + 1 || t.PredecessorDigest != AnchorDigest) { return false; }
            Head++; AnchorDigest = t.TargetDigest; transitions.Add(t.TargetDigest, t with { TargetBytes = t.TargetBytes.ToArray() }); return true;
        });
        Authority.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call => {
            var t = call.Arg<AnchoredStateTransition>(); return transitions.TryGetValue(t.TargetDigest, out var original) && JsonSerializer.Serialize(original) == JsonSerializer.Serialize(t);
        });
        Authority.RecordRevisionAsync("tenant-a", Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<string>(1) != Epoch || call.ArgAt<long>(2) != Head || call.ArgAt<long>(3) != Head + 1) { return false; }
            Head++; AnchorDigest = call.ArgAt<string>(4); return true;
        }); Actor = Create(Backend, Authority);
    }
    internal static InteractionOccurrenceRegistryActor Create(IActorStateManager backend, IInteractionOccurrenceAuthority? authority = null)
    {
        var actor = new InteractionOccurrenceRegistryActor(ActorHost.CreateForTest<InteractionOccurrenceRegistryActor>(new ActorTestOptions { ActorId = new(InteractionOccurrenceRegistryActor.GetActorId("tenant-a")) }), authority);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, backend); return actor;
    }
    internal static string Digest(InteractionOccurrenceRegistrySnapshot state) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    internal static InteractionOccurrenceRequest Request(ulong sequence = 1, string reference = "01ARZ3NDEKTSV4RRFFQ69G5FAV") => new(new(new("tenant-a", "interaction-a", "alias-a"),
        new AggregateIdentity("tenant-a", "conversations", "directory-1"), PayloadProtectionPayloadKind.Event, sequence, "Event.Type", "root-v1", "candidate-hkdf-sha256-v1"),
        "retained-digest-v1", Convert.ToHexString(HMACSHA256.HashData(new byte[32], Encoding.UTF8.GetBytes("synthetic-intent"))), 1, reference);
    internal static InteractionOccurrenceSealedResult Sealed(string reference = "01ARZ3NDEKTSV4RRFFQ69G5FAV") => new(reference, "{\"ProtectedContent\":{\"$pdenc\":\"synthetic-owner-verified-ciphertext\"}}"u8.ToArray(), "json+pdenc-v2", 1);

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
