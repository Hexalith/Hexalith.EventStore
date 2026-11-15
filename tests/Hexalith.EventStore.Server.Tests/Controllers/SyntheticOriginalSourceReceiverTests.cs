using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

using Dapr.Actors.Client;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.OriginalSource;
using Hexalith.EventStore.Testing.Security;
using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Server.Tests.Fakes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>The actual scanner, HTTP receiver and owner append worker qualify synthetic originals under unchanged authorization.</summary>
public sealed class SyntheticOriginalSourceReceiverTests
{
    /// <summary>Real scan authorization precedes selected source access and independently retained originals survive receiver replacement.</summary>
    [Theory]
    [InlineData(10_001)]
    [InlineData(32_769)]
    public async Task RealScannerReadsOnlyAuthorizedCommittedOriginalsAcrossReceiverRestart(int count)
    {
        count.ShouldBeGreaterThan(10_000);
        const string run = "0123456789abcdef";
        const string tenant = "durability-" + run;
        const string channel = "original-source-channel";
        const string caller = "folders";
        string directory = Path.Combine(Path.GetTempPath(), "original-receiver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using RSA key = RSA.Create(2048);
        var proxies = Substitute.For<IActorProxyFactory>();
        var config = new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = channel,
            ["Authentication:JwtBearer:Issuer"] = WorkloadAssertionTestTokens.Issuer,
            ["Authentication:JwtBearer:Audience"] = DaprInternalAuthenticationOptions.DefaultAudience,
            ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:NativeSourceScan:PublicKeys:" + caller] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            ["Authentication:NativeSourceScan:AllowedDomains:folders"] = "folders",
            ["Authentication:WorkloadIssuer:Workload"] = "eventstore-" + run,
            ["EventStore:Testing:OriginalSource:Enabled"] = "true",
            ["EventStore:Testing:OriginalSource:Mode"] = SyntheticOriginalSourceOptions.SelectedMode,
            ["EventStore:Testing:OriginalSource:Directory"] = directory,
            ["EventStore:Testing:OriginalSource:RunId"] = run,
            ["EventStore:Testing:OriginalSource:Tenant"] = tenant,
            ["EventStore:Testing:OriginalSource:Domain"] = "folders",
            ["EventStore:Testing:OriginalSource:AggregatePrefix"] = "capacity-",
        };
#if DEBUG
        async Task<WebApplication> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.WebHost.UseTestServer(); builder.Logging.ClearProviders(); builder.Configuration.AddInMemoryCollection(config);
            builder.Services.Configure<RouteCatalogHostOptions>(options =>
            { options.DevelopmentFixture = true; options.Profile = "Development"; options.RequireTechnicalHttpBinding = true; });
            builder.Services.AddAuthentication(DaprInternalAuthenticationOptions.SchemeName)
                .AddEventStoreWorkloadScheme(DaprInternalAuthenticationOptions.SchemeName, "Authentication:DaprInternal", (options, _) =>
                { options.Audience = DaprInternalAuthenticationOptions.DefaultAudience; options.AllowedCallers = [caller]; });
            builder.Services.AddEventStoreWorkloadPolicies(DaprInternalAuthenticationOptions.SchemeName,
                [EventStoreWorkloadOperations.GatewayNativeSourceScan]);
            builder.Services.AddControllers().AddApplicationPart(typeof(StreamsController).Assembly);
            builder.Services.AddSingleton(proxies);
            builder.Services.AddSingleton<Hexalith.EventStore.Authorization.ITenantValidator>(new FakeTenantValidator());
            builder.Services.AddSingleton<IRbacValidator>(new FakeRbacValidator());
            builder.Services.AddPrivateSyntheticOriginalSource(builder.Configuration, builder.Environment);
            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync(TestContext.Current.CancellationToken); return app;
        }
        async Task<HttpResponseMessage> SendAsync(HttpClient http, StreamReadRequest body, string fault = "valid")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/streams/scan/workload") { Content = JsonContent.Create(body) };
            request.Headers.Add(DaprAppChannelToken.HeaderName, channel);
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
                WorkloadAssertionTestTokens.Create(DaprInternalAuthenticationOptions.DefaultAudience, caller,
                    [fault == "operation" ? EventStoreWorkloadOperations.GatewayStreamRead : EventStoreWorkloadOperations.GatewayNativeSourceScan],
                    bindings: fault == "binding" ? null : new Dictionary<string, string>
                    { [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
                        [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = "folders" }));
            if (fault != "proof") request.Headers.Add(BoundNativeSourceScanProof.HeaderName,
                BoundNativeSourceScanProof.Issue(body, caller, Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow));
            if (fault == "human") request.Headers.Authorization = new("Bearer", "human");
            return await http.SendAsync(request, TestContext.Current.CancellationToken);
        }
        try
        {
            var original = new SyntheticOriginalSourceClient(directory, run);
            var identity = new AggregateIdentity(tenant, "folders", "capacity-real-original");
            var metadata = new StreamReadRequest(tenant, "folders", identity.AggregateId, 0, 0, PageSize: 1) { RequireNativeProof = true };
            for (int restart = 0; restart < 2; restart++)
            {
                await using var app = await StartAsync(); using var http = app.GetTestClient();
                if (restart == 0)
                {
                    using var missing = await SendAsync(http, metadata); missing.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
                    var events = Enumerable.Range(1, count).Select(sequence => new ProjectionEventDto("Original.Event", BitConverter.GetBytes(sequence),
                        "json", sequence, DateTimeOffset.UnixEpoch.AddSeconds(sequence), "original", "original-message-" + sequence,
                        GlobalPosition: sequence + 1) { MetadataVersion = 1 }).ToArray();
                    (await original.AppendAsync(tenant, "folders", identity.AggregateId, events, TestContext.Current.CancellationToken)).ShouldBeTrue();
                    (await original.AppendAsync(tenant, "folders", identity.AggregateId, [events[0] with { Payload = [99] }], TestContext.Current.CancellationToken)).ShouldBeFalse();
                }
                else
                {
                    var growth = new ProjectionEventDto("Original.Event", BitConverter.GetBytes(count + 1), "json", count + 1,
                        DateTimeOffset.UnixEpoch.AddSeconds(count + 1), "original", "original-message-" + (count + 1), GlobalPosition: count + 2)
                        { MetadataVersion = 1 };
                    (await original.AppendAsync(tenant, "folders", identity.AggregateId, [growth], TestContext.Current.CancellationToken)).ShouldBeTrue();
                }
                foreach (string fault in new[] { "operation", "binding", "proof", "human" })
                {
                    using var refusal = await SendAsync(http, metadata, fault);
                    refusal.StatusCode.ShouldBe(fault == "human" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
                }
                var gateway = Substitute.For<IEventStoreGatewayClient>();
                gateway.ReadNativeSourceScanPageAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
                {
                    using var response = await SendAsync(http, call.Arg<StreamReadRequest>());
                    response.EnsureSuccessStatusCode();
                    return (await response.Content.ReadFromJsonAsync<StreamReadPage>(TestContext.Current.CancellationToken))!;
                });
                var result = await new NativeSourceScanner(gateway, TimeProvider.System).ScanAsync(identity,
                    () => new OriginalSourceCountingFold(), TestContext.Current.CancellationToken);
                result.ReasonCode.ShouldBeNull(); result.Observation.ShouldNotBeNull(); result.Value.ShouldBe(count + restart);
                proxies.ReceivedCalls().ShouldBeEmpty();
                await app.StopAsync(TestContext.Current.CancellationToken);
                Directory.GetFiles(directory, "*.working").ShouldBeEmpty();
                Directory.GetFiles(directory, "*.response").ShouldBeEmpty();
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
#else
        try
        {
            await Task.Yield();
            var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Development);
            var services = new ServiceCollection();
            services.AddPrivateSyntheticOriginalSource(new ConfigurationBuilder().Build(), environment); services.ShouldBeEmpty();
            Should.Throw<InvalidOperationException>(() => services.AddPrivateSyntheticOriginalSource(
                new ConfigurationBuilder().AddInMemoryCollection(config).Build(), environment));
            services.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, recursive: true); }
#endif
    }
}
