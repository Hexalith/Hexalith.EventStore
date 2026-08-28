using System.Text;

using Hexalith.EventStore.Operations.Capture;
using Hexalith.EventStore.Operations.Models;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies replay-safe structured CloudEvent identity extraction.
/// </summary>
public sealed class DeadLetterEnvelopeParserTests
{
    /// <summary>Verifies complete EventStore identity is replayable without exposing the payload.</summary>
    [Fact]
    public void StructuredCloudEventProducesReplayableIdentity()
    {
        byte[] body = Encoding.UTF8.GetBytes("""
            {
              "specversion":"1.0",
              "id":"01ARZ3NDEKTSV4RRFFQ69G5FB4",
              "source":"urn:hexalith:eventstore",
              "type":"work.event",
              "data":{
                "messageId":"01ARZ3NDEKTSV4RRFFQ69G5FB4",
                "tenantId":"tenant-a",
                "domain":"work",
                "aggregateId":"work-a",
                "correlationId":"01ARZ3NDEKTSV4RRFFQ69G5FB5",
                "eventName":"WorkItemCreated",
                "payload":"must-not-escape"
              }
            }
            """);

        (DeadLetterSafeIdentity identity, string hash) = DeadLetterEnvelopeParser.Parse(body);

        identity.IsReplayable.ShouldBeTrue();
        identity.MessageId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FB4");
        identity.TenantId.ShouldBe("tenant-a");
        identity.Domain.ShouldBe("work");
        identity.AggregateId.ShouldBe("work-a");
        identity.CorrelationId.ShouldBe("01ARZ3NDEKTSV4RRFFQ69G5FB5");
        identity.EventType.ShouldBe("WorkItemCreated");
        hash.Length.ShouldBe(64);
        identity.ToString().ShouldNotContain("must-not-escape");
    }

    /// <summary>Verifies malformed bodies remain deduplicated but replay-ineligible.</summary>
    [Fact]
    public void MalformedBodyProducesStableReplayIneligibleIdentity()
    {
        byte[] body = "{"u8.ToArray();

        (DeadLetterSafeIdentity first, string firstHash) = DeadLetterEnvelopeParser.Parse(body);
        (DeadLetterSafeIdentity second, string secondHash) = DeadLetterEnvelopeParser.Parse(body);

        first.ShouldBe(second);
        firstHash.ShouldBe(secondHash);
        first.MessageId.ShouldStartWith("unidentified-");
        first.IsReplayable.ShouldBeFalse();
    }

    /// <summary>Verifies ambiguous, incomplete, conflicting, and oversized identities fail replay closed.</summary>
    [Theory]
    [MemberData(nameof(InvalidStructuredCloudEvents))]
    public void InvalidStructuredCloudEventRemainsStableButReplayIneligible(string json)
    {
        byte[] body = Encoding.UTF8.GetBytes(json);

        (DeadLetterSafeIdentity first, string firstHash) = DeadLetterEnvelopeParser.Parse(body);
        (DeadLetterSafeIdentity second, string secondHash) = DeadLetterEnvelopeParser.Parse(body);

        first.ShouldBe(second);
        firstHash.ShouldBe(secondHash);
        first.MessageId.ShouldStartWith("unidentified-");
        first.IsReplayable.ShouldBeFalse();
    }

    /// <summary>Gets adversarial structured-envelope cases.</summary>
    public static TheoryData<string> InvalidStructuredCloudEvents()
    {
        string oversized = new('x', DeadLetterSafeIdentity.MaxValueLength + 1);
        return new TheoryData<string>
        {
            ValidJson().Replace("\"specversion\":\"1.0\"", "\"specversion\":\"0.3\"", StringComparison.Ordinal),
            ValidJson().Replace("\"source\":\"urn:hexalith:eventstore\",", string.Empty, StringComparison.Ordinal),
            ValidJson().Replace("\"id\":\"message-a\"", "\"id\":\"message-b\"", StringComparison.Ordinal),
            ValidJson().Replace("\"id\":\"message-a\"", "\"id\":\"message-a\",\"ID\":\"message-a\"", StringComparison.Ordinal),
            ValidJson().Replace("\"tenantId\":\"tenant-a\"", "\"tenantId\":\"tenant-a\",\"TENANTID\":\"tenant-b\"", StringComparison.Ordinal),
            ValidJson().Replace("\"data\":{", "\"data\":[],\"unused\":{", StringComparison.Ordinal),
            ValidJson().Replace("\"aggregateId\":\"work-a\"", $"\"aggregateId\":\"{oversized}\"", StringComparison.Ordinal),
            ValidJson().Replace("\"eventName\":\"WorkItemCreated\"", "\"eventName\":\"WorkItemCreated\",\"eventType\":\"Other\"", StringComparison.Ordinal),
        };
    }

    private static string ValidJson() => """
        {
          "specversion":"1.0",
          "id":"message-a",
          "source":"urn:hexalith:eventstore",
          "type":"work.event",
          "data":{
            "messageId":"message-a",
            "tenantId":"tenant-a",
            "domain":"work",
            "aggregateId":"work-a",
            "correlationId":"correlation-a",
            "eventName":"WorkItemCreated"
          }
        }
        """;
}
