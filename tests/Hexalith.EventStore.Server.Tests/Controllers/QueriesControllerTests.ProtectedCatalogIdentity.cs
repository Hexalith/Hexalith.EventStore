using System.Security.Claims;
using System.Text.Json;
using Hexalith.EventStore.Client.Security;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.Server.Identity;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public partial class QueriesControllerTests
{
    /// <summary>The legacy identity-reader wire gains no generic internal-operation exception.</summary>
    [Theory]
    [InlineData("allowed")]
    [InlineData("missing-surface")]
    [InlineData("ui-surface")]
    [InlineData("forged-header")]
    [InlineData("unprotected-query")]
    [InlineData("unconfigured-owner")]
    [InlineData("denied-proof")]
    [InlineData("empty-proof")]
    [InlineData("tenant-scope")]
    [InlineData("legacy-projection-hint")]
    [InlineData("foreign-projection-hint")]
    [InlineData("actor-override")]
    public async Task InternalIdentityCatalogRequiresExistingOwnerAdmissionAndSignedServiceSurface(string scenario)
    {
        string queryType = scenario == "unprotected-query" ? "OtherQuery" : "ResolvePartyIdentity";
        var declaration = new RouteContractDeclaration("identity-fixture", "query", "party", queryType, null, "v1", null,
            "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
            new(typeof(IdentityReaderFixtureRequest).FullName!, "exact", [queryType], "domain-service", "computed", []));
        var operation = RouteOperationSchema.Create(declaration, typeof(IdentityReaderFixtureRequest), typeof(IdentityReaderFixtureResult),
            "internal", "UI-only", EventStorePayloadSerialization.Options);
        var envelope = RouteCatalogCodec.Seal(new(3, 1, "Development", false, [declaration],
            [new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), "parties", "query", "v1", "workload",
                declaration.Operation, false, "test-tenant", operation.Digest)], [], string.Empty, string.Empty, [operation], []));
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, static () => Task.CompletedTask));
        var admission = Substitute.For<IIdentityGatewayAdmission>();
        admission.RequiresAdmission("party", queryType).Returns(scenario != "unconfigured-owner");
        admission.AdmitAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<IdentityAdmissionScope>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(scenario == "denied-proof" ? null : scenario == "empty-proof" ? "" : "unit-owner-proof");
        var claims = new List<Claim> { new("sub", "reader") };
        if (scenario is not ("missing-surface" or "forged-header")) claims.Add(new("eventstore:surface", scenario == "ui-surface" ? "ui" : "service"));
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>()).Returns(new SubmitQueryResult("correlation", JsonSerializer.SerializeToElement(new { })));
        var controller = CreateController(mediator, principal: new ClaimsPrincipal(new ClaimsIdentity(claims, "validated-reader")),
            catalogAuthority: authority, identityAdmission: admission);
        if (scenario == "forged-header") controller.Request.Headers["eventstore:surface"] = "service";
        var request = new SubmitQueryRequest(scenario == "tenant-scope" ? "other-tenant" : "test-tenant", "party", "party-a", queryType,
            ProjectionType: scenario == "legacy-projection-hint" ? "party" : scenario == "foreign-projection-hint" ? "foreign" : null,
            Payload: JsonSerializer.SerializeToElement(new IdentityReaderFixtureRequest("test-tenant", "party-a", "actor-a")),
            ProjectionActorType: scenario == "actor-override" ? "ForeignActor" : null);
        if (scenario is "tenant-scope" or "foreign-projection-hint" or "actor-override")
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => controller.Submit(request, null, TestContext.Current.CancellationToken));
        else if (scenario is "allowed" or "legacy-projection-hint")
        {
            (await controller.Submit(request, null, TestContext.Current.CancellationToken)).ShouldBeOfType<OkObjectResult>();
            await mediator.Received(1).Send(Arg.Is<SubmitQuery>(query => query.IdentityAdmissionProof == "unit-owner-proof" && query.ProjectionType == null), Arg.Any<CancellationToken>());
        }
        else if (scenario is "denied-proof" or "empty-proof")
            (await controller.Submit(request, null, TestContext.Current.CancellationToken)).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(403);
        else (await controller.Submit(request, null, TestContext.Current.CancellationToken)).ShouldBeOfType<ForbidResult>();
        if (scenario is not ("allowed" or "legacy-projection-hint")) await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
        bool ownerCalled = scenario is "allowed" or "legacy-projection-hint" or "denied-proof" or "empty-proof";
        await admission.Received(ownerCalled ? 1 : 0).AdmitAsync(Arg.Any<ClaimsPrincipal>(),
            Arg.Is<IdentityAdmissionScope>(scope => scope.TenantId == request.Tenant && scope.Domain == request.Domain
                && scope.AggregateId == request.AggregateId && scope.Operation == request.QueryType
                && scope.PayloadDigest == IdentityAdmissionProof.Digest(JsonSerializer.SerializeToUtf8Bytes(request.Payload!.Value))),
            Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    private sealed record IdentityReaderFixtureRequest(string TenantId, string PartyId, string ExpectedActorId);
    private sealed record IdentityReaderFixtureResult(bool Allowed);
}
