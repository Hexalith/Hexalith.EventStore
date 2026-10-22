using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Distinct immutable roles allow several current witnesses from one committed source envelope.</summary>
public sealed class ReminderOrdinalTests
{
    /// <summary>Different roles retain distinct receipts across restart while duplicate roles still quarantine.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultipleWitnessesUsePersistedOrdinals(bool collide)
    {
        var harness = new ReminderTestHarness();
        ReminderTarget target = ReminderTestHarness.Target("multi-date");
        ReminderIntent first = ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1)) with { SourceMessageId = "committed-source-message" };
        ReminderIntent second = first with { DueUtc = first.DueUtc.AddHours(1), ScheduleRevision = 2, Ordinal = collide ? 0 : 1 };
        harness.Source.Set(target, first, second);
        ReminderConvergenceResult registered = await harness.CreateRegistrar().ConvergeAsync(target);
        registered.Quarantined.ShouldBe(collide ? 2 : 0);
        if (collide) return;

        registered.Armed.ShouldBe(2);
        ReminderItemState persisted = harness.ItemState(ReminderTestHarness.ActorId(target))!;
        persisted.Entries.Select(entry => entry.Ordinal).Order().ShouldBe([0L, 1L]);
        harness.Time.Advance(TimeSpan.FromHours(3));
        ReminderConvergenceResult recovered = await harness.CreateRegistrar(new ReminderRuntimeStatus()).ConvergeAsync(target);
        recovered.Submitted.ShouldBe(2);
        harness.Submitter.Receipts.Count.ShouldBe(2);
        harness.Submitter.Calls.All(call => call.Context.CausationId == "committed-source-message").ShouldBeTrue();
        harness.ItemState(ReminderTestHarness.ActorId(target)).ShouldBeNull();
    }
}
