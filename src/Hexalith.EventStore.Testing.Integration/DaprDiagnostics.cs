using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Domain-agnostic diagnostics helpers shared by the DAPR/Aspire integration-test fixtures: local
/// dependency probing, support-safe message scrubbing, and infrastructure-startup classification.
/// </summary>
/// <remarks>
/// These helpers are extracted from the per-domain integration-test harness so every domain module
/// built on the Hexalith event store reuses the same prerequisite probing and the same support-safe
/// redaction guarantees instead of re-implementing them.
/// </remarks>
public static class DaprDiagnostics {
    /// <summary>Stable classification for a healthy resource whose HTTP endpoint is not ready.</summary>
    public const string AppHostEndpointNotReady = "APPHOST_ENDPOINT_NOT_READY";

    /// <summary>Stable classification for an occupied effective DAPR control-plane binding.</summary>
    public const string DaprControlPlanePortCollision = "DAPR_CONTROL_PLANE_PORT_COLLISION";

    /// <summary>Stable classification when the required DAPR sidecar executable is absent.</summary>
    public const string DaprExecutableNotFound = "DAPR_EXECUTABLE_NOT_FOUND";

    /// <summary>Fails before startup when a selected DAPR executable is not installed.</summary>
    /// <param name="executable">An explicit executable path or command resolved from PATH.</param>
    public static void RequireDaprExecutable(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        bool explicitPath = Path.IsPathRooted(executable)
            || executable.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0;
        bool exists = explicitPath ? File.Exists(Path.GetFullPath(executable))
            : (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator)
                .Any(directory => File.Exists(Path.Combine(directory, executable)));
        if (!exists) throw new FileNotFoundException($"{DaprExecutableNotFound}: install the selected DAPR runtime before retrying.");
    }

