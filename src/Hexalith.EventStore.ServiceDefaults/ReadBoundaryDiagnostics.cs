using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Emits one finite public-query completion without inherited identity-bearing scopes or trace links.</summary>
internal static partial class ReadBoundaryDiagnostics
{
    /// <summary>The sole category used for the common query boundary record.</summary>
    internal const string Category = "Hexalith.EventStore.QueryBoundary";

    /// <summary>Observes final status after exception handling and clears ambient logging scopes for the record.</summary>
    /// <param name="context">The completed public request.</param>
    /// <param name="loggers">The real host logger factory.</param>
    /// <returns>Completion of the actual provider emission.</returns>
    internal static Task CompleteAsync(HttpContext context, ILoggerFactory loggers)
    {
        string? correlation = context.Items["CorrelationId"] as string;
        if (string.IsNullOrEmpty(correlation) || correlation.Length > 128 || !correlation.All(static value => char.IsAsciiLetterOrDigit(value) || value == '-'))
        {
            correlation = ActivityTraceId.CreateRandom().ToHexString();
        }
        int status = context.Response.StatusCode;
        ILogger logger = loggers.CreateLogger(Category);
        string safeCorrelation = correlation;
        // Provider scopes can otherwise append arbitrary application data to an entirely safe log state.
        if (ExecutionContext.IsFlowSuppressed())
        {
            return Task.Run(() => Completed(logger, safeCorrelation, status));
        }
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(() => Completed(logger, safeCorrelation, status));
        }
    }

    [LoggerMessage(EventId = 5510, Level = LogLevel.Information,
        Message = "Query boundary completed: CorrelationId={CorrelationId}, StatusCode={StatusCode}, Category=QueryBoundary")]
    private static partial void Completed(ILogger logger, string correlationId, int statusCode);
}
