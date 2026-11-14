using System.Diagnostics;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Diagnostics;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Diagnostics;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Checks opt-in privacy and advisory behavior without changing shutdown ownership.</summary>
[Collection("ReadDiagnosticsPrivacy")]
public sealed class ShutdownStageScopeTests
{
    private const string EnableVariable = "HEXALITH_EVENTSTORE_SHUTDOWN_TRACE";

    /// <summary>Verifies default and invalid opt ins do not touch logger.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public void DefaultAndInvalidOptInsDoNotTouchLogger(string? value)
    {
        WithFlag(value, () =>
        {
            var logger = new ShutdownStageTestLogger<ShutdownStageScopeTests> { Throw = true };
            ShutdownStageScope.TryStart(logger, ShutdownStage.TelemetryStop, default).ShouldBeNull();
            AdminIndexShutdownTrace.StartStop(logger, default).ShouldBeNull();
            logger.EnableChecks.ShouldBe(0);
            logger.Records.ShouldBeEmpty();
        });
    }

    /// <summary>Verifies debug filter and closed stage set are required.</summary>
    [Fact]
    public void DebugFilterAndClosedStageSetAreRequired()
    {
        WithFlag("true", () =>
        {
            var logger = new ShutdownStageTestLogger<ShutdownStageScopeTests> { Enabled = false };
            ShutdownStageScope.TryStart(logger, ShutdownStage.TelemetryStop, default).ShouldBeNull();
            logger.Enabled = true;
            ShutdownStageScope.TryStart(logger, (ShutdownStage)int.MaxValue, default).ShouldBeNull();
            logger.Records.ShouldBeEmpty();
        });
    }

    /// <summary>Verifies returned scope is paired once and payload is closed.</summary>
    [Fact]
    public void ReturnedScopeIsPairedOnceAndPayloadIsClosed()
    {
        WithFlag("true", () =>
        {
            using var activity = new Activity("private-business-activity").Start();
            activity.SetTag("private-key", "private-body");
            activity.AddBaggage("private-credential", "private-value");
            var logger = new ShutdownStageTestLogger<ShutdownStageScopeTests>();
            using var cancellation = new CancellationTokenSource();
            ShutdownStageScope scope = ShutdownStageScope.TryStart(logger, ShutdownStage.ProjectionActivationRemoteDrain, cancellation.Token)!;
            cancellation.Cancel();
            scope.Complete();
            scope.Complete();
            scope.Dispose();
            logger.Records.Count.ShouldBe(2);
            IReadOnlyDictionary<string, object?>[] records = logger.Records.ToArray();
            records[0]["State"].ShouldBe("Started");
            records[1]["State"].ShouldBe("Returned");
            records[0]["Sequence"].ShouldBe(records[1]["Sequence"]);
            records[0]["ObservedCanceled"].ShouldBe(false);
            records[1]["ObservedCanceled"].ShouldBe(true);
            foreach (IReadOnlyDictionary<string, object?> record in records)
            {
                record.Keys.Order().ShouldBe(new[] { "ElapsedMs", "ObservedCanBeCanceled", "ObservedCanceled", "ProcessId", "Sequence", "Stage", "State", "{OriginalFormat}" }.Order());
                record["Stage"].ShouldBe("ProjectionActivationRemoteDrain");
                record["ProcessId"].ShouldBe(Environment.ProcessId);
                record["ObservedCanBeCanceled"].ShouldBe(true);
                ((int)record["ElapsedMs"]!).ShouldBeGreaterThanOrEqualTo(0);
                string.Join("|", record.Values).ShouldNotContain("private-");
            }
            Activity.Current.ShouldBeSameAs(activity);
            activity.GetTagItem("private-key").ShouldBe("private-body");
            activity.GetBaggageItem("private-credential").ShouldBe("private-value");
        });
    }

    /// <summary>Verifies unwinding preserves exact exception and cancellation token.</summary>
    [Fact]
    public void UnwindingPreservesExactExceptionAndCancellationToken()
    {
        WithFlag("true", () =>
        {
            var logger = new ShutdownStageTestLogger<ShutdownStageScopeTests>();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var original = new OperationCanceledException("private-state", cancellation.Token);
            OperationCanceledException observed = Should.Throw<OperationCanceledException>(() =>
            {
                using ShutdownStageScope? scope = ShutdownStageScope.TryStart(logger, ShutdownStage.AdminIndexRefreshDrain, cancellation.Token);
                throw original;
            });
            observed.ShouldBeSameAs(original);
            observed.CancellationToken.ShouldBe(cancellation.Token);
            logger.Records.Count.ShouldBe(2);
            logger.Records.Last()["State"].ShouldBe("Unwound");
        });
    }

    /// <summary>Verifies throwing logger cannot replace original result.</summary>
    [Fact]
    public void ThrowingLoggerCannotReplaceOriginalResult()
    {
        WithFlag("true", () =>
        {
            var logger = new ShutdownStageTestLogger<ShutdownStageScopeTests> { Throw = true };
            ShutdownStageScope.TryStart(logger, ShutdownStage.TelemetryStop, default).ShouldBeNull();
            logger.Throw = false;
            using ShutdownStageScope? scope = ShutdownStageScope.TryStart(logger, ShutdownStage.TelemetryStop, default);
            scope.ShouldNotBeNull();
            logger.Throw = true;
            scope.Complete();
            scope.Dispose();
        });
    }

