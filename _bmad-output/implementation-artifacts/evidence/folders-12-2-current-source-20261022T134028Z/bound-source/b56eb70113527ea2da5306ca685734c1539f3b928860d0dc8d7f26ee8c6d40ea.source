namespace Hexalith.EventStore.Client.Projections;

/// <summary>Optional per-execution observation of the same production measurements exported by the meter.</summary>
internal sealed class ProjectionDeliveryCostScope(ProjectionDeliveryCostScope? parent) : IDisposable
{
    private readonly long[] _values = new long[Enum.GetValues<ProjectionDeliveryCost>().Length];
    private int _disposed;

    /// <summary>Reads the actual count or bytes accumulated for a category, including failed attempts.</summary>
    internal long this[ProjectionDeliveryCost cost] => Interlocked.Read(ref _values[(int)cost]);

    /// <summary>Records an actual operation and also charges any enclosing observation.</summary>
    internal void Add(ProjectionDeliveryCost cost, long value)
    {
        Interlocked.Add(ref _values[(int)cost], value);
        parent?.Add(cost, value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            ProjectionDeliveryCostDiagnostics.Restore(this, parent);
    }
}
