using System.Net;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Qualifies the durable catalog fence shared only by one native workload read sequence.</summary>
public sealed class NativeSourceReadScopeTests
{
    /// <summary>Two guarded native HTTP calls retain one durable lease and validate each request.</summary>
    [Fact]
    public async Task GuardedNativeHttpReadsReuseOneLeaseWithoutSkippingPerRequestValidation()
    {
        (RouteCatalogTestFixture fixture, RouteCatalogAuthority authority) = await NativeFixtureAsync();
        var scope = new RouteCatalogInvocationScope(authority);
        int delivered = 0;
        var options = Options.Create(new RouteCatalogHostOptions
        {
            InvocationTransportFactory = _ => new MockHttpMessageHandler(_ =>
            {
                delivered++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
        });
        using var client = new HttpClient(new RouteCatalogInvocationHandler(authority,
            new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            options, false, scope));
        const string address = "http://fixture/v1.0/invoke/eventstore/method/api/v1/streams/read/workload";
        _ = await scope.ExecuteNativeSourceReadAsync("eventstore", async () =>
        {
            using var first = await client.PostAsync(address, null);
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var second = await client.PostAsync(address, null);
            second.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
            return true;
        });
        delivered.ShouldBe(2);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>The exact target and method share one lease until the callback returns.</summary>
    [Fact]
    public async Task ExactNativeRequestsShareOneLeaseUntilCallbackReturns()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        RouteCatalogAuthority authority = fixture.Authorities[0];
        var scope = new RouteCatalogInvocationScope(authority);
        _ = await scope.ExecuteNativeSourceReadAsync("eventstore", async () =>
        {
            RouteCatalogDispatchLease first = scope.LeaseFor(authority, "eventstore", "api/v1/streams/read/workload", HttpMethod.Post)!;
            first.ShouldNotBeNull();
            await authority.ValidateNativeReadLeaseAsync(first, TestContext.Current.CancellationToken);
            RouteCatalogDispatchLease second = scope.LeaseFor(authority, "eventstore", "api/v1/streams/read/workload", HttpMethod.Post)!;
            second.ShouldBeSameAs(first);
            await authority.ValidateNativeReadLeaseAsync(second, TestContext.Current.CancellationToken);
            (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
            scope.LeaseFor(authority, "another-gateway", "api/v1/streams/read/workload", HttpMethod.Post).ShouldBeNull();
            scope.LeaseFor(authority, "eventstore", "api/v1/streams/read", HttpMethod.Post).ShouldBeNull();
            scope.LeaseFor(authority, "eventstore", "api/v1/streams/read/workload", HttpMethod.Get).ShouldBeNull();
            return true;
        });
        scope.LeaseFor(authority, "eventstore", "api/v1/streams/read/workload", HttpMethod.Post).ShouldBeNull();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Fresh local host observation still fails closed inside a shared lease.</summary>
    [Fact]
    public async Task LocalObservationDriftStillRefusesEachScopedNativeRequest()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        RouteCatalogAuthority authority = fixture.Authorities[0];
        var scope = new RouteCatalogInvocationScope(authority);
        _ = await scope.ExecuteNativeSourceReadAsync("eventstore", async () =>
        {
            fixture.Recapture = observation => observation with { AuthenticationProfile = [1, 2, 3] };
            RouteCatalogDispatchLease lease = scope.LeaseFor(authority, "eventstore", "api/v1/streams/read/workload", HttpMethod.Post)!;
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
                authority.ValidateNativeReadLeaseAsync(lease, TestContext.Current.CancellationToken));
            return true;
        });
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>A failed local read releases its fence and cannot leave an ambient scope for later work.</summary>
    [Fact]
    public async Task FailedNativeReadClosesTheScopeAndReleasesItsLease()
    {
        (RouteCatalogTestFixture fixture, RouteCatalogAuthority authority) = await NativeFixtureAsync();
        var scope = new RouteCatalogInvocationScope(authority);
        await Should.ThrowAsync<InvalidOperationException>(() => scope.ExecuteNativeSourceReadAsync("eventstore", () =>
        {
            scope.LeaseFor(authority, "eventstore", "api/v1/streams/read/workload", HttpMethod.Post).ShouldNotBeNull();
            return Task.FromException<bool>(new InvalidOperationException("Controlled local read failure."));
        }));
        scope.LeaseFor(authority, "eventstore", "api/v1/streams/read/workload", HttpMethod.Post).ShouldBeNull();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>A child task that inherited the execution context cannot start work after the enclosing read ends.</summary>
    [Fact]
    public async Task DetachedNativeReadCannotBorrowAReleasedLease()
    {
        (RouteCatalogTestFixture fixture, RouteCatalogAuthority authority) = await NativeFixtureAsync();
        var scope = new RouteCatalogInvocationScope(authority);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<RouteCatalogDispatchLease?>? detached = null;
        _ = await scope.ExecuteNativeSourceReadAsync("eventstore", () =>
        {
            detached = Task.Run(async () =>
            {
                await resume.Task;
                return scope.LeaseFor(authority, "eventstore", "api/v1/streams/read/workload", HttpMethod.Post);
            });
            return Task.FromResult(true);
        });
        resume.SetResult();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(async () => _ = await detached!);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    private static async Task<(RouteCatalogTestFixture Fixture, RouteCatalogAuthority Authority)> NativeFixtureAsync()
    {
        var fixture = new RouteCatalogTestFixture();
        var operation = new RouteCatalogAuxiliaryOperation("eventstore", "api/v1/streams/read/workload",
            "eventstore:stream-read", "gateway");
        int owner = Array.FindIndex(fixture.Observations, observation => observation.AppId == "eventstore");
        fixture.UpdateObservation(owner, fixture.Observations[owner] with { AuxiliaryOperations = [operation] });
        RouteCatalogActivation candidate = fixture.Candidate() with { AuxiliaryOperations = [operation] };
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        await fixture.ActivateAsync(candidate);
        return (fixture, fixture.Authorities[owner]);
    }
}
