namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>A bounded failure containing a structural path and code, never input values.</summary>
/// <param name="Path">Schema field path with numeric entry indexes.</param>
/// <param name="Code">Stable validation failure code.</param>
public sealed record RouteCatalogDiagnostic(string Path, string Code);
