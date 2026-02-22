
using System.Diagnostics;

namespace Hexalith.EventStore.Server.Telemetry;
/// <summary>
/// Provides a single static <see cref="ActivitySource"/> for OpenTelemetry distributed tracing
/// across the EventStore actor pipeline. Following .NET OpenTelemetry conventions, all activities
/// are created from this source and integrate with Aspire dashboard / OTLP collectors.
/// </summary>
public static class EventStoreActivitySource {
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

    /// <summary>Event publication stage (Story 4.1).</summary>
    public const string EventsPublish = "EventStore.Events.Publish";

    /// <summary>Event drain recovery stage (Story 4.4).</summary>
    public const string EventsDrain = "EventStore.Events.Drain";

    /// <summary>Dead-letter publication stage (Story 4.5).</summary>
    public const string EventsPublishDeadLetter = "EventStore.Events.PublishDeadLetter";

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

    /// <summary>Tag key for event count.</summary>
    public const string TagEventCount = "eventstore.event_count";

    /// <summary>Tag key for pub/sub topic.</summary>
    public const string TagTopic = "eventstore.topic";

    /// <summary>Tag key for exception type (Story 4.5).</summary>
    public const string TagExceptionType = "eventstore.exception_type";

    /// <summary>Tag key for failure stage (Story 4.5).</summary>
    public const string TagFailureStage = "eventstore.failure_stage";

    /// <summary>Tag key for dead-letter topic (Story 4.5).</summary>
    public const string TagDeadLetterTopic = "eventstore.deadletter_topic";

    /// <summary>Gets the singleton <see cref="ActivitySource"/> instance.</summary>
    public static ActivitySource Instance { get; } = new(SourceName);
}
