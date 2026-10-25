using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Independent in-memory component owner with explicit genesis enrollment, permissions and immutable admitted originals. Never installed authority.</summary>
internal sealed class EnrolledDeletionOwnerFixture : IDeletionConsumptionOwnerStateAuthority
{
    private readonly Dictionary<string, string> _admissions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _recorded = new(StringComparer.Ordinal);

    /// <summary>Creates a separately retained exact tenant genesis; actor absence does not call this constructor.</summary>
    internal EnrolledDeletionOwnerFixture(string tenant = "tenant-a", string? scope = null)
    {
        TenantId = tenant;
        ScopeId = scope ?? DeletionConsumptionActor.GetOwnerScope(tenant);
        Digest = DeletionConsumptionIdentity.Digest(new DeletionConsumptionLedger(tenant, 0, 0, [], [], []));
    }

    /// <inheritdoc />
    public string TenantId { get; }

    /// <inheritdoc />
    public string ScopeId { get; }

    /// <summary>Gets or sets independent installation availability.</summary>
    internal bool Enrolled { get; set; } = true;

    /// <summary>Gets or sets whether original record delivery is available.</summary>
    internal bool RecordAvailable { get; set; } = true;

    /// <summary>Gets or sets simulated loss after advancing the independently surviving anchor.</summary>
    internal bool LoseRecordAcknowledgement { get; set; }

    /// <summary>Gets the current independently retained owner revision.</summary>
    internal long Revision { get; private set; }

    /// <summary>Gets the current independently retained owner digest.</summary>
    internal string Digest { get; private set; }

    /// <summary>Gets explicit exact private operation permissions.</summary>
    internal HashSet<(string Identity, string Operation)> Permissions { get; } = [];

    /// <summary>Gets exact input objects observed at each transition boundary.</summary>
    internal List<(string Phase, AnchoredStateTransition Transition)> Calls { get; } = [];

    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(long ownerRevision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enrolled && ownerRevision == Revision && exactStateDigest == Digest);
    }

    /// <inheritdoc />
    public Task<bool> AuthorizeOperationAsync(string identity, string operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enrolled && Permissions.Contains((identity, operation)));
    }

    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(("admit", transition));
        string exact = JsonSerializer.Serialize(transition);
        if (!Valid(transition)) { return Task.FromResult(false); }
        if (_admissions.TryGetValue(transition.TargetDigest, out string? original)) { return Task.FromResult(original == exact); }
        if (transition.ExpectedRevision != Revision || transition.PredecessorDigest != Digest) { return Task.FromResult(false); }
        _admissions.Add(transition.TargetDigest, exact);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(("record", transition));
        bool recorded = RecordAvailable && Advance(transition);
        return Task.FromResult(recorded && !LoseRecordAcknowledgement);
    }

    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(("recover", transition));
        return Task.FromResult(Advance(transition));
    }

    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(("verify", transition));
        return Task.FromResult(Valid(transition) && _recorded.TryGetValue(transition.TargetDigest, out string? original)
            && original == JsonSerializer.Serialize(transition));
    }

    private bool Valid(AnchoredStateTransition transition) => Enrolled && transition.ScopeId == ScopeId
        && transition.ExpectedRevision >= 0 && transition.ExpectedRevision < long.MaxValue
        && transition.TargetRevision == transition.ExpectedRevision + 1 && transition.TargetBytes is { Length: > 0 }
        && transition.TargetDigest == Convert.ToHexString(SHA256.HashData(transition.TargetBytes));

    private bool Advance(AnchoredStateTransition transition)
    {
        if (!Valid(transition)) { return false; }
        string exact = JsonSerializer.Serialize(transition);
        if (!_admissions.TryGetValue(transition.TargetDigest, out string? admitted) || admitted != exact) { return false; }
        if (_recorded.TryGetValue(transition.TargetDigest, out string? original)) { return original == exact; }
        if (transition.ExpectedRevision != Revision || transition.PredecessorDigest != Digest) { return false; }
        Revision = transition.TargetRevision;
        Digest = transition.TargetDigest;
        _recorded.Add(transition.TargetDigest, exact);
        return true;
    }
}
