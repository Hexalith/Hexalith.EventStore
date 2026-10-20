using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public sealed class BoundOwnerQueryAuthorityTests
{
    [Fact]
    public void LegacyTwoFieldRequestKeepsItsExactProofSerialization()
    {
        using RSA key = RSA.Create(2048);
        var query = new SubmitQueryRequest("tenant-a", "folders", "folder-1", "OwnerRead", Payload: JsonSerializer.SerializeToElement(new { }));
        var request = new BoundOwnerQueryRequest(query, "actor-1");
        string legacy = JsonSerializer.Serialize(new { Query = query, PrincipalId = "actor-1" }, JsonSerializerOptions.Web);
        JsonSerializer.Serialize(request, JsonSerializerOptions.Web).ShouldBe(legacy);
        string proof = BoundOwnerQueryProof.Issue(request, "projects", Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow);
        BoundOwnerQueryProof.Verify(JsonSerializer.Deserialize<BoundOwnerQueryRequest>(legacy, JsonSerializerOptions.Web)!, proof, "projects",
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("nondelegated")]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("payload")]
    [InlineData("permission")]
    [InlineData("audience")]
    [InlineData("caller")]
    [InlineData("expired")]
    [InlineData("workload")]
    [InlineData("delegation")]
    [InlineData("scopes")]
    [InlineData("origin-context")]
    public async Task NestedOriginalAuthorityRequiresExactSignedActorTenantPayloadAudienceAndPermission(string fault)
    {
        using RSA key = RSA.Create(2048);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = WorkloadAssertionTestTokens.Issuer,
            ["Authentication:JwtBearer:Audience"] = "human-gateway", ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256", ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["Authentication:BoundStreamRead:PublicKeys:projects"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            ["Authentication:BoundOwnerQuery:AllowedQueries:projects"] = "OwnerRead",
        });
        builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(DaprInternalAuthenticationOptions.SchemeName, "Authentication:DaprInternal",
            (options, _) => { options.Audience = "  custom-owner-gateway  "; options.AllowedCallers.Add("projects"); });
        await using WebApplication app = builder.Build();
        Claim[] originalClaims = fault == "nondelegated" ? [new Claim("sub", "actor-1")] : [
            new Claim("sub", "actor-1"), new Claim("azp", "original-client"), new Claim("client_id", "gateway-client"),
            new Claim("act", "{\"sub\":\"delegation-1\"}"), new Claim("scope", "read resolve"), new Claim("aud", "public-gateway")];
        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(new ClaimsPrincipal(new ClaimsIdentity(originalClaims, "authenticated-user")), "actor-1");
        var original = new QueryEnvelope("tenant-a", "projects", "project-1", "Open", "{}"u8.ToArray(), "correlation", "actor-1")
        {
            OriginalActorId = identity.OriginalActorId, AuthenticatedWorkloadId = identity.AuthenticatedWorkloadId,
            IsDelegated = identity.IsDelegated, DelegationId = identity.DelegationId, Scopes = identity.Scopes, Audience = identity.Audience,
            CallerAuthority = new("authenticated-caller", ["ProjectUser"], ["read_metadata"]),
        };
        string assertion = WorkloadAssertionTestTokens.Create(fault == "audience" ? "other" : "projects", fault == "caller" ? "other" : "eventstore",
            [EventStoreWorkloadOperations.DomainServiceQuery], issuedAt: fault == "expired" ? DateTime.UtcNow.AddMinutes(-10) : null,
            bindings: QueryCallerAuthorityBinding.Bind(original));
        original = fault switch
        {
            "actor" => original with { UserId = "other", OriginalActorId = "other" },
            "tenant" => original with { TenantId = "other" },
            "payload" => original with { Payload = "[]"u8.ToArray() },
            "permission" => original with { CallerAuthority = original.CallerAuthority with { Permissions = ["read_metadata", "inspect_metadata"] } },
            "workload" => original with { AuthenticatedWorkloadId = "forged-client" },
            "delegation" => original with { DelegationId = "forged-delegation" },
            "scopes" => original with { Scopes = ["admin"] },
            "origin-context" => original with { CallerAuthority = original.CallerAuthority with { OriginatingContext = new("actor-1", "forged-client", true, "forged", null, null) } },
            _ => original,
        };
        var request = new BoundOwnerQueryRequest(new("tenant-a", "folders", "folder-1", "OwnerRead", Payload: JsonSerializer.SerializeToElement(new { })), "actor-1")
            { OriginatingQuery = original, OriginatingAssertion = assertion };
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(new SubmitQueryResult("correlation", JsonSerializer.SerializeToElement(new { }), null, null));
        var controller = new QueriesController(mediator, Substitute.For<IETagService>(), new ClaimsTenantValidator(), new ClaimsRbacValidator(),
            NullLogger<QueriesController>.Instance, configuration: builder.Configuration,
            assertionValidator: app.Services.GetRequiredService<WorkloadAssertionValidator>(),
            workloadOptions: app.Services.GetRequiredService<IOptionsMonitor<WorkloadAuthenticationOptions>>())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "workload:projects"), new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "projects")], "validated-workload")) } } };
        controller.Request.Headers[BoundOwnerQueryProof.HeaderName] = BoundOwnerQueryProof.Issue(request, "projects", Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow);
        IActionResult result = await controller.SubmitBoundOwnerAuthorityAsync(request, TestContext.Current.CancellationToken);
        if (fault is "valid" or "nondelegated")
        {
            result.ShouldBeOfType<OkObjectResult>();
            await mediator.Received(1).Send(Arg.Is<SubmitQuery>(query => query.CallerAuthority != null && query.CallerAuthority.Origin == "delegated-owner-query"
                && query.CallerAuthority.Roles.Count == 0 && query.CallerAuthority.Permissions.SequenceEqual(new[] { "read_metadata" })), Arg.Any<CancellationToken>());
            SubmitQuery submitted = (SubmitQuery)mediator.ReceivedCalls().Single(call => call.GetMethodInfo().Name == "Send").GetArguments()[0]!;
            submitted.AuthenticatedWorkloadId.ShouldBe("projects"); submitted.Audience.ShouldBe(["custom-owner-gateway"]);
            submitted.IsDelegated.ShouldBeTrue();
            submitted.DelegationId.ShouldBeNull(); submitted.Scopes.ShouldBeNull();
            OriginatingQueryContext context = submitted.CallerAuthority!.OriginatingContext.ShouldNotBeNull();
            context.OriginalActorId.ShouldBe("actor-1"); context.AuthenticatedWorkloadId.ShouldBe(identity.AuthenticatedWorkloadId);
            context.IsDelegated.ShouldBe(identity.IsDelegated); context.DelegationId.ShouldBe(identity.DelegationId);
            context.Scopes.ShouldBe(identity.Scopes); context.Audience.ShouldBe(identity.Audience);
            IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
            registry.SupportsQueryAsync(submitted.Domain, submitted.QueryType, Arg.Any<CancellationToken>()).Returns(true);
            IDomainQueryInvoker invoker = Substitute.For<IDomainQueryInvoker>();
            QueryEnvelope? routedQuery = null;
            invoker.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                routedQuery = call.Arg<QueryEnvelope>();
                return QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { }));
            });
            var router = new HandlerAwareQueryRouter(Substitute.For<IQueryRouter>(), registry, invoker, NullLogger<HandlerAwareQueryRouter>.Instance);
            (await router.RouteQueryAsync(submitted, TestContext.Current.CancellationToken)).Success.ShouldBeTrue();
            await invoker.Received(1).InvokeAsync(Arg.Is<QueryEnvelope>(routed => routed.CallerAuthority == submitted.CallerAuthority
                && routed.AuthenticatedWorkloadId == "projects" && routed.DelegationId == null
                && routed.Audience != null && routed.Audience.SequenceEqual(new[] { "custom-owner-gateway" })), Arg.Any<CancellationToken>());
            QueryEnvelope received = JsonSerializer.Deserialize<QueryEnvelope>(JsonSerializer.Serialize(routedQuery.ShouldNotBeNull(), JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
            string ownerAssertion = WorkloadAssertionTestTokens.Create("folders-owner", "eventstore",
                [EventStoreWorkloadOperations.DomainServiceQuery], bindings: QueryCallerAuthorityBinding.Bind(received));
            WorkloadAssertionEvaluation verifiedTransport = await app.Services.GetRequiredService<WorkloadAssertionValidator>().ValidateForAudienceAsync(
                DaprInternalAuthenticationOptions.SchemeName, ownerAssertion, EventStoreWorkloadOperations.DomainServiceQuery,
                ["eventstore"], "folders-owner", TestContext.Current.CancellationToken);
            verifiedTransport.Succeeded.ShouldBeTrue();
            var ownerHttp = new DefaultHttpContext { User = verifiedTransport.Principal! };
            IDomainQueryHandler ownerHandler = Substitute.For<IDomainQueryHandler>();
            ownerHandler.Domain.Returns(received.Domain); ownerHandler.QueryType.Returns(received.QueryType);
            ownerHandler.ExecuteAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                VerifiedQueryCaller verified = VerifiedQueryCaller.Get(ownerHttp).ShouldNotBeNull();
                verified.OriginatingContext.ShouldBe(context);
                verified.Query.AuthenticatedWorkloadId.ShouldBe("projects"); verified.Query.Audience.ShouldBe(["custom-owner-gateway"]);
                verified.Query.IsDelegated.ShouldBeTrue(); verified.Query.DelegationId.ShouldBeNull(); verified.Query.Scopes.ShouldBeNull();
                return QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { }));
            });
            await using ServiceProvider ownerServices = new ServiceCollection()
                .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = ownerHttp }).AddSingleton(ownerHandler).BuildServiceProvider();
            (await DomainQueryDispatcher.ExecuteAsync(ownerServices, received, TestContext.Current.CancellationToken)).Success.ShouldBeTrue();
            await ownerHandler.Received(1).ExecuteAsync(received, Arg.Any<CancellationToken>());
            VerifiedQueryCaller.Get(ownerHttp).ShouldBeNull();
            (submitted with { CallerAuthority = submitted.CallerAuthority with { OriginatingContext = context with { DelegationId = "other" } } }).ShouldNotBe(submitted);
        }
        else
        {
            result.ShouldBeOfType<ForbidResult>();
            await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        }
    }
}
