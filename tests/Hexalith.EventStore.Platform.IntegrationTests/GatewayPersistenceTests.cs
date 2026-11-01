namespace Hexalith.EventStore.Platform.IntegrationTests;

using System.Net;

using Shouldly;
using StackExchange.Redis;

/// <summary>Requires a live gateway and real run-scoped persistence; no absent-environment skip is permitted.</summary>
public sealed class GatewayPersistenceTests(PlatformEnvironmentFixture fixture) : IClassFixture<PlatformEnvironmentFixture>
{
    /// <summary>Proves the declared gateway is ready and the supplied isolated store retains the exact synthetic payload.</summary>
    /// <returns>The real gateway and persistence checks.</returns>
    [Fact]
    public async Task GatewayReadinessAndOwnedPersistenceEndStateAsync()
    {
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, fixture.Descriptor.RequestCancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        using HttpResponseMessage ready = await fixture.Gateway.GetAsync("/ready", bound.Token).ConfigureAwait(true);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        Uri endpoint = fixture.Descriptor.GetEndpoint("redis", "eventstore", "tcp");
        ConfigurationOptions options = new()
        {
            EndPoints = { { endpoint.Host, endpoint.Port } },
            ConnectTimeout = 5000, SyncTimeout = 5000, AsyncTimeout = 5000, AbortOnConnectFail = true, AllowAdmin = false,
        };
        using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(options).WaitAsync(bound.Token).ConfigureAwait(true);
        IDatabase store = redis.GetDatabase();
        string key = fixture.Descriptor.DataIsolationId + ":gateway-persistence:" + fixture.Descriptor.SyntheticTenantId;
        string value = fixture.Descriptor.RunId + ":" + fixture.Descriptor.SyntheticTenantId;
        (await store.StringSetAsync(key, value, TimeSpan.FromMinutes(10), When.NotExists).WaitAsync(bound.Token).ConfigureAwait(true)).ShouldBeTrue();
        (await store.StringGetAsync(key).WaitAsync(bound.Token).ConfigureAwait(true)).ToString().ShouldBe(value);
        // Leave the asserted state intact for runner-owned teardown; the suite never stops infrastructure.
    }

    /// <summary>Verifies the live gateway rejects an anonymous command-status request.</summary>
    /// <returns>The real gateway authorization check.</returns>
    [Fact]
    public async Task GatewayRefusesAnonymousCommandStatusAsync()
    {
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, fixture.Descriptor.RequestCancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        Uri endpoint = fixture.Descriptor.GetEndpoint("eventstore", "eventstore", "http");
        using HttpClient anonymous = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = endpoint };
        using HttpResponseMessage response = await anonymous.GetAsync("/api/v1/commands/status/01J00000000000000000000000", bound.Token).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
