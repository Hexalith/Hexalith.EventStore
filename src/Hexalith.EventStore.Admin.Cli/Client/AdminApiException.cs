namespace Hexalith.EventStore.Admin.Cli.Client;

/// <summary>
/// Exception thrown by <see cref="AdminApiClient"/> for all API communication errors,
/// with a user-friendly message suitable for direct display on stderr.
/// </summary>
public class AdminApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiException"/> class.
    /// </summary>
    public AdminApiException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiException"/> class.
    /// </summary>
    public AdminApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminApiException"/> class.
    /// </summary>
    public AdminApiException()
        : base()
    {
    }
}
