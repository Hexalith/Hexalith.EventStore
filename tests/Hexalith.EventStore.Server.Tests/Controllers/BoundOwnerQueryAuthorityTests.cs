using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
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
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("payload")]
    [InlineData("permission")]
    [InlineData("audience")]
    [InlineData("caller")]
    [InlineData("expired")]
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
            (options, _) => { options.Audience = "eventstore"; options.AllowedCallers.Add("projects"); });
        await using WebApplication app = builder.Build();
        var original = new QueryEnvelope("tenant-a", "projects", "project-1", "Open", "{}"u8.ToArray(), "correlation", "actor-1")
            { OriginalActorId = "actor-1", CallerAuthority = new("authenticated-caller", ["ProjectUser"], ["read_metadata"]) };
        string assertion = WorkloadAssertionTestTokens.Create(fault == "audience" ? "other" : "projects", fault == "caller" ? "other" : "eventstore",
            [EventStoreWorkloadOperations.DomainServiceQuery], issuedAt: fault == "expired" ? DateTime.UtcNow.AddMinutes(-10) : null,
            bindings: QueryCallerAuthorityBinding.Bind(original));
        original = fault switch
        {
            "actor" => original with { UserId = "other", OriginalActorId = "other" },
            "tenant" => original with { TenantId = "other" },
            "payload" => original with { Payload = "[]"u8.ToArray() },
            "permission" => original with { CallerAuthority = original.CallerAuthority with { Permissions = ["read_metadata", "inspect_metadata"] } },
            _ => original,
        };
        var request = new BoundOwnerQueryRequest(new("tenant-a", "folders", "folder-1", "OwnerRead", Payload: JsonSerializer.SerializeToElement(new { })), "actor-1")
            { OriginatingQuery = original, OriginatingAssertion = assertion };
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(new SubmitQueryResult("correlation", JsonSerializer.SerializeToElement(new { }), null, null));
        var controller = new QueriesController(mediator, Substitute.For<IETagService>(), new ClaimsTenantValidator(), new ClaimsRbacValidator(),
            NullLogger<QueriesController>.Instance, configuration: builder.Configuration,
            assertionValidator: app.Services.GetRequiredService<WorkloadAssertionValidator>())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "workload:projects"), new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "projects")], "validated-workload")) } } };
        controller.Request.Headers[BoundOwnerQueryProof.HeaderName] = BoundOwnerQueryProof.Issue(request, "projects", Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow);
        IActionResult result = await controller.SubmitBoundOwnerAuthorityAsync(request, TestContext.Current.CancellationToken);
        if (fault == "valid")
        {
            result.ShouldBeOfType<OkObjectResult>();
            await mediator.Received(1).Send(Arg.Is<SubmitQuery>(query => query.CallerAuthority != null && query.CallerAuthority.Origin == "delegated-owner-query"
                && query.CallerAuthority.Roles.Count == 0 && query.CallerAuthority.Permissions.SequenceEqual(new[] { "read_metadata" })), Arg.Any<CancellationToken>());
        }
        else
        {
            result.ShouldBeOfType<ForbidResult>();
            await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        }
    }
}
