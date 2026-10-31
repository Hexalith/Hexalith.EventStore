using NSubstitute;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

public class DevelopmentAdminTransportTests
{
    [Fact]
    public async Task ActualCompiledAdminMvcPatternsCarryCodeVerbAndHumanRoleMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminHealthController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        var owned = endpoints.Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.Assembly == typeof(AdminHealthController).Assembly).ToArray();
        owned.ShouldNotBeEmpty();
        foreach (var endpoint in owned)
        {
            var http = DevelopmentTechnicalHttpTransport.Observe(endpoint);
            http.ShouldNotBeNull(endpoint.RoutePattern.RawText + ": " + endpoint.RequestDelegate?.Method);
            http.CodeDigest.Length.ShouldBe(64);
            http.Owner.ShouldStartWith("Hexalith.EventStore.Admin.Server.Controllers.");
        }
        owned.ShouldContain(endpoint => endpoint.RoutePattern.Parameters.Count > 0);
    }

    [Fact]
    public async Task ActualPatternedAdminRequestKeepsSignedRoleAndCommittedDurableLease()
    {
        await using var host = new TechnicalAdminHost();
        await host.StartAsync();
        host.Backend.GetActorInstanceStateAsync(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                (await host.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
                return (Hexalith.EventStore.Admin.Abstractions.Models.Dapr.DaprActorInstanceState?)new Hexalith.EventStore.Admin.Abstractions.Models.Dapr.DaprActorInstanceState(call.ArgAt<string>(0), call.ArgAt<string>(1), [], 0, DateTimeOffset.UtcNow);
            });
        using var request = host.Request("/api/v1/admin/dapr/actors/ProjectionActor/state?id=actor-one");
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        (await host.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing-generation", System.Net.HttpStatusCode.ServiceUnavailable)]
    [InlineData("wrong-channel", System.Net.HttpStatusCode.Unauthorized)]
    [InlineData("insufficient-role", System.Net.HttpStatusCode.Forbidden)]
    [InlineData("foreign-pattern", System.Net.HttpStatusCode.Forbidden)]
    [InlineData("mutated-policy", System.Net.HttpStatusCode.Forbidden)]
    [InlineData("ungranted-delete", System.Net.HttpStatusCode.Forbidden)]
    public async Task TechnicalBoundaryRefusesUnboundForeignAndMutatedRequests(string change, System.Net.HttpStatusCode expected)
    {
        await using var host = new TechnicalAdminHost();
        await host.StartAsync();
        if (change == "mutated-policy")
        {
            var endpoint = host.Endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "api/v1/admin/dapr/actors/{actorType}/state");
            var policy = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                .Single(policy => policy.Policy == Hexalith.EventStore.Admin.Server.Authorization.AdminAuthorizationPolicies.Admin);
            policy.Policy = Hexalith.EventStore.Admin.Server.Authorization.AdminAuthorizationPolicies.ReadOnly;
        }
        string path = change == "foreign-pattern" ? "/api/v1/admin/foreign/one"
            : change == "ungranted-delete" ? "/api/v1/admin/storage/tenant-a/counter/counter-state/snapshot-policy" : "/api/v1/admin/dapr/actors/ProjectionActor/state?id=actor-one";
        using var request = host.Request(path,
            role: change == "insufficient-role" ? "ReadOnly" : "Admin", generation: change != "missing-generation",
            channel: change == "wrong-channel" ? "forged" : null);
        if (change == "ungranted-delete") request.Method = HttpMethod.Delete;
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        host.ForeignCalls.ShouldBe(0);
        host.Backend.ReceivedCalls().ShouldBeEmpty();
        (await host.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task ActualHttpSenderOverwritesGenerationAndKeepsBothDurableLeasesUntilReceiverCompletes()
    {
        await using var host = new TechnicalAdminHost();
        await host.StartAsync();
        int observedLeases = -1;
        host.Backend.GetActorInstanceStateAsync(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                observedLeases = (await host.Store.ReadAsync(default)).State.Leases.Count;
                return (Hexalith.EventStore.Admin.Abstractions.Models.Dapr.DaprActorInstanceState?)new Hexalith.EventStore.Admin.Abstractions.Models.Dapr.DaprActorInstanceState(call.ArgAt<string>(0), call.ArgAt<string>(1), [], 0, DateTimeOffset.UtcNow);
            });
        using var request = host.Request("/api/v1/admin/dapr/actors/ProjectionActor/state?id=actor-one", generation: false);
        request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, "untrusted-root");
        request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, "999");
        using var response = await host.SendThroughGuardAsync(request);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        observedLeases.ShouldBe(2);
        (await host.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        using var denied = host.Request("/api/v1/admin/storage/tenant-a/counter/counter-state/snapshot-policy", generation: false);
        denied.Method = HttpMethod.Delete;
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.SendThroughGuardAsync(denied));
    }

    [Fact]
    public async Task SenderMatchesOnlyActualPatternAndGrantedVerbWithoutEncodedOrForeignPaths()
    {
        await using var host = new TechnicalAdminHost();
        await host.StartAsync();
        var activation = (await host.Store.ReadAsync(default)).State.Committed.ShouldNotBeNull();
        var operation = activation.AuxiliaryOperations!.Single(operation => operation.Method == "api/v1/admin/dapr/actors/{actorType}/state");
        DevelopmentTechnicalHttpTransport.Matches(operation, "api/v1/admin/dapr/actors/ProjectionActor/state", "GET").ShouldBeTrue();
        DevelopmentTechnicalHttpTransport.Matches(operation, "api/v1/admin/dapr/actors/ProjectionActor/state", "PUT").ShouldBeFalse();
        DevelopmentTechnicalHttpTransport.Matches(operation, "api/v1/admin/dapr/actors/ProjectionActor/foreign", "GET").ShouldBeFalse();
        DevelopmentTechnicalHttpTransport.Matches(operation, "api/v1/admin/dapr/actors/%252f/state", "GET").ShouldBeFalse();
        DevelopmentTechnicalHttpTransport.Matches(operation, "api/v1/admin/dapr/actors/../state", "GET").ShouldBeFalse();
        var policy = activation.AuxiliaryOperations!.Single(operation => operation.Method == "api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy");
        policy.Http.ShouldNotBeNull().Verbs.ShouldBe("PUT");
    }

    [Fact]
    public async Task ForeignDelegateCannotBorrowActualSdkControllerMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminHealthController).Assembly);
        await using var app = builder.Build(); app.MapControllers();
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "api/v1/admin/dapr/actors/{actorType}/state");
        var foreign = new RouteEndpoint(_ => Task.CompletedTask, endpoint.RoutePattern, endpoint.Order, endpoint.Metadata, "copied-metadata");
        DevelopmentTechnicalHttpTransport.Observe(endpoint).ShouldNotBeNull();
        DevelopmentTechnicalHttpTransport.Observe(foreign).ShouldBeNull();
    }

    [Fact]
    public async Task RealObserverRefusesCopiedCandidateCodeWithoutChangingCommittedAuthority()
    {
        await using var host = new TechnicalAdminHost(); await host.StartAsync();
        var before = (await host.Store.ReadAsync(default)).State.Committed.ShouldNotBeNull();
        var envelope = RouteCatalogCodec.Decode(before.CanonicalCatalog, before.RootDigest);
        envelope = RouteCatalogCodec.Seal(envelope with { Generation = 2, Routes = envelope.Routes.Select(route => route with { Generation = 2 }).ToArray() });
        var forged = before with { AttemptId = "forged-code", CanonicalCatalog = RouteCatalogCodec.Encode(envelope),
            RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope), PredecessorRootDigest = before.RootDigest,
            AuxiliaryOperations = before.AuxiliaryOperations!.Select(operation => operation.Http is null ? operation
                : operation with { Http = operation.Http with { CodeDigest = new string('0', 64) } }).ToArray() };
        forged = forged with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(forged) };
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.Authority.PrepareAsync(forged));
        var after = (await host.Store.ReadAsync(default)).State;
        after.Pending.ShouldBeNull(); after.Committed.ShouldBe(before);
    }

    [Fact]
    public void LegacyAuxiliaryConstructorDeconstructAndCanonicalJsonArePreserved()
    {
        var operation = new RouteCatalogAuxiliaryOperation("sample", "replay-state", "domain-service:replay-state", "counter");
        var (app, method, policy, domain) = operation;
        (app, method, policy, domain).ShouldBe(("sample", "replay-state", "domain-service:replay-state", "counter"));
        System.Text.Json.JsonSerializer.Serialize(operation).ShouldBe("""{"AppId":"sample","Method":"replay-state","Operation":"domain-service:replay-state","Domain":"counter"}""");
        typeof(RouteCatalogAuxiliaryOperation).GetConstructor([typeof(string), typeof(string), typeof(string), typeof(string)]).ShouldNotBeNull();
    }
}
