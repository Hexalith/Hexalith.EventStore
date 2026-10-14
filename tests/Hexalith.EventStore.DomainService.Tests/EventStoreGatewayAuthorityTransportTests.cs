using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exercises complete authority credential acquisition and real gateway client routing.</summary>
public sealed class EventStoreGatewayAuthorityTransportTests
{
    /// <summary>Both supported grants supply a cached authority bearer to ordinary commands and stream reads.</summary>
    [Theory]
    [InlineData("password")]
    [InlineData("client_credentials")]
    public async Task AuthorityCredentialsAuthorizeOrdinaryClientCallsWithoutResourceBoundIssuance(string grant)
    {
        var token = new CaptureHandler(true);
        var gateway = new CaptureHandler(false);
        await using ServiceProvider provider = Build(token, gateway, grant: grant);
        var client = provider.GetRequiredService<IEventStoreGatewayClient>();
        _ = await client.SubmitCommandAsync(new SubmitCommandRequest("message", "tenant-a", "work", "item", "ResumeWorkItem", JsonSerializer.SerializeToElement(new { })), TestContext.Current.CancellationToken);
        _ = await client.ReadStreamAsync(new StreamReadRequest("tenant-a", "work", "item"), TestContext.Current.CancellationToken);
        gateway.Paths.ShouldBe(["/api/v1/commands", "/api/v1/streams/read"]);
        gateway.Authorizations.ShouldBe(["Bearer authority-access-token", "Bearer authority-access-token"]);
        gateway.HasWorkloadAssertion.ShouldBeFalse();
        token.Paths.ShouldBe(["/realms/hexalith/protocol/openid-connect/token"]);
        token.Form.ShouldNotBeNull().ShouldContain("grant_type=" + grant);
        token.Form.ShouldContain("client_id=hexalith-eventstore");
        token.Form.ShouldContain(grant == "password" ? "password=realm-secret" : "client_secret=realm-client-secret");
        token.Authorizations.ShouldBe([null]);
    }

