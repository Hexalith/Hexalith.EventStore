using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Owns bounded copies of every deterministic binding input, excluding the independently verified trust proof.</summary>
internal sealed class RouteCatalogBindingInput
{
    private const int MaximumBytes = 4 * 1024 * 1024;
    private const int MaximumItems = 4096;

    private RouteCatalogBindingInput(RouteCatalogActivation activation, int bytes)
    {
        Activation = activation;
        Bytes = bytes;
    }

    /// <summary>Gets the private graph supplied only to the unchanged binding computation.</summary>
    internal RouteCatalogActivation Activation { get; }

    /// <summary>Gets the conservative retained size, including UTF-16 strings and collection storage.</summary>
    internal int Bytes { get; }

    /// <summary>Captures supported finite graphs; malformed or oversized inputs retain the ordinary computation path.</summary>
    internal static bool TryCapture(RouteCatalogActivation source, out RouteCatalogBindingInput? captured)
    {
        captured = null;
        // Include the activation, input wrapper and collection/record object headers.
        // Strings are immutable, but charging their complete UTF-16 size is deliberately conservative.
        long bytes = 512;
        bool Text(params string?[] values)
        {
            foreach (string? value in values) bytes += 24L + (value?.Length ?? 0) * 2L;
            return bytes <= MaximumBytes;
        }
        bool Collection<T>(IReadOnlyList<T>? values, bool optional = false)
        {
            if (values is null) return optional;
            Type type = values.GetType();
            if (type != typeof(T[]) && type != typeof(List<T>) && type != typeof(System.Collections.ObjectModel.ReadOnlyCollection<T>)) return false;
            bytes += 32L + values.Count * 128L;
            return values.Count is >= 0 and <= MaximumItems && bytes <= MaximumBytes;
        }
        if (source.CanonicalCatalog is not { Length: > 0 and <= MaximumBytes } catalog
            || !Text(source.AttemptId, source.RootDigest, source.PredecessorRootDigest, source.Owner)
            || !Collection(source.RequiredHosts) || !Collection(source.AuxiliaryOperations, true)
            || !Collection(source.ProjectionCapabilities, true) || !Collection(source.TechnicalHttpReceivers, true)) return false;
        bytes += catalog.Length;
        if (bytes > MaximumBytes) return false;

        List<RouteCatalogHostIdentity> hosts = [];
        foreach (var host in source.RequiredHosts)
        {
            if (host is null || !Text(host.HostId, host.AppId, host.InventoryDigest, host.TopologyDigest,
                host.AuthenticationFingerprint, host.ExecutableDigest)) return false;
            hosts.Add(host with { });
        }
        List<RouteCatalogAuxiliaryOperation>? auxiliary = source.AuxiliaryOperations is null ? null : [];
        foreach (var operation in source.AuxiliaryOperations ?? [])
        {
            if (operation is null || !Text(operation.AppId, operation.Method, operation.Operation, operation.Domain)) return false;
            var http = operation.Http;
            if (http is not null)
            {
                if (!Text(http.Owner, http.Verbs, http.CodeDigest) || !Collection(http.Grants, true)) return false;
                foreach (var grant in http.Grants ?? [])
                    if (grant is null || !Text(grant.Method, grant.Verbs)) return false;
                http = http with { Grants = http.Grants?.Select(grant => grant with { }).ToArray() };
            }
            if (operation.NativeActor is { } native && !Text(native.Kind, native.Verbs, native.CodeDigest, native.FrameworkDigest, native.InventoryDigest)
                || operation.DaprCallback is { } callback && !Text(callback.Kind, callback.Verbs, callback.CodeDigest, callback.FrameworkDigest, callback.InventoryDigest)
                || operation.DomainAuxiliary is { } domain && !Text(domain.Owner, domain.RequestType, domain.ScopeMethod, domain.CodeDigest)
                || operation.Sidecar is { } sidecar && !Text(sidecar.Owner, sidecar.CodeDigest, sidecar.PubSubName, sidecar.TopicName)) return false;
            auxiliary!.Add(operation with
            {
                Http = http,
                NativeActor = operation.NativeActor is null ? null : operation.NativeActor with { },
                DaprCallback = operation.DaprCallback is null ? null : operation.DaprCallback with { },
                DomainAuxiliary = operation.DomainAuxiliary is null ? null : operation.DomainAuxiliary with { },
                Sidecar = operation.Sidecar is null ? null : operation.Sidecar with { },
            });
        }
        List<RouteCatalogProjectionCapability>? capabilities = source.ProjectionCapabilities is null ? null : [];
        foreach (var capability in source.ProjectionCapabilities ?? [])
        {
            if (capability is null || !Collection(capability.ProjectionTypes)
                || !Text(capability.AppId, capability.Domain, capability.ContractVersion, capability.Fingerprint)
                || !Text(capability.ProjectionTypes.ToArray())) return false;
            capabilities!.Add(capability with { ProjectionTypes = capability.ProjectionTypes.ToArray() });
        }
        List<RouteCatalogTechnicalHttpReceiver>? receivers = source.TechnicalHttpReceivers is null ? null : [];
        foreach (var receiver in source.TechnicalHttpReceivers ?? [])
        {
            if (receiver is null || !Text(receiver.AppId, receiver.FenceVersion) || !Collection(receiver.Endpoints)
                || !Collection(receiver.Invocations, true)) return false;
            List<RouteCatalogTechnicalHttpEndpoint> endpoints = [];
            foreach (var endpoint in receiver.Endpoints)
            {
                if (endpoint is null || !Collection(endpoint.Methods) || !Collection(endpoint.Policies)
                    || !Text(endpoint.Pattern, endpoint.ActionIdentity, endpoint.RequestDigest, endpoint.ResponseDigest,
                        endpoint.PolicyDigest, endpoint.FilterDigest, endpoint.CredentialKind)
                    || !Text(endpoint.Methods.ToArray()) || !Text(endpoint.Policies.ToArray())) return false;
                endpoints.Add(endpoint with { Methods = endpoint.Methods.ToArray(), Policies = endpoint.Policies.ToArray() });
            }
            List<RouteCatalogDeclaredInvocation>? invocations = receiver.Invocations is null ? null : [];
            foreach (var invocation in receiver.Invocations ?? [])
            {
                if (invocation is null || !Collection(invocation.HttpMethods) || !Text(invocation.CallerAppId, invocation.Method)
                    || !Text(invocation.HttpMethods.ToArray())) return false;
                invocations!.Add(invocation with { HttpMethods = invocation.HttpMethods.ToArray() });
            }
            receivers!.Add(receiver with { Endpoints = endpoints.ToArray(), Invocations = invocations?.ToArray() });
        }
        captured = new(source with
        {
            CanonicalCatalog = catalog.ToArray(), RequiredHosts = hosts.ToArray(), AuxiliaryOperations = auxiliary?.ToArray(),
            ProjectionCapabilities = capabilities?.ToArray(), TechnicalHttpReceivers = receivers?.ToArray(), TrustProof = string.Empty,
        }, (int)bytes);
        return true;
    }

