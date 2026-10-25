using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Independent durable enrollment, conditional anchors, admitted originals and immutable progress journals.</summary>
/// <remarks>This owner performs only local signature/evidence validation and actor-state I/O. Observation happens before its turn.</remarks>
public sealed class SourcePublicationAuthorityActor(ActorHost host, SourcePublicationAuthorityInstallation installation,
    ISourcePublicationAuthorityInstallationEvidence evidence, WorkloadAssertionValidator validator, TimeProvider clock)
    : Actor(host), ISourcePublicationAuthorityActor
{
    /// <summary>The independent private actor registration name.</summary>
    public const string ActorTypeName = "SourcePublicationAuthorityActor";
    private const string StateKey = "source-publication-authority-v1";
    internal const string NamespacePurpose = "|source-publication-namespace-v1";
    internal const string IndexPurpose = "|source-publications-v1";

    /// <inheritdoc/>
    public async Task<byte[]?> CallAsync(SourcePublicationAuthorityCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.Scope is null || Host.Id.GetId() != call.Scope.ActorId) return null;
        _ = SourcePublicationWorkloadBinding.Bind(call);
        call = call with { Body = call.Body.ToArray() };
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var stored = await StateManager.TryGetStateAsync<SourcePublicationAuthorityState>(StateKey).ConfigureAwait(false);
        SourcePublicationAuthorityState? state = stored.HasValue ? Clone(stored.Value) : null;
        SourcePublicationAuthorityEnrollment? enrollment = call.Method == "enroll"
            ? JsonSerializer.Deserialize<SourcePublicationAuthorityEnrollment>(call.Body) : call.Method == "renew-enrollment"
                ? JsonSerializer.Deserialize<SourcePublicationAuthorityEnrollmentChange>(call.Body)?.Successor : state?.Enrollment;
        if (enrollment is null || !Valid(enrollment, call.Scope) || !evidence.IsCurrent(enrollment)
            || enrollment.Epoch != call.Epoch || enrollment.StorageIdentity != installation.StorageIdentity
            || enrollment.RegistrarWorkload != installation.RegistrarWorkload || enrollment.ValidUntil <= clock.GetUtcNow()) return null;
        string? caller = call.Method switch
        {
            "enroll" or "renew-enrollment" => enrollment.RegistrarWorkload,
            "read" => enrollment.ReaderWorkload,
            "lookup" or "transition-recover" => enrollment.RecoveryWorkload,
            "reconciliation" or "dispatch" => enrollment.ObserverWorkload,
            "transition-admit" or "transition-commit" => enrollment.ProducerWorkload,
            _ => null,
        };
        if (caller is null) return null;
        var authenticated = await validator.ValidateLocallyAsync(installation.WorkloadScheme, call.Assertion,
            SourcePublicationWorkloadBinding.AuthorityOperation, [caller], installation.Audience).ConfigureAwait(false);
        if (!SourcePublicationWorkloadBinding.Verify(call, authenticated)) return null;
        if (call.Method == "enroll")
        {
            if (state is not null) return Same(state.Enrollment, enrollment) ? JsonSerializer.SerializeToUtf8Bytes(true) : null;
            state = new(enrollment, []);
            if (!await PersistAsync(state).ConfigureAwait(false)) return null;
            return JsonSerializer.SerializeToUtf8Bytes(true);
        }
        if (call.Method == "renew-enrollment")
        {
            var change = JsonSerializer.Deserialize<SourcePublicationAuthorityEnrollmentChange>(call.Body)!;
            if (state is null) return null;
            string key = "enrollment-successor:" + RecoverableAnchoredState.Digest(change);
            if (Same(state.Enrollment, enrollment) && Exact(state.Records, key, call.Body)) return JsonSerializer.SerializeToUtf8Bytes(true);
            var previous = state.Enrollment;
            if (previous.Epoch != change.ExpectedEpoch || RecoverableAnchoredState.Digest(previous) != change.ExpectedEnrollmentDigest
                || previous.Scope != enrollment.Scope || previous.StorageIdentity != enrollment.StorageIdentity
                || previous.InventoryEvidenceId != enrollment.InventoryEvidenceId || previous.WriterEnforcementEvidenceId != enrollment.WriterEnforcementEvidenceId
                || !previous.InitialSources.SequenceEqual(enrollment.InitialSources) || previous.CommandSources.Any(identity => !enrollment.CommandSources.Contains(identity))
                || previous.ProducerWorkload != enrollment.ProducerWorkload || previous.ObserverWorkload != enrollment.ObserverWorkload
                || previous.ReaderWorkload != enrollment.ReaderWorkload || previous.RecoveryWorkload != enrollment.RecoveryWorkload) return null;
            var successors = state.Records.ToList();
            if (!Append(successors, "enrollment-predecessor:" + RecoverableAnchoredState.Digest(previous), JsonSerializer.SerializeToUtf8Bytes(previous))) return null;
            if (!Append(successors, key, call.Body)) return null;
            if (!await PersistAsync(state with { Enrollment = enrollment, Records = successors.AsReadOnly() }).ConfigureAwait(false)) return null;
            return JsonSerializer.SerializeToUtf8Bytes(true);
        }
        if (state is null || !Same(state.Enrollment, enrollment)) return null;
        if (call.Method == "read") return JsonSerializer.SerializeToUtf8Bytes(state);
        if (call.Method == "lookup")
        {
            string? key = JsonSerializer.Deserialize<string>(call.Body);
            return key is not null ? state.Records.SingleOrDefault(item => item.Key == key)?.Body.ToArray() : null;
        }
        var records = state.Records.ToList();
        if (call.Method.StartsWith("transition-", StringComparison.Ordinal))
        {
            AnchoredStateTransition? transition = JsonSerializer.Deserialize<AnchoredStateTransition>(call.Body);
            if (transition is null || !ValidTransition(transition, enrollment, records)) return null;
            string admissionKey = "admitted:" + TransitionId(transition);
            string committedKey = "transition:" + TransitionId(transition);
            string anchorKey = "anchor:" + transition.ScopeId;
            var anchor = Value<SourcePublicationAuthorityAnchor>(records, anchorKey) ?? new(0, RecoverableAnchoredState.Digest<object?>(null));
            bool committed = Exact(records, committedKey, call.Body);
            if (committed)
                return anchor.Revision >= transition.TargetRevision && Exact(records, admissionKey, call.Body)
                    ? JsonSerializer.SerializeToUtf8Bytes(true) : null;
            if (anchor.Revision != transition.ExpectedRevision || anchor.Digest != transition.PredecessorDigest) return null;
            if (call.Method == "transition-admit")
            {
                if (!Append(records, admissionKey, call.Body)) return null;
            }
            else
            {
                if (!Exact(records, admissionKey, call.Body) || !Append(records, committedKey, call.Body)) return null;
                Put(records, anchorKey, JsonSerializer.SerializeToUtf8Bytes(new SourcePublicationAuthorityAnchor(transition.TargetRevision, transition.TargetDigest)));
            }
        }
        else if (call.Method == "reconciliation")
        {
            var progress = JsonSerializer.Deserialize<SourcePublicationReconciliationProgress>(call.Body);
            if (progress is null) return null;
            progress = SourcePublicationReconciliationProgress.Capture(progress);
            var prior = Value<SourcePublicationReconciliationProgress>(records, "reconciliation-head");
            if (Exact(records, "reconciliation:" + progress.ReceiptId, call.Body)) return JsonSerializer.SerializeToUtf8Bytes(progress);
            var primaryAnchor = Value<SourcePublicationAuthorityAnchor>(records, "anchor:" + call.Scope.ActorId + IndexPurpose);
            long mirroredVersion = primaryAnchor is null ? 0
                : Prior<SourcePublicationIndexState>(records, call.Scope.ActorId + IndexPurpose, primaryAnchor.Revision)?.ReconciliationProgress?.Version ?? 0;
            if (progress.Scope != call.Scope || progress.Version != (prior?.Version ?? 0) + 1
                || progress.ExpectedPredecessorVersion is { } predecessor && predecessor != mirroredVersion
                || progress.OriginalCut is not { } original || original.ValidUntil <= clock.GetUtcNow()
                || !progress.SourceAuthorityRevision.StartsWith(enrollment.Epoch + ":", StringComparison.Ordinal)
                || (prior is not null && prior.OriginalCut is { } priorCut && priorCut.ValidUntil > clock.GetUtcNow()
                    && (progress.ExpectedPredecessorVersion is null || progress.ExpectedPredecessorVersion == prior.Version)
                    && RecoverableAnchoredState.Digest(priorCut) == RecoverableAnchoredState.Digest(original) ? progress.VerifiedSources != prior.VerifiedSources + 1
                    || !prior.Publications.SequenceEqual(progress.Publications.Take(prior.Publications.Count)) : progress.VerifiedSources != 1)) return null;
            if (!Append(records, "reconciliation:" + progress.ReceiptId, call.Body)) return null;
            Put(records, "reconciliation-head", call.Body);
        }
        else if (call.Method == "dispatch")
        {
            var proof = JsonSerializer.Deserialize<SourcePublicationDispatchProof>(call.Body);
            var progress = proof?.Progress;
            var prior = Value<SourcePublicationDispatchProgress>(records, "dispatch-head");
            if (progress is null) return null;
            if (Exact(records, "dispatch-proof:" + progress.ReceiptId, call.Body)) return JsonSerializer.SerializeToUtf8Bytes(progress);
            if (progress.Scope != call.Scope || progress.Version != (prior?.Version ?? 0) + 1
                || proof!.Receipts is not { Count: > 0 and <= 100 } || proof.ExpectedPrefix < 0
                || progress.AcknowledgedPrefix != proof.ExpectedPrefix + proof.Receipts.Count || progress.AcknowledgedPrefix > progress.LastOffset
                || proof.Receipts.Where((receipt, index) => receipt.Entry.Offset != proof.ExpectedPrefix + index + 1
                    || receipt.ProtectedDeletionRevision <= 0 || string.IsNullOrWhiteSpace(receipt.ReceiptId)).Any()
                || (proof.ExpectedPrefix == 0 ? proof.PredecessorReceiptId is not null
                    : prior is null || proof.PredecessorReceiptId != prior.ReceiptId || proof.ExpectedPrefix != prior.AcknowledgedPrefix)
                || progress.DeliveryTarget != enrollment.DeliveryTarget || progress.DeliveryAuthorityRevision != enrollment.DeliveryAuthorityRevision) return null;
            if (!Append(records, "dispatch-proof:" + progress.ReceiptId, call.Body)
                || !Append(records, "dispatch:" + progress.ReceiptId, JsonSerializer.SerializeToUtf8Bytes(progress))) return null;
            Put(records, "dispatch-head", JsonSerializer.SerializeToUtf8Bytes(progress));
        }
        else return null;
        var next = state with { Records = records.AsReadOnly() };
        if (!evidence.IsCurrent(enrollment) || enrollment.ValidUntil <= clock.GetUtcNow()) return null;
        if (!await PersistAsync(next).ConfigureAwait(false)) return null;
        return call.Method == "dispatch" ? JsonSerializer.SerializeToUtf8Bytes(Value<SourcePublicationDispatchProgress>(records, "dispatch-head"))
            : call.Method == "reconciliation" ? call.Body : JsonSerializer.SerializeToUtf8Bytes(true);
    }

    internal static bool CanPersist(SourcePublicationAuthorityState state)
    {
        if (state.Records.Count > 30000) return false;
        try { _ = RecoverableAnchoredState.Digest(state); return true; }
        catch (InvalidOperationException) { return false; }
    }
    private async Task<bool> PersistAsync(SourcePublicationAuthorityState state)
    {
        // Preflight the actual canonical serialization, including base64 expansion, before staging.
        // Confirmation uses this same bounded digest; no committed target may be incomparable.
        if (!CanPersist(state)) return false;
        await StateManager.SetStateAsync(StateKey, Clone(state)).ConfigureAwait(false);
        try { await StateManager.SaveStateAsync().ConfigureAwait(false); }
        catch
        {
            await StateManager.ClearCacheAsync().ConfigureAwait(false);
            var recovered = await StateManager.TryGetStateAsync<SourcePublicationAuthorityState>(StateKey).ConfigureAwait(false);
            if (!recovered.HasValue || !Same(recovered.Value, state)) throw;
        }
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var confirmed = await StateManager.TryGetStateAsync<SourcePublicationAuthorityState>(StateKey).ConfigureAwait(false);
        if (!confirmed.HasValue || !Same(confirmed.Value, state)) throw new InvalidOperationException("PublicationAuthorityOutcomeUnknown");
        return true;
    }
    internal static bool Valid(SourcePublicationAuthorityEnrollment value, SourcePublicationScope scope)
        => value.Scope == scope && value.InitialSources is { Count: <= 1000 } && value.CommandSources is { Count: <= 1000 }
            && value.InitialSources.Concat(value.CommandSources).All(item => item is not null && item.TenantId == scope.Tenant && item.Domain == scope.Domain)
            && value.InitialSources.Distinct().Count() == value.InitialSources.Count && value.CommandSources.Distinct().Count() == value.CommandSources.Count
            && value.InitialSources.SequenceEqual(value.InitialSources.OrderBy(item => item.ActorId, StringComparer.Ordinal))
            && value.CommandSources.SequenceEqual(value.CommandSources.OrderBy(item => item.ActorId, StringComparer.Ordinal))
            && new[] { value.Epoch, value.StorageIdentity, value.InventoryEvidenceId, value.WriterEnforcementEvidenceId,
                value.ProducerWorkload, value.ObserverWorkload, value.RegistrarWorkload, value.ReaderWorkload, value.RecoveryWorkload,
                value.DeliveryTarget, value.DeliveryAuthorityRevision }.All(item => !string.IsNullOrWhiteSpace(item) && item.Length <= 200)
            && new[] { value.ProducerWorkload, value.ObserverWorkload, value.RegistrarWorkload, value.ReaderWorkload, value.RecoveryWorkload }.Distinct(StringComparer.Ordinal).Count() == 5;
    private bool ValidTransition(AnchoredStateTransition item, SourcePublicationAuthorityEnrollment enrollment, List<SourcePublicationAuthorityRecord> records)
    {
        if (item.ScopeId != enrollment.Scope.ActorId + NamespacePurpose && item.ScopeId != enrollment.Scope.ActorId + IndexPurpose
            || item.ExpectedRevision < 0 || item.TargetRevision != checked(item.ExpectedRevision + 1)
            || item.TargetBytes is not { Length: > 0 and <= RecoverableAnchoredState.MaximumPendingBytes }
            || item.TargetDigest != Convert.ToHexString(SHA256.HashData(item.TargetBytes))) return false;
        if (item.ScopeId.EndsWith(NamespacePurpose, StringComparison.Ordinal))
        {
            var roster = JsonSerializer.Deserialize<SourcePublicationNamespaceState>(item.TargetBytes);
            if (roster is null) return false;
            roster = SourcePublicationNamespaceActor.Capture(roster);
            bool retainedOriginal = Exact(records, "admitted:" + TransitionId(item), JsonSerializer.SerializeToUtf8Bytes(item));
            var previousRoster = Prior<SourcePublicationNamespaceState>(records, item.ScopeId, item.ExpectedRevision);
            if (item.ExpectedRevision == 0 ? roster.Sources.Count != enrollment.InitialSources.Count
                : previousRoster is null || roster.Sources.Count != previousRoster.Sources.Count + 1
                    || previousRoster.Sources.Any(identity => !roster.Sources.Contains(identity))) return false;
            return roster.Scope == enrollment.Scope && roster.Revision == item.TargetRevision && (roster.AuthorityRevision == enrollment.Epoch || retainedOriginal)
                && roster.LegacyCoverageReceipt == enrollment.InventoryEvidenceId && roster.WriterEnforcementReceipt == enrollment.WriterEnforcementEvidenceId
                && roster.InitialSources!.SequenceEqual(enrollment.InitialSources.OrderBy(identity => identity.ActorId, StringComparer.Ordinal))
                && RecoverableAnchoredState.Digest(roster) == item.TargetDigest;
        }
        var index = JsonSerializer.Deserialize<SourcePublicationIndexState>(item.TargetBytes);
        if (index is null || index.Scope != enrollment.Scope || index.Revision != item.TargetRevision || index.Sources is not { Count: <= 1000 }
            || index.Entries is not { Count: <= 10000 } || index.PoisonCode is not (null or "publication-source-regressed" or "publication-source-incompatible"
                or "publication-reference-conflict" or "publication-source-disappeared") || RecoverableAnchoredState.Digest(index) != item.TargetDigest) return false;
        var rosterAnchor = Value<SourcePublicationAuthorityAnchor>(records, "anchor:" + enrollment.Scope.ActorId + NamespacePurpose);
        var installedRoster = rosterAnchor is null ? null : Prior<SourcePublicationNamespaceState>(records, enrollment.Scope.ActorId + NamespacePurpose, rosterAnchor.Revision);
        if (installedRoster is null) return false;
        string currentAuthority = enrollment.Epoch + ":" + installedRoster.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var previousIndex = Prior<SourcePublicationIndexState>(records, item.ScopeId, item.ExpectedRevision);
        if (item.ExpectedRevision > 0 && previousIndex is null || previousIndex is not null
            && (index.Entries.Count < previousIndex.Entries.Count || !previousIndex.Entries.SequenceEqual(index.Entries.Take(previousIndex.Entries.Count))
                || previousIndex.PoisonCode is not null && index.PoisonCode != previousIndex.PoisonCode
                || previousIndex.Sources.Any(head => !index.Sources.Any(head.CanAdvanceUnder))
                || previousIndex.ReconciliationProgress is not null && index.ReconciliationProgress is null && index.PoisonCode is null)) return false;
        if (index.ReconciliationProgress is { } reconciliation && !Exact(records, "reconciliation:" + reconciliation.ReceiptId, JsonSerializer.SerializeToUtf8Bytes(reconciliation))) return false;
        if (index.DispatchProgress is { } dispatch && !Exact(records, "dispatch:" + dispatch.ReceiptId, JsonSerializer.SerializeToUtf8Bytes(dispatch))) return false;
        bool retainedData = previousIndex is not null && index.Sources.SequenceEqual(previousIndex.Sources) && index.Entries.SequenceEqual(previousIndex.Entries);
        bool retainedProgress = previousIndex is not null && RecoverableAnchoredState.Digest(index.ReconciliationProgress)
            == RecoverableAnchoredState.Digest(previousIndex.ReconciliationProgress);
        if (retainedData)
        {
            if (index.AuthorityRevision != previousIndex!.AuthorityRevision) return false;
        }
        else if (index.AuthorityRevision != currentAuthority) return false;
        if (!retainedData || !retainedProgress)
        {
            if (index.ReconciliationProgress is { } proof)
            {
                proof = SourcePublicationReconciliationProgress.Capture(proof);
                if (proof.SourceAuthorityRevision != currentAuthority || proof.OriginalCut is not { } original
                    || original.ValidUntil <= clock.GetUtcNow() || original.ValidUntil > enrollment.ValidUntil
                    || !proof.Sources.Select(head => head.Identity).SequenceEqual(installedRoster.Sources)
                    || proof.Sources.Any(head => head.NativeProof is not { Erased: false, RetainedFloor: 1 } native
                        || !Hexalith.EventStore.Client.Streams.NativeSourceProofCodec.IsValid(native, head.Identity, head.Head, 1))) return false;
                bool initialPartial = previousIndex is null && index.Sources.Count == 0 && index.Entries.Count == 0;
                if (!retainedData && !initialPartial && (proof.VerifiedSources != proof.Sources.Count || !index.Sources.SequenceEqual(proof.Sources))) return false;
                if (!retainedData && !initialPartial)
                {
                    var expectedEntries = previousIndex?.Entries.ToList() ?? [];
                    foreach (var publication in proof.Publications)
                    {
                        var existing = expectedEntries.SingleOrDefault(entry => entry.Publication.PublicationId == publication.PublicationId);
                        if (existing is not null) { if (existing.Publication != publication) return false; }
                        else expectedEntries.Add(new(expectedEntries.Count + 1L, publication));
                    }
                    if (!index.Entries.SequenceEqual(expectedEntries)) return false;
                }
            }
            else if (installedRoster.Sources.Count != 0 || index.Sources.Count != 0 || index.Entries.Count != 0) return false;
        }
        if (index.DispatchProgress is { } dispatchProof && dispatchProof != previousIndex?.DispatchProgress
            && (dispatchProof.Scope != index.Scope || dispatchProof.SourceAuthorityRevision != index.AuthorityRevision
                || dispatchProof.LastOffset != index.Entries.Count || dispatchProof.SourcesDigest != RecoverableAnchoredState.Digest(index.Sources)
                || dispatchProof.AcknowledgedPrefix <= 0 || dispatchProof.AcknowledgedPrefix > index.Entries.Count
                || dispatchProof.PrefixDigest != RecoverableAnchoredState.Digest(index.Entries.Take(checked((int)dispatchProof.AcknowledgedPrefix)).ToArray()))) return false;
        if (index.Entries.Count > 0 && !retainedData && (index.ReconciliationProgress is not { } complete || complete.VerifiedSources != complete.Sources.Count
            || index.Entries.Any(entry => !complete.Publications.Contains(entry.Publication)))) return false;
        return true;
    }
    private static T? Prior<T>(IReadOnlyList<SourcePublicationAuthorityRecord> records, string purpose, long revision)
        => records.Where(record => record.Key.StartsWith("transition:", StringComparison.Ordinal))
            .Select(record => JsonSerializer.Deserialize<AnchoredStateTransition>(record.Body)!)
            .SingleOrDefault(transition => transition.ScopeId == purpose && transition.TargetRevision == revision) is { } original
                ? JsonSerializer.Deserialize<T>(original.TargetBytes) : default;
    internal static string TransitionId(AnchoredStateTransition value) => RecoverableAnchoredState.Digest(value);
    internal static T? Value<T>(IReadOnlyList<SourcePublicationAuthorityRecord> records, string key)
        => records.SingleOrDefault(record => record.Key == key) is { } found ? JsonSerializer.Deserialize<T>(found.Body) : default;
    internal static bool Exact(IReadOnlyList<SourcePublicationAuthorityRecord> records, string key, byte[] body)
        => records.SingleOrDefault(record => record.Key == key) is { } found && found.Body.AsSpan().SequenceEqual(body);
    private static bool Append(List<SourcePublicationAuthorityRecord> records, string key, byte[] body)
    {
        if (records.Any(record => record.Key == key)) return Exact(records, key, body);
        records.Add(new(key, body.ToArray())); return true;
    }
    private static void Put(List<SourcePublicationAuthorityRecord> records, string key, byte[] body)
    { records.RemoveAll(record => record.Key == key); records.Add(new(key, body.ToArray())); }
    private static bool Same<T>(T first, T second) => RecoverableAnchoredState.Digest(first) == RecoverableAnchoredState.Digest(second);
    private static SourcePublicationAuthorityState Clone(SourcePublicationAuthorityState value)
        => JsonSerializer.Deserialize<SourcePublicationAuthorityState>(JsonSerializer.SerializeToUtf8Bytes(value))!;
}
