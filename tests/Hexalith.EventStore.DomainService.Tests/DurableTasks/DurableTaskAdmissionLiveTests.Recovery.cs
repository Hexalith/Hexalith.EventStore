using System.Diagnostics;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Exercises crash recovery through the native test entry point pinned by the Platform runner.</summary>
public sealed partial class DurableTaskAdmissionLiveTests
{
    private const string CrashWorkerTaskId = "HEXALITH_G1_CRASH_WORKER_TASK_ID";
    private const string StepName = "write-owner-effect@1";
    private static readonly DurableTaskScope RecoveryScope = new("g1-tenant", "original-actor", "g1.recover@1", "one-admitted-task");
    private static readonly string IntentHash = DurableTaskAdmission.Hash("one normalized G-1 recovery intent"u8);
    private static readonly DateTimeOffset FixtureTime = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    /// <summary>Kills the first worker after the owner commits and completes the task through the other sidecar.</summary>
    [Fact]
    public async Task WorkerCrashResumesCommittedCheckpointWithOneOwnerEffect()
    {
        using DaprClient first = Client("HEXALITH_G1_PRIMARY_GRPC");
        var firstStore = new DaprReadModelStore(first);
        var firstClock = Substitute.For<TimeProvider>();
        firstClock.GetUtcNow().Returns(FixtureTime);
        var admission = new DurableTaskAdmission(firstStore, Component, RecoveryAuthority(), firstClock);
        var firstReplica = new DurableTaskRecovery(firstStore, Component, firstClock, LeaseDuration);
        CancellationToken token = TestContext.Current.CancellationToken;

        string? crashTaskId = Environment.GetEnvironmentVariable(CrashWorkerTaskId);
        if (crashTaskId is not null)
        {
            CommittedDurableTaskAdmission admitted = (await admission.GetCommittedAsync(RecoveryScope, IntentHash, token)).ShouldNotBeNull();
            admitted.TaskId.ShouldBe(crashTaskId);
            DurableTaskLease lease = (await firstReplica.TryClaimAsync(admitted, "worker-a", token)).ShouldNotBeNull();
            await firstReplica.RunStepAsync(admitted, lease, StepName, new DurableTaskFixtureOwner(first, Component, admitted, true), token);
            throw new InvalidOperationException("The crash worker returned from an intentionally withheld owner response.");
        }

        using DaprClient second = Client("HEXALITH_G1_REPLICA_GRPC");
        var secondStore = new DaprReadModelStore(second);
        var secondClock = Substitute.For<TimeProvider>();
        secondClock.GetUtcNow().Returns(FixtureTime);
        var secondReplica = new DurableTaskRecovery(secondStore, Component, secondClock, LeaseDuration);
        CommittedDurableTaskAdmission task = (await admission.AdmitAsync(RecoveryScope, IntentHash, cancellationToken: token)).Task.ShouldNotBeNull();
        string taskKey = DurableTaskAdmission.ValidateAndGetKey(task.Scope, task.RequestSha256);
        var admittedRow = await ReadEvidenceAsync(first, taskKey, token);
        DurableTaskExecutionState interrupted;
        string ownerKey;
        DurableTaskOwnerReceipt receipt;
        int exitCode;

        using (Process worker = StartCrashWorker(task.TaskId))
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                while (true)
                {
                    worker.HasExited.ShouldBeFalse("worker-a must be blocked waiting for its owner response");
                    DurableTaskExecutionState? observed = (await secondStore.GetAsync<DurableTaskAdmissionState>(
                        Component, taskKey, deadline.Token)).Value.ShouldNotBeNull().Execution;
                    if (observed?.PreparedStep is { } checkpoint)
                    {
                        ownerKey = DurableTaskFixtureOwner.RecordKey(checkpoint);
                        DurableTaskFixtureOwnerState? owner = (await secondStore.GetAsync<DurableTaskFixtureOwnerState>(
                            Component, ownerKey, deadline.Token)).Value;
                        if (owner is not null)
                        {
                            interrupted = observed;
                            receipt = owner.Receipt;
                            owner.Effects.ShouldHaveSingleItem().ShouldBe(checkpoint);
                            receipt.Step.ShouldBe(checkpoint);
                            break;
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(50), deadline.Token);
                }
            }
            finally
            {
                if (!worker.HasExited)
                {
                    worker.Kill(entireProcessTree: true);
                }

                await worker.WaitForExitAsync(CancellationToken.None);
            }

            exitCode = worker.ExitCode;
        }

        exitCode.ShouldBe(137);
        interrupted.Owner.ShouldBe("worker-a");
        interrupted.Fence.ShouldBe(1);
        interrupted.PreparedStep.ShouldNotBeNull().Task.ShouldBe(task);
        interrupted.Progress.IsTerminal.ShouldBeFalse();
        (await secondReplica.TryClaimAsync(task, "worker-b", token)).ShouldBeNull();
        var before = await ReadEvidenceAsync(second, taskKey, token);
        var ownerBefore = await ReadEvidenceAsync(second, ownerKey, token);

        secondClock.GetUtcNow().Returns(FixtureTime + LeaseDuration);
        DurableTaskLease takeover = (await secondReplica.TryClaimAsync(task, "worker-b", token)).ShouldNotBeNull();
        takeover.Fence.ShouldBe(2);
        takeover.Task.TaskId.ShouldBe(task.TaskId);
        var claimed = await ReadEvidenceAsync(second, taskKey, token);
        DurableTaskExecutionState claimedState = (await secondStore.GetAsync<DurableTaskAdmissionState>(
            Component, taskKey, token)).Value.ShouldNotBeNull().Execution.ShouldNotBeNull();
        claimedState.ShouldBe(interrupted with
        {
            Owner = takeover.Owner,
            Fence = takeover.Fence,
            ExpiresAt = FixtureTime + LeaseDuration + LeaseDuration,
        });