    /// <summary>Compares every captured value; root or object identity alone never establishes reuse.</summary>
    internal bool Matches(RouteCatalogBindingInput other)
    {
        RouteCatalogActivation left = Activation, right = other.Activation;
        return left.AttemptId == right.AttemptId && left.RootDigest == right.RootDigest && left.PredecessorRootDigest == right.PredecessorRootDigest
            && left.Owner == right.Owner && left.CanonicalCatalog.AsSpan().SequenceEqual(right.CanonicalCatalog)
            && Equal(left.RequiredHosts, right.RequiredHosts, (a, b) => a == b)
            && Equal(left.AuxiliaryOperations, right.AuxiliaryOperations, (a, b) => a.AppId == b.AppId && a.Method == b.Method
                && a.Operation == b.Operation && a.Domain == b.Domain && a.NativeActor == b.NativeActor && a.DaprCallback == b.DaprCallback
                && a.DomainAuxiliary == b.DomainAuxiliary && a.Sidecar == b.Sidecar && HttpEqual(a.Http, b.Http))
            && Equal(left.ProjectionCapabilities, right.ProjectionCapabilities, (a, b) => a.AppId == b.AppId && a.Domain == b.Domain
                && a.ContractVersion == b.ContractVersion && a.Fingerprint == b.Fingerprint && Equal(a.ProjectionTypes, b.ProjectionTypes, (c, d) => c == d))
            && Equal(left.TechnicalHttpReceivers, right.TechnicalHttpReceivers, (a, b) => a.AppId == b.AppId && a.FenceVersion == b.FenceVersion
                && Equal(a.Endpoints, b.Endpoints, EndpointEqual)
                && Equal(a.Invocations, b.Invocations, (c, d) => c.CallerAppId == d.CallerAppId && c.Method == d.Method
                    && Equal(c.HttpMethods, d.HttpMethods, (e, f) => e == f)));
    }

    private static bool HttpEqual(RouteCatalogHttpTransport? left, RouteCatalogHttpTransport? right)
        => left is null || right is null ? left == right : left.Owner == right.Owner && left.Verbs == right.Verbs && left.CodeDigest == right.CodeDigest
            && Equal(left.Grants, right.Grants, (a, b) => a == b);

    private static bool EndpointEqual(RouteCatalogTechnicalHttpEndpoint left, RouteCatalogTechnicalHttpEndpoint right)
        => left.Pattern == right.Pattern && left.ActionIdentity == right.ActionIdentity && left.RequestDigest == right.RequestDigest
            && left.ResponseDigest == right.ResponseDigest && left.PolicyDigest == right.PolicyDigest && left.FilterDigest == right.FilterDigest
            && left.CredentialKind == right.CredentialKind && Equal(left.Methods, right.Methods, (a, b) => a == b)
            && Equal(left.Policies, right.Policies, (a, b) => a == b);

    private static bool Equal<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right, Func<T, T, bool> equal)
    {
        if (left is null || right is null) return left is null && right is null;
        if (left.Count != right.Count) return false;
        for (int index = 0; index < left.Count; index++) if (!equal(left[index], right[index])) return false;
        return true;
    }
}
