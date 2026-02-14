namespace Hexalith.EventStore.Server.Telemetry;

using System.Diagnostics;

/// <summary>
/// Provides a single static <see cref="ActivitySource"/> for OpenTelemetry distributed tracing
/// across the EventStore actor pipeline. Following .NET OpenTelemetry conventions, all activities
/// are created from this source and integrate with Aspire dashboard / OTLP collectors.
/// </summary>
public static class EventStoreActivitySource
{
    /// <summary>The source name registered with OpenTelemetry.</summary>
    public const string SourceName = "Hexalith.EventStore";

    /// <summary>Outer span for the full command processing pipeline.</summary>
    public const string ProcessCommand = "EventStore.Actor.ProcessCommand";

    /// <summary>Idempotency check stage.</summary>
    public const string IdempotencyCheck = "EventStore.Actor.IdempotencyCheck";

    /// <summary>Tenant validation stage.</summary>
    public const string TenantValidation = "EventStore.Actor.TenantValidation";

    /// <summary>State rehydration stage.</summary>
    public const string StateRehydration = "EventStore.Actor.StateRehydration";

    /// <summary>Domain service invocation stage.</summary>
    public const string DomainServiceInvoke = "EventStore.DomainService.Invoke";

    /// <summary>Event persistence stage.</summary>
    public const string EventsPersist = "EventStore.Events.Persist";

    /// <summary>State machine transition stage.</summary>
    public const string StateMachineTransition = "EventStore.Actor.StateMachineTransition";

    /// <summary>Tag key for correlation ID.</summary>
    public const string TagCorrelationId = "eventstore.correlation_id";

    /// <summary>Tag key for tenant ID.</summary>
    public const string TagTenantId = "eventstore.tenant_id";

    /// <summary>Tag key for domain.</summary>
    public const string TagDomain = "eventstore.domain";

    /// <summary>Tag key for aggregate ID.</summary>
    public const string TagAggregateId = "eventstore.aggregate_id";

    /// <summary>Tag key for command type.</summary>
    public const string TagCommandType = "eventstore.command_type";

    /// <summary>Gets the singleton <see cref="ActivitySource"/> instance.</summary>
    public static ActivitySource Instance { get; } = new(SourceName);
}
