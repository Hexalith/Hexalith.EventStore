using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public sealed class BoundOwnerQueryControllerTests
{
    [Fact]
    public async Task Signed_query_binds_caller_principal_payload_and_allowlisted_type()
    {
        using RSA key = RSA.Create(2048);
        string privateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:BoundStreamRead:PublicKeys:chatbot"] = publicKey,
            ["Authentication:BoundOwnerQuery:AllowedQueries:chatbot"] = "ResolveTenantOwnerAuthority",
        }).Build();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitQueryResult("corr-1", JsonSerializer.SerializeToElement(new { allowed = true }), null, null));
        var controller = new QueriesController(mediator, Substitute.For<IETagService>(),
            Substitute.For<ITenantValidator>(), Substitute.For<IRbacValidator>(),
            NullLogger<QueriesController>.Instance, configuration: config)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "workload:chatbot"),
                        new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "chatbot"),
                    ], "workload")),
                },
            },
        };
        var request = new BoundOwnerQueryRequest(new SubmitQueryRequest("tenant-a", "tenants", "tenant-a",
            "ResolveTenantOwnerAuthority", Payload: JsonSerializer.SerializeToElement(new { tenantId = "tenant-a" })), "user-a");
        controller.Request.Headers[BoundOwnerQueryProof.HeaderName] = BoundOwnerQueryProof.Issue(request, "chatbot", privateKey, DateTimeOffset.UtcNow);

        (await controller.SubmitBoundOwnerAuthorityAsync(request, CancellationToken.None)).ShouldBeOfType<OkObjectResult>();
        _ = await mediator.Received(1).Send(Arg.Is<SubmitQuery>(value => value.UserId == "user-a"
            && value.AuthenticatedWorkloadId == "chatbot" && value.OriginalActorId == "user-a"
            && value.Tenant == "tenant-a"), Arg.Any<CancellationToken>());
        (await controller.SubmitBoundOwnerAuthorityAsync(request with { PrincipalId = "user-b" }, CancellationToken.None))
            .ShouldBeOfType<ForbidResult>();
        (await controller.SubmitBoundOwnerAuthorityAsync(request with { Query = request.Query with
            { Payload = JsonSerializer.SerializeToElement(new { tenantId = "tenant-b" }) } }, CancellationToken.None))
            .ShouldBeOfType<ForbidResult>();
        (await controller.SubmitBoundOwnerAuthorityAsync(request with { Query = request.Query with
            { QueryType = "OtherQuery" } }, CancellationToken.None)).ShouldBeOfType<ForbidResult>();
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "workload:other"),
            new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "chatbot"),
        ], "workload"));
        (await controller.SubmitBoundOwnerAuthorityAsync(request, CancellationToken.None)).ShouldBeOfType<ForbidResult>();
        await mediator.Received(1).Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }
}
