using Hexalith.EventStore.Contracts.Routing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

public sealed class RouteCatalogRemoteCancellationTests
{
    [Fact]
    public async Task CancellationBeforeRemoteWorkStartsNeitherEnrollsNorRetains()
    {
        IRouteCatalogRemoteBoundary boundary = Substitute.For<IRouteCatalogRemoteBoundary>();
        RouteCatalogRemoteExecution.Current = boundary;
        try
        {
            using CancellationTokenSource cancelled = new();
            await cancelled.CancelAsync();
            bool invoked = false;
            await Should.ThrowAsync<OperationCanceledException>(() => RouteCatalogRemoteExecution.InvokeAsync(
                () => { invoked = true; return Task.CompletedTask; }, cancelled.Token));
            invoked.ShouldBeFalse();
            boundary.ReceivedCalls().ShouldBeEmpty();
        }
        finally { RouteCatalogRemoteExecution.Current = null; }
    }

    [Fact]
    public async Task FailureOnceRemoteWorkIsInFlightStillRetainsTheFence()
    {
        IRouteCatalogRemoteBoundary boundary = Substitute.For<IRouteCatalogRemoteBoundary>();
        RouteCatalogRemoteExecution.Current = boundary;
        try
        {
            using CancellationTokenSource source = new();
            await Should.ThrowAsync<OperationCanceledException>(() => RouteCatalogRemoteExecution.InvokeAsync(
                () => { source.Cancel(); return Task.FromCanceled(source.Token); }, source.Token));
            boundary.Received(1).Enroll();
            boundary.Received(1).Retain();
            boundary.Received(1).Complete();
        }
        finally { RouteCatalogRemoteExecution.Current = null; }
    }
}
