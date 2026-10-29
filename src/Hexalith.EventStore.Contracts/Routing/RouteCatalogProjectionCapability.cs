namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Generation-bound existing named projection capability fingerprint, separate from message declaration hashes.</summary>
/// <param name="AppId">Exact receiver.</param>
/// <param name="Domain">Exact domain.</param>
/// <param name="ContractVersion">Exact service version.</param>
/// <param name="Fingerprint">Existing capability protocol fingerprint, without redefining its bytes.</param>
/// <param name="ProjectionTypes">Complete callable named handler set.</param>
public sealed record RouteCatalogProjectionCapability(string AppId, string Domain, string ContractVersion,
    string Fingerprint, IReadOnlyList<string> ProjectionTypes);
