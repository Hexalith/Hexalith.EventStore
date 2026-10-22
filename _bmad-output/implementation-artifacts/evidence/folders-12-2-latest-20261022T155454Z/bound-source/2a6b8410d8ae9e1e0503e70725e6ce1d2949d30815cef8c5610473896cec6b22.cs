using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.Projections;
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

public sealed class ProjectionCurrentAuthorityTests
{
    [Theory]
    [InlineData("current")]
    [InlineData("unselected-version")]
    [InlineData("missing-domain-enrollment")]
    [InlineData("cross-tenant")]
    public async Task ExistingSignedOwnerRouteReturnsOnlyTheExactNativeSelectedGeneration(string scenario)
    {
        using var fixture = await GuardedTailProductionHarness.CreateAsync(1, 1);
        await fixture.UseProductionOwnersAsync();
        var inspected = await fixture.Probe.InspectCurrentAsync(fixture.Identity, CancellationToken.None);
        string route = fixture.Catalog.OrdinaryProjectionTypes[0];
        var selection = inspected.ProvenOwners[route].Selection;
        using RSA key = RSA.Create(2048);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = WorkloadAssertionTestTokens.Issuer,
            ["Authentication:JwtBearer:Audience"] = "human-gateway",
            ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["Authentication:BoundStreamRead:PublicKeys:conversations"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            ["Authentication:BoundOwnerQuery:AllowedQueries:conversations"] = InspectProjectionCurrentQuery.QueryType,
            ["Authentication:BoundOwnerQuery:AllowedProjectionDomains:conversations"] = scenario == "missing-domain-enrollment" ? null : fixture.Identity.Domain,
        });
        builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(DaprInternalAuthenticationOptions.SchemeName, "Authentication:DaprInternal",
            (options, _) => { options.Audience = "hexalith-eventstore"; options.AllowedCallers.Add("conversations"); });
        builder.Services.AddSingleton<IProjectionSourceGuardDelivery>(fixture.Probe);
        await using var app = builder.Build();
        var original = new QueryEnvelope(fixture.Identity.TenantId, "conversations", fixture.Identity.AggregateId,
            "Metadata", "{}"u8.ToArray(), "corr", "actor")
        {
            OriginalActorId = "actor", AuthenticatedWorkloadId = "projects", IsDelegated = true,
            CallerAuthority = new("delegated-owner-query", [], [])
            { OriginatingContext = new("actor", "original", true, "original-delegation", ["projects.read"], ["hexalith-eventstore"]) },
        };
        string assertion = WorkloadAssertionTestTokens.Create("conversations", "eventstore", [EventStoreWorkloadOperations.DomainServiceQuery],
            bindings: QueryCallerAuthorityBinding.Bind(original));
        if (scenario == "unselected-version") selection = selection with { FreshnessVersion = "forged-version" };
        if (scenario == "cross-tenant") selection = selection with { TenantId = "other-tenant" };
        var request = new BoundOwnerQueryRequest(new(fixture.Identity.TenantId, fixture.Identity.Domain, fixture.Identity.AggregateId,
            InspectProjectionCurrentQuery.QueryType, Payload: JsonSerializer.SerializeToElement(new InspectProjectionCurrentQuery(selection), JsonSerializerOptions.Web)), "actor")
        { OriginatingQuery = original, OriginatingAssertion = assertion };
        var controller = new QueriesController(Substitute.For<IMediator>(), Substitute.For<IETagService>(),
            Substitute.For<ITenantValidator>(), Substitute.For<IRbacValidator>(), NullLogger<QueriesController>.Instance,
            configuration: builder.Configuration, assertionValidator: app.Services.GetRequiredService<WorkloadAssertionValidator>())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { RequestServices = app.Services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "workload:conversations"),
                new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "conversations")], "workload")) } } };
        controller.Request.Headers[BoundOwnerQueryProof.HeaderName] = BoundOwnerQueryProof.Issue(request, "conversations",
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow);
        BoundOwnerQueryProof.Verify(request, controller.Request.Headers[BoundOwnerQueryProof.HeaderName][0], "conversations",
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
        var verifiedOrigin = await app.Services.GetRequiredService<WorkloadAssertionValidator>().ValidateForAudienceAsync(
            DaprInternalAuthenticationOptions.SchemeName, assertion, EventStoreWorkloadOperations.DomainServiceQuery,
            ["eventstore"], "conversations", TestContext.Current.CancellationToken);
        verifiedOrigin.Succeeded.ShouldBeTrue(verifiedOrigin.ReasonCode);
        QueryCallerAuthorityBinding.Verify(original, verifiedOrigin.Principal!, "eventstore").ShouldBeTrue();
        controller.HttpContext.RequestServices.GetRequiredService<IProjectionSourceGuardDelivery>().ShouldBeSameAs(fixture.Probe);
        using var costs = Hexalith.EventStore.Client.Projections.ProjectionDeliveryCostDiagnostics.BeginScope();
        var result = await controller.SubmitBoundOwnerAuthorityAsync(request, TestContext.Current.CancellationToken);
        if (scenario is "cross-tenant" or "missing-domain-enrollment") result.ShouldBeOfType<ForbidResult>();
        else
        {
            var value = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<SubmitQueryResponse>().Payload;
            if (scenario == "current")
            {
                var proof = value.Deserialize<ProjectionCurrentProof>(JsonSerializerOptions.Web)!;
                proof.Owner.Selection.ShouldBe(selection);
                proof.Source.CumulativeDigest.ShouldBe(fixture.Head.CumulativeDigest);
                proof.CatalogFingerprint.ShouldBe(fixture.Catalog.CatalogFingerprint);
            }
            else value.ValueKind.ShouldBe(JsonValueKind.Null);
        }
        costs[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.SourceRanges].ShouldBe(0);
        costs[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
        costs[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
        fixture.RowWrites.ShouldBe(0);
    }
}
