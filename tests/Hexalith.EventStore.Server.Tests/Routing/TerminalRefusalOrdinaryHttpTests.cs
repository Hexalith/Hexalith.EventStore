using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Ordinary finite receivers use genuine complete v1 binding without technical membership or public v2 readbacks.</summary>
public sealed class TerminalRefusalOrdinaryHttpTests
{
    [Fact]
    public async Task SdkSenderBindsActualOrdinaryReceiverAndLeavesLegacyReadbacksAndRawSnapshotsUnchanged()
    {
        await using var fixture = new TerminalRefusalHttpFixture(ordinaryReceiver: true);
        await fixture.StartAsync();
        fixture.Activation.TechnicalHttpReceivers.ShouldBeNull();
        var receiver = fixture.Catalog.Observations.Single(observation => observation.AppId == "sample");
        receiver.ReceiverSurface!.FenceVersion.ShouldBe(FiniteRouteCatalogReceiver.Version);
        receiver.ReceiverSurface.Endpoints.ShouldContain(endpoint => endpoint.Pattern == "/process" && endpoint.Admitted);
        receiver.ExecutableOwners.ShouldNotBeEmpty();
        fixture.Catalog.Hosts.ShouldAllBe(host => host.ExecutableDigest != null);
        var readback = await fixture.Catalog.Authorities[0].ReadbackAsync(TestContext.Current.CancellationToken);
        readback.AttemptId.ShouldBeNull(); readback.ActivationBindingDigest.ShouldBeNull();
        await using (var lease = await fixture.Catalog.Authorities[0].AcquireAsync(TestContext.Current.CancellationToken))
        {
            lease.Snapshot.ActivationBindingDigest.ShouldBeNull();
            lease.Snapshot.TechnicalHttpReceivers.ShouldBeEmpty();
            lease.Snapshot.CompleteActivationBindingDigest.ShouldBe(RouteCatalogActivationBinding.ComputeDigest(fixture.Activation));
            var raw = new CommittedRouteCatalogSnapshot(lease.Snapshot.GetCanonicalBytes(), lease.Snapshot.RootDigest, lease.Snapshot.Epoch, localAppId: "sample");
            raw.CompleteActivationBindingDigest.ShouldBeNull();
            var headers = new HeaderDictionary { [RouteCatalogInvocationHandler.ProcessBindingHeader] = lease.Snapshot.CompleteActivationBindingDigest };
            RouteCatalogPeerBinding.MatchesTerminalRefusal(headers, raw).ShouldBeFalse();
        }
        using var response = await SendThroughSdkAsync(fixture, TerminalRefusalHttpFixture.Command());
        await AssertRejectionAsync(response);
        await fixture.AssertNoEffectsAsync(1);
    }

