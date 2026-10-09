using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual sealed source to actor-free certificate path with explicitly synthetic independent lifecycle/receipt authority.</summary>
public sealed class ExpiredIdentityHistorySourceTests
{
    /// <summary>Only exact current terminal receipt releases continuity; predecessor decryption is never attempted.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("foreign", false)]
    [InlineData("wrong-position", false)]
    [InlineData("changed-ciphertext", false)]
    [InlineData("expired", false)]
    [InlineData("missing-receipt", false)]
    [InlineData("changed-terminal-receipt", false)]
    public async Task ExpiredTransitionNeedsExactReconfirmedTerminalProof(string vector, bool available)
    {
        var identity = new AggregateIdentity("tenant-a", "party", "party-a");
        var now = DateTimeOffset.UtcNow;
        string type = typeof(HistoryCustodyProbeEvent).FullName!;
        byte[] ciphertext = Encoding.UTF8.GetBytes("SEALED-EXPIRED-HISTORY");
        var stored = new EventEnvelope("event-id", identity.AggregateId, "Party", identity.TenantId, identity.Domain, 1, 1,
            now.AddMinutes(-1), "private-correlation", "private-causation", "private-subject", "v1", type, 1, "json", ciphertext,
            EventStorePayloadProtectionMetadataCarrier.Write((IDictionary<string, string>?)null,
                new(PayloadProtectionState.Protected, 1, "history-test", "history-k1", null, null)));
        var actor = Substitute.For<IAggregateActor>(); actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 1));
        actor.ReadEventsRangeAsync(0, 1, 100).Returns([stored]);
        var actors = Substitute.For<IActorProxyFactory>(); actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(actor);
        var admission = Substitute.For<IRetainedIdentityHistoryAdmission>();
        admission.AdmitAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RetainedIdentityHistoryGrant(identity, RetainedIdentityHistoryReadRequest.AttributionPurpose,
                "current-authority", now.AddMinutes(1), [typeof(HistoryCustodyProbeEvent)]));
        var custody = Substitute.For<IIdentityHistoryCustody, IExpiredIdentityHistoryCustody>();
        var expired = (IExpiredIdentityHistoryCustody)custody;
        var proof = new ExpiredIdentityHistoryCertificate(identity, RetainedIdentityHistoryReadRequest.AttributionPurpose, "history-policy-v1", 1, type,
            Convert.ToHexString(SHA256.HashData(ciphertext)), "opaque-all-copy-terminal-receipt", 4, "fresh-nonrollback-authority", now, now.AddMinutes(1));
        proof = vector switch
        {
            "foreign" => proof with { Identity = new("tenant-b", "party", "party-a") },
            "wrong-position" => proof with { SourceSequence = 2 },
            "changed-ciphertext" => proof with { SealedPayloadDigest = new string('A', 64) },
            "expired" => proof with { ValidUntil = now },
            "missing-receipt" => proof with { DestructionReceiptId = "" },
            _ => proof,
        };
        int calls = 0;
        expired.ReadExpiredAsync(identity, type, 1, Arg.Any<byte[]>(), "json", Arg.Any<CancellationToken>()).Returns(_ =>
            vector == "changed-terminal-receipt" && ++calls > 1 ? proof with { DestructionReceiptId = "different-terminal-receipt" } : proof);
        var reader = new RetainedIdentityHistorySourceReader(actors, admission, custody, TimeProvider.System);
        var result = await reader.ReadAsync(new(new ClaimsIdentity("internal-history")), new(identity,
            RetainedIdentityHistoryReadRequest.AttributionPurpose), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBe(available);
        await custody.DidNotReceiveWithAnyArgs().UnprotectEventAsync(default!, default!, default!, default!, default);
        stored.Payload.ShouldBe(ciphertext);
        if (available)
        {
            result.Stream!.ExpiredEvents.Single().ShouldBe(proof); result.Stream.Events.ShouldBeEmpty(); result.Stream.ExcludedSequences.ShouldBeEmpty();
            System.Text.Json.JsonSerializer.Serialize(result).ShouldNotContain("SEALED-EXPIRED-HISTORY");
        }
        else { result.Stream.ShouldBeNull(); }
    }
}
