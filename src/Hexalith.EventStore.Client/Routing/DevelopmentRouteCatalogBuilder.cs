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
                binding.AppId, binding.Method, binding.ContractVersion, declaration.CredentialKind, declaration.Operation, binding.IsRuntimeOverride, binding.ResolverTenantPattern));
        }

        return RouteCatalogCodec.Seal(new(input.Declarations.Any(d => d.Execution is not null) ? 2 : 1, input.Generation, "Development", input.AllowRuntimeOverrides,
            input.Declarations, routes, input.Idempotency, string.Empty, string.Empty));
    }

    /// <summary>
    /// Inspects only explicitly whitelisted registration fields from base-to-overlay JSON layers.
    /// It never binds idempotency options, reads keys, scans assemblies, or claims runtime parity.
    /// Flattened colon-delimited configuration property paths are unsupported and rejected.
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
                ReadOnlyMemory<byte> bytes = configurationLayers[layer];
                if (bytes.Span.StartsWith("\uFEFF"u8))
                {
                    bytes = bytes[3..];
                }

                using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                ValidateConfigurationProperties(document.RootElement, $"$.configuration[{layer}]");
                if (!TryGetConfigurationProperty(document.RootElement, "EventStore", out JsonElement eventStore)
                    || !TryGetConfigurationProperty(eventStore, "DomainServices", out JsonElement services)
                    || !TryGetConfigurationProperty(services, "Registrations", out JsonElement section))
                {
                    continue;
                }

                foreach (JsonProperty registration in section.EnumerateObject())
                {
                    // Configuration property lookup ignores case, but the resolver's registration keys do not.
                    if (!string.Equals(registration.Name, registration.Name.ToLowerInvariant(), StringComparison.Ordinal))
                    {
                        throw Failure($"$.configuration[{layer}].selector", "selector-value-mismatch-or-unsupported-selector");
                    }

                    if (!registrations.TryGetValue(registration.Name, out Dictionary<string, string?>? values))
                    {
                        values = new(StringComparer.OrdinalIgnoreCase);
                        registrations.Add(registration.Name, values);
                    }

                    foreach (string field in fields)
                    {
                        if (TryGetConfigurationProperty(registration.Value, field, out JsonElement value))
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
        foreach ((string selector, Dictionary<string, string?> values) in registrations)
        {
            string path = $"$.bindings[{inventory.Count}]";
            string SafeField(string name)
            {
                if (!values.TryGetValue(name, out string? value) || value is null || value.Length > 128)
                {
                    throw Failure(path + "." + name, "missing-or-invalid-safe-binding");
                }

                bool valid = name switch
                {
                    "Domain" => value.Length <= 64 && DomainPattern().IsMatch(value),
                    "AppId" => DomainPattern().IsMatch(value),
                    "TenantId" when value == "*" => true,
                    _ => SafeBindingField().IsMatch(value),
                };
                if (!valid)
                {
                    throw Failure(path + "." + name, "missing-or-invalid-safe-binding");
                }

                return value;
            }

            string tenant = SafeField("TenantId");
            string domain = SafeField("Domain");
            string version = SafeField("Version");
            string pipeSelector = $"{tenant}|{domain}|{version}";
            string sanitizedSelector = $"wildcard_{domain}_{version}";
            if (!string.Equals(selector, pipeSelector, StringComparison.Ordinal)
                && !(tenant == "*" && string.Equals(selector, sanitizedSelector, StringComparison.Ordinal)))
            {
                throw Failure(path + ".selector", "selector-value-mismatch-or-unsupported-selector");
            }

            inventory.Add(new(tenant, domain, SafeField("AppId"), SafeField("MethodName"), "query", version));
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
            "Checked-in JSON registrations only; base/Development overlays are merged case-insensitively by registration field. "
            + "Only canonical matching pipe selectors and sanitized wildcard selectors are supported. "
            + "Flattened colon-delimited configuration property paths are unsupported and rejected. "
            + "No environment, user secrets, AppHost injection, Dapr configuration, assembly declarations, adapter registrations, "
            + "or runtime projection discovery were inspected. Configured key options are not authoritative AD-25 declarations. "
            + "The query method reflects the current gateway query invoker; runtime parity remains unverified. "
            + "Existing system bindings are reported unchanged; their migration belongs to Story 2.14.");
    }

    private static RouteCatalogValidationException Failure(string path, string code) => new([new(path, code)]);

    private static bool TryGetConfigurationProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static void ValidateConfigurationProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Contains(':'))
                {
                    throw Failure(path, "unsupported-flattened-configuration-path");
                }

                if (!names.Add(property.Name))
                {
                    throw Failure(path, "duplicate-configuration-key");
                }

                ValidateConfigurationProperties(property.Value, path);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                ValidateConfigurationProperties(item, path);
            }
        }
    }

    [GeneratedRegex("\\A[a-z0-9]+(?:[/-][a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex SafeBindingField();

    [GeneratedRegex("\\A[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();
}
