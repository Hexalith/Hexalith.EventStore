using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Testing.Integration.Tests;

internal sealed class DescriptorHandoffScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);
    private readonly string _path = Path.Combine(Path.GetTempPath(), "descriptor-" + Guid.NewGuid().ToString("N") + ".json");

    internal DescriptorHandoffScope(bool missing = false)
    {
        JsonNode root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ready-owner.json")))!;
        JsonNode environment = root["bindings"]!["environment"]!;
        JsonNode sidecar = environment["resources"]![0]!.DeepClone();
        sidecar["resourceId"] = "sidecar";
        environment["resources"]!.AsArray().Add(sidecar);
        JsonNode ready = environment["readiness"]!["resources"]![0]!.DeepClone();
        ready["resourceId"] = "sidecar";
        environment["readiness"]!["resources"]!.AsArray().Add(ready);
        File.WriteAllText(_path, root.ToJsonString());
        foreach ((string key, string value) in new Dictionary<string, string>
        {
            ["HEXALITH_TEST_ENVIRONMENT_PATH"] = _path,
            ["HEXALITH_G4_RUN_ID"] = "sample-owner-run",
            ["HEXALITH_G4_TENANT"] = "sample-synthetic-tenant",
            ["HEXALITH_G4_RESOURCE_NAMESPACE"] = "sample-data",
            ["HEXALITH_G4_ACCESS_TOKEN"] = "private-runner-token",
        })
        {
            _previous[key] = System.Environment.GetEnvironmentVariable(key);
            System.Environment.SetEnvironmentVariable(key, missing ? null : value);
        }
    }

    public void Dispose()
    {
        foreach ((string key, string? value) in _previous)
        {
            System.Environment.SetEnvironmentVariable(key, value);
        }
        File.Delete(_path);
    }
}
