namespace Hexalith.EventStore.Admin.UI.Services.Exceptions;

/// <summary>
/// Thrown when the Admin API returns a 403 Forbidden response.
/// </summary>
public class ForbiddenAccessException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class.
    /// </summary>
    public ForbiddenAccessException()
        : base("Access denied. Insufficient permissions.") {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ForbiddenAccessException(string message)
        : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenAccessException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ForbiddenAccessException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
