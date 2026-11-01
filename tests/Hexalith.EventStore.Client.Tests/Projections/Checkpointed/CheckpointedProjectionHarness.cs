using Hexalith.EventStore.Client.Projections.Checkpointed;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>One persisted record, its authoritative stream, and factories for replica-local writers.</summary>
internal sealed class CheckpointedProjectionHarness {
    public const string StoreName = "checkpointed-store";
    public const string Key = "checkpointed:aggregate-1";

    public InMemoryReadModelStore Store { get; } = new();

    public CheckpointedHistoryGateway Gateway { get; } = new();

    public CheckpointedTestTimeProvider Clock { get; } = new();

    public CheckpointedTestTransition Transition { get; } = new();

    public static CheckpointedProjectionStream Stream { get; } = new(
        CheckpointedHistoryGateway.Tenant,
        CheckpointedHistoryGateway.Domain,
        CheckpointedHistoryGateway.Aggregate);

    public CheckpointedProjectionRecord<CheckpointedTestState>? Record
        => Store.Snapshot<CheckpointedProjectionRecord<CheckpointedTestState>>(StoreName, Key);

    public static CheckpointedProjectionOptions Options(string workerId) => new() {
        WorkerId = workerId,
        LeaseDuration = TimeSpan.FromSeconds(30),
        HistoryPageSize = 2,
    };

    public static CheckpointedProjectionEvent Delivery(long sequence, string? value = null, string? messageId = null)
        => new(
            Stream,
            sequence,
            new CheckpointedProjectionEvidence(
                messageId ?? CheckpointedHistoryGateway.MessageIdOf(sequence),
                CheckpointedHistoryGateway.EventType,
                CheckpointedHistoryGateway.FingerprintOf(sequence, value)),
            DateTimeOffset.UnixEpoch.AddMinutes(sequence),
            $"correlation-{sequence}") {
            DecodedEvent = value ?? $"value-{sequence}",
        };

    public CheckpointedProjectionWriter<CheckpointedTestState> Writer(string workerId = "replica-a", bool withHistory = true)
        => new(
            Store,
            Transition,
            withHistory ? new CheckpointedProjectionHistory(Gateway, Options(workerId)) : null,
            Options(workerId),
            Clock);

    public CheckpointedProjectionRecoveryWorker<CheckpointedTestState> Worker(string workerId)
        => new(Store, Transition, new CheckpointedProjectionHistory(Gateway, Options(workerId)), Options(workerId), Clock);

    public Task<CheckpointedProjectionResult<CheckpointedTestState>> ApplyAsync(
        long sequence,
        string? value = null,
        string? messageId = null,
        string workerId = "replica-a",
        bool withHistory = true)
        => Writer(workerId, withHistory).ApplyAsync(StoreName, Key, Delivery(sequence, value, messageId), TestContext.Current.CancellationToken);

    public async Task SeedHealthyThroughAsync(long sequence) {
        Gateway.AppendRange(1, sequence);
        for (long current = 1; current <= sequence; current++) {
            (await ApplyAsync(current)).Outcome.ShouldBe(CheckpointedProjectionOutcome.Applied);
        }
    }
}
