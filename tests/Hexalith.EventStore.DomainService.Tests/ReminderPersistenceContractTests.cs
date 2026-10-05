using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Pins the version-one durable key layout and JSON consumed after restart or restore.</summary>
public sealed class ReminderPersistenceContractTests
{
    private const string ActorType = "WidgetReminderActor";
    private const string ActorId = "wra-HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG";
    private const string ScheduleToken = "wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0";
    private const string Name = "date-" + ScheduleToken;
    private const string Digest = "HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG";
    private const string ItemJson = """
        {"tenant":"tenant-a","domain":"widget","aggregate":"item-1","version":7,"entries":[{"reminderName":"date-wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0","scheduleToken":"wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0","kind":"works.date-resume.v1","dueUtc":"2026-10-01T09:30:00+00:00","scheduleRevision":1,"sourceDomain":"widget","sourceAggregate":"item-1","sourceSequence":3,"payloadType":"widget.resume-payload.v1","payloadDigest":"HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG","status":"Retrying","attempts":2,"lastReasonCode":"submission-uncertain","updatedAt":"2026-10-01T09:30:00+00:00"}],"quarantine":[{"evidenceDigest":"HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG","reasonCode":"witness-collision","reminderName":"date-wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0","recordedAt":"2026-10-01T09:30:00+00:00"}]}
        """;
    private const string DispositionJson = """
        {"tenant":"tenant-a","actorId":"wra-HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG","subject":"date-wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0","disposition":"Submitted","reasonCode":"receipt","effectId":"HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG","targetDisposition":"Success","replayed":true,"attempts":2,"recordedAt":"2026-10-01T09:30:00+00:00"}
        """;
    private const string RegistryJson = """
        {"tenants":["tenant-a","tenant-b"]}
        """;
    private const string CandidatesJson = """
        {"tenant":"tenant-a","candidates":[{"domain":"widget","aggregate":"Item-1","actorId":"wra-750NNZXCE0515AR0KPZV9Y1ZWYEYKXFT0ERPXMXV6B9W0V27MFPG"},{"domain":"widget","aggregate":"item-1","actorId":"wra-HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG"}]}
        """;
    private static readonly DateTimeOffset Instant = new(2026, 10, 1, 9, 30, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions = ConfiguredSerializerOptions();

    /// <summary>The version-one control, tenant, item, and disposition key literals remain stable.</summary>
    [Fact]
    public void PersistedKeysAreStable()
    {
        ReminderStateKeys.TenantRegistry(ActorType).ShouldBe("eventstore:reminders:v1:WidgetReminderActor:control:tenants");
        ReminderStateKeys.TenantCandidates(ActorType, "tenant-a").ShouldBe("eventstore:reminders:v1:WidgetReminderActor:tenant:tenant-a:candidates");
        ReminderStateKeys.Item(ActorType, ActorId).ShouldBe("eventstore:reminders:v1:WidgetReminderActor:item:wra-HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG");
        ReminderStateKeys.Disposition(ActorType, ActorId, Name).ShouldBe("eventstore:reminders:v1:WidgetReminderActor:item:wra-HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG:disposition:date-wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0");
    }

    /// <summary>The configured Dapr writer and reader preserve the fixed item and disposition documents.</summary>
    [Fact]
    public void PersistedJsonMatchesVersionOneFixtures()
    {
        ReminderEntry entry = Entry();
        var quarantine = new ReminderQuarantineRecord(Digest, "witness-collision", Name, Instant);
        var item = new ReminderItemState("tenant-a", "widget", "item-1", 7, [entry], [quarantine]);
        ReminderDispositionRecord disposition = Disposition();

        JsonSerializer.Serialize(item, SerializerOptions).ShouldBe(ItemJson);
        JsonSerializer.Serialize(disposition, SerializerOptions).ShouldBe(DispositionJson);

        ReminderItemState restored = JsonSerializer.Deserialize<ReminderItemState>(ItemJson, SerializerOptions).ShouldNotBeNull();
        restored.Tenant.ShouldBe("tenant-a");
        restored.Domain.ShouldBe("widget");
        restored.Aggregate.ShouldBe("item-1");
        restored.Version.ShouldBe(7);
        restored.Entries.ShouldBe([entry]);
        restored.Quarantine.ShouldBe([quarantine]);
        JsonSerializer.Deserialize<ReminderDispositionRecord>(DispositionJson, SerializerOptions).ShouldBe(disposition);
    }

    /// <summary>Discovery registry and candidate documents retain their literal configured Dapr JSON contracts.</summary>
    [Fact]
    public void PersistedDiscoveryJsonMatchesVersionOneFixtures()
    {
        var registry = new ReminderTenantRegistry(["tenant-a", "tenant-b"]);
        ReminderCandidate[] rows =
        [
            new("widget", "Item-1", "wra-750NNZXCE0515AR0KPZV9Y1ZWYEYKXFT0ERPXMXV6B9W0V27MFPG"),
            new("widget", "item-1", ActorId),
        ];
        var candidates = new ReminderTenantCandidates("tenant-a", rows);

        JsonSerializer.Serialize(registry, SerializerOptions).ShouldBe(RegistryJson);
        JsonSerializer.Serialize(candidates, SerializerOptions).ShouldBe(CandidatesJson);

        ReminderTenantRegistry restoredRegistry = JsonSerializer.Deserialize<ReminderTenantRegistry>(RegistryJson, SerializerOptions).ShouldNotBeNull();
        restoredRegistry.Tenants.ShouldBe(["tenant-a", "tenant-b"]);
        ReminderTenantCandidates restoredCandidates = JsonSerializer.Deserialize<ReminderTenantCandidates>(CandidatesJson, SerializerOptions).ShouldNotBeNull();
        restoredCandidates.Tenant.ShouldBe("tenant-a");
        restoredCandidates.Candidates.ShouldBe(rows);
    }

    /// <summary>Every persisted witness lifecycle name remains a JSON string with its version-one spelling.</summary>
    [Theory]
    [InlineData(0, "Pending")]
    [InlineData(1, "Armed")]
    [InlineData(2, "Retrying")]
    [InlineData(3, "Quarantined")]
    public void PersistedEntryStatusNamesAreStable(int status, string expected)
    {
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(Entry() with { Status = (ReminderEntryStatus)status }, SerializerOptions));
        json.RootElement.GetProperty("status").GetString().ShouldBe(expected);
    }

