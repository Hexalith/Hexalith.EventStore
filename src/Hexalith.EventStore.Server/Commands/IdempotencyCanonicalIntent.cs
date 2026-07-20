namespace Hexalith.EventStore.Server.Commands;

/// <summary>Contains server-trusted semantic fields used to derive canonical mutation intent.</summary>
/// <param name="CanonicalTarget">The schema-normalized target identity.</param>
/// <param name="SemanticPayload">The schema-normalized semantic JSON payload.</param>
/// <param name="SemanticOptions">Optional schema-declared behavior-affecting options. Correlation, bearer/provider tokens, clocks, traces, delivery attempts, and retry metadata are excluded.</param>
/// <param name="PolicyVersion">The server-owned operation policy version.</param>
/// <param name="DelegatedTaskScope">The optional delegated task scope.</param>
/// <param name="CredentialScope">The optional behavior-affecting credential scope.</param>
public sealed record IdempotencyCanonicalIntent(
    string CanonicalTarget,
    byte[] SemanticPayload,
    IReadOnlyDictionary<string, string>? SemanticOptions,
    string PolicyVersion,
    string? DelegatedTaskScope,
    string? CredentialScope);
