using System.Net;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Story 5.5 (BS-A): a domain service's trusted-effect submission carries the service's own workload assertion —
/// caller = its Dapr application id, audience <c>eventstore</c>, operation <c>eventstore:trusted-effect</c> — issued by
/// the shared trusted issuer, and never a forwarded human bearer or caller-supplied assertion.
/// </summary>
public sealed class DomainServiceTrustedEffectAssertionHandlerTests
{
    private const string AppId = "works";

    /// <summary>The trusted-effect route is recognized in every URI form the submitter produces.</summary>
    /// <param name="uri">The request URI.</param>
    /// <param name="expected">Whether the URI targets the trusted-effect route.</param>
    [Theory]
    [InlineData("http://localhost:3500/api/v1/trusted-effects", true)]
    [InlineData("http://localhost:3500/api/v1/trusted-effects/", true)]
    [InlineData("api/v1/trusted-effects", true)]
    [InlineData("http://localhost:3500/api/v1/commands", false)]
    [InlineData("http://localhost:3500/api/v1/trusted-effects-other", false)]
    public void IsTrustedEffectSubmission_MatchesOnlyTheTrustedEffectRoute(string uri, bool expected)
        => DomainServiceTrustedEffectAssertionHandler.IsTrustedEffectSubmission(new Uri(uri, UriKind.RelativeOrAbsolute)).ShouldBe(expected);

    /// <summary>
    /// End to end through the domain-service host registration: the submitter client attaches an assertion naming
    /// this service, the gateway audience, and only the trusted-effect operation, and strips forwarded credentials.
    /// </summary>
    [Fact]
    public async Task SubmitterClient_AttachesTheDomainServiceOwnTrustedEffectAssertion()
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = BuildDomainService(capture);
        HttpClient client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ITrustedEffectSubmitter));
        using var request = new HttpRequestMessage(HttpMethod.Post, HttpTrustedEffectSubmitter.Route)
        {
            Content = new StringContent("{}"),
        };
        string forwarded = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        request.Headers.Add("Authorization", "Bearer " + forwarded);
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "caller-supplied-assertion");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        capture.Authorization.ShouldBeNull();
        var token = new JsonWebToken(capture.Assertions.ShouldHaveSingleItem());
        token.Audiences.ShouldBe([EventStoreTrustedEffectSubmissionExtensions.DefaultGatewayAudience]);
        token.GetClaim(EventStoreWorkloadAuthenticationDefaults.CallerClaimType).Value.ShouldBe(AppId);
        token.Claims.Where(static claim => claim.Type == EventStoreWorkloadAuthenticationDefaults.OperationClaimType)
            .Select(static claim => claim.Value)
            .ShouldBe([EventStoreWorkloadOperations.TrustedEffect]);
        token.TryGetClaim("global_admin", out _).ShouldBeFalse();
        (token.ValidTo - token.IssuedAt).ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(WorkloadAuthenticationOptions.DefaultMaximumLifetimeSeconds));
    }

    /// <summary>Other requests on the same client are left untouched.</summary>
    [Fact]
    public async Task SubmitterClient_LeavesOtherRoutesUntouched()
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = BuildDomainService(capture);
        HttpClient client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ITrustedEffectSubmitter));

        using HttpResponseMessage response = await client.PostAsync("api/v1/commands", new StringContent("{}"), TestContext.Current.CancellationToken);

        capture.Assertions.ShouldBeEmpty();
    }

    /// <summary>When the issuer cannot issue, the submission leaves without an assertion and the gateway denies it.</summary>
    [Fact]
    public async Task SubmitterClient_WithoutIssuableAssertion_SendsNoCredential()
    {
        var capture = new CapturingHandler();
        await using ServiceProvider services = BuildDomainService(capture, environmentName: Environments.Production);
        HttpClient client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ITrustedEffectSubmitter));

        using HttpResponseMessage response = await client.PostAsync(HttpTrustedEffectSubmitter.Route, new StringContent("{}"), TestContext.Current.CancellationToken);

        capture.Assertions.ShouldBeEmpty();
        capture.Authorization.ShouldBeNull();
    }

    private static ServiceProvider BuildDomainService(CapturingHandler capture, string environmentName = "Development")
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["EventStore:DomainService:AppId"] = AppId,
            ["Authentication:JwtBearer:Issuer"] = "hexalith-dev",
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
        };
        var services = new ServiceCollection();
        _ = services.AddLogging();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        _ = services.AddSingleton(configuration);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        environment.ApplicationName.Returns("works-host");
        _ = services.AddSingleton(environment);
        _ = services.AddOptions<DomainProjectionIdentityOptions>().BindConfiguration("EventStore:DomainService");
        _ = services.AddEventStoreDomainServiceSecurity();
        _ = services.AddHttpClient<ITrustedEffectSubmitter, HttpTrustedEffectSubmitter>(
                client => client.BaseAddress = new Uri("http://localhost:3500/"))
            .AddEventStoreTrustedEffectWorkloadAssertion()
            .ConfigurePrimaryHttpMessageHandler(() => capture);
        return services.BuildServiceProvider();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        public IReadOnlyList<string> Assertions { get; private set; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.TryGetValues("Authorization", out IEnumerable<string>? authorization)
                ? authorization.Single()
                : null;
            Assertions = request.Headers.TryGetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, out IEnumerable<string>? values)
                ? [.. values]
                : [];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"effectId\":\"effect\",\"disposition\":\"Success\",\"replayed\":false}"),
            });
        }
    }
}
