using System.Collections.Concurrent;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionActivationWorkerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(ProjectionDispatchOptions.DefaultRetryScanBatchSize)]
    [InlineData(int.MaxValue)]
    public async Task HostedSlotsAllowAnotherAggregateAndLaterScanWhileHeldWorkIsOwnedAndStopDrainsIt(int batchSize)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var outbox = new DaprProjectionActivationOutbox(backend, Options.Create(new ProjectionOptions()), TimeProvider.System);
        var held = new AggregateIdentity("tenant-a", "counter", "held");
        var second = new AggregateIdentity("tenant-a", "counter", "second");
        var later = new AggregateIdentity("tenant-a", "counter", "later");
        await outbox.EnsureAsync(held); await outbox.EnsureAsync(second);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivery = Substitute.For<IProjectionUpdateOrchestrator>();
        delivery.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            AggregateIdentity identity = call.Arg<AggregateIdentity>();
            if (identity == held) { entered.TrySetResult(); await resume.Task; return; }
            ProjectionActivationWorkItem sampled = (await outbox.GetAsync(identity)).ShouldNotBeNull();
            await outbox.CompleteAsync(sampled);
            (identity == second ? secondDone : laterDone).TrySetResult();
        });
        var settings = Options.Create(new ProjectionDispatchOptions { RetryWorkerInterval = TimeSpan.FromMilliseconds(10), RetryScanBatchSize = batchSize });
        using var worker = new ProjectionActivationWorker(outbox, delivery, settings, TimeProvider.System);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await Task.WhenAll(entered.Task, secondDone.Task).WaitAsync(TimeSpan.FromSeconds(5));
            (await outbox.GetAsync(held)).ShouldNotBeNull();
            (await outbox.GetAsync(second)).ShouldBeNull();
            await outbox.EnsureAsync(later);
            await laterDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
            (await outbox.GetAsync(later)).ShouldBeNull();
            await delivery.Received(1).UpdateProjectionAsync(held, Arg.Any<CancellationToken>());
            Task stopping = worker.StopAsync(CancellationToken.None);
            worker.IsRunning.ShouldBeFalse();
            stopping.IsCompleted.ShouldBeFalse();
            resume.SetResult();
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            // Cancellation retains durable work for another hosted lifetime.
            (await outbox.GetAsync(held)).ShouldNotBeNull();
        }
        finally { resume.TrySetResult(); await worker.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task CompleteWorkerAttemptRetainsIndependentActualAuthorityThroughHeldLegacyRpcAndCleanup()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2);
        var catalog = new RouteCatalogTestFixture();
        await catalog.ActivateAsync(catalog.Candidate());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Http.BeforePreparation = async () => { entered.TrySetResult(); await resume.Task; };
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeFinalizeReplayAsync = async () => { cleanupEntered.TrySetResult(); await resumeCleanup.Task; };
        using var worker = new ProjectionActivationWorker(fixture.Outbox,
            fixture.CreateOrchestrator(catalog.Authorities[0]), Options.Create(new ProjectionDispatchOptions()), fixture.Clock,
            catalogAuthority: catalog.Authorities[0]);
        Task attempt = worker.RunOnceAsync(CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var state = (await catalog.Store.ReadAsync(default)).State;
            state.Leases.ShouldHaveSingleItem();
            var next = catalog.Candidate(2, catalog.Candidate().RootDigest);
            await catalog.Authorities[0].PrepareAsync(next);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => catalog.Authorities[0].ReadyAsync(next.AttemptId));
            resume.SetResult();
            await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            attempt.IsCompleted.ShouldBeFalse();
            (await catalog.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => catalog.Authorities[0].CommitAsync(next.AttemptId));
            resumeCleanup.SetResult();
            await attempt.WaitAsync(TimeSpan.FromSeconds(5));
            (await catalog.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
            (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldBeNull();
            (await fixture.Rows.ReadAsync(fixture.Identity, "counter")).State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(2);
            foreach (RouteCatalogAuthority authority in catalog.Authorities) await authority.ReadyAsync(next.AttemptId);
            await catalog.Authorities[0].CommitAsync(next.AttemptId);
            (await catalog.Authorities[0].ReadbackAsync()).Generation.ShouldBe(2);
        }
        finally { resume.TrySetResult(); resumeCleanup.TrySetResult(); await attempt.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public async Task RepeatedScanFailuresAndRecoveryEmitClosedSignalsWithoutExceptionContent()
    {
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        int scans = 0;
        outbox.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ =>
            ++scans <= 2 ? Task.FromException<IReadOnlyList<ProjectionActivationWorkItem>>(new IOException("secret-payload-credential"))
                : Task.FromResult<IReadOnlyList<ProjectionActivationWorkItem>>([]));
        var entries = new List<LogEntry>();
        using var worker = new ProjectionActivationWorker(outbox, Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(new ProjectionDispatchOptions()), TimeProvider.System, logger: new TestLogger<ProjectionActivationWorker>(entries));
        await worker.RunOnceAsync(default); await worker.RunOnceAsync(default); await worker.RunOnceAsync(default); await worker.RunOnceAsync(default);
        entries.Count(entry => entry.Message.Contains("Operation=scan, ReasonCode=activation_scan_failed", StringComparison.Ordinal)).ShouldBe(2);
        entries.Count(entry => entry.Message.Contains("Operation=scan, Stage=ProjectionActivationRecovered", StringComparison.Ordinal)).ShouldBe(1);
        entries.ShouldAllBe(entry => !entry.Message.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeliveryAndDeferFailureRemainVisibleAndSubsequentProvenRecoveryIsSignaled()
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var persisted = new DaprProjectionActivationOutbox(backend, Options.Create(new ProjectionOptions()), TimeProvider.System);
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        await persisted.EnsureAsync(identity);
        ProjectionActivationWorkItem work = (await persisted.GetAsync(identity)).ShouldNotBeNull();
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        outbox.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([work]);
        int pass = 0;
        outbox.DeferAsync(Arg.Any<ProjectionActivationWorkItem>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => pass == 1 ? Task.FromException(new IOException("secret-defer-message"))
                : persisted.DeferAsync(call.Arg<ProjectionActivationWorkItem>(), call.Arg<DateTimeOffset>()));
        outbox.CompleteAsync(Arg.Any<ProjectionActivationWorkItem>(), Arg.Any<CancellationToken>()).Returns(call => persisted.CompleteAsync(call.Arg<ProjectionActivationWorkItem>()));
        outbox.GetAsync(identity, Arg.Any<CancellationToken>()).Returns(call => persisted.GetAsync(identity, call.Arg<CancellationToken>()));
        var guard = Substitute.For<IProjectionSourceGuardDelivery>();
        guard.TryDeliverAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => ++pass == 1
            ? Task.FromException<ProjectionSourceGuardDeliveryResult>(new IOException("secret-delivery-message"))
            : Task.FromResult(new ProjectionSourceGuardDeliveryResult(true, pass == 3, null) { FinalizedCleanupProven = pass == 3 }));
        var entries = new List<LogEntry>();
        using var worker = new ProjectionActivationWorker(outbox, Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(new ProjectionDispatchOptions()), TimeProvider.System, guard, logger: new TestLogger<ProjectionActivationWorker>(entries));
        await worker.RunOnceAsync(default);
        (await persisted.GetAsync(identity)).ShouldNotBeNull();
        await worker.RunOnceAsync(default);
        work = (await persisted.GetAsync(identity)).ShouldNotBeNull();
        work.Attempt.ShouldBe(1);
        outbox.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([work]);
        await worker.RunOnceAsync(default);
        (await persisted.GetAsync(identity)).ShouldBeNull();
        entries.ShouldContain(entry => entry.Message.Contains("ReasonCode=activation_delivery_failed", StringComparison.Ordinal));
        entries.ShouldContain(entry => entry.Message.Contains("ReasonCode=activation_defer_failed", StringComparison.Ordinal));
        entries.ShouldContain(entry => entry.Message.Contains("Operation=delivery, Stage=ProjectionActivationRecovered", StringComparison.Ordinal));
        entries.ShouldContain(entry => entry.Message.Contains("Operation=defer, Stage=ProjectionActivationRecovered", StringComparison.Ordinal));
        entries.ShouldAllBe(entry => !entry.Message.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostedNineActivationsKeepEightSlotsAndDurableNinthUntilCapacityReleasesThenDrainOwnedCalls()
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var persisted = new DaprProjectionActivationOutbox(backend, Options.Create(new ProjectionOptions()), TimeProvider.System);
        for (int index = 0; index < 9; index++) await persisted.EnsureAsync(new("tenant-a", "counter", "slot-" + index));
        ProjectionActivationWorkItem[] ordered = [.. await persisted.GetDueAsync(DateTimeOffset.MaxValue, 9)];
        var releases = ordered.ToDictionary(item => item.ActivationId,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), StringComparer.Ordinal);
        var entered = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var saturated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterScan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ninthEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0, maximum = 0;
        var gate = new object();
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        outbox.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (entered.Count == 8) laterScan.TrySetResult();
            return persisted.GetDueAsync(call.Arg<DateTimeOffset>(), call.Arg<int>(), call.Arg<CancellationToken>());
        });
        outbox.CompleteAsync(Arg.Any<ProjectionActivationWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => persisted.CompleteAsync(call.Arg<ProjectionActivationWorkItem>(), call.Arg<CancellationToken>()));
        var catalog = new RouteCatalogTestFixture();
        await catalog.ActivateAsync(catalog.Candidate());
        var guard = Substitute.For<IProjectionSourceGuardDelivery>();
        guard.TryDeliverAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            AggregateIdentity identity = call.Arg<AggregateIdentity>();
            string id = ProjectionActivationWorkItem.Create(identity, DateTimeOffset.MinValue).ActivationId;
            Task<ProjectionSourceGuardDeliveryResult> actual = RouteCatalogRemoteCall.InvokeAsync(async () =>
            {
                entered.TryAdd(id, 0);
                lock (gate) { active++; maximum = Math.Max(maximum, active); }
                if (entered.Count == 8) saturated.TrySetResult();
                if (id == ordered[8].ActivationId) ninthEntered.TrySetResult();
                try
                {
                    await releases[id].Task;
                    return new ProjectionSourceGuardDeliveryResult(true, true, null) { FinalizedCleanupProven = true };
                }
                finally { lock (gate) active--; }
            });
            return actual.WaitAsync(call.Arg<CancellationToken>());
        });
        using var worker = new ProjectionActivationWorker(outbox, Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(new ProjectionDispatchOptions { RetryWorkerInterval = TimeSpan.FromMilliseconds(10) }),
            TimeProvider.System, guard, catalog.Authorities[0]);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await Task.WhenAll(saturated.Task, laterScan.Task).WaitAsync(TimeSpan.FromSeconds(5));
            entered.Count.ShouldBe(8);
            entered.ContainsKey(ordered[8].ActivationId).ShouldBeFalse();
            lock (gate) { active.ShouldBe(8); maximum.ShouldBe(8); }
            (await catalog.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(8);
            (await persisted.GetAsync(new(ordered[8].TenantId, ordered[8].Domain, ordered[8].AggregateId))).ShouldBe(ordered[8]);
            releases[ordered[0].ActivationId].SetResult();
            await ninthEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            entered.Count.ShouldBe(9);
            lock (gate) { active.ShouldBe(8); maximum.ShouldBe(8); }
            using var deadline = new CancellationTokenSource();
            Task stopped = worker.StopAsync(deadline.Token);
            stopped.IsCompleted.ShouldBeFalse();
            (await persisted.GetDueAsync(DateTimeOffset.MaxValue, 9)).Count.ShouldBe(8);
            (await catalog.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(8);
            deadline.Cancel();
            await stopped.WaitAsync(TimeSpan.FromSeconds(5));
            worker.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
        }
        finally
        {
            foreach (TaskCompletionSource release in releases.Values) release.TrySetResult();
            using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StopAsync(cleanupDeadline.Token).WaitAsync(TimeSpan.FromSeconds(5));
        }
        worker.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeTrue();
        lock (gate) { active.ShouldBe(0); maximum.ShouldBe(8); }
        (await catalog.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        (await persisted.GetDueAsync(DateTimeOffset.MaxValue, 9)).Count.ShouldBe(8);
    }

    [Theory]
    [InlineData("source_conflict", "source_conflict")]
    [InlineData("checkpoint_drift", "checkpoint_drift")]
    [InlineData("delivery_reconciliation_required", "delivery_reconciliation_required")]
    [InlineData("delivery_byte_limit_exceeded", "delivery_byte_limit_exceeded")]
    [InlineData("head_changed", "head_changed")]
    [InlineData("unsupported_dispatch_capability", "unsupported_dispatch_capability")]
    [InlineData("malformed_projection_outcome", "malformed_projection_outcome")]
    [InlineData("malformed_outcome", "activation_delivery_unproven")]
    [InlineData(null, "activation_delivery_unproven")]
    [InlineData("Bearer redacted", "activation_delivery_unproven")]
    [InlineData("source_conflict Bearer redacted", "activation_delivery_unproven")]
    [InlineData("arbitrary-provider-error", "activation_delivery_unproven")]
    public async Task GuardRefusalsLogOnlyExplicitClosedProductionReasons(string? supplied, string expected)
    {
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        var work = ProjectionActivationWorkItem.Create(identity, DateTimeOffset.MinValue);
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        outbox.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([work]);
        var guard = Substitute.For<IProjectionSourceGuardDelivery>();
        guard.TryDeliverAsync(identity, Arg.Any<CancellationToken>()).Returns(new ProjectionSourceGuardDeliveryResult(true, false, supplied));
        var entries = new List<LogEntry>();
        using var worker = new ProjectionActivationWorker(outbox, Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(new ProjectionDispatchOptions()), TimeProvider.System, guard, logger: new TestLogger<ProjectionActivationWorker>(entries));
        await worker.RunOnceAsync(default);
        entries.ShouldHaveSingleItem().Message.ShouldContain("ReasonCode=" + expected + ", Stage=ProjectionActivationFailed");
        entries.ShouldAllBe(entry => !entry.Message.Contains("secret", StringComparison.Ordinal)
            && !entry.Message.Contains("arbitrary-provider-error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedDeferAtAttemptZeroRecoversWhenLaterDeliveryCompletesWithoutAnotherDefer()
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var persisted = new DaprProjectionActivationOutbox(backend, Options.Create(new ProjectionOptions()), TimeProvider.System);
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        await persisted.EnsureAsync(identity);
        ProjectionActivationWorkItem work = (await persisted.GetAsync(identity)).ShouldNotBeNull();
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        outbox.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([work]);
        outbox.DeferAsync(Arg.Any<ProjectionActivationWorkItem>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new IOException("defer failed")));
        outbox.CompleteAsync(Arg.Any<ProjectionActivationWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => persisted.CompleteAsync(call.Arg<ProjectionActivationWorkItem>()));
        var guard = Substitute.For<IProjectionSourceGuardDelivery>();
        bool recover = false;
        guard.TryDeliverAsync(identity, Arg.Any<CancellationToken>()).Returns(_ => recover
            ? Task.FromResult(new ProjectionSourceGuardDeliveryResult(true, true, null) { FinalizedCleanupProven = true })
            : Task.FromException<ProjectionSourceGuardDeliveryResult>(new IOException("delivery failed")));
        var entries = new List<LogEntry>();
        using var worker = new ProjectionActivationWorker(outbox, Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(new ProjectionDispatchOptions()), TimeProvider.System, guard, logger: new TestLogger<ProjectionActivationWorker>(entries));
        await worker.RunOnceAsync(default);
        (await persisted.GetAsync(identity)).ShouldNotBeNull().Attempt.ShouldBe(0);
        recover = true;
        await worker.RunOnceAsync(default);
        (await persisted.GetAsync(identity)).ShouldBeNull();
        entries.Count(entry => entry.Message.Contains("Operation=delivery, Stage=ProjectionActivationRecovered", StringComparison.Ordinal)).ShouldBe(1);
        entries.ShouldNotContain(entry => entry.Message.Contains("Operation=defer, Stage=ProjectionActivationRecovered", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleConditionalDeferralNeverClaimsPersistenceAndRecoveryReadsRemainAdvisory(bool failDiagnosticRead)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var persisted = new DaprProjectionActivationOutbox(backend, Options.Create(new ProjectionOptions()), TimeProvider.System);
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        await persisted.EnsureAsync(identity);
        ProjectionActivationWorkItem work = (await persisted.GetAsync(identity)).ShouldNotBeNull();
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        outbox.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => new[] { work });
        int phase = 0, defers = 0;
        outbox.DeferAsync(Arg.Any<ProjectionActivationWorkItem>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            defers++;
            if (phase == 0) throw new IOException("first defer failed");
            if (phase == 1) { phase = 2; await persisted.EnsureAsync(identity); }
            await persisted.DeferAsync(call.Arg<ProjectionActivationWorkItem>(), call.Arg<DateTimeOffset>());
        });
        outbox.GetAsync(identity, Arg.Any<CancellationToken>()).Returns(call => failDiagnosticRead && phase is 2 or 3
            ? Task.FromException<ProjectionActivationWorkItem?>(new IOException("private diagnostic failure"))
            : persisted.GetAsync(identity, call.Arg<CancellationToken>()));
        var guard = Substitute.For<IProjectionSourceGuardDelivery>();
        guard.TryDeliverAsync(identity, Arg.Any<CancellationToken>()).Returns(new ProjectionSourceGuardDeliveryResult(true, false, "delivery_in_progress"));
        var entries = new List<LogEntry>();
        using var worker = new ProjectionActivationWorker(outbox, Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(new ProjectionDispatchOptions()), TimeProvider.System, guard, logger: new TestLogger<ProjectionActivationWorker>(entries));
        await worker.RunOnceAsync(default);
        defers.ShouldBe(2); // The failed delivery's bounded cleanup retry also failed.
        phase = 1;
        await worker.RunOnceAsync(default);
        defers.ShouldBe(3);
        ProjectionActivationWorkItem newer = (await persisted.GetAsync(identity)).ShouldNotBeNull();
        newer.Revision.ShouldBeGreaterThan(work.Revision);
        newer.Attempt.ShouldBe(0);
        entries.ShouldNotContain(entry => entry.Message.Contains("Operation=defer, Stage=ProjectionActivationRecovered", StringComparison.Ordinal));
        work = newer;
        phase = 3;
        await worker.RunOnceAsync(default);
        defers.ShouldBe(4);
        ProjectionActivationWorkItem deferred = (await persisted.GetAsync(identity)).ShouldNotBeNull();
        deferred.Revision.ShouldBe(newer.Revision);
        deferred.Attempt.ShouldBe(1);
        deferred.NextDueUtc.ShouldBeGreaterThan(newer.NextDueUtc);
        if (failDiagnosticRead)
        {
            entries.ShouldNotContain(entry => entry.Message.Contains("Operation=defer, Stage=ProjectionActivationRecovered", StringComparison.Ordinal));
            work = deferred;
            phase = 4;
            await worker.RunOnceAsync(default);
            defers.ShouldBe(5);
            (await persisted.GetAsync(identity)).ShouldNotBeNull().Attempt.ShouldBe(2);
        }
        entries.Count(entry => entry.Message.Contains("Operation=defer, Stage=ProjectionActivationRecovered", StringComparison.Ordinal)).ShouldBe(1);
        entries.ShouldAllBe(entry => !entry.Message.Contains("private diagnostic failure", StringComparison.Ordinal));
    }
}
