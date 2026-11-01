using Dapr.Client;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class DaprProjectionActivationOutboxTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovedActivationNeverReusesRevisionAcrossRestartIncludingLegacyLedgers(bool legacy)
    {
        var state = new Dictionary<string, ProjectionActivationLedger>(StringComparer.Ordinal);
        DaprClient dapr = CreateStatefulDaprClient(state);
        var clock = new FakeTimeProvider();
        var outbox = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), clock);
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-a");
        await outbox.EnsureAsync(identity);
        string key = state.Keys.Single();
        if (legacy)
        {
            // Legacy JSON lacks the counter. Removal must first preserve its live revision.
            state[key] = new ProjectionActivationLedger([state[key].Items.Single() with { Revision = 7 }]);
        }
        ProjectionActivationWorkItem sampled = (await outbox.GetAsync(identity)).ShouldNotBeNull();
        await outbox.CompleteAsync(sampled);
        state[key].Items.ShouldBeEmpty();
        state[key].LastRevision.ShouldBe(sampled.Revision);
        var restarted = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), clock);

        await restarted.EnsureAsync(identity);

        ProjectionActivationWorkItem rearmed = (await restarted.GetAsync(identity)).ShouldNotBeNull();
        rearmed.Revision.ShouldBe(sampled.Revision + 1);
        await restarted.CompleteAsync(sampled);
        await restarted.DeferAsync(sampled, clock.GetUtcNow().AddHours(1));
        (await restarted.GetAsync(identity)).ShouldBe(rearmed);
    }

    [Fact]
    public async Task ExhaustedRevisionRefusesEnsureWithoutWrappingOrMutatingPersistedWork()
    {
        var state = new Dictionary<string, ProjectionActivationLedger>(StringComparer.Ordinal);
        DaprClient dapr = CreateStatefulDaprClient(state);
        var outbox = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), new FakeTimeProvider());
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-a");
        await outbox.EnsureAsync(identity);
        string key = state.Keys.Single();
        state[key] = state[key] with { LastRevision = long.MaxValue };
        ProjectionActivationLedger before = state[key];

        await Should.ThrowAsync<OverflowException>(() => outbox.EnsureAsync(identity));

        state[key].ShouldBeSameAs(before);
    }

    [Fact]
    public async Task NewEnsure_FencesStaleCompletionAndDeferral() {
        var state = new Dictionary<string, ProjectionActivationLedger>(StringComparer.Ordinal);
        DaprClient daprClient = CreateStatefulDaprClient(state);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var outbox = new DaprProjectionActivationOutbox(
            daprClient,
            Options.Create(new ProjectionOptions()),
            timeProvider);
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-a");

        await outbox.EnsureAsync(identity);
        ProjectionActivationWorkItem first = (await outbox.GetAsync(identity)).ShouldNotBeNull();
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await outbox.EnsureAsync(identity);
        ProjectionActivationWorkItem second = (await outbox.GetAsync(identity)).ShouldNotBeNull();

        second.Revision.ShouldBe(first.Revision + 1);
        second.NextDueUtc.ShouldBe(timeProvider.GetUtcNow());
        await outbox.CompleteAsync(first);
        await outbox.DeferAsync(first, timeProvider.GetUtcNow().AddMinutes(1));
        ProjectionActivationWorkItem persisted = (await outbox.GetAsync(identity)).ShouldNotBeNull();
        persisted.ShouldBe(second);

        await outbox.CompleteAsync(second);

        (await outbox.GetAsync(identity)).ShouldBeNull();
    }

    [Fact]
    public async Task CompetingSameShardRemovalAndRecreationRetriesAgainstLatestHighWaterAndPreservesNeighbor()
    {
        var state = new Dictionary<string, ProjectionActivationLedger>(StringComparer.Ordinal);
        var clock = new FakeTimeProvider();
        var identity = new AggregateIdentity("tenant-a", "counter", "aggregate-a");
        static int Shard(AggregateIdentity value) => SHA256.HashData(Encoding.UTF8.GetBytes(
            ProjectionActivationWorkItem.Create(value, DateTimeOffset.MinValue).ActivationId))[0] % 64;
        AggregateIdentity neighbor = Enumerable.Range(0, 4096).Select(index => new AggregateIdentity("tenant-a", "counter", "neighbor-" + index))
            .First(value => Shard(value) == Shard(identity));
        var staleSaveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeStaleSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool block = false;
        int conflicts = 0;
        using DaprClient dapr = CreateStatefulDaprClient(state, async (_, body, _) =>
        {
            ProjectionActivationLedger candidate = JsonSerializer.Deserialize<ProjectionActivationLedger>(body.Span, JsonSerializerOptions.Web)!;
            if (!block || candidate.Items.FirstOrDefault(item => item.AggregateId == identity.AggregateId)?.Revision != 3) return;
            block = false;
            staleSaveEntered.TrySetResult();
            await resumeStaleSave.Task;
        }, () => conflicts++);
        var first = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), clock);
        var second = new DaprProjectionActivationOutbox(dapr, Options.Create(new ProjectionOptions()), clock);
        await first.EnsureAsync(identity);
        await second.EnsureAsync(neighbor);
        state.ShouldHaveSingleItem().Value.Items.Count.ShouldBe(2); // Confirm selection used the actual same persisted shard.
        ProjectionActivationWorkItem sampled = (await first.GetAsync(identity)).ShouldNotBeNull();
        ProjectionActivationWorkItem oldNeighbor = (await first.GetAsync(neighbor)).ShouldNotBeNull();
        state.Values.Single().LastRevision.ShouldBe(2);
        block = true;
        Task competing = first.EnsureAsync(identity);
        try
        {
            await staleSaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await second.CompleteAsync(sampled);
            (await second.GetAsync(identity)).ShouldBeNull();
            await second.EnsureAsync(identity);
            ProjectionActivationWorkItem recreated = (await second.GetAsync(identity)).ShouldNotBeNull();
            recreated.Revision.ShouldBe(3);
            await second.EnsureAsync(neighbor);
            ProjectionActivationWorkItem latestNeighbor = (await second.GetAsync(neighbor)).ShouldNotBeNull();
            latestNeighbor.Revision.ShouldBe(4);
            resumeStaleSave.SetResult();
            await competing.WaitAsync(TimeSpan.FromSeconds(5));
            conflicts.ShouldBe(1);
            ProjectionActivationWorkItem rearmed = (await first.GetAsync(identity)).ShouldNotBeNull();
            rearmed.Revision.ShouldBe(5);
            state.Values.Single().LastRevision.ShouldBe(5);
            state.Values.Single().Items.Count.ShouldBe(2);
            await first.CompleteAsync(sampled);
            await second.CompleteAsync(recreated);
            await second.DeferAsync(recreated, clock.GetUtcNow().AddHours(1));
            await first.CompleteAsync(oldNeighbor);
            (await first.GetAsync(identity)).ShouldBe(rearmed);
            (await second.GetAsync(neighbor)).ShouldBe(latestNeighbor);
            state.Values.Single().LastRevision.ShouldBe(5);
        }
        finally { resumeStaleSave.TrySetResult(); await competing.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    private static DaprClient CreateStatefulDaprClient(
        Dictionary<string, ProjectionActivationLedger> state,
        Func<string, ReadOnlyMemory<byte>, string, Task>? beforeSave = null, Action? onConflict = null) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.JsonSerializerOptions.Returns(JsonSerializerOptions.Web);
        var versions = new Dictionary<string, int>(StringComparer.Ordinal);
        daprClient.GetByteStateAndETagAsync("statestore", Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>()).Returns(call =>
        {
            lock (state)
            {
                string key = call.ArgAt<string>(1);
                return state.TryGetValue(key, out ProjectionActivationLedger? ledger)
                    ? ((ReadOnlyMemory<byte>)JsonSerializer.SerializeToUtf8Bytes(ledger, JsonSerializerOptions.Web),
                        versions.GetValueOrDefault(key).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    : (ReadOnlyMemory<byte>.Empty, string.Empty);
            }
        });
        daprClient.TrySaveByteStateAsync("statestore", Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (beforeSave is not null) await beforeSave(call.ArgAt<string>(1), call.ArgAt<ReadOnlyMemory<byte>>(2), call.ArgAt<string>(3));
            lock (state)
            {
                string key = call.ArgAt<string>(1);
                string expected = state.ContainsKey(key)
                    ? versions.GetValueOrDefault(key).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                if (expected != call.ArgAt<string>(3)) { onConflict?.Invoke(); return false; }
                state[key] = JsonSerializer.Deserialize<ProjectionActivationLedger>(call.ArgAt<ReadOnlyMemory<byte>>(2).Span,
                    JsonSerializerOptions.Web)!;
                versions[key] = versions.GetValueOrDefault(key) + 1;
                return true;
            }
        });
        return daprClient;
    }
}
