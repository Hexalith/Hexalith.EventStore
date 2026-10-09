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
            .Returns(_ => new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence() with { ExpiresAt = _now })), "json"));

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
            .Returns(_ => new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence() with { PolicyId = "" })), "json"));
        (await reader.ReadAsync(_principal, Request())).Stream.ShouldBeNull();
    }

    /// <summary>Verifies expiry Crossed During Custody Recheck Does Not Return Earlier Readable History.</summary>
    [Fact]
    public async Task ExpiryCrossedDuringCustodyRecheckDoesNotReturnEarlierReadableHistory()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence() with { ExpiresAt = _now.AddSeconds(30) })), "json"));
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
            .Returns(_ => new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(payload), "json"));

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

    /// <summary>Bounds every admission and custody await without relying on provider cancellation.</summary>
    /// <param name="stage">The initial/final admission, unprotect, or initial/final lifecycle read.</param>
    /// <param name="expireDeadline">Whether the actual thirty-second SDK timer expires.</param>
    [Theory]
    [InlineData("initial-admission", false)]
    [InlineData("final-admission", false)]
    [InlineData("unprotect", false)]
    [InlineData("initial-custody", false)]
    [InlineData("final-custody", false)]
    [InlineData("initial-admission", true)]
    [InlineData("final-admission", true)]
    [InlineData("unprotect", true)]
    [InlineData("initial-custody", true)]
    [InlineData("final-custody", true)]
    public async Task NoncooperativeProviderStopsBeforeCompletionAndNeverResumesSourceWork(string stage, bool expireDeadline)
    {
        TimeSpan watchdog = TimeSpan.FromSeconds(2);
        Arrange();
        ArrangeCompleteSource();
        var clock = new RetainedHistoryTimeProvider(_now);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var admission = new TaskCompletionSource<RetainedIdentityHistoryGrant?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unprotect = new TaskCompletionSource<PayloadProtectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var custody = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readable = new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence())), "json");
        int admissionCount = 0;
        int custodyCount = 0;
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                admissionCount++;
                if ((stage == "initial-admission" && admissionCount == 1) || (stage == "final-admission" && admissionCount == 2))
                {
                    started.TrySetResult();
                    return admission.Task;
                }

                return Task.FromResult<RetainedIdentityHistoryGrant?>(Grant());
            });
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (stage == "unprotect")
                {
                    started.TrySetResult();
                    return unprotect.Task;
                }

                return Task.FromResult(readable);
            });
        _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                custodyCount++;
                if ((stage == "initial-custody" && custodyCount == 1) || (stage == "final-custody" && custodyCount == 2))
                {
                    started.TrySetResult();
                    return custody.Task;
                }

                return Task.FromResult(true);
            });
        Task pending = stage switch
        {
            "initial-admission" or "final-admission" => admission.Task,
            "unprotect" => unprotect.Task,
            _ => custody.Task,
        };
        using var cancellation = new CancellationTokenSource();
        var reader = new RetainedIdentityHistorySourceReader(_actors, _admission, _custody, clock);
        Task<RetainedIdentityHistoryReadResult> reading = reader.ReadAsync(_principal, Request(), cancellation.Token);
        await started.Task.WaitAsync(watchdog);
        int sourceCalls = _actor.ReceivedCalls().Count();
        int providerCalls = _custody.ReceivedCalls().Count() + _admission.ReceivedCalls().Count();
        try
        {
            pending.IsCompleted.ShouldBeFalse();
            reading.IsCompleted.ShouldBeFalse();
            clock.LastDueTime.ShouldBe(TimeSpan.FromSeconds(30));
            if (expireDeadline)
            {
                clock.Advance(TimeSpan.FromSeconds(29));
                reading.IsCompleted.ShouldBeFalse();
                clock.Advance(TimeSpan.FromSeconds(1));
                RetainedIdentityHistoryReadResult result = await reading.WaitAsync(watchdog);
                result.Stream.ShouldBeNull();
                result.IsAuthoritative.ShouldBeFalse();
                result.FailureReason.ShouldBe("history-time-bound-exceeded");
            }
            else
            {
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(watchdog));
            }

            pending.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            admission.TrySetResult(Grant());
            unprotect.TrySetResult(readable);
            custody.TrySetResult(true);
        }

        await pending.WaitAsync(watchdog);
        _actor.ReceivedCalls().Count().ShouldBe(sourceCalls);
        (_custody.ReceivedCalls().Count() + _admission.ReceivedCalls().Count()).ShouldBe(providerCalls);
        if (stage == "initial-admission")
        {
            _actors.DidNotReceive().CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>());
        }
    }

    /// <summary>Protection output cannot legitimize unsupported stored metadata, even at excluded profile positions.</summary>
    [Theory]
    [InlineData("redacted", false)]
    [InlineData("redacted", true)]
    [InlineData("metadata-version", false)]
    [InlineData("metadata-version", true)]
    [InlineData("event-contract", false)]
    [InlineData("event-contract", true)]
    [InlineData("payload-version-out-of-range", false)]
    [InlineData("payload-version-out-of-range", true)]
    public async Task UnsupportedStoredMetadata_DeniesBeforeCustody(string corruption, bool excludedProfile)
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        EventEnvelope history = Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history");
        EventEnvelope profile = Stored(1, "Profile", "sealed");
        EventEnvelope changed = excludedProfile ? profile : history;
        changed = corruption switch
        {
            "redacted" => changed with { SerializationFormat = "json-redacted" },
            "metadata-version" => changed with { MetadataVersion = 2 },
            "event-contract" => changed with { EventContractType = "future-event" },
            _ => changed with { PayloadVersion = 1025 },
        };
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns(excludedProfile ? [changed, history] : [profile, changed]);

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken);

        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe("history-source-metadata-unsupported");
        await _custody.DidNotReceiveWithAnyArgs().UnprotectEventAsync(default!, default!, default!, default!, default);
    }

    /// <summary>Custody receives a copy so a mutating provider cannot overwrite sealed source bytes.</summary>
    [Fact]
    public async Task CustodyMutation_CannotOverwriteStoredSourcePayload()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        EventEnvelope history = Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history");
        byte[] original = history.Payload.ToArray();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), history]);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Array.Fill(call.Arg<byte[]>(), (byte)0);
                return new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence())), "json");
            });

        (await reader.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeTrue();

        history.Payload.ShouldBe(original);
    }

    /// <summary>Actual source reads cannot skip an expired predecessor to release a live successor without new lifecycle proof.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredPredecessor_BlocksSuccessorInsteadOfFabricatingExclusion(bool destroyed)
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        _actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 3));
        EventEnvelope predecessor = Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "expired-predecessor");
        EventEnvelope successor = Stored(3, typeof(HistoryCustodyProbeEvent).FullName!, "live-successor");
        _actor.ReadEventsRangeAsync(0, 3, 100).Returns([Stored(1, "Profile", "sealed"), predecessor, successor]);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == "expired-predecessor"), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => destroyed ? throw new InvalidOperationException("synthetic destroyed retention unit")
                : new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence() with { PolicyId = "party-actor-retention-v1", ExpiresAt = _now })), "json"));

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken);

        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe(destroyed ? "history-unavailable" : "history-custody-unavailable-or-expired");
        await _custody.DidNotReceive().UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
            Arg.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == "live-successor"), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Encoding.UTF8.GetString(predecessor.Payload).ShouldBe("expired-predecessor");
        Encoding.UTF8.GetString(successor.Payload).ShouldBe("live-successor");
    }

    /// <summary>Restarted source readers consult current independent lifecycle before releasing restored old-key history.</summary>
    [Fact]
    public async Task RestoredCiphertextAndReadableOldKey_CurrentLifecycleDenialPreventsResurrection()
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        ArrangeCompleteSource();
        (await reader.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeTrue();
        _custody.ClearReceivedCalls();
        _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>()).Returns(false);
        var restarted = new RetainedIdentityHistorySourceReader(_actors, _admission, _custody, _clock);

        RetainedIdentityHistoryReadResult result = await restarted.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken);

        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe("history-custody-unavailable-or-expired");
        await _custody.Received(1).CanReadAsync(_identity, Evidence(), Arg.Any<CancellationToken>());
        JsonSerializer.Serialize(result).ShouldNotContain("provider-evidence");
    }

    /// <summary>Restoring exact sealed bytes never restores absent, stale or unavailable independent lifecycle authority.</summary>
    [Theory]
    [InlineData("unavailable")]
    [InlineData("missing-receipt")]
    [InlineData("changed-receipt")]
    [InlineData("changed-revision")]
    [InlineData("source-expiry")]
    [InlineData("restore")]
    [InlineData("copies")]
    public async Task RestoredSealedSource_RequiresCurrentExactLifecycleEvidence(string fault)
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        EventEnvelope[] stored = [Stored(1, "Profile", "sealed-profile"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")];
        byte[] backup = JsonSerializer.SerializeToUtf8Bytes(stored);
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns(stored);
        (await reader.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeTrue();
        _custody.ClearReceivedCalls();

        // This is a serialized source-copy exercise, with synthetic independent lifecycle.
        // No production key, backup or destruction receipt is claimed.
        EventEnvelope[] restored = JsonSerializer.Deserialize<EventEnvelope[]>(backup)!;
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns(restored);
        IdentityHistoryCustodyEvidence original = Evidence();
        IdentityHistoryCustodyEvidence copied = fault switch
        {
            "changed-receipt" => original with { EvidenceId = "restored-unrecognized-receipt" },
            "changed-revision" => original with { LifecycleRevision = original.LifecycleRevision + 1 },
            "source-expiry" => original with { SourceExpiryEnforced = false },
            "restore" => original with { RestoreSafe = false },
            "copies" => original with { DerivedCopiesCovered = false },
            _ => original,
        };
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(copied)), "json"));
        _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(call => fault == "unavailable" ? throw new IOException("synthetic lifecycle outage")
                : fault != "missing-receipt" && call.Arg<AggregateIdentity>() == _identity
                    && call.Arg<IdentityHistoryCustodyEvidence>() == original);
        var restarted = new RetainedIdentityHistorySourceReader(_actors, _admission, _custody, _clock);

        RetainedIdentityHistoryReadResult result = await restarted.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken);

        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe(fault == "unavailable" ? "history-unavailable" : "history-custody-unavailable-or-expired");
        JsonSerializer.Serialize(result).ShouldNotContain("restored-unrecognized-receipt");
        JsonSerializer.SerializeToUtf8Bytes(restored).ShouldBe(backup);
        if (fault is "source-expiry" or "restore" or "copies")
        {
            await _custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
        }
        else
        {
            await _custody.Received(1).CanReadAsync(_identity, copied, Arg.Any<CancellationToken>());
        }
    }

    /// <summary>Restored decoded evidence stays fixed while independent synthetic authority advances its receipt or revision.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoredOriginalEvidence_ChangedIndependentLifecycleDeniesRelease(bool changeRevision)
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        EventEnvelope[] stored = [Stored(1, "Profile", "sealed-profile"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")];
        byte[] backup = JsonSerializer.SerializeToUtf8Bytes(stored);
        IdentityHistoryCustodyEvidence decoded = Evidence();
        IdentityHistoryCustodyEvidence accepted = decoded;
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns(stored);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(decoded)), "json"));
        _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<AggregateIdentity>() == _identity && call.Arg<IdentityHistoryCustodyEvidence>() == accepted);
        (await reader.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeTrue();
        _custody.ClearReceivedCalls();

        EventEnvelope[] restored = JsonSerializer.Deserialize<EventEnvelope[]>(backup)!;
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns(restored);
        accepted = changeRevision ? accepted with { LifecycleRevision = accepted.LifecycleRevision + 1 }
            : accepted with { EvidenceId = "independent-successor-receipt" };
        var restarted = new RetainedIdentityHistorySourceReader(_actors, _admission, _custody, _clock);

        RetainedIdentityHistoryReadResult result = await restarted.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken);

        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe("history-custody-unavailable-or-expired");
        await _custody.Received(1).CanReadAsync(_identity, decoded, Arg.Any<CancellationToken>());
        JsonSerializer.SerializeToUtf8Bytes(restored).ShouldBe(backup);
        JsonSerializer.Serialize(result).ShouldNotContain(accepted.EvidenceId);
    }

    /// <summary>Custody transfers one transient buffer; closed history survives clearing on every completed path.</summary>
    [Theory]
    [InlineData("success")]
    [InlineData("parse")]
    [InlineData("validation")]
    public async Task OwnedReadableBufferIsClearedAfterCompletedUse(string outcome)
    {
        RetainedIdentityHistorySourceReader reader = Arrange();
        ArrangeCompleteSource();
        byte[] bytes = outcome == "parse" ? Encoding.UTF8.GetBytes("invalid-json")
            : JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(outcome == "validation"
                ? Evidence() with { RestoreSafe = false } : Evidence()));
        byte[] exact = bytes.ToArray();
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult(bytes, "json"));

        RetainedIdentityHistoryReadResult result = await reader.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken);

        bytes.ShouldBe(new byte[bytes.Length]);
        if (outcome == "success")
        {
            result.IsAuthoritative.ShouldBeTrue();
            result.Stream!.Events.Single().Payload.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence()), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            exact.ShouldNotBe(new byte[exact.Length]);
        }
        else
        {
            result.Stream.ShouldBeNull();
        }
    }

    /// <summary>Abandonment does not touch a live provider's input and clears only its eventual transferred result.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbandonedCustodyResultIsClearedAtProviderCompletion(bool deadlineExpires)
    {
        Arrange();
        ArrangeCompleteSource();
        var clock = new RetainedHistoryTimeProvider(_now);
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<PayloadProtectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] readable = JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence()));
        byte[] exactReadable = readable.ToArray();
        byte[]? providerInput = null;
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => { providerInput = call.Arg<byte[]>(); entered.SetResult(); return completed.Task; });
        var reader = new RetainedIdentityHistorySourceReader(_actors, _admission, _custody, clock);
        Task<RetainedIdentityHistoryReadResult> reading = reader.ReadAsync(_principal, Request(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            if (deadlineExpires)
            {
                clock.Advance(TimeSpan.FromSeconds(30));
                (await reading.WaitAsync(TimeSpan.FromSeconds(2))).FailureReason.ShouldBe("history-time-bound-exceeded");
            }
            else
            {
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2)));
            }
            completed.Task.IsCompleted.ShouldBeFalse();
            Encoding.UTF8.GetString(providerInput!).ShouldBe("sealed-history");
            readable.ShouldBe(exactReadable);
        }
        finally { completed.TrySetResult(new PayloadProtectionResult(readable, "json")); }

        await completed.Task;
        DateTime until = DateTime.UtcNow.AddSeconds(2);
        while (readable.Any(value => value != 0) && DateTime.UtcNow < until) { await Task.Yield(); }
        readable.ShouldBe(new byte[readable.Length]);
        Encoding.UTF8.GetString(providerInput!).ShouldBe("sealed-history");
        await _custody.DidNotReceiveWithAnyArgs().CanReadAsync(default!, default!, default);
        await _custody.Received(1).UnprotectEventAsync(_identity, typeof(HistoryCustodyProbeEvent).FullName!, Arg.Any<byte[]>(), "json", Arg.Any<CancellationToken>());
        await _actor.Received(1).GetStreamMetadataAsync();
    }

    /// <summary>Actual closed serialized arrays remain owned until success; every later refusal clears them without touching stored ciphertext.</summary>
    [Theory]
    [InlineData("authority")][InlineData("custody")][InlineData("source")][InlineData("expiry")][InlineData("cancellation")][InlineData("success")]
    public async Task ClosedSerializedPayloadTransfersOnlyWithSuccessfulStream(string vector)
    {
        Arrange(); var clock = new RetainedHistoryTimeProvider(_now); using var caller = new CancellationTokenSource();
        EventEnvelope[] stored = [Stored(1, "Profile", "sealed-profile"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")];
        byte[][] originals = stored.Select(e => e.Payload.ToArray()).ToArray();
        _actor.ReadEventsRangeAsync(0, 2, 100).Returns(stored);
        int admissions = 0; int reads = 0; byte[]? closed = null; byte[]? probe = null;
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (++admissions == 2)
            {
                closed.ShouldNotBeNull(); closed.Any(b => b != 0).ShouldBeTrue();
                if (vector == "authority") { return Grant() with { AuthorityRevision = "withdrawn" }; }
                if (vector == "expiry") { clock.Advance(TimeSpan.FromDays(1)); }
                if (vector == "cancellation") { caller.Cancel(); }
            }
            return Grant();
        });
        _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++reads == 1 || vector != "custody");
        _actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 2), new AggregateStreamMetadata(true, vector == "source" ? 3 : 2));
        var reader = new RetainedIdentityHistorySourceReader(_actors, _admission, _custody, clock, bytes => { if (closed is null) { closed = bytes; } else { probe = bytes; } });
        if (vector == "cancellation")
        {
            var error = await Should.ThrowAsync<OperationCanceledException>(() => reader.ReadAsync(_principal, Request(), caller.Token));
            error.CancellationToken.ShouldBe(caller.Token);
        }
        else
        {
            var result = await reader.ReadAsync(_principal, Request(), caller.Token);
            if (vector == "success")
            {
                result.IsAuthoritative.ShouldBeTrue(); result.Stream!.Events.Single().Payload.ShouldBeSameAs(closed);
                probe.ShouldNotBeNull(); probe.All(b => b == 0).ShouldBeTrue();
                closed.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence()), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }
            else { result.Stream.ShouldBeNull(); }
        }
        closed.ShouldNotBeNull(); if (vector != "success") { closed.All(b => b == 0).ShouldBeTrue(); }
        for (int n = 0; n < stored.Length; n++) { stored[n].Payload.ShouldBe(originals[n]); }
    }

    /// <summary>Initial and final independently returned grant collections cannot retain the whole read beyond its original budget.</summary>
    /// <param name="phase">The initial or final grant.</param>
    /// <param name="field">The admitted types or excluded names.</param>
    /// <param name="access">The actually suspended collection operation.</param>
    /// <param name="expires">Whether the thirty-second operation expires instead of caller cancellation.</param>
    /// <param name="invalid">Whether the released carrier is invalid.</param>
    [Theory]
    [InlineData("initial", "types", "count", false, false)][InlineData("initial", "types", "count", true, false)]
    [InlineData("initial", "types", "traversal", false, false)][InlineData("initial", "types", "traversal", true, false)]
    [InlineData("initial", "excluded", "count", false, false)][InlineData("initial", "excluded", "count", true, false)]
    [InlineData("initial", "excluded", "traversal", false, false)][InlineData("initial", "excluded", "traversal", true, false)]
    [InlineData("final", "types", "count", false, false)][InlineData("final", "types", "count", true, false)]
    [InlineData("final", "types", "traversal", false, false)][InlineData("final", "types", "traversal", true, false)]
    [InlineData("final", "excluded", "count", false, false)][InlineData("final", "excluded", "count", true, false)]
    [InlineData("final", "excluded", "traversal", false, false)][InlineData("final", "excluded", "traversal", true, false)]
    [InlineData("initial", "types", "count", false, true)][InlineData("final", "excluded", "traversal", false, true)]
    public async Task SuspendedGrantCaptureCannotReturnHistoryOrContinueAfterAbandonment(string phase, string field, string access, bool expires, bool invalid)
    {
        Arrange(); ArrangeCompleteSource();
        var clock = new RetainedHistoryTimeProvider(_now);
        using var caller = new CancellationTokenSource(); using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Block() { entered.TrySetResult(); release.Wait(); finished.TrySetResult(); }
        Type[] types = [invalid ? null! : typeof(HistoryCustodyProbeEvent)];
        string[] names = [invalid ? string.Empty : "Profile", "ErasedProfile"];
        var suppliedTypes = Substitute.For<IReadOnlyList<Type>>();
        var suppliedNames = Substitute.For<IReadOnlyList<string>>();
        suppliedTypes.Count.Returns(_ => { if (field == "types" && access == "count") { Block(); } return types.Length; });
        suppliedNames.Count.Returns(_ => { if (field == "excluded" && access == "count") { Block(); } return names.Length; });
        suppliedTypes.GetEnumerator().Returns(_ => { if (field == "types" && access == "traversal") { Block(); } return ((IEnumerable<Type>)types).GetEnumerator(); });
        suppliedNames.GetEnumerator().Returns(_ => { if (field == "excluded" && access == "traversal") { Block(); } return ((IEnumerable<string>)names).GetEnumerator(); });
        var supplied = Grant() with { EventTypes = suppliedTypes, ExcludedEventTypeNames = suppliedNames };
        int admissions = 0; var owned = new List<byte[]>();
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++admissions == (phase == "initial" ? 1 : 2) ? supplied : Grant());
        var reader = new RetainedIdentityHistorySourceReader(_actors, _admission, _custody, clock, owned.Add);
        var reading = reader.ReadAsync(_principal, Request(), caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        int sourceCalls = _actor.ReceivedCalls().Count(); int providerCalls = _custody.ReceivedCalls().Count();
        try
        {
            if (expires)
            {
                clock.Advance(TimeSpan.FromSeconds(30));
                var result = await reading.WaitAsync(TimeSpan.FromSeconds(2));
                result.Stream.ShouldBeNull(); result.FailureReason.ShouldBe("history-time-bound-exceeded");
            }
            else
            {
                caller.Cancel();
                var error = await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2)));
                error.CancellationToken.ShouldBe(caller.Token);
            }
            if (phase == "final") { owned.ShouldNotBeEmpty(); owned.All(bytes => bytes.All(value => value == 0)).ShouldBeTrue(); }
        }
        finally { release.Set(); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _actor.ReceivedCalls().Count().ShouldBe(sourceCalls); _custody.ReceivedCalls().Count().ShouldBe(providerCalls);
        if (phase == "initial") { _actors.DidNotReceive().CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>()); }
    }

    /// <summary>Only detached admitted collections are used after the independently owned originals change.</summary>
    [Fact]
    public async Task GrantTypeAndExclusionSnapshotsRemainExactAcrossSourceAwaits()
    {
        Arrange(); ArrangeCompleteSource();
        Type[] types = [typeof(HistoryCustodyProbeEvent)]; string[] excluded = ["Profile", "ErasedProfile"];
        var first = Grant() with { EventTypes = types, ExcludedEventTypeNames = excluded };
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>()).Returns(first, Grant());
        _actor.GetStreamMetadataAsync().Returns(_ => { types[0] = null!; excluded[0] = "changed-provider-name"; return new AggregateStreamMetadata(true, 2); });
        var reader = new RetainedIdentityHistorySourceReader(_actors, _admission, _custody, _clock);
        var result = await reader.ReadAsync(_principal, Request(), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeTrue(); result.Stream!.ExcludedSequences.ShouldBe([1L]);
        result.Stream.Events.Single().EventTypeName.ShouldBe(typeof(HistoryCustodyProbeEvent).FullName);
    }

    private void ArrangeCompleteSource()
        => _actor.ReadEventsRangeAsync(0, 2, 100).Returns([Stored(1, "Profile", "sealed"), Stored(2, typeof(HistoryCustodyProbeEvent).FullName!, "sealed-history")]);

    private RetainedIdentityHistorySourceReader Arrange()
    {
        _clock.GetUtcNow().Returns(_now);
        _clock.GetTimestamp().Returns(_ => System.Diagnostics.Stopwatch.GetTimestamp());
        _clock.TimestampFrequency.Returns(System.Diagnostics.Stopwatch.Frequency);
        _clock.CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>())
            .Returns(call => TimeProvider.System.CreateTimer(call.ArgAt<TimerCallback>(0), call.ArgAt<object?>(1),
                call.ArgAt<TimeSpan>(2), call.ArgAt<TimeSpan>(3)));
        _admission.AdmitAsync(_principal, Arg.Any<RetainedIdentityHistoryReadRequest>(), Arg.Any<CancellationToken>()).Returns(Grant());
        _actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(_actor);
        _actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 2));
        _custody.CanReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>()).Returns(true);
        _custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new PayloadProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new HistoryCustodyProbeEvent(Evidence())), "json"));
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
