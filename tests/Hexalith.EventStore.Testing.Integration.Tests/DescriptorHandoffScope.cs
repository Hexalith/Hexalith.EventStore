using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Testing.Integration.Tests;

internal sealed class DescriptorHandoffScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "descriptor-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    internal DescriptorHandoffScope(bool missing = false, bool invocation = false, bool credentials = true)
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "test-environment.json");
        JsonNode root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ready-owner.json")))!;
        JsonNode environment = root["bindings"]!["environment"]!;
        environment["resources"]![0]!["endpoints"]!.AsArray().Add(new JsonObject
        {
            ["name"] = "dapr-http", ["protocol"] = "http", ["uri"] = "http://127.0.0.1:18081",
        });
        JsonNode sidecar = environment["resources"]![0]!.DeepClone();
        sidecar["resourceId"] = "sidecar";
        environment["resources"]!.AsArray().Add(sidecar);
        JsonNode ready = environment["readiness"]!["resources"]![0]!.DeepClone();
        ready["resourceId"] = "sidecar";
        environment["readiness"]!["resources"]!.AsArray().Add(ready);
        const string invocationUri = "http://127.0.0.1:18081/v1.0/invoke/declared-owner/method/api/v1/";
        string credentialPath = Path.Combine(_directory, "native-endpoint-credentials.json");
        if (invocation)
        {
            environment["resources"]![0]!["endpoints"]!.AsArray().Add(new JsonObject
            {
                ["name"] = "dapr-invocation", ["protocol"] = "http", ["uri"] = invocationUri,
            });
            if (credentials)
            {
                File.WriteAllText(credentialPath, new JsonObject
                {
                    ["schema"] = "hexalith.test-endpoint-credentials.v1", ["runId"] = "sample-owner-run",
                    ["syntheticTenantId"] = "sample-synthetic-tenant", ["dataIsolationId"] = "sample-data",
                    ["endpoints"] = new JsonArray(new JsonObject
                    {
                        ["resourceId"] = "eventstore", ["moduleId"] = "Hexalith.EventStore", ["endpointName"] = "dapr-invocation",
                        ["uri"] = invocationUri, ["headerName"] = "dapr-api-token", ["value"] = "fixture-private-sidecar-credential",
                    }),
                }.ToJsonString());
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(credentialPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        File.WriteAllText(_path, root.ToJsonString());
        foreach ((string key, string value) in new Dictionary<string, string>
        {
            ["HEXALITH_TEST_ENVIRONMENT_PATH"] = _path,
            ["HEXALITH_G4_RUN_ID"] = "sample-owner-run",
            ["HEXALITH_G4_TENANT"] = "sample-synthetic-tenant",
            ["HEXALITH_G4_RESOURCE_NAMESPACE"] = "sample-data",
            ["HEXALITH_G4_ACCESS_TOKEN"] = "private-runner-token",
            ["HEXALITH_TEST_ENDPOINT_CREDENTIALS_PATH"] = invocation && credentials ? credentialPath : string.Empty,
        })
        {
            _previous[key] = System.Environment.GetEnvironmentVariable(key);
            System.Environment.SetEnvironmentVariable(key, missing || value.Length == 0 ? null : value);
        }
    }

    public void Dispose()
    {
        foreach ((string key, string? value) in _previous)
        {
            System.Environment.SetEnvironmentVariable(key, value);
        }
        Directory.Delete(_directory, recursive: true);
    }
}
