// Legacy IDomainProcessor implementation. See CounterAggregate for the fluent API approach.

using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Sample.Counter;
/// <summary>
/// Domain processor for the Counter aggregate. Implements the pure function contract:
/// (Command, CurrentState?) -> DomainResult with three possible outcomes:
/// success events, rejection events, or no-op.
/// </summary>
public sealed class CounterProcessor : IDomainProcessor {
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState) {
        ArgumentNullException.ThrowIfNull(command);

        int currentCount = RehydrateCount(currentState);

        // Deserialize command.Payload to the concrete command type (D3 pattern).
        // Even for parameterless commands, this validates the payload is well-formed JSON.
        DomainResult result = command.CommandType switch {
            nameof(IncrementCounter) => HandleIncrement(
                DeserializePayload<IncrementCounter>(command)),
            nameof(DecrementCounter) => HandleDecrement(
                DeserializePayload<DecrementCounter>(command),
                currentCount),
            nameof(ResetCounter) => HandleReset(
                DeserializePayload<ResetCounter>(command),
                currentCount),
            _ => throw new InvalidOperationException($"Unknown command type: '{command.CommandType}'."),
        };

        return Task.FromResult(result);
    }

    private static T DeserializePayload<T>(CommandEnvelope command) =>
        command.Payload.Length == 0
            ? throw new InvalidOperationException(
                $"Command '{command.CommandType}' has an empty payload. Expected valid JSON for {typeof(T).Name}.")
            : JsonSerializer.Deserialize<T>(command.Payload)
              ?? throw new InvalidOperationException(
                  $"Failed to deserialize payload for command '{command.CommandType}' to {typeof(T).Name}.");

    private static DomainResult HandleIncrement(IncrementCounter _)
        => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });

    private static DomainResult HandleDecrement(DecrementCounter _, int currentCount) {
        if (currentCount == 0) {
            return DomainResult.Rejection(new IRejectionEvent[] { new CounterCannotGoNegative() });
        }

        return DomainResult.Success(new IEventPayload[] { new CounterDecremented() });
    }

    private static DomainResult HandleReset(ResetCounter _, int currentCount) {
        if (currentCount == 0) {
            return DomainResult.NoOp();
        }

        return DomainResult.Success(new IEventPayload[] { new CounterReset() });
    }

    private static int RehydrateCount(object? currentState) {
        if (currentState is null) {
            return 0;
        }

        if (currentState is CounterState typedState) {
            return typedState.Count;
        }

        if (currentState is DomainServiceCurrentState snapshotAwareState) {
            return RehydrateCount(snapshotAwareState);
        }

        if (currentState is JsonElement json) {
            return RehydrateCountFromJson(json);
        }

        return RehydrateCountFromObjectEnumerable(currentState);
    }

    private static int RehydrateCount(DomainServiceCurrentState currentState) {
        int countValue = RehydrateCount(currentState.SnapshotState);

        foreach (EventEnvelope envelope in currentState.Events) {
            ApplyEventToCount(envelope.Metadata.EventTypeName, ref countValue);
        }

        return Math.Max(0, countValue);
    }

    private static int RehydrateCountFromJson(JsonElement json) {
        if (IsDomainServiceCurrentState(json)) {
            DomainServiceCurrentState currentState = json.Deserialize<DomainServiceCurrentState>(WebJsonOptions)
                ?? throw new InvalidOperationException("Unable to deserialize snapshot-aware current state payload.");
            return RehydrateCount(currentState);
        }

        if (json.ValueKind == JsonValueKind.Object) {
            if (json.TryGetProperty("count", out JsonElement countElement)
                && countElement.ValueKind == JsonValueKind.Number
                && countElement.TryGetInt32(out int count)) {
                return count;
            }

            return 0;
        }

        if (json.ValueKind != JsonValueKind.Array) {
            return 0;
        }

        int countValue = 0;
        foreach (JsonElement eventElement in json.EnumerateArray()) {
            if (eventElement.ValueKind != JsonValueKind.Object) {
                continue;
            }

            if (!eventElement.TryGetProperty("eventTypeName", out JsonElement eventTypeElement)
                || eventTypeElement.ValueKind != JsonValueKind.String) {
                continue;
            }

            string? eventTypeName = eventTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(eventTypeName)) {
                continue;
            }

            ApplyEventToCount(eventTypeName, ref countValue);
        }

        return Math.Max(0, countValue);
    }

    private static int RehydrateCountFromObjectEnumerable(object currentState) {
        if (currentState is DomainServiceCurrentState snapshotAwareState) {
            return RehydrateCount(snapshotAwareState);
        }

        if (currentState is not System.Collections.IEnumerable events) {
            return 0;
        }

        int countValue = 0;
        foreach (object? evt in events) {
            if (evt is null) {
                continue;
            }

            string? eventTypeName = evt is EventEnvelope envelope
                ? envelope.Metadata.EventTypeName
                : evt.GetType().GetProperty("EventTypeName")?.GetValue(evt) as string;
            if (string.IsNullOrWhiteSpace(eventTypeName)) {
                eventTypeName = evt.GetType().FullName ?? evt.GetType().Name;
            }

            ApplyEventToCount(eventTypeName, ref countValue);
        }

        return Math.Max(0, countValue);
    }

    private static bool IsDomainServiceCurrentState(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("currentSequence", out _)
        && json.TryGetProperty("events", out _);

    private static void ApplyEventToCount(string eventTypeName, ref int countValue) {
        if (eventTypeName.EndsWith("CounterIncremented", StringComparison.Ordinal)) {
            countValue++;
            return;
        }

        if (eventTypeName.EndsWith("CounterDecremented", StringComparison.Ordinal)) {
            countValue = Math.Max(0, countValue - 1);
            return;
        }

        if (eventTypeName.EndsWith("CounterReset", StringComparison.Ordinal)) {
            countValue = 0;
        }
    }
}
