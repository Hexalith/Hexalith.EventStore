using System.Net.Sockets;
using System.Text;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Discovery-time prerequisite check for DAPR-backed integration tests. Probes the local DAPR runtime
/// dependencies (Redis, placement, scheduler) started by <c>dapr init</c> so tests can skip cleanly
/// when the runtime is not reachable rather than failing with opaque sidecar errors.
/// </summary>
public static class DaprTestPrerequisites {
    private static readonly int PlacementPort = DaprLocalEndpoints.PlacementPort;
    private static readonly int SchedulerPort = DaprLocalEndpoints.SchedulerPort;
    private const int RedisPort = 6379;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private static readonly Lazy<bool> s_isAvailable = new(CheckAvailability);

    /// <summary>
    /// Gets a value indicating whether local DAPR runtime dependencies are reachable.
    /// </summary>
    public static bool IsAvailable => s_isAvailable.Value;

    /// <summary>
    /// Gets the skip reason used when DAPR runtime dependencies are not reachable.
    /// </summary>
    public static string SkipReason
        => "DAPR integration prerequisites are unavailable. Run 'dapr init' and ensure Redis, placement, and scheduler are reachable.";

    private static bool CheckAvailability()
        => IsRedisResponsive()
            && IsPortReachable(PlacementPort)
            && IsPortReachable(SchedulerPort);

    private static bool IsRedisResponsive() {
        try {
            using var client = new TcpClient();
            Task connect = client.ConnectAsync("localhost", RedisPort);
            if (!connect.Wait(ProbeTimeout) || !client.Connected) {
                return false;
            }

            client.SendTimeout = (int)ProbeTimeout.TotalMilliseconds;
            client.ReceiveTimeout = (int)ProbeTimeout.TotalMilliseconds;

            using NetworkStream stream = client.GetStream();
            byte[] ping = Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n");
            stream.Write(ping);

            Span<byte> buffer = stackalloc byte[16];
            int total = 0;
            while (total < 5) {
                int chunk = stream.Read(buffer[total..]);
                if (chunk <= 0) {
                    break;
                }

                total += chunk;
            }

            return total >= 5 && Encoding.ASCII.GetString(buffer[..total]).StartsWith("+PONG", StringComparison.Ordinal);
        }
        catch {
            return false;
        }
    }

    private static bool IsPortReachable(int port) {
        try {
            using var client = new TcpClient();
            Task connect = client.ConnectAsync("localhost", port);
            return connect.Wait(ProbeTimeout) && client.Connected;
        }
        catch {
            return false;
        }
    }
}

/// <summary>
/// Discovery-time prerequisite check for DAPR-backed performance tests. Performance tests are opt-in
/// (they are slow and resource-intensive) and additionally require the standard DAPR prerequisites.
/// </summary>
public static class DaprPerformanceTestPrerequisites {
    /// <summary>Environment variable that enables DAPR-backed performance tests when set to <c>1</c>.</summary>
    public const string EnablePerformanceTestsVariable = "HEXALITH_EVENTSTORE_RUN_PERFORMANCE_TESTS";

    /// <summary>
    /// Gets a value indicating whether DAPR-backed performance tests should run.
    /// </summary>
    public static bool IsAvailable
        => Environment.GetEnvironmentVariable(EnablePerformanceTestsVariable) == "1"
            && DaprTestPrerequisites.IsAvailable;

    /// <summary>
    /// Gets the skip reason used when performance tests are not enabled.
    /// </summary>
    public static string SkipReason
        => $"DAPR performance tests are disabled. Set {EnablePerformanceTestsVariable}=1 and ensure DAPR prerequisites are reachable to run them.";
}
