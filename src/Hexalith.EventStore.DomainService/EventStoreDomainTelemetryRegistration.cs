namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Tracks idempotent domain telemetry registration.
/// </summary>
/// <param name="Domain">The registered domain name.</param>
internal sealed record EventStoreDomainTelemetryRegistration(string Domain);
