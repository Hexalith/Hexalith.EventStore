using System.Text.Json;

using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>A minimal command used to exercise the domain-service SDK in this test assembly.</summary>
public sealed record CreateWidget;

/// <summary>A second minimal command used to exercise multi-domain discovery.</summary>
public sealed record CreateGadget;

/// <summary>A minimal event used to exercise the domain-service SDK in this test assembly.</summary>
public sealed record WidgetCreated : IEventPayload;

/// <summary>A second minimal event used to exercise multi-domain discovery.</summary>
public sealed record GadgetCreated : IEventPayload;

/// <summary>A minimal rejection event used to exercise the domain-service admission hook.</summary>
public sealed record WidgetRejected(string Reason) : IRejectionEvent;

/// <summary>Minimal aggregate state for the local <c>widget</c> test domain.</summary>
public sealed class WidgetState {
    /// <summary>Gets the number of times the widget was created.</summary>
    public int Count { get; private set; }

    /// <summary>Applies a <see cref="WidgetCreated"/> event.</summary>
    /// <param name="event">The event to apply.</param>
    public void Apply(WidgetCreated @event) => Count++;
}

/// <summary>Minimal aggregate state for the local <c>gadget</c> test domain.</summary>
public sealed class GadgetState {
    /// <summary>Gets the number of times the gadget was created.</summary>
    public int Count { get; private set; }

    /// <summary>Applies a <see cref="GadgetCreated"/> event.</summary>
    /// <param name="event">The event to apply.</param>
    public void Apply(GadgetCreated @event) => Count++;
}

/// <summary>
/// A minimal aggregate that lives in the test assembly so discovery of the calling assembly can be
/// verified. The convention derives the domain name <c>widget</c> from the <c>WidgetAggregate</c> class name.
/// </summary>
public sealed class WidgetAggregate : EventStoreAggregate<WidgetState> {
    /// <summary>Handles <see cref="CreateWidget"/> by emitting a <see cref="WidgetCreated"/> event.</summary>
    /// <param name="command">The command.</param>
    /// <param name="state">The current state, or <c>null</c> for a new aggregate.</param>
    /// <returns>A successful domain result with one event.</returns>
    public static DomainResult Handle(CreateWidget command, WidgetState? state)
        => DomainResult.Success(new IEventPayload[] { new WidgetCreated() });
}

/// <summary>
/// A second aggregate in the test assembly, used to verify multi-domain host diagnostics.
/// </summary>
public sealed class GadgetAggregate : EventStoreAggregate<GadgetState> {
    /// <summary>Handles <see cref="CreateGadget"/> by emitting a <see cref="GadgetCreated"/> event.</summary>
    /// <param name="command">The command.</param>
    /// <param name="state">The current state, or <c>null</c> for a new aggregate.</param>
    /// <returns>A successful domain result with one event.</returns>
    public static DomainResult Handle(CreateGadget command, GadgetState? state)
        => DomainResult.Success(new IEventPayload[] { new GadgetCreated() });
}

/// <summary>
/// A minimal query handler in the test assembly, used to verify discovery, registration, and dispatch of
/// <see cref="IDomainQueryHandler"/> by the SDK.
/// </summary>
public sealed class WidgetQueryHandler : IDomainQueryHandler {
    /// <inheritdoc/>
    public string Domain => "widget";

    /// <inheritdoc/>
    public string QueryType => "get-widget";

    /// <inheritdoc/>
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(query);
        JsonElement payload = JsonSerializer.SerializeToElement(new { domain = query.Domain, aggregateId = query.AggregateId });
        var metadata = new QueryResponseMetadata(
            IsStale: false,
            ProjectionVersion: "widget-projection-v1",
            Paging: new QueryPagingMetadata(PageSize: 10, Offset: 0));
        return Task.FromResult(QueryResult.FromPayload(payload, projectionType: "widget", metadata));
    }
}

