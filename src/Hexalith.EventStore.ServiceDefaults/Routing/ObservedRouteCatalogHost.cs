using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Derives all fingerprints from independent local facts and compares complete declaration and target inventories.</summary>
public sealed class ObservedRouteCatalogHost(Func<CancellationToken, Task<RouteCatalogHostObservation>> capture) : IRouteCatalogHostObserver
{
    /// <summary>Calculates the observed host identity; no candidate-supplied fingerprint participates in a preimage.</summary>
    public static RouteCatalogHostIdentity Identity(RouteCatalogHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (string.IsNullOrWhiteSpace(observation.HostId) || string.IsNullOrWhiteSpace(observation.AppId)
            || observation.Declarations is null || observation.Targets is null || observation.RequiredHostIds is not { Count: > 0 }
            || observation.TopologyDocuments is not { Count: > 0 } || observation.TopologyDocuments.Any(d => d is not { Length: > 0 })
            || observation.AuthenticationProfile is not { Length: > 0 }
            || observation.AuthorityCoordinates is not { StateStore.Length: > 0, StateKey.Length: > 0, HttpEndpoint.Length: > 0 }
            || observation.AdmissionPolicies is null || observation.ProjectionCapabilities is null || observation.AuxiliaryOperations is null
            || observation.ProjectionCapabilities.Any(c => c.AppId != observation.AppId || string.IsNullOrWhiteSpace(c.Fingerprint)
                || c.ProjectionTypes is not { Count: > 0 } || c.ProjectionTypes.Distinct(StringComparer.Ordinal).Count() != c.ProjectionTypes.Count)
            || observation.ProjectionCapabilities.Select(c => (c.Domain, c.ContractVersion)).Distinct().Count() != observation.ProjectionCapabilities.Count
            || observation.AuxiliaryOperations.Any(a => a.AppId != observation.AppId || string.IsNullOrWhiteSpace(a.Operation))
            || observation.AuxiliaryOperations.Select(a => (a.Domain, a.Method)).Distinct().Count() != observation.AuxiliaryOperations.Count)
            throw new RouteCatalogUnavailableException("Independent host inventory, loaded topology and authentication observations are required.", RouteCatalogRefusalReason.Configuration);
        string inventory = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            admission = observation.AdmissionPolicies.OrderBy(a => a.RouteId, StringComparer.Ordinal).Select(RouteCatalogCodec.ComputeIdempotencyPolicyDigest).ToArray(),
            capabilities = observation.ProjectionCapabilities.OrderBy(c => c.Domain, StringComparer.Ordinal).ThenBy(c => c.ContractVersion, StringComparer.Ordinal)
                .Select(c => new { c.AppId, c.Domain, c.ContractVersion, c.Fingerprint, types = c.ProjectionTypes.Order(StringComparer.Ordinal).ToArray() }).ToArray(),
            auxiliary = observation.AuxiliaryOperations.OrderBy(a => a.Domain, StringComparer.Ordinal).ThenBy(a => a.Method, StringComparer.Ordinal).ToArray(),
            declarations = observation.Declarations.OrderBy(d => d.RouteId, StringComparer.Ordinal).Select(RouteCatalogCodec.ComputeDeclarationDigest).ToArray(),
            operations = observation.Operations?.OrderBy(o => o.RouteId, StringComparer.Ordinal).Select(RouteCatalogCodec.ComputeOperationContractDigest).ToArray(),
            targets = observation.Targets.OrderBy(t => t.RouteId, StringComparer.Ordinal).Select(t => new
            { t.RouteId, t.DeclarationDigest, t.AppId, t.Method, t.ContractVersion, t.CredentialKind, t.Operation, t.IsRuntimeOverride, t.ResolverTenantPattern }).ToArray(),
        }));
        string topology = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            hosts = observation.RequiredHostIds.Order(StringComparer.Ordinal).ToArray(),
            documents = observation.TopologyDocuments.Select(Hash).ToArray(),
            authority = observation.AuthorityCoordinates,
            receiver = observation.ReceiverSurface,
        }));
        if (observation.SelectedAppIds is not null || observation.DeclaredInvocations is not null)
        {
            if (observation.SelectedAppIds is null || observation.DeclaredInvocations is null
                || !observation.SelectedAppIds.Contains(observation.AppId, StringComparer.Ordinal)
                || observation.SelectedAppIds.Any(string.IsNullOrWhiteSpace)
                || observation.SelectedAppIds.Distinct(StringComparer.Ordinal).Count() != observation.SelectedAppIds.Count
                || observation.DeclaredInvocations.Any(invocation => string.IsNullOrWhiteSpace(invocation.CallerAppId)
                    || string.IsNullOrWhiteSpace(invocation.Method) || invocation.HttpMethods is not { Count: > 0 }))
                throw new RouteCatalogUnavailableException("An exact selected application and declared caller inventory is required.", RouteCatalogRefusalReason.Configuration);
            topology = Hash(JsonSerializer.SerializeToUtf8Bytes(new
            {
                topology, selectedApps = observation.SelectedAppIds.Order(StringComparer.Ordinal).ToArray(),
                declaredInvocations = observation.DeclaredInvocations.OrderBy(invocation => invocation.CallerAppId, StringComparer.Ordinal)
                    .ThenBy(invocation => invocation.Method, StringComparer.Ordinal).ToArray(),
            }));
        }
        string? executable = null;
        if (observation.ExecutableOwners is { } owners)
        {
            if (owners.Count == 0 || owners.Any(owner => string.IsNullOrWhiteSpace(owner.Key)
                || owner.Value is not { Length: 64 } || !owner.Value.All(char.IsAsciiHexDigitLower)))
                throw new RouteCatalogUnavailableException("The actual executable owner identities are incomplete.", RouteCatalogRefusalReason.Inventory);
            executable = Hash(JsonSerializer.SerializeToUtf8Bytes(owners.OrderBy(owner => owner.Key, StringComparer.Ordinal).ToArray()));
        }
        return new(observation.HostId, observation.AppId, inventory, topology, Hash(observation.AuthenticationProfile)) { ExecutableDigest = executable };
    }

    /// <inheritdoc/>
    public async Task<RouteCatalogHostIdentity> ObserveAsync(RouteCatalogActivation activation, RouteCatalogStateCoordinates coordinates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentNullException.ThrowIfNull(coordinates);
        RouteCatalogHostObservation actual = await capture(cancellationToken).ConfigureAwait(false);
        if (actual.AuthorityCoordinates != coordinates)
            throw new RouteCatalogUnavailableException("Observed authority coordinates disagree with the effective state store.", RouteCatalogRefusalReason.Configuration);
        RouteCatalogHostIdentity identity = Identity(actual);
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(activation.CanonicalCatalog, activation.RootDigest);
        if (envelope.SchemaVersion == 3 && (actual.Operations is null
            || !actual.Operations.Select(o => o.RouteId).Order(StringComparer.Ordinal).SequenceEqual(actual.Declarations.Select(d => d.RouteId).Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || actual.Operations.Any(o => o.Digest != RouteCatalogCodec.ComputeOperationContractDigest(o)
                || !envelope.Operations!.Any(candidate => candidate.RouteId == o.RouteId && candidate.Digest == o.Digest))))
            throw new RouteCatalogUnavailableException("The installed owner operation schemas or eligibility differ from the proposed generation.", RouteCatalogRefusalReason.Inventory);
        if (actual.TopologyDocuments.Count < 2) throw new RouteCatalogUnavailableException("Loaded component and ACL inputs are required.", RouteCatalogRefusalReason.Configuration);
        try
        {
            using JsonDocument component = JsonDocument.Parse(actual.TopologyDocuments[0]);
            if (!component.RootElement.TryGetProperty("metadata", out var metadata)
                || !metadata.TryGetProperty("name", out var name) || name.GetString() != coordinates.StateStore)
                throw new RouteCatalogUnavailableException("Loaded authority component disagrees with the selected state store.", RouteCatalogRefusalReason.Topology);
            RouteCatalogTopologyValidator.ValidateDevelopment(actual.TopologyDocuments[0], actual.TopologyDocuments[1], envelope,
                activation.RequiredHosts, actual.AppId, activation.AuxiliaryOperations, actual.ReceiverSurface,
                actual.SelectedAppIds, actual.DeclaredInvocations);
        }
        catch (RouteCatalogValidationException)
        {
            throw new RouteCatalogUnavailableException("Loaded topology disagrees with the candidate.", RouteCatalogRefusalReason.Topology);
        }
        if (!actual.RequiredHostIds.Order(StringComparer.Ordinal).SequenceEqual(activation.RequiredHosts.Select(h => h.HostId).Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || actual.Declarations.Select(d => d.RouteId).Distinct(StringComparer.Ordinal).Count() != actual.Declarations.Count
            || actual.Targets.Select(t => t.RouteId).Distinct(StringComparer.Ordinal).Count() != actual.Targets.Count
            || actual.Targets.Any(t => !actual.Declarations.Any(d => d.RouteId == t.RouteId
                && RouteCatalogCodec.ComputeDeclarationDigest(d) == t.DeclarationDigest))
            || actual.Declarations.Any(d => !envelope.Declarations.Any(candidate => candidate.RouteId == d.RouteId
                && RouteCatalogCodec.ComputeDeclarationDigest(candidate) == RouteCatalogCodec.ComputeDeclarationDigest(d)))
            || actual.Targets.Any(t => !envelope.Routes.Any(r => r.RouteId == t.RouteId && r.DeclarationDigest == t.DeclarationDigest
                && r.AppId == t.AppId && r.Method == t.Method && r.ContractVersion == t.ContractVersion && r.CredentialKind == t.CredentialKind
                && r.Operation == t.Operation && r.IsRuntimeOverride == t.IsRuntimeOverride && r.ResolverTenantPattern == t.ResolverTenantPattern))
            || envelope.Routes.Where(r => r.AppId == actual.AppId).Any(r => !actual.Targets.Any(t => t.RouteId == r.RouteId)))
            throw new RouteCatalogUnavailableException("The proposed declarations, executable targets or exact host inventory disagree with actual local registrations.", RouteCatalogRefusalReason.Inventory);
        var localAdmission = envelope.Idempotency.Where(a => a.ConsumerAppId == actual.AppId).ToArray();
        if (actual.AdmissionPolicies!.Any(a => a.ConsumerAppId != actual.AppId)
            || actual.AdmissionPolicies!.Select(a => a.RouteId).Distinct(StringComparer.Ordinal).Count() != actual.AdmissionPolicies!.Count
            || localAdmission.Any(a => !actual.AdmissionPolicies!.Any(installed => installed.RouteId == a.RouteId
                && RouteCatalogCodec.ComputeIdempotencyPolicyDigest(installed) == RouteCatalogCodec.ComputeIdempotencyPolicyDigest(a)))
            || envelope.Routes.Where(r => r.AppId == actual.AppId && r.Method == "project/v2").Any(r =>
                !envelope.Declarations.Any(d => d.RouteId == r.RouteId && d.Kind == "projection"
                    && (activation.ProjectionCapabilities ?? []).Any(c => c.AppId == actual.AppId && c.Domain == d.Domain
                        && c.ContractVersion == r.ContractVersion && c.ProjectionTypes.Contains(d.ProjectionType!, StringComparer.Ordinal))))
            || (activation.ProjectionCapabilities ?? []).Where(c => c.AppId == actual.AppId).Any(c =>
                !actual.ProjectionCapabilities!.Any(installed => installed.AppId == c.AppId && installed.Domain == c.Domain
                    && installed.ContractVersion == c.ContractVersion && installed.Fingerprint == c.Fingerprint
                    && installed.ProjectionTypes.Order(StringComparer.Ordinal).SequenceEqual(c.ProjectionTypes.Order(StringComparer.Ordinal), StringComparer.Ordinal)))
            || (activation.AuxiliaryOperations ?? []).Where(a => a.AppId == actual.AppId).Any(a => !actual.AuxiliaryOperations!.Any(installed => JsonSerializer.Serialize(installed) == JsonSerializer.Serialize(a))))
            throw new RouteCatalogUnavailableException("The proposed admission, named projection capability or auxiliary operation disagrees with installed local policy.", RouteCatalogRefusalReason.Inventory);
        return identity;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
