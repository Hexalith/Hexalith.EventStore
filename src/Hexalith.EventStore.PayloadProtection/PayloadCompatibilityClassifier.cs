// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8.4, 12.1-12.3, 14, and Appendix B.
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Classifies one stored event or snapshot in the normative section 12.1 order: metadata carrier first, then
/// reserved serialization format and bounded byte shape, then exactly one route.
/// </summary>
/// <remarks>
/// Classification is pure: it performs no I/O, calls no key resolver or legacy reader, and takes no write mode,
/// watermark, or clock. Every locally decidable disagreement returns its bounded reason. Neither results nor
/// exceptions carry payload, envelope, or carrier text.
/// </remarks>
internal static class PayloadCompatibilityClassifier
{
    /// <summary>Gets the exact protected-metadata scheme of the shared pdenc-v2 engine (normative section 8.4).</summary>
    internal const string SharedV2Scheme = "hexalith-pdenc-v2";

    /// <summary>Gets the exact Parties pdenc-v1 metadata scheme (normative Appendix B).</summary>
    internal const string PartiesV1Scheme = "parties-aes-gcm-json-fields";

    /// <summary>Gets the exact pdenc-v2 metadata content hint (normative section 8.4).</summary>
    internal const string V2ContentHint = "application/json";

    /// <summary>Gets the carrier text ceiling; every allowlisted carrier is far smaller.</summary>
    internal const int MaximumCarrierCharacters = 65_536;

    private const string FormatFlag = "format";
    private const string V2EnvelopeFlag = "envelope";
    private const string V2EnvelopeFlagValue = "pdenc-v2";
    private const string V1EnvelopeFlag = "field-envelope";
    private const string V1EnvelopeFlagValue = "pdenc-v1";
    private const string V1SnapshotMarker = "$protectedSnapshot";
    private const string V1SnapshotMarkerMember = "marker";
    private const string V1SnapshotFormatMember = "serializationFormat";

