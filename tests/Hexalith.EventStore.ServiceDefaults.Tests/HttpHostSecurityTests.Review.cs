using System.Net;
using System.Net.Http.Headers;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

public sealed partial class HttpHostSecurityTests
{
    /// <summary>Includes actual inline policy metadata when rejecting alternate or unregistered schemes.</summary>
    [Theory]
    [InlineData("alternate")]
    [InlineData("unregistered")]
    public async Task InlinePolicyCannotAddAnAlternateScheme(string scheme)
    {
        await using var app = Build(services => services.AddAuthentication().AddJwtBearer("alternate", static _ => { }));
        app.MapGet("/alive", () => Results.Ok()).AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        int calls = 0;
        app.MapGet("/late", () => { calls++; return Results.Ok(); })
            .RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy)
            .RequireAuthorization(new AuthorizationPolicyBuilder(scheme).RequireAuthenticatedUser().Build());
        using HttpClient client = Client(app);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        using var response = await client.GetAsync("/late", TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        calls.ShouldBe(0);
        (await EventStoreHttpEndpointInventory.ValidateAsync(Endpoints(app), app.Services).ConfigureAwait(true)).ShouldNotBeEmpty();
    }

    /// <summary>Allows named and inline policies that resolve to the same human scheme.</summary>
    [Fact]
    public async Task InlinePolicyWithTheSameHumanSchemeSucceeds()
    {
        await using var app = Build();
        int calls = 0;
        app.MapGet("/human", () => { calls++; return Results.Ok(); })
            .RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy)
            .RequireAuthorization(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build());
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = Client(app);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        using var response = await client.GetAsync("/human", TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        calls.ShouldBe(1);
    }

    /// <summary>Normalizes framework route aliases before allowing a named human policy.</summary>
    [Theory]
    [InlineData("/DAPR/CONFIG/")]
    [InlineData("/Dapr/Subscribe/")]
    [InlineData("/HealthZ/")]
    [InlineData("/ACTORS/probe/id/")]
    public async Task FrameworkAliasesCannotUseHumanClassification(string route)
    {
        await using var app = Build();
        app.MapGet("/alive", () => Results.Ok()).AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        int calls = 0;
        app.MapGet(route, () => { calls++; return Results.Ok(); }).RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
        using HttpClient client = Client(app);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        using var response = await client.GetAsync(route, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        calls.ShouldBe(0);
    }

    /// <summary>Refuses a later options override that removes human credential-kind enforcement.</summary>
    [Fact]
    public async Task WeakHumanPolicyOverrideFailsStartup()
    {
        await using var app = Build(services => services.PostConfigure<AuthorizationOptions>(options =>
            options.AddPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy,
                new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build())));
        int calls = 0;
        app.MapGet("/human", () => { calls++; return Results.Ok(); }).RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
        await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
        calls.ShouldBe(0);
    }

    /// <summary>Preserves the internal human requirement when a named policy composes additional constraints.</summary>
    [Theory]
    [InlineData(false, HttpStatusCode.OK)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task ComposedHumanPolicyRetainsCredentialKind(bool workload, HttpStatusCode expected)
    {
        await using var app = Build(services => services.AddAuthorization(options => options.AddPolicy("composed",
            new AuthorizationPolicyBuilder().Combine(EventStoreHttpHostSecurityExtensions.CreateHumanPolicy()).RequireAssertion(static _ => true).Build())));
        int calls = 0;
        app.MapGet("/human", () => { calls++; return Results.Ok(); }).RequireAuthorization("composed");
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = Client(app);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(workload));
        using var response = await client.GetAsync("/human", TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(expected);
        calls.ShouldBe(workload ? 0 : 1);
    }
}
