namespace Hexalith.EventStore.Client.Handlers;

/// <summary>An optional owner declaration for copying a known typed snapshot graph.</summary>
/// <typeparam name="TState">The exact state type owned by the application.</typeparam>
/// <remarks>
/// The owner must copy every mutable member and declare a conservative bound covering
/// both source and detached graph. The SDK validates the root, not arbitrary graph
/// independence or the owner's allocation claim. This declaration grants no catalog,
/// serializer, authenticated-source or production authority. It applies to legacy
/// command reconstruction; it does not enable versioned replay or snapshot intake.
/// </remarks>
public sealed class DetachedStateCapture<TState> where TState : class
{
    private readonly Func<TState, CancellationToken, TState> _capture;

    /// <summary>Creates a finite declaration for the application's exact state graph.</summary>
    /// <param name="maximumAccountedBytes">Maximum combined source/copy graph charge, including allocation overhead.</param>
    /// <param name="capture">A copy function that never mutates the source or returns a shared mutable graph.</param>
    public DetachedStateCapture(long maximumAccountedBytes, Func<TState, CancellationToken, TState> capture)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAccountedBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumAccountedBytes, 256L * 1024 * 1024);
        ArgumentNullException.ThrowIfNull(capture);
        MaximumAccountedBytes = maximumAccountedBytes;
        _capture = capture;
    }

    /// <summary>Gets the owner-declared combined graph charge admitted before copying.</summary>
    public long MaximumAccountedBytes { get; }

    internal TState Capture(TState source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TState copy = _capture(source, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (copy is null || ReferenceEquals(source, copy) || copy.GetType() != source.GetType())
        {
            throw new InvalidOperationException("DetachedStateInvalid: snapshot capture must return a distinct root of the exact source type.");
        }

        return copy;
    }
}
