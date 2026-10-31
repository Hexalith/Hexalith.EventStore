using System.Net;

using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Native channel-only callbacks require actual committed code and retain real durable dispatch fences.</summary>
public sealed class DomainSidecarCatalogLeaseTests
{
    [Fact]
    public async Task UncommittedCallbackCannotExecuteRealProcessor()
    {
        await using var host = await DomainSidecarLeaseTestHost.StartAsync();
        using var response = await host.DeliverAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        host.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ExactCommittedSidecarExecutesWithoutSyntheticSenderHeaders()
    {
        await using var host = await DomainSidecarLeaseTestHost.StartAsync();
        await host.ActivateAsync(host.Candidate());
        using var response = await host.DeliverAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        host.Calls.ShouldBe(1);
        (await host.Store.ReadAsync(default)).State!.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task MissingOrWrongRealChannelStillRefuses(string? token)
    {
        await using var host = await DomainSidecarLeaseTestHost.StartAsync();
        await host.ActivateAsync(host.Candidate());
        host.Client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
        if (token is not null) host.Client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, token);
        using var response = await host.DeliverAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        host.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("topic")]
    [InlineData("component")]
    [InlineData("code")]
    [InlineData("method")]
    [InlineData("operation")]
    public async Task MutatedSubscriptionBindingCannotActivateOrExecute(string mutation)
    {
        await using var host = await DomainSidecarLeaseTestHost.StartAsync();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.ActivateAsync(host.Candidate(mutation: mutation)));
        using var response = await host.DeliverAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        host.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PendingGenerationRefusesAndExactPendingRollbackRestoresCommittedCallback()
    {
        await using var host = await DomainSidecarLeaseTestHost.StartAsync();
        var committed = host.Candidate();
        await host.ActivateAsync(committed);
        var pending = host.Candidate(2, committed.RootDigest);
        await host.Authority.PrepareAsync(pending);
        using (var refused = await host.DeliverAsync()) refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        host.Calls.ShouldBe(0);
        await host.Authority.RollbackAsync(new(pending.AttemptId, RouteCatalogActivationBinding.ComputeDigest(pending), 1, "pending"));
        using var restored = await host.DeliverAsync();
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        host.Calls.ShouldBe(1);
        (await host.Store.ReadAsync(default)).State!.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task ActualPendingRemoteMutationBlocksRetirementUntilKnownCompletion()
    {
        await using var host = await DomainSidecarLeaseTestHost.StartAsync();
        var committed = host.Candidate();
        await host.ActivateAsync(committed);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Work = () => RouteCatalogRemoteCall.InvokeAsync(() => { entered.SetResult(); return complete.Task; });
        Task<HttpResponseMessage> delivery = host.DeliverAsync();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        (await host.Store.ReadAsync(default)).State!.Leases.Count.ShouldBe(1);
        var next = host.Candidate(2, committed.RootDigest);
        await host.Authority.PrepareAsync(next);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.Authority.ReadyAsync(next.AttemptId));
        complete.SetResult(true);
        using var response = await delivery;
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.Store.ReadAsync(default)).State!.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task AmbiguousRealRemoteCompletionRetainsFenceAfterProcessorCatchesFailure()
    {
        await using var host = await DomainSidecarLeaseTestHost.StartAsync();
        await host.ActivateAsync(host.Candidate());
        host.Work = () => RouteCatalogRemoteCall.InvokeAsync<bool>(() => Task.FromException<bool>(new TimeoutException("ambiguous remote completion")));
        using var response = await host.DeliverAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        host.Calls.ShouldBe(1);
        (await host.Store.ReadAsync(default)).State!.Leases.Count.ShouldBe(1);
    }
}
