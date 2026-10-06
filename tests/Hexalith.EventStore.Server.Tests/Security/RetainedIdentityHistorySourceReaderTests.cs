using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

/// <summary>Verifies retained Identity History Source Reader Tests.</summary>
public sealed class RetainedIdentityHistorySourceReaderTests
{
    private readonly AggregateIdentity _identity = new("tenant-a", "party", "party-a");
    private readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-10-06T12:00:00Z");
    private readonly IActorProxyFactory _actors = Substitute.For<IActorProxyFactory>();
    private readonly IAggregateActor _actor = Substitute.For<IAggregateActor>();
    private readonly IRetainedIdentityHistoryAdmission _admission = Substitute.For<IRetainedIdentityHistoryAdmission>();
    private readonly IIdentityHistoryCustody _custody = Substitute.For<IIdentityHistoryCustody>();
    private readonly TimeProvider _clock = Substitute.For<TimeProvider>();
    private readonly ClaimsPrincipal _principal = new(new ClaimsIdentity("internal-history"));

    /// <summary>Verifies erased Profile Remains Sealed While History Retains Original Positions.</summary>
    [Fact]
    public async Task ErasedProfileRemainsSealedWhileHistoryRetainsOriginalPositions()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        EventEnvelope profile = Stored(1, "ErasedProfile", "ERASED-PROFILE-CIPHERTEXT");
        EventEnvelope history = Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "HISTORY-CIPHERTEXT");
        EventEnvelope[] persisted = [profile, history];
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns(persisted);

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request());

        result.IsAuthoritative.ShouldBeTrue();
        result.Stream!.Events.Single().SequenceNumber.ShouldBe(2);
        result.Stream.ExcludedSequences.ShouldBe([1L]);
        result.Stream.Events.Single().UserId.ShouldBeNull();
        result.Stream.Events.Single().CorrelationId.ShouldBeNull();
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(result)).ShouldNotContain("ERASED-PROFILE");
        Encoding.UTF8.GetString(persisted[0].Payload).ShouldBe("ERASED-PROFILE-CIPHERTEXT");
        Encoding.UTF8.GetString(persisted[1].Payload).ShouldBe("HISTORY-CIPHERTEXT");
        await _custody.DidNotReceive().UnprotectEventAsync(Arg.Any<AggregateIdentity>(), "ErasedProfile", Arg.Any<byte[]>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies denial Performs No Target Lookup.</summary>
    [Fact]
    public async Task DenialPerformsNoTargetLookup()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>())
            .Returns((RetainedIdentityHistoryGrant?)null);

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request());

        result.Stream.ShouldBeNull();
        _actors.DidNotReceive().CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>());
    }

    /// <summary>Verifies changed Source Head Discards All Readable History.</summary>
    [Fact]
    public async Task ChangedSourceHeadDiscardsAllReadableHistory()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 2), new AggregateStreamMetadata(true, 3));
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request());

        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe("history-source-or-authority-changed");
    }

    /// <summary>Verifies foreign Persisted Identity Does Not Produce An Exclusion Certificate.</summary>
    [Fact]
    public async Task ForeignPersistedIdentityDoesNotProduceAnExclusionCertificate()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed") with { TenantId = "tenant-b" }]);

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request());

        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe("history-source-gap-or-scope-mismatch");
    }

    /// <summary>Verifies missing Original Source Position Does Not Become Authoritative History.</summary>
    [Fact]
    public async Task MissingOriginalSourcePositionDoesNotBecomeAuthoritativeHistory()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);
        (await reader.ReadAsync(_principal, Request())).Stream.ShouldBeNull();
    }

    /// <summary>Verifies expired Custody Discards Retained Events.</summary>
    [Fact]
    public async Task ExpiredCustodyDiscardsRetainedEvents()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence() with { ExpiresAt = _now })), "json"));

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request());

        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe("history-custody-unavailable-or-expired");
    }

    /// <summary>Verifies revoked Read Authority Discards The Whole Result.</summary>
    [Fact]
    public async Task RevokedReadAuthorityDiscardsTheWholeResult()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Grant(), Grant() with { AuthorityRevision = "revoked-successor" });

        (await reader.ReadAsync(_principal, Request())).Stream.ShouldBeNull();
    }

    /// <summary>Verifies custody Without Its Policy Reference Does Not Authorize History.</summary>
    [Fact]
    public async Task CustodyWithoutItsPolicyReferenceDoesNotAuthorizeHistory()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence() with { PolicyId = "" })), "json"));
        (await reader.ReadAsync(_principal, Request())).Stream.ShouldBeNull();
    }

    /// <summary>Verifies expiry Crossed During Custody Recheck Does Not Return Earlier Readable History.</summary>
    [Fact]
    public async Task ExpiryCrossedDuringCustodyRecheckDoesNotReturnEarlierReadableHistory()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence() with { ExpiresAt = _now.AddSeconds(30) })), "json"));
        _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(true), _ => { _clock.GetUtcNow().Returns(_now.AddSeconds(31)); return Task.FromResult(true); });
        (await reader.ReadAsync(_principal, Request())).Stream.ShouldBeNull();
    }

    /// <summary>Verifies plaintext History At Rest Cannot Pass Production Source Reader.</summary>
    [Fact]
    public async Task PlaintextHistoryAtRestCannotPassProductionSourceReader()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        EventEnvelope history = Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "plaintext") with { Extensions = null };
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), history]);
        (await reader.ReadAsync(_principal, Request())).FailureReason.ShouldBe("history-source-protection-missing");
    }

    /// <summary>Verifies undeclared Profile Fields Never Escape In Readable Attribution.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UndeclaredProfileFieldsNeverEscapeInReadableAttribution(bool nested)
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        ArrangeCompleteSource();
        JsonNode payload = JsonSerializer.SerializeToNode(new HistoryCustodyProbeEvent(Evidence()), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        (nested ? payload["custody"]! : payload)["profile"] = "private-profile-content";
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(payload), "json"));

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request());
        result.Stream.ShouldBeNull();
        JsonSerializer.Serialize(result).ShouldNotContain("private-profile-content");
    }

    /// <summary>Verifies unregistered Simple Or Assembly Qualified Names Cannot Become Excluded Profile.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnregisteredSimpleOrAssemblyQualifiedNamesCannotBecomeExcludedProfile(bool assemblyQualified)
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        string alias = assemblyQualified ? typeof(HistoryCustodyProbeEvent).AssemblyQualifiedName! : nameof(HistoryCustodyProbeEvent);
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, alias, "sealed-history")]);
        (await reader.ReadAsync(_principal, Request())).FailureReason.ShouldBe("history-source-contract-unavailable");
    }

    /// <summary>Verifies custody Revocation During Final Authority Read Prevents Release.</summary>
    [Fact]
    public async Task CustodyRevocationDuringFinalAuthorityReadPreventsRelease()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        ArrangeCompleteSource();
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<RetainedIdentityHistoryGrant?>(Grant()), _ =>
            {
                _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>()).Returns(false);
                return Task.FromResult<RetainedIdentityHistoryGrant?>(Grant());
            });
        (await reader.ReadAsync(_principal, Request())).Stream.ShouldBeNull();
    }

    /// <summary>Verifies cancellation During Final Authority Read Cannot Return Success.</summary>
    [Fact]
    public async Task CancellationDuringFinalAuthorityReadCannotReturnSuccess()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        ArrangeCompleteSource();
        using var cancellation = new CancellationTokenSource();
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<RetainedIdentityHistoryGrant?>(Grant()), _ =>
            {
                cancellation.Cancel();
                return Task.FromResult<RetainedIdentityHistoryGrant?>(Grant());
            });
        await Should.ThrowAsync<OperationCanceledException>(() => reader.ReadAsync(_principal, Request(), cancellation.Token));
    }

    /// <summary>Verifies already Cancelled Read Performs No Admission Or Source Lookup.</summary>
    [Fact]
    public async Task AlreadyCancelledReadPerformsNoAdmissionOrSourceLookup()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        await Should.ThrowAsync<OperationCanceledException>(() => reader.ReadAsync(_principal, Request(), new CancellationToken(true)));
        await _admission.DidNotReceive().AdmitAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>());
        _actors.DidNotReceive().CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>());
    }

    private void ArrangeCompleteSource()
        => _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);

    private RetainedIdentityHistorySourceReader Arrange()
    {
        _clock.GetUtcNow().Returns(_now);
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>()).Returns(Grant());
        _actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(_actor);
        _actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 2));
        _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>()).Returns(true);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence())), "json"));
        return new(_actors, _admission, _custody, _clock);
    }

    private RetainedIdentityHistoryGrant Grant()
        => new(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose, "current-authority", _now.AddMinutes(1), [typeof(HistoryCustodyProbeEvent)])
        {
            ExcludedEventTypeNames = ["Profile", "ErasedProfile"],
        };

    private IdentityHistoryCustodyEvidence Evidence()
        => new("accepted-policy", RetainedIdentityHistoryReadRequest.AttributionPurpose, _now.AddDays(1), 1, "provider-evidence", true, true, true);

    private RetainedIdentityHistoryReadRequest Request() => new(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose);

    private EventEnvelope Stored(long sequence, string type, string payload)
        => new("event-id", _identity.AggregateId, "Party", _identity.TenantId, _identity.Domain, sequence, sequence,
            _now.AddMinutes(-1), "private-correlation", "private-causation", "private-subject", "v1", type, 1, "json", Encoding.UTF8.GetBytes(payload),
            EventStorePayloadProtectionMetadataCarrier.Write((IDictionary<string, string>?)null,
                new(PayloadProtectionState.Protected, 1, "history-test", "history-k1", null, null)));
}
