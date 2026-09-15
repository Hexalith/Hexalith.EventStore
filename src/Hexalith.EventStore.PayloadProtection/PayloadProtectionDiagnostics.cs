// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 10, 14, and 15.
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Emits only the closed operation, result, reason, and format fields permitted by normative sections 10.8 and 15.2.
/// </summary>
internal static class PayloadProtectionDiagnostics
{
    /// <summary>Gets the shared activity-source and meter name.</summary>
    internal const string Name = "Hexalith.EventStore.PayloadProtection";

    private static readonly ActivitySource _activitySource = new(Name);
    private static readonly Meter _meter = new(Name);
    private static readonly Counter<long> _operations = _meter.CreateCounter<long>("eventstore.payload_protection.operations");
    private static readonly Histogram<double> _duration = _meter.CreateHistogram<double>("eventstore.payload_protection.duration", "ms");

    /// <summary>
    /// Starts a closed-name core activity without allowing a diagnostic listener to affect the operation.
    /// </summary>
    internal static Activity? Start(PayloadProtectionOperation operation)
    {
        try
        {
            return _activitySource.StartActivity(
                operation == PayloadProtectionOperation.Protect
                    ? "EventStore.PayloadProtection.Protect"
                    : "EventStore.PayloadProtection.Unprotect");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Stops an activity without allowing listener callbacks to affect the operation.
    /// </summary>
    internal static void Stop(Activity? activity)
    {
        try
        {
            activity?.Dispose();
        }
        catch
        {
            // Diagnostic listeners are untrusted observers of the operation outcome.
        }
    }

    /// <summary>
    /// Records one bounded operation result without allowing a meter listener to affect the operation.
    /// </summary>
    internal static void Record(
        PayloadProtectionOperation operation,
        PayloadProtectionDiagnosticResult result,
        double durationMilliseconds,
        bool protectedFormat = true)
    {
        try
        {
            TagList tags = default;
            tags.Add("operation", operation == PayloadProtectionOperation.Protect ? "protect" : "unprotect");
            tags.Add("result", ResultToken(result));
            tags.Add("format_version", protectedFormat ? "v2" : "none");
            _operations.Add(1, tags);
            _duration.Record(durationMilliseconds, tags);
        }
        catch
        {
            // Metrics are best effort and cannot change cryptographic ownership or returned results.
        }
    }

    private static string ResultToken(PayloadProtectionDiagnosticResult result)
        => result switch
        {
            PayloadProtectionDiagnosticResult.Success => "success",
            PayloadProtectionDiagnosticResult.Malformed => "malformed",
            PayloadProtectionDiagnosticResult.AuthenticationFailed => "authentication-failed",
            PayloadProtectionDiagnosticResult.Cancelled => "cancelled",
            PayloadProtectionDiagnosticResult.Unavailable => "unavailable",
            PayloadProtectionDiagnosticResult.MissingKey => "missing-key",
            PayloadProtectionDiagnosticResult.ConsistencyMismatch => "consistency-mismatch",
            PayloadProtectionDiagnosticResult.CryptographicFailure => "cryptographic-failure",
            _ => "malformed",
        };
}
