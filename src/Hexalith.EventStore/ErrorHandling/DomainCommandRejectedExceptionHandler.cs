using System.Text.Json;
using System.Text;

using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

public class DomainCommandRejectedExceptionHandler(ILogger<DomainCommandRejectedExceptionHandler> logger) : IExceptionHandler {
    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DomainCommandRejectedException rejection) {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? rejection.CorrelationId;

        logger.LogWarning(
            "Domain rejection returned to caller: CorrelationId={CorrelationId}, TenantId={TenantId}, RejectionType={RejectionType}",
            correlationId,
            rejection.TenantId,
            rejection.RejectionType);

        string rejectionName = GetShortRejectionName(rejection.RejectionType);
        string reasonCode = ToReasonCode(rejectionName);
        int statusCode = GetStatusCode(rejectionName);
        string title = ToTitle(rejectionName);

        var problemDetails = new ProblemDetails {
            Status = statusCode,
            Title = title,
            Type = $"{ProblemTypeUris.DomainRejection}/{reasonCode}",
            Detail = AuthorizationExceptionHandler.SanitizeForbiddenTerms(rejection.Detail),
            Instance = httpContext.Request.Path,
            Extensions = {
                [GatewayProblemDetailsExtensions.CorrelationId] = correlationId,
                [GatewayProblemDetailsExtensions.TenantId] = rejection.TenantId,
                [GatewayProblemDetailsExtensions.ReasonCode] = reasonCode,
                [GatewayProblemDetailsExtensions.RejectionType] = rejection.RejectionType,
                [GatewayProblemDetailsExtensions.CorrectiveAction] = GetCorrectiveAction(rejectionName, statusCode),
            },
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, (JsonSerializerOptions?)null, ProblemJsonContentType, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static int GetStatusCode(string rejectionName) {
        if (rejectionName.Contains("NotFound", StringComparison.Ordinal)) {
            return StatusCodes.Status404NotFound;
        }

        if (rejectionName.Contains("Already", StringComparison.Ordinal)
            || rejectionName.Contains("Duplicate", StringComparison.Ordinal)) {
            return StatusCodes.Status409Conflict;
        }

        return StatusCodes.Status422UnprocessableEntity;
    }

    private static string GetCorrectiveAction(string rejectionName, int statusCode)
        => statusCode switch {
            StatusCodes.Status404NotFound => "Verify the identifier and tenant context, then retry with an existing resource.",
            StatusCodes.Status409Conflict => "Use a different identifier or treat the existing resource as the current state.",
            _ when rejectionName.Contains("Cannot", StringComparison.Ordinal)
                || rejectionName.Contains("Invalid", StringComparison.Ordinal)
                || rejectionName.Contains("Mismatch", StringComparison.Ordinal)
                => "Correct the command payload and retry.",
            _ => "Review the rejection detail, correct the request, and retry when appropriate.",
        };

    private static string GetShortRejectionName(string rejectionType) {
        int lastDot = rejectionType.LastIndexOf('.');
        return lastDot < 0 || lastDot == rejectionType.Length - 1
            ? rejectionType
            : rejectionType[(lastDot + 1)..];
    }

    private static string ToTitle(string rejectionName) => string.Join(' ', SplitWords(rejectionName));

    private static string ToReasonCode(string rejectionName)
        => string.Join('-', SplitWords(rejectionName).Select(static word => word.ToLowerInvariant()));

    private static IReadOnlyList<string> SplitWords(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return ["domain", "rejection"];
        }

        var words = new List<string>();
        var builder = new StringBuilder();
        char previous = '\0';
        foreach (char current in value) {
            if (!char.IsLetterOrDigit(current)) {
                FlushWord();
                previous = '\0';
                continue;
            }

            if (builder.Length > 0
                && char.IsUpper(current)
                && (char.IsLower(previous) || char.IsDigit(previous))) {
                FlushWord();
            }

            _ = builder.Append(current);
            previous = current;
        }

        FlushWord();
        return words.Count == 0 ? ["domain", "rejection"] : words;

        void FlushWord() {
            if (builder.Length == 0) {
                return;
            }

            words.Add(builder.ToString());
            _ = builder.Clear();
        }
    }
}
