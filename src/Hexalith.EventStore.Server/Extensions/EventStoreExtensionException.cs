namespace Hexalith.EventStore.Server.Extensions;

/// <summary>A fail-closed extension composition refusal that names its stable condition.</summary>
public sealed class EventStoreExtensionException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="EventStoreExtensionException"/> class.</summary>
    public EventStoreExtensionException()
        : this(EventStoreExtensionConditions.InvalidDeclaration, "The EventStore extension composition is invalid.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EventStoreExtensionException"/> class.</summary>
    /// <param name="message">The refusal reason.</param>
    public EventStoreExtensionException(string message)
        : this(EventStoreExtensionConditions.InvalidDeclaration, message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EventStoreExtensionException"/> class.</summary>
    /// <param name="message">The refusal reason.</param>
    /// <param name="innerException">The underlying failure.</param>
    public EventStoreExtensionException(string message, Exception? innerException)
        : this(EventStoreExtensionConditions.InvalidDeclaration, message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EventStoreExtensionException"/> class.</summary>
    /// <param name="condition">The stable named condition from <see cref="EventStoreExtensionConditions"/>.</param>
    /// <param name="message">The refusal reason naming the package and offending values.</param>
    /// <param name="innerException">The underlying failure, when any.</param>
    public EventStoreExtensionException(string condition, string message, Exception? innerException = null)
        : base(condition + ": " + message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(condition);
        Condition = condition;
    }

    /// <summary>Gets the stable named condition.</summary>
    public string Condition { get; }
}
