using System.Net;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Carries sanitized upstream ProblemDetails from EventStore to Admin controllers.
/// </summary>
public sealed class AdminUpstreamProblemException(
    ProblemDetails problemDetails,
    HttpStatusCode statusCode) : Exception(problemDetails.Detail ?? problemDetails.Title) {
    /// <summary>Gets the sanitized upstream problem details.</summary>
    public ProblemDetails ProblemDetails { get; } = problemDetails ?? throw new ArgumentNullException(nameof(problemDetails));

    /// <summary>Gets the upstream HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
}
