namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// DAPR actor runtime configuration values.
/// </summary>
/// <param name="IdleTimeout">The actor idle timeout before deactivation.</param>
/// <param name="ScanInterval">The interval between actor deactivation scans.</param>
/// <param name="DrainOngoingCallTimeout">The timeout for draining ongoing calls during rebalancing.</param>
/// <param name="DrainRebalancedActors">Whether to drain actors during rebalancing.</param>
/// <param name="ReentrancyEnabled">Whether actor reentrancy is enabled.</param>
/// <param name="ReentrancyMaxStackDepth">The maximum reentrancy stack depth.</param>
public record DaprActorRuntimeConfig(
    TimeSpan IdleTimeout,
    TimeSpan ScanInterval,
    TimeSpan DrainOngoingCallTimeout,
    bool DrainRebalancedActors,
    bool ReentrancyEnabled,
    int ReentrancyMaxStackDepth);
