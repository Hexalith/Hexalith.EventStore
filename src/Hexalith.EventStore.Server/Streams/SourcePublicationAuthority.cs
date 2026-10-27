using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Concrete independent enrollment, admission, exact recovery and observation authority for publication and complete commands.</summary>
/// <remarks>All external observation precedes a separate conditional journal turn. It never calls the requesting namespace/index or a registering aggregate.</remarks>
public sealed class SourcePublicationAuthority(SourcePublicationAuthorityClient journal,
    IReadOnlyList<SourcePublicationAuthorityEnrollment> enrollments, SourcePublicationAuthorityInstallation installation,
    ISourcePublicationAuthorityInstallationEvidence evidence, WorkloadAssertionValidator validator,
    IAuthoritativeEventStreamReader streams, ISourcePublicationProjector projector,
    ISourcePublicationAcknowledgementObserver acknowledgements, TimeProvider clock,
    Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null)
    : ISourcePublicationNamespaceAuthority, ISourcePublicationOperationAuthority, ICompleteCommandSourceAuthority
{
    /// <summary>Creates one finite observation budget for an authenticated primary owner turn.</summary>
    internal AuthoritativeStreamReadDeadline CreateObservationDeadline(CancellationToken cancellationToken = default)
        => new(TimeSpan.FromSeconds(30), clock, cancellationToken.CanBeCanceled ? cancellationToken
            : httpContextAccessor?.HttpContext?.RequestAborted ?? CancellationToken.None, clock.GetTimestamp());

    private async Task<T> ReadExternalAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken = default)
    {
        if (SourcePublicationOperationContext.Deadline is { } current) return await current.ReadWithAdmissionAsync(read).ConfigureAwait(false);
        using var deadline = CreateObservationDeadline(cancellationToken);
        return await deadline.ReadWithAdmissionAsync(read).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public bool Requires(AggregateIdentity identity) => enrollments.Any(enrollment => enrollment.CommandSources.Contains(identity));
    /// <inheritdoc/>
    public async Task<CompleteCommandSourceAuthorization?> AuthorizeAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        var matches = enrollments.Where(enrollment => enrollment.CommandSources.Contains(identity)).ToArray();
        if (matches.Length != 1) return null;
        var state = await CurrentAsync(matches[0].Scope, cancellationToken).ConfigureAwait(false);
        return state is null ? null : new(identity, state.Enrollment.Epoch, state.Enrollment.ValidUntil);
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationNamespaceAuthorization?> AuthorizeAsync(SourcePublicationNamespaceState installed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installed);
        var current = await CurrentAsync(installed.Scope, cancellationToken).ConfigureAwait(false);
        if (current is null || !Anchored(current, installed.Scope.ActorId + SourcePublicationAuthorityActor.NamespacePurpose,
            installed.Revision, RecoverableAnchoredState.Digest(installed))) return null;
        return new(current.Enrollment.Epoch, current.Enrollment.ValidUntil);
    }
    /// <summary>Verifies current enrollment and the exact primary operation assertion.</summary>
    internal async Task<bool> AuthenticateAsync(SourcePublicationAuthorityCall call)
    {
        var state = await CurrentAsync(call.Scope).ConfigureAwait(false);
        if (state is null || state.Enrollment.Epoch != call.Epoch) return false;
        string caller = call.Method == "install-namespace" ? state.Enrollment.RegistrarWorkload
            : call.Method == "register-source" || call.Method.StartsWith("write-", StringComparison.Ordinal)
                || call.Method.StartsWith("advance-", StringComparison.Ordinal) ? state.Enrollment.ProducerWorkload : state.Enrollment.ReaderWorkload;
        var authenticated = await ReadExternalAsync(token => validator.ValidateForAudienceAsync(installation.WorkloadScheme, call.Assertion,
            SourcePublicationWorkloadBinding.PrimaryOperation, [caller], installation.PrimaryAudience, token)).ConfigureAwait(false);
        return SourcePublicationWorkloadBinding.Verify(call, authenticated);
    }
    /// <inheritdoc/>
    public Task<bool> ReadIndexAsync(SourcePublicationScope scope) => PermitAsync(scope, "read-index", "write-index", "read-dispatch", "advance-dispatch", "read-reconciliation", "advance-reconciliation");
    /// <inheritdoc/>
    public Task<bool> ReadNamespaceAsync(SourcePublicationScope scope) => PermitAsync(scope, "read-namespace", "register-source", "install-namespace");
    /// <inheritdoc/>
    public async Task<bool> InstallNamespaceAsync(SourcePublicationNamespaceState value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!ExactInvocation(value.Scope, "install-namespace", value)) return false;
        var state = await CurrentAsync(value.Scope).ConfigureAwait(false); var enrollment = state?.Enrollment;
        return enrollment is not null && value.AuthorityRevision == enrollment.Epoch && value.LegacyCoverageReceipt == enrollment.InventoryEvidenceId
            && value.WriterEnforcementReceipt == enrollment.WriterEnforcementEvidenceId
            && (value.InitialSources ?? value.Sources).SequenceEqual(enrollment.InitialSources.OrderBy(item => item.ActorId, StringComparer.Ordinal));
    }
    /// <inheritdoc/>
    public async Task<bool> RegisterSourceAsync(SourcePublicationScope scope, long expectedRevision, AggregateIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(identity);
        return identity.TenantId == scope.Tenant && identity.Domain == scope.Domain && expectedRevision > 0
            && ExactInvocation(scope, "register-source", new SourcePublicationSourceRegistration(scope, expectedRevision, identity))
            && await CurrentAsync(scope).ConfigureAwait(false) is not null;
    }
    /// <inheritdoc/>
    public async Task<bool> WriteIndexAsync(SourcePublicationIndexWrite request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await PermitAsync(request.State.Scope, "write-index", "advance-dispatch", "advance-reconciliation").ConfigureAwait(false)) return false;
        if (SourcePublicationOperationContext.Current!.Method == "write-index" && !ExactInvocation(request.State.Scope, "write-index", request)) return false;
        var journalState = await CurrentAsync(request.State.Scope).ConfigureAwait(false);
        if (journalState is null || !await VerifyIndexMutationAsync(request, journalState).ConfigureAwait(false)) return false;
        if (request.State.PoisonCode is not null)
        {
            var original = LatestIndex(journalState);
            return original is not null && RecoverableAnchoredState.Digest(request.State.ReconciliationProgress) == RecoverableAnchoredState.Digest(original.ReconciliationProgress)
                && request.State.DispatchProgress == original.DispatchProgress;
        }
        if (request.State.ReconciliationProgress is { } progress && !await VerifyReconciliationProgressAsync(progress).ConfigureAwait(false)) return false;
        // Retaining an already anchored historical acknowledgement is not a claim that it
        // authorizes the new cut. Current-cut reuse/advance is authenticated separately.
        if (request.State.DispatchProgress == LatestIndex(journalState)?.DispatchProgress) return true;
        return request.State.DispatchProgress is null || await VerifyDispatchProgressAsync(
            new(request.State.Scope, request.ExpectedRevision, request.State.AuthorityRevision, request.State.Sources, request.State.Entries.Count), request.State.DispatchProgress).ConfigureAwait(false);
    }
    private async Task<bool> VerifyIndexMutationAsync(SourcePublicationIndexWrite request, SourcePublicationAuthorityState state)
    {
        var next = request.State; var previous = LatestIndex(state); var roster = LatestNamespace(state);
        if (roster is null || next.Revision != request.ExpectedRevision + 1) return false;
        // The actor repeats admission after committing its transition. Only the exact
        // independently anchored successor can satisfy that post-commit verification.
        if (previous is not null && previous.Revision == next.Revision
            && RecoverableAnchoredState.Digest(previous) == RecoverableAnchoredState.Digest(next)) return true;
        if (request.ExpectedRevision != (previous?.Revision ?? 0)) return false;
        bool preservesData = previous is not null && next.AuthorityRevision == previous.AuthorityRevision
            && next.Sources.SequenceEqual(previous.Sources) && next.Entries.SequenceEqual(previous.Entries);
        if (next.PoisonCode is not null)
        {
            if (previous is null || !preservesData) return false;
            string? poison = previous.Sources.Any(head => !roster.Sources.Contains(head.Identity)) ? "publication-source-disappeared" : null;
            foreach (var head in previous.Sources)
            {
                var read = await ReadExternalAsync(token => streams.ReadAsync(head.Identity, token)).ConfigureAwait(false);
                if (!read.IsAuthoritative || read.Stream is not { } source || source.NativeProof is null) return false;
                if (source.Head < head.Head) { poison ??= "publication-source-regressed"; continue; }
                if (source.NativeProof.SourceIncarnation != head.NativeProof?.SourceIncarnation
                    || source.Head == head.Head && source.NativeProof != head.NativeProof) { poison ??= "publication-source-incompatible"; continue; }
                var projected = await ObserveAsync(head).ConfigureAwait(false);
                if (projected is null) return false;
                if (previous.Entries.Any(entry => entry.Publication.Identity == head.Identity && !projected.Contains(entry.Publication)))
                    poison ??= "publication-reference-conflict";
            }
            var projectedCurrent = new List<SourcePublicationDescriptor>();
            foreach (var identity in roster.Sources)
            {
                var read = await ReadExternalAsync(token => streams.ReadAsync(identity, token)).ConfigureAwait(false);
                if (!read.IsAuthoritative || read.Stream is not { NativeProof: { } proof } current) return false;
                IReadOnlyList<SourcePublicationDescriptor>? projected;
                try { projected = await ReadExternalAsync(token => Task.FromResult(projector.Project(current, token))).ConfigureAwait(false); }
                catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
                { poison ??= "publication-source-incompatible"; continue; }
                if (projected is null) return false;
                foreach (var publication in projected)
                {
                    if (projectedCurrent.Any(item => item.PublicationId == publication.PublicationId && item != publication)) poison ??= "publication-reference-conflict";
                    projectedCurrent.Add(publication);
                }
            }
            return poison == next.PoisonCode;
        }
        // Partial reconciliation and acknowledgement retention can only preserve the already anchored data.
        if (preservesData) return true;
        if (next.ReconciliationProgress is { } progress)
        {
            if (next.Sources.Count == 0 && next.Entries.Count == 0 && previous is null) return true;
            if (progress.VerifiedSources != progress.Sources.Count || !next.Sources.SequenceEqual(progress.Sources)
                || next.AuthorityRevision != progress.SourceAuthorityRevision) return false;
            var entries = previous?.Entries.ToList() ?? [];
            foreach (var publication in progress.Publications)
            {
                var original = entries.SingleOrDefault(item => item.Publication.PublicationId == publication.PublicationId);
                if (original is not null) { if (original.Publication != publication) return false; }
                else entries.Add(new(entries.Count + 1L, publication));
            }
            return next.Entries.SequenceEqual(entries);
        }
        return roster.Sources.Count == 0 && next.Sources.Count == 0 && next.Entries.Count == 0
            && next.AuthorityRevision == state.Enrollment.Epoch + ":" + roster.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    /// <inheritdoc/>
    public Task<bool> ValidateIndexStateAsync(SourcePublicationScope scope, long revision, string stateDigest)
    { ArgumentNullException.ThrowIfNull(scope); return ValidateAnchorAsync(scope, scope.ActorId + SourcePublicationAuthorityActor.IndexPurpose, revision, stateDigest); }
    /// <inheritdoc/>
    public Task<bool> ValidateNamespaceStateAsync(SourcePublicationScope scope, long revision, string stateDigest)
    { ArgumentNullException.ThrowIfNull(scope); return ValidateAnchorAsync(scope, scope.ActorId + SourcePublicationAuthorityActor.NamespacePurpose, revision, stateDigest); }
    /// <inheritdoc/>
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync("transition-admit", transition, cancellationToken);
    /// <inheritdoc/>
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync("transition-commit", transition, cancellationToken);
    /// <inheritdoc/>
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync("transition-recover", transition, cancellationToken);
    /// <inheritdoc/>
    public async Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        var enrollment = EnrollmentForTransition(transition);
        if (enrollment is null || await CurrentAsync(enrollment.Scope, cancellationToken).ConfigureAwait(false) is null) return false;
        var retained = await ReadExternalAsync(token => journal.LookupAsync<AnchoredStateTransition>(enrollment,
            "transition:" + SourcePublicationAuthorityActor.TransitionId(transition), token), cancellationToken).ConfigureAwait(false);
        return retained is not null && RecoverableAnchoredState.Digest(retained) == RecoverableAnchoredState.Digest(transition);
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationReconciliationProgress?> AuthorizeReconciliationAdvanceAsync(SourcePublicationReconciliationAdvance advance, SourcePublicationIndexState? current)
    {
        ArgumentNullException.ThrowIfNull(advance);
        if (!ExactInvocation(advance.Cut.Scope, "advance-reconciliation", advance)) return null;
        var state = await CurrentAsync(advance.Cut.Scope).ConfigureAwait(false);
        if (state is null || !await VerifyCutAsync(advance.CurrentCut ?? advance.Cut, state).ConfigureAwait(false)
            || advance.Cut.ValidUntil <= clock.GetUtcNow() || advance.ExpectedVerifiedSources < 0
            || advance.ExpectedVerifiedSources >= advance.Cut.Sources.Count || advance.Source != advance.Cut.Sources[advance.ExpectedVerifiedSources]) return null;
        var prior = current?.ReconciliationProgress;
        bool reusable = prior is not null && prior.MatchesCut(advance.Cut) && await VerifyReconciliationProgressAsync(prior).ConfigureAwait(false);
        if ((reusable ? prior!.VerifiedSources : 0) != advance.ExpectedVerifiedSources) return null;
        var projected = await ObserveAsync(advance.Source).ConfigureAwait(false);
        if (projected is null || !projected.SequenceEqual(advance.Publications)) return null;
        var issued = SourcePublicationAuthorityActor.Value<SourcePublicationReconciliationProgress>(state.Records, "reconciliation-head");
        long primaryVersion = prior?.Version ?? 0;
        var proposed = SourcePublicationReconciliationProgress.Capture(new(advance.Cut.Scope, Math.Max(primaryVersion, issued?.Version ?? 0) + 1,
            advance.Cut.AuthorityRevision, advance.Cut.Sources, advance.ExpectedVerifiedSources + 1,
            (reusable ? prior!.Publications : []).Concat(projected).ToArray(), "pending")
            { OriginalCut = reusable ? prior!.OriginalCut ?? advance.Cut : advance.Cut, ExpectedPredecessorVersion = primaryVersion });
        proposed = proposed with { ReceiptId = RecoverableAnchoredState.Digest(proposed) };
        // A lost issuance response must recover the independently retained original, including its original expiry.
        if (issued is not null && issued.Version > primaryVersion && issued.ExpectedPredecessorVersion == primaryVersion && issued.VerifiedSources == proposed.VerifiedSources
            && issued.MatchesCut(advance.Cut) && issued.Publications.SequenceEqual(proposed.Publications)
            && await VerifyReconciliationProgressAsync(issued).ConfigureAwait(false)) return issued;
        if (await CurrentAsync(advance.Cut.Scope).ConfigureAwait(false) is null) return null;
        return await ReadExternalAsync(token => journal.CallAsync<SourcePublicationReconciliationProgress>(state.Enrollment, "reconciliation", proposed, token)).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<bool> VerifyReconciliationProgressAsync(SourcePublicationReconciliationProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var state = await CurrentAsync(progress.Scope).ConfigureAwait(false);
        if (state is null || progress.OriginalCut is not { } original || original.ValidUntil <= clock.GetUtcNow()
            || !SourcePublicationAuthorityActor.Exact(state.Records, "reconciliation:" + progress.ReceiptId, JsonSerializer.SerializeToUtf8Bytes(progress))
            || !await VerifyCutAsync(original, state, allowGrowth: true).ConfigureAwait(false)) return false;
        var projected = new List<SourcePublicationDescriptor>();
        foreach (var head in progress.Sources.Take(progress.VerifiedSources))
        {
            var source = await ObserveAsync(head).ConfigureAwait(false);
            if (source is null) return false;
            projected.AddRange(source);
        }
        return projected.SequenceEqual(progress.Publications) && await CurrentAsync(progress.Scope).ConfigureAwait(false) is not null;
    }
    /// <inheritdoc/>
    public async Task<bool> VerifyFiniteCutContinuationAsync(SourcePublicationReconciliationProgress original, SourcePublicationCut currentCut)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(currentCut);
        var state = await CurrentAsync(currentCut.Scope).ConfigureAwait(false);
        return state is not null && original.CanContinueUnder(currentCut) && original.OriginalCut!.ValidUntil > clock.GetUtcNow()
            && await VerifyCutAsync(currentCut, state).ConfigureAwait(false) && await VerifyReconciliationProgressAsync(original).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationDispatchProgress?> AuthorizeDispatchAdvanceAsync(SourcePublicationDispatchAdvance advance, SourcePublicationIndexState current)
    {
        ArgumentNullException.ThrowIfNull(advance); ArgumentNullException.ThrowIfNull(current);
        if (!ExactInvocation(advance.Cut.Scope, "advance-dispatch", advance)) return null;
        var state = await CurrentAsync(advance.Cut.Scope).ConfigureAwait(false);
        if (state is null || !await VerifyCheckpointAsync(advance.Cut, state).ConfigureAwait(false)
            || advance.AcknowledgedEntries is not { Count: > 0 and <= 100 } || advance.ExpectedPrefix < 0
            || !advance.AcknowledgedEntries.SequenceEqual(current.Entries.Skip(checked((int)advance.ExpectedPrefix)).Take(advance.AcknowledgedEntries.Count))) return null;
        long predecessor = current.DispatchProgress is { } previous && await VerifyDispatchProgressAsync(advance.Cut, previous).ConfigureAwait(false)
            ? previous.AcknowledgedPrefix : 0;
        if (predecessor != advance.ExpectedPrefix) return null;
        var receipts = new List<SourcePublicationAcknowledgement>();
        for (int index = 0; index < advance.AcknowledgedEntries.Count; index++)
        {
            var entry = advance.AcknowledgedEntries[index];
            var receipt = await ObserveAcknowledgementAsync(entry, state).ConfigureAwait(false);
            if (entry.Offset != predecessor + index + 1 || receipt is null) return null;
            receipts.Add(receipt);
        }
        long prefix = advance.AcknowledgedEntries[^1].Offset;
        var progress = new SourcePublicationDispatchProgress(advance.Cut.Scope, (current.DispatchProgress?.Version ?? 0) + 1, prefix,
            advance.Cut.AuthorityRevision, RecoverableAnchoredState.Digest(advance.Cut.Sources), advance.Cut.LastOffset,
            RecoverableAnchoredState.Digest(current.Entries.Take(checked((int)prefix)).ToArray()), state.Enrollment.DeliveryTarget,
            state.Enrollment.DeliveryAuthorityRevision, "pending");
        var proof = new SourcePublicationDispatchProof(progress, predecessor, predecessor == 0 ? null : current.DispatchProgress!.ReceiptId, receipts.AsReadOnly());
        progress = progress with { ReceiptId = RecoverableAnchoredState.Digest(proof) }; proof = proof with { Progress = progress };
        if (await CurrentAsync(progress.Scope).ConfigureAwait(false) is null) return null;
        return await ReadExternalAsync(token => journal.CallAsync<SourcePublicationDispatchProgress>(state.Enrollment, "dispatch", proof, token)).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<bool> VerifyDispatchProgressAsync(SourcePublicationCheckpoint cut, SourcePublicationDispatchProgress progress)
    {
        ArgumentNullException.ThrowIfNull(cut); ArgumentNullException.ThrowIfNull(progress);
        var state = await CurrentAsync(cut.Scope).ConfigureAwait(false);
        if (state is null || progress.Scope != cut.Scope || !KnownDeliveryBinding(state, progress.DeliveryTarget, progress.DeliveryAuthorityRevision)
            || progress.SourceAuthorityRevision != cut.AuthorityRevision
            || progress.LastOffset != cut.LastOffset || progress.SourcesDigest != RecoverableAnchoredState.Digest(cut.Sources)
            || !SourcePublicationAuthorityActor.Exact(state.Records, "dispatch:" + progress.ReceiptId, JsonSerializer.SerializeToUtf8Bytes(progress))
            || !await VerifyCheckpointAsync(cut, state).ConfigureAwait(false)) return false;
        var index = LatestIndex(state);
        if (index is null || progress.AcknowledgedPrefix > index.Entries.Count
            || progress.PrefixDigest != RecoverableAnchoredState.Digest(index.Entries.Take(checked((int)progress.AcknowledgedPrefix)).ToArray())) return false;
        var retained = new Dictionary<long, SourcePublicationAcknowledgement>();
        var chain = SourcePublicationAuthorityActor.Value<SourcePublicationDispatchProof>(state.Records, "dispatch-proof:" + progress.ReceiptId);
        long expectedPrefix = progress.AcknowledgedPrefix;
        while (chain is not null)
        {
            if (chain.Progress.AcknowledgedPrefix != expectedPrefix || chain.ExpectedPrefix < 0 || chain.Receipts.Count is < 1 or > 100) return false;
            foreach (var receipt in chain.Receipts) if (!retained.TryAdd(receipt.Entry.Offset, receipt)) return false;
            expectedPrefix = chain.ExpectedPrefix;
            if (expectedPrefix == 0) break;
            chain = SourcePublicationAuthorityActor.Value<SourcePublicationDispatchProof>(state.Records, "dispatch-proof:" + chain.PredecessorReceiptId);
        }
        if (expectedPrefix != 0 || retained.Count != progress.AcknowledgedPrefix) return false;
        foreach (var entry in index.Entries.Take(checked((int)progress.AcknowledgedPrefix)))
            if (!retained.TryGetValue(entry.Offset, out var original)
                || await ObserveAcknowledgementAsync(entry, state).ConfigureAwait(false) != original) return false;
        return await CurrentAsync(cut.Scope).ConfigureAwait(false) is not null;
    }
    private async Task<SourcePublicationAcknowledgement?> ObserveAcknowledgementAsync(SourcePublicationIndexEntry entry, SourcePublicationAuthorityState state)
    {
        var source = await ReadExternalAsync(token => acknowledgements.ReadSourceAsync(entry, token)).ConfigureAwait(false);
        var receiver = await ReadExternalAsync(token => acknowledgements.ReadReceiverAsync(entry, token)).ConfigureAwait(false);
        return source is not null && source == receiver && source.Entry == entry && source.ProtectedDeletionRevision > 0
            && !string.IsNullOrWhiteSpace(source.ReceiptId) && source.ReceiptId.Length <= 256
            && KnownDeliveryBinding(state, source.DeliveryTarget, source.DeliveryAuthorityRevision) ? source : null;
    }
    private static bool KnownDeliveryBinding(SourcePublicationAuthorityState state, string target, string revision)
        => state.Enrollment.DeliveryTarget == target && state.Enrollment.DeliveryAuthorityRevision == revision
            || state.Records.Where(item => item.Key.StartsWith("enrollment-predecessor:", StringComparison.Ordinal))
                .Select(item => JsonSerializer.Deserialize<SourcePublicationAuthorityEnrollment>(item.Body)!)
                .Any(item => item.Scope == state.Enrollment.Scope && item.Epoch == state.Enrollment.Epoch
                    && item.DeliveryTarget == target && item.DeliveryAuthorityRevision == revision);
    private async Task<IReadOnlyList<SourcePublicationDescriptor>?> ObserveAsync(SourcePublicationHead head)
    {
        if (head.NativeProof is not { Erased: false, RetainedFloor: 1 } || head.Head is < 0 or > 10000) return null;
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, CancellationToken.None, clock.GetTimestamp());
        var read = await ReadExternalAsync(token => streams.ReadAsync(head.Identity, token)).ConfigureAwait(false);
        if (!read.IsAuthoritative || read.Stream is not { } source || source.Identity != head.Identity || source.Head < head.Head
            || source.Head > 10000 || source.ObservedAt > clock.GetUtcNow() || source.ObservedAt == default
            || !NativeSourcePrefix.Matches(source, head, deadline)) return null;
        var prefix = source.Events.Take(checked((int)head.Head)).Select(item => item with { Payload = item.Payload.ToArray() }).ToArray();
        if (prefix.Sum(item => (long)item.Payload.Length) > CompleteCommandSource.MaximumPayloadBytes) return null;
        var projected = await ReadExternalAsync(token => Task.FromResult(projector.Project(source with { Head = head.Head, Events = prefix, NativeProof = head.NativeProof }, token))).ConfigureAwait(false);
        if (projected is null || projected.Count > 10000 || projected.Any(item => item.Identity != head.Identity || item.SourceRevision <= 0
            || item.SourceRevision > head.Head || prefix[checked((int)item.SourceRevision - 1)].MessageId != item.SourceMessageId)) return null;
        return projected.OrderBy(item => item.SourceRevision).ToArray();
    }
    private async Task<bool> VerifyCutAsync(SourcePublicationCut cut, SourcePublicationAuthorityState state, bool allowGrowth = false)
    {
        var roster = LatestNamespace(state);
        if (!cut.IsComplete || cut.Scope != state.Enrollment.Scope || cut.ObservedAt == default || cut.ObservedAt > clock.GetUtcNow()
            || cut.ValidUntil <= clock.GetUtcNow() || cut.ValidUntil > state.Enrollment.ValidUntil || roster is null
            || cut.AuthorityRevision != state.Enrollment.Epoch + ":" + roster.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)
            || !cut.Sources.Select(item => item.Identity).SequenceEqual(roster.Sources)) return false;
        foreach (var head in cut.Sources)
        {
            var read = await ReadExternalAsync(token => streams.ReadAsync(head.Identity, token)).ConfigureAwait(false);
            if (!read.IsAuthoritative || read.Stream is not { } source || source.Identity != head.Identity || head.NativeProof is null
                || !allowGrowth && source.Head != head.Head) return false;
            using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, CancellationToken.None, clock.GetTimestamp());
            if (!NativeSourcePrefix.Matches(source, head, deadline)) return false;
        }
        return evidence.IsCurrent(state.Enrollment) && state.Enrollment.ValidUntil > clock.GetUtcNow();
    }
    private async Task<bool> VerifyCheckpointAsync(SourcePublicationCheckpoint cut, SourcePublicationAuthorityState state)
        => await VerifyCutAsync(new(cut.Scope, cut.AuthorityRevision, clock.GetUtcNow(), state.Enrollment.ValidUntil, cut.Sources, true), state).ConfigureAwait(false)
            && LatestIndex(state) is { PoisonCode: null } index && (index.Revision >= cut.IndexRevision
                || SourcePublicationOperationContext.Current?.Method == "advance-dispatch" && index.Revision + 1 == cut.IndexRevision)
            && index.Entries.Count == cut.LastOffset && index.Sources.SequenceEqual(cut.Sources);
    private async Task<bool> TransitionAsync(string method, AnchoredStateTransition transition, CancellationToken cancellationToken)
    {
        var enrollment = EnrollmentForTransition(transition);
        if (enrollment is null || !await PermitAsync(enrollment.Scope, "write-index", "advance-dispatch", "advance-reconciliation", "install-namespace", "register-source").ConfigureAwait(false)) return false;
        return await ReadExternalAsync(token => journal.CallAsync<bool>(enrollment, method, transition, token), cancellationToken).ConfigureAwait(false);
    }
    private SourcePublicationAuthorityEnrollment? EnrollmentForTransition(AnchoredStateTransition transition)
        => enrollments.SingleOrDefault(item => transition.ScopeId == item.Scope.ActorId + SourcePublicationAuthorityActor.IndexPurpose
            || transition.ScopeId == item.Scope.ActorId + SourcePublicationAuthorityActor.NamespacePurpose);
    private async Task<bool> ValidateAnchorAsync(SourcePublicationScope scope, string purpose, long revision, string digest)
    {
        var state = await CurrentAsync(scope).ConfigureAwait(false);
        return state is not null && Anchored(state, purpose, revision, digest);
    }
    private static bool Anchored(SourcePublicationAuthorityState state, string purpose, long revision, string digest)
    {
        var anchor = SourcePublicationAuthorityActor.Value<SourcePublicationAuthorityAnchor>(state.Records, "anchor:" + purpose);
        return anchor is null ? revision == 0 && digest == RecoverableAnchoredState.Digest<object?>(null) : anchor.Revision == revision && anchor.Digest == digest;
    }
    private async Task<bool> PermitAsync(SourcePublicationScope scope, params string[] methods)
        => SourcePublicationOperationContext.Current is { } call && call.Scope == scope && methods.Contains(call.Method, StringComparer.Ordinal)
            && await AuthenticateAsync(call).ConfigureAwait(false);
    private static bool ExactInvocation(SourcePublicationScope scope, string method, object body)
        => SourcePublicationOperationContext.Current is { } call && call.Scope == scope && call.Method == method
            && call.Body.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(body, body.GetType()));
    private async Task<SourcePublicationAuthorityState?> CurrentAsync(SourcePublicationScope scope, CancellationToken cancellationToken = default)
    {
        var enrollment = enrollments.SingleOrDefault(item => item.Scope == scope);
        if (enrollment is null || enrollment.ValidUntil <= clock.GetUtcNow() || !evidence.IsCurrent(enrollment)) return null;
        var state = await ReadExternalAsync(token => journal.ReadAsync(enrollment, token), cancellationToken).ConfigureAwait(false);
        return state is not null && RecoverableAnchoredState.Digest(state.Enrollment) == RecoverableAnchoredState.Digest(enrollment)
            && evidence.IsCurrent(state.Enrollment) && state.Enrollment.ValidUntil > clock.GetUtcNow() ? state : null;
    }
    private static SourcePublicationNamespaceState? LatestNamespace(SourcePublicationAuthorityState state)
        => Latest<SourcePublicationNamespaceState>(state, SourcePublicationAuthorityActor.NamespacePurpose);
    private static SourcePublicationIndexState? LatestIndex(SourcePublicationAuthorityState state)
        => Latest<SourcePublicationIndexState>(state, SourcePublicationAuthorityActor.IndexPurpose);
    private static T? Latest<T>(SourcePublicationAuthorityState state, string purpose)
    {
        var transitions = state.Records.Where(item => item.Key.StartsWith("transition:", StringComparison.Ordinal))
            .Select(item => JsonSerializer.Deserialize<AnchoredStateTransition>(item.Body)!)
            .Where(item => item.ScopeId == state.Enrollment.Scope.ActorId + purpose).OrderByDescending(item => item.TargetRevision);
        return transitions.FirstOrDefault() is { } latest ? JsonSerializer.Deserialize<T>(latest.TargetBytes) : default;
    }
}
