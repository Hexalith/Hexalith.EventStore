using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Stale/forged row of the Story 4.11 matrix: callback admission authenticates origin, stored identity,
/// purpose, and stream currency before any submission, and a refused callback mutates and discloses nothing.
/// </summary>
public sealed class ReminderCallbackAdmissionTests
{
    private const string ReminderRoute = "/actors/EventStoreReminderActor/wra-X/method/remind/date-wrs-X";
    private static readonly ReminderTarget Item = ReminderTestHarness.Target("item-1");

    /// <summary>A superseded witness is an audited no-op: nothing is submitted, the reminder is cancelled, and the current witness is armed.</summary>
    [Fact]
    public async Task StaleWitnessIsAuditedNoOpAndCancelled()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent stale = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), revision: 1);
        ReminderIntent current = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(8), revision: 2, sequence: 6);
        harness.Source.Set(Item, stale);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        // The domain rescheduled, but its own convergence call was lost before the stale reminder fired.
        harness.Source.Set(Item, current);
        harness.Time.Advance(TimeSpan.FromHours(1));
        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(stale));

        disposition.ShouldBe(ReminderDisposition.Stale);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.Disposition(actorId, ReminderTestHarness.Name(stale)).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Stale);
        FakeReminderScheduler scheduler = harness.SchedulerFor(actorId);
        scheduler.Cancelled.ShouldContain(ReminderTestHarness.Name(stale));
        scheduler.Armed.Keys.ShouldBe([ReminderTestHarness.Name(current)]);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(ReminderTestHarness.Name(current));
        harness.Candidates().ShouldHaveSingleItem();
    }

    /// <summary>A later schedule for the same logical submission replaces a stale witness without quarantining its effect identity.</summary>
    [Fact]
    public async Task StaleWitnessWithSameSourceCoordinatesArmsReplacement()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent stale = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), revision: 1, sequence: 3);
        ReminderIntent current = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(8), revision: 2, sequence: 3);
        string staleName = ReminderTestHarness.Name(stale);
        string currentName = ReminderTestHarness.Name(current);
        harness.Source.Set(Item, stale);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, current);
        harness.Time.Advance(TimeSpan.FromHours(1));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, staleName);

        disposition.ShouldBe(ReminderDisposition.Stale);
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        state.Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(currentName);
        state.Quarantine.ShouldBeEmpty();
        harness.SchedulerFor(actorId).Armed.Keys.ShouldBe([currentName]);
        harness.SchedulerFor(actorId).Cancelled.ShouldContain(staleName);
        harness.Submitter.Calls.ShouldBeEmpty();

        harness.Time.Advance(TimeSpan.FromHours(7));
        (await harness.FireAsync(actorId, currentName)).ShouldBe(ReminderDisposition.Submitted);
        harness.Submitter.Calls.ShouldHaveSingleItem();
        string effectId = EffectIdentityCodec.ComputeEffectId(new EffectIdentity(
            Item.Tenant, Item.Domain, Item.Aggregate, 3, EffectKindCatalog.DateResume, Item.Domain, Item.Aggregate, 0));
        harness.Submitter.Receipts.ShouldHaveSingleItem().Value.EffectId.ShouldBe(effectId);
        harness.Disposition(actorId, currentName).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Submitted);
        harness.ItemState(actorId).ShouldBeNull();
        harness.Status.Snapshot().Quarantined.ShouldBe(0);
    }

    /// <summary>A callback delivered to another item's actor finds no witness: no submission, no write, no disclosure.</summary>
    [Fact]
    public async Task WrongIdentityCallbackMutatesNothing()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        string otherActorId = ReminderTestHarness.ActorId(ReminderTestHarness.Target("item-2"));
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(2));
        long version = harness.ItemState(actorId).ShouldNotBeNull().Version;
        int keys = harness.Store.Count;
        int reads = harness.Source.Reads;

        ReminderDisposition? wrongActor = await harness.FireAsync(otherActorId, ReminderTestHarness.Name(intent));
        ReminderDisposition? malformed = await harness.FireAsync(actorId, "date-wrs-FORGED");
        ReminderDisposition? foreignName = await harness.FireAsync(actorId, "drain-unpublished-1");

        wrongActor.ShouldBeNull();
        malformed.ShouldBeNull();
        foreignName.ShouldBeNull();
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.Source.Reads.ShouldBe(reads);
        harness.Store.Count.ShouldBe(keys);
        harness.ItemState(actorId).ShouldNotBeNull().Version.ShouldBe(version);
        harness.Disposition(otherActorId, ReminderTestHarness.Name(intent)).ShouldBeNull();
        harness.SchedulerFor(otherActorId).Cancelled.ShouldContain(ReminderTestHarness.Name(intent));
        harness.SchedulerFor(actorId).Cancelled.ShouldContain("date-wrs-FORGED");
        harness.SchedulerFor(actorId).Cancelled.ShouldContain("drain-unpublished-1");
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(ReminderTestHarness.Name(intent));
    }

    /// <summary>A stale witness whose audit record cannot be written is kept rather than silently dropped.</summary>
    [Fact]
    public async Task StaleWitnessWithoutAuditIsKept()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent stale = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), revision: 1);
        harness.Source.Set(Item, stale);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(8), revision: 2, sequence: 6));
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.CoordinatorStore.FailDispositionWrites = true;

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(stale));

        disposition.ShouldBe(ReminderDisposition.Retrying);
        harness.Submitter.Calls.ShouldBeEmpty();
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.ReminderName.ShouldBe(ReminderTestHarness.Name(stale));
        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retained.LastReasonCode.ShouldBe("audit-unavailable");
        retained.Attempts.ShouldBe(1);
        harness.Status.Snapshot().UnresolvedItems.ShouldBe(1);
        harness.Status.Snapshot().Unresolved.ShouldBe(1);
        harness.SchedulerFor(actorId).Cancelled.ShouldNotContain(ReminderTestHarness.Name(stale));
    }

    /// <summary>A stale witness stays retrying until Scheduler cancellation succeeds on a later callback.</summary>
    [Fact]
    public async Task StaleWitnessIsRetainedUntilCancellationRecovers()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent stale = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), revision: 1);
        ReminderIntent current = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(8), revision: 2, sequence: 6);
        string staleName = ReminderTestHarness.Name(stale);
        harness.Source.Set(Item, stale);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, current);
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.SchedulerFor(actorId).CancelFailure = new InvalidOperationException("Synthetic Scheduler outage.");

        ReminderDisposition? retained = await harness.FireAsync(actorId, staleName);

        retained.ShouldBe(ReminderDisposition.Retrying);
        ReminderItemState held = harness.ItemState(actorId).ShouldNotBeNull();
        ReminderEntry retrying = held.Entries.Single(entry => entry.ReminderName == staleName);
        retrying.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retrying.LastReasonCode.ShouldBe("cancel-failed");
        held.Entries.ShouldContain(entry => entry.ReminderName == ReminderTestHarness.Name(current));
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(staleName);
        harness.Candidates().ShouldHaveSingleItem();

        harness.SchedulerFor(actorId).CancelFailure = null;
        ReminderDisposition? recovered = await harness.FireAsync(actorId, staleName);

        recovered.ShouldBe(ReminderDisposition.Stale);
        harness.SchedulerFor(actorId).Cancelled.ShouldContain(staleName);
        harness.SchedulerFor(actorId).Armed.Keys.ShouldBe([ReminderTestHarness.Name(current)]);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(ReminderTestHarness.Name(current));
    }

    /// <summary>A stream that cannot be re-folded during a callback retains the witness and counts the attempt.</summary>
    [Fact]
    public async Task UnreadableStreamDuringCallbackIsRetained()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.Source.Failing.Add(Item);

        ReminderDisposition? disposition = await harness.FireAsync(actorId, name);

        disposition.ShouldBe(ReminderDisposition.Retrying);
        harness.Submitter.Calls.ShouldBeEmpty();
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retained.LastReasonCode.ShouldBe("source-unavailable");
        retained.Attempts.ShouldBe(1);
        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(30));
    }

    /// <summary>Two current intents carrying the fired name with different evidence quarantine the witness before submission.</summary>
    [Fact]
    public async Task SeveralMatchingWitnessesAtCallbackAreQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, intent, intent with { Payload = [7] });
        harness.Time.Advance(TimeSpan.FromHours(1));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        disposition.ShouldBe(ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("witness-collision");
        ReminderDispositionRecord audit = harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Quarantined);
        audit.ReasonCode.ShouldBe("witness-collision");
    }

    /// <summary>A current intent under another name that shares the fired witness's effect identity quarantines it.</summary>
    [Fact]
    public async Task SharedEffectIdentityAtCallbackIsQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), revision: 1, sequence: 3);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, intent, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(3), revision: 2, sequence: 3));
        harness.Time.Advance(TimeSpan.FromHours(1));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        disposition.ShouldBe(ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("effect-collision");
        ReminderDispositionRecord audit = harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Quarantined);
        audit.ReasonCode.ShouldBe("effect-collision");
    }

    /// <summary>A stored tuple that no longer re-derives its actor and name is quarantined and retained, never submitted.</summary>
    [Fact]
    public async Task TamperedStoredTupleIsQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        ReminderItemState stored = harness.ItemState(actorId).ShouldNotBeNull();
        ReminderEntry original = stored.Entries.Single();
        harness.SeedItemState(actorId, stored with { Entries = [original with { DueUtc = original.DueUtc.AddDays(1) }] });
        harness.Time.Advance(TimeSpan.FromHours(1));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, name);

        disposition.ShouldBe(ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        ReminderItemState quarantined = harness.ItemState(actorId).ShouldNotBeNull();
        quarantined.Entries.ShouldBeEmpty();
        ReminderQuarantineRecord evidence = quarantined.Quarantine.ShouldHaveSingleItem();
        evidence.ReasonCode.ShouldBe("stored-entry-invalid");
        evidence.ReminderName.ShouldBe(name);
        ReminderDispositionRecord audit = harness.Disposition(actorId, evidence.EvidenceDigest).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Quarantined);
        audit.ReasonCode.ShouldBe("stored-entry-invalid");
        harness.SchedulerFor(actorId).Armed.ShouldNotContainKey(name);
        harness.Candidates().ShouldHaveSingleItem();
        harness.Status.Snapshot().Quarantined.ShouldBe(1);

        // Quarantine is retained across later firings and convergence until an operator disposes of it.
        (await harness.FireAsync(actorId, name)).ShouldBe(ReminderDisposition.Quarantined);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.ItemState(actorId).ShouldNotBeNull().Quarantine.ShouldHaveSingleItem();
        harness.Submitter.Calls.ShouldBeEmpty();
    }

    /// <summary>A kind whose purpose is not configured is denied before submission and the work is retained.</summary>
    [Fact]
    public async Task UnconfiguredPurposeIsDeniedAndRetained()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Options.Purposes.Clear();
        harness.Time.Advance(TimeSpan.FromHours(1));
        int reads = harness.Source.Reads;

        ReminderDisposition? disposition = await harness.FireAsync(actorId, name);

        disposition.ShouldBe(ReminderDisposition.Denied);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.Source.Reads.ShouldBe(reads);
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retained.LastReasonCode.ShouldBe("purpose-unconfigured");
        harness.Disposition(actorId, name).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Denied);
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
    }

    /// <summary>The same witness name carrying different evidence is a collision: quarantined, not submitted.</summary>
    [Fact]
    public async Task WitnessCollisionAtCallbackIsQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), sequence: 3);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, intent with { SourceSequence = 9 });
        harness.Time.Advance(TimeSpan.FromHours(1));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        disposition.ShouldBe(ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("witness-collision");
        ReminderDispositionRecord audit = harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Quarantined);
        audit.ReasonCode.ShouldBe("witness-collision");
    }

    /// <summary>Two current intents that map to one reminder name with different evidence are both quarantined at registration.</summary>
    [Fact]
    public async Task CollidingIntentsInOneFoldAreQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent, intent with { Payload = [1] });

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.ShouldBe(new ReminderConvergenceResult(0, 0, 0, 0, 2));
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        state.Entries.ShouldBeEmpty();
        state.Quarantine.Count.ShouldBe(2);
        state.Quarantine.ShouldAllBe(record => record.ReasonCode == "witness-collision");
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
        harness.Candidates().ShouldHaveSingleItem();
    }

    /// <summary>Malformed evidence is quarantined by digest, never dropped, and repeated folds do not duplicate it.</summary>
    [Theory]
    [InlineData("target-mismatch")]
    [InlineData("kind-unsupported")]
    [InlineData("due-not-utc")]
    [InlineData("revision-invalid")]
    [InlineData("source-sequence-invalid")]
    [InlineData("payload-invalid")]
    [InlineData("identity-invalid")]
    [InlineData("intent-missing")]
    public async Task MalformedIntentIsQuarantinedNotDropped(string reasonCode)
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent valid = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        ReminderIntent malformed = reasonCode switch
        {
            "target-mismatch" => valid with { TargetDomain = "other" },
            "kind-unsupported" => valid with { Kind = EffectKindCatalog.CascadeCancel },
            "due-not-utc" => valid with { DueUtc = valid.DueUtc.ToOffset(TimeSpan.FromHours(1)) },
            "revision-invalid" => valid with { ScheduleRevision = -1 },
            "source-sequence-invalid" => valid with { SourceSequence = 0 },
            "payload-invalid" => valid with { Payload = null! },
            "intent-missing" => null!,
            _ => valid with { SourceDomain = "Not-Canonical" },
        };
        harness.Source.Set(Item, malformed);

        ReminderConvergenceResult first = await harness.CreateRegistrar().ConvergeAsync(Item);
        ReminderConvergenceResult second = await harness.CreateRegistrar().ConvergeAsync(Item);

        first.Quarantined.ShouldBe(1);
        second.Quarantined.ShouldBe(1);
        ReminderQuarantineRecord record = harness.ItemState(actorId).ShouldNotBeNull().Quarantine.ShouldHaveSingleItem();
        record.ReasonCode.ShouldBe(reasonCode);
        record.EvidenceDigest.Length.ShouldBe(52);
        harness.Disposition(actorId, record.EvidenceDigest).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Quarantined);
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
        harness.Candidates().ShouldHaveSingleItem();
        harness.Status.Snapshot().Quarantined.ShouldBe(1);
    }

    /// <summary>A translation failure quarantines only after a durable audit; an audit outage retains the armed witness.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TranslationFailureIsQuarantined(bool auditUnavailable)
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent);
        harness.Source.Translator = _ => throw new FormatException("synthetic undecodable payload");
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.CoordinatorStore.FailDispositionWrites = auditUnavailable;

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        if (auditUnavailable)
        {
            disposition.ShouldBe(ReminderDisposition.Retrying);
            ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
            retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
            retained.LastReasonCode.ShouldBe("audit-unavailable");
            retained.Attempts.ShouldBe(1);
            harness.Submitter.Calls.ShouldBeEmpty();
            harness.SchedulerFor(actorId).Armed.ShouldContainKey(ReminderTestHarness.Name(intent));
            harness.SchedulerFor(actorId).Cancelled.ShouldNotContain(ReminderTestHarness.Name(intent));
            harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Registered);
            harness.Status.Snapshot().Unresolved.ShouldBe(1);

            harness.CoordinatorStore.FailDispositionWrites = false;
            harness.Time.Advance(harness.Options.RetryInitialDelay);
            disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));
        }

        disposition.ShouldBe(ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("translation-failed");
        ReminderDispositionRecord audit = harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Quarantined);
        audit.ReasonCode.ShouldBe("translation-failed");
        harness.SchedulerFor(actorId).Armed.ShouldNotContainKey(ReminderTestHarness.Name(intent));

        harness.Source.Translator = _ => new ReminderCommand("ResumeWidget", [.. intent.Payload]);
        ReminderDisposition? repeated = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        repeated.ShouldBe(ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Quarantined);
    }

    /// <summary>A null or blank translation is quarantined and is not submitted.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlankTranslationIsQuarantined(bool nullCommand)
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent);
        harness.Source.Translator = nullCommand
            ? _ => null!
            : _ => new ReminderCommand(" ", null!);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        disposition.ShouldBe(ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("translation-invalid");
        ReminderDispositionRecord audit = harness.Disposition(actorId, ReminderTestHarness.Name(intent)).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Quarantined);
        audit.ReasonCode.ShouldBe("translation-invalid");
    }

    /// <summary>
    /// A stale firing whose replacement cannot be indexed leaves the original reminder armed.
    /// </summary>
    [Fact]
    public async Task StaleCallbackKeepsReminderWhenReplacementIndexIsFull()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent stale = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2), revision: 1);
        string staleName = ReminderTestHarness.Name(stale);
        harness.Source.Set(Item, stale);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        ReminderTarget other = ReminderTestHarness.Target("item-2");
        harness.Source.Set(other, ReminderTestHarness.Intent(other, harness.Time.Now.AddHours(3)));
        _ = await harness.CreateRegistrar().ConvergeAsync(other);
        harness.Options.MaxCandidatesPerTenant = 1;
        harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
            new ReminderTenantCandidates(
                ReminderTestHarness.Tenant,
                [new ReminderCandidate(other.Domain, other.Aggregate, ReminderTestHarness.ActorId(other))]));
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(8), revision: 2, sequence: 6));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, staleName);

        disposition.ShouldBe(ReminderDisposition.Retrying);
        harness.SchedulerFor(actorId).Cancelled.ShouldNotContain(staleName);
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(staleName);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldContain(entry => entry.ReminderName == staleName);
        harness.Submitter.Calls.ShouldBeEmpty();
    }

    /// <summary>Outside Development the reminder routes require the exact app-channel token.</summary>
    [Theory]
    [InlineData(null, "token-missing")]
    [InlineData("wrong-token", "token-invalid")]
    [InlineData("app-token", null)]
    public async Task TokenFilterRequiresAppChannelToken(string? presented, string? expectedDenial)
    {
        ReminderCallbackTokenFilter filter = CreateFilter(Environments.Production, "app-token");
        DefaultHttpContext context = CreateContext(ReminderRoute, presented);
        bool reachedActor = false;

        await filter.InvokeAsync(context, _ =>
        {
            reachedActor = true;
            return Task.CompletedTask;
        });

        filter.GetDenialReason(context.Request).ShouldBe(expectedDenial);
        reachedActor.ShouldBe(expectedDenial is null);
        context.Response.StatusCode.ShouldBe(expectedDenial is null ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized);
        context.Response.Body.Length.ShouldBe(0);
    }

    /// <summary>A repeated app-channel token is denied even when the first value matches.</summary>
    [Fact]
    public async Task TokenFilterRejectsRepeatedAppChannelToken()
    {
        ReminderCallbackTokenFilter filter = CreateFilter(Environments.Production, "app-token");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        context.Request.Path = ReminderRoute;
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Append(ReminderCallbackTokenFilter.HeaderName, "app-token");
        context.Request.Headers.Append(ReminderCallbackTokenFilter.HeaderName, "second-token");
        bool reachedActor = false;

        await filter.InvokeAsync(context, _ =>
        {
            reachedActor = true;
            return Task.CompletedTask;
        });

        filter.GetDenialReason(context.Request).ShouldBe("token-missing");
        reachedActor.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Body.Length.ShouldBe(0);
    }

    /// <summary>Without a configured token the filter fails closed outside Development and reports itself unconfigured.</summary>
    [Fact]
    public async Task TokenFilterFailsClosedWhenTokenIsUnconfigured()
    {
        ReminderCallbackTokenFilter filter = CreateFilter(Environments.Staging, token: null);
        DefaultHttpContext context = CreateContext(ReminderRoute, "anything");

        await filter.InvokeAsync(context, _ => throw new InvalidOperationException("must not reach the actor"));

        filter.IsConfigured.ShouldBeFalse();
        filter.GetDenialReason(context.Request).ShouldBe("token-unconfigured");
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    /// <summary>Development admits a tokenless sidecar, but still compares a configured token.</summary>
    [Fact]
    public void TokenFilterDevelopmentBehavior()
    {
        CreateFilter(Environments.Development, token: null).GetDenialReason(CreateContext(ReminderRoute, null).Request).ShouldBeNull();
        CreateFilter(Environments.Development, "app-token").GetDenialReason(CreateContext(ReminderRoute, null).Request).ShouldBe("token-missing");
        CreateFilter(Environments.Development, token: null).IsConfigured.ShouldBeTrue();
    }

    /// <summary>The filter guards only the reminder actor type, matched without regard to case like routing.</summary>
    [Theory]
    [InlineData("/actors/EventStoreReminderActor", true)]
    [InlineData("/actors/EventStoreReminderActor/wra-X/method/ConvergeAsync", true)]
    [InlineData("/ACTORS/eventstorereminderactor/wra-X/method/remind/date-wrs-X", true)]
    [InlineData("/actors/AggregateActor/tenant:widget:1/method/remind/drain-unpublished-1", false)]
    [InlineData("/actors/EventStoreReminderActorShadow/wra-X", false)]
    [InlineData("/base/actors/EventStoreReminderActor/wra-X/method/remind/date-wrs-X", true)]
    [InlineData("/tenant-gateway/v1/ACTORS/eventstorereminderactor/wra-X", true)]
    [InlineData("/base/actors/AggregateActor/wra-X", false)]
    [InlineData("/process", false)]
    [InlineData("/ready", false)]
    public async Task TokenFilterGuardsOnlyReminderActorRoutes(string path, bool guarded)
    {
        ReminderCallbackTokenFilter filter = CreateFilter(Environments.Production, "app-token");
        DefaultHttpContext context = CreateContext(path, presented: null);
        bool reached = false;

        await filter.InvokeAsync(context, _ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        filter.IsReminderActorPath(new PathString(path)).ShouldBe(guarded);
        reached.ShouldBe(!guarded);
    }

    /// <summary>A host path base cannot hide the reminder actor route from the filter.</summary>
    [Fact]
    public async Task TokenFilterGuardsRoutesBehindPathBase()
    {
        ReminderCallbackTokenFilter filter = CreateFilter(Environments.Production, "app-token");
        DefaultHttpContext context = CreateContext(ReminderRoute, presented: null);
        context.Request.PathBase = "/base";

        await filter.InvokeAsync(context, _ => throw new InvalidOperationException("must not reach the actor"));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    private static ReminderCallbackTokenFilter CreateFilter(string environmentName, string? token)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ReminderCallbackTokenFilter.ConfigurationKey] = token })
            .Build();
        return new ReminderCallbackTokenFilter(
            environment,
            configuration,
            Options.Create(new EventStoreReminderOptions { ActorTypeName = "EventStoreReminderActor" }),
            NullLogger<ReminderCallbackTokenFilter>.Instance);
    }

    private static DefaultHttpContext CreateContext(string path, string? presented)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (presented is not null)
        {
            context.Request.Headers[ReminderCallbackTokenFilter.HeaderName] = presented;
        }

        return context;
    }
}
