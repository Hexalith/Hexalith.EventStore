using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Client.Routing;

/// <summary>Offline construction and deliberately incomplete inspection of checked-in Development inputs.</summary>
public static partial class DevelopmentRouteCatalogBuilder
{
    /// <summary>Reads closed explicit construction inputs; missing properties and unknown fields fail.</summary>
    public static DevelopmentRouteCatalogInput ReadInput(ReadOnlyMemory<byte> bytes) => RouteCatalogCodec.ReadClosedDocument<DevelopmentRouteCatalogInput>(bytes);

    /// <summary>Builds only from complete declarations, bindings, and policy metadata. No runtime discovery or fallback inference occurs.</summary>
    public static RouteCatalogEnvelope Build(DevelopmentRouteCatalogInput input)
    {
        if (input?.Declarations is null || input.Bindings is null || input.Idempotency is null
            || input.Declarations.Count > 4096 || input.Bindings.Count > 4096 || input.Idempotency.Count > 4096)
        {
            throw Failure("$", "missing-or-invalid-input");
        }

        var declarations = new Dictionary<string, RouteContractDeclaration>(StringComparer.Ordinal);
        foreach (RouteContractDeclaration declaration in input.Declarations)
        {
            if (declaration is null)
            {
                throw Failure("$.declarations", "null-entry");
            }

            RouteCatalogValidator.ValidateDeclaration(declaration);
            if (!declarations.TryAdd(declaration.RouteId, declaration))
            {
                throw Failure("$.declarations", "duplicate-route-id");
            }
        }

        var routes = new List<RouteCatalogEntry>();
        for (int index = 0; index < input.Bindings.Count; index++)
        {
            RouteCompositionBinding binding = input.Bindings[index];
            if (binding is null || !declarations.TryGetValue(binding.RouteId ?? string.Empty, out RouteContractDeclaration? declaration))
            {
                throw Failure($"$.bindings[{index}].routeId", "missing-declaration");
            }

            routes.Add(new(declaration.RouteId, input.Generation, RouteCatalogCodec.ComputeDeclarationDigest(declaration),
                binding.AppId, binding.Method, binding.ContractVersion, declaration.CredentialKind, declaration.Operation, binding.IsRuntimeOverride));
        }

        return RouteCatalogCodec.Seal(new(1, input.Generation, "Development", input.AllowRuntimeOverrides,
            input.Declarations, routes, input.Idempotency, string.Empty, string.Empty));
    }

    /// <summary>
    /// Inspects only explicitly whitelisted registration fields from base-to-overlay JSON layers.
    /// It never binds idempotency options, reads keys, scans assemblies, or claims runtime parity.
    /// </summary>
    public static DevelopmentCatalogInspection Inspect(IReadOnlyList<ReadOnlyMemory<byte>> configurationLayers)
    {
        ArgumentNullException.ThrowIfNull(configurationLayers);
        var registrations = new SortedDictionary<string, Dictionary<string, string?>>(StringComparer.Ordinal);
        string[] fields = ["TenantId", "Domain", "AppId", "MethodName", "Version"];
        for (int layer = 0; layer < configurationLayers.Count; layer++)
        {
            if (configurationLayers[layer].Length > RouteCatalogCodec.MaximumByteLength)
            {
                throw Failure($"$.configuration[{layer}]", "size-limit");
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(configurationLayers[layer]);
                if (!document.RootElement.TryGetProperty("EventStore", out JsonElement eventStore)
                    || !eventStore.TryGetProperty("DomainServices", out JsonElement services)
                    || !services.TryGetProperty("Registrations", out JsonElement section))
                {
                    continue;
                }

                foreach (JsonProperty registration in section.EnumerateObject())
                {
                    if (!registrations.TryGetValue(registration.Name, out Dictionary<string, string?>? values))
                    {
                        values = new(StringComparer.Ordinal);
                        registrations.Add(registration.Name, values);
                    }

                    foreach (string field in fields)
                    {
                        if (registration.Value.TryGetProperty(field, out JsonElement value))
                        {
                            values[field] = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                throw Failure($"$.configuration[{layer}]", "invalid-configuration-json");
            }
        }

        var inventory = new List<DevelopmentBindingInventory>();
        var gaps = new List<RouteCatalogDiagnostic>();
        foreach (Dictionary<string, string?> values in registrations.Values)
        {
            string path = $"$.bindings[{inventory.Count}]";
            string SafeField(string name)
            {
                if (!values.TryGetValue(name, out string? value) || value is null || value.Length > 128
                    || !(name == "TenantId" && value == "*") && !SafeBindingField().IsMatch(value))
                {
                    throw Failure(path + "." + name, "missing-or-invalid-safe-binding");
                }

                return value;
            }

            inventory.Add(new(SafeField("TenantId"), SafeField("Domain"), SafeField("AppId"), SafeField("MethodName"), "query", SafeField("Version")));
            foreach (string missing in new[]
            {
                "command-and-query-declarations", "stable-route-ids", "message-id-versions",
                "credential-and-operation-declarations", "projection-declarations-and-bindings",
                "idempotency-adapter-and-operation", "idempotency-policy-version", "descriptor-schema-and-digest",
                "retention-tier", "active-and-reader-generations", "logical-secret-map-and-generation", "consumer-app-identity",
            })
            {
                gaps.Add(new(path, missing));
            }

            if (values["TenantId"] == "system")
            {
                gaps.Add(new(path, "platform-namespace-and-exact-human-permission"));
            }
        }

        gaps.Add(new("$", "complete-declared-profile-required"));
        gaps.Add(new("$", "convention-and-dynamic-fallbacks-not-inventoried"));
        return new(inventory, gaps,
            "Checked-in JSON registrations only; base/Development overlays are merged by registration field. "
            + "No environment, user secrets, AppHost injection, Dapr configuration, assembly declarations, adapter registrations, "
            + "or runtime projection discovery were inspected. Configured key options are not authoritative AD-25 declarations. "
            + "The query method reflects the current gateway query invoker; runtime parity remains unverified. "
            + "Existing system bindings are reported unchanged; their migration belongs to Story 2.14.");
    }

    private static RouteCatalogValidationException Failure(string path, string code) => new([new(path, code)]);

    [GeneratedRegex("\\A[a-z0-9]+(?:[/-][a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex SafeBindingField();
}
