using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Closed Development qualification of retained JSON Dapr component/ACL inputs; unfinished deployed profiles are never inferred.</summary>
public static class RouteCatalogTopologyValidator
{
    /// <summary>Compares exact scopes and effective allow grants to catalog targets, auxiliary inventory and protected lifecycle methods.</summary>
    public static void ValidateDevelopment(ReadOnlyMemory<byte> componentBytes, ReadOnlyMemory<byte> configurationBytes,
        RouteCatalogEnvelope envelope, IReadOnlyList<RouteCatalogHostIdentity> hosts, string localAppId,
        IReadOnlyList<RouteCatalogAuxiliaryOperation>? auxiliary = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(hosts);
        if (envelope.Profile != "Development") throw Failure("owner-bound-production-topology-required");
        try
        {
            using JsonDocument component = JsonDocument.Parse(componentBytes);
            using JsonDocument configuration = JsonDocument.Parse(configurationBytes);
            JsonElement root = component.RootElement;
            if (root.GetProperty("kind").GetString() != "Component" || root.GetProperty("spec").GetProperty("type").GetString() != "state.redis"
                || root.GetProperty("spec").GetProperty("metadata").EnumerateArray().Count(p => p.GetProperty("name").GetString() == "keyPrefix") != 1
                || root.GetProperty("spec").GetProperty("metadata").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "keyPrefix").GetProperty("value").GetString() != "none")
                throw Failure("unqualified-shared-authority-component");
            string?[] scopes = [.. root.GetProperty("scopes").EnumerateArray().Select(s => s.GetString())];
            if (scopes.Any(string.IsNullOrWhiteSpace) || scopes.Distinct(StringComparer.Ordinal).Count() != scopes.Length
                || !scopes.Order(StringComparer.Ordinal).SequenceEqual(hosts.Select(h => h.AppId).Order(StringComparer.Ordinal), StringComparer.Ordinal))
                throw Failure("component-app-id-scope-drift");
            JsonElement acl = configuration.RootElement.GetProperty("spec").GetProperty("accessControl");
            if (acl.GetProperty("defaultAction").GetString() != "deny" || string.IsNullOrWhiteSpace(acl.GetProperty("trustDomain").GetString()))
                throw Failure("default-open-or-unbound-access-control");
            var methods = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (RouteCatalogEntry target in envelope.Routes.Where(r => r.AppId == localAppId))
            {
                var declaration = envelope.Declarations.Single(d => d.RouteId == target.RouteId);
                string method = declaration.Execution?.TargetKind == "projection-actor"
                    ? $"actors/{declaration.Execution.ActorType}/*/method/{target.Method}" : target.Method;
                if (methods.TryGetValue(method, out string? operation) && operation != target.Operation) throw Failure("ambiguous-acl-operation");
                methods[method] = target.Operation;
            }
            foreach (var operation in auxiliary ?? [])
                if (operation.AppId == localAppId) methods[operation.Method] = operation.Operation;
            methods["internal/routing/catalog-generation"] = EventStoreOperationIdentifiers.RouteCatalogRead;
            foreach (string stage in new[] { "prepare", "ready", "commit", "rollback", "reconcile" }) methods["internal/routing/catalog/" + stage] = EventStoreOperationIdentifiers.RouteCatalogActivate;
            if (!acl.TryGetProperty("policies", out JsonElement policies)) return;
            var callers = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement policy in policies.EnumerateArray())
            {
                string caller = policy.GetProperty("appId").GetString()!;
                if (!hosts.Any(h => h.AppId == caller) || !callers.Add(caller) || policy.GetProperty("defaultAction").GetString() != "deny")
                    throw Failure("undeclared-acl-app-id-or-default-grant");
                foreach (JsonElement grant in policy.GetProperty("operations").EnumerateArray())
                {
                    if (grant.GetProperty("action").GetString() != "allow") continue;
                    string method = grant.GetProperty("name").GetString()!.TrimStart('/');
                    if (!methods.ContainsKey(method)) throw Failure("undeclared-acl-operation");
                    string[] verbs = [.. grant.GetProperty("httpVerb").EnumerateArray().Select(v => v.GetString()!)];
                    string expected = method == "internal/routing/catalog-generation" ? "GET" : "POST";
                    if (verbs.Length != 1 || verbs[0] != expected) throw Failure("unbounded-acl-http-verb");
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
        {
            throw Failure("unsupported-or-incomplete-development-topology");
        }
    }

    private static RouteCatalogValidationException Failure(string code) => new([new("$.activation.topology", code)]);
}
