namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Connects generated actions to hosting validation using their already-resolved partition tenant.</summary>
public static class RouteCatalogGeneratedAction
{
    /// <summary>Gets the hosting-owned validation callback key for one generated action execution.</summary>
    public const string ValidatorKey = "Hexalith.EventStore.RouteCatalog.ResolvedTenantValidator";

    /// <summary>Validates a generated action before dispatch; missing hosting validation refuses dispatch.</summary>
    public static bool ValidateTenant(IDictionary<object, object?> executionContext, Type contractType, string kind, string tenant)
    {
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentNullException.ThrowIfNull(contractType);
        return executionContext.TryGetValue(ValidatorKey, out object? value)
            && value is Func<Type, string, string, bool> validate && validate(contractType, kind, tenant);
    }
}
