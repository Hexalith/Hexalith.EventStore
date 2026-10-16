using Dapr.Client;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class DaprProjectionActivationOutboxTests {
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

    private static DaprClient CreateStatefulDaprClient(
        Dictionary<string, ProjectionActivationLedger> state) {
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
                cancellationToken: Arg.Any<CancellationToken>()).Returns(call =>
        {
            lock (state)
            {
                string key = call.ArgAt<string>(1);
                string expected = state.ContainsKey(key)
                    ? versions.GetValueOrDefault(key).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                if (expected != call.ArgAt<string>(3)) return false;
                state[key] = JsonSerializer.Deserialize<ProjectionActivationLedger>(call.ArgAt<ReadOnlyMemory<byte>>(2).Span,
                    JsonSerializerOptions.Web)!;
                versions[key] = versions.GetValueOrDefault(key) + 1;
                return true;
            }
        });
        return daprClient;
    }
}
