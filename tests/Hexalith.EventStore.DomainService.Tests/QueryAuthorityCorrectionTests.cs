using System.Security.Claims;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class QueryAuthorityCorrectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerifiedFeatureIsClearedAfterHandlerResolutionOrDuplicateRouteThrows(bool duplicate)
    {
        QueryEnvelope query = Query();
        DefaultHttpContext context = Context(query);
        var services = new ServiceCollection().AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });
        if (duplicate)
        {
            for (int index = 0; index < 2; index++)
            {
                IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
                handler.Domain.Returns(query.Domain); handler.QueryType.Returns(query.QueryType);
                services.AddSingleton(handler);
            }
        }
        else services.AddTransient<IDomainQueryHandler>(_ => throw new InvalidOperationException("resolution failure"));
        await using ServiceProvider provider = services.BuildServiceProvider();
        await Should.ThrowAsync<InvalidOperationException>(() => DomainQueryDispatcher.ExecuteAsync(provider, query));
        VerifiedQueryCaller.Get(context).ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOwnerSigningConfigurationOrIssuerOutputIsUnavailable(bool configured)
    {
        QueryEnvelope query = Query();
        DefaultHttpContext context = Context(query);
        context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName] = "verified-original-assertion";
        var accessor = new HttpContextAccessor { HttpContext = context };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:WorkloadIssuer:Workload"] = configured ? "projects" : null,
            ["Authentication:BoundStreamRead:PrivateKey"] = configured ? Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) : null,
        }).Build();
        using DaprClient dapr = new DaprClientBuilder().Build();
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        var client = new DelegatedOwnerQueryClient(dapr, Substitute.For<IHttpClientFactory>(), issuer, accessor, configuration, TimeProvider.System);
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns(query.Domain); handler.QueryType.Returns(query.QueryType);
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(_ => client.InvokeAsync(new("tenant", "folders", "folder", "read")));
        await using ServiceProvider provider = new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).AddSingleton(handler).BuildServiceProvider();
        (await DomainQueryDispatcher.ExecuteAsync(provider, query)).ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
        (await client.InvokeAsync(new("tenant", "folders", "folder", "read"))).ErrorMessage.ShouldBe("safe-denial");
    }

    [Fact]
    public async Task GenuinelyUnboundLegacyQueryStillDispatchesWithoutAuthorityFeature()
    {
        QueryEnvelope query = Query() with { CallerAuthority = null };
        DefaultHttpContext context = Context(query);
        var identity = (ClaimsIdentity)context.User.Identity!;
        identity.RemoveClaim(identity.FindFirst(QueryCallerAuthorityBinding.DigestClaim)!);
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns(query.Domain); handler.QueryType.Returns(query.QueryType);
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            VerifiedQueryCaller.Get(context).ShouldBeNull();
            return QueryResult.FromPayload(System.Text.Json.JsonSerializer.SerializeToElement(new { legacy = true }));
        });
        await using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context }).AddSingleton(handler).BuildServiceProvider();
        (await DomainQueryDispatcher.ExecuteAsync(provider, query)).Success.ShouldBeTrue();
        await handler.Received(1).ExecuteAsync(query, Arg.Any<CancellationToken>());
    }

    private static QueryEnvelope Query() => new("tenant", "projects", "project", "read", [], "corr", "actor")
        { OriginalActorId = "actor", CallerAuthority = new("authenticated-caller", ["reader"], ["read_metadata"]) };
    private static DefaultHttpContext Context(QueryEnvelope query) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"),
            new Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreWorkloadOperations.DomainServiceQuery),
            new Claim(QueryCallerAuthorityBinding.DigestClaim, QueryCallerAuthorityBinding.Bind(query)[QueryCallerAuthorityBinding.DigestClaim])], "workload")),
    };
}
