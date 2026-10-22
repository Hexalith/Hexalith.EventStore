namespace Hexalith.EventStore.Client.Tests.Handlers;

public sealed class DetachedSnapshotState
{
    public int Value { get; set; }

    public void Apply(DetachedSnapshotEvent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Value += payload.Amount;
        DetachedSnapshotProbe.Current!.AppliedState = this;
        DetachedSnapshotProbe.Current.OnApply?.Invoke();
        if (payload.Fail) { throw new InvalidOperationException("Injected Apply failure after mutation."); }
    }
}
