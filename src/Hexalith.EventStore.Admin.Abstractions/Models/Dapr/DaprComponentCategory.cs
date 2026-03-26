namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Categories of DAPR components derived from the component type prefix.
/// </summary>
public enum DaprComponentCategory
{
    /// <summary>Unknown or unrecognized component type.</summary>
    Unknown,

    /// <summary>State store component (state.*).</summary>
    StateStore,

    /// <summary>Pub/Sub component (pubsub.*).</summary>
    PubSub,

    /// <summary>Binding component (bindings.*).</summary>
    Binding,

    /// <summary>Configuration component (configuration.*).</summary>
    Configuration,

    /// <summary>Lock component (lock.*).</summary>
    Lock,

    /// <summary>Secret store component (secretstores.*).</summary>
    SecretStore,

    /// <summary>Middleware component (middleware.*).</summary>
    Middleware,
}
