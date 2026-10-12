using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Events;

namespace FoldedSnapshotCompatibility;

/// <summary>A consumer compiled against the published pre-Story-6.2 packages.</summary>
public static class LegacyConsumer
{
    /// <summary>Exercises the old positional API without referring to any new member.</summary>
    public static bool AssertLegacyApis()
    {
        var command = new CommandEnvelope(
            "01HZX6FKA8B3YTX6MBQ3AYT3PT", "tenant", "domain", "aggregate",
            "Create", [], "01HZX6FKA8B3YTX6MBQ3AYT3PT", null, "user", null);
        var request = new DomainServiceRequest(command, null);
        var (deconstructedCommand, currentState) = request;
        var wire = new DomainServiceWireResult(false, [], null);
        var (isRejection, events, resultPayload) = wire;
        var snapshot = new SnapshotRecord(7, JsonSerializer.Deserialize<JsonElement>("{}"),
            DateTimeOffset.UnixEpoch, "domain", "aggregate", "tenant");
        var (sequence, state, createdAt, domain, aggregateId, tenantId, protection) = snapshot;
        return ReferenceEquals(command, deconstructedCommand) && currentState is null
            && !isRejection && events.Count == 0 && resultPayload is null
            && sequence == 7 && state is JsonElement { ValueKind: JsonValueKind.Object }
            && createdAt == DateTimeOffset.UnixEpoch && domain == "domain"
            && aggregateId == "aggregate" && tenantId == "tenant" && protection is null
            && DomainResult.NoOp().IsNoOp;
    }

    /// <summary>Emits the old wire shape while the published packages are loaded.</summary>
    public static async Task<int> Main(string[] args)
    {
        if (!AssertLegacyApis()) { return 1; }
        if (args.Length == 4 && args[0] == "--read-new")
        {
            SnapshotRecord? newSnapshot = JsonSerializer.Deserialize<SnapshotRecord>(
                File.ReadAllText(args[1]), JsonSerializerOptions.Web);
            if (newSnapshot?.State is not JsonElement { ValueKind: JsonValueKind.Object } state
                || state.TryGetProperty("currentSequence", out _)
                || state.TryGetProperty("events", out _)
                || state.GetProperty("count").GetInt32() != 7)
            {
                throw new InvalidOperationException("The old package could not read the new folded object.");
            }
            DomainServiceRequest? newRequest = JsonSerializer.Deserialize<DomainServiceRequest>(
                File.ReadAllText(args[2]), JsonSerializerOptions.Web);
            if (newRequest?.Command.CommandType != "Create" || newRequest.CurrentState is not null)
            {
                throw new InvalidOperationException("The old package could not read the new capture request.");
            }
            DomainServiceWireResult newWire = await ParseOldBoundedWireAsync(
                await File.ReadAllBytesAsync(args[3]).ConfigureAwait(false)).ConfigureAwait(false);
            if (newWire.IsRejection || newWire.ResultPayload != "new-wire"
                || newWire.Events.Count != 1 || newWire.Events[0].EventTypeName != "compat-event"
                || !newWire.Events[0].Payload.SequenceEqual("{\"count\":7}"u8.ToArray()))
            {
                throw new InvalidOperationException("The published bounded parser could not read the new optional wire result.");
            }
            Console.WriteLine("PASS: old package reads new folded snapshot and capture request; published bounded parser reads new optional wire result");
            return 0;
        }
        if (args.Length != 3 || args[0] != "--emit-old")
        {
            throw new ArgumentException("Expected --emit-old with snapshot/request paths or --read-new with snapshot/request/wire paths.");
        }
        if (typeof(SnapshotRecord).GetProperty("SnapshotEnvelopeVersion") is not null
            || typeof(DomainServiceRequest).GetProperty("IncludePostCommandState") is not null
            || typeof(DomainServiceWireResult).GetProperty("PostCommandStateJson") is not null)
        {
            throw new InvalidOperationException("The published baseline already contains Story 6.2 members.");
        }
        File.WriteAllText(args[1], JsonSerializer.Serialize(new SnapshotRecord(
            7, JsonSerializer.Deserialize<JsonElement>("{\"count\":7}"),
            DateTimeOffset.UnixEpoch, "domain", "aggregate", "tenant"),
            JsonSerializerOptions.Web));
        File.WriteAllText(args[2], JsonSerializer.Serialize(new DomainServiceRequest(
            new CommandEnvelope("01HZX6FKA8B3YTX6MBQ3AYT3PT", "tenant", "domain", "aggregate",
                "Create", [], "01HZX6FKA8B3YTX6MBQ3AYT3PT", null, "user", null), null),
            JsonSerializerOptions.Web));
        Console.Write(JsonSerializer.Serialize(new DomainServiceWireResult(false, [], null),
            JsonSerializerOptions.Web));
        return 0;
    }

    private static async Task<DomainServiceWireResult> ParseOldBoundedWireAsync(byte[] wire)
    {
        Type parserType = typeof(SnapshotRecord).Assembly.GetType(
            "Hexalith.EventStore.Server.DomainServices.BoundedV1DomainResponseParser", throwOnError: true)!;
        ConstructorInfo constructor = parserType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        using var input = new MemoryStream(wire, writable: false);
        using IDisposable parser = (IDisposable)constructor.Invoke([input, CancellationToken.None, 1024 * 1024, 1000]);
        MethodInfo parse = parserType.GetMethod("ParseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return await (Task<DomainServiceWireResult>)parse.Invoke(parser, null)!;
    }
}
