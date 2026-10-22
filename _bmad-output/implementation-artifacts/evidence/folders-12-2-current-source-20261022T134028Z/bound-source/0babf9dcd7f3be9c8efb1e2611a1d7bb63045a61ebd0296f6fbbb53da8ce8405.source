using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Optional witness fields must not collapse distinct retained quarantine evidence.</summary>
public sealed class ReminderEvidenceFramingTests
{
    private const long AmbiguousOrdinal = (4L << 32) | 0x41424344;

    /// <summary>Default optional fields preserve the pre-extension durable quarantine identifiers byte for byte.</summary>
    [Theory]
    [InlineData(false, "KHW1GJH3AE51MWT85YPGM6J27KX0P9RW9E3BP1Q6HDZZ54MK1PY0")]
    [InlineData(true, "F21STEAP32DGYH7SZ1MXQV7ARQ5S0AZY33WV0P0YXC73S1G5WJJG")]
    public async Task DefaultFieldsPreserveHistoricalEvidence(bool persisted, string expectedDigest)
    {
        var harness = new ReminderTestHarness();
        ReminderTarget target = ReminderTestHarness.Target("evidence-framing");
        string actorId = ReminderTestHarness.ActorId(target);
        if (persisted)
        {
            ReminderEntry malformed = new("invalid-name", "invalid-token", "invalid-kind", harness.Time.Now.AddHours(1),
                1, target.Domain, target.Aggregate, 3, "widget.resume-payload.v1", "invalid-digest",
                ReminderEntryStatus.Armed, 0, null, harness.Time.Now);
            harness.Source.Set(target);
            harness.SeedItemState(actorId, new(target.Tenant, target.Domain, target.Aggregate, 1, [malformed], []));
        }
        else
        {
            harness.Source.Set(target, ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1)) with { Payload = null! });
        }

        _ = await harness.CreateRegistrar().ConvergeAsync(target);

        harness.ItemState(actorId).ShouldNotBeNull().Quarantine.ShouldHaveSingleItem().EvidenceDigest.ShouldBe(expectedDigest);
    }

    /// <summary>An integer resembling a text length and bytes must retain separate malformed intents across restart.</summary>
    [Fact]
    public async Task OptionalIntentFieldsRetainDistinctQuarantineRecords()
    {
        var harness = new ReminderTestHarness();
        ReminderTarget target = ReminderTestHarness.Target("evidence-framing");
        string actorId = ReminderTestHarness.ActorId(target);
        ReminderIntent malformed = ReminderTestHarness.Intent(target, harness.Time.Now.AddHours(1)) with { Payload = null! };
        harness.Source.Set(target, malformed with { Ordinal = AmbiguousOrdinal });
        (await harness.CreateRegistrar().ConvergeAsync(target)).Quarantined.ShouldBe(1);
        string firstDigest = harness.ItemState(actorId).ShouldNotBeNull().Quarantine.ShouldHaveSingleItem().EvidenceDigest;

        harness.Source.Set(target, malformed with { SourceMessageId = "ABCD" });
        _ = await harness.CreateRegistrar(new ReminderRuntimeStatus()).ConvergeAsync(target);

        ReminderItemState retained = harness.ItemState(actorId).ShouldNotBeNull();
        retained.Quarantine.Count.ShouldBe(2);
        retained.Quarantine.Select(record => record.EvidenceDigest).Distinct().Count().ShouldBe(2);
        retained.Quarantine.ShouldContain(record => record.EvidenceDigest == firstDigest);
        retained.Quarantine.ShouldAllBe(record => record.ReasonCode == "payload-invalid"
            && harness.Disposition(actorId, record.EvidenceDigest) != null);
        harness.Submitter.Calls.ShouldBeEmpty();
    }

    /// <summary>The same ambiguous pair in a restored entry at the same position must remain distinguishable.</summary>
    [Fact]
    public async Task OptionalPersistedEntryFieldsRetainDistinctQuarantineRecords()
    {
        var harness = new ReminderTestHarness();
        ReminderTarget target = ReminderTestHarness.Target("evidence-framing");
        string actorId = ReminderTestHarness.ActorId(target);
        ReminderEntry malformed = new("invalid-name", "invalid-token", "invalid-kind", harness.Time.Now.AddHours(1),
            1, target.Domain, target.Aggregate, 3, "widget.resume-payload.v1", "invalid-digest",
            ReminderEntryStatus.Armed, 0, null, harness.Time.Now);
        harness.Source.Set(target);
        harness.SeedItemState(actorId, new(target.Tenant, target.Domain, target.Aggregate, 1,
            [malformed with { Ordinal = AmbiguousOrdinal }], []));
        _ = await harness.CreateRegistrar().ConvergeAsync(target);
        ReminderItemState first = harness.ItemState(actorId).ShouldNotBeNull();
        string firstDigest = first.Quarantine.ShouldHaveSingleItem().EvidenceDigest;

        harness.SeedItemState(actorId, first with { Entries = [malformed with { SourceMessageId = "ABCD" }] });
        _ = await harness.CreateRegistrar(new ReminderRuntimeStatus()).ConvergeAsync(target);

        ReminderItemState retained = harness.ItemState(actorId).ShouldNotBeNull();
        retained.Entries.ShouldBeEmpty();
        retained.Quarantine.Count.ShouldBe(2);
        retained.Quarantine.Select(record => record.EvidenceDigest).Distinct().Count().ShouldBe(2);
        retained.Quarantine.ShouldContain(record => record.EvidenceDigest == firstDigest);
        retained.Quarantine.ShouldAllBe(record => record.ReasonCode == "stored-entry-invalid"
            && harness.Disposition(actorId, record.EvidenceDigest) != null);
        harness.Submitter.Calls.ShouldBeEmpty();
    }
}
