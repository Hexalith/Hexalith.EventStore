using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Constrains whole-snapshot protection to one explicit complete target without granting enrollment authority.</summary>
internal static class SnapshotProtectionTargetReader
{
    /// <summary>Validates bounded snapshot JSON and returns its sole explicit target, or null for a state with no target.</summary>
    internal static ProtectionTarget? Read(byte[] bytes, string tenantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length > PayloadProtectionLimits.CiphertextBytes) { throw new PayloadProtectionFormatException(); }
        using BoundedJsonDocument bounded = BoundedJsonDocument.Inspect(bytes, cancellationToken);
        if (bounded.ContainsProtectedMember || bounded.ContainsLegacyProtectedMember)
        { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
        using JsonDocument document = JsonDocument.Parse(bytes);
        ProtectionTarget? target = null;
        Visit(document.RootElement, tenantId, ref target, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return target;
    }

    private static void Visit(JsonElement value, string tenantId, ref ProtectionTarget? target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray()) { Visit(item, tenantId, ref target, token); }
            return;
        }
        if (value.ValueKind != JsonValueKind.Object) { return; }

        bool hasTarget = false;
        bool hasValue = false;
        bool hasAlias = false;
        foreach (JsonProperty property in value.EnumerateObject())
        {
            token.ThrowIfCancellationRequested();
            hasTarget |= property.Name.Equals("Target", StringComparison.OrdinalIgnoreCase);
            hasValue |= property.Name.Equals("Value", StringComparison.OrdinalIgnoreCase);
            hasAlias |= property.Name.Equals("TargetProtectionKeyAlias", StringComparison.OrdinalIgnoreCase);
            if (property.Name.Equals("ProtectedContent", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Name != "ProtectedContent") { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
                Include(ReadEnvelope(property.Value), tenantId, ref target);
            }
        }

        // ProtectedContent may be stored under a domain member such as Request or Output.
        // Its existing closed Target/Value shape, rather than that member's name, carries ownership.
        if (hasTarget && hasValue && HasProtectionTarget(value, token)) { Include(ReadEnvelope(value), tenantId, ref target); }
        if (hasAlias) { Include(ReadTarget(value), tenantId, ref target); }
        foreach (JsonProperty property in value.EnumerateObject()) { Visit(property.Value, tenantId, ref target, token); }
    }

    private static bool HasProtectionTarget(JsonElement envelope, CancellationToken token)
    {
        foreach (JsonProperty property in envelope.EnumerateObject())
        {
            token.ThrowIfCancellationRequested();
            if (!property.Name.Equals("Target", StringComparison.OrdinalIgnoreCase) || property.Value.ValueKind != JsonValueKind.Object)
            { continue; }
            bool hasTenant = false;
            bool hasInteraction = false;
            foreach (JsonProperty member in property.Value.EnumerateObject())
            {
                token.ThrowIfCancellationRequested();
                if (member.Name.Equals("TargetProtectionKeyAlias", StringComparison.OrdinalIgnoreCase)) { return true; }
                hasTenant |= member.Name.Equals("TenantId", StringComparison.OrdinalIgnoreCase);
                hasInteraction |= member.Name.Equals("AgentInteractionId", StringComparison.OrdinalIgnoreCase);
            }
            if (hasTenant && hasInteraction) { return true; }
        }
        return false;
    }

    private static ProtectionTarget ReadEnvelope(JsonElement envelope)
    {
        if (envelope.ValueKind != JsonValueKind.Object || envelope.EnumerateObject().Count() != 2
            || !envelope.TryGetProperty("Value", out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || !envelope.TryGetProperty("Target", out JsonElement target))
        { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
        return ReadTarget(target);
    }

    private static ProtectionTarget ReadTarget(JsonElement target)
    {
        if (target.ValueKind != JsonValueKind.Object || target.EnumerateObject().Count() != 3
            || !target.TryGetProperty("TenantId", out JsonElement tenant) || tenant.ValueKind != JsonValueKind.String
            || !target.TryGetProperty("AgentInteractionId", out JsonElement interaction) || interaction.ValueKind != JsonValueKind.String
            || !target.TryGetProperty("TargetProtectionKeyAlias", out JsonElement alias) || alias.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(tenant.GetString()) || string.IsNullOrWhiteSpace(interaction.GetString())
            || string.IsNullOrWhiteSpace(alias.GetString()))
        { throw new InvalidOperationException("ProtectionEnvelopeInvalid"); }
        string tenantId = tenant.GetString()!;
        string interactionId = interaction.GetString()!;
        string keyAlias = alias.GetString()!;
        _ = CanonicalText.GetByteCount(tenantId, 1, 2048);
        _ = CanonicalText.GetByteCount(interactionId, 1, 2048);
        _ = CanonicalText.GetByteCount(keyAlias, 1, 2048);
        return new(tenantId, interactionId, keyAlias);
    }

    private static void Include(ProtectionTarget candidate, string tenantId, ref ProtectionTarget? target)
    {
        if (candidate.TenantId != tenantId || target is not null && candidate != target)
        { throw new InvalidOperationException("ProtectionSnapshotTargetMismatch"); }
        target = candidate;
    }
}
