using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class RouteCatalogHostedLiveTests
{
    /// <summary>Qualifies public owned transport with authenticated peers, real persisted actors, scheduler delivery and a native restart.</summary>
    [Fact(Timeout = 180000)]
    public async Task PublicDevelopmentTransportAuthenticatesPeersAndPersistsActorsAcrossNativeRestart()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string id = Guid.NewGuid().ToString("N");
        string scratch = System.IO.Directory.CreateTempSubdirectory("eventstore-transport-qualification-").FullName;
        string custody = Path.Combine(scratch, "custody");
        string resources = System.IO.Directory.CreateDirectory(Path.Combine(scratch, "components")).FullName;
        string runtime = Environment.GetEnvironmentVariable("EVENTSTORE_ROUTE_CATALOG_DAPRD")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr/bin/daprd");
        await using var redis = new RouteCatalogDevelopmentRedis();
        await redis.StartAsync();
        await File.WriteAllBytesAsync(Path.Combine(resources, "statestore.yaml"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name = "statestore" },
            spec = new { type = "state.redis", version = "v1", metadata = new[]
            {
                new { name = "redisHost", value = redis.Endpoint }, new { name = "actorStateStore", value = "true" },
                new { name = "keyPrefix", value = "none" },
            } },
        }));
        string receiver = "transport-receiver-" + id, caller = "transport-caller-" + id;
        string configuration = Path.Combine(scratch, "receiver.json");
        await File.WriteAllBytesAsync(configuration, JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = "owned-transport-qualification" },
            spec = new { mtls = new { enabled = true }, accessControl = new { defaultAction = "deny", trustDomain = "public", policies = new[]
            {
                new { appId = caller, @namespace = "default", trustDomain = "public", defaultAction = "deny",
                    operations = new[] {
                        new { name = "dapr/config", httpVerb = new[] { "GET" }, action = "allow" },
                        new { name = "transport/marker", httpVerb = new[] { "POST" }, action = "allow" } } },
            } } },
        }));
        await using var transport = await DevelopmentRouteCatalogTransport.StartAsync(new(custody, runtime), "Development", TestContext.Current.CancellationToken);
        string temporary = transport.Directory;
        var sidecar = new RouteCatalogDevelopmentSidecar(receiver, resources, configuration, "owned-channel-" + id);
        var peer = new RouteCatalogDevelopmentSidecar(caller, resources, configuration);
        var wrongApp = new RouteCatalogDevelopmentSidecar("wrong-" + id, resources, configuration);
        var wrongNamespace = new RouteCatalogDevelopmentSidecar(caller, resources, configuration);
        foreach (var selected in new[] { sidecar, peer, wrongApp, wrongNamespace }) selected.ConfigureOwnedTransport(transport);
        wrongNamespace.ConfigureNativeActorNamespace("wrong-namespace");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:" + sidecar.AppPort);
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "owned-channel-" + id;
        builder.Services.AddEventStoreHttpHostSecurity();
        builder.Services.AddActors(options => options.Actors.RegisterActor<DevelopmentTransportActor>());
        builder.Services.Configure<ActorRuntimeOptions>(options => options.HttpEndpoint = sidecar.HttpEndpoint);
        await using var app = builder.Build();
        app.UseRouting(); app.UseEventStoreHttpHostSecurity(); app.UseAuthentication(); app.UseAuthorization();
        app.MapActorsHandlers().RequireEventStoreChannel();
        app.MapPost("/transport/marker", async ([FromBody] string marker) =>
        {
            var writer = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = sidecar.HttpEndpoint })
                .CreateActorProxy<IDevelopmentTransportActor>(new ActorId(id), nameof(DevelopmentTransportActor));
            await writer.RecordAsync(marker);
            return Results.NoContent();
        }).RequireEventStoreChannel();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken);
            foreach (var selected in new[] { sidecar, peer, wrongApp, wrongNamespace })
            {
                await selected.StartAsync();
                selected.ObserveOwnedTransport(transport);
            }
            using (var observed = Process.GetProcessById(peer.ProcessId))
                Should.Throw<InvalidOperationException>(() =>
                {
                    if (OperatingSystem.IsLinux()) transport.ObserveSidecar(observed, _ => null);
                });
            string path = "/v1.0/invoke/" + receiver + ".default/method/dapr/config";
            string writePath = "/v1.0/invoke/" + receiver + ".default/method/transport/marker";
            using (var positive = await client.GetAsync(peer.HttpEndpoint + path, TestContext.Current.CancellationToken))
                positive.StatusCode.ShouldBe(HttpStatusCode.OK, await positive.Content.ReadAsStringAsync());
            await Should.ThrowAsync<InvalidOperationException>(async () =>
            {
                if (OperatingSystem.IsLinux()) await transport.DisposeAsync();
            });
            System.IO.Directory.Exists(temporary).ShouldBeTrue("Live enrolled sidecars must preserve trust and control plane.");
            // This is an application-channel refusal reached after authenticated peer delivery.
            using (var applicationDenied = await client.GetAsync("http://127.0.0.1:" + sidecar.AppPort + "/dapr/config", TestContext.Current.CancellationToken))
                applicationDenied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            var proxyFactory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = peer.HttpEndpoint });
            IDevelopmentTransportActor actor = proxyFactory.CreateActorProxy<IDevelopmentTransportActor>(new ActorId(id), nameof(DevelopmentTransportActor));
            for (int attempt = 0; ; attempt++)
            {
                try { await actor.RecordAsync("persisted-before-restart"); break; }
                catch (Dapr.DaprApiException) when (attempt < 3) { await Task.Delay(100, TestContext.Current.CancellationToken); }
            }
            (await actor.ReadAsync()).ShouldBe("persisted-before-restart");
            string actorState = "/v1.0/actors/DevelopmentTransportActor/" + id + "/state/marker";
            using (var persisted = await client.GetAsync(sidecar.HttpEndpoint + actorState))
            {
                persisted.StatusCode.ShouldBe(HttpStatusCode.OK);
                (await persisted.Content.ReadAsStringAsync()).ShouldBe("\"persisted-before-restart\"");
            }
            using (var positiveWrite = await client.PostAsJsonAsync(peer.HttpEndpoint + writePath, "service-write-positive"))
                positiveWrite.StatusCode.ShouldBe(HttpStatusCode.NoContent, await positiveWrite.Content.ReadAsStringAsync());
            async Task AssertPersistedMarkerAsync()
            {
                using var persisted = await client.GetAsync(sidecar.HttpEndpoint + actorState);
                persisted.StatusCode.ShouldBe(HttpStatusCode.OK);
                (await persisted.Content.ReadAsStringAsync()).ShouldBe("\"service-write-positive\"");
            }
            await AssertPersistedMarkerAsync();
            foreach (var deniedPeer in new[] { wrongApp, wrongNamespace })
            {
                using var denied = await client.PostAsJsonAsync(deniedPeer.HttpEndpoint + writePath, "denied-write");
                denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                (await denied.Content.ReadAsStringAsync()).ShouldContain("ERR_DIRECT_INVOKE");
                await AssertPersistedMarkerAsync();
            }
            await using (var foreign = await DevelopmentRouteCatalogTransport.StartAsync(new(Path.Combine(scratch, "foreign-custody"), runtime), "Development"))
            {
                await using var untrusted = new RouteCatalogDevelopmentSidecar(caller, resources, configuration);
                untrusted.ConfigureOwnedTransport(foreign);
                await untrusted.StartAsync();
                untrusted.ObserveOwnedTransport(foreign);
                using var denied = await client.PostAsJsonAsync(untrusted.HttpEndpoint + writePath, "foreign-trust-write");
                denied.IsSuccessStatusCode.ShouldBeFalse("A certificate from another owned Sentry must not cross the receiver trust root.");
                await AssertPersistedMarkerAsync();
            }
            using (var reminder = await client.PutAsJsonAsync(sidecar.HttpEndpoint + "/v1.0/actors/DevelopmentTransportActor/" + id + "/reminders/owned-proof",
                new { dueTime = "1s", period = "" }))
                reminder.IsSuccessStatusCode.ShouldBeTrue(await reminder.Content.ReadAsStringAsync());
            bool delivered = false;
            for (int attempt = 0; attempt < 100 && !delivered; attempt++)
            {
                delivered = await actor.ReminderDeliveredAsync();
                if (!delivered) await Task.Delay(100, TestContext.Current.CancellationToken);
            }
            delivered.ShouldBeTrue("The owned authenticated scheduler must actually deliver its reminder.");
            await sidecar.StopAsync();
            await sidecar.StartAsync();
            sidecar.ObserveOwnedTransport(transport);
            (await actor.ReadAsync()).ShouldBe("service-write-positive");
            using (var persisted = await client.GetAsync(sidecar.HttpEndpoint + actorState))
                (await persisted.Content.ReadAsStringAsync()).ShouldBe("\"service-write-positive\"");
            TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new { stage = "public-transport-qualified",
                trustAnchorSha256 = transport.TrustAnchorSha256, peerDelivery = true, applicationDenial = true,
                wrongAppDenied = true, wrongNamespaceDenied = true, actorPersisted = true, nativeRestartReadback = true, schedulerDelivered = true }));
        }
        finally
        {
            List<Exception> cleanupFailures = [];
            foreach (var selected in new[] { wrongNamespace, wrongApp, peer, sidecar })
            {
                try { await selected.DisposeAsync(); }
                catch (Exception exception) { cleanupFailures.Add(exception); }
                try { await File.WriteAllTextAsync(Path.Combine(scratch, selected.AppId + "-" + selected.AppPort + ".log"), selected.Diagnostics); }
                catch (Exception exception) { cleanupFailures.Add(exception); }
            }
            using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await app.StopAsync(cleanupDeadline.Token); }
            catch (Exception exception) { cleanupFailures.Add(exception); }
            try { await transport.DisposeAsync(); }
            catch (Exception exception) { cleanupFailures.Add(exception); }
            if (cleanupFailures.Count > 0) throw new AggregateException("Owned transport qualification cleanup did not complete.", cleanupFailures);
        }
        System.IO.Directory.Exists(temporary).ShouldBeFalse();
        using var cleanup = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(custody, "cleanup.json")));
        cleanup.RootElement.GetProperty("complete").GetBoolean().ShouldBeTrue();
        // The receipt remains available for the calling qualification lane; no secret bytes are emitted.
        TestContext.Current.TestOutputHelper!.WriteLine("Owned transport custody: " + custody);
    }

    /// <summary>Refuses ambiguous native flags, missing/changed trust and unavailable Sentry before any sidecar launch.</summary>
    [Fact(Timeout = 60000)]
    public async Task PublicDevelopmentTransportRefusesIncompleteIdentityWithoutFallback()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string scratch = System.IO.Directory.CreateTempSubdirectory("eventstore-transport-negative-").FullName;
        string runtime = Environment.GetEnvironmentVariable("EVENTSTORE_ROUTE_CATALOG_DAPRD")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr/bin/daprd");
        await using var transport = await DevelopmentRouteCatalogTransport.StartAsync(new(Path.Combine(scratch, "custody"), runtime), "Development", TestContext.Current.CancellationToken);
        try
        {
            foreach (string[] arguments in new[]
            {
                new[] { "--enable-mtls=false" }, new[] { "--sentry-address=127.0.0.1:50001" },
                new[] { "--sentry-address", "127.0.0.1:50001", "--sentry-address", "127.0.0.1:50002" },
                new[] { "--sentry-address" }, new[] { "--sentry-address", "--app-id", "unrelated" },
                new[] { "--placement-host-address=127.0.0.1:50005" }, new[] { "--scheduler-host-address" },
            })
            {
                var start = new ProcessStartInfo(runtime);
                foreach (string argument in arguments) start.ArgumentList.Add(argument);
                Should.Throw<InvalidOperationException>(() =>
                {
                    if (OperatingSystem.IsLinux()) transport.ConfigureSidecar(start);
                });
            }
            string issuerKey = Path.Combine(transport.Directory, "credentials/issuer.key");
            File.Move(issuerKey, issuerKey + ".held");
            try
            {
                Should.Throw<InvalidOperationException>(() => { if (OperatingSystem.IsLinux()) transport.ConfigureSidecar(new(runtime)); });
            }
            finally { File.Move(issuerKey + ".held", issuerKey); }
            string issuer = Path.Combine(transport.Directory, "credentials/issuer.crt");
            byte[] original = await File.ReadAllBytesAsync(issuer);
            using (var certificate = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(await File.ReadAllTextAsync(issuer)))
            {
                foreach (DateTime instant in new[] { certificate.NotBefore.ToUniversalTime().AddSeconds(-1), certificate.NotAfter.ToUniversalTime().AddSeconds(1) })
                {
                    transport.CredentialClock = new FakeTimeProvider(new DateTimeOffset(instant));
                    Should.Throw<InvalidOperationException>(() => { if (OperatingSystem.IsLinux()) transport.ConfigureSidecar(new(runtime)); })
                        .Message.ShouldBe("Owned Development trust is expired or unavailable.");
                    (await File.ReadAllBytesAsync(issuer)).ShouldBe(original);
                }
                transport.CredentialClock = TimeProvider.System;
            }
            using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(scratch, "custody/ready.json")));
            var identity = receipt.RootElement.GetProperty("processes")[0];
            using var sentry = Process.GetProcessById(identity.GetProperty("pid").GetInt32());
            sentry.StartTime.ToUniversalTime().ShouldBe(identity.GetProperty("startTimeUtc").GetDateTime());
            sentry.Kill(entireProcessTree: true);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await sentry.WaitForExitAsync(deadline.Token);
            Should.Throw<InvalidOperationException>(() => { if (OperatingSystem.IsLinux()) transport.ConfigureSidecar(new(runtime)); });
        }
        finally { await transport.DisposeAsync(); }
        System.IO.Directory.Exists(transport.Directory).ShouldBeFalse();
    }

    /// <summary>A deterministic failure after Sentry and placement start stops exact owners and retains incomplete private custody.</summary>
    [Fact(Timeout = 60000)]
    public async Task PublicDevelopmentTransportRetainsPrivateCustodyAfterPartialStartupFailure()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string scratch = System.IO.Directory.CreateTempSubdirectory("eventstore-transport-partial-").FullName;
        string runtime = Environment.GetEnvironmentVariable("EVENTSTORE_ROUTE_CATALOG_DAPRD")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr/bin/daprd");
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        bool inventoryObservedBeforeCleanup = false;
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            if (OperatingSystem.IsLinux()) await DevelopmentRouteCatalogTransport.StartAsync(
                new(Path.Combine(scratch, "custody"), runtime, SchedulerAddress: "127.0.0.1:" + port),
                "Development", (transport, checkpoint) =>
                {
                    if (checkpoint != "scheduler-started") return;
                    File.Exists(Path.Combine(scratch, "custody/startup.json")).ShouldBeTrue();
                    File.Exists(Path.Combine(scratch, "custody/cleanup.json")).ShouldBeFalse();
                    foreach (string name in new[] { "sentry", "placement", "scheduler" })
                    {
                        File.Exists(Path.Combine(scratch, "custody", name + ".launch.json")).ShouldBeTrue();
                        using var identity = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(scratch, "custody", name + ".identity.json")));
                        identity.RootElement.GetProperty("pid").GetInt32().ShouldBeGreaterThan(0);
                        identity.RootElement.GetProperty("start").GetString().ShouldNotBeNullOrEmpty();
                    }
                    inventoryObservedBeforeCleanup = true;
                }, TestContext.Current.CancellationToken);
        });
        inventoryObservedBeforeCleanup.ShouldBeTrue();
        using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(scratch, "custody/cleanup.json")));
        receipt.RootElement.GetProperty("complete").GetBoolean().ShouldBeFalse();
        receipt.RootElement.GetProperty("ownedProcessesExited").GetBoolean().ShouldBeTrue();
        receipt.RootElement.GetProperty("startupQualified").GetBoolean().ShouldBeFalse();
        receipt.RootElement.GetProperty("processes").GetArrayLength().ShouldBe(3);
        string retained = receipt.RootElement.GetProperty("directory").GetString()!;
        System.IO.Directory.Exists(retained).ShouldBeTrue();
        File.Exists(Path.Combine(retained, "credentials/issuer.key")).ShouldBeTrue();
        foreach (var identity in receipt.RootElement.GetProperty("processes").EnumerateArray())
        {
            string path = "/proc/" + identity.GetProperty("pid").GetInt32() + "/stat";
            if (!File.Exists(path)) continue;
            string stat = await File.ReadAllTextAsync(path);
            stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19]
                .ShouldNotBe(identity.GetProperty("start").GetString());
        }
        TestContext.Current.TestOutputHelper!.WriteLine("Retained incomplete transport custody: " + Path.Combine(scratch, "custody"));
    }

    /// <summary>Rejects noncanonical runtimes and unavailable adapter prerequisites before allocating transport custody.</summary>
    [Fact]
    public async Task PublicDevelopmentTransportValidatesExecutablePrerequisitesBeforeResources()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string scratch = System.IO.Directory.CreateTempSubdirectory("eventstore-transport-prerequisites-").FullName;
        string runtime = Environment.GetEnvironmentVariable("EVENTSTORE_ROUTE_CATALOG_DAPRD")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr/bin/daprd");
        string linked = Path.Combine(scratch, "daprd-link");
        File.CreateSymbolicLink(linked, runtime);
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            if (OperatingSystem.IsLinux()) await DevelopmentRouteCatalogTransport.StartAsync(new(Path.Combine(scratch, "custody"), linked), "Development");
        });
        System.IO.Directory.Exists(Path.Combine(scratch, "custody")).ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            if (OperatingSystem.IsLinux()) await DevelopmentRouteCatalogTransport.ValidatePrerequisitesAsync(runtime, Path.Combine(scratch, "missing-python"), TestContext.Current.CancellationToken);
        });
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            if (OperatingSystem.IsLinux()) await DevelopmentRouteCatalogTransport.ValidatePrerequisitesAsync(runtime, "/usr/bin/false", TestContext.Current.CancellationToken);
        });
        System.IO.Directory.Exists(Path.Combine(scratch, "custody")).ShouldBeFalse();
    }

    /// <summary>Final ready-publication failure never marks startup qualified or deletes private trust.</summary>
    [Fact(Timeout = 60000)]
    public async Task PublicDevelopmentTransportRetainsTrustWhenFinalReadyPublicationFails()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string scratch = System.IO.Directory.CreateTempSubdirectory("eventstore-transport-publication-").FullName;
        string custody = Path.Combine(scratch, "custody");
        string runtime = Environment.GetEnvironmentVariable("EVENTSTORE_ROUTE_CATALOG_DAPRD")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr/bin/daprd");
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            if (OperatingSystem.IsLinux()) await DevelopmentRouteCatalogTransport.StartAsync(new(custody, runtime), "Development",
                (_, checkpoint) => { if (checkpoint == "publish-ready") System.IO.Directory.CreateDirectory(Path.Combine(custody, "ready.json")); },
                TestContext.Current.CancellationToken);
        });
        using var cleanup = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(custody, "cleanup.json")));
        cleanup.RootElement.GetProperty("startupQualified").GetBoolean().ShouldBeFalse();
        cleanup.RootElement.GetProperty("complete").GetBoolean().ShouldBeFalse();
        cleanup.RootElement.GetProperty("ownedProcessesExited").GetBoolean().ShouldBeTrue();
        File.Exists(Path.Combine(cleanup.RootElement.GetProperty("directory").GetString()!, "credentials/issuer.key")).ShouldBeTrue();
        File.Exists(Path.Combine(custody, "ready.json")).ShouldBeFalse();
    }
}