    /// <summary>Credential acquisition cannot leave the configured authority or send cleartext outside Development.</summary>
    [Theory]
    [InlineData("https://other.example/token")]
    [InlineData("http://authority.example/realms/hexalith/token")]
    public async Task UntrustedTokenEndpointFailsBeforeAnyCredentialOrGatewayRequest(string endpoint)
    {
        var token = new CaptureHandler(true);
        var gateway = new CaptureHandler(false);
        await using ServiceProvider provider = Build(token, gateway, endpoint);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => provider.GetRequiredService<IEventStoreGatewayClient>()
            .ReadStreamAsync(new StreamReadRequest("tenant-a", "work", "item"), TestContext.Current.CancellationToken));
        token.Paths.ShouldBeEmpty(); gateway.Paths.ShouldBeEmpty();
    }

    /// <summary>Rejected credentials never produce an unauthenticated fallback gateway request.</summary>
    [Fact]
    public async Task RejectedCredentialsFailClosed()
    {
        var token = new CaptureHandler(true) { Reject = true };
        var gateway = new CaptureHandler(false);
        await using ServiceProvider provider = Build(token, gateway);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => provider.GetRequiredService<IEventStoreGatewayClient>()
            .ReadStreamAsync(new StreamReadRequest("tenant-a", "work", "item"), TestContext.Current.CancellationToken));
        gateway.Paths.ShouldBeEmpty();
    }

    /// <summary>The body deadline releases the singleton gate so a queued acquisition can complete.</summary>
    [Fact]
    public async Task StalledResponseBodyTimesOutAndReleasesTheAcquisitionGate()
    {
        var token = new CaptureHandler(true) { StallFirstBody = true };
        await using ServiceProvider provider = Build(token, new CaptureHandler(false), timeout: TimeSpan.FromMilliseconds(100));
        var source = provider.GetRequiredService<GatewayAuthorityAccessTokenProvider>();
        Task<string> stalled = source.GetAsync(TestContext.Current.CancellationToken);
        Task<string> queued = source.GetAsync(TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<OperationCanceledException>(() => stalled.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        (await queued.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBe("authority-access-token");
        token.Paths.Count.ShouldBe(2);
    }

    /// <summary>Expired tokens are replaced; a rejected renewal never reuses the expired bearer.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredTokenIsReplacedAndRejectedRenewalCannotReuseIt(bool rejected)
    {
        var token = new CaptureHandler(true) { ShortFirstToken = true, RejectCall = rejected ? 2 : 0 };
        var gateway = new CaptureHandler(false);
        await using ServiceProvider provider = Build(token, gateway);
        var client = provider.GetRequiredService<IEventStoreGatewayClient>();
        var request = new StreamReadRequest("tenant-a", "work", "item");
        _ = await client.ReadStreamAsync(request, TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(1100), TestContext.Current.CancellationToken);
        if (rejected)
            _ = await Should.ThrowAsync<InvalidOperationException>(() => client.ReadStreamAsync(request, TestContext.Current.CancellationToken));
        _ = await client.ReadStreamAsync(request, TestContext.Current.CancellationToken);
        _ = await client.ReadStreamAsync(request, TestContext.Current.CancellationToken);
        token.Paths.Count.ShouldBe(rejected ? 3 : 2);
        gateway.Authorizations.ShouldBe(["Bearer token-1", rejected ? "Bearer token-3" : "Bearer token-2",
            rejected ? "Bearer token-3" : "Bearer token-2"]);
    }

    private static ServiceProvider Build(CaptureHandler token, CaptureHandler gateway,
        string endpoint = "https://authority.example/realms/hexalith/protocol/openid-connect/token", string grant = "password", TimeSpan? timeout = null)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:Authentication:Authority"] = "https://authority.example/realms/hexalith",
            ["EventStore:Authentication:TokenEndpoint"] = endpoint,
            ["EventStore:Authentication:ClientId"] = "hexalith-eventstore",
            ["EventStore:Authentication:GrantType"] = grant,
            ["EventStore:Authentication:Username"] = "realm-user",
            ["EventStore:Authentication:Password"] = "realm-secret",
            ["EventStore:Authentication:ClientSecret"] = grant == "client_credentials" ? "realm-client-secret" : null,
        }).Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri("https://gateway.example/"))
            .AddEventStoreGatewayAuthorityTransport(configuration).ConfigurePrimaryHttpMessageHandler(() => gateway);
        _ = services.AddHttpClient("EventStoreGatewayAuthorityToken", client => client.Timeout = timeout ?? TimeSpan.FromSeconds(15)).ConfigurePrimaryHttpMessageHandler(() => token);
        return services.BuildServiceProvider();
    }

    private sealed class CaptureHandler(bool token) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string?> Authorizations { get; } = [];
        public bool HasWorkloadAssertion { get; private set; }
        public string? Form { get; private set; }
        public bool Reject { get; init; }
        public bool StallFirstBody { get; init; }
        public bool ShortFirstToken { get; init; }
        public int RejectCall { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Authorizations.Add(request.Headers.Authorization?.ToString());
            HasWorkloadAssertion |= request.Headers.Contains(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
            if (token)
            {
                Form = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (StallFirstBody && Paths.Count == 1)
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) };
                return new HttpResponseMessage(Reject || RejectCall == Paths.Count ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
                { Content = JsonContent.Create(new { access_token = ShortFirstToken ? "token-" + Paths.Count : "authority-access-token",
                    token_type = "Bearer", expires_in = ShortFirstToken && Paths.Count == 1 ? 1 : 300 }) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Paths[^1].Contains("commands", StringComparison.Ordinal)
                    ? JsonContent.Create(new SubmitCommandResponse("correlation"))
                    : JsonContent.Create(new StreamReadPage("tenant-a", "work", "item", [], new(0, null, null, 0, 0, false, null))),
            };
        }
    }
    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }
    }
}