    /// <summary>
    /// Classifies one raw stored <c>eventstore.protection</c> carrier.
    /// </summary>
    /// <remarks>
    /// A missing or blank carrier is legacy, matching <see cref="EventStorePayloadProtectionMetadataCarrier.Read(string?)"/>.
    /// Before that reader runs, a duplicate member (top level or flag), a state spelling other than an exact
    /// defined name, or a metadata version below one is <see cref="UnreadableProtectedDataReason.MalformedMetadata"/>,
    /// even where the reader would accept or reclassify it.
    /// </remarks>
    /// <param name="carrier">The raw carrier text, or <see langword="null"/>.</param>
    /// <returns>The metadata route or one bounded rejection.</returns>
    internal static CompatibilityClassification ClassifyCarrier(string? carrier)
    {
        if (string.IsNullOrWhiteSpace(carrier))
        {
            return ClassifyMetadata(null);
        }

        if (carrier.Length > MaximumCarrierCharacters || !HasCanonicalCarrierStructure(carrier, out bool futureVersion))
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.MalformedMetadata);
        }

        if (futureVersion)
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.UnknownMetadataVersion);
        }

        return ClassifyMetadata(EventStorePayloadProtectionMetadataCarrier.Read(carrier));
    }

    /// <summary>
    /// Classifies typed stored metadata against the exact legacy, unprotected, Parties v1, and v2 allowlists.
    /// </summary>
    /// <param name="metadata">The stored metadata, or <see langword="null"/> for a legacy record.</param>
    /// <returns>The metadata route or one bounded rejection.</returns>
    internal static CompatibilityClassification ClassifyMetadata(EventStorePayloadProtectionMetadata? metadata)
    {
        if (metadata is null)
        {
            return CompatibilityClassification.Select(
                CompatibilityReadRoute.LegacyUnprotected,
                EventStorePayloadProtectionMetadataCarrier.Legacy());
        }

        if (!Enum.IsDefined(metadata.State))
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.MalformedMetadata);
        }

        if (metadata.MetadataVersion > EventStorePayloadProtectionMetadata.CurrentMetadataVersion)
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.UnknownMetadataVersion);
        }

        if (!EventStorePayloadProtectionMetadataCarrier.TryValidate(metadata, out _))
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.MalformedMetadata);
        }

        switch (metadata.State)
        {
            case PayloadProtectionState.ProviderOpaque:
                return CompatibilityClassification.Reject(
                    UnreadableProtectedDataReasonMapper.FromProviderOpaqueMetadata(metadata));
            case PayloadProtectionState.Unprotected:
                return CompatibilityClassification.Select(
                    metadata.Equals(EventStorePayloadProtectionMetadataCarrier.Legacy())
                        ? CompatibilityReadRoute.LegacyUnprotected
                        : CompatibilityReadRoute.Unprotected,
                    metadata);
            default:
                if (IsExactSharedV2(metadata))
                {
                    return CompatibilityClassification.Select(CompatibilityReadRoute.SharedV2, metadata);
                }

                if (IsExactPartiesV1(metadata))
                {
                    return CompatibilityClassification.Select(CompatibilityReadRoute.RegisteredV1, metadata);
                }

                // A known scheme outside its exact allowlist is malformed; an unknown scheme is opaque.
                return CompatibilityClassification.Reject(
                    string.Equals(metadata.Scheme, SharedV2Scheme, StringComparison.Ordinal)
                    || string.Equals(metadata.Scheme, PartiesV1Scheme, StringComparison.Ordinal)
                        ? UnreadableProtectedDataReason.MalformedMetadata
                        : UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        }
    }

    /// <summary>
    /// Classifies one stored event under the normative section 12.2 matrix.
    /// </summary>
    /// <param name="record">The stored event.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The selected route or one bounded rejection.</returns>
    internal static CompatibilityClassification ClassifyEvent(
        CompatibilityEventRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.PayloadBytes);
        cancellationToken.ThrowIfCancellationRequested();
        CompatibilityClassification carrier = ClassifyCarrier(record.ProtectionCarrier);
        if (carrier.IsRejected)
        {
            return carrier;
        }

        string? format = record.SerializationFormat;
        if (string.IsNullOrWhiteSpace(format))
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }

        if (IsReservedUnknownFormat(format))
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        }

        bool v2Format = string.Equals(format, PayloadProtectionWireFormat.ProtectedSerializationFormat, StringComparison.Ordinal);
        bool v1Format = string.Equals(format, PayloadProtectionWireFormat.LegacyProtectedSerializationFormat, StringComparison.Ordinal);
        bool metadataAgreesWithFormat = carrier.Route switch
        {
            CompatibilityReadRoute.SharedV2 => v2Format,
            CompatibilityReadRoute.RegisteredV1 => v1Format,
            CompatibilityReadRoute.Unprotected => !v1Format && !v2Format,
            _ => !v2Format,
        };
        if (!metadataAgreesWithFormat)
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }

        bool parsed = TryInspectJson(record.PayloadBytes, cancellationToken, out bool hasV2Wrapper, out bool hasV1Marker);
        bool plain = parsed && !hasV2Wrapper && !hasV1Marker;
        if (v2Format)
        {
            return parsed && hasV2Wrapper
                ? carrier
                : CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }

        if (v1Format)
        {
            return parsed && hasV1Marker && !hasV2Wrapper
                ? CompatibilityClassification.Select(CompatibilityReadRoute.RegisteredV1, carrier.Metadata!)
                : CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }

        if (string.Equals(format, PayloadProtectionWireFormat.RedactedSerializationFormat, StringComparison.Ordinal))
        {
            return plain
                ? CompatibilityClassification.Select(CompatibilityReadRoute.Redacted, carrier.Metadata!)
                : CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }

        if (string.Equals(format, PayloadProtectionWireFormat.UnprotectedSerializationFormat, StringComparison.Ordinal))
        {
            return plain
                ? carrier
                : CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }

        // A non-reserved custom format passes through byte-for-byte unless a reserved shape is detectable.
        return parsed && !plain
            ? CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch)
            : carrier;
    }

    /// <summary>
    /// Classifies one stored snapshot under the normative section 12.3 matrix.
    /// </summary>
    /// <param name="record">The stored snapshot.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The selected route or one bounded rejection.</returns>
    /// <exception cref="ArgumentException">The state is neither a JSON element nor a v2 snapshot carrier.</exception>
    internal static CompatibilityClassification ClassifySnapshot(
        CompatibilitySnapshotRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.State is not (JsonElement or ProtectedSnapshotPayloadV2))
        {
            throw new ArgumentException("The snapshot state must be a stored JSON element or a v2 snapshot carrier.", nameof(record));
        }

        cancellationToken.ThrowIfCancellationRequested();
        CompatibilityClassification metadata = ClassifyMetadata(record.ProtectionMetadata);
        if (metadata.IsRejected)
        {
            return metadata;
        }

        ProtectedSnapshotPayloadV2? v2Carrier;
        bool v1Wrapper;
        bool protectedShape;
        if (record.State is ProtectedSnapshotPayloadV2 carrier)
        {
            v2Carrier = carrier.Format is not null && IsReservedFormat(carrier.Format) ? carrier : null;
            v1Wrapper = false;
            protectedShape = true;
        }
        else if (!TryInspectSnapshotState(
            (JsonElement)record.State,
            cancellationToken,
            out v2Carrier,
            out v1Wrapper,
            out protectedShape))
        {
            return CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }

        switch (metadata.Route)
        {
            case CompatibilityReadRoute.SharedV2:
                if (v2Carrier is null)
                {
                    return CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
                }

                if (IsReservedUnknownFormat(v2Carrier.Format))
                {
                    return CompatibilityClassification.Reject(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
                }

                return string.Equals(v2Carrier.Format, PayloadProtectionWireFormat.ProtectedSerializationFormat, StringComparison.Ordinal)
                    && v2Carrier.SnapshotTypeId is not null
                    && v2Carrier.Envelope is not null
                        ? CompatibilityClassification.Select(CompatibilityReadRoute.SharedV2, metadata.Metadata!, v2Carrier)
                        : CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
            case CompatibilityReadRoute.RegisteredV1:
                return v1Wrapper
                    ? metadata
                    : CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch);
            default:
                // Missing or unprotected metadata over any protected snapshot shape matches the no-op mismatch.
                return protectedShape
                    ? CompatibilityClassification.Reject(UnreadableProtectedDataReason.BytesMetadataMismatch)
                    : metadata;
        }
    }

    /// <summary>
    /// Determines whether a serialization format belongs to the reserved protected family: the normative
    /// <c>json+pdenc-</c> prefix, plus the protected markers the existing no-op provider already refuses.
    /// Comparison ignores case so a respelled reserved format is never custom plaintext.
    /// </summary>
    /// <param name="format">The stored serialization format.</param>
    /// <returns><see langword="true"/> when the format is reserved.</returns>
    internal static bool IsReservedFormat(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return format.StartsWith(PayloadProtectionWireFormat.ReservedSerializationFormatPrefix, StringComparison.OrdinalIgnoreCase)
            || format.Contains("+pdenc-", StringComparison.OrdinalIgnoreCase)
            || format.StartsWith("protected+", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses bytes under the Story 8.3 core limits (16 MiB, 65,536 nodes, depth 64, strict UTF-8, unique member
    /// names) and reports reserved <c>$pdenc</c> and <c>$enc</c> members found at any depth.
    /// </summary>
    /// <param name="utf8Json">The candidate JSON bytes. They are copied once and the copy is cleared.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <param name="hasV2Wrapper">Whether any object has a <c>$pdenc</c> member.</param>
    /// <param name="hasV1Marker">Whether any object has a <c>$enc</c> member.</param>
    /// <returns><see langword="true"/> when the bytes are valid bounded JSON.</returns>
    internal static bool TryInspectJson(
        ReadOnlySpan<byte> utf8Json,
        CancellationToken cancellationToken,
        out bool hasV2Wrapper,
        out bool hasV1Marker)
    {
        hasV2Wrapper = false;
        hasV1Marker = false;
        try
        {
            using BoundedJsonDocument document = BoundedJsonDocument.Parse(utf8Json, cancellationToken);
            hasV2Wrapper = document.ContainsProtectedMember;
            hasV1Marker = document.ContainsLegacyProtectedMember;
            return true;
        }
        catch (PayloadProtectionFormatException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    private static bool IsReservedUnknownFormat(string? format)
        => format is not null
            && IsReservedFormat(format)
            && !string.Equals(format, PayloadProtectionWireFormat.ProtectedSerializationFormat, StringComparison.Ordinal)
            && !string.Equals(format, PayloadProtectionWireFormat.LegacyProtectedSerializationFormat, StringComparison.Ordinal);

    private static bool IsExactSharedV2(EventStorePayloadProtectionMetadata metadata)
        => metadata.State == PayloadProtectionState.Protected
            && metadata.MetadataVersion == 1
            && string.Equals(metadata.Scheme, SharedV2Scheme, StringComparison.Ordinal)
            && metadata.KeyAlias is null
            && string.Equals(metadata.ContentHint, V2ContentHint, StringComparison.Ordinal)
            && HasExactFlags(
                metadata.CompatibilityFlags,
                FormatFlag,
                PayloadProtectionWireFormat.ProtectedSerializationFormat,
                V2EnvelopeFlag,
                V2EnvelopeFlagValue);

    private static bool IsExactPartiesV1(EventStorePayloadProtectionMetadata metadata)
        => metadata.State == PayloadProtectionState.Protected
            && metadata.MetadataVersion == 1
            && string.Equals(metadata.Scheme, PartiesV1Scheme, StringComparison.Ordinal)
            && HasExactFlags(
                metadata.CompatibilityFlags,
                FormatFlag,
                PayloadProtectionWireFormat.LegacyProtectedSerializationFormat,
                V1EnvelopeFlag,
                V1EnvelopeFlagValue);

    private static bool HasExactFlags(
        IReadOnlyDictionary<string, string>? flags,
        string firstKey,
        string firstValue,
        string secondKey,
        string secondValue)
    {
        if (flags is null || flags.Count != 2)
        {
            return false;
        }

        bool first = false;
        bool second = false;
        foreach (KeyValuePair<string, string> flag in flags)
        {
            if (string.Equals(flag.Key, firstKey, StringComparison.Ordinal)
                && string.Equals(flag.Value, firstValue, StringComparison.Ordinal))
            {
                first = true;
            }
            else if (string.Equals(flag.Key, secondKey, StringComparison.Ordinal)
                && string.Equals(flag.Value, secondValue, StringComparison.Ordinal))
            {
                second = true;
            }
            else
            {
                return false;
            }
        }

        return first && second;
    }

    private static bool HasCanonicalCarrierStructure(string carrier, out bool futureVersion)
    {
        futureVersion = false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(carrier, new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasUniqueMemberNames(root))
            {
                return false;
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.NameEquals("state")
                    && (property.Value.ValueKind != JsonValueKind.String || !IsDefinedStateName(property.Value.GetString())))
                {
                    return false;
                }

                // The carrier reader maps a version below one to "unknown version"; it is malformed, not newer.
                if (property.NameEquals("metadataVersion")
                    && property.Value.ValueKind == JsonValueKind.Number)
                {
                    if (!BigInteger.TryParse(
                        property.Value.GetRawText(),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out BigInteger version)
                        || version < BigInteger.One)
                    {
                        return false;
                    }

                    // A future schema may add carrier members unknown to the current reader. Classify
                    // its version before that reader treats those members as malformed.
                    futureVersion = version > EventStorePayloadProtectionMetadata.CurrentMetadataVersion;
                }

                if (property.NameEquals("compatibilityFlags")
                    && property.Value.ValueKind == JsonValueKind.Object
                    && !HasUniqueMemberNames(property.Value))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasUniqueMemberNames(JsonElement value)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDefinedStateName(string? value)
        => string.Equals(value, nameof(PayloadProtectionState.Unprotected), StringComparison.Ordinal)
            || string.Equals(value, nameof(PayloadProtectionState.Protected), StringComparison.Ordinal)
            || string.Equals(value, nameof(PayloadProtectionState.ProviderOpaque), StringComparison.Ordinal);

    private static bool TryInspectSnapshotState(
        JsonElement state,
        CancellationToken cancellationToken,
        out ProtectedSnapshotPayloadV2? v2Carrier,
        out bool v1Wrapper,
        out bool protectedShape)
    {
        v2Carrier = null;
        v1Wrapper = false;
        protectedShape = false;
        if (state.ValueKind == JsonValueKind.Undefined
            || !TryInspectJson(JsonMarshal.GetRawUtf8Value(state), cancellationToken, out bool hasV2Wrapper, out bool hasV1Marker))
        {
            return false;
        }

        protectedShape = hasV2Wrapper || hasV1Marker;
        if (state.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        // The bounded parse above already proved unique member names and bounded size.
        int memberCount = 0;
        string? format = null;
        string? snapshotTypeId = null;
        string? envelope = null;
        string? marker = null;
        string? markerFormat = null;
        string? v1TypeName = null;
        bool hasV1Payload = false;
        foreach (JsonProperty property in state.EnumerateObject())
        {
            memberCount++;
            string? value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            // Dapr actor state uses web (camelCase) naming, so the three v2 member names ignore case.
            if (string.Equals(property.Name, nameof(ProtectedSnapshotPayloadV2.Format), StringComparison.OrdinalIgnoreCase))
            {
                format = value;
            }
            else if (string.Equals(property.Name, nameof(ProtectedSnapshotPayloadV2.SnapshotTypeId), StringComparison.OrdinalIgnoreCase))
            {
                snapshotTypeId = value;
            }
            else if (string.Equals(property.Name, nameof(ProtectedSnapshotPayloadV2.Envelope), StringComparison.OrdinalIgnoreCase))
            {
                envelope = value;
            }
            else if (property.NameEquals(V1SnapshotMarkerMember))
            {
                marker = value;
            }
            else if (property.NameEquals(V1SnapshotFormatMember))
            {
                markerFormat = value;
            }
            else if (property.NameEquals("typeName"))
            {
                v1TypeName = value;
            }
            else if (property.NameEquals("payload"))
            {
                // The registered reader owns the v1 payload representation. The router requires
                // the field to be present and non-null, without interpreting its contents.
                hasV1Payload = property.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
            }

            if (string.Equals(property.Name, nameof(ProtectedSnapshotPayloadV2.SnapshotTypeId), StringComparison.OrdinalIgnoreCase)
                || string.Equals(property.Name, nameof(ProtectedSnapshotPayloadV2.Envelope), StringComparison.OrdinalIgnoreCase))
            {
                protectedShape = true;
            }

            if (value is not null && IsProtectedSnapshotMember(property.Name, value))
            {
                protectedShape = true;
            }
        }

        if (memberCount == 3
            && format is not null
            && snapshotTypeId is not null
            && envelope is not null
            && IsReservedFormat(format))
        {
            v2Carrier = new ProtectedSnapshotPayloadV2(format, snapshotTypeId, envelope);
        }

        v1Wrapper = !hasV2Wrapper
            && string.Equals(marker, V1SnapshotMarker, StringComparison.Ordinal)
            && string.Equals(markerFormat, PayloadProtectionWireFormat.LegacyProtectedSerializationFormat, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(v1TypeName)
            && hasV1Payload;
        return true;
    }

    private static bool IsProtectedSnapshotMember(string name, string value)
        => ((string.Equals(name, nameof(ProtectedSnapshotPayloadV2.Format), StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, V1SnapshotFormatMember, StringComparison.OrdinalIgnoreCase))
                && IsReservedFormat(value))
            || (string.Equals(name, V1SnapshotMarkerMember, StringComparison.OrdinalIgnoreCase)
                && string.Equals(value, V1SnapshotMarker, StringComparison.OrdinalIgnoreCase));
}
