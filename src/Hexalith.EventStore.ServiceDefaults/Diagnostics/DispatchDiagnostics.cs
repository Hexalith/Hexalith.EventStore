using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.ServiceDefaults.Diagnostics;

/// <summary>Optional, payload-free per-invocation diagnostics. Never participates in request control flow.</summary>
internal sealed partial class DispatchDiagnostics
{
    private readonly ILogger _logger;
    private readonly CancellationToken _requestCancellation;
    private readonly string _traceId;
    private readonly string _spanId;
    private readonly string _attemptId;

    private DispatchDiagnostics(ILogger logger, CancellationToken requestCancellation)
    {
        _logger = logger;
        _requestCancellation = requestCancellation;
        _traceId = (Activity.Current?.TraceId ?? ActivityTraceId.CreateRandom()).ToHexString();
        _spanId = (Activity.Current?.SpanId ?? ActivitySpanId.CreateRandom()).ToHexString();
        _attemptId = ActivitySpanId.CreateRandom().ToHexString();
    }

    /// <summary>Allocates diagnostics only when the owning logger enables the diagnostic level.</summary>
    internal static DispatchDiagnostics? Create(ILogger? logger, CancellationToken requestCancellation)
    {
        try
        {
            return logger?.IsEnabled(LogLevel.Debug) == true ? new(logger, requestCancellation) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Uses optional host logging without imposing a new service requirement on the receiver.</summary>
    internal static DispatchDiagnostics? Create<T>(IServiceProvider services, CancellationToken requestCancellation)
    {
        try
        {
            return Create(services.GetService<ILogger<T>>(), requestCancellation);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Records an open await without adding timers, token registrations, or transport wrappers.</summary>
    internal DispatchStageScope Start(DispatchStageName stage)
        => Start(stage, _requestCancellation);

    /// <summary>Records the actual operation token separately when it differs from the request token.</summary>
    internal DispatchStageScope Start(DispatchStageName stage, CancellationToken operationCancellation)
        => new(this, stage, operationCancellation);

    /// <summary>Emits bounded primitives and generated trace identities only; provider failures are advisory.</summary>
    internal void Write(DispatchStageName stage, bool? completed, long elapsedMilliseconds, CancellationToken operationCancellation)
    {
        try
        {
            Stage(_logger, stage.ToString(), completed is null ? "Started" : completed.Value ? "Completed" : "Failed", elapsedMilliseconds,
                _requestCancellation.IsCancellationRequested, operationCancellation.CanBeCanceled,
                operationCancellation.IsCancellationRequested, _traceId, _spanId, _attemptId);
        }
        catch
        {
            // Diagnostics must not replace an original exception or fail an otherwise successful request.
        }
    }

    [LoggerMessage(EventId = 1190, Level = LogLevel.Debug,
        Message = "Dispatch await: Stage={Stage}, Status={Status}, ElapsedMilliseconds={ElapsedMilliseconds}, RequestCanceled={RequestCanceled}, OperationCanBeCanceled={OperationCanBeCanceled}, OperationCanceled={OperationCanceled}, TraceId={TraceId}, SpanId={SpanId}, AttemptId={AttemptId}")]
    private static partial void Stage(ILogger logger, string stage, string status, long elapsedMilliseconds,
        bool requestCanceled, bool operationCanBeCanceled, bool operationCanceled, string traceId, string spanId, string attemptId);
}
