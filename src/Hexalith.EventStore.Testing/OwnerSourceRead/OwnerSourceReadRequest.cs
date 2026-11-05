namespace Hexalith.EventStore.Testing.OwnerSourceRead;

/// <summary>One bounded owner-source request without caller-supplied credentials or transport coordinates.</summary>
/// <param name="Id">One-shot request identity.</param>
/// <param name="ExpiresAt">Absolute operation deadline.</param>
/// <param name="Mode">The exact administrator-bootstrap mode.</param>
/// <param name="Scope">The explicit platform source scope.</param>
/// <param name="Domain">The exact administrator domain.</param>
/// <param name="AggregateId">The exact administrator aggregate.</param>
public sealed record OwnerSourceReadRequest(string Id, DateTimeOffset ExpiresAt, string Mode,
    string Scope, string Domain, string AggregateId);
