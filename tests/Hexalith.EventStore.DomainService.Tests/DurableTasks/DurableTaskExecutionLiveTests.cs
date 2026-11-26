using System.Net;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>The two native execution phases, run before and after the Platform provider restart.</summary>
/// <param name="output">The native xUnit receipt output.</param>
public sealed class DurableTaskExecutionLiveTests(ITestOutputHelper output)
{
    private const string Component = DurableTaskExecutionTests.Component;

    /// <summary>Persists the starting checkpoint and loses the second owner reply after its commit.</summary>
    [Fact]
    public async Task CommitWithLostOwnerReply()
    {
        using DaprClient first = DurableTaskAdmissionLiveTests.Client("HEXALITH_G1_PRIMARY_GRPC");
        using DaprClient second = DurableTaskAdmissionLiveTests.Client("HEXALITH_G1_REPLICA_GRPC");
        var primary = new DaprReadModelStore(first);
        var replica = new DaprReadModelStore(second);
        (DurableTaskAdmission admission, DurableTaskLease lease) = await DurableTaskExecutionTests.CommitWithLostReplyAsync(primary, replica,
            DurableTaskStepLookupRuntimeClient.IsConfigured ? VerifyBindingBeforeOwnerEffectAsync : null);

        await using var host = await DurableTaskStepLookupFixture.StartAsync(admission, lease.Task.Scope.Operation);
        DurableTaskStepLookupRequest request = DurableTaskStepLookupFixture.RequestFor(lease);
        string tenant = lease.Task.Scope.TenantId;
        DurableTaskStepLookupResult binding = await host.ExpectBindingAsync(lease, DurableTaskStatus.WaitingForDependency, null, acceptsExecution: true);
        // Real Dapr invocation attributes the call to sample-owner, so another signed caller fails authentication.
        HttpStatusCode wrongWorkload = DurableTaskStepLookupRuntimeClient.IsConfigured ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound;
        var denials = new
        {
            wrongWorkload = (int)await host.ExpectDenialAsync(request, tenant, wrongWorkload, workload: DurableTaskStepLookupFixture.UnregisteredWorkload),
            untrustedWorkload = (int)await host.ExpectDenialAsync(request, tenant, HttpStatusCode.Unauthorized, workload: "untrusted-owner"),
            missingChannel = (int)await host.ExpectDenialAsync(request, tenant, HttpStatusCode.Unauthorized, channel: false),
            missingAssertion = (int)await host.ExpectDenialAsync(request, tenant, HttpStatusCode.Unauthorized, assertion: false),
            wrongAssertionOperation = (int)await host.ExpectDenialAsync(request, tenant, HttpStatusCode.Forbidden, operation: EventStoreWorkloadOperations.DomainServiceQuery),
            wrongTenant = (int)await host.ExpectDenialAsync(request, "foreign-tenant"),
            wrongSignedDomain = (int)await host.ExpectDenialAsync(request, tenant, signedDomain: "foreign-domain"),
            wrongTargetDomain = (int)await host.ExpectDenialAsync(request with { TargetDomain = "foreign-domain" }, tenant, signedDomain: "foreign-domain"),
            wrongTargetId = (int)await host.ExpectDenialAsync(request with { TargetId = "foreign-target" }, tenant),
        };
        if (!DurableTaskStepLookupRuntimeClient.IsConfigured)
        {
            await host.ExpectDenialAsync(request, tenant: null);
            await host.ExpectDenialAsync(request, tenant, signedDomain: null);
        }

        await host.ExpectDenialAsync(request with { AdmissionOperation = DurableTaskStepLookupFixture.OtherOperation }, tenant);
        await host.ExpectDenialAsync(DurableTaskStepLookupFixture.RequestFor(lease, 1), tenant);
        await host.ExpectDenialAsync(request with { StepKey = $"{lease.Task.TaskId}/step/02" }, tenant);
        await host.ExpectDenialAsync(request with { RequestSha256 = new string('b', 64) }, tenant);
        await host.ExpectDenialAsync(request with { Fence = 0 }, tenant);
        await host.ExpectDenialAsync(request with { Fence = lease.Fence + 1 }, tenant);
        await VerifyNamedRecoveryRefusedAsync(primary, host, lease.Task);

        ReadModelEntry<string> owner = await replica.GetAsync<string>(Component, DurableTaskExecutionTests.OwnerKey(lease.Task, 2));
        output.WriteLine("g1-step-lookup " + JsonSerializer.Serialize(new
        {
            phase = "before-restart", transport = host.Transport, currentFence = lease.Fence, currentHttpStatus = 200, binding, denials,
            ownerReceipt = owner.Value, ownerETag = owner.ETag, scopeDenialsEmpty = true, namedRecoveryRefused = true,
        }));
    }

