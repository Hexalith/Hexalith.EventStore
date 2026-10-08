// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 10, 14, and 15.
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Emits only the closed operation, result, and format fields permitted by normative sections 10.8 and 15.2.
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
    /// <param name="operation">The bounded core operation name.</param>
    /// <param name="ambient">The prior ambient activity to restore when the owned activity is stopped.</param>
    internal static Activity? Start(PayloadProtectionOperation operation, out Activity? ambient)
    {
        ambient = Activity.Current;
        Activity? activity = null;
        try
        {
            // Parent identity preserves correlation without retaining a parent object or inheriting its baggage.
            activity = _activitySource.CreateActivity(
                operation == PayloadProtectionOperation.Protect
                    ? "EventStore.PayloadProtection.Protect"
                    : "EventStore.PayloadProtection.Unprotect",
                ActivityKind.Internal,
                parentId: ambient?.Id);
            if (activity is not null)
            {
                _ = activity.Start();
            }

            return activity;
        }
        catch
        {
            RestoreActivity(ambient);
            return activity;
        }
    }

    /// <summary>
    /// Stops an activity without allowing listener callbacks to affect the operation.
    /// </summary>
    /// <param name="activity">The activity to stop, or <see langword="null"/> when nothing sampled it.</param>
    /// <param name="result">
    /// The bounded outcome. It sets <see cref="ActivityStatusCode"/> only: no description is attached, because
    /// normative section 15.2 forbids any cryptographic, provider, or payload detail on a trace status.
    /// </param>
    /// <param name="ambient">The ambient activity captured before starting the owned activity.</param>
    internal static void Stop(Activity? activity, PayloadProtectionDiagnosticResult result, Activity? ambient)
    {
        try
        {
            _ = activity?.SetStatus(result == PayloadProtectionDiagnosticResult.Success
                ? ActivityStatusCode.Ok
                : ActivityStatusCode.Error);
            activity?.Dispose();
        }
        catch
        {
            // Diagnostic listeners are untrusted observers of the operation outcome.
        }
        finally
        {
            RestoreActivity(ambient);
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
        Activity? ambient = Activity.Current;
        TagList tags = default;
        tags.Add("operation", operation == PayloadProtectionOperation.Protect ? "protect" : "unprotect");
        tags.Add("result", ResultToken(result));
        tags.Add("format_version", protectedFormat ? "v2" : "none");
        try
        {
            _operations.Add(1, tags);
        }
        catch
        {
            // Each metric is best effort and cannot suppress the other instrument or change operation outcomes.
        }
        finally
        {
            RestoreActivity(ambient);
        }

        try
        {
            _duration.Record(durationMilliseconds, tags);
        }
        catch
        {
            // Each metric is best effort and cannot suppress the other instrument or change operation outcomes.
        }
        finally
        {
            RestoreActivity(ambient);
        }
    }

    private static void RestoreActivity(Activity? activity)
    {
        try
        {
            Activity.Current = activity;
        }
        catch
        {
            // CurrentChanged observers run after assignment and cannot replace the cryptographic outcome.
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
