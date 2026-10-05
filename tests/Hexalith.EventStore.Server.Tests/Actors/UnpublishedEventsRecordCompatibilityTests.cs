using System.Text.Json;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Checks the prior drain-record API and current actor-state JSON remain compatible.</summary>
public sealed class UnpublishedEventsRecordCompatibilityTests
{
    /// <summary>Checks twelve-member source calls bind the preserved signatures and retain every value.</summary>
    [Fact]
    public void LegacyTwelveMemberConstructionAndDeconstructionPreserveValues()
    {
        DateTimeOffset failedAt = DateTimeOffset.UnixEpoch;
        var record = new UnpublishedEventsRecord("correlation", 1, 3, 3, "command", true,
            failedAt, 2, "failure", "message", true, failedAt);
        var (correlation, start, end, count, command, rejection, time, retry, failure, message, deadLettered, reminder) = record;
        correlation.ShouldBe("correlation");
        start.ShouldBe(1);
        end.ShouldBe(3);
        count.ShouldBe(3);
        command.ShouldBe("command");
        rejection.ShouldBeTrue();
        time.ShouldBe(failedAt);
        retry.ShouldBe(2);
        failure.ShouldBe("failure");
        message.ShouldBe("message");
        deadLettered.ShouldBeTrue();
        reminder.ShouldBe(failedAt);
        record.CausationId.ShouldBeNull();
    }

    /// <summary>Checks current JSON retains causation and old JSON selects its null default.</summary>
    [Fact]
    public void JsonRoundTripSelectsCurrentConstructorAndPreservesCausation()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var record = new UnpublishedEventsRecord("correlation", 1, 1, 1, "command", false,
            DateTimeOffset.UnixEpoch, 0, null, CausationId: "causation");
        string json = JsonSerializer.Serialize(record, options);
        JsonSerializer.Deserialize<UnpublishedEventsRecord>(json, options).ShouldBe(record);
        string legacy = json.Replace(",\"causationId\":\"causation\"", string.Empty, StringComparison.Ordinal);
        JsonSerializer.Deserialize<UnpublishedEventsRecord>(legacy, options)!.CausationId.ShouldBeNull();
    }
}
