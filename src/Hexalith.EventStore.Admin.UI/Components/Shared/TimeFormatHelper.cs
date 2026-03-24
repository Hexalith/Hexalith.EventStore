namespace Hexalith.EventStore.Admin.UI.Components.Shared;

/// <summary>
/// Shared helper for formatting time values in the admin UI.
/// </summary>
public static class TimeFormatHelper
{
    /// <summary>
    /// Formats a timestamp as a human-readable relative time string.
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>A relative time string like "2m ago", "3h ago", "5d ago".</returns>
    public static string FormatRelativeTime(DateTimeOffset timestamp)
    {
        if (timestamp == DateTimeOffset.MinValue)
        {
            return "Never";
        }

        TimeSpan elapsed = DateTimeOffset.UtcNow - timestamp;
        if (elapsed < TimeSpan.Zero)
        {
            return "Just now";
        }

        if (elapsed.TotalSeconds < 60)
        {
            return $"{(int)elapsed.TotalSeconds}s ago";
        }

        if (elapsed.TotalMinutes < 60)
        {
            return $"{(int)elapsed.TotalMinutes}m ago";
        }

        if (elapsed.TotalHours < 24)
        {
            return $"{(int)elapsed.TotalHours}h ago";
        }

        if (elapsed.TotalDays < 30)
        {
            return $"{(int)elapsed.TotalDays}d ago";
        }

        return timestamp.ToString("yyyy-MM-dd");
    }
}
