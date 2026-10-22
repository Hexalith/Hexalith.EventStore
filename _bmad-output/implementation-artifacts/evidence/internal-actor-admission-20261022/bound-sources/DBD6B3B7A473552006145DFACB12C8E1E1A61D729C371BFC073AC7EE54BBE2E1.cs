using System.Globalization;
using System.Net.Sockets;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Resolves local DAPR placement and scheduler endpoints for Aspire-managed sidecars.
/// </summary>
public static class AspireDaprLocalServiceEndpoints {
    /// <summary>
    /// Gets the configuration key for the DAPR placement service host address.
    /// </summary>
    public const string PlacementHostAddressKey = "Dapr:PlacementHostAddress";

    /// <summary>
    /// Gets the configuration key for the DAPR scheduler service host address.
    /// </summary>
    public const string SchedulerHostAddressKey = "Dapr:SchedulerHostAddress";

    private static readonly int[] PlacementCandidatePorts = [6050, 50005];
    private static readonly int[] SchedulerCandidatePorts = [6060, 50006];
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Resolves configured or reachable local DAPR placement and scheduler host addresses.
    /// </summary>
    /// <param name="placementHostAddress">The configured placement service host address.</param>
    /// <param name="schedulerHostAddress">The configured scheduler service host address.</param>
    /// <returns>The resolved placement and scheduler service host addresses.</returns>
    public static (string? PlacementHostAddress, string? SchedulerHostAddress) Resolve(
        string? placementHostAddress,
        string? schedulerHostAddress)
        => Resolve(placementHostAddress, schedulerHostAddress, IsPortReachable);

    internal static (string? PlacementHostAddress, string? SchedulerHostAddress) Resolve(
        string? placementHostAddress,
        string? schedulerHostAddress,
        Func<int, bool> isPortReachable) {
        ArgumentNullException.ThrowIfNull(isPortReachable);

        return (
            ResolveHostAddress(placementHostAddress, PlacementCandidatePorts, isPortReachable),
            ResolveHostAddress(schedulerHostAddress, SchedulerCandidatePorts, isPortReachable));
    }

    private static string? ResolveHostAddress(
        string? configuredHostAddress,
        int[] candidatePorts,
        Func<int, bool> isPortReachable) {
        string trimmed = configuredHostAddress?.Trim() ?? string.Empty;
        if (trimmed.Length > 0) {
            return trimmed;
        }

        foreach (int candidatePort in candidatePorts) {
            if (isPortReachable(candidatePort)) {
                return "localhost:" + candidatePort.ToString(CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static bool IsPortReachable(int port) {
        try {
            using CancellationTokenSource timeout = new(ProbeTimeout);
            using TcpClient client = new();
            client.ConnectAsync("localhost", port, timeout.Token).GetAwaiter().GetResult();
            return client.Connected;
        }
        catch (OperationCanceledException) {
            return false;
        }
        catch (SocketException) {
            return false;
        }
        catch (ObjectDisposedException) {
            return false;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }
}
