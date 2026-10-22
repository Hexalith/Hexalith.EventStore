using System.Buffers;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Shared validation for colon-delimited projection key segments. Reuses the same reserved-character
/// discipline as <c>AggregateIdentity</c> and the projection-identity / rebuild-checkpoint key schemes:
/// <c>':'</c> (separator), <c>'\0'</c> (terminator), <c>'|'</c> (domain extraction), and CR/LF (log-line
/// safety) are reserved and must never appear inside a segment.
/// </summary>
internal static class ProjectionKeySegments {
    private static readonly SearchValues<char> s_reservedChars = SearchValues.Create(":\0|\r\n");

    /// <summary>
    /// Validates that a key segment is non-null, non-blank, and free of reserved characters.
    /// </summary>
    /// <param name="value">The segment value.</param>
    /// <param name="parameterName">The originating parameter name (for diagnostics).</param>
    /// <exception cref="ArgumentException">The value is null, blank, or contains a reserved character.</exception>
    public static void Validate(string value, string parameterName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.AsSpan().IndexOfAny(s_reservedChars) >= 0) {
            throw new ArgumentException(
                $"{parameterName} must not contain ':', '\\0', '|', '\\r', or '\\n' — reserved by the projection key scheme.",
                parameterName);
        }
    }
}
