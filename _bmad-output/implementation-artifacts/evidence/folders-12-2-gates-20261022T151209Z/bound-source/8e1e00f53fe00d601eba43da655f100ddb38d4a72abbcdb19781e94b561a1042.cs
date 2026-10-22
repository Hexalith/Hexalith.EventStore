namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Requests append-only discovery of the delivery's tenant in one cross-tenant control index.
/// The coordinator prepares this intent with the delivery and applies it through one CAS writer.
/// </summary>
/// <param name="IndexName">The stable, store-local name of the discovery index.</param>
public sealed record SharedProjectionControlIndexIntent(string IndexName);
