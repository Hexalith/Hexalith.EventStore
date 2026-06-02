using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>A minimal command used to exercise the domain-service SDK in this test assembly.</summary>
public sealed record CreateWidget;

/// <summary>A minimal event used to exercise the domain-service SDK in this test assembly.</summary>
public sealed record WidgetCreated : IEventPayload;

/// <summary>Minimal aggregate state for the local <c>widget</c> test domain.</summary>
public sealed class WidgetState {
    /// <summary>Gets the number of times the widget was created.</summary>
    public int Count { get; private set; }

    /// <summary>Applies a <see cref="WidgetCreated"/> event.</summary>
    /// <param name="event">The event to apply.</param>
    public void Apply(WidgetCreated @event) => Count++;
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
        return Task.FromResult(QueryResult.FromPayload(payload, projectionType: "widget"));
    }
}
