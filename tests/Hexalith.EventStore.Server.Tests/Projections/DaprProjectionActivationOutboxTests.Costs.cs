using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class DaprProjectionActivationOutboxTests
{
    [Fact]
    public async Task OwningOutbox_BulkScanChargesEveryRawShardAndOccupiedEntryBeforeSelectingOneDueItem()
    {
        using var dapr = new RecordingDaprClient { PersistTypedState = true };
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        var outbox = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), clock);
        for (int index = 0; index < 100; index++)
            await outbox.EnsureAsync(new("tenant-a", "counter", "aggregate-" + index)).ConfigureAwait(true);
        IReadOnlyList<BulkStateItem> raw = await dapr.GetBulkStateAsync("statestore",
            [.. Enumerable.Range(0, 64).Select(static value => $"projection-activation:outbox:v1:{value:x2}")], 8, null!, CancellationToken.None).ConfigureAwait(true);
        long bytes = raw.Sum(static item => item.Value is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(item.Value));
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await outbox.GetDueAsync(clock.GetUtcNow(), 1).ConfigureAwait(true)).Count.ShouldBe(1);
        cost[ProjectionDeliveryCost.ActivationScans].ShouldBe(1);
        cost[ProjectionDeliveryCost.ActivationShardReads].ShouldBe(64);
        cost[ProjectionDeliveryCost.ActivationEntries].ShouldBe(100);
        cost[ProjectionDeliveryCost.ActivationReadBytes].ShouldBe(bytes);
        cost[ProjectionDeliveryCost.MetadataReadBytes].ShouldBe(bytes);
        cost[ProjectionDeliveryCost.StateRpcAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.ActivationWriteAttempts].ShouldBe(0);
    }

    [Fact]
    public async Task OwningOutbox_ChargesExactRawLedgerReadsAndEveryFailedConditionalWrite()
    {
        var state = new Dictionary<string, ProjectionActivationLedger>(StringComparer.Ordinal);
        DaprClient dapr = CreateStatefulDaprClient(state);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        var outbox = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), clock);
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-a");
        await outbox.EnsureAsync(identity).ConfigureAwait(true);
        ProjectionActivationLedger before = state.Values.Single();
        long originalBytes = JsonSerializer.SerializeToUtf8Bytes(before, JsonSerializerOptions.Web).Length;
        using (ProjectionDeliveryCostScope read = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            (await outbox.GetAsync(identity).ConfigureAwait(true)).ShouldBe(before.Items.Single());
            read[ProjectionDeliveryCost.ActivationShardReads].ShouldBe(1);
            read[ProjectionDeliveryCost.ActivationEntries].ShouldBe(1);
            read[ProjectionDeliveryCost.ActivationReadBytes].ShouldBe(originalBytes);
            read[ProjectionDeliveryCost.MetadataReadBytes].ShouldBe(originalBytes);
        }
        dapr.TrySaveByteStateAsync("statestore", Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(),
            Arg.Any<StateOptions?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(false);
        using ProjectionDeliveryCostScope failed = ProjectionDeliveryCostDiagnostics.BeginScope();
        await Should.ThrowAsync<InvalidOperationException>(() => outbox.EnsureAsync(identity)).ConfigureAwait(true);
        failed.Dispose();
        ProjectionActivationLedger proposed = new([before.Items.Single() with { Revision = 2 }]) { LastRevision = 2 };
        long proposedBytes = JsonSerializer.SerializeToUtf8Bytes(proposed, JsonSerializerOptions.Web).Length;
        failed[ProjectionDeliveryCost.ActivationShardReads].ShouldBe(5);
        failed[ProjectionDeliveryCost.ActivationReadBytes].ShouldBe(5 * originalBytes);
        failed[ProjectionDeliveryCost.ActivationWriteAttempts].ShouldBe(5);
        failed[ProjectionDeliveryCost.ActivationWriteBytes].ShouldBe(5 * proposedBytes);
        failed[ProjectionDeliveryCost.MetadataSerializationBytes].ShouldBe(5 * proposedBytes);
        failed[ProjectionDeliveryCost.MutationBytes].ShouldBe(5 * proposedBytes);
        failed[ProjectionDeliveryCost.StateRpcAttempts].ShouldBe(10);
        state.Values.Single().ShouldBe(before);
        foreach (NSubstitute.Core.ICall call in dapr.ReceivedCalls().Where(static item => item.GetMethodInfo().Name == nameof(DaprClient.TrySaveByteStateAsync)))
            ((StateOptions)call.GetArguments()[4]!).Concurrency.ShouldBe(ConcurrencyMode.FirstWrite);
    }
}
