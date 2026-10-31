extern alias SampleApi;

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public class SampleApiCatalogReadinessTests
{
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    [Fact]
    public async Task UnenrolledNativeHostIsLiveButNotReadyAndCannotInvokeGateway()
    {
        var gateway = Gateway();
        await using var factory = Factory(gateway);
        using var client = Client(factory);
        factory.Services.GetRequiredService<IRouteCatalogAuthority>().ShouldBeOfType<RouteCatalogAuthority>();
        using var alive = await client.GetAsync("/alive", TestContext.Current.CancellationToken);
        using var ready = await client.GetAsync("/ready", TestContext.Current.CancellationToken);
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var request = Request("ui");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        gateway.QueryCallCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("ui", HttpStatusCode.OK)]
    [InlineData("agent", HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Forbidden)]
    public async Task SchemaThreeRunsActualGeneratedFilterWithSignedSurface(string? surface, HttpStatusCode expected)
    {
        var gateway = Gateway();
        await using var factory = Factory(gateway, 3);
        using var client = Client(factory);
        using var request = Request(surface);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(expected);
        factory.Services.GetRequiredService<IRouteCatalogAuthority>().ShouldBeOfType<SampleApiCommittedCatalog>().Acquisitions.ShouldBe(1);
        gateway.QueryCallCount.ShouldBe(expected == HttpStatusCode.OK ? 1 : 0);
        if (expected == HttpStatusCode.OK)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            body.RootElement.GetProperty("count").GetInt32().ShouldBe(7);
        }
    }

    [Fact]
    public async Task ActualPrivateWorkerObservesFiveCompiledDeclarationsSchemasAndActualAppSuffix()
    {
        string directory = Path.Combine(Path.GetTempPath(), "sample-api-observation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "component.json"), """{"metadata":{"name":"route-catalog"}}""");
            await File.WriteAllTextAsync(Path.Combine(directory, "acl.json"), """{"spec":{"accessControl":{"defaultAction":"deny","policies":[]}}}""");
            RouteCatalogHostObservation actual = await ObserveNativeApiProcessAsync(directory);
            actual.AppId.ShouldBe("sample-api-session-one");
            actual.Declarations.Count.ShouldBe(5);
            actual.Declarations.Select(RouteCatalogCodec.ComputeDeclarationDigest).Order(StringComparer.Ordinal)
                .ShouldBe(CounterCatalogDeclarations.Routes.Select(RouteCatalogCodec.ComputeDeclarationDigest).Order(StringComparer.Ordinal));
            actual.Operations.ShouldNotBeNull().Count.ShouldBe(5);
            actual.Operations.Select(operation => operation.Digest).Order(StringComparer.Ordinal)
                .ShouldBe(CounterCatalogDeclarations.Operations.Select(operation => operation.Digest).Order(StringComparer.Ordinal));
            actual.Operations.ShouldAllBe(operation => operation.Digest == RouteCatalogCodec.ComputeOperationContractDigest(operation));
            actual.Targets.ShouldBeEmpty(); actual.AdmissionPolicies.ShouldNotBeNull().ShouldBeEmpty();
            actual.ReceiverSurface.ShouldNotBeNull().Endpoints.ShouldNotBeEmpty();
            actual.RequiredHostIds.ShouldBe(["eventstore", "sample-api"]);
            actual.SelectedAppIds.ShouldBe(["eventstore-session-one", "sample-api-session-one"]);
            File.Exists(Path.Combine(directory, "activation.json")).ShouldBeFalse();
            File.Exists(Path.Combine(directory, "sample-api.committed.json")).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task FixtureRefusesEveryLifecycleMutationAndOutOfScopeRoute()
    {
        var authority = new SampleApiCommittedCatalog();
        string root = (await authority.ReadbackAsync()).RootDigest;
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.PrepareAsync(null!));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReadyAsync("unowned"));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.CommitAsync("unowned"));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.RollbackAsync(null!));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReconcileAsync(null!));
        await using var lease = await authority.AcquireAsync();
        lease.Snapshot.RootDigest.ShouldBe(root);
        Should.Throw<RouteCatalogUnavailableException>(() => lease.Snapshot.Resolve("query", "outside", "counter", "get-counter-status"));
        Should.Throw<RouteCatalogUnavailableException>(() => lease.Snapshot.Resolve("query", "tenant-a", "counter", "undeclared"));
    }

    private static async Task<RouteCatalogHostObservation> ObserveNativeApiProcessAsync(string directory)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "samples/Hexalith.EventStore.Sample.Api/Program.cs"))) root = root.Parent;
        string configuration = typeof(SampleApiCatalogReadinessTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
            ?? throw new InvalidOperationException("The actual test build configuration is required.");
        if (configuration is not ("Debug" or "Release")) throw new InvalidOperationException("A qualified native test configuration is required.");
        string executable = Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException("Cannot find the owning sample API."),
            "samples/Hexalith.EventStore.Sample.Api/bin", configuration, "net10.0/Hexalith.EventStore.Sample.Api.dll");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(executable);
        start.WorkingDirectory = Path.GetDirectoryName(executable)!;
        var settings = new Dictionary<string, string>
        {
            ["DOTNET_ENVIRONMENT"] = "Development", ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:0", ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:3500",
            ["Authentication__JwtBearer__Issuer"] = "sample-api-catalog", ["Authentication__JwtBearer__Audience"] = "sample-api",
            ["Authentication__JwtBearer__SigningKey"] = Key, ["Authentication__JwtBearer__RequireHttpsMetadata"] = "false",
            ["EventStore__DomainService__AppId"] = "sample-api-session-one", ["APP_API_TOKEN"] = "sample-api-test-channel",
            ["EventStore__RouteCatalog__DevelopmentBootstrap__Directory"] = directory,
            ["EventStore__RouteCatalog__DevelopmentBootstrap__HostId"] = "sample-api",
            ["EventStore__RouteCatalog__DevelopmentBootstrap__CoordinatorHostId"] = "eventstore",
            ["EventStore__RouteCatalog__DevelopmentBootstrap__AttemptId"] = "sample-api-observation",
            ["EventStore__RouteCatalog__DevelopmentBootstrap__RequiredHostIds__0"] = "eventstore",
            ["EventStore__RouteCatalog__DevelopmentBootstrap__RequiredHostIds__1"] = "sample-api",
            ["EventStore__RouteCatalog__DevelopmentBootstrap__ComponentPath"] = Path.Combine(directory, "component.json"),
            ["EventStore__RouteCatalog__DevelopmentBootstrap__AccessControlPath"] = Path.Combine(directory, "acl.json"),
            ["EventStore__RouteCatalog__DevelopmentBootstrap__SelectedAppIds__0"] = "eventstore-session-one",
            ["EventStore__RouteCatalog__DevelopmentBootstrap__SelectedAppIds__1"] = "sample-api-session-one",
            ["EventStore__RouteCatalog__DevelopmentBootstrap__DeclaredInvocations"] = "[]",
        };
        foreach (var setting in settings) start.Environment[setting.Key] = setting.Value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the actual native API observer.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        try
        {
            string path = Path.Combine(directory, "sample-api.observation.json");
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(10));
            while (!File.Exists(path))
            {
                if (process.HasExited) throw new InvalidOperationException("Native API exited before observation: " + await output + await errors);
                await Task.Delay(25, bound.Token);
            }
            return JsonSerializer.Deserialize<RouteCatalogHostObservation>(await File.ReadAllBytesAsync(path, bound.Token)).ShouldNotBeNull();
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cleanup.Token);
            process.HasExited.ShouldBeTrue();
            await output; await errors;
        }
    }

    private static WebApplicationFactory<SampleApi::Program> Factory(FakeEventStoreGatewayClient gateway, int? schema = null)
        => new WebApplicationFactory<SampleApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Authority"] = null, ["Authentication:JwtBearer:Issuer"] = "sample-api-catalog",
                ["Authentication:JwtBearer:Audience"] = "sample-api", ["Authentication:JwtBearer:SigningKey"] = Key,
                ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:3500",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEventStoreGatewayClient>(); services.AddSingleton<IEventStoreGatewayClient>(gateway);
                if (schema is not null) services.AddSampleApiCommittedCatalog(schema.Value);
            });
        });

    private static HttpClient Client(WebApplicationFactory<SampleApi::Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static HttpRequestMessage Request(string? surface)
    {
        var claims = new List<Claim> { new("sub", "sample-human") };
        if (surface is not null) claims.Add(new("eventstore:surface", surface));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "sample-api-catalog", Audience = "sample-api", Expires = DateTime.UtcNow.AddMinutes(2),
            Subject = new ClaimsIdentity(claims), SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256),
        });
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenant-a/counter/counter-1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static FakeEventStoreGatewayClient Gateway() => new()
    {
        QueryHandler = static (_, _, _) => Task.FromResult(new EventStoreQueryResult("catalog-query", JsonSerializer.SerializeToElement(new { count = 7 }), false, null)),
    };
}
