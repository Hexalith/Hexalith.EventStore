using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Private bounded typed owner. Every permitted source/outbox/phase mutation and its append-time guard state share the mandatory installed backend guard CAS.</summary>
/// <param name="transaction">Actual shared qualified transaction source; ordinary aggregate writers remain unchanged.</param>
/// <param name="clock">Whole-operation clock.</param><param name="authority">Independent current exact source/policy/owner evidence; omission disables all operations.</param>
public sealed class GovernanceScopeGuardOwner(DaprGuardedStateTransaction transaction, TimeProvider clock, IGovernanceGuardAuthority? authority = null) : IGovernanceScopeGuard
{
    /// <inheritdoc/>
    public async Task<TenantGovernanceGuardState?> ReadAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        try
        {
            deadline.ThrowIfCancellationRequested();
            if (authority is null) { return null; }
            var cell = await deadline.ReadAsync(token => transaction.ReadGuardAsync(tenantId, token)).ConfigureAwait(false);
            if (cell is null) { return null; }
            var state = await deadline.ReadAsync(_ => Task.FromResult(JsonSerializer.Deserialize<TenantGovernanceGuardState>(cell.Value))).ConfigureAwait(false);
            if (state is null || state.TenantId != tenantId || state.InstallationId != cell.InstallationId || state.Revision <= 0 || state.Revision > cell.Revision
                || state.Deletions is null || state.Deletions.Any(value => value is null || value.AdmissionFenceGuardRevision <= 0)) { return null; }
            var final = await deadline.ReadAsync(token => transaction.ReadGuardAsync(tenantId, token)).ConfigureAwait(false); deadline.ThrowIfCancellationRequested();
            return final is not null && final.Revision == cell.Revision && final.InstallationId == cell.InstallationId && final.Value.AsSpan().SequenceEqual(cell.Value) ? state : null;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }
    /// <inheritdoc/>
    public async Task<GovernanceSigningRecovery?> ReadNoIssueAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, string detachedJwsDigest, CancellationToken cancellationToken = default)
    {
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        try
        {
            deadline.ThrowIfCancellationRequested();
            if (authority is null || payload is null || signingRequestId != DeletionBatchCapabilityIdentity.SigningRequestId(payload)
                || detachedJwsDigest is not { Length: 64 } || !detachedJwsDigest.All(char.IsAsciiHexDigit)) { return null; }
            string operationId = "issue-" + Hash(new[] { signingRequestId, detachedJwsDigest });
            var original = await deadline.ReadAsync(token => transaction.LookupOriginalAsync(payload.TenantId, operationId, Hash(new { Payload = payload, signingRequestId, detachedJwsDigest }), token)).ConfigureAwait(false);
            if (original.Status != GuardedStateCommitStatus.Committed || original.Receipt is null) { return null; }
            var proof = JsonSerializer.Deserialize<GovernanceProtocolReceipt>(original.Receipt.Outcome);
            if (proof is null || proof.Status is not ("Stale" or "IssuanceStale") || proof.Capability != payload || proof.SigningRequestId != signingRequestId
                || proof.DetachedJwsDigest != detachedJwsDigest || proof.OperationId != operationId || proof.GuardHighWater != original.Receipt.LogicalGuardHighWater) { return null; }
            var state = await deadline.ReadAsync(token => ReadAsync(payload.TenantId, token)).ConfigureAwait(false);
            if (state is null) { return null; }
            var read = await deadline.ReadAsync(token => authority.ReadSigningRecoveryAsync(Copy(proof), Copy(state), token)).ConfigureAwait(false);
            if (read is null) { return null; }
            var recovery = Copy(ValidateStrings(read));
            if (recovery.Payload != payload || recovery.SigningRequestId != signingRequestId || recovery.DetachedJwsDigest != detachedJwsDigest || recovery.NoIssueReceiptId != proof.ReceiptId
                || recovery.CurrentGuardRevision != state.Revision || state.Revision <= payload.IntendedIssuedGuardRevision || string.IsNullOrWhiteSpace(recovery.CurrentHealthyKeyVersion)
                || state.CompromisedKeyVersions.Contains(recovery.CurrentHealthyKeyVersion, StringComparer.Ordinal) || !Current(recovery)) { return null; }
            var finalState = await deadline.ReadAsync(token => ReadAsync(payload.TenantId, token)).ConfigureAwait(false);
            var final = await deadline.ReadAsync(token => authority.ReadSigningRecoveryAsync(Copy(proof), Copy(state), token)).ConfigureAwait(false);
            return Hash(finalState) == Hash(state) && Hash(final) == Hash(recovery) && Current(recovery) ? recovery : null;
            bool Current(GovernanceSigningRecovery value) => !string.IsNullOrWhiteSpace(value.AuthorityRevision) && value.ObservedAt != default && value.ObservedAt <= clock.GetUtcNow() && value.ValidUntil > clock.GetUtcNow();
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }
    /// <summary>Conditionally applies the exact protocol transition; unknown commit uses only original lookup. A stale/obsolete result persists without a logical guard append or source effect.</summary>
    public async Task<GovernanceProtocolReceipt?> ExecuteAsync(GovernanceGuardTransition transition, IReadOnlyList<GuardedStateMutation> targets,
        CancellationToken cancellationToken = default)
    {
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        try
        {
            deadline.ThrowIfCancellationRequested();
            if (authority is null || transition is null || !GovernanceScopeGuardReducer.IsClosedCommand(transition) || targets is null) { return null; }
            transition = await deadline.ReadAsync(_ => Task.FromResult(Capture(transition))).ConfigureAwait(false);
            targets = await deadline.ReadAsync(_ =>
            {
                if (targets.Count > 1000) { throw new ArgumentException("Oversized joint write."); }
                long bytes = 0; var capturedTargets = new List<GuardedStateMutation>();
                foreach (var value in targets)
                {
                    deadline.ThrowIfCancellationRequested();
                    if (capturedTargets.Count >= 1000 || value is null || value.NextValue is null || value.NextValue.Length > 16 * 1024 * 1024 - bytes
                        || string.IsNullOrWhiteSpace(value.CellId) || value.CellId.Length > 2048 || value.ExpectedDigest is not { Length: 64 } || !value.ExpectedDigest.All(char.IsAsciiHexDigit)) { throw new ArgumentException("Oversized or malformed joint write."); }
                    new System.Text.UTF8Encoding(false, true).GetByteCount(value.CellId);
                    bytes += value.NextValue.Length; capturedTargets.Add(value with { NextValue = value.NextValue.ToArray() });
                }
                return Task.FromResult<IReadOnlyList<GuardedStateMutation>>(capturedTargets.AsReadOnly());
            }).ConfigureAwait(false);
            if (targets.Count != 0 && transition.Operation != GovernanceGuardOperation.AppendWrite) { return null; }
            string targetDigest = Hash(targets);
            string intentDigest = Hash(new { Transition = transition, TargetMutationDigest = targetDigest });
            var lookupAuthority = await LookupEvidenceAsync().ConfigureAwait(false);
            if (lookupAuthority is null) { return null; }
            var guard = await deadline.ReadAsync(token => transaction.ReadGuardAsync(transition.TenantId, token)).ConfigureAwait(false);
            if (guard is null) { return null; }
            var state = JsonSerializer.Deserialize<TenantGovernanceGuardState>(guard.Value);
            if (state is null || state.TenantId != guard.TenantId || state.InstallationId != guard.InstallationId || state.Revision <= 0 || state.Revision > guard.Revision
                || state.Deletions is null || state.Deletions.Any(value => value is null || value.AdmissionFenceGuardRevision <= 0)) { return null; }
            var prior = await deadline.ReadAsync(token => transaction.LookupByIntentAsync(transition.TenantId, transition.OperationId, intentDigest, token)).ConfigureAwait(false);
            if (prior.Status == GuardedStateCommitStatus.Committed)
            {
                var original = Parse(prior);
                return original is not null && Hash(await LookupEvidenceAsync().ConfigureAwait(false)) == Hash(lookupAuthority) && Current(lookupAuthority) ? original : null;
            }
            if (prior.Status != GuardedStateCommitStatus.Unknown) { return null; }
            var evidence = await EvidenceAsync().ConfigureAwait(false);
            if (evidence is null) { return null; }
            var reduction = await deadline.ReadAsync(_ => Task.FromResult(GovernanceScopeGuardReducer.Reduce(Copy(state), Copy(transition), Copy(evidence), intentDigest))).ConfigureAwait(false);
            if (reduction.AllowsTargetWrites && targets.Count == 0) { return null; }
            byte[] nextGuard = reduction.MutatesGuard ? JsonSerializer.SerializeToUtf8Bytes(reduction.State) : guard.Value.ToArray();
            var request = new GuardedStateCommitRequest(transition.TenantId, transition.OperationId,
                new(guard.CellId, guard.Revision, HashBytes(guard.Value), nextGuard), reduction.AllowsTargetWrites ? targets : [])
                { CompareGuardWithoutMutation = !reduction.MutatesGuard, LogicalIntentDigest = intentDigest, Outcome = JsonSerializer.SerializeToUtf8Bytes(reduction.Receipt), LogicalGuardHighWater = reduction.State.Revision };
            if (Hash(await EvidenceAsync().ConfigureAwait(false)) != Hash(evidence) || !Current(evidence)) { return null; }
            var result = await deadline.ReadAsync(token => transaction.CommitAsync(request, token)).ConfigureAwait(false);
            if (result.Status != GuardedStateCommitStatus.Committed) { return null; }
            var outcome = Parse(result);
            return outcome is not null && Hash(await EvidenceAsync().ConfigureAwait(false)) == Hash(evidence) && Current(evidence) ? outcome : null;

            async Task<GovernanceGuardEvidence?> EvidenceAsync()
            {
                var read = await deadline.ReadAsync(token => authority.ReadAsync(Copy(transition), intentDigest, targetDigest, Copy(state), token)).ConfigureAwait(false);
                if (read is null) { return null; }
                var owned = await deadline.ReadAsync(_ => Task.FromResult(Capture(read))).ConfigureAwait(false);
                return owned.TenantId == transition.TenantId && owned.IntentDigest == intentDigest && owned.TargetMutationDigest == targetDigest
                    && !string.IsNullOrWhiteSpace(owned.AuthorityRevision) && !string.IsNullOrWhiteSpace(owned.AuthorityReceiptId) && Current(owned) ? owned : null;
            }
            async Task<GovernanceGuardEvidence?> LookupEvidenceAsync()
            {
                var read = await deadline.ReadAsync(token => authority.ReadLookupAsync(Copy(transition), intentDigest, targetDigest, token)).ConfigureAwait(false);
                if (read is null) { return null; }
                var owned = await deadline.ReadAsync(_ => Task.FromResult(Capture(read))).ConfigureAwait(false);
                return owned.TenantId == transition.TenantId && owned.IntentDigest == intentDigest && owned.TargetMutationDigest == targetDigest
                    && !string.IsNullOrWhiteSpace(owned.AuthorityRevision) && !string.IsNullOrWhiteSpace(owned.AuthorityReceiptId) && Current(owned) ? owned : null;
            }
            GovernanceProtocolReceipt? Parse(GuardedStateCommitResult result)
            {
                var receipt = result.Receipt;
                if (receipt is null || receipt.LogicalIntentDigest != intentDigest) { return null; }
                var protocol = JsonSerializer.Deserialize<GovernanceProtocolReceipt>(receipt.Outcome);
                return protocol is not null && protocol.OperationId == transition.OperationId && protocol.IntentDigest == intentDigest
                    && protocol.GuardHighWater == receipt.LogicalGuardHighWater && !string.IsNullOrWhiteSpace(protocol.ReceiptId) ? protocol : null;
            }
            bool Current(GovernanceGuardEvidence proof) => proof.ObservedAt != default && proof.ObservedAt <= clock.GetUtcNow() && proof.ValidUntil > clock.GetUtcNow();
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }

    private static T Copy<T>(T value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length > 16 * 1024 * 1024) { throw new ArgumentException("Oversized private owner state."); }
        return JsonSerializer.Deserialize<T>(bytes)!;
    }
    private static GovernanceGuardTransition Capture(GovernanceGuardTransition value)
        => Copy(ValidateStrings(value with
        {
            Repair = value.Repair is null ? null : value.Repair with { Cohort = List(value.Repair.Cohort) },
            Ordinal = value.Ordinal is null ? null : value.Ordinal with { InvalidatedArtifactIds = List(value.Ordinal.InvalidatedArtifactIds) },
            Batch = value.Batch is null ? null : value.Batch with { Targets = List(value.Batch.Targets), TargetReceiptIds = List(value.Batch.TargetReceiptIds) },
            RevocationReceipt = value.RevocationReceipt is null ? null : value.RevocationReceipt with { AffectedBatchIds = List(value.RevocationReceipt.AffectedBatchIds) },
        }));
    private static GovernanceGuardEvidence Capture(GovernanceGuardEvidence value)
        => Copy(ValidateStrings(value with { RequiredOwnerIds = List(value.RequiredOwnerIds), ObligationIds = List(value.ObligationIds), RepairCohort = List(value.RepairCohort),
            RequiredOutcomeReceiptIds = List(value.RequiredOutcomeReceiptIds),
            ProtectionOriginalRequest = value.ProtectionOriginalRequest is null ? null : value.ProtectionOriginalRequest with { Targets = List(value.ProtectionOriginalRequest.Targets) },
            ProtectionTerminalOutcome = value.ProtectionTerminalOutcome is null ? null : value.ProtectionTerminalOutcome with { TargetReceipts = List(value.ProtectionTerminalOutcome.TargetReceipts) },
            ProtectionBlockedReplacement = value.ProtectionBlockedReplacement is null ? null : value.ProtectionBlockedReplacement with {
                Original = value.ProtectionBlockedReplacement.Original with { Targets = List(value.ProtectionBlockedReplacement.Original.Targets),
                    RevocationReceipt = value.ProtectionBlockedReplacement.Original.RevocationReceipt with { AffectedBatchIds = List(value.ProtectionBlockedReplacement.Original.RevocationReceipt.AffectedBatchIds) } },
                Outcome = value.ProtectionBlockedReplacement.Outcome with { TargetReceipts = List(value.ProtectionBlockedReplacement.Outcome.TargetReceipts) } },
            ProtectionActivationRequest = value.ProtectionActivationRequest is null ? null : value.ProtectionActivationRequest with { Replacement = value.ProtectionActivationRequest.Replacement with { Targets = List(value.ProtectionActivationRequest.Replacement.Targets) } },
            ProtectionActivationOutcome = value.ProtectionActivationOutcome is null ? null : value.ProtectionActivationOutcome with { TargetReceipts = List(value.ProtectionActivationOutcome.TargetReceipts) }, RevocationReceipt = value.RevocationReceipt is null ? null : value.RevocationReceipt with { AffectedBatchIds = List(value.RevocationReceipt.AffectedBatchIds) } }));
    private static IReadOnlyList<T> List<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values); var owned = new List<T>();
        foreach (var value in values) { if (owned.Count >= 1000) { throw new ArgumentException("Oversized exact evidence vector."); } owned.Add(value); }
        return owned.AsReadOnly();
    }
    private static T ValidateStrings<T>(T value)
    {
        long strings = 0;
        void Visit(object? current)
        {
            if (current is null) { return; }
            if (current is string text)
            { if (text.Length > 16384 || (strings += text.Length) > 8 * 1024 * 1024) { throw new ArgumentException("Oversized exact evidence."); }
                _ = new System.Text.UTF8Encoding(false, true).GetByteCount(text); return; }
            if (current is System.Collections.IEnumerable sequence)
            { foreach (var item in sequence) { Visit(item); } return; }
            if (current.GetType().Namespace == typeof(GovernanceGuardTransition).Namespace || current is Hexalith.EventStore.Contracts.Identity.AggregateIdentity)
            { foreach (var property in current.GetType().GetProperties()) { Visit(property.GetValue(current)); } }
        }
        Visit(value); return value;
    }
    private static string Hash<T>(T value) => HashBytes(JsonSerializer.SerializeToUtf8Bytes(value));
    private static string HashBytes(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
}
