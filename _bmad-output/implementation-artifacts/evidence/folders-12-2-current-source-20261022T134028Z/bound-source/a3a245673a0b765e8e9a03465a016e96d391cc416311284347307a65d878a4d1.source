using System.Globalization;
using System.Net.Sockets;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Resolves the host ports of the local DAPR placement and scheduler services started by <c>dapr init</c>.
/// </summary>
/// <remarks>
/// <para>
/// Modern containerized <c>dapr init</c> (Dapr 1.15+) publishes the placement service on host port
/// <c>6050</c> and the scheduler service on host port <c>6060</c> — mapped to the container-internal
/// gRPC ports <c>50005</c>/<c>50006</c> — on <strong>every</strong> platform, including Linux and WSL2.
/// Legacy slim-mode <c>dapr init</c> instead runs them as native processes directly on
/// <c>50005</c>/<c>50006</c>.
/// </para>
/// <para>
/// The previous OS-based guess (<c>OperatingSystem.IsWindows() ? 6050 : 50005</c>) misclassified
/// Linux/WSL2 hosts running the Docker-based <c>dapr init</c>, so the fixtures probed the unpublished
/// container-internal port and skipped every DAPR integration test. Instead of guessing from the OS,
/// probe the documented host ports and use whichever responds, so the same fixtures work under
/// Docker-based init, slim init, Windows, Linux, and WSL2. Each port can be overridden explicitly via
/// an environment variable for non-standard setups.
/// </para>
/// </remarks>
public static class DaprLocalEndpoints {
    /// <summary>Environment variable that overrides the resolved placement host port.</summary>
    public const string PlacementPortVariable = "HEXALITH_EVENTSTORE_TEST_PLACEMENT_PORT";

    /// <summary>Environment variable that overrides the resolved scheduler host port.</summary>
    public const string SchedulerPortVariable = "HEXALITH_EVENTSTORE_TEST_SCHEDULER_PORT";

    // Candidate host ports in preference order: containerized `dapr init` (6050/6060) first, then the
    // legacy slim-mode native ports (50005/50006).
    private static readonly int[] PlacementCandidatePorts = [6050, 50005];
    private static readonly int[] SchedulerCandidatePorts = [6060, 50006];
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private static readonly Lazy<int> s_placementPort = new(() => ResolvePort(PlacementPortVariable, PlacementCandidatePorts));
    private static readonly Lazy<int> s_schedulerPort = new(() => ResolvePort(SchedulerPortVariable, SchedulerCandidatePorts));

    /// <summary>
    /// Gets the resolved host port of the local DAPR placement service.
    /// </summary>
    public static int PlacementPort => s_placementPort.Value;

    /// <summary>
    /// Gets the resolved host port of the local DAPR scheduler service.
    /// </summary>
    public static int SchedulerPort => s_schedulerPort.Value;

    private static int ResolvePort(string overrideVariable, int[] candidatePorts) {
        string? overrideValue = Environment.GetEnvironmentVariable(overrideVariable);
        if (!string.IsNullOrWhiteSpace(overrideValue)
            && int.TryParse(overrideValue, CultureInfo.InvariantCulture, out int parsed)
            && parsed is > 0 and < 65536) {
            return parsed;
        }

        foreach (int candidate in candidatePorts) {
            if (IsPortReachable(candidate)) {
                return candidate;
            }
        }

        // None reachable: return the preferred (modern) default so diagnostics report the expected
        // port. The caller's prerequisite probe will still find it closed and skip the test.
        return candidatePorts[0];
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