    [Theory]
    [InlineData("missing", "unsupported", 503)]
    [InlineData("wrong", "unsupported", 503)]
    [InlineData("duplicate", "unsupported", 503)]
    [InlineData("other-attempt", "unsupported", 503)]
    [InlineData("missing", "increment-counter", 200)]
    [InlineData("wrong", "increment-counter", 200)]
    [InlineData("duplicate", "increment-counter", 200)]
    public async Task MissingOrInvalidOptionalProofCannotUnlockRefusalAndKeepsOrdinaryAdmission(string mode, string command, int status)
    {
        await using var fixture = new TerminalRefusalHttpFixture(ordinaryReceiver: true);
        await fixture.StartAsync(knownRule: true, middleware: app => app.Use((context, next) =>
        {
            string header = RouteCatalogInvocationHandler.ProcessBindingHeader;
            context.Request.Headers[header].Count.ShouldBe(1);
            context.Request.Headers[header][0].ShouldBe(RouteCatalogActivationBinding.ComputeDigest(fixture.Activation));
            if (mode == "missing") context.Request.Headers.Remove(header);
            if (mode == "wrong") context.Request.Headers[header] = new string('0', 64);
            if (mode == "duplicate") context.Request.Headers.Append(header, context.Request.Headers[header][0]);
            if (mode == "other-attempt") context.Request.Headers[header] = RouteCatalogActivationBinding.ComputeDigest(fixture.Activation with { AttemptId = "different-attempt" });
            return next(context);
        }));
        using var response = await SendThroughSdkAsync(fixture, TerminalRefusalHttpFixture.Command(command));
        ((int)response.StatusCode).ShouldBe(status);
        if (command == "unsupported") await fixture.AssertNoEffectsAsync(0);
        else
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            json.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeFalse();
            json.RootElement.GetProperty("events").GetArrayLength().ShouldBe(1);
            fixture.Callbacks.ShouldBe(0); fixture.Admissions.ShouldBe(1); fixture.Processors.ShouldBe(1);
            fixture.State.ByteStoreContains("successful-write").ShouldBeTrue();
            (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ExtensionUtf8KeysValuesScalarsAndPayloadUseConfiguredCombinedBudgetBeforeServiceResolution(int excess)
    {
        const int budget = 256;
        int resolutions = 0, callbacks = 0;
        await using var fixture = new TerminalRefusalHttpFixture(ordinaryReceiver: true);
        await fixture.StartAsync(registerRule: false, commandByteBudget: budget, configure: services =>
        {
            services.AddScoped(_ => { resolutions++; return new TerminalRefusalScopedService(); });
            RegisterScoped(services, (_, context, _) =>
            {
                callbacks++;
                context.Extensions!["clé😀"].ShouldBe("é😀");
                return Task.FromResult(DomainServiceTerminalRefusal.Reject(new TerminalRefusalRejected("unsupported")));
            });
        });
        var command = TerminalRefusalHttpFixture.Command().Command with { UserId = "é😀", Extensions = new() { ["clé😀"] = "é😀", ["empty"] = "", ["null"] = null! } };
        int metadataBytes = new[] { command.MessageId, command.TenantId, command.Domain, command.AggregateId, command.CommandType,
            command.CorrelationId, command.CausationId, command.UserId }.Sum(value => value is null ? 0 : Encoding.UTF8.GetByteCount(value))
            + command.Extensions.Sum(entry => Encoding.UTF8.GetByteCount(entry.Key) + (entry.Value is null ? 0 : Encoding.UTF8.GetByteCount(entry.Value)));
        command = command with { Payload = new byte[budget - metadataBytes + excess] };
        using var response = await SendThroughSdkAsync(fixture, new(command, null));
        response.StatusCode.ShouldBe(excess == 0 ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        resolutions.ShouldBe(excess == 0 ? 1 : 0); callbacks.ShouldBe(resolutions);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Fact]
    public async Task ScopedServicesAreIsolatedAcrossConcurrentRequestsAndDisposedByTheirRequestScopes()
    {
        var servicesSeen = new ConcurrentBag<TerminalRefusalScopedService>();
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int callbacks = 0;
        await using var fixture = new TerminalRefusalHttpFixture(ordinaryReceiver: true);
        await fixture.StartAsync(registerRule: false, configure: services =>
        {
            services.AddScoped(_ => { var service = new TerminalRefusalScopedService(); servicesSeen.Add(service); return service; });
            RegisterScoped(services, async (service, context, token) =>
            {
                service.ConstructedInsideRemoteScope.ShouldBeTrue();
                RouteCatalogRemoteExecution.Current.ShouldNotBeNull();
                context.Extensions!["taskId"].ShouldBe("safe-task");
                context.Extensions["surfaceOrigin"].ShouldBe("relay");
                context.Extensions["SurfaceOrigin"].ShouldBe("");
                context.Extensions["actorType"].ShouldBeNull();
                context.Extensions["serviceClientId"].ShouldBe("{malformed");
                if (Interlocked.Increment(ref callbacks) == 2) bothEntered.TrySetResult();
                await release.Task.WaitAsync(token);
                return DomainServiceTerminalRefusal.Reject(new TerminalRefusalRejected("unsupported"));
            });
        });
        var body = TerminalRefusalHttpFixture.Command();
        body = body with { Command = body.Command with { Extensions = new(StringComparer.Ordinal)
        { ["taskId"] = "safe-task", ["surfaceOrigin"] = "relay", ["SurfaceOrigin"] = "", ["actorType"] = null!, ["serviceClientId"] = "{malformed" } } };
        Task<HttpResponseMessage> first = SendThroughSdkAsync(fixture, body), second = SendThroughSdkAsync(fixture, body);
        try
        {
            await bothEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
            servicesSeen.Count.ShouldBe(2);
            servicesSeen.Select(service => service.Id).Distinct().Count().ShouldBe(2);
            servicesSeen.ShouldAllBe(service => !service.Disposed.Task.IsCompleted);
            (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.Count.ShouldBe(4);
        }
        finally { release.TrySetResult(); }
        using var firstResponse = await first; using var secondResponse = await second;
        await AssertRejectionAsync(firstResponse); await AssertRejectionAsync(secondResponse);
        await Task.WhenAll(servicesSeen.Select(service => service.Disposed.Task)).WaitAsync(TestContext.Current.CancellationToken);
        callbacks.ShouldBe(2);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Theory]
    [InlineData("callback", 503)]
    [InlineData("serializer", 503)]
    [InlineData("cancel", 499)]
    public async Task ScopedFailureAndCancellationDisposeRequestServicesAndDrainCompletedAuthority(string phase, int status)
    {
        using var cancellation = new CancellationTokenSource();
        TerminalRefusalScopedService? resolved = null;
        int callbacks = 0;
        await using var fixture = new TerminalRefusalHttpFixture(ordinaryReceiver: true);
        await fixture.StartAsync(registerRule: false, middleware: app => app.Use((context, next) =>
        {
            context.RequestAborted = cancellation.Token;
            return next(context);
        }), configure: services =>
        {
            services.AddScoped(_ => resolved = new TerminalRefusalScopedService());
            services.AddEventStoreDomainServiceTerminalRefusal<TerminalRefusalScopedService>("counter", "unsupported", async (service, _, token) =>
            {
                callbacks++;
                service.ConstructedInsideRemoteScope.ShouldBeTrue();
                await Task.Yield();
                if (phase == "callback") throw new InvalidOperationException("Refusal callback failed.");
                if (phase == "cancel") { cancellation.Cancel(); token.ThrowIfCancellationRequested(); }
                return DomainServiceTerminalRefusal.Reject(new TerminalRefusalRejected("unsupported"));
            }, profile => profile.Add<TerminalRefusalRejected>("terminal-refusal-rejected", "json", 128, async (value, stream, token) =>
            {
                await JsonSerializer.SerializeAsync(stream, value, cancellationToken: token);
                await Task.Yield();
                if (phase == "serializer") throw new InvalidOperationException("Refusal serializer failed.");
            }));
        });
        using var response = await SendThroughSdkAsync(fixture, TerminalRefusalHttpFixture.Command());
        ((int)response.StatusCode).ShouldBe(status);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        resolved.ShouldNotBeNull();
        await resolved.Disposed.Task.WaitAsync(TestContext.Current.CancellationToken);
        callbacks.ShouldBe(1);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Theory]
    [InlineData("missing-service")]
    [InlineData("anonymous")]
    [InlineData("surface")]
    [InlineData("root")]
    [InlineData("epoch")]
    [InlineData("binding")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("pending")]
    [InlineData("entries")]
    [InlineData("bytes")]
    public async Task IneligibleRequestsNeverResolveScopedServicesOrInvokeTheirCallbacks(string mode)
    {
        int resolutions = 0, callbacks = 0;
        await using var fixture = new TerminalRefusalHttpFixture(ordinaryReceiver: true);
        await fixture.StartAsync(registerRule: false, commandByteBudget: 256, configure: services =>
        {
            if (mode != "missing-service") services.AddScoped(_ => { resolutions++; return new TerminalRefusalScopedService(); });
            RegisterScoped(services, (_, _, _) => { callbacks++; return Task.FromResult(DomainServiceTerminalRefusal.NoOpinion); });
            if (mode == "domain") services.AddEventStoreDomainServiceTerminalRefusal<TerminalRefusalScopedService>("unowned", "unsupported",
                (_, _, _) => { callbacks++; return Task.FromResult(DomainServiceTerminalRefusal.NoOpinion); }, ConfigureSerializer);
        });
        if (mode == "anonymous") fixture.Client.DefaultRequestHeaders.Clear();
        if (mode == "surface") fixture.Cohort.Workload(fixture.Client, "domain-service:process", surface: null);
        if (mode == "pending") await fixture.Catalog.Authorities[0].PrepareAsync(fixture.Successor());
        var body = TerminalRefusalHttpFixture.Command(tenant: mode == "tenant" ? "tenant-b" : "tenant-a", domain: mode == "domain" ? "unowned" : "counter");
        if (mode == "entries") body = body with { Command = body.Command with { Extensions = Enumerable.Range(0, 129).ToDictionary(index => index.ToString(), _ => "") } };
        if (mode == "bytes") body = body with { Command = body.Command with { Extensions = new() { ["x"] = new string('é', 128) } } };
        using var request = fixture.Request(body);
        if (mode is "root" or "epoch" or "binding") request.Headers.Remove(mode switch
        { "root" => RouteCatalogInvocationHandler.RootHeader, "epoch" => RouteCatalogInvocationHandler.EpochHeader, _ => RouteCatalogInvocationHandler.ProcessBindingHeader });
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(mode == "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable);
        resolutions.ShouldBe(0); callbacks.ShouldBe(0);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Fact]
    public void BothOverloadsShareExactPairLengthAndFiniteRegistrationLimits()
    {
        var services = new ServiceCollection();
        services.AddEventStoreDomainServiceTerminalRefusal("counter", "unsupported", (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), ConfigureSerializer);
        Should.Throw<ArgumentException>(() => RegisterScoped(services, (_, _, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion)));
        for (int index = 1; index < 1024; index++) services.AddEventStoreDomainServiceTerminalRefusal<TerminalRefusalScopedService>("counter", "command-" + index,
            (_, _, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), ConfigureSerializer);
        services.Count(descriptor => descriptor.ServiceType == typeof(DomainServiceTerminalRefusalRule)).ShouldBe(1024);
        Should.Throw<ArgumentException>(() => services.AddEventStoreDomainServiceTerminalRefusal("counter", "last", (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), ConfigureSerializer));
        Should.Throw<ArgumentException>(() => services.AddEventStoreDomainServiceTerminalRefusal<TerminalRefusalScopedService>("counter", "last", (_, _, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), ConfigureSerializer));
        var names = new ServiceCollection();
        names.AddEventStoreDomainServiceTerminalRefusal<TerminalRefusalScopedService>(new string('é', 512), new string('é', 2048),
            (_, _, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), ConfigureSerializer);
        Should.Throw<ArgumentException>(() => names.AddEventStoreDomainServiceTerminalRefusal<TerminalRefusalScopedService>(new string('é', 512) + "x", "command", (_, _, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), ConfigureSerializer));
        Should.Throw<ArgumentException>(() => names.AddEventStoreDomainServiceTerminalRefusal<TerminalRefusalScopedService>("counter", new string('é', 2048) + "x", (_, _, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), ConfigureSerializer));
    }

    private static void RegisterScoped(IServiceCollection services,
        Func<TerminalRefusalScopedService, DomainServiceTerminalRefusalContext, CancellationToken, Task<DomainServiceTerminalRefusal>> evaluate)
        => services.AddEventStoreDomainServiceTerminalRefusal("counter", "unsupported", evaluate, ConfigureSerializer);

    private static void ConfigureSerializer(BoundedV1DomainSerializerProfile profile) => profile.Add<TerminalRefusalRejected>("terminal-refusal-rejected", "json", 128,
        (value, stream, token) => JsonSerializer.SerializeAsync(stream, value, cancellationToken: token));

    private static async Task<HttpResponseMessage> SendThroughSdkAsync(TerminalRefusalHttpFixture fixture, DomainServiceRequest body)
    {
        using var client = new HttpClient(new RouteCatalogInvocationHandler(fixture.Catalog.Authorities[0], new SocketsHttpHandler { AllowAutoRedirect = false }));
        using var request = new HttpRequestMessage(HttpMethod.Post, fixture.App.Urls.Single() + "/process") { Content = JsonContent.Create(body) };
        foreach (var header in fixture.Client.DefaultRequestHeaders) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        request.Headers.TryAddWithoutValidation("dapr-app-id", "sample");
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.ProcessBindingHeader, "caller-injected");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertRejectionAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("events").GetArrayLength().ShouldBe(1);
    }
}
