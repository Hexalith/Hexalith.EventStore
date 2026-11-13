namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Identifies a genuinely held local admission phase to avoid reentrant scheduling deadlock.</summary>
/// <remarks>This is a private scheduling marker, never dispatch authority or a reusable catalog observation.</remarks>
internal sealed class RouteCatalogLocalAdmissionTurn : IDisposable
{
    private static readonly AsyncLocal<RouteCatalogLocalAdmissionTurn?> Current = new();
    private readonly RouteCatalogAuthority _owner;
    private readonly RouteCatalogLocalAdmissionTurn? _previous;
    private int _open = 1;

    private RouteCatalogLocalAdmissionTurn(RouteCatalogAuthority owner)
    {
        _owner = owner;
        _previous = Current.Value;
        Current.Value = this;
    }

    /// <summary>Opens a marker only after this authority has acquired its actual local admission gate.</summary>
    internal static RouteCatalogLocalAdmissionTurn Enter(RouteCatalogAuthority owner) => new(owner);

    /// <summary>Checks the actual owner and live lifetime, including a genuinely open ancestor callback.</summary>
    internal static bool IsHeldBy(RouteCatalogAuthority owner)
    {
        for (RouteCatalogLocalAdmissionTurn? turn = Current.Value; turn is not null; turn = turn._previous)
            if (ReferenceEquals(turn._owner, owner) && Volatile.Read(ref turn._open) != 0) return true;
        return false;
    }

    /// <summary>Closes the marker for this flow and all escaped execution contexts.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _open, 0);
        if (ReferenceEquals(Current.Value, this)) Current.Value = _previous;
    }
}