        interrupted.ExpiresAt.ShouldNotBeNull().ShouldBeGreaterThan(firstClock.GetUtcNow());
        var staleOwner = new DurableTaskFixtureOwner(first, Component, task);
        var staleLease = new DurableTaskLease(task, interrupted.Owner!, interrupted.Fence);
        (await firstReplica.RunStepAsync(task, staleLease, StepName, staleOwner, token)).ShouldBeFalse();
        staleOwner.Calls.ShouldBeEmpty();
        var afterStale = await ReadEvidenceAsync(first, taskKey, token);
        afterStale.ETag.ShouldBe(claimed.ETag);
        afterStale.Bytes.ShouldBe(claimed.Bytes);

        var recoveringOwner = new DurableTaskFixtureOwner(second, Component, task);
        (await secondReplica.RunStepAsync(task, takeover, StepName, recoveringOwner, token)).ShouldBeTrue();
        recoveringOwner.Calls.ShouldBe(["lookup"]);

        DurableTaskExecutionState completed = claimedState with
        {
            Owner = null,
            ExpiresAt = null,
            Progress = claimedState.Progress with
            {
                Receipts = [receipt.ReceiptSha256],
                EffectPending = false,
                Status = DurableTaskStatus.Succeeded,
            },
        };
        foreach (IReadModelStore replica in new[] { firstStore, secondStore })
        {
            DurableTaskAdmissionState final = (await replica.GetAsync<DurableTaskAdmissionState>(
                Component, taskKey, token)).Value.ShouldNotBeNull();
            final.Task.ShouldBe(task);
            final.Execution.ShouldBe(completed);
            DurableTaskFixtureOwnerState owner = (await replica.GetAsync<DurableTaskFixtureOwnerState>(
                Component, ownerKey, token)).Value.ShouldNotBeNull();
            owner.Receipt.ShouldBe(receipt);
            owner.Effects.ShouldHaveSingleItem().ShouldBe(interrupted.PreparedStep);
        }

        var after = await ReadEvidenceAsync(first, taskKey, token);
        secondClock.GetUtcNow().Returns(claimedState.ExpiresAt!.Value);
        (await secondReplica.TryClaimAsync(task, "worker-c", token)).ShouldBeNull();
        var ownerAfter = await ReadEvidenceAsync(first, ownerKey, token);
        var peer = await ReadEvidenceAsync(second, taskKey, token);
        var ownerPeer = await ReadEvidenceAsync(second, ownerKey, token);
        peer.Bytes.ShouldBe(after.Bytes);
        peer.ETag.ShouldBe(after.ETag);
        ownerAfter.Bytes.ShouldBe(ownerBefore.Bytes);
        ownerAfter.ETag.ShouldBe(ownerBefore.ETag);
        ownerPeer.Bytes.ShouldBe(ownerAfter.Bytes);
        ownerPeer.ETag.ShouldBe(ownerAfter.ETag);
        Console.WriteLine("G1_RECOVERY_RECEIPT " + JsonSerializer.Serialize(new
        {
            WorkerExitCode = exitCode,
            TaskRecordKey = taskKey,
            OwnerRecordKey = ownerKey,
            AdmittedTask = Describe(admittedRow),
            BeforeTakeover = Describe(before),
            AfterTakeover = Describe(claimed),
            AfterStaleAttempt = Describe(afterStale),
            CompletedTask = Describe(after),
            CommittedOwner = Describe(ownerAfter),
            RecoveryOwnerCalls = recoveringOwner.Calls,
        }));
    }

    private static IDurableTaskAdmissionAuthorizer RecoveryAuthority()
    {
        IDurableTaskAdmissionAuthorizer authority = Substitute.For<IDurableTaskAdmissionAuthorizer>();
        authority.AuthorizeAsync(RecoveryScope, IntentHash, Arg.Any<CancellationToken>())
            .Returns(new DurableTaskAdmissionAuthority(true, false, null));
        return authority;
    }

    private static Process StartCrashWorker(string taskId)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add(typeof(DurableTaskAdmissionLiveTests).Assembly.Location);
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add(typeof(DurableTaskAdmissionLiveTests).FullName + "." + nameof(WorkerCrashResumesCommittedCheckpointWithOneOwnerEffect));
        start.ArgumentList.Add("-failSkips");
        start.Environment[CrashWorkerTaskId] = taskId;
        return Process.Start(start).ShouldNotBeNull();
    }

    private static async Task<(byte[] Bytes, string ETag)> ReadEvidenceAsync(
        DaprClient client, string key, CancellationToken cancellationToken)
    {
        (ReadOnlyMemory<byte> bytes, string etag) = await client.GetByteStateAndETagAsync(
            Component, key, consistencyMode: ConsistencyMode.Strong, cancellationToken: cancellationToken);
        etag.ShouldNotBeNullOrWhiteSpace();
        return (bytes.ToArray(), etag);
    }

    private static object Describe((byte[] Bytes, string ETag) entry) => new
    {
        entry.ETag,
        Sha256 = DurableTaskAdmission.Hash(entry.Bytes),
        PersistedBytes = Convert.ToBase64String(entry.Bytes),
    };
}
