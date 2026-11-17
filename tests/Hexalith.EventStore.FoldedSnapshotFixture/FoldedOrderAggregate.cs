using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.FoldedSnapshotFixture;

/// <summary>Convention-discovered SDK fixture retaining ordinary dispatch and both replay contracts.</summary>
[EventStoreDomain("folded-orders")]
public sealed class FoldedOrderAggregate : EventStoreAggregate<FoldedOrderState>
{
    /// <summary>The SDK-fixture profile, not a module enrollment.</summary>
    public const string ProfileId = "sdk-order-fixture-v1";
    /// <summary>The SDK-fixture parity identity, not consuming-module evidence.</summary>
    public const string ParityId = "sdk-order-fixture-parity-v1";

    /// <summary>Gets or sets the exact finite fixture limits.</summary>
    public FoldedSnapshotCaptureLimits CaptureLimits { get; set; } = FoldedOrderLimits.Create();
    /// <summary>Gets or sets a fixture callback for copy failure/cancellation controls.</summary>
    public Action<CancellationToken>? OnCopy { get; set; }
    /// <summary>Gets the number of owner-copier calls.</summary>
    public int Copies { get; private set; }

    /// <inheritdoc/>
    protected override DetachedStateCapture<FoldedOrderState> SnapshotCapture => FoldedSnapshotCapture.StateCapture;
    /// <inheritdoc/>
    protected override FoldedSnapshotCaptureProfile<FoldedOrderState> FoldedSnapshotCapture
        => new(ProfileId, ParityId, new(CaptureLimits.MaximumPreCommandStateGraphBytes, Copy), CaptureLimits);

    /// <summary>Uses the original reflection command-dispatch route and does not apply returned events.</summary>
    public static DomainResult Handle(FoldedOrderCommand command, FoldedOrderState? state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (command.Operation == "noop") return DomainResult.NoOp();
        if (command.Operation == "reject") return DomainResult.Rejection([new FoldedOrderRejected("declined")]);
        if (state is not null && command.Operation == "mutate")
        {
            state.Items["handler-only"] = "excluded";
            state.Tags.Add("handler-only");
        }
        return DomainResult.Success([new FoldedOrderChanged(command.Operation == "mutate" ? "set" : command.Operation, command.Key, command.Value)]);
    }

    private FoldedOrderState Copy(FoldedOrderState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Copies++;
        OnCopy?.Invoke(cancellationToken);
        long charge = checked(1024L + 4L * state.Name.Length);
        foreach ((string key, string value) in state.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            charge = checked(charge + 256L + 4L * key.Length + 4L * value.Length);
        }
        foreach (string tag in state.Tags)
        {
            cancellationToken.ThrowIfCancellationRequested();
            charge = checked(charge + 128L + 4L * tag.Length);
        }
        if (charge > CaptureLimits.MaximumPreCommandStateGraphBytes) throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
        return new FoldedOrderState
        {
            Name = state.Name, Items = new(state.Items, StringComparer.Ordinal), Tags = [.. state.Tags],
            Closed = state.Closed, HasItemHistory = state.HasItemHistory,
            CreatedAt = state.CreatedAt, ThrowOnApply = state.ThrowOnApply,
        };
    }
}
