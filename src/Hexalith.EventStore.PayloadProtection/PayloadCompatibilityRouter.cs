// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8.4, 12, 13, 15, and Appendix B.
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Routes each stored event or snapshot independently to exactly one compatibility reader (normative section 12).
/// </summary>
/// <remarks>
/// <para>
/// Every record is first classified by <see cref="PayloadCompatibilityClassifier"/>. A local rejection returns its
/// bounded reason without calling the key resolver or any legacy reader. The registered <c>parties-pdenc-v1</c>
/// reader owns v1 records, and the unchanged Story 8.3 core authenticates v2 records before any plaintext leaves
/// the router. A mixed stream stops at its first unreadable record.
/// </para>
/// <para>
/// The router has no write-mode, watermark, or clock input and emits no telemetry of its own. Cancellation
/// propagates as <see cref="OperationCanceledException"/>. Results and exceptions never carry plaintext,
/// ciphertext, envelope, or carrier text.
/// </para>
/// </remarks>
internal sealed class PayloadCompatibilityRouter
{
    /// <summary>Gets the reader identifier of the built-in legacy and unprotected plaintext pass-through.</summary>
    internal const string PlaintextPassThroughReaderId = "eventstore-plaintext-passthrough";

    /// <summary>Gets the reader identifier of the built-in <c>json-redacted</c> pass-through.</summary>
    internal const string RedactedPassThroughReaderId = "eventstore-redacted-passthrough";

    /// <summary>Gets the only approved legacy reader identifier (normative section 12.2).</summary>
    internal const string PartiesV1ReaderId = "parties-pdenc-v1";

    /// <summary>Gets the reader identifier of the shared pdenc-v2 core.</summary>
    internal const string SharedV2ReaderId = "hexalith-pdenc-v2";

    private readonly PayloadProtectionCore _core;
    private readonly Func<string, uint, CancellationToken, ValueTask<byte[]?>> _keyResolver;
    private readonly ILegacyPayloadReader? _partiesV1Reader;
    private readonly SnapshotTypeRegistry _snapshotTypes;

    /// <summary>
    /// Initializes a router over an exact reader set.
    /// </summary>
    /// <param name="keyResolver">
    /// Resolves an exact pdenc-v2 key reference and version; the Story 8.5 seam. Ownership of each returned DEK
    /// transfers to the core, which clears it.
    /// </param>
    /// <param name="legacyReaders">
    /// The registered legacy readers. Only <c>parties-pdenc-v1</c> is routable; an unknown or duplicate identifier
    /// fails construction.
    /// </param>
    /// <param name="snapshotTypes">The v2 snapshot type registry, or <see langword="null"/> for none.</param>
    /// <param name="core">The Story 8.3 core, or <see langword="null"/> for a default instance.</param>
    /// <exception cref="ArgumentException">A legacy reader is null, unknown, or registered twice.</exception>
    internal PayloadCompatibilityRouter(
        Func<string, uint, CancellationToken, ValueTask<byte[]?>> keyResolver,
        IEnumerable<ILegacyPayloadReader>? legacyReaders = null,
        SnapshotTypeRegistry? snapshotTypes = null,
        PayloadProtectionCore? core = null)
    {
        ArgumentNullException.ThrowIfNull(keyResolver);
        _keyResolver = keyResolver;
        _core = core ?? new PayloadProtectionCore();
        _snapshotTypes = snapshotTypes ?? new SnapshotTypeRegistry([]);
        foreach (ILegacyPayloadReader? reader in legacyReaders ?? [])
        {
            if (reader is null || !string.Equals(reader.ReaderId, PartiesV1ReaderId, StringComparison.Ordinal))
            {
                throw new ArgumentException("A legacy payload reader is null or has an unapproved identifier.", nameof(legacyReaders));
            }

            if (_partiesV1Reader is not null)
            {
                throw new ArgumentException("A legacy payload reader identifier is registered twice.", nameof(legacyReaders));
            }

            _partiesV1Reader = reader;
        }

        List<CompatibilityReaderCapability> capabilities =
        [
            new(PlaintextPassThroughReaderId, PayloadProtectionWireFormat.UnprotectedSerializationFormat, 0),
            new(RedactedPassThroughReaderId, PayloadProtectionWireFormat.RedactedSerializationFormat, 0),
        ];
        if (_partiesV1Reader is not null)
        {
            capabilities.Add(new(PartiesV1ReaderId, PayloadProtectionWireFormat.LegacyProtectedSerializationFormat, 1));
        }

        capabilities.Add(new(SharedV2ReaderId, PayloadProtectionWireFormat.ProtectedSerializationFormat, 2));
        Capabilities = capabilities.AsReadOnly();
    }

