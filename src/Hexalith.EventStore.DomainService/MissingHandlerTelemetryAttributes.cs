namespace Hexalith.EventStore.DomainService;

/// <summary>Handler types omitted from automatic telemetry registration.</summary>
/// <param name="HandlerTypes">Handler types that lack the domain attribute.</param>
internal sealed record MissingHandlerTelemetryAttributes(IReadOnlyList<Type> HandlerTypes);
