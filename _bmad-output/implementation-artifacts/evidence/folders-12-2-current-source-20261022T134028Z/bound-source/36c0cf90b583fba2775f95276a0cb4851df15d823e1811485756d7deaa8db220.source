namespace Hexalith.EventStore.Admin.Abstractions.Models.Health;

/// <summary>
/// Per-metric availability state for <see cref="SystemHealthReport"/>. Distinguishes a real
/// measured value (which may be zero) from a metric whose source is not configured in this
/// build, from a value served from a stale cache after a refresh failure.
/// </summary>
/// <remarks>
/// <para>This is the wire-level status that backs the seven UI state classes for metric tiles:
/// loading, empty, zero, unavailable, unauthorized, stale, error. Loading is owned by the calling
/// component; unauthorized and error map onto the page-level failure handling that already exists;
/// zero is just <see cref="Available"/> with a value of 0.</para>
/// <para>Default of 0 (<see cref="Available"/>) preserves wire-format compatibility with
/// existing clients that do not yet read the per-metric status fields.</para>
/// </remarks>
public enum SystemHealthMetricStatus {
    /// <summary>
    /// The accompanying metric value is a real measurement from the named source. May be zero
    /// if zero is the actual measured value for the selected scope.
    /// </summary>
    Available = 0,

    /// <summary>
    /// The metric has no source wired in this build. The accompanying numeric value is meaningless
    /// and the UI must render an explicit unavailable indicator rather than the raw number.
    /// </summary>
    Unavailable = 1,

    /// <summary>
    /// The metric source is wired but the most recent fetch failed; the accompanying value is the
    /// last successful sample. The UI must label or visually differentiate stale from fresh.
    /// </summary>
    Stale = 2,
}
