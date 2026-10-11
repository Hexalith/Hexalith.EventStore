using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
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
    public static int Main(string[] args)
    {
        if (!AssertLegacyApis()) { return 1; }
        if (args.Length == 2 && args[0] == "--read-new")
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
            Console.WriteLine("PASS: old package reads version-1 folded snapshot object");
            return 0;
        }
        if (args.Length != 2 || args[0] != "--emit-old")
        {
            throw new ArgumentException("Expected --emit-old or --read-new with a snapshot path.");
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
        Console.Write(JsonSerializer.Serialize(new DomainServiceWireResult(false, [], null),
            JsonSerializerOptions.Web));
        return 0;
    }
}

/// <summary>Old invoker implementation with only the original method.</summary>
public sealed class LegacyInvoker : IDomainServiceInvoker
{
    /// <inheritdoc/>
    public Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState,
        CancellationToken cancellationToken = default)
        => Task.FromResult(DomainResult.NoOp());
}

/// <summary>Old async processor implementation with no capture capability.</summary>
public sealed class LegacyProcessor : IAsyncDomainProcessor
{
    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState,
        CancellationToken cancellationToken)
        => Task.FromResult(DomainResult.NoOp());
}