    /// <summary>
    /// Gets the exact reader identifiers, formats, and versions this router executes. An unregistered legacy reader
    /// is never advertised. Plaintext pass-through also covers non-reserved custom formats.
    /// </summary>
    internal IReadOnlyList<CompatibilityReaderCapability> Capabilities { get; }

    /// <summary>
    /// Routes one stored event.
    /// </summary>
    /// <param name="record">The stored event.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The readable event or one bounded unreadable reason.</returns>
    internal async ValueTask<CompatibilityEventReadResult> ReadEventAsync(
        CompatibilityEventRecord record,
        CancellationToken cancellationToken = default)
    {
        ValidateEventRecord(record);
        cancellationToken.ThrowIfCancellationRequested();
        CompatibilityClassification classification = PayloadCompatibilityClassifier.ClassifyEvent(record, cancellationToken);
        if (classification.UnreadableReason is { } rejection)
        {
            return CompatibilityEventReadResult.Unreadable(record.SequenceNumber, CompatibilityReadRoute.Rejected, rejection);
        }

        return classification.Route switch
        {
            CompatibilityReadRoute.LegacyUnprotected or CompatibilityReadRoute.Unprotected or CompatibilityReadRoute.Redacted
                => CompatibilityEventReadResult.Readable(
                    record.SequenceNumber,
                    classification.Route,
                    record.PayloadBytes,
                    record.SerializationFormat,
                    classification.Metadata!),
            CompatibilityReadRoute.RegisteredV1
                => await ReadRegisteredV1EventAsync(record, classification.Metadata!, cancellationToken).ConfigureAwait(false),
            CompatibilityReadRoute.SharedV2
                => await ReadSharedV2EventAsync(record, cancellationToken).ConfigureAwait(false),
            _ => CompatibilityEventReadResult.Unreadable(
                record.SequenceNumber,
                CompatibilityReadRoute.Rejected,
                UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation),
        };
    }

    /// <summary>
    /// Routes one mixed-history stream record by record and stops at the first unreadable record.
    /// </summary>
    /// <param name="records">The stored events of one aggregate in contiguous ascending sequence order.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>
    /// Every event readable in order, or only the first unreadable decision. No later record is examined and no
    /// partial list is returned; router-owned plaintext from earlier records is cleared.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// A record is null, belongs to another aggregate, or does not follow its predecessor's sequence by exactly one.
    /// </exception>
    internal async ValueTask<CompatibilityStreamReadResult> ReadStreamAsync(
        IReadOnlyList<CompatibilityEventRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        for (int index = 0; index < records.Count; index++)
        {
            CompatibilityEventRecord? record = records[index];
            if (record is null)
            {
                throw new ArgumentException("A stream record is null.", nameof(records));
            }

            ValidateEventRecord(record);
            if (index > 0
                && (record.SequenceNumber == 0
                    || record.SequenceNumber - 1 != records[index - 1].SequenceNumber
                    || record.Identity != records[0].Identity))
            {
                throw new ArgumentException(
                    "Stream records must belong to one aggregate in contiguous ascending sequence order.",
                    nameof(records));
            }
        }

        var events = new List<CompatibilityEventReadResult>(records.Count);
        bool completed = false;
        try
        {
            for (int index = 0; index < records.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CompatibilityEventReadResult result = await ReadEventAsync(records[index], cancellationToken).ConfigureAwait(false);
                if (!result.IsReadable)
                {
                    return CompatibilityStreamReadResult.Unreadable(result);
                }

                events.Add(result);
            }

            completed = true;
            return CompatibilityStreamReadResult.Readable(events.AsReadOnly());
        }
        finally
        {
            if (!completed)
            {
                ClearOwnedPayloads(events);
            }
        }
    }

    /// <summary>
    /// Routes one stored snapshot.
    /// </summary>
    /// <param name="record">The stored snapshot.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>
    /// Readable state, or one bounded unreadable reason with no state. An unreadable snapshot must be retained.
    /// </returns>
    /// <exception cref="ArgumentException">The state is neither a JSON element nor a v2 snapshot carrier.</exception>
    internal async ValueTask<CompatibilitySnapshotReadResult> ReadSnapshotAsync(
        CompatibilitySnapshotRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.Identity);
        ArgumentNullException.ThrowIfNull(record.State);
        cancellationToken.ThrowIfCancellationRequested();
        CompatibilityClassification classification = PayloadCompatibilityClassifier.ClassifySnapshot(record, cancellationToken);
        if (classification.UnreadableReason is { } rejection)
        {
            return CompatibilitySnapshotReadResult.Unreadable(CompatibilityReadRoute.Rejected, rejection);
        }

