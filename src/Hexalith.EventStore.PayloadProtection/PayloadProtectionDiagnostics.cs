// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 8-10, 13-15; lifecycle amendment sections 1-8.
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

    private static int _initializationInProgress;
    private static ActivitySource? _activitySource;
    private static Meter? _meter;
    private static Counter<long>? _operations;
    private static Histogram<double>? _duration;
    private static Counter<long>? _providerFailures;
    private static Counter<long>? _cacheHits;
    private static Counter<long>? _cacheMisses;
    private static UpDownCounter<long>? _cacheEntries;
    private static Counter<long>? _lifecycleTransitions;

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
            InitializeDiagnostics();
            // Parent identity preserves correlation without retaining a parent object or inheriting its baggage.
            activity = _activitySource?.CreateActivity(
                ActivityName(operation),
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
        InitializeDiagnostics();
        TagList tags = default;
        tags.Add("operation", OperationToken(operation));
        tags.Add("result", ResultToken(result));
        tags.Add("format_version", protectedFormat ? "v2" : "none");
        try
        {
            _operations?.Add(1, tags);
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
            _duration?.Record(durationMilliseconds, tags);
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

    /// <summary>Records a typed backend failure using only the validated registered backend ID and closed reason.</summary>
    internal static void ProviderFailure(string backendId, PayloadBackendFailure failure)
    {
        try
        {
            PersonalDataPolicyRegistry.ValidateId(backendId);
            InitializeDiagnostics();
            TagList tags = default;
            tags.Add("backend_id", backendId);
            tags.Add("reason_code", failure == PayloadBackendFailure.Denied ? "provider-denied" : failure == PayloadBackendFailure.Unavailable ? "provider-unavailable" : "consistency-mismatch");
            _providerFailures?.Add(1, tags);
        }
        catch { /* Diagnostic observers cannot affect backend authority. */ }
    }

    /// <summary>Records one cache access without a scope, key, provider version or size.</summary>
    internal static void CacheAccess(bool hit)
    {
        try
        {
            InitializeDiagnostics();
            TagList tags = default;
            tags.Add("cache_result", hit ? "hit" : "miss");
            if (hit) { _cacheHits?.Add(1, tags); } else { _cacheMisses?.Add(1, tags); }
        }
        catch { /* Diagnostic observers cannot change cache ownership. */ }
    }

    /// <summary>Records only a single entry's insertion/removal.</summary>
    internal static void CacheEntry(bool added)
    {
        try { InitializeDiagnostics(); _cacheEntries?.Add(added ? 1 : -1); }
        catch { /* Diagnostic observers cannot change clearing. */ }
    }

    /// <summary>Counts a confirmed lifecycle transition without scope or epoch labels.</summary>
    internal static void LifecycleTransition()
    {
        try { InitializeDiagnostics(); _lifecycleTransitions?.Add(1); }
        catch { /* Diagnostic observers cannot change a persisted transition. */ }
    }

    private static void InitializeDiagnostics()
    {
        if (Interlocked.CompareExchange(ref _initializationInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            try
            {
                _activitySource ??= new ActivitySource(Name);
            }
            catch
            {
                // Source publication callbacks cannot poison static initialization; later calls retry.
            }

            try
            {
                _meter ??= new Meter(Name);
                _operations ??= _meter.CreateCounter<long>("eventstore.payload_protection.operations");
                _duration ??= _meter.CreateHistogram<double>("eventstore.payload_protection.duration", "ms");
                _providerFailures ??= _meter.CreateCounter<long>("eventstore.payload_protection.provider_failures");
                _cacheHits ??= _meter.CreateCounter<long>("eventstore.payload_protection.key_cache_hits");
                _cacheMisses ??= _meter.CreateCounter<long>("eventstore.payload_protection.key_cache_misses");
                _cacheEntries ??= _meter.CreateUpDownCounter<long>("eventstore.payload_protection.key_cache_entries");
                _lifecycleTransitions ??= _meter.CreateCounter<long>("eventstore.payload_protection.lifecycle_transitions");
            }
            catch
            {
                // Preserve successful instruments and retry missing ones after an observer fault.
            }
        }
        finally
        {
            Volatile.Write(ref _initializationInProgress, 0);
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
            PayloadProtectionDiagnosticResult.Denied => "denied",
            PayloadProtectionDiagnosticResult.Invalidated => "invalidated",
            _ => "malformed",
        };

    private static string OperationToken(PayloadProtectionOperation operation) => operation switch
    {
        PayloadProtectionOperation.Protect => "protect", PayloadProtectionOperation.Unprotect => "unprotect",
        PayloadProtectionOperation.Wrap => "wrap", PayloadProtectionOperation.Unwrap => "unwrap",
        PayloadProtectionOperation.Rewrap => "rewrap", PayloadProtectionOperation.Invalidate => "invalidate",
        PayloadProtectionOperation.Reconcile => "reconcile", _ => "protect",
    };
    private static string ActivityName(PayloadProtectionOperation operation) => operation switch
    {
        PayloadProtectionOperation.Protect => "EventStore.PayloadProtection.Protect",
        PayloadProtectionOperation.Unprotect => "EventStore.PayloadProtection.Unprotect",
        PayloadProtectionOperation.Wrap => "EventStore.PayloadProtection.Key.Wrap",
        PayloadProtectionOperation.Unwrap => "EventStore.PayloadProtection.Key.Unwrap",
        PayloadProtectionOperation.Rewrap => "EventStore.PayloadProtection.Key.Rewrap",
        PayloadProtectionOperation.Invalidate => "EventStore.PayloadProtection.Key.Invalidate",
        PayloadProtectionOperation.Reconcile => "EventStore.PayloadProtection.Key.Reconcile", _ => "EventStore.PayloadProtection.Protect",
    };
}
