using System.Security.Claims;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Domain-owned auxiliary calls remain concrete, code-bound, generation-bound and tenant-scoped.</summary>
public sealed class DomainRouteCatalogAuxiliaryTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("root")]
    [InlineData("epoch")]
    [InlineData("tenant")]
    [InlineData("blank-tenant")]
    [InlineData("blank-domain")]
    [InlineData("domain")]
    [InlineData("owner")]
    [InlineData("service-surface")]
    [InlineData("path")]
    [InlineData("verb")]
    public async Task GuardChecksActualAuxiliaryOwnerAndExactRequestScope(string scenario)
    {
        await using var app = WebApplication.CreateBuilder().Build();
        app.MapPost("/widget/revalidate", static (AuxiliaryRequest request) => Results.NoContent())
            .RequireEventStoreDomainServiceAuxiliaryPolicy<AuxiliaryRequest>("/process", "widget", Scope);
        RouteEndpoint endpoint = Endpoint(app);
        var metadata = endpoint.Metadata.GetMetadata<DomainRouteCatalogAuxiliaryMetadata>().ShouldNotBeNull();
        var owner = metadata.Observe(endpoint);
        var declaration = new RouteContractDeclaration("widget-create", "command", "widget", nameof(CreateWidget), null,
            "v1", 1, "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(CreateWidget).FullName!, "clr-short-name-v1", [nameof(CreateWidget)], "domain-service", null, []));
        var intent = new CommandIntentDeclaration(declaration, "widget:adapter", "widget:create", "widget:policy-v1",
            new("widget:descriptor", 1, [new("target", "string"), new("payload-sha256", "string")]),
            EventStore.Contracts.Commands.IdempotencyReplayRetentionTier.Mutation, false);
        var operation = RouteOperationSchema.Create(declaration, typeof(CreateWidget), typeof(EventStore.Contracts.Commands.SubmitCommandResponse),
            "internal", "UI-only", EventStorePayloadSerialization.Options);
        var envelope = RouteCatalogCodec.Seal(new(3, 1, "Development", false, [declaration],
            [new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), "widget", "process", "v1",
                "workload", declaration.Operation, false, "tenant-a", operation.Digest)],
            [DevelopmentIdempotencyBindings.Bind(intent, 1)], string.Empty, string.Empty, [operation], []));
        var auxiliary = new RouteCatalogAuxiliaryOperation("widget", "widget/revalidate", declaration.Operation, "widget")
            { DomainAuxiliary = scenario == "owner" ? owner with { CodeDigest = new string('0', 64) } : owner };
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            [auxiliary], localAppId: "widget");
        var authority = Substitute.For<IRouteCatalogAuthority>();
        int released = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () =>
        { released++; return Task.CompletedTask; }));
        var guard = new DomainRouteCatalogGuard(authority, Options.Create(new RouteCatalogHostOptions
            { Identity = new("widget", "widget", "inventory", "topology", "authentication") }));
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        context.Request.Path = scenario == "path" ? "/widget/other" : "/widget/revalidate";
        context.Request.Method = scenario == "verb" ? "GET" : "POST";
        context.Request.Headers[RouteCatalogInvocationHandler.RootHeader] = scenario == "root" ? new string('0', 64) : snapshot.RootDigest;
        context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader] = scenario == "epoch" ? "2" : "1";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(scenario == "service-surface" ? [] : [new Claim("eventstore:surface", "service")], "verified-workload"));
        var request = new AuxiliaryRequest(scenario == "blank-tenant" ? " " : scenario == "tenant" ? "tenant-b" : "tenant-a",
            scenario == "blank-domain" ? " " : scenario == "domain" ? "foreign" : "widget");
        var invocation = new DefaultEndpointFilterInvocationContext(context, request);
        if (scenario == "valid")
        {
            var lease = (await guard.AcquireAsync(invocation)).ShouldNotBeNull();
            released.ShouldBe(0);
            await lease.DisposeAsync();
        }
        else
        {
            bool invoked = false;
            object? result = await new DomainRouteCatalogFilter(guard).InvokeAsync(invocation, _ =>
            { invoked = true; return ValueTask.FromResult<object?>(Results.NoContent()); });
            invoked.ShouldBeFalse();
            ((IStatusCodeHttpResult)result!).StatusCode.ShouldBe(scenario switch
            {
                "blank-tenant" or "blank-domain" => 400,
                "tenant" or "domain" => 403,
                _ => 503,
            });
        }
        released.ShouldBe(1);
    }

    [Theory]
    [InlineData("/widget/{id}", "POST", false)]
    [InlineData("/widget/revalidate", "GET", false)]
    [InlineData("/widget/revalidate", "POST", true)]
    public async Task ActualEndpointMustBeConcretePostAndProtected(string path, string verb, bool anonymous)
    {
        await using var app = WebApplication.CreateBuilder().Build();
        var builder = app.MapMethods(path, [verb], static ([Microsoft.AspNetCore.Mvc.FromBody] AuxiliaryRequest request) => Results.NoContent())
            .RequireEventStoreDomainServiceAuxiliaryPolicy<AuxiliaryRequest>("/process", "widget", Scope);
        if (anonymous) builder.AllowAnonymous();
        RouteEndpoint endpoint = Endpoint(app);
        Should.Throw<RouteCatalogUnavailableException>(() => endpoint.Metadata.GetMetadata<DomainRouteCatalogAuxiliaryMetadata>()!.Observe(endpoint));
    }

    [Fact]
    public async Task ScopeExtractionCannotCaptureMutablePolicy()
    {
        await using var app = WebApplication.CreateBuilder().Build();
        string domain = "widget";
        Should.Throw<ArgumentException>(() => app.MapPost("/widget/revalidate", static (AuxiliaryRequest request) => Results.NoContent())
            .RequireEventStoreDomainServiceAuxiliaryPolicy<AuxiliaryRequest>("/process", domain, request => new(request.Tenant, domain)));
    }

    [Theory]
    [InlineData("delegate")]
    [InlineData("policy")]
    public async Task CopiedMetadataCannotQualifyDifferentCodeOrMissingCanonicalPolicy(string scenario)
    {
        await using var app = WebApplication.CreateBuilder().Build();
        app.MapPost("/widget/revalidate", static (AuxiliaryRequest request) => Results.NoContent())
            .RequireEventStoreDomainServiceAuxiliaryPolicy<AuxiliaryRequest>("/process", "widget", Scope);
        var original = Endpoint(app);
        var metadata = original.Metadata.GetMetadata<DomainRouteCatalogAuxiliaryMetadata>().ShouldNotBeNull();
        var copied = new RouteEndpoint(scenario == "delegate" ? static _ => Task.CompletedTask : original.RequestDelegate!,
            original.RoutePattern, original.Order, new EndpointMetadataCollection(original.Metadata.Where(item =>
                scenario != "policy" || item is not Microsoft.AspNetCore.Authorization.IAuthorizeData)), original.DisplayName);
        Should.Throw<RouteCatalogUnavailableException>(() => metadata.Observe(copied));
    }

    private static RouteEndpoint Endpoint(WebApplication app) => ((IEndpointRouteBuilder)app).DataSources
        .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Single();

    private static DomainRouteCatalogAuxiliaryScope Scope(AuxiliaryRequest request) => new(request.Tenant, request.Domain);

    private sealed record AuxiliaryRequest(string Tenant, string Domain);
}
