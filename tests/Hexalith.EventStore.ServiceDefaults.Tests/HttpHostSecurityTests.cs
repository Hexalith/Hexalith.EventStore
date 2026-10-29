using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

/// <summary>Exercises the common boundary through real Kestrel HTTP requests.</summary>
public sealed partial class HttpHostSecurityTests
{
    private const string Channel = "story-514-real-channel-token";
    private const string SigningKey = "story-514-test-signing-key-at-least-32-bytes";

    /// <summary>Verifies framework Delivery Requires Actual Channel Token In Development.</summary>
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("wrong", HttpStatusCode.Unauthorized)]
    [InlineData(Channel, HttpStatusCode.OK)]
    public async Task FrameworkDeliveryRequiresActualChannelTokenInDevelopment(string? token, HttpStatusCode expected)
    {
        await using WebApplication app = Build();
        int calls = 0;
        app.MapPost("/delivery", () => { calls++; return Results.Ok(); }).AllowAnonymous().RequireEventStoreChannel();
        app.MapGet("/dapr/config", () => Results.Ok()).RequireEventStoreChannel().AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/delivery");
        if (token is not null) request.Headers.Add(DaprAppChannelToken.HeaderName, token);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(expected);
        calls.ShouldBe(expected == HttpStatusCode.OK ? 1 : 0);
        (await EventStoreHttpEndpointInventory.ValidateAsync(Endpoints(app), app.Services).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>Verifies framework Delivery Rejects Duplicate And Mixed Credentials Before Handler.</summary>
    [Theory]
    [InlineData("duplicate")]
    [InlineData("human")]
    [InlineData("workload")]
    public async Task FrameworkDeliveryRejectsDuplicateAndMixedCredentialsBeforeHandler(string credential)
    {
        await using WebApplication app = Build();
        int calls = 0;
        app.MapPost("/delivery", () => { calls++; return Results.Ok(); }).RequireEventStoreChannel();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/delivery");
        request.Headers.TryAddWithoutValidation(DaprAppChannelToken.HeaderName, credential == "duplicate" ? [Channel, Channel] : [Channel]);
        if (credential == "human") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        if (credential == "workload") request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token(workload: true));
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        calls.ShouldBe(0);
    }

    /// <summary>Verifies human Policy Admits Only Validated Human Credentials.</summary>
    [Theory]
    [InlineData("human", HttpStatusCode.OK)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("channel", HttpStatusCode.Unauthorized)]
    [InlineData("workload", HttpStatusCode.Forbidden)]
    [InlineData("mixed", HttpStatusCode.Forbidden)]
    public async Task HumanPolicyAdmitsOnlyValidatedHumanCredentials(string credential, HttpStatusCode expected)
    {
        await using WebApplication app = Build();
        int calls = 0;
        app.MapPost("/human", () => { calls++; return Results.Ok(); }).RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/human");
        if (credential is "human" or "workload" or "mixed") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(credential == "workload"));
        if (credential == "channel") request.Headers.Add(DaprAppChannelToken.HeaderName, Channel);
        if (credential == "mixed") request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "assertion");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(expected);
        calls.ShouldBe(expected == HttpStatusCode.OK ? 1 : 0);
    }

    /// <summary>Verifies anonymous Inventory Accepts Only Exact Probes.</summary>
    [Theory]
    [InlineData("/health", true)]
    [InlineData("/alive", true)]
    [InlineData("/ready", true)]
    [InlineData("/health/child", false)]
    [InlineData("/healthz", false)]
    [InlineData("/ready{suffix}", false)]
    [InlineData("/Health", false)]
    public async Task AnonymousInventoryAcceptsOnlyExactProbes(string route, bool allowed)
    {
        await using WebApplication app = Build();
        app.MapGet(route, () => Results.Ok()).AllowAnonymous();
        if (allowed)
        {
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using HttpClient client = Client(app);
            using HttpResponseMessage response = await client.GetAsync(route, TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
        }
    }

    /// <summary>Verifies startup Rejects Missing Or Ineffective Classification.</summary>
    [Theory]
    [InlineData("unclassified")]
    [InlineData("unnamed")]
    [InlineData("unresolved-policy")]
    [InlineData("unresolved-scheme")]
    [InlineData("unbound-scheme")]
    [InlineData("framework-human")]
    public async Task StartupRejectsMissingOrIneffectiveClassification(string failure)
    {
        await using WebApplication app = Build(services => services.AddAuthorization(options =>
        {
            options.AddPolicy("missing-scheme", policy => policy.AddAuthenticationSchemes("absent").RequireAuthenticatedUser());
            options.AddPolicy("unbound", policy => policy.RequireAuthenticatedUser());
        }));
        var endpoint = app.MapGet(failure == "framework-human" ? "/dapr/subscribe" : "/endpoint", () => Results.Ok());
        if (failure == "unnamed") endpoint.RequireAuthorization();
        if (failure == "unresolved-policy") endpoint.RequireAuthorization("absent");
        if (failure == "unresolved-scheme") endpoint.RequireAuthorization("missing-scheme");
        if (failure == "unbound-scheme") endpoint.RequireAuthorization("unbound");
        if (failure == "framework-human") endpoint.RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
        await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    /// <summary>Verifies late Unclassified Endpoint Denies Every Credential Before Handler.</summary>
    [Theory]
    [InlineData("anonymous")]
    [InlineData("human")]
    [InlineData("channel")]
    [InlineData("workload")]
    public async Task LateUnclassifiedEndpointDeniesEveryCredentialBeforeHandler(string credential)
    {
        await using WebApplication app = Build();
        app.MapGet("/alive", () => Results.Ok()).AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        int calls = 0;
        app.MapGet("/late", () => { calls++; return Results.Ok(); });
        using HttpClient client = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/late");
        if (credential is "human" or "workload") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(credential == "workload"));
        if (credential == "channel") request.Headers.Add(DaprAppChannelToken.HeaderName, Channel);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        calls.ShouldBe(0);
        (await EventStoreHttpEndpointInventory.ValidateAsync(Endpoints(app), app.Services).ConfigureAwait(true)).ShouldNotBeEmpty();
    }

    /// <summary>Verifies conforming Host Rejects Embedded Authority Even With Fixture Flag.</summary>
    [Fact]
    public async Task ConformingHostRejectsEmbeddedAuthorityEvenWithFixtureFlag()
    {
        await using WebApplication app = Build();
        app.Configuration[WorkloadAuthorityOptions.SectionName + ":Enabled"] = "true";
        app.Configuration[WorkloadAuthorityOptions.SectionName + ":NonconformingDevelopmentFixture"] = "true";
        await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    /// <summary>Verifies probe Request Path Must Match Exactly.</summary>
    [Theory]
    [InlineData("/health/", HttpStatusCode.Forbidden)]
    [InlineData("/Health", HttpStatusCode.Forbidden)]
    [InlineData("/health/child", HttpStatusCode.NotFound)]
    public async Task ProbeRequestPathMustMatchExactly(string path, HttpStatusCode expected)
    {
        await using WebApplication app = Build();
        app.MapGet("/health", () => Results.Ok()).AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = Client(app);
        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(expected);
    }

    /// <summary>Verifies replacing Denying Fallback Fails Startup.</summary>
    [Fact]
    public async Task ReplacingDenyingFallbackFailsStartup()
    {
        await using WebApplication app = Build(services => services.PostConfigure<AuthorizationOptions>(options =>
            options.FallbackPolicy = EventStoreHttpHostSecurityExtensions.CreateHumanPolicy()));
        app.MapGet("/health", () => Results.Ok()).AllowAnonymous();
        await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    /// <summary>Verifies embedded Bootstrap Requires Explicit Nonconforming Development Fixture.</summary>
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task EmbeddedBootstrapRequiresExplicitNonconformingDevelopmentFixture(string environment, bool fixture)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration[WorkloadAuthorityOptions.SectionName + ":Enabled"] = "true";
        builder.Configuration[WorkloadAuthorityOptions.SectionName + ":NonconformingDevelopmentFixture"] = fixture.ToString();
        builder.Services.AddEventStoreWorkloadBindingAuthority(builder.Configuration);
        await using WebApplication app = builder.Build();
        Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(() => app.MapEventStoreWorkloadBindingAuthority());
    }

    /// <summary>Verifies missing Runtime Inventory Enrollment Fails Startup.</summary>
    [Fact]
    public async Task MissingRuntimeInventoryEnrollmentFailsStartup()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.Services.AddEventStoreHttpHostSecurity();
        await using WebApplication app = builder.Build();
        app.MapGet("/unclassified", () => Results.Ok());
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
        failure.Message.ShouldContain("UseEventStoreHttpHostSecurity");
    }

    private static WebApplication Build(Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = Channel;
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            JwtBearerAuthenticationContract.Configure(options, new JwtBearerAuthenticationOptions
            { Issuer = "story514", Audience = "story514", SigningKey = SigningKey }));
        builder.Services.AddEventStoreHttpHostSecurity();
        configure?.Invoke(builder.Services);
        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseEventStoreHttpHostSecurity();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    private static IEnumerable<Endpoint> Endpoints(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints);

    private static HttpClient Client(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
    };

    private static string Token(bool workload = false) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "story514", Audience = "story514", Expires = DateTime.UtcNow.AddMinutes(5),
        Claims = workload ? new Dictionary<string, object> { [EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType] = "peer" }
            : new Dictionary<string, object> { ["sub"] = "human" },
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
    });
}
