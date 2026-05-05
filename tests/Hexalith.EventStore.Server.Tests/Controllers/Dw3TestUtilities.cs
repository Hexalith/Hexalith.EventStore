using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// Shared helpers for DW3 ATDD red-phase scaffolds. Mirrors the established
/// pattern in <see cref="AdminStreamQueryControllerTimelineTests"/> while
/// adding payload-byte overloads needed for malformed/array/non-object
/// fixtures that exercise <c>DeepMerge</c>, <c>JsonDiff</c>, <c>FlattenJson</c>,
/// and <c>ReconstructState</c> through the public endpoint surface.
/// </summary>
internal static class Dw3TestUtilities {
    public const string TenantId = "tenant-a";
    public const string Domain = "counter";
    public const string AggregateId = "counter-1";

    /// <summary>
    /// DW3 decision-ledger seed values for the <c>GetEventsAsync(0)</c> disposition
    /// per debugging surface (AC #7). Dev may revise during implementation; tests
    /// pin the expected disposition vocabulary so reviewers can detect drift.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Dw3GetEventsAsyncDispositionMatrix
        = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["timeline"] = "bounded-range-read",
            ["blame"] = "preserve-legacy",
            ["bisect"] = "preserve-legacy",
            ["step"] = "preserve-legacy",
            ["sandbox"] = "preserve-legacy",
            ["trace-map"] = "preserve-legacy",
        };

    /// <summary>
    /// Allowed direct-CommandApi over-limit reason codes (AC #5). Vocabulary must
    /// match the regex <c>^[a-z][a-z0-9_]*$</c> and length less than 64 characters.
    /// </summary>
    public static readonly IReadOnlySet<string> Dw3DirectBoundReasonCodes
        = new HashSet<string>(StringComparer.Ordinal) {
            "count_above_limit",
            "max_events_above_limit",
            "max_fields_above_limit",
            "max_steps_above_limit",
        };

    /// <summary>
    /// Trace-map partial-coverage reason code (AC #8).
    /// </summary>
    public const string TraceScanCapReasonCode = "trace_scan_cap_reached";

    /// <summary>
    /// JSON behavior-matrix dispositions (AC #2, #3, #4).
    /// </summary>
    public static readonly IReadOnlySet<string> Dw3JsonBehaviorDispositions
        = new HashSet<string>(StringComparer.Ordinal) {
            "supported",
            "preserved-limitation",
            "accepted-debt",
            "future-actor-api",
        };

    public static ServerEventEnvelope BuildEnvelope(
        long seq,
        string payloadJson = "{}",
        string corrId = "corr-1",
        string typeName = "CounterIncremented",
        string? userId = "user-1") {
        byte[] payload = string.IsNullOrEmpty(payloadJson)
            ? []
            : Encoding.UTF8.GetBytes(payloadJson);
        return BuildEnvelope(seq, payload, corrId, typeName, userId);
    }

    public static ServerEventEnvelope BuildEnvelope(
        long seq,
        byte[] payloadBytes,
        string corrId = "corr-1",
        string typeName = "CounterIncremented",
        string? userId = "user-1")
        => new(
            MessageId: $"msg-{seq}",
            AggregateId: AggregateId,
            AggregateType: "Counter",
            TenantId: TenantId,
            Domain: Domain,
            SequenceNumber: seq,
            GlobalPosition: seq,
            Timestamp: new DateTimeOffset(2026, 04, 19, 12, 0, 0, TimeSpan.Zero).AddSeconds(seq),
            CorrelationId: corrId,
            CausationId: $"cause-{seq}",
            UserId: userId ?? string.Empty,
            DomainServiceVersion: "1.0.0",
            EventTypeName: typeName,
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: payloadBytes,
            Extensions: null);

    public static AdminStreamQueryController CreateStreamController(IAggregateActor actor) {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory
            .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>())
            .Returns(actor);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        return new AdminStreamQueryController(
            actorProxyFactory,
            invoker,
            NullLogger<AdminStreamQueryController>.Instance);
    }

    public static AdminTraceQueryController CreateTraceController(
        IAggregateActor actor,
        ICommandStatusStore commandStatusStore,
        IConfiguration? configuration = null) {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory
            .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>())
            .Returns(actor);
        IConfiguration config = configuration ?? new ConfigurationBuilder().Build();
        return new AdminTraceQueryController(
            commandStatusStore,
            actorProxyFactory,
            config,
            NullLogger<AdminTraceQueryController>.Instance);
    }
}
