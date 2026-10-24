// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment sections 1-4.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Enumerates only exact catalog keys and builds mutations to enlist in the authority transaction.</summary>
internal sealed class PayloadProtectionCatalogs(IPayloadProtectionStateStore store, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Conditionally initializes explicit empty heads. Existing heads must be enumerated, never reset.</summary>
    internal async ValueTask<PayloadStateCommitResult> InitializeAsync(string root, CancellationToken cancellationToken = default)
    {
        _ = PayloadProtectionStateKeys.Sha(root);
        string now = PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
        var mutations = new List<PayloadProtectionStateMutation>();
        if (await PayloadOperationBudget.ReadAsync<MemberHeadV1>(store, PayloadProtectionStateKeys.MemberHead(root), cancellationToken).ConfigureAwait(false) is null)
        {
            if (await PayloadOperationBudget.ReadAsync<MemberPageV1>(store, PayloadProtectionStateKeys.MemberPage(root, 0), cancellationToken).ConfigureAwait(false) is not null) { throw Mismatch(); }
            mutations.Add(PayloadProtectionStateMutation.Put(new MemberHeadV1(root, 0, 0, 0, now)));
        }
        if (await PayloadOperationBudget.ReadAsync<WorkHeadV1>(store, PayloadProtectionStateKeys.WorkHead(root), cancellationToken).ConfigureAwait(false) is null)
        {
            if (await PayloadOperationBudget.ReadAsync<WorkPageV1>(store, PayloadProtectionStateKeys.WorkPage(root, 0), cancellationToken).ConfigureAwait(false) is not null) { throw Mismatch(); }
            mutations.Add(PayloadProtectionStateMutation.Put(new WorkHeadV1(root, 0, 0, 0, now)));
        }
        return mutations.Count == 0 ? PayloadStateCommitResult.Committed
            : await store.CommitAsync(mutations, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads head, all pages and head again within a bounded monotonic operation budget.</summary>
    internal async ValueTask<MemberCatalogSnapshot> ReadMembersAsync(string root, CancellationToken cancellationToken = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        long started = _clock.GetTimestamp();
        for (int attempt = 0; attempt < 3 && _clock.GetElapsedTime(started) < TimeSpan.FromSeconds(10); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PayloadProtectionStateValue<MemberHeadV1> head = await RequiredAsync<MemberHeadV1>(PayloadProtectionStateKeys.MemberHead(root), cancellationToken).ConfigureAwait(false);
            var pages = new List<PayloadProtectionStateValue<MemberPageV1>>();
            var instances = new HashSet<string>(StringComparer.Ordinal);
            uint count = 0;
            for (uint page = 0; page < head.Value.PageCount; page++)
            {
                PayloadProtectionStateValue<MemberPageV1> value = await RequiredAsync<MemberPageV1>(PayloadProtectionStateKeys.MemberPage(root, page), cancellationToken).ConfigureAwait(false);
                if (value.Value.TenantDomainDigest != root || value.Value.Page != page) { throw Mismatch(); }
                foreach (MemberEntryV1 entry in value.Value.Entries)
                {
                    count = checked(count + 1);
                    if (entry.Ordinal != count || !instances.Add(entry.InstanceId)) { throw Mismatch(); }
                }
                pages.Add(value);
            }
            PayloadProtectionStateValue<MemberHeadV1> confirmed = await RequiredAsync<MemberHeadV1>(PayloadProtectionStateKeys.MemberHead(root), cancellationToken).ConfigureAwait(false);
            if (head.ETag != confirmed.ETag || head.Value.Revision != confirmed.Value.Revision) { continue; }
            if (head.Value.TenantDomainDigest != root || count != head.Value.EntryCount) { throw Mismatch(); }
            cancellationToken.ThrowIfCancellationRequested();
            if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { break; }
            return new(confirmed, pages.AsReadOnly());
        }
        throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable);
    }

    /// <summary>Reads a complete work catalog; no scan or missing-head-as-empty shortcut exists.</summary>
    internal async ValueTask<WorkCatalogSnapshot> ReadWorkAsync(string root, CancellationToken cancellationToken = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        long started = _clock.GetTimestamp();
        for (int attempt = 0; attempt < 3 && _clock.GetElapsedTime(started) < TimeSpan.FromSeconds(10); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PayloadProtectionStateValue<WorkHeadV1> head = await RequiredAsync<WorkHeadV1>(PayloadProtectionStateKeys.WorkHead(root), cancellationToken).ConfigureAwait(false);
            var pages = new List<PayloadProtectionStateValue<WorkPageV1>>();
            var operations = new HashSet<string>(StringComparer.Ordinal);
            uint count = 0;
            for (uint page = 0; page < head.Value.PageCount; page++)
            {
                PayloadProtectionStateValue<WorkPageV1> value = await RequiredAsync<WorkPageV1>(PayloadProtectionStateKeys.WorkPage(root, page), cancellationToken).ConfigureAwait(false);
                if (value.Value.TenantDomainDigest != root || value.Value.Page != page) { throw Mismatch(); }
                foreach (WorkEntryV1 entry in value.Value.Entries)
                {
                    count = checked(count + 1);
                    if (!operations.Add(entry.OperationId)) { throw Mismatch(); }
                }
                pages.Add(value);
            }
            PayloadProtectionStateValue<WorkHeadV1> confirmed = await RequiredAsync<WorkHeadV1>(PayloadProtectionStateKeys.WorkHead(root), cancellationToken).ConfigureAwait(false);
            if (head.ETag != confirmed.ETag || head.Value.Revision != confirmed.Value.Revision) { continue; }
            if (head.Value.TenantDomainDigest != root || count != head.Value.EntryCount) { throw Mismatch(); }
            cancellationToken.ThrowIfCancellationRequested();
            if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { break; }
            return new(confirmed, pages.AsReadOnly());
        }
        throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable);
    }

    /// <summary>Appends one permanent member ordinal, preserving all existing tombstones and comparison metadata.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> AppendMember(MemberCatalogSnapshot current, ulong generation, string instance)
    {
        MemberHeadV1 head = current.Head.Value;
        if (head.EntryCount >= 65536 || current.Pages.SelectMany(p => p.Value.Entries).Any(e => e.InstanceId == instance)) { throw Mismatch(); }
        uint ordinal = checked(head.EntryCount + 1);
        uint page = (ordinal - 1) / 256;
        PayloadProtectionStateValue<MemberPageV1>? previous = page < current.Pages.Count ? current.Pages[(int)page] : null;
        MemberEntryV1[] entries = [.. previous?.Value.Entries ?? [], new(ordinal, generation, instance, "Serving")];
        return [PayloadProtectionStateMutation.Put(head with { Revision = checked(head.Revision + 1), EntryCount = ordinal,
                PageCount = Math.Max(head.PageCount, checked(page + 1)), UpdatedAtUtc = Now() }, current.Head.ETag),
            PayloadProtectionStateMutation.Put(new MemberPageV1(head.TenantDomainDigest, page, entries), previous?.ETag)];
    }

    /// <summary>Advances one member through Serving, Draining, Cleared without changing its ordinal or identity.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> TransitionMember(MemberCatalogSnapshot current, string instance, string next)
    {
        PayloadProtectionStateValue<MemberPageV1> page = current.Pages.SingleOrDefault(p => p.Value.Entries.Any(e => e.InstanceId == instance)) ?? throw Mismatch();
        MemberEntryV1 member = page.Value.Entries.Single(e => e.InstanceId == instance);
        if (!(member.State == "Serving" && next == "Draining") && !(member.State == "Draining" && next == "Cleared")) { throw Mismatch(); }
        return [PayloadProtectionStateMutation.Put(current.Head.Value with { Revision = checked(current.Head.Value.Revision + 1), UpdatedAtUtc = Now() }, current.Head.ETag),
            PayloadProtectionStateMutation.Put(page.Value with { Entries = page.Value.Entries.Select(e => e.InstanceId == instance ? e with { State = next } : e).ToArray() }, page.ETag)];
    }

    /// <summary>Inserts work in the first page with capacity, for the same transaction as its operation and reconciliation.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> AddWork(WorkCatalogSnapshot current, WorkEntryV1 entry)
    {
        WorkHeadV1 head = current.Head.Value;
        WorkEntryV1? existing = current.Pages.SelectMany(p => p.Value.Entries).SingleOrDefault(e => e.OperationId == entry.OperationId);
        if (existing is not null)
        {
            if (existing != entry) { throw Mismatch(); }
            return [PayloadProtectionStateMutation.Compare(current.Head)];
        }
        if (head.EntryCount >= 65536) { throw Mismatch(); }
        PayloadProtectionStateValue<WorkPageV1>? page = current.Pages.FirstOrDefault(p => p.Value.Entries.Length < 256);
        uint number = page?.Value.Page ?? head.PageCount;
        if (number >= 256) { throw Mismatch(); }
        WorkEntryV1[] entries = [.. page?.Value.Entries ?? [], entry];
        Array.Sort(entries, (a, b) => StringComparer.Ordinal.Compare(a.OperationId, b.OperationId));
        return [PayloadProtectionStateMutation.Put(head with { Revision = checked(head.Revision + 1), EntryCount = checked(head.EntryCount + 1),
                PageCount = Math.Max(head.PageCount, checked(number + 1)), UpdatedAtUtc = Now() }, current.Head.ETag),
            PayloadProtectionStateMutation.Put(new WorkPageV1(head.TenantDomainDigest, number, entries), page?.ETag)];
    }

    /// <summary>Removes work only alongside a terminal reconciliation CAS; empty pages remain permanently numbered.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> CompleteWork(WorkCatalogSnapshot current,
        PayloadProtectionStateValue<ReconciliationV1> claim, ReconciliationV1 terminal)
    {
        if (terminal.Status is not ("Completed" or "Quarantined") || terminal.OperationId != claim.Value.OperationId
            || terminal.ScopeDigest != claim.Value.ScopeDigest || terminal.Kind != claim.Value.Kind
            || terminal.KeyRef != claim.Value.KeyRef || terminal.DekVersion != claim.Value.DekVersion || terminal.Attempt != claim.Value.Attempt) { throw Mismatch(); }
        PayloadProtectionStateValue<WorkPageV1> page = current.Pages.SingleOrDefault(p => p.Value.Entries.Any(e => e.OperationId == terminal.OperationId)) ?? throw Mismatch();
        if (page.Value.Entries.Single(e => e.OperationId == terminal.OperationId).ScopeDigest != terminal.ScopeDigest) { throw Mismatch(); }
        return [PayloadProtectionStateMutation.Put(current.Head.Value with { Revision = checked(current.Head.Value.Revision + 1),
                EntryCount = checked(current.Head.Value.EntryCount - 1), UpdatedAtUtc = Now() }, current.Head.ETag),
            PayloadProtectionStateMutation.Put(page.Value with { Entries = page.Value.Entries.Where(e => e.OperationId != terminal.OperationId).ToArray() }, page.ETag),
            PayloadProtectionStateMutation.Put(terminal, claim.ETag)];
    }

    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken cancellationToken) where T : class
        => await PayloadOperationBudget.ReadAsync<T>(store, key, cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
    private string Now() => PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
