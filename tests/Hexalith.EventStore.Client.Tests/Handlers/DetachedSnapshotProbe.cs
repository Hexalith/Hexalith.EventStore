namespace Hexalith.EventStore.Client.Tests.Handlers;

public sealed class DetachedSnapshotProbe : IDisposable
{
    private static readonly AsyncLocal<DetachedSnapshotProbe?> _current = new();
    private readonly DetachedSnapshotProbe? _previous;

    public DetachedSnapshotProbe() { _previous = _current.Value; _current.Value = this; }
    public static DetachedSnapshotProbe? Current => _current.Value;
    public DetachedSnapshotState? AppliedState { get; set; }
    public DetachedSnapshotState? HandledState { get; set; }
    public Action? OnApply { get; set; }
    public Action? OnDeclaration { get; set; }
    public Action? OnHandle { get; set; }
    public int CaptureCalls { get; set; }
    public bool FailHandle { get; set; }
    public void Dispose() => _current.Value = _previous;
}
