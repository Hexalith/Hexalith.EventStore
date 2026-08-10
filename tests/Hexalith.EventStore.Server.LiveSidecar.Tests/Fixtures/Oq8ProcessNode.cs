using System.Diagnostics;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Tracks one independently launched application process and its Dapr sidecar.</summary>
internal sealed class Oq8ProcessNode
{
    /// <summary>Gets or sets the stable diagnostic name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the Dapr application identifier.</summary>
    public required string AppId { get; init; }

    /// <summary>Gets or sets a value indicating whether this is an EventStore node.</summary>
    public bool IsEventStore { get; init; }

    /// <summary>Gets or sets the application HTTP port.</summary>
    public int AppPort { get; set; }

    /// <summary>Gets or sets the Dapr HTTP port.</summary>
    public int DaprHttpPort { get; set; }

    /// <summary>Gets or sets the Dapr gRPC port.</summary>
    public int DaprGrpcPort { get; set; }

    /// <summary>Gets or sets the Dapr internal gRPC port.</summary>
    public int DaprInternalGrpcPort { get; set; }

    /// <summary>Gets or sets the Dapr metrics port.</summary>
    public int DaprMetricsPort { get; set; }

    /// <summary>Gets or sets the Dapr profiling port.</summary>
    public int DaprProfilePort { get; set; }

    /// <summary>Gets or sets the shadow application directory.</summary>
    public string ApplicationDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the application assembly path.</summary>
    public string ApplicationAssembly { get; set; } = string.Empty;

    /// <summary>Gets or sets the boundary-counter file.</summary>
    public string CounterFile { get; set; } = string.Empty;

    /// <summary>Gets or sets the application process.</summary>
    public Process? Application { get; set; }

    /// <summary>Gets or sets the Dapr sidecar process.</summary>
    public Process? Sidecar { get; set; }

    /// <summary>Gets application standard output.</summary>
    public Oq8BoundedLog ApplicationOutput { get; } = new();

    /// <summary>Gets application standard error.</summary>
    public Oq8BoundedLog ApplicationError { get; } = new();

    /// <summary>Gets sidecar standard output.</summary>
    public Oq8BoundedLog SidecarOutput { get; } = new();

    /// <summary>Gets sidecar standard error.</summary>
    public Oq8BoundedLog SidecarError { get; } = new();

    /// <summary>Gets the direct application endpoint.</summary>
    public string ApplicationEndpoint => $"http://127.0.0.1:{AppPort}";

    /// <summary>Gets the Dapr HTTP endpoint.</summary>
    public string DaprHttpEndpoint => $"http://127.0.0.1:{DaprHttpPort}";
}