        return classification.Route switch
        {
            CompatibilityReadRoute.LegacyUnprotected or CompatibilityReadRoute.Unprotected
                => CompatibilitySnapshotReadResult.Readable(classification.Route, record.State, classification.Metadata!),
            CompatibilityReadRoute.RegisteredV1
                => await ReadRegisteredV1SnapshotAsync(record, classification.Metadata!, cancellationToken).ConfigureAwait(false),
            CompatibilityReadRoute.SharedV2
                => await ReadSharedV2SnapshotAsync(record, classification.ProtectedSnapshot!, cancellationToken).ConfigureAwait(false),
            _ => CompatibilitySnapshotReadResult.Unreadable(
                CompatibilityReadRoute.Rejected,
                UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation),
        };
    }

    private static void ValidateEventRecord(CompatibilityEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.Identity);
        ArgumentNullException.ThrowIfNull(record.EventTypeName);
        ArgumentNullException.ThrowIfNull(record.PayloadBytes);
        ArgumentNullException.ThrowIfNull(record.SerializationFormat);
    }

    private static void ClearOwnedPayloads(List<CompatibilityEventReadResult> events)
    {
        // Pass-through results alias caller-owned stored bytes and are never cleared.
        for (int index = 0; index < events.Count; index++)
        {
            if (events[index].OwnsPayload && events[index].PayloadBytes is { } owned)
            {
                CryptographicOperations.ZeroMemory(owned);
            }
        }
    }

    private static UnreadableProtectedDataReason? AcceptLegacyPlaintext(
        CoreUnprotectionResult? result,
        byte[]? callerBuffer,
        CancellationToken cancellationToken,
        out byte[]? plaintext)
    {
        plaintext = null;
        if (result is null)
        {
            return UnreadableProtectedDataReason.ProviderUnavailable;
        }

        byte[]? owned = result.PayloadBytes;
        if (owned is not null && ReferenceEquals(owned, callerBuffer))
        {
            // A reader that hands back the stored buffer did not produce plaintext; never clear caller bytes.
            return UnreadableProtectedDataReason.ConsistencyMismatch;
        }

        try
        {
            if (result.UnreadableReason is { } reason)
            {
                return reason;
            }

            if (owned is null)
            {
                return UnreadableProtectedDataReason.ProviderUnavailable;
            }

            // A readable legacy result must be complete JSON with no remaining protected marker.
            if (!PayloadCompatibilityClassifier.TryInspectJson(owned, cancellationToken, out bool hasV2Wrapper, out bool hasV1Marker)
                || hasV2Wrapper
                || hasV1Marker)
            {
                return UnreadableProtectedDataReason.ConsistencyMismatch;
            }

            cancellationToken.ThrowIfCancellationRequested();
            plaintext = owned;
            owned = null;
            return null;
        }
        finally
        {
            if (owned is not null)
            {
                CryptographicOperations.ZeroMemory(owned);
            }
        }
    }

    private async ValueTask<CompatibilityEventReadResult> ReadSharedV2EventAsync(
        CompatibilityEventRecord record,
        CancellationToken cancellationToken)
    {
        var context = new PayloadProtectionContext(
            record.Identity,
            record.EventTypeName,
            PayloadProtectionPayloadKind.Event,
            record.SequenceNumber);
        CoreUnprotectionResult result = await _core
            .TryUnprotectEventAsync(record.PayloadBytes, context, _keyResolver, cancellationToken)
            .ConfigureAwait(false);
        return result.IsReadable
            ? CompatibilityEventReadResult.Readable(
                record.SequenceNumber,
                CompatibilityReadRoute.SharedV2,
                result.PayloadBytes!,
                PayloadProtectionWireFormat.UnprotectedSerializationFormat,
                EventStorePayloadProtectionMetadata.Unprotected())
            : CompatibilityEventReadResult.Unreadable(
                record.SequenceNumber,
                CompatibilityReadRoute.SharedV2,
                result.UnreadableReason ?? UnreadableProtectedDataReason.ProviderUnavailable);
    }

    private async ValueTask<CompatibilityEventReadResult> ReadRegisteredV1EventAsync(
        CompatibilityEventRecord record,
        EventStorePayloadProtectionMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (_partiesV1Reader is null)
        {
            return CompatibilityEventReadResult.Unreadable(
                record.SequenceNumber,
                CompatibilityReadRoute.Rejected,
                UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        }

        CoreUnprotectionResult? result;
        try
        {
            result = await _partiesV1Reader.ReadEventAsync(record, metadata, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CompatibilityEventReadResult.Unreadable(
                record.SequenceNumber,
                CompatibilityReadRoute.RegisteredV1,
                UnreadableProtectedDataReason.ProviderUnavailable);
        }

        UnreadableProtectedDataReason? reason = AcceptLegacyPlaintext(
            result,
            record.PayloadBytes,
            cancellationToken,
            out byte[]? plaintext);
        return reason is { } unreadable
            ? CompatibilityEventReadResult.Unreadable(record.SequenceNumber, CompatibilityReadRoute.RegisteredV1, unreadable)
            : CompatibilityEventReadResult.Readable(
                record.SequenceNumber,
                CompatibilityReadRoute.RegisteredV1,
                plaintext!,
                PayloadProtectionWireFormat.UnprotectedSerializationFormat,
                EventStorePayloadProtectionMetadata.Unprotected());
    }

    private async ValueTask<CompatibilitySnapshotReadResult> ReadSharedV2SnapshotAsync(
        CompatibilitySnapshotRecord record,
        ProtectedSnapshotPayloadV2 carrier,
        CancellationToken cancellationToken)
    {
        if (!_snapshotTypes.TryResolve(carrier.SnapshotTypeId, out SnapshotTypeRegistration? registration))
        {
            return CompatibilitySnapshotReadResult.Unreadable(
                CompatibilityReadRoute.Rejected,
                UnreadableProtectedDataReason.ConsistencyMismatch);
        }

        // The stored identifier (current or alias) is the authenticated payload type the writer bound into the AAD.
        var context = new PayloadProtectionContext(
            record.Identity,
            carrier.SnapshotTypeId,
            PayloadProtectionPayloadKind.Snapshot,
            record.SnapshotSequence);
        CoreUnprotectionResult result = await _core
            .TryUnprotectSnapshotAsync(carrier, context, _keyResolver, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsReadable)
        {
            return CompatibilitySnapshotReadResult.Unreadable(
                CompatibilityReadRoute.SharedV2,
                result.UnreadableReason ?? UnreadableProtectedDataReason.ProviderUnavailable);
        }

        byte[] plaintext = result.PayloadBytes!;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? state;
            try
            {
                state = JsonSerializer.Deserialize(plaintext, registration.TypeInfo);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                state = null;
            }

            return state is null
                ? CompatibilitySnapshotReadResult.Unreadable(
                    CompatibilityReadRoute.SharedV2,
                    UnreadableProtectedDataReason.ConsistencyMismatch)
                : CompatibilitySnapshotReadResult.Readable(
                    CompatibilityReadRoute.SharedV2,
                    state,
                    EventStorePayloadProtectionMetadata.Unprotected());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private async ValueTask<CompatibilitySnapshotReadResult> ReadRegisteredV1SnapshotAsync(
        CompatibilitySnapshotRecord record,
        EventStorePayloadProtectionMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (_partiesV1Reader is null)
        {
            return CompatibilitySnapshotReadResult.Unreadable(
                CompatibilityReadRoute.Rejected,
                UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        }

        CoreUnprotectionResult? result;
        try
        {
            result = await _partiesV1Reader.ReadSnapshotAsync(record, metadata, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CompatibilitySnapshotReadResult.Unreadable(
                CompatibilityReadRoute.RegisteredV1,
                UnreadableProtectedDataReason.ProviderUnavailable);
        }

        UnreadableProtectedDataReason? reason = AcceptLegacyPlaintext(result, null, cancellationToken, out byte[]? plaintext);
        if (reason is { } unreadable)
        {
            return CompatibilitySnapshotReadResult.Unreadable(CompatibilityReadRoute.RegisteredV1, unreadable);
        }

        try
        {
            // Clone copies the element so the decrypted buffer can be cleared before returning.
            using JsonDocument document = JsonDocument.Parse(plaintext!);
            return CompatibilitySnapshotReadResult.Readable(
                CompatibilityReadRoute.RegisteredV1,
                document.RootElement.Clone(),
                EventStorePayloadProtectionMetadata.Unprotected());
        }
        catch (JsonException)
        {
            return CompatibilitySnapshotReadResult.Unreadable(
                CompatibilityReadRoute.RegisteredV1,
                UnreadableProtectedDataReason.ConsistencyMismatch);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
