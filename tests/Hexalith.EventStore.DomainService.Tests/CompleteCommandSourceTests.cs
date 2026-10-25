using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class CompleteCommandSourceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void CompleteNativePrefixVerifiesLocallyAcrossWireRoundTrip(int count)
    {
        var request = Request(count);
        var principal = Principal(request);
        var wire = JsonSerializer.Deserialize<DomainServiceRequest>(JsonSerializer.SerializeToUtf8Bytes(request, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
        var source = new CompleteCommandSourceVerifier(TimeProvider.System).Verify(wire, principal, "eventstore");
        source.Events.Count.ShouldBe(count); source.Proof.HeadSequence.ShouldBe(count);
        source.Proof.ShouldBe(request.CompleteSource!.Proof);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("partial")]
    [InlineData("foreign")]
    [InlineData("expired")]
    [InlineData("snapshot")]
    [InlineData("gap")]
    [InlineData("proof")]
    [InlineData("versioned")]
    [InlineData("unbound")]
    [InlineData("changed-command")]
    public void InvalidOrUnboundPrefixDeniesBeforeDomainWork(string change)
    {
        DomainServiceRequest request = Request(2);
        var originalPrincipal = Principal(request);
        var source = request.CompleteSource!;
        request = change switch
        {
            "missing" => request with { CompleteSource = null },
            "partial" => request with { CompleteSource = source with { Events = source.Events.Take(1).ToArray() } },
            "foreign" => request with { CompleteSource = source with { Identity = new("foreign", "conversation", "source-1") } },
            "expired" => request with { CompleteSource = source with { ValidUntil = DateTimeOffset.UtcNow.AddSeconds(-1) } },
            "snapshot" => request with { CurrentState = ((DomainServiceCurrentState)request.CurrentState!) with { SnapshotState = new { Fake = true }, LastSnapshotSequence = 1 } },
            "gap" => request with { CompleteSource = source with { Events = [source.Events[0], source.Events[1] with { SequenceNumber = 3 }] } },
            "proof" => request with { CompleteSource = source with { Proof = source.Proof with { CumulativeDigest = new string('b', 64) } } },
            "versioned" => request with { VerifiedEffectiveEvents = [] },
            "changed-command" => request with { Command = request.Command with { Payload = "{\"changed\":true}"u8.ToArray() } },
            _ => request,
        };
        var principal = change == "unbound" ? new ClaimsPrincipal(new ClaimsIdentity([], EventStoreWorkloadAuthenticationDefaults.WorkloadScheme))
            : change == "changed-command" ? originalPrincipal : Principal(request);
        Should.Throw<Exception>(() => new CompleteCommandSourceVerifier(TimeProvider.System).Verify(request, principal, "eventstore"));
    }

    private static DomainServiceRequest Request(int count)
    {
        var identity = new AggregateIdentity("tenant-a", "conversation", "source-1");
        var native = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var sourceEvents = new List<StreamReadEvent>(); var events = new List<EventEnvelope>();
        for (int index = 0; index < count; index++)
        {
            string message = "event-" + index; string digest = new('a', 64);
            native = NativeSourceProofCodec.ExtendDigest(native, identity, index + 1, message, digest);
            var item = new StreamReadEvent(index + 1, "ConversationCreated", "{}"u8.ToArray(), "json", 1, message, "correlation", "cause", DateTimeOffset.UtcNow, "worker") { NativeEventDigest = digest };
            sourceEvents.Add(item);
            events.Add(new(new(message, identity.AggregateId, "Conversation", identity.TenantId, identity.Domain, index + 1, 0,
                item.Timestamp, "correlation", "cause", "worker", "v1", item.EventTypeName, 1, "json"), item.Payload, null));
        }
        var now = DateTimeOffset.UtcNow;
        return new(new("command-1", identity.TenantId, identity.Domain, identity.AggregateId, "Create", "{}"u8.ToArray(), "correlation", null, "worker", null),
            new DomainServiceCurrentState(null, events, 0, count))
        { CompleteSource = new(identity, "epoch-1", now, now.AddMinutes(1), native, sourceEvents) };
    }
    private static ClaimsPrincipal Principal(DomainServiceRequest request)
    {
        var claims = new List<Claim> { new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"),
            new(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceProcess) };
        if (request.CompleteSource is not null) claims.AddRange(CommandSourceAuthorityBinding.Bind(request).Select(pair => new Claim(pair.Key, pair.Value)));
        return new(new ClaimsIdentity(claims, EventStoreWorkloadAuthenticationDefaults.WorkloadScheme));
    }
}