    /// <summary>Resumes from the retained store through new worker and sidecar processes.</summary>
    [Fact]
    public async Task RecoverCommittedReceiptAfterRestart()
    {
        using DaprClient first = DurableTaskAdmissionLiveTests.Client("HEXALITH_G1_PRIMARY_GRPC");
        using DaprClient second = DurableTaskAdmissionLiveTests.Client("HEXALITH_G1_REPLICA_GRPC");
        var primary = new DaprReadModelStore(first);
        var replica = new DaprReadModelStore(second);
        (DurableTaskAdmission admission, DurableTaskLease lease, long staleFence) = await DurableTaskExecutionTests.RecoverAfterLostReplyAsync(
            primary, replica, VerifyPendingLookupAfterTakeoverAsync);
        string key = DurableTaskExecutionTests.OwnerKey(lease.Task, 2);
        ReadModelEntry<string> owner = await primary.GetAsync<string>(Component, key);
        owner.Value.ShouldBe("receipt-2");
        owner.ETag.ShouldNotBeNullOrWhiteSpace();

        await using var host = await DurableTaskStepLookupFixture.StartAsync(admission, lease.Task.Scope.Operation);
        DurableTaskStepLookupRequest request = DurableTaskStepLookupFixture.RequestFor(lease);
        HttpStatusCode stale = await host.ExpectDenialAsync(request with { Fence = staleFence }, lease.Task.Scope.TenantId);
        DurableTaskStepLookupResult binding = await host.ExpectBindingAsync(lease, DurableTaskStatus.Running, owner.Value, acceptsExecution: false);
        (await replica.GetAsync<string>(Component, key)).ShouldBe(owner);
        output.WriteLine("g1-step-lookup " + JsonSerializer.Serialize(new
        {
            phase = "after-restart", transport = host.Transport, staleFence, staleHttpStatus = (int)stale, currentFence = lease.Fence,
            currentHttpStatus = 200, binding,
            ownerReceipt = owner.Value, ownerETag = owner.ETag, ownerRowUnchanged = true,
        }));
    }

    private async Task VerifyBindingBeforeOwnerEffectAsync(DurableTaskAdmission admission, DurableTaskLease lease)
    {
        await using var host = await DurableTaskStepLookupFixture.StartAsync(admission, lease.Task.Scope.Operation);
        DurableTaskStepLookupResult binding = await host.ExpectBindingAsync(lease, DurableTaskStatus.Running, null, acceptsExecution: true);
        output.WriteLine("g1-step-lookup " + JsonSerializer.Serialize(new
        {
            phase = "owner-before-effect", transport = host.Transport, currentFence = lease.Fence, currentHttpStatus = 200, binding,
        }));
    }

    private async Task VerifyPendingLookupAfterTakeoverAsync(DurableTaskAdmission admission, DurableTaskLease lease, long staleFence)
    {
        await using var host = await DurableTaskStepLookupFixture.StartAsync(admission, lease.Task.Scope.Operation);
        DurableTaskStepLookupRequest request = DurableTaskStepLookupFixture.RequestFor(lease);
        HttpStatusCode stale = await host.ExpectDenialAsync(request with { Fence = staleFence }, lease.Task.Scope.TenantId);
        DurableTaskStepLookupResult binding = await host.ExpectBindingAsync(lease, DurableTaskStatus.WaitingForDependency, null, acceptsExecution: true);
        output.WriteLine("g1-step-lookup " + JsonSerializer.Serialize(new
        {
            phase = "after-takeover-pending", transport = host.Transport, staleFence, staleHttpStatus = (int)stale, currentFence = lease.Fence,
            currentHttpStatus = 200, binding,
        }));
    }

    private static async Task VerifyNamedRecoveryRefusedAsync(IReadModelStore store, DurableTaskStepLookupFixture host,
        CommittedDurableTaskAdmission numberedTask)
    {
        DurableTaskScope scope = numberedTask.Scope with { Operation = DurableTaskStepLookupFixture.NamedOperation };
        IDurableTaskAdmissionAuthorizer authority = Substitute.For<IDurableTaskAdmissionAuthorizer>();
        authority.AuthorizeAsync(scope, numberedTask.RequestSha256, Arg.Any<CancellationToken>())
            .Returns(new DurableTaskAdmissionAuthority(true, false, null));
        var admission = new DurableTaskAdmission(store, Component, authority, TimeProvider.System);
        CommittedDurableTaskAdmission namedTask = (await admission.AdmitAsync(scope, numberedTask.RequestSha256)).Task.ShouldNotBeNull();
        var recovery = new DurableTaskRecovery(store, Component, TimeProvider.System, TimeSpan.FromMinutes(2));
        DurableTaskLease lease = (await recovery.TryClaimAsync(namedTask, "named-recovery-fixture")).ShouldNotBeNull();
        IDurableTaskStepOwner owner = Substitute.For<IDurableTaskStepOwner>();
        owner.LookupAsync(Arg.Any<DurableTaskStep>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DurableTaskOwnerReceipt?>(new IOException("Owner is unavailable.")));

        await Should.ThrowAsync<IOException>(() => recovery.RunStepAsync(namedTask, lease, "named", owner));
        DurableTaskStepLookupRequest request = DurableTaskStepLookupFixture.RequestFor(lease, 1);
        await host.ExpectDenialAsync(request, scope.TenantId);

        var receipt = new string('c', 64);
        owner.LookupAsync(Arg.Any<DurableTaskStep>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<DurableTaskOwnerReceipt?>(new(call.Arg<DurableTaskStep>(), receipt)));
        (await recovery.RunStepAsync(namedTask, lease, "named", owner)).ShouldBeTrue();
        (await admission.GetProgressAsync(scope, namedTask.RequestSha256)).ShouldNotBeNull().Receipts.ShouldBe([receipt]);
        await host.ExpectDenialAsync(request, scope.TenantId);
    }
}
