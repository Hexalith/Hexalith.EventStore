namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Owns actual short admission phases while closing their scheduling markers before releasing the semaphore.</summary>
internal sealed class RouteCatalogAdmissionGate(RouteCatalogAuthority owner, SemaphoreSlim semaphore,
    RouteCatalogLocalAdmissionTurn initialTurn) : IDisposable
{
    private RouteCatalogLocalAdmissionTurn? _turn = initialTurn;

    /// <summary>Gets whether this operation currently owns the local semaphore.</summary>
    internal bool IsHeld { get; private set; } = true;

    /// <summary>Marks a new phase synchronously in the caller's execution context, only after its successful wait.</summary>
    internal void EnterAfterWait()
    {
        if (IsHeld) throw new InvalidOperationException("The local admission phase is already held.");
        IsHeld = true;
        _turn = RouteCatalogLocalAdmissionTurn.Enter(owner);
    }

    /// <summary>Closes this exact turn for all flowed contexts before allowing another local phase to enter.</summary>
    internal void Release()
    {
        if (!IsHeld) throw new InvalidOperationException("The local admission phase is not held.");
        _turn!.Dispose();
        _turn = null;
        IsHeld = false;
        semaphore.Release();
    }

    /// <summary>Releases only a successfully acquired phase, including exceptional initial reads or final validation.</summary>
    public void Dispose()
    {
        if (IsHeld) Release();
    }
}
