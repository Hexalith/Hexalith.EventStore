namespace Hexalith.EventStore.Admin.Cli.Client;

/// <summary>
/// Exception thrown by <see cref="AdminApiClient"/> for all API communication errors,
/// with a user-friendly message suitable for direct display on stderr.
/// </summary>
public class AdminApiException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiException"/> class.
    /// </summary>
    public AdminApiException(string message)
        : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiException"/> class.
    /// </summary>
    public AdminApiException(string message, Exception innerException)
        : base(message, innerException) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiException"/> class with an HTTP status code.
    /// </summary>
    public AdminApiException(string message, int httpStatusCode)
        : base(message) => HttpStatusCode = httpStatusCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiException"/> class with safe ProblemDetails.
    /// </summary>
    public AdminApiException(string message, int httpStatusCode, AdminApiProblemDetails problem)
        : base(message) {
        HttpStatusCode = httpStatusCode;
        Problem = problem;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiException"/> class.
    /// </summary>
    public AdminApiException()
        : base() {
    }

    /// <summary>
    /// Gets the HTTP status code that caused this exception, or <c>null</c> if not HTTP-related.
    /// </summary>
    public int? HttpStatusCode { get; }

    /// <summary>
    /// Gets the safe ProblemDetails subset returned by the Admin API, if one was available.
    /// </summary>
    public AdminApiProblemDetails? Problem { get; }
}
