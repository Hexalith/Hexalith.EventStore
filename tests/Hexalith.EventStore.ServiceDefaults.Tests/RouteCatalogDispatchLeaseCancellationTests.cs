using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

public sealed class RouteCatalogDispatchLeaseCancellationTests
{
    [Fact]
    public async Task LeaseCancelledOnEntryNeitherInvokesNorRetainsTheFence()
    {
        int released = 0;
        RouteCatalogDispatchLease lease = new(null!, () => { released++; return Task.CompletedTask; });
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        bool invoked = false;

        await Should.ThrowAsync<OperationCanceledException>(() => lease.InvokeRemoteAsync(
            () => { invoked = true; return Task.FromResult(0); }, cancelled.Token));
        await lease.DisposeAsync();

        invoked.ShouldBeFalse();
        released.ShouldBe(1);
    }

    [Fact]
    public async Task LeaseFailureOnceRemoteWorkIsInFlightRetainsTheFence()
    {
        int released = 0;
        RouteCatalogDispatchLease lease = new(null!, () => { released++; return Task.CompletedTask; });
        using CancellationTokenSource source = new();

        await Should.ThrowAsync<OperationCanceledException>(() => lease.InvokeRemoteAsync<int>(
            () => { source.Cancel(); return Task.FromCanceled<int>(source.Token); }, source.Token));
        await lease.DisposeAsync();

        released.ShouldBe(0);
    }

    [Fact]
    public async Task GenericRemoteExecutionCancelledOnEntryNeitherEnrollsNorRetains()
    {
        IRouteCatalogRemoteBoundary boundary = Substitute.For<IRouteCatalogRemoteBoundary>();
        RouteCatalogRemoteExecution.Current = boundary;
        try
        {
            using CancellationTokenSource cancelled = new();
            await cancelled.CancelAsync();
            bool invoked = false;
            await Should.ThrowAsync<OperationCanceledException>(() => RouteCatalogRemoteExecution.InvokeAsync(
                () => { invoked = true; return Task.FromResult(0); }, cancelled.Token));
            invoked.ShouldBeFalse();
            boundary.ReceivedCalls().ShouldBeEmpty();
        }
        finally { RouteCatalogRemoteExecution.Current = null; }
    }
}
