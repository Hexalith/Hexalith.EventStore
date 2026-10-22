namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable identity retirement inventory, independent of disposable rebuild scratch.</summary>
/// <param name="Reference">The reserved or sealed owner identity reference.</param>
/// <param name="Retired">An irreversible installer fence retained after abort cleanup.</param>
internal sealed record SharedProjectionCapturedIdentityInventory(
    SharedProjectionCapturedIdentityReference Reference, bool Retired = false);