    /// <summary>Verifies admin bridge returns only its own accessible type.</summary>
    [Fact]
    public void AdminBridgeReturnsOnlyItsOwnAccessibleType()
    {
        WithFlag("true", () =>
        {
            var logger = new ShutdownStageTestLogger<ShutdownStageScopeTests>();
            using AdminIndexShutdownTrace? scope = AdminIndexShutdownTrace.StartCancellation(logger, default);
            scope.ShouldNotBeNull();
            scope.Complete();
            logger.Records.Last()["Stage"].ShouldBe("AdminIndexCancelCallbacks");
            logger.Records.Last()["State"].ShouldBe("Returned");
        });
    }

    /// <summary>Verifies null telemetry providers keep sequential stage order.</summary>
    [Fact]
    public async Task NullTelemetryProvidersKeepSequentialStageOrder()
    {
        string? previous = Environment.GetEnvironmentVariable(EnableVariable);
        try
        {
            Environment.SetEnvironmentVariable(EnableVariable, "true");
            var logger = new ShutdownStageTestLogger<BoundedTelemetryShutdownService>();
            var service = new BoundedTelemetryShutdownService(logger);
            await service.StopAsync(CancellationToken.None).ConfigureAwait(true);
            logger.Records.Select(record => $"{record["Stage"]}:{record["State"]}").ShouldBe(new[]
            {
                "TelemetryStop:Started", "TelemetryTracerCall:Started", "TelemetryTracerCall:Returned",
                "TelemetryMeterCall:Started", "TelemetryMeterCall:Returned", "TelemetryLoggerCall:Started",
                "TelemetryLoggerCall:Returned", "TelemetryStop:Returned",
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnableVariable, previous);
        }
    }

    /// <summary>Observes the production worker's independent remote drain without shortening ownership after host cancellation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkerTracePreservesActualRemoteCallAndAmbiguousLeaseAfterCanceledStop(bool ambiguous)
    {
        string? previous = Environment.GetEnvironmentVariable(EnableVariable);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Environment.SetEnvironmentVariable(EnableVariable, "true");
            using var fixture = new LegacyReplayProductionHarness();
            await fixture.InitializeAsync(1).ConfigureAwait(true);
            var catalog = new RouteCatalogTestFixture();
            await catalog.ActivateAsync(catalog.Candidate()).ConfigureAwait(true);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var guard = Substitute.For<IProjectionSourceGuardDelivery>();
            Task<ProjectionSourceGuardDeliveryResult>? nativeCall = null;
            guard.TryDeliverAsync(fixture.Identity, Arg.Any<CancellationToken>()).Returns(call =>
            {
                nativeCall = RouteCatalogRemoteCall.InvokeAsync(async () =>
                {
                    await resume.Task.ConfigureAwait(false);
                    if (ambiguous) throw new IOException("private-ambiguous-call");
                    return new ProjectionSourceGuardDeliveryResult(true, true, null) { FinalizedCleanupProven = true };
                });
                entered.TrySetResult();
                return nativeCall.WaitAsync(call.Arg<CancellationToken>());
            });
            var logger = new ShutdownStageTestLogger<ProjectionActivationWorker>();
            using var worker = new ProjectionActivationWorker(fixture.Outbox, Substitute.For<IProjectionUpdateOrchestrator>(),
                Options.Create(new ProjectionDispatchOptions { RetryWorkerInterval = TimeSpan.FromMilliseconds(10) }),
                TimeProvider.System, guard, catalog.Authorities[0], logger);
            await worker.StartAsync(CancellationToken.None).ConfigureAwait(true);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                using var stopDeadline = new CancellationTokenSource();
                Task stopping = worker.StopAsync(stopDeadline.Token);
                stopDeadline.Cancel();
                await stopping.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (!logger.Records.Any(record => Equals(record["Stage"], "ProjectionActivationRemoteDrain")))
                {
                    if (DateTime.UtcNow >= deadline) throw new TimeoutException("The actual remote drain did not start.");
                    await Task.Delay(10, TestContext.Current.CancellationToken).ConfigureAwait(true);
                }
                worker.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
                nativeCall.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
                (await catalog.Store.ReadAsync(default).ConfigureAwait(true)).State.Leases.ShouldHaveSingleItem();
                logger.Records.ShouldContain(record => Equals(record["Stage"], "ProjectionActivationStop") && Equals(record["State"], "Returned"));
                logger.Records.ShouldNotContain(record => Equals(record["Stage"], "ProjectionActivationRemoteDrain") && Equals(record["State"], "Returned"));
                resume.SetResult();
                if (ambiguous) await Should.ThrowAsync<IOException>(() => nativeCall!).ConfigureAwait(true);
                else await nativeCall!.ConfigureAwait(true);
                await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                (await catalog.Store.ReadAsync(default).ConfigureAwait(true)).State.Leases.Count.ShouldBe(ambiguous ? 1 : 0);
                (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
                logger.Records.ShouldContain(record => Equals(record["Stage"], "ProjectionActivationRemoteDrain") && Equals(record["State"], "Returned"));
            }
            finally
            {
                resume.TrySetResult();
                await worker.StopAsync(CancellationToken.None).ConfigureAwait(true);
            }
        }
        finally
        {
            resume.TrySetResult();
            Environment.SetEnvironmentVariable(EnableVariable, previous);
        }
    }

    private static void WithFlag(string? value, Action action)
    {
        string? previous = Environment.GetEnvironmentVariable(EnableVariable);
        try
        {
            Environment.SetEnvironmentVariable(EnableVariable, value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnableVariable, previous);
        }
    }
}
