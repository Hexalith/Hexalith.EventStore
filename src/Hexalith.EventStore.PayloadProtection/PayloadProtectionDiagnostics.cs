using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Emits only the closed operation, result, reason, and format fields permitted by normative sections 10.8 and 15.2.
/// </summary>
internal static class PayloadProtectionDiagnostics {
    internal const string Name = "Hexalith.EventStore.PayloadProtection";
    private static readonly ActivitySource _activitySource = new(Name);
    private static readonly Meter _meter = new(Name);
    private static readonly Counter<long> _operations = _meter.CreateCounter<long>("eventstore.payload_protection.operations");
    private static readonly Histogram<double> _duration = _meter.CreateHistogram<double>("eventstore.payload_protection.duration", "ms");

    /// <summary>
    /// Starts a closed-name core activity.
    /// </summary>
    internal static Activity? Start(PayloadProtectionOperation operation)
        => _activitySource.StartActivity(
            operation == PayloadProtectionOperation.Protect
                ? "EventStore.PayloadProtection.Protect"
                : "EventStore.PayloadProtection.Unprotect");

    /// <summary>
    /// Records one bounded operation result.
    /// </summary>
    internal static void Record(
        PayloadProtectionOperation operation,
        PayloadProtectionDiagnosticResult result,
        double durationMilliseconds) {
        TagList tags = default;
        tags.Add("operation", operation == PayloadProtectionOperation.Protect ? "protect" : "unprotect");
        tags.Add("result", ResultToken(result));
        tags.Add("format_version", "v2");
        _operations.Add(1, tags);
        _duration.Record(durationMilliseconds, tags);
    }

    private static string ResultToken(PayloadProtectionDiagnosticResult result)
        => result switch {
            PayloadProtectionDiagnosticResult.Success => "success",
            PayloadProtectionDiagnosticResult.Malformed => "malformed",
            PayloadProtectionDiagnosticResult.AuthenticationFailed => "authentication-failed",
            PayloadProtectionDiagnosticResult.Cancelled => "cancelled",
            PayloadProtectionDiagnosticResult.Unavailable => "unavailable",
            _ => "malformed",
        };
}
