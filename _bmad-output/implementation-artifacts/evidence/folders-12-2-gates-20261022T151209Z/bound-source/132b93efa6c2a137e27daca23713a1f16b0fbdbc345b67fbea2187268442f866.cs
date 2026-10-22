using System.Net;

namespace Hexalith.EventStore.Admin.UI.Services.Exceptions;

/// <summary>
/// Carries sanitized Admin API problem details without exposing the raw response body.
/// </summary>
public sealed class AdminApiProblemException(
    string message,
    HttpStatusCode? statusCode,
    string? title = null,
    string? detail = null,
    string? errorCode = null,
    string? traceId = null,
    string? operationId = null,
    string? problemType = null,
    IReadOnlyDictionary<string, object?>? extensions = null,
    Exception? innerException = null) : Exception(message, innerException) {
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string? Title { get; } = title;
    public string? Detail { get; } = detail;
    public string? ErrorCode { get; } = errorCode;
    public string? TraceId { get; } = traceId;
    public string? OperationId { get; } = operationId;
    public string? ProblemType { get; } = problemType;
    public IReadOnlyDictionary<string, object?> Extensions { get; } = extensions ?? new Dictionary<string, object?>();
}
