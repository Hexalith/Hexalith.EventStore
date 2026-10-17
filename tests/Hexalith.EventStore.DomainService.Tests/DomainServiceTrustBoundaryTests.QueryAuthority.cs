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
        string digest = QueryCallerAuthorityBinding.Bind(query)[QueryCallerAuthorityBinding.DigestClaim];
        string assertion = Assertion([EventStoreWorkloadOperations.DomainServiceQuery], extraClaims: new Dictionary<string, object>
        { [QueryCallerAuthorityBinding.DigestClaim] = mutation == "duplicate-binding" ? new[] { digest, digest } : digest });
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
