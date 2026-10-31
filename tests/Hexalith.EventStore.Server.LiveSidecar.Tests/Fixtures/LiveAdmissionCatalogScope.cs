namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Explicit finite tenant selection for direct in-process fixture calls without HTTP.</summary>
internal sealed class LiveAdmissionCatalogScope : IDisposable
{
    private static readonly AsyncLocal<string?> Current = new();
    private readonly string? _previous = Current.Value;

    internal LiveAdmissionCatalogScope(string tenant) => Current.Value = tenant;

    internal static string? Tenant => Current.Value;

    public void Dispose() => Current.Value = _previous;
}
