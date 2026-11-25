using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Owns one labelled, disposable container and awaits its removal.</summary>
internal sealed class OwnedAuthenticationContainer : IAsyncDisposable
{
    private const string OwnerLabel = "hexalith.eventstore.authentication-test-owner";
    private readonly string _owner = Environment.GetEnvironmentVariable("HEXALITH_PRIVATE_AUTH_OWNER") ?? Guid.NewGuid().ToString("N");
    private readonly string _name = $"eventstore-auth-{Guid.NewGuid():N}";
    private readonly Func<string[], CancellationToken, Task<string>> _docker;
    private string? _identity;
    private bool _creationAttempted;

    /// <summary>Uses the real Docker CLI for owned infrastructure.</summary>
    internal OwnedAuthenticationContainer() : this(DockerAsync) { }

    /// <summary>Allows focused ownership-race controls to supply the Docker boundary.</summary>
    internal OwnedAuthenticationContainer(Func<string[], CancellationToken, Task<string>> docker) => _docker = docker;

    /// <summary>Gets the container's private loopback endpoint.</summary>
    internal string Endpoint { get; private set; } = string.Empty;

    /// <summary>Gets the invocation ownership label shared with managed resources.</summary>
    internal string Owner => _owner;

    /// <summary>Starts one private container, retaining cleanup ownership on failure.</summary>
    internal async Task StartAsync(string image, string? entrypoint, int port, string[] arguments,
        CancellationToken cancellationToken, string[]? runtimeArguments = null)
    {
        _creationAttempted = true;
        string[] command = ["run", "--detach", "--rm", "--name", _name,
            "--label", $"{OwnerLabel}={_owner}", "--publish", $"127.0.0.1:{port}:{port}"];
        if (entrypoint is not null)
        {
            command = [.. command, "--entrypoint", entrypoint];
        }
        _identity = (await _docker([.. command, .. (runtimeArguments ?? []), image, .. arguments], cancellationToken).ConfigureAwait(false)).Trim();
        if (_identity.Length != 64 || !_identity.All(char.IsAsciiHexDigitLower))
        {
            _identity = null;
            throw new InvalidOperationException("The private authentication container identity is unavailable.");
        }
        Endpoint = $"127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Reserves a candidate ephemeral port; Docker enforces exclusive binding.</summary>
    internal static int CandidatePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_creationAttempted) return;
        string inspection;
        try
        {
            inspection = await _docker(["inspect", "--format", "{\"id\":{{json .Id}},\"owner\":{{json (index .Config.Labels \"" + OwnerLabel + "\")}}}", _identity ?? _name], CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (IsMissing(ex))
        {
            _creationAttempted = false;
            return;
        }
        using JsonDocument snapshot = JsonDocument.Parse(inspection);
        string? identity = snapshot.RootElement.GetProperty("id").GetString();
        string? owner = snapshot.RootElement.GetProperty("owner").GetString();
        if (identity is not { Length: 64 } || !identity.All(char.IsAsciiHexDigitLower)
            || owner != _owner || (_identity is not null && identity != _identity))
            throw new InvalidOperationException("Private authentication container ownership changed; refusing removal.");
        _identity = identity;
        try
        {
            _ = await _docker(["rm", "--force", _identity], CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (IsMissing(ex)) { }
        _creationAttempted = false;
    }

    private static bool IsMissing(InvalidOperationException exception)
        => exception.Message.Contains("No such object", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("No such container", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> DockerAsync(string[] arguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start Docker for private authentication infrastructure.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            _ = await Task.WhenAll(output, error).ConfigureAwait(false);
            throw;
        }
        string stdout = await output.ConfigureAwait(false);
        string stderr = await error.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Private authentication Docker operation {arguments[0]} failed: {stderr.Trim()}");
        return stdout.Trim();
    }
}
