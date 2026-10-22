using Hexalith.EventStore.Contracts.Snapshots;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Trusted, finite qualification of one aggregate state's folded snapshot capture.</summary>
/// <typeparam name="TState">The exact reviewed aggregate state type.</typeparam>
/// <remarks>The deployment owner supplies parity evidence and a detached graph copier. The limits cover library-owned live representations; trusted callbacks must honor their declared bounds.</remarks>
public sealed class FoldedSnapshotCaptureProfile<TState> where TState : class
{
    /// <summary>Creates an immutable qualification for one state and serializer profile.</summary>
    public FoldedSnapshotCaptureProfile(
        string profileId,
        string parityEvidenceId,
        DetachedStateCapture<TState> stateCapture,
        FoldedSnapshotCaptureLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(parityEvidenceId);
        ArgumentNullException.ThrowIfNull(stateCapture);
        ArgumentNullException.ThrowIfNull(limits);
        if (stateCapture.MaximumAccountedBytes > limits.MaximumPreCommandStateGraphBytes
            || stateCapture.MaximumAccountedBytes > limits.MaximumWorkingStateGraphBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "SnapshotCaptureLimitExceeded");
        }
        ProfileId = profileId;
        ParityEvidenceId = parityEvidenceId;
        StateCapture = stateCapture;
        Limits = limits;
    }

    /// <summary>Gets the exact serializer and deployment profile identifier.</summary>
    public string ProfileId { get; }

    /// <summary>Gets the owner's reviewed replay and JSON restore parity evidence identifier.</summary>
    public string ParityEvidenceId { get; }

    /// <summary>Gets the declared detached state copier.</summary>
    public DetachedStateCapture<TState> StateCapture { get; }

    /// <summary>Gets the exact finite representation declaration for this profile.</summary>
    public FoldedSnapshotCaptureLimits Limits { get; }

    /// <summary>Gets the maximum number of admitted events.</summary>
    public int MaximumEventCount => Limits.MaximumEventCount;

    /// <summary>Gets the maximum combined admitted event bytes.</summary>
    public int MaximumEventBytes => Limits.MaximumEventBytes;

    /// <summary>Gets the maximum folded JSON bytes.</summary>
    public int MaximumStateJsonBytes => Limits.MaximumStateJsonBytes;

    /// <summary>Gets the maximum library-owned live capture workspace.</summary>
    public long MaximumWorkspaceBytes => Limits.MaximumCaptureWorkspaceBytes;

    /// <summary>Gets the maximum folded JSON node count.</summary>
    public int MaximumNodeCount => Limits.MaximumNodeCount;

    /// <summary>Gets the maximum folded JSON depth.</summary>
    public int MaximumDepth => Limits.MaximumDepth;
}
