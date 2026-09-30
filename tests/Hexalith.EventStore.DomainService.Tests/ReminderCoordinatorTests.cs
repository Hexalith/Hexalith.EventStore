using System.Text.Json;

using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Register/reschedule, callback-replay, and restore/HA rows of the Story 4.11 matrix, asserted against the
/// persisted fake-store state rather than call counts alone.
/// </summary>
public sealed class ReminderCoordinatorTests
{
    private static readonly ReminderTarget Item = ReminderTestHarness.Target("item-1");

    /// <summary>A registration persists the witness and the index before the scheduler sees the reminder.</summary>
    [Fact]
    public async Task RegistrationPersistsWitnessAndIndexBeforeArming()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.Source.Set(Item, intent);
        bool indexedBeforeArm = false;
        harness.SchedulerFor(actorId).OnArm = _ =>
            indexedBeforeArm = harness.Candidates().Any(c => c.ActorId == actorId) && harness.ItemState(actorId) is not null;

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.ShouldBe(new ReminderConvergenceResult(1, 0, 0, 0, 0));
        indexedBeforeArm.ShouldBeTrue();
        harness.Tenants().ShouldBe([ReminderTestHarness.Tenant]);
        harness.Candidates().ShouldBe([new ReminderCandidate(Item.Domain, Item.Aggregate, actorId)]);
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        ReminderEntry entry = state.Entries.ShouldHaveSingleItem();
        entry.ReminderName.ShouldBe(ReminderTestHarness.Name(intent));
        entry.Status.ShouldBe(ReminderEntryStatus.Armed);
        entry.SourceSequence.ShouldBe(3);
        harness.SchedulerFor(actorId).Armed[entry.ReminderName].ShouldBe((TimeSpan.FromHours(2), harness.Options.RetryMaxDelay));
        harness.Disposition(actorId, entry.ReminderName).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Registered);

        // The witness is identifiers plus a digest: the domain payload never leaves the stream.
        string persisted = JsonSerializer.Serialize(state);
        persisted.ShouldNotContain(Convert.ToBase64String(intent.Payload));
        entry.PayloadDigest.Length.ShouldBe(52);
    }

    /// <summary>Duplicate intents and repeated registration keep one witness, one reminder, and one candidate.</summary>
    [Fact]
    public async Task DuplicateRegistrationKeepsOneReminder()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.Source.Set(Item, intent, intent);
        ReminderRegistrar registrar = harness.CreateRegistrar();

        _ = await registrar.ConvergeAsync(Item);
        long version = harness.ItemState(actorId).ShouldNotBeNull().Version;
        ReminderConvergenceResult second = await registrar.ConvergeAsync(Item);

        second.Unresolved.ShouldBe(0);
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        state.Entries.ShouldHaveSingleItem();
        state.Version.ShouldBe(version);
        harness.SchedulerFor(actorId).Armed.Count.ShouldBe(1);
        harness.Candidates().Count.ShouldBe(1);
    }

    /// <summary>A new schedule witness replaces the obsolete one, whose reminder is cancelled and audited.</summary>
    [Fact]
    public async Task RescheduleCancelsObsoleteWitness()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent first = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2), revision: 1);
        ReminderIntent rescheduled = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(5), revision: 2, sequence: 4);
        harness.Source.Set(Item, first);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        harness.Source.Set(Item, rescheduled);
        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.ShouldBe(new ReminderConvergenceResult(1, 0, 1, 0, 0));
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(ReminderTestHarness.Name(rescheduled));
        FakeReminderScheduler scheduler = harness.SchedulerFor(actorId);
        scheduler.Armed.Keys.ShouldBe([ReminderTestHarness.Name(rescheduled)]);
        scheduler.Cancelled.ShouldContain(ReminderTestHarness.Name(first));
        harness.Disposition(actorId, ReminderTestHarness.Name(first)).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Cancelled);
    }

    /// <summary>An obsolete witness stays durable and scheduled until its cancellation audit can be written.</summary>
    [Fact]
    public async Task ObsoleteWitnessIsRetainedWhenAuditFails()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item);
        harness.CoordinatorStore.FailDispositionWrites = true;

        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);

        retained.Cancelled.ShouldBe(0);
        retained.Unresolved.ShouldBe(1);
        ReminderEntry entry = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        entry.ReminderName.ShouldBe(name);
        entry.LastReasonCode.ShouldBe("audit-unavailable");
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
        harness.SchedulerFor(actorId).Cancelled.ShouldBeEmpty();
        harness.Candidates().ShouldHaveSingleItem();

        harness.CoordinatorStore.FailDispositionWrites = false;
        ReminderConvergenceResult released = await harness.CreateRegistrar().ConvergeAsync(Item);

        released.Cancelled.ShouldBe(1);
        harness.ItemState(actorId).ShouldBeNull();
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
        harness.Candidates().ShouldBeEmpty();
    }

    /// <summary>An obsolete witness stays durable while Scheduler cancellation is unavailable.</summary>
    [Fact]
    public async Task ObsoleteWitnessIsRetainedWhenCancellationFails()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item);
        harness.SchedulerFor(actorId).CancelFailure = new InvalidOperationException("Synthetic Scheduler outage.");

        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);

        retained.Cancelled.ShouldBe(0);
        ReminderEntry entry = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        entry.LastReasonCode.ShouldBe("cancel-failed");
        harness.Candidates().ShouldHaveSingleItem();

        harness.SchedulerFor(actorId).CancelFailure = null;
        ReminderConvergenceResult released = await harness.CreateRegistrar().ConvergeAsync(Item);

        released.Cancelled.ShouldBe(1);
        harness.ItemState(actorId).ShouldBeNull();
        harness.Candidates().ShouldBeEmpty();
    }

    /// <summary>A scheduler outage leaves the persisted, indexed witness pending and the item unresolved until re-armed.</summary>
    [Fact]
    public async Task PendingStateSurvivesSchedulerFailure()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));
        harness.SchedulerFor(actorId).ArmFailure = new InvalidOperationException("scheduler unavailable");

        ReminderConvergenceResult failed = await harness.CreateRegistrar().ConvergeAsync(Item);

        failed.ShouldBe(new ReminderConvergenceResult(0, 0, 0, 1, 0));
        ReminderEntry pending = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        pending.Status.ShouldBe(ReminderEntryStatus.Pending);
        pending.LastReasonCode.ShouldBe("arm-failed");
        harness.Candidates().ShouldHaveSingleItem();
        harness.Status.Snapshot().Unresolved.ShouldBe(1);

        harness.SchedulerFor(actorId).ArmFailure = null;
        ReminderConvergenceResult recovered = await harness.CreateRegistrar().ConvergeAsync(Item);

        recovered.ShouldBe(new ReminderConvergenceResult(1, 0, 0, 0, 0));
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Armed);
        harness.Status.Snapshot().Unresolved.ShouldBe(0);
    }

    /// <summary>An armed reminder whose registration audit failed stays pending until that audit can be retried.</summary>
    [Fact]
    public async Task RegisteredAuditFailureKeepsArmedWitnessUnresolved()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        harness.CoordinatorStore.FailDispositionWrites = true;

        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);

        retained.Armed.ShouldBe(1);
        retained.Unresolved.ShouldBe(1);
        ReminderEntry pending = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        pending.Status.ShouldBe(ReminderEntryStatus.Pending);
        pending.LastReasonCode.ShouldBe("audit-unavailable");
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
        harness.Disposition(actorId, name).ShouldBeNull();
        harness.Candidates().ShouldHaveSingleItem();

        harness.CoordinatorStore.FailDispositionWrites = false;
        ReminderConvergenceResult recovered = await harness.CreateRegistrar().ConvergeAsync(Item);

        recovered.Armed.ShouldBe(1);
        recovered.Unresolved.ShouldBe(0);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Armed);
        harness.Disposition(actorId, name).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Registered);
    }

    /// <summary>When the stream no longer holds an intent, the state is released, the reminder cancelled, and the index entry removed last.</summary>
    [Fact]
    public async Task RemovedIntentReleasesStateThenIndex()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        harness.Source.Set(Item);
        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.ShouldBe(new ReminderConvergenceResult(0, 0, 1, 0, 0));
        harness.ItemState(actorId).ShouldBeNull();
        harness.Candidates().ShouldBeEmpty();
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
    }

    /// <summary>A valid witness submits one deterministic effect built only from the stored tuple and the re-folded intent.</summary>
    [Fact]
    public async Task CallbackSubmitsDeterministicEffect()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(2));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, name);

        disposition.ShouldBe(ReminderDisposition.Submitted);
        (TrustedEffectSubmission submission, TrustedEffectContext context) = harness.Submitter.Calls.ShouldHaveSingleItem();
        submission.Identity.ShouldBe(new EffectIdentity(
            ReminderTestHarness.Tenant, Item.Domain, Item.Aggregate, 3, EffectKindCatalog.DateResume, Item.Domain, Item.Aggregate, 0));
        string effectId = EffectIdentityCodec.ComputeEffectId(submission.Identity);
        submission.MessageId.ShouldBe("wrk-" + effectId);
        submission.IdempotencyKey.ShouldBe(submission.MessageId);
        submission.CommandType.ShouldBe("ResumeWidget");
        context.ShouldBe(new TrustedEffectContext("widget-service", ReminderTestHarness.DatePurpose, name, "synthetic-delegation"));
        harness.Tokens.Requests.ShouldHaveSingleItem().ShouldBe(
            new ReminderDelegationRequest(submission, "widget-service", ReminderTestHarness.DatePurpose, name));

        ReminderDispositionRecord audit = harness.Disposition(actorId, name).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Submitted);
        audit.EffectId.ShouldBe(effectId);
        audit.TargetDisposition.ShouldBe(TrustedEffectDisposition.Success);
        audit.Replayed.ShouldBeFalse();
        harness.ItemState(actorId).ShouldBeNull();
        harness.Candidates().ShouldHaveSingleItem();
        harness.SchedulerFor(actorId).Cancelled.ShouldContain(name);
    }

    /// <summary>After a restart the same valid witness yields the same effect identity and a replayed persisted receipt.</summary>
    [Fact]
    public async Task CallbackReplayAfterRestartReturnsSameEffectAndReceipt()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(3));
        _ = await harness.FireAsync(actorId, name);
        string firstEffect = harness.Disposition(actorId, name).ShouldNotBeNull().EffectId.ShouldNotBeNull();

        // Restart: a fresh host re-registers the still-current witness and the scheduler fires it again.
        var restarted = new ReminderRuntimeStatus();
        _ = await harness.CreateRegistrar(restarted).ConvergeAsync(Item);
        ReminderDispositionRecord replay = harness.Disposition(actorId, name).ShouldNotBeNull();

        replay.Disposition.ShouldBe(ReminderDisposition.Submitted);
        replay.EffectId.ShouldBe(firstEffect);
        replay.Replayed.ShouldBeTrue();
        harness.Submitter.Receipts.Count.ShouldBe(1);
        harness.Submitter.Calls.Count.ShouldBe(2);
        harness.ItemState(actorId).ShouldBeNull();
    }

    /// <summary>A host that loses its release after the receipt persisted replays the same receipt from another host.</summary>
    [Fact]
    public async Task CrashBetweenReceiptAndReleaseReplaysOnAnotherHost()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(2));
        harness.Store.ConcurrentWriteBeforeTryErase = () => throw new InvalidOperationException("host crashed");

        ReminderDisposition? crashed = await harness.FireAsync(actorId, name);

        crashed.ShouldBe(ReminderDisposition.Retrying);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(name);
        harness.Candidates().ShouldHaveSingleItem();
        harness.Submitter.Receipts.Count.ShouldBe(1);

        harness.Store.ConcurrentWriteBeforeTryErase = null;
        ReminderDisposition? replayed = await harness.FireAsync(actorId, name, new ReminderRuntimeStatus());

        replayed.ShouldBe(ReminderDisposition.Submitted);
        ReminderDispositionRecord audit = harness.Disposition(actorId, name).ShouldNotBeNull();
        audit.Replayed.ShouldBeTrue();
        audit.EffectId.ShouldBe(harness.Submitter.Receipts.Keys.Single());
        harness.Submitter.Receipts.Count.ShouldBe(1);
        harness.ItemState(actorId).ShouldBeNull();
        harness.Candidates().ShouldHaveSingleItem();
    }

    /// <summary>A callback keeps a submitted receipt unresolved until its Scheduler reminder is cancelled.</summary>
    [Fact]
    public async Task SubmittedCallbackWaitsForCancellationBeforeRelease()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.SchedulerFor(actorId).CancelFailure = new InvalidOperationException("Synthetic Scheduler outage.");

        ReminderDisposition? retained = await harness.FireAsync(actorId, name);

        retained.ShouldBe(ReminderDisposition.Retrying);
        harness.Submitter.Receipts.Count.ShouldBe(1);
        ReminderEntry retrying = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retrying.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retrying.LastReasonCode.ShouldBe("cancel-failed");
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
        harness.Candidates().ShouldHaveSingleItem();
        harness.Status.Snapshot().Unresolved.ShouldBe(1);

        harness.SchedulerFor(actorId).CancelFailure = null;
        harness.Time.Advance(TimeSpan.FromSeconds(31));
        ReminderDisposition? released = await harness.FireAsync(actorId, name);

        released.ShouldBe(ReminderDisposition.Submitted);
        harness.Disposition(actorId, name).ShouldNotBeNull().Replayed.ShouldBeTrue();
        harness.ItemState(actorId).ShouldBeNull();
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
        harness.Candidates().ShouldHaveSingleItem();
        harness.Status.Snapshot().Unresolved.ShouldBe(0);
    }

    /// <summary>Convergence also retains a submitted receipt until Scheduler cancellation recovers.</summary>
    [Fact]
    public async Task SubmittedConvergenceWaitsForCancellationBeforeRelease()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddMinutes(-1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        harness.SchedulerFor(actorId).CancelFailure = new InvalidOperationException("Synthetic Scheduler outage.");

        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);

        retained.Submitted.ShouldBe(0);
        retained.Unresolved.ShouldBe(1);
        harness.Submitter.Receipts.Count.ShouldBe(1);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("cancel-failed");
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
        harness.Candidates().ShouldHaveSingleItem();

        harness.SchedulerFor(actorId).CancelFailure = null;
        harness.Time.Advance(TimeSpan.FromSeconds(31));
        ReminderConvergenceResult released = await harness.CreateRegistrar().ConvergeAsync(Item);

        released.Submitted.ShouldBe(1);
        harness.Disposition(actorId, name).ShouldNotBeNull().Replayed.ShouldBeTrue();
        harness.ItemState(actorId).ShouldBeNull();
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
        harness.Candidates().ShouldHaveSingleItem();
    }

    /// <summary>An uncertain receipt keeps the witness, re-arms a backoff reminder, and is never acknowledged by throwing.</summary>
    [Fact]
    public async Task UncertainReceiptIsRetriedWithoutAcknowledgement()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(2));
        harness.Submitter.FailuresRemaining = 3;
        harness.Submitter.PersistBeforeFailure = true;

        ReminderDisposition? first = await harness.FireAsync(actorId, name);

        first.ShouldBe(ReminderDisposition.Retrying);
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retained.Attempts.ShouldBe(1);
        retained.LastReasonCode.ShouldBe("submission-uncertain");
        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(30));
        harness.Disposition(actorId, name).ShouldNotBeNull().Disposition.ShouldBe(ReminderDisposition.Retrying);
        harness.Candidates().ShouldHaveSingleItem();
        harness.Status.Snapshot().Unresolved.ShouldBe(1);

        ReminderDisposition? duplicate = await harness.FireAsync(actorId, name);

        duplicate.ShouldBe(ReminderDisposition.Retrying);
        harness.Submitter.Calls.Count.ShouldBe(1);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Attempts.ShouldBe(1);
        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(30));

        harness.Time.Advance(TimeSpan.FromSeconds(31));
        _ = await harness.FireAsync(actorId, name);
        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(60));
        harness.Time.Advance(TimeSpan.FromSeconds(61));
        _ = await harness.FireAsync(actorId, name);
        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(120));

        harness.Time.Advance(TimeSpan.FromSeconds(121));
        ReminderDisposition? settled = await harness.FireAsync(actorId, name);

        settled.ShouldBe(ReminderDisposition.Submitted);
        harness.Disposition(actorId, name).ShouldNotBeNull().Replayed.ShouldBeTrue();
        harness.Submitter.Receipts.Count.ShouldBe(1);
        harness.ItemState(actorId).ShouldBeNull();
        harness.Status.Snapshot().Unresolved.ShouldBe(0);
    }

    /// <summary>Without a delegation issuer or a submitter, submission fails closed and the work stays retained.</summary>
    [Theory]
    [InlineData(false, true, "delegation-unavailable")]
    [InlineData(true, false, "submitter-unavailable")]
    public async Task MissingSubmissionSeamsFailClosed(bool withTokens, bool withSubmitter, string reasonCode)
    {
        var harness = new ReminderTestHarness { WithTokens = withTokens, WithSubmitter = withSubmitter };
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(2));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        disposition.ShouldBe(ReminderDisposition.Retrying);
        harness.Submitter.Calls.ShouldBeEmpty();
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retained.LastReasonCode.ShouldBe(reasonCode);
        harness.Status.Snapshot().Unresolved.ShouldBe(1);
    }

    /// <summary>A blank workload is denied before delegation or submission, and the witness stays retained.</summary>
    [Fact]
    public async Task BlankWorkloadIsDeniedAndRetained()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Options.Workload = " ";
        harness.Time.Advance(TimeSpan.FromHours(2));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        disposition.ShouldBe(ReminderDisposition.Denied);
        harness.Submitter.Calls.ShouldBeEmpty();
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retained.LastReasonCode.ShouldBe("workload-unconfigured");
    }

    /// <summary>A null fold is not an empty stream: stored reminders stay armed.</summary>
    [Fact]
    public async Task NullIntentSourceDoesNotCancelStoredReminders()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.ReturnNull = true;

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);
        ReminderDisposition? callback = await harness.FireAsync(actorId, name);

        result.Cancelled.ShouldBe(0);
        callback.ShouldBe(ReminderDisposition.Retrying);
        harness.SchedulerFor(actorId).Cancelled.ShouldBeEmpty();
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.ReminderName.ShouldBe(name);
        retained.LastReasonCode.ShouldBe("source-unavailable");
    }

    /// <summary>A blank domain is rejected before it can be stored.</summary>
    [Fact]
    public async Task BlankTargetDomainIsRejected()
    {
        var harness = new ReminderTestHarness();
        var blank = new ReminderTarget(ReminderTestHarness.Tenant, " ", "item-1");
        harness.Source.Set(blank, ReminderTestHarness.Intent(blank, harness.Time.Now.AddHours(2)));

        ArgumentException failure = await Should.ThrowAsync<ArgumentException>(
            () => harness.CreateRegistrar().ConvergeAsync(blank));

        failure.ParamName.ShouldBe("target");
        harness.ItemState(ReminderTestHarness.ActorId(blank)).ShouldBeNull();
        harness.Candidates().ShouldBeEmpty();
    }

    /// <summary>A restored blank domain is quarantined instead of being retired as a stale witness.</summary>
    [Fact]
    public async Task BlankStoredDomainIsQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        ReminderItemState stored = harness.ItemState(actorId).ShouldNotBeNull();
        harness.SeedItemState(actorId, stored with { Domain = " " });
        harness.Time.Advance(TimeSpan.FromHours(1));

        ReminderDisposition? disposition = await harness.FireAsync(actorId, name);

        disposition.ShouldBe(ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.Disposition(actorId, name).ShouldNotBeNull().Disposition.ShouldNotBe(ReminderDisposition.Stale);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("domain-invalid");
    }

    /// <summary>A null persisted candidate list does not throw when a candidate is removed.</summary>
    [Fact]
    public async Task NullCandidateListIsIgnoredOnRemoval()
    {
        var harness = new ReminderTestHarness();
        harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
            new ReminderTenantCandidates(ReminderTestHarness.Tenant, null!));

        await harness.CreateIndex().RemoveCandidateAsync(Item, ReminderTestHarness.ActorId(Item), CancellationToken.None);

        harness.Candidates().ShouldBeEmpty();
    }

    /// <summary>Concurrent registrations from two hosts serialize in the actor turn and produce one registration.</summary>
    [Fact]
    public async Task TwoHostsRacingRegisterOnce()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));
        ReminderRegistrar hostA = harness.CreateRegistrar(new ReminderRuntimeStatus());
        ReminderRegistrar hostB = harness.CreateRegistrar(new ReminderRuntimeStatus());

        _ = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => (i % 2 == 0 ? hostA : hostB).ConvergeAsync(Item)));

        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        harness.SchedulerFor(actorId).Armed.Count.ShouldBe(1);
        harness.Candidates().ShouldHaveSingleItem();
        harness.Tenants().ShouldHaveSingleItem();
    }

    /// <summary>
    /// A first writer that lost the compare-and-swap race fails closed by throwing, so its caller retries: nothing is
    /// scheduled and the winner's state stands.
    /// </summary>
    [Fact]
    public async Task ConflictingWriterFailsClosed()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));
        var winner = new ReminderItemState(Item.Tenant, Item.Domain, Item.Aggregate, 42, [], []);
        bool raced = false;
        harness.Store.ConcurrentWriteBeforeTrySave = () =>
        {
            if (!raced)
            {
                raced = true;
                harness.SeedItemState(actorId, winner);
            }
        };

        ReminderFailClosedException failure = await Should.ThrowAsync<ReminderFailClosedException>(() => harness.CreateCoordinator()
            .ConvergeAsync(actorId, Item, harness.SchedulerFor(actorId), CancellationToken.None));

        failure.ReasonCode.ShouldBe("state-conflict");
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
        harness.ItemState(actorId).ShouldNotBeNull().Version.ShouldBe(42);
    }

    /// <summary>A full tenant index fails the first registration closed by throwing, so the caller's delivery retries.</summary>
    [Fact]
    public async Task FullTenantIndexThrowsWithoutState()
    {
        var harness = new ReminderTestHarness();
        harness.Options.MaxCandidatesPerTenant = 1;
        ReminderTarget second = ReminderTestHarness.Target("item-2");
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));
        harness.Source.Set(second, ReminderTestHarness.Intent(second, harness.Time.Now.AddHours(2)));
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        ReminderFailClosedException failure = await Should.ThrowAsync<ReminderFailClosedException>(
            () => harness.CreateRegistrar().ConvergeAsync(second));

        failure.ReasonCode.ShouldBe("index-capacity");
        string secondActor = ReminderTestHarness.ActorId(second);
        harness.ItemState(secondActor).ShouldBeNull();
        harness.SchedulerFor(secondActor).ArmCalls.ShouldBe(0);
        harness.Candidates().ShouldHaveSingleItem().Aggregate.ShouldBe(Item.Aggregate);
    }

    /// <summary>An exhausted index compare-and-swap budget fails the first registration closed by throwing.</summary>
    [Fact]
    public async Task ExhaustedIndexWriteAttemptsThrowWithoutState()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Options.IndexWriteAttempts = 2;
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));
        string registryKey = ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName);
        int competingWrites = 0;
        harness.Store.ConcurrentWriteBeforeTrySave = () => harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            registryKey,
            new ReminderTenantRegistry([$"competitor-{++competingWrites}"]));

        ReminderFailClosedException failure = await Should.ThrowAsync<ReminderFailClosedException>(
            () => harness.CreateRegistrar().ConvergeAsync(Item));

        failure.ReasonCode.ShouldBe("index-conflict");
        competingWrites.ShouldBe(2);
        harness.ItemState(actorId).ShouldBeNull();
        harness.SchedulerFor(actorId).ArmCalls.ShouldBe(0);
    }

    /// <summary>Intents with different names but one effect identity are quarantined and never submitted.</summary>
    [Fact]
    public async Task SharedEffectIdentityIsQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent first = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2), revision: 1, sequence: 3);
        ReminderIntent sharesEffect = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(4), revision: 2, sequence: 3);
        harness.Source.Set(Item, first);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        harness.Source.Set(Item, first, sharesEffect);
        harness.Time.Advance(TimeSpan.FromHours(5));
        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.Submitted.ShouldBe(0);
        result.Armed.ShouldBe(0);
        harness.Submitter.Calls.ShouldBeEmpty();
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        ReminderEntry entry = state.Entries.ShouldHaveSingleItem();
        entry.Status.ShouldBe(ReminderEntryStatus.Quarantined);
        entry.LastReasonCode.ShouldBe("effect-collision");
        state.Quarantine.Count.ShouldBe(2);
        state.Quarantine.ShouldAllBe(record => record.ReasonCode == "effect-collision");
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
    }

    /// <summary>A quarantine audit outage retains the original witness and does not cancel its Scheduler reminder.</summary>
    [Fact]
    public async Task QuarantineAuditFailureRetainsOriginalWitness()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent original = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2), sequence: 3);
        string name = ReminderTestHarness.Name(original);
        harness.Source.Set(Item, original);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, original with { SourceSequence = 9 });
        harness.CoordinatorStore.FailDispositionWrites = true;

        ReminderConvergenceResult retained = await harness.CreateRegistrar().ConvergeAsync(Item);

        retained.Unresolved.ShouldBe(1);
        ReminderEntry entry = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        entry.Status.ShouldBe(ReminderEntryStatus.Retrying);
        entry.LastReasonCode.ShouldBe("audit-unavailable");
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(name);
        harness.SchedulerFor(actorId).Cancelled.ShouldBeEmpty();

        harness.CoordinatorStore.FailDispositionWrites = false;
        ReminderConvergenceResult quarantined = await harness.CreateRegistrar().ConvergeAsync(Item);

        quarantined.Quarantined.ShouldBe(2);
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Quarantined);
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
    }

    /// <summary>Due retained work waits out its backoff window on every pass, then is resubmitted after it.</summary>
    [Fact]
    public async Task RetryingWorkWaitsOutBackoffAcrossPasses()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddMinutes(-1));
        harness.Source.Set(Item, intent);
        harness.Submitter.FailuresRemaining = 1;
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Submitter.Calls.Count.ShouldBe(1);
        int armCalls = harness.SchedulerFor(actorId).ArmCalls;

        harness.Time.Advance(TimeSpan.FromSeconds(10));
        ReminderReconciliationPass inside = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        inside.Submitted.ShouldBe(0);
        inside.Unresolved.ShouldBe(1);
        harness.Submitter.Calls.Count.ShouldBe(1);
        harness.SchedulerFor(actorId).ArmCalls.ShouldBe(armCalls);

        harness.Time.Advance(TimeSpan.FromSeconds(25));
        ReminderReconciliationPass after = await harness.CreateReconciler().RunPassAsync(CancellationToken.None);

        after.Submitted.ShouldBe(1);
        harness.Submitter.Calls.Count.ShouldBe(2);
        harness.ItemState(actorId).ShouldBeNull();
    }

    /// <summary>A retrying witness whose backoff reminder was lost is re-armed without an early submission.</summary>
    [Fact]
    public async Task LostRetryReminderIsRearmedDuringBackoff()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddMinutes(-1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        harness.Submitter.FailuresRemaining = 1;
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.SchedulerFor(actorId).Lose(name);
        harness.Time.Advance(TimeSpan.FromSeconds(10));

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.Submitted.ShouldBe(0);
        result.Armed.ShouldBe(1);
        harness.Submitter.Calls.Count.ShouldBe(1);
        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(20));
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Retrying);
    }

    /// <summary>An armed reminder the scheduler still holds is not re-registered by a later convergence.</summary>
    [Fact]
    public async Task HeldArmedReminderIsNotRearmed()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        ReminderConvergenceResult again = await harness.CreateRegistrar().ConvergeAsync(Item);

        again.Armed.ShouldBe(0);
        harness.SchedulerFor(actorId).ArmCalls.ShouldBe(1);
    }

    /// <summary>An inconclusive Scheduler lookup re-registers the deterministic reminder name.</summary>
    [Fact]
    public async Task SchedulerLookupFailureRearmsFutureWitness()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.SchedulerFor(actorId).LookupFailure = new HttpRequestException("Synthetic lookup outage.");

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.Armed.ShouldBe(1);
        harness.SchedulerFor(actorId).ArmCalls.ShouldBe(2);
        harness.SchedulerFor(actorId).Armed.ShouldContainKey(ReminderTestHarness.Name(intent));
    }

    /// <summary>The Scheduler due time is calculated from a fresh clock after the authoritative stream fold.</summary>
    [Fact]
    public async Task FutureIntentUsesFreshClockWhenArmed()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.Source.Set(Item, intent);
        harness.Source.OnRead = _ => harness.Time.Advance(TimeSpan.FromHours(1));

        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        harness.SchedulerFor(actorId).Armed[ReminderTestHarness.Name(intent)].DueTime.ShouldBe(TimeSpan.FromHours(1));
    }

    /// <summary>Item state that holds work but lost its index entry, as after an older index restore, is re-indexed.</summary>
    [Fact]
    public async Task MissingIndexEntryIsRestored()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
            new ReminderTenantCandidates(ReminderTestHarness.Tenant, []));
        harness.ItemState(actorId).ShouldNotBeNull();

        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        harness.Candidates().ShouldBe([new ReminderCandidate(Item.Domain, Item.Aggregate, actorId)]);
    }

    /// <summary>The fail-closed catch retries discovery for existing unindexed state before acknowledging it.</summary>
    [Fact]
    public async Task FailClosedCatchRestoresDiscoveryAfterIndexConflict()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Store.SeedRaw(
            harness.Options.StateStoreName,
            ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
            new ReminderTenantCandidates(ReminderTestHarness.Tenant, []));
        harness.Options.IndexWriteAttempts = 1;
        bool conflicted = false;
        harness.Store.ConcurrentWriteBeforeTrySave = () =>
        {
            if (conflicted)
            {
                return;
            }

            conflicted = true;
            harness.Store.ConcurrentWriteBeforeTrySave = null;
            harness.Store.SeedRaw(
                harness.Options.StateStoreName,
                ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, ReminderTestHarness.Tenant),
                new ReminderTenantCandidates(ReminderTestHarness.Tenant, []));
        };

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        conflicted.ShouldBeTrue();
        result.Unresolved.ShouldBe(1);
        harness.Candidates().ShouldBe([new ReminderCandidate(Item.Domain, Item.Aggregate, actorId)]);
        harness.ItemState(actorId).ShouldNotBeNull();
        harness.SchedulerFor(actorId).ArmCalls.ShouldBe(1);
    }

    /// <summary>An actor collision re-indexes the restored state under its stored target, not the colliding caller.</summary>
    [Fact]
    public async Task ActorCollisionRestoresStoredTargetIndex()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        var foreign = new ReminderItemState(Item.Tenant, "other-domain", Item.Aggregate, 3, [], []);
        harness.SeedItemState(actorId, foreign);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));

        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        harness.Candidates().ShouldBe([new ReminderCandidate("other-domain", Item.Aggregate, actorId)]);
    }

    /// <summary>Malformed persisted collection elements become durable quarantine instead of terminating convergence.</summary>
    [Fact]
    public async Task MalformedPersistedCollectionElementsAreQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        harness.SeedItemState(actorId, new ReminderItemState(
            Item.Tenant,
            Item.Domain,
            Item.Aggregate,
            3,
            [null!],
            [null!]));
        harness.Source.Set(Item, intent);

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.Armed.ShouldBe(1);
        result.Quarantined.ShouldBe(2);
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        state.Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(ReminderTestHarness.Name(intent));
        state.Quarantine.Select(static record => record.ReasonCode).Order().ShouldBe([
            "stored-entry-invalid",
            "stored-quarantine-invalid",
        ]);
        state.Quarantine.ShouldAllBe(record => harness.Disposition(actorId, record.EvidenceDigest) != null);
        harness.Candidates().ShouldHaveSingleItem();
    }

    /// <summary>Duplicate persisted reminder identities become audited quarantine before any execution.</summary>
    [Fact]
    public async Task DuplicatePersistedReminderIdentitiesAreQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        ReminderItemState stored = harness.ItemState(actorId).ShouldNotBeNull();
        ReminderEntry entry = stored.Entries.ShouldHaveSingleItem();
        harness.SeedItemState(actorId, stored with { Entries = [entry, entry] });

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.Armed.ShouldBe(0);
        result.Submitted.ShouldBe(0);
        result.Quarantined.ShouldBe(2);
        ReminderItemState quarantined = harness.ItemState(actorId).ShouldNotBeNull();
        quarantined.Entries.ShouldBeEmpty();
        quarantined.Quarantine.Count.ShouldBe(2);
        quarantined.Quarantine.ShouldAllBe(record => record.ReasonCode == "stored-entry-duplicate" && record.ReminderName == name);
        quarantined.Quarantine.ShouldAllBe(record =>
            harness.Disposition(actorId, record.EvidenceDigest) != null
            && harness.Disposition(actorId, record.EvidenceDigest)!.Disposition == ReminderDisposition.Quarantined);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.SchedulerFor(actorId).Armed.ShouldNotContainKey(name);
        harness.Candidates().ShouldHaveSingleItem();
    }

    /// <summary>A fail-closed error after a durable write reports the reloaded state rather than the turn's stale snapshot.</summary>
    [Fact]
    public async Task ExistingStateFailClosedUsesReloadedSnapshot()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent colliding = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), sequence: 3);
        ReminderIntent due = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2), revision: 2, sequence: 4);
        harness.Source.Set(Item, colliding, due);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Source.Set(Item, colliding with { SourceSequence = 9 }, due);
        harness.Time.Advance(TimeSpan.FromHours(3));
        string itemKey = ReminderStateKeys.Item(harness.Options.ActorTypeName, actorId);
        int itemWrites = 0;
        harness.CoordinatorStore.RejectTrySave = key => string.Equals(key, itemKey, StringComparison.Ordinal)
            && ++itemWrites == 2;

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.Unresolved.ShouldBe(1);
        result.Quarantined.ShouldBe(2);
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        state.Entries.Count(static entry => entry.Status != ReminderEntryStatus.Quarantined).ShouldBe(1);
        state.Entries.Count(static entry => entry.Status == ReminderEntryStatus.Quarantined).ShouldBe(1);
        state.Quarantine.ShouldHaveSingleItem();
    }

    /// <summary>Without a durable audit record a durable receipt does not release the witness; it is retried after backoff.</summary>
    [Fact]
    public async Task UnwritableAuditKeepsReceiptWitnessRetrying()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.CoordinatorStore.FailDispositionWrites = true;

        ReminderDisposition? disposition = await harness.FireAsync(actorId, name);

        disposition.ShouldBe(ReminderDisposition.Retrying);
        harness.Submitter.Receipts.Count.ShouldBe(1);
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retained.LastReasonCode.ShouldBe("audit-unavailable");
        harness.SchedulerFor(actorId).Armed[name].DueTime.ShouldBe(TimeSpan.FromSeconds(30));
        harness.Candidates().ShouldHaveSingleItem();
    }

    /// <summary>A receipt for another effect identity is uncertain: nothing is released.</summary>
    [Fact]
    public async Task MismatchedReceiptIsRetained()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.Submitter.ReceiptOverride = receipt => receipt with { EffectId = "NOT-THIS-EFFECT" };

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        disposition.ShouldBe(ReminderDisposition.Retrying);
        ReminderEntry retained = harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        retained.Status.ShouldBe(ReminderEntryStatus.Retrying);
        retained.LastReasonCode.ShouldBe("receipt-mismatch");
        harness.Candidates().ShouldHaveSingleItem();
        harness.SchedulerFor(actorId).Cancelled.ShouldBeEmpty();
    }

    /// <summary>A delegation issuer that throws fails the submission closed as <c>delegation-failed</c>.</summary>
    [Fact]
    public async Task ThrowingDelegationIssuerIsRetained()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.Tokens.Failure = new HttpRequestException("Synthetic issuer outage.");

        ReminderDisposition? disposition = await harness.FireAsync(actorId, ReminderTestHarness.Name(intent));

        disposition.ShouldBe(ReminderDisposition.Retrying);
        harness.Submitter.Calls.ShouldBeEmpty();
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().LastReasonCode.ShouldBe("delegation-failed");
    }

    /// <summary>Every durable target disposition releases the witness as <c>Submitted</c> and records it.</summary>
    [Theory]
    [InlineData(TrustedEffectDisposition.Success)]
    [InlineData(TrustedEffectDisposition.Rejection)]
    [InlineData(TrustedEffectDisposition.NoOp)]
    public async Task EveryDurableTargetDispositionReleases(TrustedEffectDisposition targetDisposition)
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent intent = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1));
        string name = ReminderTestHarness.Name(intent);
        harness.Source.Set(Item, intent);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);
        harness.Time.Advance(TimeSpan.FromHours(1));
        harness.Submitter.Disposition = targetDisposition;

        ReminderDisposition? disposition = await harness.FireAsync(actorId, name);

        disposition.ShouldBe(ReminderDisposition.Submitted);
        ReminderDispositionRecord audit = harness.Disposition(actorId, name).ShouldNotBeNull();
        audit.Disposition.ShouldBe(ReminderDisposition.Submitted);
        audit.TargetDisposition.ShouldBe(targetDisposition);
        harness.ItemState(actorId).ShouldBeNull();
        harness.Candidates().ShouldHaveSingleItem();
    }

    /// <summary>A complete pass prunes items it no longer discovers from readiness; an incomplete pass prunes nothing.</summary>
    [Fact]
    public void RuntimeStatusPrunesOnlyAfterCompletePass()
    {
        var status = new ReminderRuntimeStatus();
        status.RecordItem("wra-A", 1, 0);
        status.RecordItem("wra-B", 0, 2);

        status.CompletePass(DateTimeOffset.UnixEpoch, 1, []);

        status.Snapshot().ShouldBe(new ReminderRuntimeSnapshot(true, DateTimeOffset.UnixEpoch, 1, 2, 1, 2));

        status.CompletePass(DateTimeOffset.UnixEpoch.AddMinutes(5), 0, ["wra-A"]);

        status.Snapshot().ShouldBe(new ReminderRuntimeSnapshot(true, DateTimeOffset.UnixEpoch.AddMinutes(5), 0, 1, 1, 0));
    }

    /// <summary>State restored from an older backup converges to the stream: the stale witness is cancelled, the current one armed.</summary>
    [Fact]
    public async Task RestoredStateConvergesToStream()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        ReminderIntent restored = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(1), revision: 1);
        ReminderIntent current = ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(6), revision: 2, sequence: 5);
        harness.Source.Set(Item, restored);
        _ = await harness.CreateRegistrar().ConvergeAsync(Item);

        harness.Source.Set(Item, current);
        ReminderConvergenceResult result = await harness.CreateRegistrar(new ReminderRuntimeStatus()).ConvergeAsync(Item);

        result.ShouldBe(new ReminderConvergenceResult(1, 0, 1, 0, 0));
        harness.ItemState(actorId).ShouldNotBeNull().Entries.ShouldHaveSingleItem().ReminderName.ShouldBe(ReminderTestHarness.Name(current));
        harness.SchedulerFor(actorId).Armed.Keys.ShouldBe([ReminderTestHarness.Name(current)]);
    }

    /// <summary>A target whose identifiers collide with another item's persisted actor state is quarantined without touching it.</summary>
    [Fact]
    public async Task ActorCollisionIsQuarantined()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        var foreign = new ReminderItemState(Item.Tenant, "other-domain", Item.Aggregate, 3, [], []);
        harness.SeedItemState(actorId, foreign);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now.AddHours(2)));

        ReminderConvergenceResult result = await harness.CreateRegistrar().ConvergeAsync(Item);

        result.ShouldBe(new ReminderConvergenceResult(0, 0, 0, 0, 1));
        ReminderItemState state = harness.ItemState(actorId).ShouldNotBeNull();
        state.Domain.ShouldBe("other-domain");
        state.Quarantine.ShouldHaveSingleItem().ReasonCode.ShouldBe("actor-collision");
        harness.SchedulerFor(actorId).Armed.ShouldBeEmpty();
    }
}
