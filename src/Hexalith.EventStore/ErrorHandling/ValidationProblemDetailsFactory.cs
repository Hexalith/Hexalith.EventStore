
using System.Text.Json;

using FluentValidation.Results;

using Hexalith.EventStore.Contracts.Problems;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.ErrorHandling;

/// <summary>
/// Shared factory for creating RFC 7807 ProblemDetails for 400 validation errors.
/// Single source of truth for validation error response shape across all three validation paths
/// (ValidateModelFilter, ValidationExceptionHandler, Controller extension sanitization).
/// </summary>
public static class ValidationProblemDetailsFactory {
    /// <summary>
    /// The stable URI identifying this error category.
    /// </summary>
    public const string TypeUri = ProblemTypeUris.ValidationError;

    /// <summary>
    /// The human-readable title for validation errors.
    /// </summary>
    public const string Title = "Command Validation Failed";

    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> from FluentValidation failures.
    /// Property names are converted to camelCase to match JSON serialization conventions.
    /// Multiple errors on the same property are joined with "; ".
    /// </summary>
    /// <param name="detail">A human-readable summary of the validation error(s).</param>
    /// <param name="failures">The FluentValidation failures to include in the errors dictionary.</param>
    /// <param name="correlationId">The correlation ID from the HTTP context, or null.</param>
    /// <param name="tenantId">The tenant ID from the request, or null.</param>
    /// <returns>A fully populated <see cref="ProblemDetails"/> instance.</returns>
    public static ProblemDetails Create(
        string detail,
        IEnumerable<ValidationFailure> failures,
        string? correlationId,
        string? tenantId) {
        var errors = failures
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => string.Join("; ", g.Select(e => e.ErrorMessage)));

        string? reasonCode = failures
            .Select(f => f.ErrorCode)
            .FirstOrDefault(static errorCode => errorCode.StartsWith("query_", StringComparison.Ordinal));

        return CreateCore(detail, errors, correlationId, tenantId, reasonCode);
    }

    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> from a pre-built errors dictionary.
    /// Use this overload for validation paths that produce their own error keys
    /// (e.g., extension metadata sanitization).
    /// </summary>
    /// <param name="detail">A human-readable summary of the validation error(s).</param>
    /// <param name="errors">A dictionary of error keys to human-readable messages.</param>
    /// <param name="correlationId">The correlation ID from the HTTP context, or null.</param>
    /// <param name="tenantId">The tenant ID from the request, or null.</param>
    /// <returns>A fully populated <see cref="ProblemDetails"/> instance.</returns>
    public static ProblemDetails Create(
        string detail,
        Dictionary<string, string> errors,
        string? correlationId,
        string? tenantId) => CreateCore(detail, errors, correlationId, tenantId, reasonCode: null);

    private static ProblemDetails CreateCore(
        string detail,
        Dictionary<string, string> errors,
        string? correlationId,
        string? tenantId,
        string? reasonCode) {
        var problemDetails = new ProblemDetails {
            Status = StatusCodes.Status400BadRequest,
            Title = Title,
            Type = TypeUri,
            Detail = detail,
            Extensions =
            {
                [GatewayProblemDetailsExtensions.CorrelationId] = correlationId,
                [GatewayProblemDetailsExtensions.TenantId] = tenantId,
                [GatewayProblemDetailsExtensions.Errors] = errors,
            },
        };

        if (!string.IsNullOrWhiteSpace(reasonCode)) {
            problemDetails.Extensions[GatewayProblemDetailsExtensions.ReasonCode] = reasonCode;
        }

        return problemDetails;
    }
}
