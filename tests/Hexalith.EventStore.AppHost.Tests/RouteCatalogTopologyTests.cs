using System.Text.Json;

using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.AppHost.Tests;

public sealed class RouteCatalogTopologyTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("unknown-operation")]
    [InlineData("unknown-app")]
    [InlineData("known-app")]
    [InlineData("open-default")]
    [InlineData("scope-drift")]
    [InlineData("duplicate-prefix")]
    [InlineData("missing-generated-host")]
    public void DevelopmentFixtureComparesSemanticAclAndExactHostInventory(string fault)
    {
        string root = Root();
        var original = RouteCatalogCodec.Decode(File.ReadAllBytes(Path.Combine(root, "deploy/dapr/eventstore-routing-catalog.json")));
        var declarations = original.Declarations.Where(d => d.Domain is "counter" or "greeting").ToArray();
        var ids = declarations.Select(d => d.RouteId).ToHashSet(StringComparer.Ordinal);
        string[] apps = ["catalog-gateway", "catalog-domain", "catalog-api"];
        var envelope = RouteCatalogCodec.Seal(original with
        {
            Declarations = declarations,
            Routes = original.Routes.Where(r => ids.Contains(r.RouteId)).Select(r => r with { AppId = r.AppId == "eventstore" ? apps[0] : apps[1] }).ToArray(),
            Idempotency = original.Idempotency.Where(i => ids.Contains(i.RouteId)).ToArray(),
        });
        var hosts = apps.Select(a => new RouteCatalogHostIdentity(a, a, new string('a', 64), new string('b', 64), new string('c', 64))).ToArray();
        var activation = new RouteCatalogActivation("fixture", RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), null, hosts, "Development-fixture", "");
        byte[] component = fault == "scope-drift"
            ? JsonSerializer.SerializeToUtf8Bytes(new { kind = "Component", spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = new[] { "undeclared" } })
            : File.ReadAllBytes(Path.Combine(root, "deploy/dapr/route-catalog-development-fixture/component.json"));
        if (fault == "duplicate-prefix") component = JsonSerializer.SerializeToUtf8Bytes(new
        {
            kind = "Component", spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" }, new { name = "keyPrefix", value = "app-id" } } }, scopes = apps,
        });
        byte[] config = fault == "open-default"
            ? JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "allow", trustDomain = "public" } } })
            : fault is "unknown-operation" or "unknown-app" or "known-app"
                ? JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public", policies = new[]
                {
                    new { appId = fault == "unknown-app" ? "unknown-source" : apps[0], defaultAction = "deny", operations = new[]
                    { new { name = fault == "unknown-operation" ? "/undeclared-effect" : "/internal/routing/catalog-generation", httpVerb = new[] { fault == "unknown-operation" ? "POST" : "GET" }, action = "allow" } } },
                } } } })
                : File.ReadAllBytes(Path.Combine(root, "deploy/dapr/route-catalog-development-fixture/configuration.json"));
        string[] observed = fault == "missing-generated-host" ? apps.Take(2).ToArray() : apps;
        var configurations = apps.ToDictionary(a => a, _ => (ReadOnlyMemory<byte>)config, StringComparer.Ordinal);
        if (fault is "valid" or "known-app") RouteCatalogTopologyGuard.ValidateDevelopmentFixture(activation, observed, component, configurations);
        else Should.Throw<RouteCatalogValidationException>(() => RouteCatalogTopologyGuard.ValidateDevelopmentFixture(activation, observed, component, configurations));
    }

    [Fact]
    public void GeneralAppHostCannotActivateUnqualifiedPrerequisiteTopology()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["EventStore:RouteCatalog:ActivationFile"] = "/candidate.json" }).Build();
        Should.Throw<InvalidOperationException>(() => RouteCatalogTopologyGuard.RefuseUnqualifiedActivation(configuration));
    }

    private static string Root()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