    /// <summary>Every audit disposition retains its version-one JSON spelling.</summary>
    [Theory]
    [InlineData(ReminderDisposition.Registered, "Registered")]
    [InlineData(ReminderDisposition.Submitted, "Submitted")]
    [InlineData(ReminderDisposition.Retrying, "Retrying")]
    [InlineData(ReminderDisposition.Stale, "Stale")]
    [InlineData(ReminderDisposition.Cancelled, "Cancelled")]
    [InlineData(ReminderDisposition.Denied, "Denied")]
    [InlineData(ReminderDisposition.Quarantined, "Quarantined")]
    public void PersistedDispositionNamesAreStable(ReminderDisposition disposition, string expected)
    {
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(Disposition() with { Disposition = disposition }, SerializerOptions));
        json.RootElement.GetProperty("disposition").GetString().ShouldBe(expected);
    }

    /// <summary>Every target receipt disposition retained in the audit remains a named JSON string.</summary>
    [Theory]
    [InlineData(TrustedEffectDisposition.Success, "Success")]
    [InlineData(TrustedEffectDisposition.Rejection, "Rejection")]
    [InlineData(TrustedEffectDisposition.NoOp, "NoOp")]
    public void PersistedTargetDispositionNamesAreStable(TrustedEffectDisposition disposition, string expected)
    {
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(Disposition() with { TargetDisposition = disposition }, SerializerOptions));
        json.RootElement.GetProperty("targetDisposition").GetString().ShouldBe(expected);
    }

    private static JsonSerializerOptions ConfiguredSerializerOptions()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.AddEventStoreDomainService(typeof(ReminderPersistenceContractTests).Assembly);
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        return provider.GetRequiredService<DaprClient>().JsonSerializerOptions;
    }

    private static ReminderEntry Entry() => new(Name, ScheduleToken, EffectKindCatalog.DateResume, Instant, 1,
        "widget", "item-1", 3, "widget.resume-payload.v1", Digest, ReminderEntryStatus.Retrying, 2, "submission-uncertain", Instant);

    private static ReminderDispositionRecord Disposition() => new("tenant-a", ActorId, Name, ReminderDisposition.Submitted,
        "receipt", Digest, TrustedEffectDisposition.Success, true, 2, Instant);
}
