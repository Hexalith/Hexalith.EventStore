namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>One complete, connection/statement/portal-attributed ordinary provider write attempt.</summary>
/// <param name="Id">Proxy-wide execution order.</param>
/// <param name="ConnectionId">Owned accepted session.</param>
/// <param name="Statement">Prepared statement identity.</param>
/// <param name="Portal">Bound portal identity.</param>
/// <param name="Key">Exact decoded provider key.</param>
/// <param name="Value">Exact decoded JSON state parameter.</param>
/// <param name="Marker">Selected fault marker, if any.</param>
/// <param name="Forwarded">Whether its execution frame was fully forwarded.</param>
/// <param name="CommandCompleted">Whether the provider reported exactly one affected row.</param>
/// <param name="IdleCommitted">Whether the successful implicit transaction reached idle.</param>
/// <param name="AcknowledgementWithheld">Whether success was withheld through idle commit.</param>
/// <param name="Failed">Whether PostgreSQL reported an error.</param>
internal sealed record PostgresWriteObservation(long Id, int ConnectionId, string Statement, string Portal, string Key, string Value,
    string? Marker, bool Forwarded = false, bool CommandCompleted = false, bool IdleCommitted = false,
    bool AcknowledgementWithheld = false, bool Failed = false);
