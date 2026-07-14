using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class DaprProjectionActivationOutboxTests {
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
        _ = daprClient.GetStateAndETagAsync<ProjectionActivationLedger>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                string key = call.ArgAt<string>(1);
                return ((ProjectionActivationLedger, string))(
                    state.GetValueOrDefault(key)!,
                    state.ContainsKey(key) ? "etag" : string.Empty);
            });
        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<ProjectionActivationLedger>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                state[call.ArgAt<string>(1)] = call.ArgAt<ProjectionActivationLedger>(2);
                return true;
            });
        return daprClient;
    }
}
