using System.Diagnostics;
using System.Text.Json;

namespace Hexalith.EventStore.Aspire;

/// <summary>Pins the effective local Docker endpoint and removes only exact labeled extraction containers.</summary>
/// <param name="endpoint">The captured local Unix Docker endpoint.</param>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
internal sealed class DevelopmentRouteCatalogTransportDocker(string endpoint)
{
    /// <summary>Captures the effective local endpoint without changing Docker configuration.</summary>
    /// <param name="cancellationToken">Bounds the initial read-only inspection.</param>
    internal static async Task<DevelopmentRouteCatalogTransportDocker> CaptureAsync(CancellationToken cancellationToken)
    {
        string? context = Environment.GetEnvironmentVariable("DOCKER_CONTEXT"), host = Environment.GetEnvironmentVariable("DOCKER_HOST");
        string endpoint;
        if (!string.IsNullOrEmpty(context) || string.IsNullOrEmpty(host))
        {
            var start = new ProcessStartInfo("docker");
            start.Environment.Remove("DOCKER_HOST"); start.Environment.Remove("DOCKER_CONTEXT");
            start.ArgumentList.Add("context"); start.ArgumentList.Add("inspect");
            if (!string.IsNullOrEmpty(context)) start.ArgumentList.Add(context);
            using var document = JsonDocument.Parse(await RunAsync(start, cancellationToken).ConfigureAwait(false));
            endpoint = document.RootElement[0].GetProperty("Endpoints").GetProperty("docker").GetProperty("Host").GetString()!;
            if (!string.IsNullOrEmpty(context) && !string.IsNullOrEmpty(host) && host != endpoint)
                throw new InvalidOperationException("Conflicting local Docker selections refuse Development transport.");
        }
        else endpoint = host;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var address) || address.Scheme != "unix" || address.Host.Length != 0)
            throw new InvalidOperationException("Development transport requires an explicitly local Unix Docker endpoint.");
        return new(endpoint);
    }

    /// <summary>Extracts one pinned native executable with durable exact-container intent and cleanup receipts.</summary>
    /// <param name="image">The immutable existing image reference.</param>
    /// <param name="name">The selected native service executable name.</param>
    /// <param name="directory">The private directory owned by this transport.</param>
    /// <param name="cancellationToken">Cancels startup without skipping exact extraction cleanup.</param>
    internal async Task ExtractAsync(string image, string name, string directory, CancellationToken cancellationToken)
    {
        string owner = "eventstore-transport-" + Guid.NewGuid().ToString("N");
        string? identity = null;
        await WriteReceiptAsync(Path.Combine(directory, "extraction-" + name + ".json"), new { owner, image }, cancellationToken).ConfigureAwait(false);
        try
        {
            identity = (await DockerAsync(["create", "--pull=never", "--name", owner, "--label", "hexalith.eventstore.transport=" + owner, image, "/" + name], cancellationToken).ConfigureAwait(false)).Trim();
            if (identity.Length != 64 || identity.Any(character => !char.IsAsciiHexDigit(character)))
                throw new InvalidOperationException("Owned transport extraction identity is unavailable.");
            await DockerAsync(["cp", identity + ":/" + name, Path.Combine(directory, name)], cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Even cancellation must resolve the recorded create intent. Never remove a name without its owner label.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var document = JsonDocument.Parse(await DockerAsync(["inspect", owner], cleanup.Token).ConfigureAwait(false));
            var container = document.RootElement[0];
            if ((identity is not null && container.GetProperty("Id").GetString() != identity)
                || container.GetProperty("Config").GetProperty("Labels").GetProperty("hexalith.eventstore.transport").GetString() != owner)
                throw new InvalidOperationException("Owned transport extraction custody is incomplete; exact cleanup refused.");
            await DockerAsync(["rm", "--force", container.GetProperty("Id").GetString()!], cleanup.Token).ConfigureAwait(false);
            await WriteReceiptAsync(Path.Combine(directory, "extraction-" + name + ".cleanup.json"),
                new { owner, image, id = container.GetProperty("Id").GetString(), removed = true }, cleanup.Token).ConfigureAwait(false);
        }
        File.SetUnixFileMode(Path.Combine(directory, name), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private Task<string> DockerAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("docker");
        start.Environment.Remove("DOCKER_CONTEXT"); start.Environment.Remove("DOCKER_TLS_VERIFY"); start.Environment.Remove("DOCKER_CERT_PATH");
        start.Environment["DOCKER_HOST"] = endpoint;
        start.ArgumentList.Add("--host"); start.ArgumentList.Add(endpoint);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return RunAsync(start, cancellationToken);
    }

    private static async Task WriteReceiptAsync(string path, object receipt, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew,
            Access = FileAccess.Write, Share = FileShare.None, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
        await stream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(receipt), cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static async Task<string> RunAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
        start.UseShellExecute = false; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned transport tooling could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), errors = process.StandardError.ReadToEndAsync();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            string result = await output.WaitAsync(deadline.Token).ConfigureAwait(false);
            await errors.WaitAsync(deadline.Token).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException("Owned Development transport tooling failed; no global fallback was attempted.");
            return result;
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            await Task.WhenAll(output, errors).WaitAsync(cleanup.Token).ConfigureAwait(false);
            throw;
        }
    }
}
