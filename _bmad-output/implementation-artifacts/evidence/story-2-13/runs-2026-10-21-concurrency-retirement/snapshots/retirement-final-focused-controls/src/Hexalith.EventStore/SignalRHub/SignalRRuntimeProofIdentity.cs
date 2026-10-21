namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Runtime identity emitted by Development-only SignalR proof endpoints.
/// </summary>
/// <param name="InstanceName">Configured proof instance name.</param>
/// <param name="ProcessId">Operating system process id.</param>
/// <param name="MachineName">Machine name.</param>
/// <param name="EnvironmentName">ASP.NET Core environment name.</param>
/// <param name="SignalREnabled">Whether SignalR is enabled in runtime configuration.</param>
/// <param name="BackplaneRedisConnectionString">Effective Redis backplane connection string.</param>
/// <param name="Timestamp">UTC timestamp when the identity was captured.</param>
public sealed record SignalRRuntimeProofIdentity(
    string InstanceName,
    int ProcessId,
    string MachineName,
    string EnvironmentName,
    bool SignalREnabled,
    string? BackplaneRedisConnectionString,
    DateTimeOffset Timestamp);
