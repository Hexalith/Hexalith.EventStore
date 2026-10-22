namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Helper methods for deriving <see cref="DaprComponentCategory"/> from a DAPR component type string.
/// </summary>
public static class DaprComponentCategoryHelper {
    /// <summary>
    /// Derives the component category from a DAPR component type string (e.g., "state.redis" → StateStore).
    /// </summary>
    /// <param name="componentType">The DAPR component type string.</param>
    /// <returns>The corresponding category, or <see cref="DaprComponentCategory.Unknown"/> if unrecognized.</returns>
    public static DaprComponentCategory FromComponentType(string? componentType) {
        if (string.IsNullOrEmpty(componentType)) {
            return DaprComponentCategory.Unknown;
        }

        string prefix = componentType.Split('.')[0];
        return prefix switch {
            "state" => DaprComponentCategory.StateStore,
            "pubsub" => DaprComponentCategory.PubSub,
            "bindings" => DaprComponentCategory.Binding,
            "configuration" => DaprComponentCategory.Configuration,
            "lock" => DaprComponentCategory.Lock,
            "secretstores" => DaprComponentCategory.SecretStore,
            "middleware" => DaprComponentCategory.Middleware,
            _ => DaprComponentCategory.Unknown,
        };
    }
}
