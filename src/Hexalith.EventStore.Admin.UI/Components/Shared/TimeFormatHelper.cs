using System.Globalization;

namespace Hexalith.EventStore.Admin.UI.Components.Shared;

/// <summary>
/// Shared helper for formatting time values in the admin UI.
/// </summary>
public static class TimeFormatHelper {
    /// <summary>
    /// Formats a timestamp as a human-readable relative time string.
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>A relative time string like "2m ago", "3h ago", "5d ago".</returns>
    public static string FormatRelativeTime(DateTimeOffset timestamp) {
        if (timestamp == DateTimeOffset.MinValue) {
            return "Never";
        }

        TimeSpan elapsed = DateTimeOffset.UtcNow - timestamp;
        if (elapsed < TimeSpan.Zero) {
            return "Just now";
        }

        if (elapsed.TotalSeconds < 60) {
            return $"{(int)elapsed.TotalSeconds}s ago";
        }

        if (elapsed.TotalMinutes < 60) {
            return $"{(int)elapsed.TotalMinutes}m ago";
        }

        if (elapsed.TotalHours < 24) {
            return $"{(int)elapsed.TotalHours}h ago";
        }

        if (elapsed.TotalDays < 30) {
            return $"{(int)elapsed.TotalDays}d ago";
        }

        return timestamp.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Formats a byte count as a human-readable string (e.g., "1.2 GB", "456 MB").
    /// </summary>
    /// <param name="bytes">The byte count, or null if not available.</param>
    /// <returns>A formatted string, or "N/A" if null.</returns>
    public static string FormatBytes(long? bytes) {
        if (bytes is null or < 0) {
            return "N/A";
        }

        double value = bytes.Value;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int i = 0;
        while (value >= 1024 && i < units.Length - 1) {
            value /= 1024;
            i++;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:F1} {1}", value, units[i]);
    }
}
