using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace PublishedConsumer;

/// <summary>Uses the SDK's real command-state rehydrator before exposing observed state or producing an event.</summary>
public sealed class CounterProcessor : DomainProcessorBase<CounterState>
{
    /// <summary>Gets the value reconstructed by the SDK before handling.</summary>
    public int ObservedCount { get; private set; }
    /// <summary>Gets the number of actual command handler invocations.</summary>
    public static int HandleCalls { get; private set; }

    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, CounterState? currentState)
    {
        ObservedCount = currentState?.Count ?? 0;
        HandleCalls++;
        DateTimeOffset? due = null;
        if (command.CommandType == "Schedule")
        {
            using var payload = System.Text.Json.JsonDocument.Parse(command.Payload);
            due = payload.RootElement.GetProperty("dueUtc").GetDateTimeOffset();
        }
        return Task.FromResult(command.CommandType == "Inspect" ? DomainResult.NoOp() : DomainResult.Success([new CounterIncremented(DueUtc: due)]));
    }
}
