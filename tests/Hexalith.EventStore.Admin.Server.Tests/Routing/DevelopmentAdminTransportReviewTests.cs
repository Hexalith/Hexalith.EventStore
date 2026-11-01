using System.Net;
using System.Net.Http.Json;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

public sealed class DevelopmentAdminTransportReviewTests
{
    [Fact]
    public async Task ActualForeignMvcDelegateAndMulticastCannotBorrowSdkMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminHealthController).Assembly).AddApplicationPart(typeof(ForeignTechnicalController).Assembly).ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new ForeignTechnicalControllerFeature()));
        await using var app = builder.Build(); app.MapControllers();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        var owned = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "api/v1/admin/dapr/actors/{actorType}/state");
        var foreign = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "foreign-mvc");
        foreign.RequestDelegate!.Method.Module.Assembly.ShouldBe(owned.RequestDelegate!.Method.Module.Assembly);
        DevelopmentTechnicalHttpTransport.Observe(owned).ShouldNotBeNull();
        foreach (RequestDelegate action in new[] { foreign.RequestDelegate!, owned.RequestDelegate! + foreign.RequestDelegate! })
            DevelopmentTechnicalHttpTransport.Observe(new RouteEndpoint(action, owned.RoutePattern, owned.Order, owned.Metadata, "borrowed-real-mvc")).ShouldBeNull();
    }

    [Theory]
    [InlineData("%2f")]
    [InlineData("%252f")]
    [InlineData("%5c")]
    public async Task ActualSenderRejectsEncodedStructuralMethodBeforeTransport(string parameter)
    {
        await using var host = new TechnicalAdminHost(); await host.StartAsync();
        using var request = host.Request("/api/v1/admin/dapr/actors/" + parameter + "/state", generation: false);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.SendThroughGuardAsync(request));
        host.Backend.ReceivedCalls().ShouldBeEmpty();
        (await host.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task FrameworkLongAndRegexConstraintsAndEscapedScalarApplyToActualOwnedTemplates()
    {
        await using var host = new TechnicalAdminHost(); await host.StartAsync();
        var operations = host.Endpoints.Select(endpoint => (Endpoint: endpoint, Http: DevelopmentTechnicalHttpTransport.Observe(endpoint))).Where(pair => pair.Http is not null)
            .Select(pair => new RouteCatalogAuxiliaryOperation("admin-test", pair.Endpoint.RoutePattern.RawText!.Trim('/'), "sdk-technical-http", "admin") { Http = pair.Http }).ToArray();
        var numeric = operations.Single(operation => operation.Method == "api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}");
        DevelopmentTechnicalHttpTransport.Matches(numeric, "api/v1/admin/streams/t/d/a/events/9223372036854775807", "GET").ShouldBeTrue();
        DevelopmentTechnicalHttpTransport.Matches(numeric, "api/v1/admin/streams/t/d/a/events/not-long", "GET").ShouldBeFalse();
        DevelopmentTechnicalHttpTransport.Matches(numeric, "api/v1/admin/streams/t/d/a/events/9223372036854775808", "GET").ShouldBeFalse();
        var regex = operations.Single(operation => operation.Method.Contains(":regex(", StringComparison.Ordinal));
        DevelopmentTechnicalHttpTransport.Matches(regex, "api/v1/admin/backups/tenant%20one", "POST").ShouldBeTrue();
        DevelopmentTechnicalHttpTransport.Matches(regex, "api/v1/admin/backups/export-stream", "POST").ShouldBeFalse();
        var actor = operations.Single(operation => operation.Method == "api/v1/admin/dapr/actors/{actorType}/state");
        DevelopmentTechnicalHttpTransport.Matches(actor, "api/v1/admin/dapr/actors/type%20one/state", "GET").ShouldBeTrue();
    }

    [Fact]
    public async Task ConcreteAclPathQualifiesKnownPatternButDoesNotAdmitPeerOrVerb()
    {
        const string concrete = "/api/v1/admin/dapr/actors/ProjectionActor/state";
        await using var host = new TechnicalAdminHost(); await host.StartAsync(concrete);
        (await host.Store.ReadAsync(default)).State.Committed!.TechnicalHttpReceivers!.Single().Invocations!.Single().Method.ShouldBe(concrete);
        host.Backend.GetActorInstanceStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new Hexalith.EventStore.Admin.Abstractions.Models.Dapr.DaprActorInstanceState("ProjectionActor", "one", [], 0, DateTimeOffset.UtcNow));
        using var admitted = host.Request(concrete + "?id=one");
        using var response = await host.SendThroughGuardAsync(admitted);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var (path, verb) in new[] { ("/api/v1/admin/dapr/actors/AnotherActor/state", HttpMethod.Get), (concrete, HttpMethod.Put) })
        {
            using var request = host.Request(path); request.Method = verb;
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.SendThroughGuardAsync(request));
        }
        (await host.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("wrong-epoch")]
    [InlineData("wrong-root")]
    [InlineData("stale-after-transition")]
    public async Task DirectReceiverRefusesWrongAndStaleGenerationWithoutLeaseResidue(string change)
    {
        await using var host = new TechnicalAdminHost(); await host.StartAsync();
        using var request = host.Request("/api/v1/admin/dapr/actors/ProjectionActor/state?id=one");
        if (change == "wrong-epoch") { request.Headers.Remove(RouteCatalogInvocationHandler.EpochHeader); request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, "999"); }
        if (change == "wrong-root") { request.Headers.Remove(RouteCatalogInvocationHandler.RootHeader); request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, new string('0', 64)); }
        if (change == "stale-after-transition") await host.AdvanceAsync();
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        host.Backend.ReceivedCalls().ShouldBeEmpty();
        (await host.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task SchemaTwoTechnicalCandidateRefusesBindingBeforeAuthorityStateChanges()
    {
        await using var host = new TechnicalAdminHost(); await host.StartAsync();
        var before = (await host.Store.ReadAsync(default)).State.Committed!;
        var envelope = RouteCatalogCodec.Decode(before.CanonicalCatalog, before.RootDigest);
        var legacy = RouteCatalogCodec.Seal(envelope with { SchemaVersion = 2, Operations = null, RetainedIdempotency = null,
            Routes = envelope.Routes.Select(route => route with { OperationContractDigest = null }).ToArray() });
        var candidate = before with { CanonicalCatalog = RouteCatalogCodec.Encode(legacy), RootDigest = RouteCatalogCodec.ComputeRootDigest(legacy) };
        Should.Throw<RouteCatalogUnavailableException>(() => RouteCatalogActivationBinding.ComputeDigest(candidate));
        (await host.Store.ReadAsync(default)).State.Committed.ShouldBe(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualCaughtOrCancelledDaprMutationRetainsReceiverFence(bool cancelled)
    {
        await using var host = new TechnicalAdminHost(); await host.StartAsync();
        host.Dapr.SaveStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RestoredBackupAdmissionResult>(), Arg.Any<Dapr.Client.StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ => cancelled
                ? Task.FromCanceled(new CancellationToken(true)) : Task.FromException(new HttpRequestException("ambiguous backend completion")));
        var value = new RestoredBackupAdmissionRequest("admission-one", "tenant-a", "counter", "one", null, null, "manifest-one", DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow, 1, KeyReferencePolicy.NoKeyReference, null, null, null, "operator");
        using var request = host.Request("/api/v1/admin/backups/admissions"); request.Method = HttpMethod.Post; request.Content = JsonContent.Create(value);
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable);
        host.Dapr.ReceivedCalls().ShouldContain(call => call.GetMethodInfo().Name == "SaveStateAsync");
        (await host.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.AdvanceAsync());
    }
    [Fact]
    public async Task NativeAdminProgramUsesActualHelperWorkerObservationAndActivation()
    {
        await using var host = new TechnicalAdminHost(); await host.StartAsync(nativeBootstrap: true);
        host.Backend.GetActorInstanceStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new Hexalith.EventStore.Admin.Abstractions.Models.Dapr.DaprActorInstanceState("ProjectionActor", "one", [], 0, DateTimeOffset.UtcNow));
        var observation = System.Text.Json.JsonSerializer.Deserialize<RouteCatalogHostObservation>(await File.ReadAllBytesAsync(Path.Combine(host.DirectoryPath, "admin.observation.json"), TestContext.Current.CancellationToken))!;
        observation.AppId.ShouldBe("admin-test");
        observation.ReceiverSurface!.FenceVersion.ShouldBe(RouteCatalogTechnicalHttpPolicy.FenceVersion);
        observation.ReceiverSurface.TechnicalHttp.ShouldNotBeEmpty();
        observation.ReceiverSurface.Endpoints.ShouldContain(endpoint => endpoint.Pattern == "/internal/routing/catalog/prepare" && endpoint.Admitted && endpoint.Http == null);
        using var request = host.Request("/api/v1/admin/dapr/actors/ProjectionActor/state?id=one");
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task ActualAdminOwnerReadUsesIndependentDestinationCredentialAndOnlyScopedReadShape()
    {
        string apiToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var owner = builder.Build();
        int calls = 0;
        owner.MapGet("/v1.0/actors/{type}/{id}/state/{key}", (HttpContext context) =>
        {
            context.Request.Headers["dapr-api-token"].ToString().ShouldBe(apiToken);
            Interlocked.Increment(ref calls);
            return Results.Json(new { value = 1 });
        });
        await owner.StartAsync(TestContext.Current.CancellationToken);
        string endpoint = owner.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single();
        await using var host = new TechnicalAdminHost(); await host.StartAsync(ownerEndpoint: endpoint, ownerToken: apiToken);
        using var request = host.Request("/api/v1/admin/dapr/actors/ETagActor/state?id=actor-one");
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("\"found\":true");
        calls.ShouldBeGreaterThan(0);
        int before = calls;
        using var client = host.CreateOwnerClient();
        foreach (var (path, method) in new[] { ("/v1.0/actors/ETagActor/one/state/key", HttpMethod.Get), ("/v1.0/actors/ETagActor/one/method/Get", HttpMethod.Get), ("/v1.0/actors/ETagActor/one/state/key", HttpMethod.Put) })
        {
            using var forbidden = new HttpRequestMessage(method, endpoint + path);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DevelopmentActorStateTransport.SendAsync(client, forbidden, "domain-test", TestContext.Current.CancellationToken));
        }
        using var wrongDestination = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:1/v1.0/actors/ETagActor/one/state/key");
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DevelopmentActorStateTransport.SendAsync(client, wrongDestination, "domain-test", TestContext.Current.CancellationToken));
        using var raw = new HttpRequestMessage(HttpMethod.Get, endpoint + "/v1.0/actors/ETagActor/one/method/Get");
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.SendAsync(raw, TestContext.Current.CancellationToken));
        using var injected = new HttpRequestMessage(HttpMethod.Get, endpoint + "/v1.0/actors/ETagActor/one/state/key");
        injected.Headers.Add("dapr-app-id", "foreign");
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DevelopmentActorStateTransport.SendAsync(client, injected, "domain-test", TestContext.Current.CancellationToken));
        calls.ShouldBe(before);
        (await host.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/internal/routing/catalog-generation", "eventstore:route-catalog-read")]
    [InlineData("/internal/routing/catalog/prepare", "eventstore:route-catalog-activate")]
    public async Task TechnicalV2RetainsStrictActualNontechnicalLifecycleGrants(string method, string operation)
    {
        await using var host = new TechnicalAdminHost(); await host.StartAsync(method);
        var committed = (await host.Store.ReadAsync(default)).State.Committed!;
        var actual = committed.AuxiliaryOperations!.Single(operation => operation.AppId == "admin-test" && operation.Method == method.Trim('/'));
        actual.Method.ShouldBe(method.Trim('/'));
        actual.Http.ShouldBeNull();
        actual.Operation.ShouldBe(operation);
        using var lease = new System.Threading.CancellationTokenSource();
        await using var acquired = await host.Authority.AcquireAsync(lease.Token);
        acquired.Snapshot.RootDigest.ShouldBe(host.Root);
    }

    [Theory]
    [InlineData("/admin/foo", "/admin/foo/bar", true)]
    [InlineData("/admin/foo", "/admin/foobar", false)]
    [InlineData("/admin/foo/*", "/admin/foo/bar", true)]
    [InlineData("/admin/foo/*", "/admin/foo/bar/baz", false)]
    [InlineData("/admin/foo/*", "/admin/foo", true)]
    [InlineData("/**", "/admin/foo/bar/baz", true)]
    public void TechnicalGrantUsesActualDaprLiteralAndSingleStageWildcardSemantics(string grant, string path, bool expected)
        => DevelopmentTechnicalHttpTransport.GrantAllows(grant, path).ShouldBe(expected);

}
