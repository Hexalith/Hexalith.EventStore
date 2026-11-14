namespace Hexalith.EventStore.ServiceDefaults.Diagnostics;

/// <summary>Names the closed set of SDK shutdown waits eligible for opt-in observation.</summary>
internal enum ShutdownStage
{
    /// <summary>The activation worker's host stop wait.</summary>
    ProjectionActivationStop,
    /// <summary>The activation execution loop's owned-task drain.</summary>
    ProjectionActivationExecutionDrain,
    /// <summary>An actual lease's remote-call drain during shutdown.</summary>
    ProjectionActivationRemoteDrain,
    /// <summary>The telemetry service's host stop wait.</summary>
    TelemetryStop,
    /// <summary>The existing tracer provider shutdown call.</summary>
    TelemetryTracerCall,
    /// <summary>The existing meter provider shutdown call.</summary>
    TelemetryMeterCall,
    /// <summary>The existing logger provider shutdown call.</summary>
    TelemetryLoggerCall,
    /// <summary>The admin index's host stop operation.</summary>
    AdminIndexStop,
    /// <summary>The admin index's refresh cancellation callbacks.</summary>
    AdminIndexCancelCallbacks,
    /// <summary>The admin index's refresh-task wait.</summary>
    AdminIndexRefreshDrain,
}
