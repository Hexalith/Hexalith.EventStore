using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;

/// <summary>Applies the exact approved client-identifier-only exception to structured unit and live observations.</summary>
internal static partial class AzureIdentityDiagnosticPolicy
{
    /// <summary>The exact owner-approved normative policy digest.</summary>
    internal const string Digest = "a0a746aeab4de17c1b1b95cfa37fbf42a119e29af0c7354fe174d09512b389d9";

    /// <summary>Classifies complete payloads before considering the narrowly enrolled identifier exception.</summary>
    internal static string Classify(AzureDiagnosticRecord record, IReadOnlyList<string> configuredClientIds, IReadOnlyList<string> prohibitedValues)
    {
        if (record.Payload.Any(value => value.Contains(AzureDiagnosticRecord.UnreadablePayload, StringComparison.Ordinal)))
            return "unreadable-payload";
        string content = record.Content;
        string[] views = Views(content).ToArray();
        if (prohibitedValues.Where(value => !string.IsNullOrEmpty(value))
            .Any(value => Forms(value).Any(form => views.Any(view => view.Contains(form, StringComparison.Ordinal)))))
            return "prohibited-payload";

        Guid[] configured = configuredClientIds.Select(value => Guid.TryParse(value, out Guid parsed) ? parsed : (Guid?)null)
            .Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        bool enrolled = Guid.TryParse(record.SelectedClientId, out Guid selected) && configured.Contains(selected);
        Guid[] rawIds = Identifiers(content).ToArray();
        bool knownPresent = rawIds.Any(configured.Contains);
        foreach (Guid identifier in configured)
        {
            string[] spellings = Spellings(identifier).ToArray();
            if (spellings.SelectMany(Forms).Where(form => !spellings.Contains(form, StringComparer.Ordinal))
                .Any(form => content.Contains(form, StringComparison.Ordinal))
                || !knownPresent && views.Skip(1).SelectMany(Identifiers).Contains(identifier))
                return "encoded-identifier";
        }

        bool vendor = record.Channel == "EventSource" && record.Source == "Azure-Identity";
        bool event24 = vendor && record.Name == "LogMsalAlways" && record.EventId == 24;
        bool event26 = vendor && record.Name == "ManagedIdentityCredentialSelected" && record.EventId == 26;
        if (event24 || event26)
        {
            if (record.Payload.Count != (event24 ? 1 : 2)) return "vendor-schema";
            string[] identityPayload = record.Payload.ToArray();
            if (event24)
            {
                // Pinned MSAL4.92 Formatter: False MSAL ... [UTC-u timestamp - correlationId]clientInfo message.
                // The source constructor supplies this nonprotected per-request correlation field, not a client identity.
                Match envelope = MsalCorrelationEnvelopePattern().Match(identityPayload[0]);
                if (envelope.Success && DateTime.TryParseExact(envelope.Groups["utc"].Value, "u",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    Group correlation = envelope.Groups["correlation"];
                    if (configured.Contains(Guid.Parse(correlation.Value))) return "identifier-channel";
                    identityPayload[0] = identityPayload[0].Remove(correlation.Index, correlation.Length);
                }
            }
            Guid[] payloadIds = identityPayload.SelectMany(Views).SelectMany(Identifiers).ToArray();
            if (payloadIds.Length != 0 && (!enrolled || payloadIds.Any(identifier => identifier != selected)))
                return "unapproved-identifier";
            if (event26 && (string.IsNullOrWhiteSpace(record.Payload[0])
                || Identifiers(record.Payload[0]).Any() || !OnlySelectedIdentity(record.Payload[1], selected)))
                return "vendor-schema";
            if (payloadIds.Length != 0) return "permitted-selected-identifier";
            return "safe";
        }
        return knownPresent ? "identifier-channel" : "safe";
    }

    /// <summary>Every consumer refuses capture failures before inspecting any text.</summary>
    internal static void RequireCaptureIntegrity(IEnumerable<AzureDiagnosticRecord> records)
    {
        if (records.Any(record => record.Payload.Any(value => value.Contains(AzureDiagnosticRecord.UnreadablePayload, StringComparison.Ordinal))))
            throw new InvalidOperationException("Azure diagnostic capture refused an unreadable payload.");
    }

    /// <summary>The native sentinel must observe both real selected-identity event kinds; absence is never safety evidence.</summary>
    internal static string AssertNativeEvidence(IEnumerable<AzureDiagnosticRecord> records, IReadOnlyList<string> configuredClientIds,
        IReadOnlyList<string> prohibitedValues)
    {
        AzureDiagnosticRecord[] observed = records.ToArray();
        foreach ((string name, int id) in new[] { ("LogMsalAlways", 24), ("ManagedIdentityCredentialSelected", 26) })
            if (!observed.Any(record => record.Name == name && record.EventId == id
                && Classify(record, configuredClientIds, prohibitedValues) == "permitted-selected-identifier"))
            {
                string categories = string.Join(",", observed.Where(record => record.Channel == "EventSource" && record.Source == "Azure-Identity" && record.Name == name && record.EventId == id)
                    .GroupBy(record => Classify(record, configuredClientIds, prohibitedValues)).Select(group => group.Key + ":" + group.Count()));
                string shapes = string.Join(";", observed.Where(record => record.Channel == "EventSource" && record.Source == "Azure-Identity" && record.Name == name && record.EventId == id).Select(record =>
                {
                    Guid.TryParse(record.SelectedClientId, out Guid selected);
                    Guid[] ids = record.Payload.SelectMany(Identifiers).ToArray();
                    return $"fields={record.Payload.Count},selected={ids.Count(value => value == selected)},empty={ids.Count(value => value == Guid.Empty)},other={ids.Count(value => value != selected && value != Guid.Empty)},correlationLabel={record.Content.Contains("correlation", StringComparison.OrdinalIgnoreCase)}";
                }));
                throw new InvalidOperationException($"Azure diagnostic sentinel requires selected-identity event{id} evidence; categories={categories}; shapes={shapes}.");
            }
        return AssertSafe(observed, configuredClientIds, prohibitedValues);
    }

    /// <summary>Rejects private observations with safe categories/fingerprints and returns only closed event counts.</summary>
    internal static string AssertSafe(IEnumerable<AzureDiagnosticRecord> records, IReadOnlyList<string> configuredClientIds,
        IReadOnlyList<string> prohibitedValues)
    {
        int event24 = 0, event26 = 0;
        foreach (AzureDiagnosticRecord record in records)
        {
            string classification = Classify(record, configuredClientIds, prohibitedValues);
            if (classification is not ("safe" or "permitted-selected-identifier"))
                throw new InvalidOperationException($"Azure diagnostic policy refused category={classification}; enrolledSelection={record.SelectedClientId is not null}; permittedName={record.Name is "LogMsalAlways" or "ManagedIdentityCredentialSelected"}; eventId={record.EventId}; observationSha256={Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(record.Content)))}.");
            if (classification == "permitted-selected-identifier")
            {
                if (record.EventId == 24) event24++;
                if (record.EventId == 26) event26++;
            }
        }
        return $"policy={Digest}; permitted-event24={event24}; permitted-event26={event26}; rejected=0";
    }

    /// <summary>Scans byte-equivalent URI/hex forms without folding the protected value's own casing.</summary>
    private static IEnumerable<string> Forms(string value)
        => new[] { value, Uri.EscapeDataString(value), Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
                Convert.ToHexStringLower(Encoding.UTF8.GetBytes(value)), Convert.ToHexString(Encoding.UTF8.GetBytes(value)) }
            .SelectMany(form => new[] { form, JsonSerializer.Serialize(form)[1..^1] }).Distinct(StringComparer.Ordinal);

    private static IEnumerable<string> Views(string value)
    {
        yield return value;
        string decoded;
        try { decoded = Uri.UnescapeDataString(value); }
        catch (UriFormatException) { decoded = value; }
        if (decoded != value) yield return decoded;
    }

    private static IEnumerable<string> Spellings(Guid value)
        => new[] { "D", "N", "B", "P", "X" }.SelectMany(format => new[] { value.ToString(format), value.ToString(format).ToUpperInvariant() })
            .Concat(["urn:uuid:" + value.ToString("D"), "URN:UUID:" + value.ToString("D").ToUpperInvariant()]);

    private static IEnumerable<Guid> Identifiers(string value)
    {
        foreach (Match match in IdentityFieldPattern().Matches(value))
            if (Guid.TryParse(match.Value, out Guid parsed)) yield return parsed;
    }

    private static bool OnlySelectedIdentity(string field, Guid selected)
    {
        Guid[] identifiers = Identifiers(field).ToArray();
        return identifiers.Length == 1 && identifiers[0] == selected;
    }

    [GeneratedRegex(@"\AFalse MSAL 4\.92\.0(?:\.0)? [^\[\]\r\n]*\[(?<utc>[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}Z) - (?<correlation>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\]", RegexOptions.CultureInvariant)]
    private static partial Regex MsalCorrelationEnvelopePattern();

    [GeneratedRegex(@"(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}|[0-9A-Fa-f]{32})(?![0-9A-Fa-f])|\{0x[0-9A-Fa-f]{8},0x[0-9A-Fa-f]{4},0x[0-9A-Fa-f]{4},\{(?:0x[0-9A-Fa-f]{2},){7}0x[0-9A-Fa-f]{2}\}\}", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IdentityFieldPattern();
}
