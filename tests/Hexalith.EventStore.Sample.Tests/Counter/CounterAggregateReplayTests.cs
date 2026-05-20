using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Sample.Counter.Events;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.Counter;

/// <summary>
/// Tier 1 end-to-end coverage of the Sample's <c>POST /replay-state</c> wiring through
/// <see cref="DomainServiceRequestRouter.Replay"/> and the registered Counter aggregate's
/// canonical Apply path. Mirrors the seeded tenant-a/counter/counter-1 stream from
/// admin-ui-aggregate-state-replay-correctness so the headline correctness assertion
/// (Count = 10 at sequence 18) is covered without DAPR or Aspire infrastructure.
/// </summary>
public class CounterAggregateReplayTests {
    private static IServiceProvider BuildServiceProvider() {
        ServiceCollection services = new();
        _ = services.AddEventStore(typeof(CounterAggregateReplayTests).Assembly, typeof(DomainServiceRequestRouter).Assembly);
        return services.BuildServiceProvider();
    }

    private static ReplayEventEnvelope MarkerEvent(long seq, string typeName)
        => new(
            SequenceNumber: seq,
            EventTypeName: typeName,
            Payload: Encoding.UTF8.GetBytes("{}"),
            SerializationFormat: "json",
            MetadataVersion: 1,
            MessageId: $"msg-{seq}",
            CorrelationId: $"corr-{seq}",
            CausationId: null);

    private static ReplayEventEnvelope[] CanonicalCounterFixture() {
        var events = new List<ReplayEventEnvelope>(18);
        long seq = 0;
        for (int i = 0; i < 5; i++) {
            events.Add(MarkerEvent(++seq, nameof(CounterIncremented)));
        }

        for (int i = 0; i < 2; i++) {
            events.Add(MarkerEvent(++seq, nameof(CounterDecremented)));
        }

        events.Add(MarkerEvent(++seq, nameof(CounterReset)));
        for (int i = 0; i < 10; i++) {
            events.Add(MarkerEvent(++seq, nameof(CounterIncremented)));
        }

        return [.. events];
    }

    private static AggregateReconstructionRequest BuildRequest(IReadOnlyList<ReplayEventEnvelope> events, long upTo, bool timeline = false)
        => new(
            TenantId: "tenant-a",
            Domain: "counter",
            AggregateType: "Counter",
            AggregateId: "counter-1",
            UpToSequence: upTo,
            Events: events,
            IncludeTimeline: timeline,
            RequestId: "test");

    private static int CountFromState(string? json) =>
        JsonDocument.Parse(json ?? "{}").RootElement.GetProperty("count").GetInt32();

    [Fact]
    public void Replay_CanonicalSeed_AtSequence18_ReturnsCount10_ProvingApplyDrivenReplay() {
        // The seeded tenant-a/counter/counter-1 stream uses marker events with empty
        // payloads. A payload deep-merge would yield {} and Count = 0. Apply-driven
        // replay through CounterState.Apply(...) yields Count = 10. This is the AC #1
        // headline test executed without the Dapr boundary.
        IServiceProvider services = BuildServiceProvider();

        AggregateReconstructionResult result = DomainServiceRequestRouter.Replay(services, BuildRequest(CanonicalCounterFixture(), 18));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        CountFromState(result.StateJson).ShouldBe(10);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(7, 3)]
    [InlineData(8, 0)]
    public void Replay_CanonicalSeed_AtCheckpoints_MatchesExpectedCount(long upTo, int expected) {
        IServiceProvider services = BuildServiceProvider();

        AggregateReconstructionResult result = DomainServiceRequestRouter.Replay(services, BuildRequest(CanonicalCounterFixture(), upTo));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        CountFromState(result.StateJson).ShouldBe(expected);
    }

    [Fact]
    public void Replay_TimelineMode_ReturnsPerEventStateMatchingCheckpoints() {
        IServiceProvider services = BuildServiceProvider();

        AggregateReconstructionResult result = DomainServiceRequestRouter.Replay(services, BuildRequest(CanonicalCounterFixture(), 18, timeline: true));

        _ = result.Timeline.ShouldNotBeNull();
        result.Timeline!.Count.ShouldBe(18);
        CountFromState(result.Timeline[0].StateJson).ShouldBe(1);
        CountFromState(result.Timeline[7].StateJson).ShouldBe(0);
        CountFromState(result.Timeline[17].StateJson).ShouldBe(10);
    }

    [Fact]
    public void Replay_UnknownDomain_ReturnsUnknownAggregateType() {
        IServiceProvider services = BuildServiceProvider();

        AggregateReconstructionRequest request = new(
            TenantId: "tenant-a",
            Domain: "domain-that-does-not-exist",
            AggregateType: string.Empty,
            AggregateId: "anything",
            UpToSequence: 0,
            Events: [],
            IncludeTimeline: false,
            RequestId: null);

        AggregateReconstructionResult result = DomainServiceRequestRouter.Replay(services, request);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownAggregateType);
    }

    [Fact]
    public void Replay_UnknownAggregateTypeForKnownDomain_ReturnsUnknownAggregateType() {
        IServiceProvider services = BuildServiceProvider();
        AggregateReconstructionRequest request = BuildRequest(CanonicalCounterFixture(), 1) with {
            AggregateType = "DefinitelyNotCounter",
        };

        AggregateReconstructionResult result = DomainServiceRequestRouter.Replay(services, request);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownAggregateType);
    }
}
