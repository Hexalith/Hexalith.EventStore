using System.Text.RegularExpressions;

using Hexalith.Commons.UniqueIds;

namespace Hexalith.EventStore.Middleware;

public partial class CorrelationIdMiddleware(RequestDelegate next) {
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextKey = "CorrelationId";

    public const int MaxIdentifierLength = 128;

    private static readonly Regex _identifierRegex = IdentifierPattern();

    public async Task InvokeAsync(HttpContext context) {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId;

        if (context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues value)
            && IsValidIdentifier(value.ToString())) {
            correlationId = value.ToString();
        }
        else {
            correlationId = UniqueIdHelper.GenerateSortableUniqueStringId();
        }

        context.Items[HttpContextKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns true when <paramref name="value"/> is a syntactically valid correlation identifier:
    /// non-empty, ≤128 chars, matching the identifier regex (alphanumeric + hyphens, with alphanumeric anchors).
    /// Covers ULID and GUID without requiring strict format parsing (CLAUDE.md R2-A7 forbids <c>Guid.TryParse</c>).
    /// </summary>
    public static bool IsValidIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length <= MaxIdentifierLength
           && _identifierRegex.IsMatch(value);

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9-]*[a-zA-Z0-9])?$", RegexOptions.Compiled)]
    private static partial Regex IdentifierPattern();
}