    /// <summary>Waits for an HTTP endpoint with bounded retries and support-safe failure details.</summary>
    /// <param name="client">The endpoint's client.</param>
    /// <param name="path">The relative readiness path.</param>
    /// <param name="endpointIdentity">The resource and endpoint name, without tenant or request data.</param>
    /// <param name="timeout">The positive total retry budget.</param>
    /// <param name="retryInterval">The positive delay between attempts.</param>
    /// <param name="cancellationToken">Cancels the wait without reclassifying caller cancellation.</param>
    /// <returns>A task completing only after a successful HTTP response.</returns>
    public static async Task WaitForAppHostEndpointAsync(HttpClient client, string path, string endpointIdentity,
        TimeSpan timeout, TimeSpan retryInterval, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointIdentity);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryInterval, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);
        string lastResult = "no response";
        try
        {
            while (true)
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, budget.Token)
                        .ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) return;
                    // Bodies, exception messages and request URLs may contain credentials or tenant identities.
                    lastResult = $"HTTP {(int)response.StatusCode}";
                }
                catch (HttpRequestException)
                {
                    lastResult = "connection unavailable";
                }
                catch (OperationCanceledException) when (!budget.IsCancellationRequested)
                {
                    lastResult = "request timeout";
                }
                await Task.Delay(retryInterval, budget.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && budget.IsCancellationRequested)
        {
            throw new TimeoutException($"{AppHostEndpointNotReady}: endpoint={ToSupportSafeDiagnostic(endpointIdentity)}; "
                + $"last={lastResult}; retry after checking the endpoint binding, within a {timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)}-second budget.");
        }
    }

    /// <summary>Checks only the effective local control-plane bindings the caller is about to create.</summary>
    /// <param name="effectiveBindings">Resource/endpoint identities and their actual configured TCP bindings.</param>
    /// <param name="cancellationToken">Cancels before probing or reporting another binding.</param>
    /// <remarks>This is an advisory preflight; a later bind race must still be handled by the owner.</remarks>
    public static void RequireAvailableDaprControlPlaneBindings(IReadOnlyDictionary<string, IPEndPoint> effectiveBindings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effectiveBindings);
        cancellationToken.ThrowIfCancellationRequested();
        var probes = new List<Socket>();
        try
        {
            foreach (var binding in effectiveBindings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(binding.Key);
                ArgumentNullException.ThrowIfNull(binding.Value);
                var socket = new Socket(binding.Value.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                probes.Add(socket);
                socket.ExclusiveAddressUse = true;
                try
                {
                    socket.Bind(binding.Value);
                    socket.Listen(1);
                }
                catch (SocketException exception) when (exception.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
                {
                    throw new InvalidOperationException($"{DaprControlPlanePortCollision}: endpoint={ToSupportSafeDiagnostic(binding.Key)}; "
                        + $"port={binding.Value.Port}; release the owning binding or select an isolated port before retrying.");
                }
            }
        }
        finally
        {
            foreach (Socket probe in probes) probe.Dispose();
        }
    }

    /// <summary>The default host port of the local <c>dapr init</c>-managed Redis instance.</summary>
    public const int DefaultRedisPort = 6379;

    private const string RedisPortVariable = "HEXALITH_EVENTSTORE_TEST_REDIS_PORT";
    private const string LegacyRedisPortVariable = "HEXALITH_TENANTS_TEST_REDIS_PORT";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Resolves the host port to probe for the local <c>dapr init</c>-managed Redis instance, honoring
    /// an explicit override and defaulting to <see cref="DefaultRedisPort"/>.
    /// </summary>
    public static int ResolveRedisPort() {
        string? overrideValue = DaprTestEnvironment.GetVariable(RedisPortVariable, LegacyRedisPortVariable);
        if (!string.IsNullOrWhiteSpace(overrideValue)
            && int.TryParse(overrideValue, CultureInfo.InvariantCulture, out int parsed)
            && parsed is > 0 and < 65536) {
            return parsed;
        }

        return DefaultRedisPort;
    }

    /// <summary>
    /// Builds the support-safe skip message naming the missing DAPR dependency categories and ports.
    /// </summary>
    /// <param name="failures">The list of human-readable, support-safe dependency-failure lines.</param>
    public static string BuildPrerequisiteFailureMessage(IReadOnlyList<string> failures) {
        ArgumentNullException.ThrowIfNull(failures);
        return "Dapr infrastructure pre-flight check failed. Have you run 'dapr init'?" + Environment.NewLine
            + string.Join(Environment.NewLine, failures.Select(f => $"  - {f}"));
    }

    /// <summary>
    /// Classifies an <see cref="InvalidOperationException"/> as a DAPR infrastructure-startup failure
    /// (a reason to skip), as opposed to a product/domain failure (a reason to fail the test).
    /// </summary>
    public static bool IsDaprInfrastructureStartupFailure(InvalidOperationException exception) {
        ArgumentNullException.ThrowIfNull(exception);

        // Match infrastructure-startup signatures specifically — broad substrings like
        // "statestore" alone over-match and would silently turn unrelated test failures
        // into prerequisite skips.
        string message = exception.Message;
        return message.Contains("daprd exited", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Dapr sidecar did not become healthy", StringComparison.OrdinalIgnoreCase)
            || message.Contains("state.redis", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("statestore", StringComparison.OrdinalIgnoreCase)
                && message.Contains("init timeout", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Redacts secrets, tokens, connection strings, private addresses, URLs, emails, and concrete
    /// tenant/user identifiers from a diagnostic string so it is safe to surface in support evidence.
    /// </summary>
    public static string ToSupportSafeDiagnostic(string value) {
        ArgumentNullException.ThrowIfNull(value);

        string result = value;
        result = Regex.Replace(
            result,
            @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+",
            "[redacted-jwt]");
        result = Regex.Replace(
            result,
            @"Bearer\s+[A-Za-z0-9._~+/=-]{20,}",
            "Bearer [redacted-token]",
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            @"(dapr[_-]?api[_-]?token\s*[:=]\s*)\S+",
            "$1[redacted-token]",
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            @"(?:AccountKey|SharedAccessKey|Password)\s*=\s*[^;\s\r\n]+",
            "[redacted-secret]",
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            @"(redisPassword[^\r\n:=]*[:=]\s*)([^{}\s\r\n]+)",
            "$1{redacted-secret}",
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            @"(redis://|amqp://|Endpoint=sb://)[^\s\r\n]+",
            "[redacted-connection]",
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            @"(?<!localhost:)(?<!127\.0\.0\.1:)\b(10\.\d{1,3}\.\d{1,3}\.\d{1,3}|172\.(1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3})\b",
            "[redacted-private-address]");
        result = Regex.Replace(
            result,
            @"https?://(?!(?:localhost|127\.0\.0\.1)(?::|/|\b))[^\s\r\n]+",
            "[redacted-url]",
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            "[redacted-email]",
            RegexOptions.IgnoreCase);
        result = Regex.Replace(
            result,
            @"\b(tenantId|tenant|userId|user|sub|subject)\s*[:=]\s*['""]?[A-Za-z0-9._@%+-]{3,}['""]?",
            "$1=[redacted-id]",
            RegexOptions.IgnoreCase);
        return result;
    }

    /// <summary>
    /// Probes a TCP endpoint for reachability within the standard probe timeout.
    /// </summary>
    public static async Task<bool> IsPortReachableAsync(string host, int port) {
        try {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(ProbeTimeout);
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch {
            return false;
        }
    }

    /// <summary>
    /// Probes the local Redis instance for a <c>PING</c>/<c>PONG</c> response within the probe timeout.
    /// </summary>
    public static async Task<bool> IsRedisResponsiveAsync(int redisPort) {
        try {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(ProbeTimeout);
            await client.ConnectAsync("localhost", redisPort, cts.Token).ConfigureAwait(false);
            using NetworkStream stream = client.GetStream();
            byte[] ping = Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n");
            await stream.WriteAsync(ping, cts.Token).ConfigureAwait(false);

            byte[] buffer = new byte[16];
            int total = 0;
            while (total < 5) {
                int chunk = await stream.ReadAsync(buffer.AsMemory(total), cts.Token).ConfigureAwait(false);
                if (chunk <= 0) {
                    break;
                }

                total += chunk;
            }

            return total >= 5 && Encoding.ASCII.GetString(buffer, 0, total).StartsWith("+PONG", StringComparison.Ordinal);
        }
        catch {
            return false;
        }
    }
}
