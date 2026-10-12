using System.Diagnostics;
using System.Security.Claims;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Bounded admission for uncooperative synchronous and asynchronous adapter reads.</summary>
[Collection("Process read slots")]
public sealed class ProcessReadDeadlineTests
{
    /// <summary>Timed-out uncooperative workers exhaust a finite pool without blocking later callers in its queue.</summary>
    [Fact]
    public async Task ExhaustedWorkersRejectPromptlyAndRecoverAfterRelease()
    {
        TaskCompletionSource<int> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>[] timedOut = Enumerable.Range(0, 16)
            .Select(_ => ProcessReadDeadline.RunAsync(_ => held.Task, TimeSpan.FromMilliseconds(100), CancellationToken.None))
            .ToArray();
        try
        {
            await Task.Delay(150);
            Stopwatch elapsed = Stopwatch.StartNew();
            await Should.ThrowAsync<ProcessReadAdmissionException>(() =>
                ProcessReadDeadline.RunAsync(_ => Task.FromResult(1), TimeSpan.FromSeconds(1), CancellationToken.None));
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(400));
        }
        finally
        {
            held.SetResult(1);
            try { await Task.WhenAll(timedOut); }
            catch (TimeoutException) { }
        }

        int? recovered = null;
        for (int attempt = 0; attempt < 100 && recovered is null; attempt++)
        {
            try
            {
                recovered = await ProcessReadDeadline.RunAsync(_ => Task.FromResult(1), TimeSpan.FromSeconds(1), CancellationToken.None);
            }
            catch (ProcessReadAdmissionException)
            {
                await Task.Delay(10);
            }
        }
        recovered.ShouldBe(1);
    }

    /// <summary>All adapter slots can be wedged while control reads still persist a degraded source.</summary>
    [Fact]
    public async Task AdapterSlotExhaustionLeavesControlReadsAvailableForSourceEvidence()
    {
        TaskCompletionSource<int> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>[] blocked = Enumerable.Range(0, 16)
            .Select(_ => ProcessReadDeadline.RunAsync(_ => held.Task, TimeSpan.FromMilliseconds(100), CancellationToken.None))
            .ToArray();
        try
        {
            await Task.Delay(150);
            await Should.ThrowAsync<ProcessReadAdmissionException>(() =>
                ProcessReadDeadline.RunAsync(_ => Task.FromResult(1), TimeSpan.FromSeconds(1), CancellationToken.None));
            InMemoryReadModelStore store = new();
            EventStoreProcessOptions options = new();
            ProcessSource source = new("tenant-a", "sample", "exhausted-slots");
            ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
            CheckpointedProcessRunner runner = new(Substitute.For<IEventStoreGatewayClient>(), Substitute.For<IProcessTranslator>(),
                Substitute.For<IProcessDelegationProvider>(), Substitute.For<ITrustedEffectSubmitter>(), checkpoints, Options.Create(options));

            ProcessRunResult result = await runner.RunAsync(source);

            result.Status.ShouldBe(ProcessSourceStatus.Retrying);
            result.ReasonCode.ShouldBe("read-worker-exhausted");
            ProcessSourceState persisted = await checkpoints.GetAsync(source);
            persisted.Checkpoint.ShouldBe(0);
            persisted.ReasonCode.ShouldBe("read-worker-exhausted");
        }
        finally
        {
            held.SetResult(1);
            try { await Task.WhenAll(blocked); }
            catch (TimeoutException) { }
        }
    }

    /// <summary>Operator authorization and audit remain available when all pure-adapter slots are exhausted.</summary>
    [Fact]
    public async Task AdapterSlotExhaustionDoesNotBlockAuditedDisposition()
    {
        TaskCompletionSource<int> held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>[] blocked = Enumerable.Range(0, 16)
            .Select(_ => ProcessReadDeadline.RunAsync(_ => held.Task, TimeSpan.FromMilliseconds(100), CancellationToken.None))
            .ToArray();
        try
        {
            await Task.Delay(150);
            await Should.ThrowAsync<ProcessReadAdmissionException>(() =>
                ProcessReadDeadline.RunAsync(_ => Task.FromResult(1), TimeSpan.FromSeconds(1), CancellationToken.None));
            InMemoryReadModelStore store = new();
            EventStoreProcessOptions options = new() { OperationTimeout = TimeSpan.FromSeconds(1) };
            ProcessSource source = new("tenant-a", "sample", "operator-slots");
            ProcessCheckpointStore checkpoints = new(store, Options.Create(options), TimeProvider.System);
            await checkpoints.EnsureCandidateAsync(source);
            ProcessSweepStore sweeps = new(store, Options.Create(options), TimeProvider.System);
            IProcessDispositionAuthorizer authorizer = Substitute.For<IProcessDispositionAuthorizer>();
            authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), source, Arg.Any<CancellationToken>()).Returns(true);
            IProcessDispositionAuditSink audit = Substitute.For<IProcessDispositionAuditSink>();
            ProcessDispositionService disposition = new(authorizer, audit, checkpoints, sweeps, Options.Create(options));
            ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator")], "synthetic"));

            (await disposition.RevalidateAsync(principal, source, "retry")).ShouldBeTrue();

            await authorizer.Received(1).AuthorizeAsync(principal, source, Arg.Any<CancellationToken>());
            await audit.Received(1).RecordAsync(principal, source, "retry", Arg.Any<CancellationToken>());
            (await checkpoints.GetAsync(source)).Status.ShouldBe(ProcessSourceStatus.Revalidating);
        }
        finally
        {
            held.TrySetResult(1);
            try { await Task.WhenAll(blocked); }
            catch (TimeoutException) { }
        }
    }
}
