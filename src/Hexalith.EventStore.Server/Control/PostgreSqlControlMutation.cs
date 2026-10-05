namespace Hexalith.EventStore.Server.Control;

/// <summary>One create, compare-and-update, transfer or delete in an existing owner's declared participant set.</summary>
internal sealed record PostgreSqlControlMutation(
    string Family,
    string Address,
    PostgreSqlControlRowImage? Expected,
    PostgreSqlControlRowImage? Next,
    bool OwnershipTransfer = false);
