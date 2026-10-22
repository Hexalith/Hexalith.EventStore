namespace Hexalith.EventStore.Admin.UI.Services.Exceptions;

/// <summary>
/// Thrown when the Admin API returns a 503 Service Unavailable response.
/// </summary>
public class ServiceUnavailableException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class.
    /// </summary>
    public ServiceUnavailableException()
        : base("The admin backend service is temporarily unavailable.") {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ServiceUnavailableException(string message)
        : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
