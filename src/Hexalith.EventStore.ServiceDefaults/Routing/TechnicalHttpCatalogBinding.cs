using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Validates and deeply freezes explicit technical membership without changing executable catalog ownership.</summary>
internal static class TechnicalHttpCatalogBinding
{
    internal static void Validate(RouteCatalogActivation activation)
    {
        var receivers = activation.TechnicalHttpReceivers ?? [];
        if (receivers.Count > 256 || receivers.Any(receiver => receiver is null) || receivers.Select(receiver => receiver.AppId).Distinct(StringComparer.Ordinal).Count() != receivers.Count
            || receivers.Any(receiver => !activation.RequiredHosts.Any(host => host.AppId == receiver.AppId)
                || receiver.FenceVersion != RouteCatalogTechnicalHttpPolicy.FenceVersion || receiver.Endpoints is not { Count: > 0 and <= 256 }
                || receiver.Endpoints.Any(endpoint => !Valid(endpoint))
                || receiver.Endpoints.Select(Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != receiver.Endpoints.Count
                || receiver.Invocations is { } invocations && (invocations.Count > 1024 || invocations.Any(invocation => !Valid(invocation))
                    || invocations.Select(InvocationKey).Distinct(StringComparer.Ordinal).Count() != invocations.Count)))
            {
            string? duplicate = receivers.Where(receiver => receiver?.Endpoints is not null)
                .SelectMany(receiver => receiver.Endpoints.Where(endpoint => endpoint is not null && endpoint.Methods is not null).GroupBy(Key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
                .FirstOrDefault()?.Key;
            throw new RouteCatalogUnavailableException("Technical receiver membership is incomplete, duplicated or unsupported."
                + (duplicate is null ? string.Empty : " Duplicate action: " + duplicate), RouteCatalogRefusalReason.Inventory);
        }
    }

    internal static bool Equivalent(RouteCatalogTechnicalHttpReceiver left, RouteCatalogTechnicalHttpReceiver right)
        => left.AppId == right.AppId && left.FenceVersion == right.FenceVersion && left.Endpoints.Count == right.Endpoints.Count
            && left.Endpoints.OrderBy(Key, StringComparer.Ordinal).Select(Encode).SequenceEqual(right.Endpoints.OrderBy(Key, StringComparer.Ordinal).Select(Encode), StringComparer.Ordinal)
            && (left.Invocations is null) == (right.Invocations is null)
            && (left.Invocations ?? []).OrderBy(InvocationKey, StringComparer.Ordinal).Select(EncodeInvocation)
                .SequenceEqual((right.Invocations ?? []).OrderBy(InvocationKey, StringComparer.Ordinal).Select(EncodeInvocation), StringComparer.Ordinal);

    internal static bool Equivalent(RouteCatalogTechnicalHttpEndpoint left, RouteCatalogTechnicalHttpEndpoint right) => Encode(left) == Encode(right);

    internal static IReadOnlyList<RouteCatalogTechnicalHttpReceiver>? Freeze(IReadOnlyList<RouteCatalogTechnicalHttpReceiver>? receivers)
    {
        if (receivers is null) return null;
        if (receivers.Any(receiver => receiver is null || receiver.Endpoints is null
            || receiver.Endpoints.Any(endpoint => endpoint is null || endpoint.Methods is null || endpoint.Policies is null)
            || receiver.Invocations?.Any(invocation => invocation is null || invocation.HttpMethods is null) == true))
            throw new RouteCatalogUnavailableException("Technical receiver membership contains an incomplete collection.", RouteCatalogRefusalReason.Inventory);
        return Array.AsReadOnly(receivers.Select(receiver => receiver with
        {
            Endpoints = Array.AsReadOnly(receiver.Endpoints.Select(endpoint => endpoint with
            {
                Methods = Array.AsReadOnly(endpoint.Methods.ToArray()), Policies = Array.AsReadOnly(endpoint.Policies.ToArray()),
            }).ToArray()),
            Invocations = receiver.Invocations is null ? null : Array.AsReadOnly(receiver.Invocations.Select(invocation => invocation with
            { HttpMethods = Array.AsReadOnly(invocation.HttpMethods.ToArray()) }).ToArray()),
        }).ToArray());
    }

    internal static bool Matches(IReadOnlyList<RouteCatalogTechnicalHttpReceiver> receivers, string appId, string method, string verb, string? callerAppId)
        => receivers.Any(receiver => receiver.AppId == appId && receiver.FenceVersion == RouteCatalogTechnicalHttpPolicy.FenceVersion
            && receiver.Invocations?.Any(invocation => invocation.CallerAppId == callerAppId && invocation.HttpMethods.Contains(verb, StringComparer.Ordinal)
                && DevelopmentTechnicalHttpTransport.GrantAllows(invocation.Method, method)) == true
            && receiver.Endpoints.Any(endpoint => endpoint.Methods.Contains(verb, StringComparer.Ordinal)
                && TechnicalHttpRouteTemplate.Matches(endpoint.Pattern, method, verb)));

    internal static bool Contains(IReadOnlyList<RouteCatalogTechnicalHttpReceiver> receivers, string appId, RouteCatalogTechnicalHttpEndpoint endpoint)
        => receivers.Any(receiver => receiver.AppId == appId && receiver.FenceVersion == RouteCatalogTechnicalHttpPolicy.FenceVersion
            && receiver.Endpoints.Any(actual => Equivalent(actual, endpoint)));

    internal static string Key(RouteCatalogTechnicalHttpEndpoint endpoint) => endpoint.Pattern + "|" + string.Join(',', endpoint.Methods);

    private static string Encode(RouteCatalogTechnicalHttpEndpoint endpoint) => JsonSerializer.Serialize(endpoint);
    internal static string InvocationKey(RouteCatalogDeclaredInvocation invocation) => invocation.CallerAppId + "|" + invocation.Method;
    private static string EncodeInvocation(RouteCatalogDeclaredInvocation invocation) => JsonSerializer.Serialize(invocation with { HttpMethods = invocation.HttpMethods.Order(StringComparer.Ordinal).ToArray() });

    private static bool Valid(RouteCatalogDeclaredInvocation invocation) => invocation is not null && invocation.CallerAppId is { Length: > 0 and <= 256 }
        && invocation.Method is { Length: > 0 and <= 512 } && invocation.Method.IndexOfAny(['?', '#', '\\', '{', '}']) < 0
        && invocation.HttpMethods is { Count: > 0 and <= 7 } && invocation.HttpMethods.Distinct(StringComparer.Ordinal).Count() == invocation.HttpMethods.Count
        && invocation.HttpMethods.All(verb => verb is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS");

    private static bool Digest(string value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool Valid(RouteCatalogTechnicalHttpEndpoint endpoint)
        => endpoint is not null && endpoint.Pattern is { Length: > 0 and <= 512 } && endpoint.Pattern.StartsWith('/')
            && !endpoint.Pattern.Contains('*', StringComparison.Ordinal) && endpoint.ActionIdentity is { Length: > 0 and <= 8192 }
            && endpoint.Methods is { Count: 1 } && endpoint.Methods[0] is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS"
            && endpoint.Policies is { Count: > 0 and <= 4 }
            && (endpoint.CredentialKind is null && endpoint.Policies.Count(policy => policy == "EventStoreHuman") == 1
                && endpoint.Policies.All(policy => policy is "EventStoreHuman" or "AdminReadOnly" or "AdminOperator" or "AdminFull")
                || endpoint.CredentialKind == "workload" && endpoint.Policies.Count == 1 && endpoint.Policies[0].StartsWith("DaprInternal:", StringComparison.Ordinal)
                    && endpoint.Policies[0].Length > "DaprInternal:".Length)
            && endpoint.Policies.Distinct(StringComparer.Ordinal).Count() == endpoint.Policies.Count
            && Digest(endpoint.RequestDigest) && Digest(endpoint.ResponseDigest) && Digest(endpoint.PolicyDigest) && Digest(endpoint.FilterDigest);
}
