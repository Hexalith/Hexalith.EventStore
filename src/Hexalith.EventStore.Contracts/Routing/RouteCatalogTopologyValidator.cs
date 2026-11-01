using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Closed Development qualification of retained JSON Dapr component/ACL inputs; unfinished deployed profiles are never inferred.</summary>
public static class RouteCatalogTopologyValidator
{
    /// <summary>Compares exact scopes and effective allow grants to catalog targets, auxiliary inventory and protected lifecycle methods.</summary>
    public static void ValidateDevelopment(ReadOnlyMemory<byte> componentBytes, ReadOnlyMemory<byte> configurationBytes,
        RouteCatalogEnvelope envelope, IReadOnlyList<RouteCatalogHostIdentity> hosts, string localAppId,
        IReadOnlyList<RouteCatalogAuxiliaryOperation>? auxiliary = null, RouteCatalogReceiverSurface? receiver = null)
        => ValidateDevelopment(componentBytes, configurationBytes, envelope, hosts, localAppId, auxiliary, receiver, null, null);

    /// <summary>Validates the same authority boundary while retaining owner-declared transport grants for applications absent from this fixed run.</summary>
    public static void ValidateDevelopment(ReadOnlyMemory<byte> componentBytes, ReadOnlyMemory<byte> configurationBytes,
        RouteCatalogEnvelope envelope, IReadOnlyList<RouteCatalogHostIdentity> hosts, string localAppId,
        IReadOnlyList<RouteCatalogAuxiliaryOperation>? auxiliary, RouteCatalogReceiverSurface? receiver,
        IReadOnlyList<string>? selectedAppIds, IReadOnlyList<RouteCatalogDeclaredInvocation>? declaredInvocations)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(hosts);
        if (envelope.Profile != "Development") throw Failure("owner-bound-production-topology-required");
        bool completeInventory = selectedAppIds is not null && declaredInvocations is not null;
        if (selectedAppIds is not null || declaredInvocations is not null)
        {
            if (envelope.SchemaVersion != 3 || !completeInventory
                || selectedAppIds is null || declaredInvocations is null
                || selectedAppIds.Any(string.IsNullOrWhiteSpace)
                || declaredInvocations.Any(invocation => string.IsNullOrWhiteSpace(invocation.CallerAppId) || string.IsNullOrWhiteSpace(invocation.Method)
                    || invocation.HttpMethods is not { Count: > 0 } || invocation.HttpMethods.Any(verb => verb is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")))
                || selectedAppIds.Distinct(StringComparer.Ordinal).Count() != selectedAppIds.Count
                || declaredInvocations.Select(invocation => (invocation.CallerAppId, invocation.Method)).Distinct().Count() != declaredInvocations.Count
                || hosts.Any(host => !selectedAppIds.Contains(host.AppId, StringComparer.Ordinal)))
                throw Failure("unqualified-selected-application-inventory");
        }
        try
        {
            using JsonDocument component = JsonDocument.Parse(componentBytes);
            using JsonDocument configuration = JsonDocument.Parse(configurationBytes);
            JsonElement root = component.RootElement;
            var componentMetadata = root.GetProperty("spec").GetProperty("metadata").EnumerateArray().ToArray();
            var prefixes = componentMetadata.Where(p => p.GetProperty("name").GetString() == "keyPrefix").ToArray();
            string? prefix = prefixes.Length == 1 ? prefixes[0].GetProperty("value").GetString() : null;
            bool namedAuthority = RouteCatalogAuthorityComponentPolicy.IsDedicatedComponent(root);
            if (root.GetProperty("kind").GetString() != "Component" || root.GetProperty("spec").GetProperty("type").GetString() != "state.redis"
                || prefixes.Length != 1 || prefix != "none" && !namedAuthority)
                throw Failure("unqualified-shared-authority-component");
            string?[] scopes = [.. root.GetProperty("scopes").EnumerateArray().Select(s => s.GetString())];
            if (scopes.Any(string.IsNullOrWhiteSpace) || scopes.Distinct(StringComparer.Ordinal).Count() != scopes.Length
                || !scopes.Order(StringComparer.Ordinal).SequenceEqual(hosts.Select(h => h.AppId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal))
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
                if (operation.AppId == localAppId)
                {
                    if (operation.DomainAuxiliary is { } owner && (receiver is null
                        || !receiver.Endpoints.Any(endpoint => endpoint.Pattern == "/" + operation.Method && endpoint.Admitted
                            && !endpoint.Anonymous && !endpoint.IsPattern && endpoint.Methods.SequenceEqual(["POST"], StringComparer.Ordinal)
                            && endpoint.Operation == operation.Operation && endpoint.DomainAuxiliary == owner)))
                        throw Failure("domain-auxiliary-owner-binding-drift");
                    if (operation.Sidecar is { } subscription && (receiver is null
                        || !receiver.Endpoints.Any(endpoint => endpoint.Pattern == "/" + operation.Method && endpoint.Admitted
                            && !endpoint.Anonymous && !endpoint.IsPattern && endpoint.Methods.SequenceEqual(["POST"], StringComparer.Ordinal)
                            && endpoint.Operation == operation.Operation && endpoint.Sidecar == subscription)))
                        throw Failure("sidecar-subscription-owner-binding-drift");
                    methods[operation.Method] = operation.Operation;
                }
            methods["internal/routing/catalog-generation"] = EventStoreOperationIdentifiers.RouteCatalogRead;
            foreach (string stage in new[] { "prepare", "ready", "commit", "rollback", "reconcile" }) methods["internal/routing/catalog/" + stage] = EventStoreOperationIdentifiers.RouteCatalogActivate;
            if (!acl.TryGetProperty("policies", out JsonElement policies)) return;
            var callers = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement policy in policies.EnumerateArray())
            {
                string caller = policy.GetProperty("appId").GetString()!;
                if (!callers.Add(caller) || policy.GetProperty("defaultAction").GetString() != "deny")
                    throw Failure("undeclared-acl-app-id-or-default-grant");
                bool participant = hosts.Any(host => host.AppId == caller);
                if (completeInventory)
                {
                    if (!declaredInvocations!.Any(invocation => invocation.CallerAppId == caller)) throw Failure("undeclared-acl-app-id-or-default-grant");
                    foreach (var grant in policy.GetProperty("operations").EnumerateArray().Where(grant => grant.GetProperty("action").GetString() == "allow"))
                        if (!declaredInvocations!.Any(invocation => invocation.CallerAppId == caller
                            && invocation.Method.TrimStart('/') == grant.GetProperty("name").GetString()!.TrimStart('/')
                            && invocation.HttpMethods.Order(StringComparer.Ordinal).SequenceEqual(grant.GetProperty("httpVerb").EnumerateArray()
                                .Select(verb => verb.GetString()!).Order(StringComparer.Ordinal), StringComparer.Ordinal)))
                            throw Failure("undeclared-acl-operation");
                }
                if (completeInventory && !selectedAppIds!.Contains(caller, StringComparer.Ordinal))
                {
                    if (!declaredInvocations!.Any(invocation => invocation.CallerAppId == caller)) throw Failure("undeclared-acl-app-id-or-default-grant");
                    // Preserve exact existing owner declarations for an absent caller; no caller or
                    // operation can be introduced by copying fingerprints from a candidate.
                    continue;
                }
                if (!participant && !completeInventory) throw Failure("undeclared-acl-app-id-or-default-grant");
                foreach (JsonElement grant in policy.GetProperty("operations").EnumerateArray())
                {
                    if (grant.GetProperty("action").GetString() != "allow") continue;
                    string method = grant.GetProperty("name").GetString()!.TrimStart('/');
                    string canonical = method.EndsWith("/*", StringComparison.Ordinal) ? method[..^2] : method;
                    string[] verbs = [.. grant.GetProperty("httpVerb").EnumerateArray().Select(v => v.GetString()!)];
                    if (envelope.SchemaVersion == 3 && method is "ready" or "health" or "alive")
                    {
                        if (verbs.Length != 1 || verbs[0] != "GET" || receiver is null || !receiver.Endpoints.Any(endpoint =>
                            endpoint.Pattern == "/" + method && endpoint.Anonymous && !endpoint.IsPattern
                            && (endpoint.Methods.Count == 0 || endpoint.Methods.SequenceEqual(["GET"], StringComparer.Ordinal))))
                            throw Failure("unqualified-readiness-probe-grant");
                        continue;
                    }
                    // A generated public human query is ingress, with its own SDK filter and
                    // captured owner schemas. Its forwarding BFF gains no authority-store scope.
                    if (completeInventory && RouteCatalogGeneratedRestPolicy.AdmitsGrant(envelope, receiver, method, verbs)) continue;
                    if (completeInventory && RouteCatalogTechnicalHttpPolicy.AdmitsGrant(receiver, method, verbs, methods, participant)) continue;
                    if (!participant) throw Failure("selected-acl-caller-lacks-catalog-enrollment");
                    if (RouteCatalogGeneratedRestPolicy.AdmitsControlGrant(receiver, method, verbs)) continue;
                    if (receiver?.FenceVersion is "eventstore-finite-sdk-http-receiver-v2" or RouteCatalogTechnicalHttpPolicy.FenceVersion
                        && receiver.TechnicalHttp is null
                        && ValidateTechnicalReceiver(receiver, method, verbs, auxiliary ?? [], localAppId, methods)) continue;
                    if (!methods.ContainsKey(canonical) || (canonical != method && (receiver is null
                        || !methods.Keys.Any(child => child.StartsWith(canonical + "/", StringComparison.Ordinal)))))
                        throw Failure("undeclared-acl-operation");
                    string expected = canonical == "internal/routing/catalog-generation" ? "GET" : "POST";
                    if (verbs.Length != 1 || verbs[0] != expected) throw Failure("unbounded-acl-http-verb");
                    if (receiver is not null) ValidateFiniteReceiver(receiver, canonical, verbs[0], methods);
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
        {
            throw Failure("unsupported-or-incomplete-development-topology");
        }
    }

    private static bool ValidateTechnicalReceiver(RouteCatalogReceiverSurface receiver, string grant, string[] verbs,
        IReadOnlyList<RouteCatalogAuxiliaryOperation> auxiliary, string localAppId, IReadOnlyDictionary<string, string> legacy)
    {
        if (RouteCatalogDaprGrant.Prefix(grant, technical: true) is null) throw Failure("unsupported-technical-acl-pattern");
        var reachable = receiver.Endpoints.Where(endpoint => RouteCatalogDaprGrant.Intersects(grant, endpoint.Pattern)
            || RouteCatalogDaprGrant.Describes(grant, endpoint.Pattern)).ToArray();
        // Path prefixes alone do not grant another HTTP verb. Validate the independently
        // captured metadata before filtering, so malformed or contradictory evidence cannot
        // disappear from qualification merely by claiming an unrelated verb.
        foreach (var endpoint in reachable.Where(endpoint => endpoint.Admitted))
        {
            if (endpoint.Methods is not { Count: > 0 } methods || methods.Distinct(StringComparer.Ordinal).Count() != methods.Count
                || methods.Any(method => method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")))
                throw Failure("unqualified-technical-receiver-methods");
            if (endpoint.Http is { } http && !methods.Order(StringComparer.Ordinal)
                .SequenceEqual(http.Verbs.Split(' ').Order(StringComparer.Ordinal), StringComparer.Ordinal))
                throw Failure("technical-receiver-method-evidence-drift");
        }
        var technical = reachable.Where(endpoint => endpoint.Admitted && (endpoint.Http is not null || endpoint.NativeActor is not null || endpoint.DaprCallback is not null)).ToArray();
        if (technical.Length == 0) return false;
        // Intrinsic owner evidence remains mandatory even when this ACL grant uses another
        // verb. Only the current-grant witness check below may depend on that intersection.
        foreach (var endpoint in reachable.Where(endpoint => endpoint.Admitted))
        {
            if (endpoint.DaprCallback is { } callback)
            {
                string expected = callback.Kind == "subscribe" ? "GET" : "POST";
                string path = callback.Kind == "subscribe" ? "dapr/subscribe" : "projections/changed";
                if (endpoint.Http is not null || endpoint.NativeActor is not null || endpoint.Anonymous || endpoint.IsPattern
                    || !endpoint.Policies.SequenceEqual(["EventStoreSidecarChannel"], StringComparer.Ordinal)
                    || callback.Kind is not ("subscribe" or "projection-notification") || endpoint.Pattern.Trim('/') != path
                    || !endpoint.Methods.SequenceEqual([expected], StringComparer.Ordinal) || callback.Verbs != expected
                    || new[] { callback.CodeDigest, callback.FrameworkDigest, callback.InventoryDigest }.Any(digest => digest is not { Length: 64 } || !digest.All(char.IsAsciiHexDigitLower))
                    || !auxiliary.Any(operation => operation.AppId == localAppId && operation.Method == path && operation.Operation == endpoint.Operation && operation.DaprCallback == callback))
                    throw Failure("unbound-sdk-dapr-callback-receiver");
            }
            else if (endpoint.NativeActor is { } native)
            {
                string expected = native.Kind is "config" or "health" ? "GET" : native.Kind == "deactivate" ? "DELETE" : "PUT";
                if (endpoint.Http is not null || endpoint.Anonymous || !endpoint.Policies.SequenceEqual(["EventStoreSidecarChannel"], StringComparer.Ordinal)
                    || !endpoint.Methods.SequenceEqual([expected], StringComparer.Ordinal) || native.Verbs != expected
                    || native.Kind is not ("config" or "health" or "method" or "reminder" or "timer" or "deactivate")
                    || new[] { native.CodeDigest, native.FrameworkDigest, native.InventoryDigest }.Any(digest => digest is not { Length: 64 } || !digest.All(char.IsAsciiHexDigitLower))
                    || !auxiliary.Any(operation => operation.AppId == localAppId && operation.Method == endpoint.Pattern.Trim('/') && operation.Operation == endpoint.Operation && operation.NativeActor == native))
                    throw Failure("unbound-native-actor-receiver");
            }
            else if (endpoint.Http is { } http)
            {
                var peers = reachable.Where(peer => peer.Admitted && peer.Pattern == endpoint.Pattern).ToArray();
                string[] groupedVerbs = peers.SelectMany(peer => peer.Methods).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (peers.Any(peer => peer.Http?.Owner != http.Owner || peer.Http.CodeDigest != http.CodeDigest
                    || peer.Operation != endpoint.Operation))
                    throw Failure("unbound-technical-controller-code-or-policy");
                var groupedWitnesses = peers.SelectMany(peer => peer.Http!.Grants ?? []).Distinct()
                    .OrderBy(witness => witness.Method, StringComparer.Ordinal).ThenBy(witness => witness.Verbs, StringComparer.Ordinal).ToArray();
                if (endpoint.Anonymous || endpoint.Policies.Count == 0 || endpoint.Policies.Any(string.IsNullOrWhiteSpace)
                    || !RouteCatalogSdkHttpPolicy.Allows(http.Owner, endpoint.Pattern, endpoint.Methods, endpoint.Policies)
                    || endpoint.Http.Grants is not { Count: > 0 } witnesses
                    || endpoint.Methods.Count == 0 || !endpoint.Methods.All(verb => http.Verbs.Split(' ').Contains(verb, StringComparer.Ordinal))
                    || http.CodeDigest is not { Length: 64 } || !http.CodeDigest.All(character => char.IsAsciiHexDigitLower(character))
                    || !auxiliary.Any(operation => operation.AppId == localAppId && operation.Method == endpoint.Pattern.Trim('/')
                        && operation.Operation == endpoint.Operation && operation.Http is { } bound
                        && bound.Owner == http.Owner && bound.CodeDigest == http.CodeDigest
                        && witnesses.All(witness => bound.Grants?.Contains(witness) == true)
                        && bound.Verbs.Split(' ').Order(StringComparer.Ordinal).SequenceEqual(groupedVerbs, StringComparer.Ordinal)
                        && bound.Grants is not null && bound.Grants.OrderBy(witness => witness.Method, StringComparer.Ordinal)
                            .ThenBy(witness => witness.Verbs, StringComparer.Ordinal).SequenceEqual(groupedWitnesses)))
                    throw Failure("unbound-technical-controller-code-or-policy");
            }
            else if (endpoint.Anonymous || endpoint.IsPattern || endpoint.Policies.Count == 0 || endpoint.Methods.Count != 1
                || !legacy.ContainsKey(endpoint.Pattern.Trim('/'))
                || endpoint.Methods[0] != (endpoint.Pattern.Trim('/') == "internal/routing/catalog-generation" ? "GET" : "POST"))
                throw Failure("unqualified-nontechnical-receiver-overlap");
        }
        if (verbs.Length == 0 || verbs.Distinct(StringComparer.Ordinal).Count() != verbs.Length
            || verbs.Any(verb => !technical.Any(endpoint => endpoint.Methods.Contains(verb, StringComparer.Ordinal))))
            throw Failure("unqualified-technical-http-verb");
        foreach (var endpoint in technical.Where(endpoint => endpoint.Http is not null
            && endpoint.Methods.Any(method => verbs.Contains(method, StringComparer.Ordinal))))
            if (!endpoint.Http!.Grants!.Any(witness => witness.Method.Trim('/') == grant.Trim('/')
                && verbs.Any(verb => witness.Verbs.Split(' ').Contains(verb, StringComparer.Ordinal))))
                throw Failure("unbound-technical-controller-code-or-policy");
        return true;
    }

    private static RouteCatalogValidationException Failure(string code) => new([new("$.activation.topology", code)]);

    private static void ValidateFiniteReceiver(RouteCatalogReceiverSurface receiver, string prefix, string verb,
        IReadOnlyDictionary<string, string> operations)
    {
        if (receiver.FenceVersion is not ("eventstore-finite-dapr-receiver-v1" or "eventstore-finite-sdk-http-receiver-v2" or RouteCatalogTechnicalHttpPolicy.FenceVersion) || receiver.Endpoints is not { Count: > 0 }
            || receiver.Endpoints.Any(endpoint => endpoint is null || string.IsNullOrEmpty(endpoint.Pattern)
                || endpoint.Methods is null || endpoint.Policies is null))
            throw Failure("missing-effective-finite-receiver-fence");
        string parent = "/" + prefix;
        // Dapr's literal parent grants are prefix grants too. Account for every reachable endpoint,
        // including patterns and anonymous endpoints explicitly denied by the installed receiver fence.
        var reachable = receiver.Endpoints.Where(endpoint => endpoint.Pattern.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || endpoint.Pattern.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase)
            || (endpoint.IsPattern && PatternMayIntersect(endpoint.Pattern, parent))).ToArray();
        if (!reachable.Any(endpoint => endpoint.Pattern.Equals(parent, StringComparison.OrdinalIgnoreCase)
            && endpoint.Admitted && endpoint.Methods.Contains(verb, StringComparer.Ordinal)))
            throw Failure("acl-grant-has-no-finite-authenticated-receiver");
        foreach (var endpoint in reachable.Where(endpoint => endpoint.Admitted))
        {
            string method = endpoint.Pattern.TrimStart('/');
            if (endpoint.Anonymous || endpoint.IsPattern || endpoint.Policies.Count == 0 || endpoint.Policies.Any(string.IsNullOrEmpty)
                || endpoint.Methods.Count != 1 || endpoint.Methods[0] != verb || !operations.TryGetValue(method, out string? operation)
                || operation != endpoint.Operation)
                throw Failure("reachable-receiver-is-not-an-exact-authenticated-operation");
        }
    }

    private static bool PatternMayIntersect(string pattern, string prefix)
    {
        int parameter = pattern.IndexOfAny(['{', '*']);
        string literal = parameter < 0 ? pattern : pattern[..parameter].TrimEnd('/');
        return prefix.Equals(literal, StringComparison.OrdinalIgnoreCase) || prefix.StartsWith(literal + "/", StringComparison.OrdinalIgnoreCase)
            || literal.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }
}
