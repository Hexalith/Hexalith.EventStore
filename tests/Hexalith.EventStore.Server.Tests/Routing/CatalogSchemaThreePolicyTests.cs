using System.Security.Claims;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogSchemaThreePolicyTests
{
    [Theory]
    [InlineData(null, false, "agent-eligible", false)]
    [InlineData("agent", true, "agent-eligible", false)]
    [InlineData("ui", true, "agent-eligible", false)]
    [InlineData("service", false, "agent-eligible", false)]
    [InlineData("service", true, "agent-eligible", true)]
    [InlineData("service", true, "step-up", false)]
    public async Task InternalGatewayExemptionRequiresAuthenticatedWorkloadServiceAndNeverAdmitsStepUp(string? surface, bool workload, string eligibility, bool accepted)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = CatalogSchemaThreeFixture.Create(fixture.Envelope, _ => eligibility);
        var declaration = envelope.Declarations.First(d => d.Kind == "query" && d.Execution!.RequiresDelegatedContext);
        var request = new SubmitQuery("system", declaration.Domain, "aggregate", declaration.MessageType!, "{}"u8.ToArray(), "correlation", "actor");
        await AssertGatewayAsync(request, envelope, Principal(surface, workload), accepted);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ServiceConfirmationRequiresExistingVerifiedCommandOrigin(bool verified, bool accepted)
    {
        var envelope = CatalogSchemaThreeFixture.Create(new RouteCatalogTestFixture().Envelope, _ => "confirmation-required");
        var declaration = envelope.Declarations.Single(d => d.RouteId == "counter-increment-counter");
        var request = new SubmitCommand("message", "tenant-a", declaration.Domain, "aggregate", declaration.MessageType!, "{}"u8.ToArray(), "correlation", "actor")
        { Extensions = verified ? new() { [EventStoreGatewayVerifiedOrigin.ActorExtensionKey] = "verified-actor" } : null };
        await AssertGatewayAsync(request, envelope, Principal("service", true), accepted);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ServiceConfirmationRequiresVerifiedDelegatedQueryOrigin(bool verified, bool accepted)
    {
        var envelope = CatalogSchemaThreeFixture.Create(new RouteCatalogTestFixture().Envelope, _ => "confirmation-required");
        var declaration = envelope.Declarations.First(d => d.Kind == "query" && !d.Execution!.RequiresDelegatedContext);
        var request = new SubmitQuery("tenant-a", declaration.Domain, "aggregate", declaration.MessageType!, "{}"u8.ToArray(), "correlation", "actor",
            OriginalActorId: verified ? "verified-actor" : null, AuthenticatedWorkloadId: verified ? "eventstore" : null, IsDelegated: verified);
        await AssertGatewayAsync(request, envelope, Principal("service", true), accepted);
    }

    [Theory]
    [InlineData(null, "agent-eligible", false)]
    [InlineData("agent", "agent-eligible", false)]
    [InlineData("ui", "agent-eligible", false)]
    [InlineData("service", "agent-eligible", true)]
    [InlineData("service", "step-up", false)]
    public async Task DomainReceiverRequiresServiceAndRefusesStepUpBeforeEffects(string? surface, string eligibility, bool accepted)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = CatalogSchemaThreeFixture.Create(fixture.Envelope, _ => eligibility);
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        int leases = 0, effects = 0;
        var authority = Authority(snapshot, () => leases++, () => leases--);
        var guard = new DomainRouteCatalogGuard(authority, Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts.Single(host => host.AppId == "sample") }));
        var http = new DefaultHttpContext { User = Principal(surface, true) };
        http.Request.Path = "/process";
        http.Request.Headers[RouteCatalogInvocationHandler.RootHeader] = snapshot.RootDigest;
        http.Request.Headers[RouteCatalogInvocationHandler.EpochHeader] = "1";
        var command = new CommandEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAV", "tenant-a", "counter", "aggregate", "increment-counter", "{}"u8.ToArray(), "correlation", null, "actor", null);
        var context = new DefaultEndpointFilterInvocationContext(http, new DomainServiceRequest(command, null));
        var result = await new DomainRouteCatalogFilter(guard).InvokeAsync(context, _ => { leases.ShouldBe(1); effects++; return ValueTask.FromResult<object?>(null); });
        effects.ShouldBe(accepted ? 1 : 0);
        leases.ShouldBe(0);
        if (!accepted) ((IStatusCodeHttpResult)result!).StatusCode.ShouldBe(503);
    }

    private static ClaimsPrincipal Principal(string? surface, bool workload)
    {
        List<Claim> claims = [];
        if (surface is not null) claims.Add(new(RouteCatalogSurfacePolicy.ClaimType, surface));
        if (workload) claims.Add(new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore"));
        return new(new ClaimsIdentity(claims, "verified-test-boundary"));
    }

    private static IRouteCatalogAuthority Authority(CommittedRouteCatalogSnapshot snapshot, Action acquire, Action release)
    {
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        { acquire(); return new RouteCatalogDispatchLease(snapshot, () => { release(); return Task.CompletedTask; }); });
        return authority;
    }

    private static async Task AssertGatewayAsync<T>(T request, RouteCatalogEnvelope envelope, ClaimsPrincipal principal, bool accepted) where T : notnull
    {
        int leases = 0, effects = 0;
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        using var services = new ServiceCollection().AddSingleton(Authority(snapshot, () => leases++, () => leases--)).BuildServiceProvider();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var behavior = new RouteCatalogEligibilityBehavior<T, int>(accessor, services);
        Task<int> Invoke() => behavior.Handle(request, _ => { leases.ShouldBe(1); effects++; return Task.FromResult(42); }, TestContext.Current.CancellationToken);
        if (accepted) (await Invoke()).ShouldBe(42);
        else await Should.ThrowAsync<CommandAuthorizationException>(Invoke);
        effects.ShouldBe(accepted ? 1 : 0);
        leases.ShouldBe(0);
    }
}
