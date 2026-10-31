using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>The explicit dedicated component role for a shared authority isolated from business state keys.</summary>
public static class RouteCatalogAuthorityComponentPolicy
{
    /// <summary>The nonsecret Dapr component role label.</summary>
    public const string RoleLabel = "hexalith.io/component-role";
    /// <summary>The versioned isolated routing authority role.</summary>
    public const string IsolatedRole = "eventstore-routing-authority-v1";

    /// <summary>Checks the actual loaded dedicated role, shared component-name keyspace and absence of actor ownership.</summary>
    public static bool IsDedicatedComponent(JsonElement component, string? expectedName = null)
    {
        try
        {
            var identity = component.GetProperty("metadata");
            string? name = identity.GetProperty("name").GetString();
            if (name is not { Length: > 0 and <= 63 } || expectedName is not null && name != expectedName
                || !char.IsAsciiLetterOrDigit(name[0]) || !char.IsAsciiLetterOrDigit(name[^1])
                || name.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
                || component.GetProperty("kind").GetString() != "Component" || component.GetProperty("spec").GetProperty("type").GetString() != "state.redis"
                || identity.GetProperty("labels").GetProperty(RoleLabel).GetString() != IsolatedRole) return false;
            var metadata = component.GetProperty("spec").GetProperty("metadata").EnumerateArray().ToArray();
            var prefixes = metadata.Where(item => item.GetProperty("name").GetString() == "keyPrefix").ToArray();
            return prefixes.Length == 1 && prefixes[0].GetProperty("value").GetString() == "name"
                && !metadata.Any(item => item.GetProperty("name").GetString() == "actorStateStore"
                    && !item.GetProperty("value").ToString().Equals("false", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
    }
}
