using System.Text.Json;

using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Creates structured CloudEvent fixtures from the EventStore publisher envelope contract.
/// </summary>
internal static class StructuredCloudEventFixture
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Creates a publisher-shaped structured CloudEvent and returns its serialized bytes and source envelope.
    /// </summary>
    internal static (byte[] Body, EventEnvelope Envelope) Create()
    {
        var envelope = new EventEnvelope(
            MessageId: "01ARZ3NDEKTSV4RRFFQ69G5FB4",
            AggregateId: "work-a",
            AggregateType: "WorkItem",
            TenantId: "tenant-a",
            Domain: "work",
            SequenceNumber: 3,
            GlobalPosition: 42,
            Timestamp: new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero),
            CorrelationId: "01ARZ3NDEKTSV4RRFFQ69G5FB5",
            CausationId: "01ARZ3NDEKTSV4RRFFQ69G5FB6",
            UserId: "user-a",
            DomainServiceVersion: "v1",
            EventTypeName: "Hexalith.Works.Events.WorkItemCreated",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: "must-not-escape"u8.ToArray(),
            Extensions: null);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                specversion = "1.0",
                id = envelope.MessageId,
                source = $"hexalith-eventstore/{envelope.TenantId}/{envelope.Domain}",
                type = envelope.EventTypeName,
                datacontenttype = "application/json",
                data = envelope,
            },
            _jsonOptions);

        return (body, envelope);
    }
}
