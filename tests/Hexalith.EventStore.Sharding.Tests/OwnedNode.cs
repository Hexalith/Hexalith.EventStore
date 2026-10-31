using System.Diagnostics;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Exact fixture-owned application and sidecar process handles.</summary>
internal sealed class OwnedNode
{
    /// <summary>Gets or sets the loopback application port.</summary>
    internal int AppPort { get; set; }

    /// <summary>Gets or sets the loopback Dapr HTTP port.</summary>
    internal int HttpPort { get; set; }

    /// <summary>Gets or sets the loopback Dapr gRPC port.</summary>
    internal int GrpcPort { get; set; }

    /// <summary>Gets or sets the invocation-distinct internal Dapr port.</summary>
    internal int InternalPort { get; set; }

    /// <summary>Gets or sets the invocation-distinct metrics port.</summary>
    internal int MetricsPort { get; set; }

    /// <summary>Gets or sets the invocation-distinct profiling port.</summary>
    internal int ProfilePort { get; set; }

    /// <summary>Gets or sets the exact application process.</summary>
    internal Process? Application { get; set; }

    /// <summary>Gets or sets the exact sidecar process.</summary>
    internal Process? Sidecar { get; set; }
}
