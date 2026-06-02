using System.Globalization;
using System.Text;

namespace Hexalith.EventStore.Client.Queries;

/// <summary>
/// Builds a stable, collision-safe scope string for use with <see cref="IQueryCursorCodec"/>.
/// </summary>
/// <remarks>
/// <para>
/// A cursor scope binds a cursor to the exact endpoint/filter set it was issued for, so a cursor minted
/// for one caller's view cannot be replayed against another's. Domains supply only the scope fields
/// (key/value pairs); this builder produces the canonical string.
/// </para>
/// <para>
/// The format uses <c>'|'</c> as the segment separator and <c>':'</c> as the key/value separator. Both
/// characters (and the escape character <c>'\'</c>) are escaped inside caller-supplied <em>values</em> so
/// an attacker-controlled identifier cannot collide with another scope by injecting a separator. Keys are
/// caller-controlled literals and are written verbatim. The resulting string is deterministic for a given
/// ordered set of fields.
/// </para>
/// </remarks>
public sealed class QueryCursorScope {
    private readonly StringBuilder _builder = new();
    private bool _hasSegment;

    private QueryCursorScope() {
    }

    /// <summary>
    /// Creates a new, empty scope builder.
    /// </summary>
    /// <returns>A new <see cref="QueryCursorScope"/>.</returns>
    public static QueryCursorScope Create() => new();

    /// <summary>
    /// Appends a string-valued scope field. A <see langword="null"/> or empty value is written as an
    /// empty segment value (preserving positional shape).
    /// </summary>
    /// <param name="key">The field key (a caller-controlled literal, written verbatim).</param>
    /// <param name="value">The field value (escaped to prevent separator injection).</param>
    /// <returns>This builder, for chaining.</returns>
    public QueryCursorScope Add(string key, string? value) {
        AppendSegment(key, EscapeSegment(value));
        return this;
    }

    /// <summary>
    /// Appends an instant-valued scope field, formatted as round-trip UTC (<c>"O"</c>) under the invariant
    /// culture. A <see langword="null"/> instant is written as an empty segment value.
    /// </summary>
    /// <param name="key">The field key (a caller-controlled literal, written verbatim).</param>
    /// <param name="value">The instant value.</param>
    /// <returns>This builder, for chaining.</returns>
    public QueryCursorScope Add(string key, DateTimeOffset? value) {
        // The formatted instant is machine-generated, fixed-width, and never attacker-controlled, so its
        // ':' separators are written verbatim rather than escaped (it cannot inject a colliding segment).
        AppendSegment(key, FormatInstant(value));
        return this;
    }

    private void AppendSegment(string key, string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_hasSegment) {
            _builder.Append('|');
        }

        _builder.Append(key).Append(':').Append(value);
        _hasSegment = true;
    }

    /// <summary>
    /// Builds the canonical scope string from the appended fields.
    /// </summary>
    /// <returns>The scope string.</returns>
    public string Build() => _builder.ToString();

    // The cursor scope uses '|' as a segment separator and ':' as a key/value separator. Escape both
    // (and the escape char) inside caller-supplied segments so an attacker-controlled id cannot collide
    // with another scope by injecting '|' or ':'.
    private static string EscapeSegment(string? value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\", StringComparison.Ordinal)
                   .Replace("|", "\\p", StringComparison.Ordinal)
                   .Replace(":", "\\c", StringComparison.Ordinal);

    private static string FormatInstant(DateTimeOffset? value)
        => value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
}
