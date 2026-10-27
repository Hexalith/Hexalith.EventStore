using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Client.Routing;

/// <summary>Incomplete-profile report. Configuration alone can never prove a complete catalog.</summary>
/// <param name="Bindings">Safe checked-in registration inventory.</param>
/// <param name="MissingInputs">Concrete missing declaration fields, addressed by inventory index.</param>
/// <param name="InventoryLimit">The exact boundary of this inspection.</param>
public sealed record DevelopmentCatalogInspection(
    IReadOnlyList<DevelopmentBindingInventory> Bindings,
    IReadOnlyList<RouteCatalogDiagnostic> MissingInputs,
    string InventoryLimit);
