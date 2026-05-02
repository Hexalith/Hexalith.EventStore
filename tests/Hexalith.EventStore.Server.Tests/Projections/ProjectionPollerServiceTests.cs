using System.Threading.Channels;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionPollerServiceTests {
    private static readonly AggregateIdentity FastIdentity = new("tenant", "fast", "agg-1");
    private static readonly AggregateIdentity SlowIdentity = new("tenant", "slow", "agg-2");

    [Fact]
    public async Task PollOnceAsync_PositiveInterval_DeliversTrackedIdentity() {
        var tracker = new FakeProjectionCheckpointTracker(FastIdentity);
        IProjectionPollerDeliveryGateway orchestrator = Substitute.For<IProjectionPollerDeliveryGateway>();
        var service = CreateService(
            tracker,
            orchestrator,
            new ProjectionOptions { DefaultRefreshIntervalMs = 1000 });

        await service.PollOnceAsync(DateTimeOffset.UtcNow);

        await orchestrator.Received(1).DeliverProjectionAsync(FastIdentity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_PerDomainIntervals_DoNotDeliverSlowDomainOnEveryFastTick() {
        var tracker = new FakeProjectionCheckpointTracker(FastIdentity, SlowIdentity);
        IProjectionPollerDeliveryGateway orchestrator = Substitute.For<IProjectionPollerDeliveryGateway>();
        var service = CreateService(
            tracker,
            orchestrator,
            new ProjectionOptions {
                DefaultRefreshIntervalMs = 0,
                Domains = new Dictionary<string, DomainProjectionOptions> {
                    ["fast"] = new() { RefreshIntervalMs = 1000 },
                    ["slow"] = new() { RefreshIntervalMs = 5000 },
                },
            });
        DateTimeOffset start = DateTimeOffset.UtcNow;

        await service.PollOnceAsync(start);
        await service.PollOnceAsync(start.AddMilliseconds(1000));

        await orchestrator.Received(2).DeliverProjectionAsync(FastIdentity, Arg.Any<CancellationToken>());
        await orchestrator.Received(1).DeliverProjectionAsync(SlowIdentity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_SameIdentityAlreadyRunning_SkipsOverlap() {
        var tracker = new FakeProjectionCheckpointTracker(FastIdentity);
        IProjectionPollerDeliveryGateway orchestrator = Substitute.For<IProjectionPollerDeliveryGateway>();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = orchestrator.DeliverProjectionAsync(FastIdentity, Arg.Any<CancellationToken>())
            .Returns(_ => release.Task);
        var service = CreateService(
            tracker,
            orchestrator,
            new ProjectionOptions { DefaultRefreshIntervalMs = 1000 });
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Task first = service.PollOnceAsync(now);
        await tracker.WaitForEnumerationAsync();
        await service.PollOnceAsync(now);
        release.SetResult();
        await first;

        await orchestrator.Received(1).DeliverProjectionAsync(FastIdentity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TickSourceEnds_ExitsCleanly() {
        var tracker = new FakeProjectionCheckpointTracker(FastIdentity);
        IProjectionPollerDeliveryGateway orchestrator = Substitute.For<IProjectionPollerDeliveryGateway>();
        var tickSource = new ManualProjectionPollerTickSource();
        var service = CreateService(
            tracker,
            orchestrator,
            new ProjectionOptions { DefaultRefreshIntervalMs = 1000 },
            tickSource);

        Task run = service.StartAsync(CancellationToken.None);
        await tickSource.WaitForWaiterAsync();
        tickSource.Complete(false);
        await service.StopAsync(CancellationToken.None);
        await run;

        await orchestrator.DidNotReceiveWithAnyArgs().DeliverProjectionAsync(default!, default);
    }

    [Fact]
    public async Task PollOnceAsync_DeliveryFailure_RetriesOnLaterDueTick() {
        var tracker = new FakeProjectionCheckpointTracker(FastIdentity);
        IProjectionPollerDeliveryGateway orchestrator = Substitute.For<IProjectionPollerDeliveryGateway>();
        _ = orchestrator.DeliverProjectionAsync(FastIdentity, Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("project endpoint unavailable"),
                _ => Task.CompletedTask);
        var service = CreateService(
            tracker,
            orchestrator,
            new ProjectionOptions { DefaultRefreshIntervalMs = 1000 });
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await service.PollOnceAsync(now);
        await service.PollOnceAsync(now.AddMilliseconds(1000));

        await orchestrator.Received(2).DeliverProjectionAsync(FastIdentity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_DeliveryFailure_LogsOperatorEvent() {
        var tracker = new FakeProjectionCheckpointTracker(FastIdentity);
        IProjectionPollerDeliveryGateway orchestrator = Substitute.For<IProjectionPollerDeliveryGateway>();
        _ = orchestrator.DeliverProjectionAsync(FastIdentity, Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("project endpoint unavailable"));
        var entries = new List<LogEntry>();
        var service = CreateService(
            tracker,
            orchestrator,
            new ProjectionOptions { DefaultRefreshIntervalMs = 1000 },
            logger: new TestLogger<ProjectionPollerService>(entries));

        await service.PollOnceAsync(DateTimeOffset.UtcNow);

        entries.ShouldContain(e => e.EventId.Id == 1131 && e.Level == LogLevel.Warning && e.Message.Contains("Stage=ProjectionPollingDeliveryFailed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PollOnceAsync_TickLimitReached_LogsOperatorEvent() {
        AggregateIdentity[] identities = Enumerable
            .Range(0, 101)
            .Select(i => new AggregateIdentity("tenant", "fast", $"agg-{i:D3}"))
            .ToArray();
        var tracker = new FakeProjectionCheckpointTracker(identities);
        IProjectionPollerDeliveryGateway orchestrator = Substitute.For<IProjectionPollerDeliveryGateway>();
        var entries = new List<LogEntry>();
        var service = CreateService(
            tracker,
            orchestrator,
            new ProjectionOptions { DefaultRefreshIntervalMs = 1000 },
            logger: new TestLogger<ProjectionPollerService>(entries));

        await service.PollOnceAsync(DateTimeOffset.UtcNow);

        entries.ShouldContain(e => e.EventId.Id == 1133 && e.Level == LogLevel.Information && e.Message.Contains("Stage=ProjectionPollingTickLimitReached", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PollOnceAsync_EnumerationFailure_LogsOperatorEvent() {
        var tracker = new FakeProjectionCheckpointTracker(FastIdentity) {
            ThrowBeforeYield = true,
        };
        IProjectionPollerDeliveryGateway orchestrator = Substitute.For<IProjectionPollerDeliveryGateway>();
        var entries = new List<LogEntry>();
        var service = CreateService(
            tracker,
            orchestrator,
            new ProjectionOptions { DefaultRefreshIntervalMs = 1000 },
            logger: new TestLogger<ProjectionPollerService>(entries));

        await service.PollOnceAsync(DateTimeOffset.UtcNow);

        entries.ShouldContain(e => e.EventId.Id == 1134 && e.Level == LogLevel.Warning && e.Message.Contains("Stage=ProjectionPollingEnumerationFailed", StringComparison.Ordinal));
    }

    private static ProjectionPollerService CreateService(
        IProjectionCheckpointTracker tracker,
        IProjectionPollerDeliveryGateway orchestrator,
        ProjectionOptions options,
        IProjectionPollerTickSource? tickSource = null,
        TimeProvider? timeProvider = null,
        ILogger<ProjectionPollerService>? logger = null) =>
        new(
            tracker,
            orchestrator,
            Options.Create(options),
            tickSource ?? new ManualProjectionPollerTickSource(),
            timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow),
            logger ?? NullLogger<ProjectionPollerService>.Instance);

    private sealed class FakeProjectionCheckpointTracker(params AggregateIdentity[] identities) : IProjectionCheckpointTracker {
        private readonly TaskCompletionSource _enumerated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ThrowBeforeYield { get; init; }

        public Task<long> ReadLastDeliveredSequenceAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task<bool> SaveDeliveredSequenceAsync(AggregateIdentity identity, long deliveredSequence, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task TrackIdentityAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public async IAsyncEnumerable<AggregateIdentity> EnumerateTrackedIdentitiesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if (ThrowBeforeYield) {
                throw new InvalidOperationException("state store unavailable");
            }

            foreach (AggregateIdentity identity in identities) {
                _enumerated.TrySetResult();
                yield return identity;
            }

            _enumerated.TrySetResult();
            await Task.CompletedTask;
        }

        public Task WaitForEnumerationAsync() => _enumerated.Task;
    }

    private sealed class ManualProjectionPollerTickSource : IProjectionPollerTickSource {
        private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();
        private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> WaitForNextTickAsync(TimeSpan interval, CancellationToken cancellationToken) {
            _waiting.TrySetResult();
            return await _ticks.Reader.ReadAsync(cancellationToken);
        }

        public void Complete(bool value) => _ticks.Writer.TryWrite(value);

        public Task WaitForWaiterAsync() => _waiting.Task;
    }

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
