using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainServiceTrustBoundaryTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("current-owner-denial")]
    [InlineData("forged-context")]
    [InlineData("forged-header")]
    [InlineData("absent-context")]
    public async Task OwnerSdkSeparatelyExposesVerifiedOriginAndStillRunsCurrentOwnerAuthorization(string scenario)
    {
        var origin = new OriginatingQueryContext("actor-1", "original-client", true, "delegation-1", ["read", "resolve"], ["public-gateway"]);
        var query = new QueryEnvelope("tenant-a", "widget", "widget-1", "owner-probe", "{}"u8.ToArray(), "corr", "actor-1")
        {
            OriginalActorId = "actor-1", AuthenticatedWorkloadId = "projects", IsDelegated = true, Audience = ["eventstore"],
            CallerAuthority = new("delegated-owner-query", [], ["read_metadata"])
                { OriginatingContext = scenario == "absent-context" ? null : origin },
        };
        int protectedCalls = 0;
        IHttpContextAccessor? accessor = null;
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns("widget"); handler.QueryType.Returns("owner-probe");
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            protectedCalls++;
            VerifiedQueryCaller verified = VerifiedQueryCaller.Get(accessor!.HttpContext).ShouldNotBeNull();
            verified.Query.AuthenticatedWorkloadId.ShouldBe("projects"); verified.Query.Audience.ShouldBe(["eventstore"]);
            verified.Query.DelegationId.ShouldBeNull(); verified.Query.Scopes.ShouldBeNull();
            accessor.HttpContext!.User.HasClaim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore").ShouldBeTrue();
            accessor.HttpContext.User.HasClaim("aud", "public-gateway").ShouldBeFalse();
            verified.Principal.HasClaim("eventstore:permission", "read_metadata").ShouldBeTrue();
            if (scenario == "absent-context") verified.OriginatingContext.ShouldBeNull();
            else verified.OriginatingContext.ShouldBe(origin);
            // A verified delegation provides evidence only: the owner still decides current actor/resource access.
            return scenario == "current-owner-denial" ? QueryResult.Failure("safe-denial")
                : QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { allowed = true }));
        });
        await using WebApplication app = await StartAsync(configureServices: services => services.AddSingleton(handler));
        accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
        Dictionary<string, object> bindings = QueryCallerAuthorityBinding.Bind(query).ToDictionary(pair => pair.Key, pair => (object)pair.Value);
        string assertion = Assertion([EventStoreWorkloadOperations.DomainServiceQuery], extraClaims: bindings);
        QueryEnvelope sent = scenario == "forged-context"
            ? query with { CallerAuthority = query.CallerAuthority with { OriginatingContext = origin with { DelegationId = "forged" } } }
            : query;
        if (scenario == "forged-header")
        {
            sent = query with { CallerAuthority = query.CallerAuthority with { OriginatingContext = null } };
            assertion = Assertion([EventStoreWorkloadOperations.DomainServiceQuery]);
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/query") { Content = JsonContent.Create(sent) };
        request.Headers.Add("X-Hexalith-Originating-Context", JsonSerializer.Serialize(origin));
        AddInternalHeaders(request, assertion, [ChannelToken]);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        QueryResult result = (await response.Content.ReadFromJsonAsync<QueryResult>(TestContext.Current.CancellationToken))!;
        bool forged = scenario is "forged-context" or "forged-header";
        result.Success.ShouldBe(!forged && scenario != "current-owner-denial");
        if (!result.Success) result.ErrorMessage.ShouldBe("safe-denial");
        protectedCalls.ShouldBe(forged ? 0 : 1);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("actor")]
    [InlineData("stripped")]
    [InlineData("tenant")]
    [InlineData("payload")]
    [InlineData("permission")]
    [InlineData("duplicate-binding")]
    public async Task SignedQueryAuthorityPreservesOriginalWireAndRebuildsAdministratorHint(string mutation)
    {
        var query = new QueryEnvelope("tenant-a", "widget", "widget-1", "authority-probe", "{}"u8.ToArray(), "authority-correlation", "actor-1", isGlobalAdmin: true)
        { OriginalActorId = "actor-1", CallerAuthority = new("authenticated-caller", ["ProjectUser"], ["read_metadata"]) };
        int calls = 0;
        IDomainQueryHandler handler = Substitute.For<IDomainQueryHandler>();
        handler.Domain.Returns("widget"); handler.QueryType.Returns("authority-probe");
        IHttpContextAccessor? accessor = null;
        handler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            calls++;
            QueryEnvelope dispatched = call.Arg<QueryEnvelope>();
            dispatched.IsGlobalAdmin.ShouldBeFalse();
            VerifiedQueryCaller caller = VerifiedQueryCaller.Get(accessor!.HttpContext).ShouldNotBeNull();
            caller.Query.ShouldBeSameAs(dispatched);
            caller.OriginalQuery.IsGlobalAdmin.ShouldBeTrue();
            caller.Principal.IsInRole("ProjectUser").ShouldBeTrue();
            caller.Principal.HasClaim("eventstore:permission", "read_metadata").ShouldBeTrue();
            accessor.HttpContext!.User.IsInRole("ProjectUser").ShouldBeFalse();
            return QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { allowed = true }));
        });
        await using WebApplication app = await StartAsync(configureServices: services => services.AddSingleton(handler));
        accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
        Dictionary<string, object> bindings = QueryCallerAuthorityBinding.Bind(query).ToDictionary(pair => pair.Key, pair => (object)pair.Value);
        if (mutation == "duplicate-binding") bindings[QueryCallerAuthorityBinding.DigestClaim] = new[] { bindings[QueryCallerAuthorityBinding.DigestClaim], bindings[QueryCallerAuthorityBinding.DigestClaim] };
        string assertion = Assertion([EventStoreWorkloadOperations.DomainServiceQuery], extraClaims: new Dictionary<string, object>
            (bindings));
        QueryEnvelope sent = mutation switch
        {
            "stripped" => query with { CallerAuthority = null },
            "actor" => query with { UserId = "other", OriginalActorId = "other" },
            "tenant" => query with { TenantId = "other" },
            "payload" => query with { Payload = "[]"u8.ToArray() },
            "permission" => query with { CallerAuthority = query.CallerAuthority with { Permissions = ["inspect_metadata"] } },
            _ => query,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/query") { Content = JsonContent.Create(sent) };
        AddInternalHeaders(request, assertion, [ChannelToken]);
        using HttpResponseMessage response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);
        if (mutation == "duplicate-binding") response.IsSuccessStatusCode.ShouldBeFalse();
        else
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            QueryResult result = (await response.Content.ReadFromJsonAsync<QueryResult>(TestContext.Current.CancellationToken))!;
            result.Success.ShouldBe(mutation == "valid");
            if (!result.Success) result.ErrorMessage.ShouldBe("safe-denial");
        }
        calls.ShouldBe(mutation == "valid" ? 1 : 0);
    }
}
