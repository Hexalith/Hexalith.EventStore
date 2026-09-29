using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Server.Events;

if (args.Length != 3 || args[0] is not ("write" or "read"))
{
    throw new ArgumentException("Usage: Probe <write|read> <Dapr HTTP base URL> <unique key prefix>");
}

string mode = args[0];
string baseUrl = args[1].TrimEnd('/');
string prefix = args[2];
JsonSerializerOptions options = JsonSerializerOptions.Web;
using HttpClient client = new();

if (mode == "write")
{
    EventEnvelope eventRecord = new(
        MessageId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        AggregateId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        AggregateType: "rollback-probe",
        TenantId: "throwaway-tenant",
        Domain: "rollback-probe",
        SequenceNumber: 1,
        GlobalPosition: 0,
        Timestamp: new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
        CorrelationId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        CausationId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        UserId: "throwaway-user",
        DomainServiceVersion: "1.0.0",
        EventTypeName: "RollbackProbe.Incremented",
        MetadataVersion: 1,
        SerializationFormat: "json",
        Payload: Encoding.UTF8.GetBytes("{\"count\":7}"),
        Extensions: new Dictionary<string, string> { ["probe"] = "p1r-rollback" });
    SnapshotRecord snapshotRecord = new(
        SequenceNumber: 1,
        State: JsonSerializer.SerializeToElement(new { Count = 7 }, options),
        CreatedAt: new DateTimeOffset(2026, 9, 27, 0, 0, 1, TimeSpan.Zero),
        Domain: "rollback-probe",
        AggregateId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        TenantId: "throwaway-tenant");
    object[] entries =
    [
        new { key = prefix + "-event", value = eventRecord },
        new { key = prefix + "-snapshot", value = snapshotRecord },
    ];
    using HttpResponseMessage write = await client.PutAsync(
        $"{baseUrl}/v1.0/state/rollbackstore",
        new StringContent(JsonSerializer.Serialize(entries, options), Encoding.UTF8, "application/json")).ConfigureAwait(false);
    write.EnsureSuccessStatusCode();
}

byte[] eventBytes = await client.GetByteArrayAsync($"{baseUrl}/v1.0/state/rollbackstore/{prefix}-event").ConfigureAwait(false);
byte[] snapshotBytes = await client.GetByteArrayAsync($"{baseUrl}/v1.0/state/rollbackstore/{prefix}-snapshot").ConfigureAwait(false);
EventEnvelope restoredEvent = JsonSerializer.Deserialize<EventEnvelope>(eventBytes, options)
    ?? throw new InvalidOperationException("The persisted event was null.");
SnapshotRecord restoredSnapshot = JsonSerializer.Deserialize<SnapshotRecord>(snapshotBytes, options)
    ?? throw new InvalidOperationException("The persisted snapshot was null.");

if (restoredEvent.SequenceNumber != 1
    || restoredEvent.TenantId != "throwaway-tenant"
    || restoredEvent.EventTypeName != "RollbackProbe.Incremented"
    || Encoding.UTF8.GetString(restoredEvent.Payload) != "{\"count\":7}"
    || restoredEvent.Extensions?["probe"] != "p1r-rollback"
    || restoredSnapshot.SequenceNumber != 1
    || restoredSnapshot.TenantId != "throwaway-tenant"
    || restoredSnapshot.State is not JsonElement state
    || state.GetProperty("count").GetInt32() != 7)
{
    throw new InvalidOperationException("Persisted event or snapshot semantics changed across the package boundary.");
}

Console.WriteLine($"{mode}: event_sha256={Convert.ToHexStringLower(SHA256.HashData(eventBytes))} snapshot_sha256={Convert.ToHexStringLower(SHA256.HashData(snapshotBytes))} sequence={restoredEvent.SequenceNumber} snapshot_count={state.GetProperty("count").GetInt32()}");
