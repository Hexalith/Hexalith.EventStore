using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Versioned EventStore authorization_details profile for OAuth client-credentials requests.</summary>
public sealed record WorkloadAuthorizationDetails
{
    /// <summary>Gets the collision-resistant profile identifier.</summary>
    public const string Profile = "https://hexalith.com/security/workload-binding/v1";
    /// <summary>Gets the requested profile.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = Profile;
    /// <summary>Gets the single audience.</summary>
    [JsonPropertyName("audience")]
    public string Audience { get; init; } = string.Empty;
    /// <summary>Gets the single operation.</summary>
    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;
    /// <summary>Gets the exact requested binding claims.</summary>
    [JsonPropertyName("bindings")]
    public Dictionary<string, string> Bindings { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Encodes one request without credentials.</summary>
    /// <param name="request">The bound request.</param>
    /// <returns>The JSON parameter value.</returns>
    public static string Encode(WorkloadAssertionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return JsonSerializer.Serialize(new[]
        {
        new WorkloadAuthorizationDetails
        {
            Audience = request.Audience.Trim(), Operation = request.Operation.Trim(),
            Bindings = new Dictionary<string, string>(request.Bindings ?? new Dictionary<string, string>(), StringComparer.Ordinal),
        },
        });
    }

    /// <summary>Decodes a bounded exact profile, rejecting duplicate and unknown JSON properties.</summary>
    /// <param name="json">The form value.</param>
    /// <returns>The request, or null on any malformed or ambiguous detail.</returns>
    public static WorkloadAuthorizationDetails? Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 8192) return null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() != 1) return null;
            JsonElement value = document.RootElement[0];
            if (value.ValueKind != JsonValueKind.Object) return null;
            string[] keys = [.. value.EnumerateObject().Select(property => property.Name)];
            if (keys.Length != 4 || keys.Distinct(StringComparer.Ordinal).Count() != 4
                || keys.Except(["type", "audience", "operation", "bindings"], StringComparer.Ordinal).Any()) return null;
            if (value.GetProperty("type").GetString() != Profile) return null;
            string? audience = value.GetProperty("audience").GetString();
            string? operation = value.GetProperty("operation").GetString();
            if (string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(operation)
                || audience.Length > 256 || operation.Length > 256) return null;
            JsonElement bindings = value.GetProperty("bindings");
            if (bindings.ValueKind != JsonValueKind.Object) return null;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty property in bindings.EnumerateObject())
            {
                if (!property.Name.StartsWith(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal)
                    || property.Name.Length > 128 || property.Value.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(property.Value.GetString()) || property.Value.GetString()!.Length > 256
                    || !result.TryAdd(property.Name, property.Value.GetString()!) || result.Count > 16) return null;
            }
            if (result.Count > 0 && (!result.ContainsKey(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType)
                || !result.ContainsKey(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType))) return null;
            if (result.TryGetValue(QueryCallerAuthorityBinding.DigestClaim, out string? digest)
                && (operation != EventStoreWorkloadOperations.DomainServiceQuery || result.Count != 3
                    || digest.Length != 64 || !digest.All(static character => character is >= '0' and <= '9' or >= 'A' and <= 'F')
                    || result.Values.Any(static binding => binding.Contains('*')))) return null;
            bool commandSource = result.TryGetValue(CommandSourceAuthorityBinding.DigestClaim, out string? commandDigest);
            bool publication = result.TryGetValue(SourcePublicationWorkloadBinding.DigestClaim, out string? publicationDigest);
            if (commandSource || publication)
            {
                if (commandSource && publication || result.ContainsKey(QueryCallerAuthorityBinding.DigestClaim) || result.Count != 6
                    || result.Values.Any(binding => binding.Contains('*'))) return null;
                string selected = (commandSource ? commandDigest : publicationDigest)!;
                if (selected.Length != 64 || !selected.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F')) return null;
                if (commandSource && (operation != EventStoreWorkloadOperations.DomainServiceProcess
                    || !result.ContainsKey(CommandSourceAuthorityBinding.SourceClaim) || !result.ContainsKey(CommandSourceAuthorityBinding.EpochClaim)
                    || !result.TryGetValue(CommandSourceAuthorityBinding.MethodClaim, out string? method) || method != "process")) return null;
                const string prefix = EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix;
                if (publication && (!result.ContainsKey(prefix + "publication-scope") || !result.ContainsKey(prefix + "publication-epoch")
                    || !result.TryGetValue(prefix + "publication-method", out string? publicationMethod)
                    || (operation == SourcePublicationWorkloadBinding.AuthorityOperation
                        ? !new[] { "enroll", "renew-enrollment", "read", "lookup", "transition-admit", "transition-commit", "transition-recover", "reconciliation", "dispatch" }.Contains(publicationMethod, StringComparer.Ordinal)
                        : operation != SourcePublicationWorkloadBinding.PrimaryOperation
                            || !new[] { "read-namespace", "install-namespace", "register-source", "read-index", "write-index", "read-dispatch", "advance-dispatch", "read-reconciliation", "advance-reconciliation" }.Contains(publicationMethod, StringComparer.Ordinal)))) return null;
            }
            return new WorkloadAuthorizationDetails { Audience = audience, Operation = operation, Bindings = result };
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }
}
