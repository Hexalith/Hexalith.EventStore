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
    internal InteractionOccurrenceFixture()
    {
        Authority.AuthorizeOperationAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.GetInstalledEpochAsync("tenant-a", Arg.Any<CancellationToken>()).Returns(_ => Epoch);
        Authority.ValidateStateAsync("tenant-a", Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<string>(0) == "tenant-a" && call.ArgAt<string>(1) == Epoch && call.Arg<long>() == Head && call.ArgAt<string>(3) == AnchorDigest);
        Authority.AuthorizeReservationAsync(Arg.Any<InteractionOccurrenceRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifySealedAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>()).Returns(true);
        Authority.VerifyWriterAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
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
}
