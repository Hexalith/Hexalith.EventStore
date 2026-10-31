namespace Hexalith.EventStore.Testing.Tests;

using System.Security.Cryptography;
using System.Text.Json.Nodes;

/// <summary>Owns only local contract files and a detached descriptor for credential-bound consumer tests.</summary>
internal sealed class EndpointCredentialTestEnvironment : IDisposable
{
    // Generated per process so no usable credential material is committed with the fixture.
    internal static readonly string ApiToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    internal static readonly string BearerToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    internal EndpointCredentialTestEnvironment(string uri = "http://127.0.0.1:18080/v1.0/invoke/declared-owner/method/")
    {
        DirectoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "endpoint-credential-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        DescriptorPath = System.IO.Path.Combine(DirectoryPath, "test-environment.json");
        CredentialPath = System.IO.Path.Combine(DirectoryPath, TestEnvironmentEndpointCredentials.FileName);
        JsonNode descriptor = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "ready-owner.json")))!;
        descriptor["bindings"]!["environment"]!["resources"]![0]!["endpoints"]!.AsArray().Add(new JsonObject
        {
            ["name"] = "dapr-invocation", ["protocol"] = "http", ["uri"] = uri,
        });
        File.WriteAllText(DescriptorPath, descriptor.ToJsonString());
        Environment = TestEnvironmentDescriptorReader.ReadDescriptor(DescriptorPath);
        Environment.BindCredentials(Environment.RunId, Environment.SyntheticTenantId, Environment.DataIsolationId, BearerToken);
        Credentials = new JsonObject
        {
            ["schema"] = TestEnvironmentEndpointCredentials.Schema, ["runId"] = Environment.RunId,
            ["syntheticTenantId"] = Environment.SyntheticTenantId, ["dataIsolationId"] = Environment.DataIsolationId,
            ["endpoints"] = new JsonArray(new JsonObject
            {
                ["resourceId"] = "eventstore", ["moduleId"] = "Hexalith.EventStore", ["endpointName"] = "dapr-invocation",
                ["uri"] = uri, ["headerName"] = "dapr-api-token", ["value"] = ApiToken,
            }),
        };
    }

    internal string DirectoryPath { get; }
    internal string DescriptorPath { get; }
    internal string CredentialPath { get; }
    internal TestEnvironmentDescriptor Environment { get; }
    internal JsonObject Credentials { get; }

    internal void WriteCredentials()
    {
        File.WriteAllText(CredentialPath, Credentials.ToJsonString());
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(CredentialPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    internal void Bind()
    {
        WriteCredentials();
        Environment.BindEndpointCredentials(CredentialPath, DescriptorPath);
    }

    public void Dispose()
    {
        Environment.Dispose();
        Directory.Delete(DirectoryPath, recursive: true);
    }
}
