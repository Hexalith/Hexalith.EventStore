namespace Hexalith.EventStore.Testing.CatalogInvocation;

/// <summary>An actual private HTTP response, or a transport failure with no invented HTTP status.</summary>
/// <param name="Id">The matching one-shot request identity.</param>
/// <param name="StatusCode">The actual HTTP status, absent on SDK/transport failure.</param>
/// <param name="Body">The exact bounded response bytes.</param>
/// <param name="Failure">A sanitized failure category, never exception text.</param>
/// <param name="PrivateDiagnostic">Optional bounded private failure details; never copied into caller diagnostics or evidence.</param>
public sealed record CatalogInvocationResult(string Id, int? StatusCode, byte[] Body, string? Failure, string? PrivateDiagnostic = null);