/// <summary>A query handler-only domain used to verify diagnostics registration without an aggregate.</summary>
[EventStoreDomain("catalog")]
public sealed class CatalogQueryHandler : IDomainQueryHandler {
    /// <inheritdoc/>
    public string Domain => "catalog";

    /// <inheritdoc/>
    public string QueryType => "list-catalog";

    /// <inheritdoc/>
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
        => Task.FromResult(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { ok = true })));
}

/// <summary>
/// A minimal full-replay projection handler in the test assembly, used to verify discovery, registration, and
/// dispatch of <see cref="IDomainProjectionHandler"/> by the SDK (Epic A3). It counts the events in the
/// request and returns the total as the projection state.
/// </summary>
public sealed class WidgetProjection : IDomainProjectionHandler {
    /// <inheritdoc/>
    public string Domain => "widget";

    /// <inheritdoc/>
    public ProjectionResponse Project(ProjectionRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        JsonElement state = JsonSerializer.SerializeToElement(new { count = request.Events?.Length ?? 0 });
        return new ProjectionResponse("widget", state);
    }
}

/// <summary>A test processor that records invocation order for admission-hook tests.</summary>
public sealed class RecordingWidgetProcessor(IList<string> calls) : IDomainProcessor {
    private readonly IList<string> _calls = calls;

    /// <summary>Gets the number of processor invocations.</summary>
    public int InvocationCount { get; private set; }

    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState) {
        ArgumentNullException.ThrowIfNull(command);
        InvocationCount++;
        _calls.Add("processor");
        return Task.FromResult(DomainResult.Success(new IEventPayload[] { new WidgetCreated() }));
    }
}

/// <summary>A test admission stage with configurable accept/reject behavior.</summary>
public sealed class RecordingAdmissionStage(
    string name,
    IList<string> calls,
    bool accept = true) : IDomainServiceAdmissionStage {
    private readonly bool _accept = accept;
    private readonly IList<string> _calls = calls;

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public Task<DomainServiceAdmissionResult> EvaluateAsync(
        DomainServiceAdmissionContext context,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        _calls.Add(Name);
        DomainServiceAdmissionResult result = _accept
            ? DomainServiceAdmissionResult.Accepted()
            : DomainServiceAdmissionResult.Rejected([new WidgetRejected("blocked")]);
        return Task.FromResult(result);
    }
}

/// <summary>A first scoped admission stage used to prove generic DI registration order.</summary>
public sealed class FirstRegisteredAdmissionStage(IList<string> calls) : IDomainServiceAdmissionStage {
    private readonly IList<string> _calls = calls;

    /// <inheritdoc/>
    public string Name => "first";

    /// <inheritdoc/>
    public Task<DomainServiceAdmissionResult> EvaluateAsync(
        DomainServiceAdmissionContext context,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        _calls.Add(Name);
        return Task.FromResult(DomainServiceAdmissionResult.Accepted());
    }
}

/// <summary>A second scoped admission stage used to prove generic DI registration order.</summary>
public sealed class SecondRegisteredAdmissionStage(IList<string> calls) : IDomainServiceAdmissionStage {
    private readonly IList<string> _calls = calls;

    /// <inheritdoc/>
    public string Name => "second";

    /// <inheritdoc/>
    public Task<DomainServiceAdmissionResult> EvaluateAsync(
        DomainServiceAdmissionContext context,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        _calls.Add(Name);
        return Task.FromResult(DomainServiceAdmissionResult.Accepted());
    }
}

/// <summary>A test admission stage that honors cancellation before accepting a command.</summary>
public sealed class CancellationAwareAdmissionStage(
    string name,
    IList<string> calls) : IDomainServiceAdmissionStage {
    private readonly IList<string> _calls = calls;

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public Task<DomainServiceAdmissionResult> EvaluateAsync(
        DomainServiceAdmissionContext context,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        _calls.Add(Name);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DomainServiceAdmissionResult.Accepted());
    }
}
