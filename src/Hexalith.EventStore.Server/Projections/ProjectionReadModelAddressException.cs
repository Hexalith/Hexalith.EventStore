namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Thrown when a requested projection read-model target cannot be resolved to a canonical, aggregate-owned
/// address — for example an unregistered slot, or a slot declared as shared. The message never discloses
/// target state; the coordinated eraser maps this to an <c>Unsupported</c> structured outcome before any
/// mutation.
/// </summary>
public sealed class ProjectionReadModelAddressException : InvalidOperationException {
    /// <summary>Initializes a new instance of the <see cref="ProjectionReadModelAddressException"/> class.</summary>
    public ProjectionReadModelAddressException() {
    }

    /// <summary>Initializes a new instance of the <see cref="ProjectionReadModelAddressException"/> class.</summary>
    /// <param name="message">The exception message.</param>
    public ProjectionReadModelAddressException(string message)
        : base(message) {
    }

    /// <summary>Initializes a new instance of the <see cref="ProjectionReadModelAddressException"/> class.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ProjectionReadModelAddressException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
