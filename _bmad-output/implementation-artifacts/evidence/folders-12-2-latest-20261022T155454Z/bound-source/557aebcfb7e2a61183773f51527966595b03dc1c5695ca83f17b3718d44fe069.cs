using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Records bounded, support-safe denial telemetry for internal credential checks.
/// </summary>
/// <remarks>
/// Only the scheme, reason code, request path, and a bounded correlation value are recorded. Tokens, raw claims,
/// payloads, secret values, and tenant inventories never enter logs or traces.
/// </remarks>
public static partial class WorkloadAuthenticationTelemetry
{
    /// <summary>Gets the trace tag carrying the authenticating scheme.</summary>
    public const string SchemeTagName = "eventstore.auth.scheme";

    /// <summary>Gets the trace tag carrying the bounded denial reason.</summary>
    public const string DenialReasonTagName = "eventstore.auth.denial_reason";

    private const string CorrelationHeaderName = "X-Correlation-ID";
    private const int MaximumCorrelationLength = 128;

    /// <summary>
    /// Records one denial.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="context">The denied request.</param>
    /// <param name="schemeName">The scheme that denied the request.</param>
    /// <param name="reasonCode">The bounded reason code.</param>
    /// <param name="statusCode">The returned status code.</param>
    public static void RecordDenial(
        ILogger logger,
        HttpContext context,
        string schemeName,
        string reasonCode,
        int statusCode)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(context);
        Activity? activity = Activity.Current;
        _ = activity?.SetTag(SchemeTagName, schemeName);
        _ = activity?.SetTag(DenialReasonTagName, reasonCode);
        LogDenied(
            logger,
            schemeName,
            reasonCode,
            statusCode,
            context.Request.Path.Value ?? string.Empty,
            GetCorrelationId(context));
    }

    /// <summary>
    /// Gets a bounded correlation value: an accepted <c>X-Correlation-ID</c> or the current trace identifier.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The correlation value.</returns>
    public static string GetCorrelationId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        StringValues values = context.Request.Headers[CorrelationHeaderName];
        if (values.Count == 1 && IsBoundedCorrelationId(values[0]))
        {
            return values[0]!;
        }

        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    private static bool IsBoundedCorrelationId(string? value)
        => !string.IsNullOrEmpty(value)
            && value.Length <= MaximumCorrelationLength
            && value.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-');

    [LoggerMessage(
        EventId = 5501,
        Level = LogLevel.Warning,
        Message = "Internal credential denied: SecurityEvent=InternalAuthenticationDenied, Scheme={Scheme}, Reason={Reason}, StatusCode={StatusCode}, Path={Path}, CorrelationId={CorrelationId}")]
    private static partial void LogDenied(
        ILogger logger,
        string scheme,
        string reason,
        int statusCode,
        string path,
        string correlationId);
}
