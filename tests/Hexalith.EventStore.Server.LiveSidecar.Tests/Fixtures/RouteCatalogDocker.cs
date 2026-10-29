using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Captures Docker's effective local selection before mutation and pins it for this fixture's lifetime.</summary>
internal sealed class RouteCatalogDocker(string endpoint, string executable)
{
    internal string Endpoint { get; } = endpoint;

    internal static async Task<RouteCatalogDocker> CaptureAsync(string executable = "docker",
        IReadOnlyDictionary<string, string?>? selection = null, CancellationToken cancellationToken = default)
    {
        string? Selected(string name) => selection is null ? Environment.GetEnvironmentVariable(name)
            : selection.TryGetValue(name, out string? value) ? value : null;
        string? context = Selected("DOCKER_CONTEXT"), host = Selected("DOCKER_HOST");
        string endpoint;
        // DOCKER_CONTEXT takes precedence over DOCKER_HOST, including the default context.
        if (!string.IsNullOrEmpty(context) || string.IsNullOrEmpty(host))
        {
            var start = new ProcessStartInfo(executable);
            start.Environment.Remove("DOCKER_HOST");
            start.Environment.Remove("DOCKER_CONTEXT");
            start.ArgumentList.Add("context"); start.ArgumentList.Add("inspect");
            if (!string.IsNullOrEmpty(context)) start.ArgumentList.Add(context);
            using var inspection = JsonDocument.Parse(await RouteCatalogOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false));
            endpoint = inspection.RootElement[0].GetProperty("Endpoints").GetProperty("docker").GetProperty("Host").GetString()
                ?? throw new InvalidOperationException("Docker has no effective endpoint.");
            if (!string.IsNullOrEmpty(context) && !string.IsNullOrEmpty(host) && host != endpoint)
                throw new InvalidOperationException("Conflicting Docker context and host selections cannot qualify an owned fixture.");
        }
        else endpoint = host!;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? address) || address.Scheme != "unix"
            || address.Host.Length != 0 || !Path.IsPathFullyQualified(address.AbsolutePath))
            throw new InvalidOperationException("Catalog qualification requires a local Unix Docker endpoint.");
        return new(endpoint, executable);
    }

    internal Task<string> RunAsync(string[] arguments, CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo(executable);
        start.Environment.Remove("DOCKER_CONTEXT");
        start.Environment.Remove("DOCKER_TLS_VERIFY");
        start.Environment.Remove("DOCKER_CERT_PATH");
        start.Environment["DOCKER_HOST"] = Endpoint;
        start.ArgumentList.Add("--host"); start.ArgumentList.Add(Endpoint);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return RouteCatalogOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(30), cancellationToken);
    }
}
