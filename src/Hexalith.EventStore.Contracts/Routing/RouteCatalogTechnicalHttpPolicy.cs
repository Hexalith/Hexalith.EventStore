namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Qualifies existing transport prefixes using the explicit SDK technical composite; never creates domain routes.</summary>
public static class RouteCatalogTechnicalHttpPolicy
{
    /// <summary>The opt-in composite implementation, separate from finite-v1.</summary>
    public const string FenceVersion = "eventstore-sdk-technical-http-composite-v1";

    /// <summary>The auxiliary classification, with no business eligibility.</summary>
    public const string Operation = "sdk-technical-http";

    /// <summary>Bounds an existing grant to unchanged SDK actions, existing core operations and separately guarded controls.</summary>
    public static bool AdmitsGrant(RouteCatalogReceiverSurface? receiver, string grant, IReadOnlyList<string> verbs,
        IReadOnlyDictionary<string, string> operations, bool participant)
    {
        ArgumentNullException.ThrowIfNull(verbs);
        ArgumentNullException.ThrowIfNull(operations);
        if (receiver?.FenceVersion != FenceVersion || receiver.TechnicalHttp is not { Count: > 0 } technical
            || verbs.Count == 0 || verbs.Distinct(StringComparer.Ordinal).Count() != verbs.Count
            || string.IsNullOrWhiteSpace(grant) || grant.IndexOfAny(['?', '#', '\\', '{', '}']) >= 0) return false;
        string prefix = grant.Trim('/');
        if (prefix.EndsWith("/**", StringComparison.Ordinal)) prefix = prefix[..^3];
        else if (prefix.EndsWith("/*", StringComparison.Ordinal)) prefix = prefix[..^2];
        else if (prefix is "*" or "**") prefix = string.Empty;
        if (prefix.Contains('*', StringComparison.Ordinal)) return false;
        bool Reachable(RouteCatalogReceiverEndpoint endpoint) => prefix.Length == 0 || endpoint.Pattern.Trim('/').Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || endpoint.Pattern.Trim('/').StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
            || endpoint.TechnicalGrants?.Any(witness => witness.Method.Trim('/') == grant.Trim('/')
                && witness.Verbs.Split(' ').Any(verb => verbs.Contains(verb, StringComparer.Ordinal))) == true;
        bool Digest(string value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (technical.Select(endpoint => endpoint.Pattern + "|" + string.Join(',', endpoint.Methods)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != technical.Count
            || technical.Any(endpoint => string.IsNullOrWhiteSpace(endpoint.ActionIdentity) || !Digest(endpoint.RequestDigest)
                || !Digest(endpoint.ResponseDigest) || !Digest(endpoint.PolicyDigest) || !Digest(endpoint.FilterDigest)
                || !(endpoint.CredentialKind is null && endpoint.Policies.Contains("EventStoreHuman", StringComparer.Ordinal)
                    || endpoint.CredentialKind == "workload" && endpoint.Policies.Count == 1 && endpoint.Policies[0].StartsWith("DaprInternal:", StringComparison.Ordinal)
                        && endpoint.Policies[0].Length > "DaprInternal:".Length)
                || !receiver.Endpoints.Any(actual => actual.Pattern == endpoint.Pattern && actual.Admitted && !actual.Anonymous
                    && actual.Operation == Operation && actual.Methods.SequenceEqual(endpoint.Methods, StringComparer.Ordinal)
                    && actual.Policies.SequenceEqual(endpoint.Policies, StringComparer.Ordinal)))) return false;
        var reachable = receiver.Endpoints.Where(endpoint => endpoint.Admitted && Reachable(endpoint)
            && endpoint.Methods.Any(verb => verbs.Contains(verb, StringComparer.Ordinal))).ToArray();
        if (reachable.Length == 0) return false;
        foreach (var endpoint in reachable)
        {
            if (endpoint.Operation == Operation && technical.Any(actual => actual.Pattern.Equals(endpoint.Pattern, StringComparison.OrdinalIgnoreCase) && actual.Methods.SequenceEqual(endpoint.Methods, StringComparer.Ordinal))) continue;
            if (endpoint.NativeActor is { } native && !endpoint.Anonymous && endpoint.Methods.SequenceEqual([native.Verbs], StringComparer.Ordinal)
                && endpoint.Policies.SequenceEqual(["EventStoreSidecarChannel"], StringComparer.Ordinal)
                && endpoint.Operation == "sdk-native-actor:" + native.Kind && Digest(native.CodeDigest) && Digest(native.FrameworkDigest) && Digest(native.InventoryDigest)
                && (native.Kind, native.Verbs, endpoint.Pattern) is ("config", "GET", "/dapr/config") or ("health", "GET", "/healthz")
                    or ("deactivate", "DELETE", "/actors/{actorTypeName}/{actorId}")
                    or ("method", "PUT", "/actors/{actorTypeName}/{actorId}/method/{methodName}")
                    or ("reminder", "PUT", "/actors/{actorTypeName}/{actorId}/method/remind/{reminderName}")
                    or ("timer", "PUT", "/actors/{actorTypeName}/{actorId}/method/timer/{timerName}")) continue;
            if (endpoint.DaprCallback is { } callback && !endpoint.Anonymous && endpoint.Methods.SequenceEqual([callback.Verbs], StringComparer.Ordinal)
                && endpoint.Policies.SequenceEqual(["EventStoreSidecarChannel"], StringComparer.Ordinal)
                && endpoint.Operation == "sdk-dapr-callback:" + callback.Kind && Digest(callback.CodeDigest) && Digest(callback.FrameworkDigest) && Digest(callback.InventoryDigest)
                && (callback.Kind, callback.Verbs, endpoint.Pattern) is ("subscribe", "GET", "/dapr/subscribe") or ("projection-notification", "POST", "/projections/changed")) continue;
            if (endpoint.Anonymous || endpoint.IsPattern || endpoint.Methods.Count != 1
                || !operations.TryGetValue(endpoint.Pattern.TrimStart('/'), out string? operation) || operation != endpoint.Operation) return false;
            bool control = endpoint.Operation is Authorization.EventStoreOperationIdentifiers.RouteCatalogActivate
                or Authorization.EventStoreOperationIdentifiers.RouteCatalogRead;
            if (!participant && !control) return false;
        }
        return true;
    }
}
