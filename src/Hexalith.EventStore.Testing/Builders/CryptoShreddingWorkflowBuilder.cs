using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Testing.Builders;

/// <summary>
/// Story 22.7c — fluent builder for <see cref="CryptoShreddingWorkflowIdentity"/>,
/// <see cref="CryptoShreddingWorkflowRequest"/>, and <see cref="CryptoShreddingWorkflowDecision"/>
/// test fixtures. Defaults emit a valid <see cref="CryptoShreddingWorkflowScope.Aggregate"/>
/// identity scoped to a deterministic test tenant/domain/aggregate so callers customize only the
/// fields under test.
/// </summary>
public sealed class CryptoShreddingWorkflowBuilder {
    private string _workflowId = "01HKAAAAAAAAAAAAAAAAAAAAAA";
    private string _tenantId = "tenant-1";
    private string _domain = "orders";
    private CryptoShreddingWorkflowScope _scope = CryptoShreddingWorkflowScope.Aggregate;
    private string? _aggregateId = "agg-1";
    private long? _fromSequence;
    private long? _toSequence;
    private KeyReferencePolicy _keyReferencePolicy = KeyReferencePolicy.NoKeyReference;
    private string? _keyAliasFingerprint;
    private CryptoShreddingWorkflowState _state = CryptoShreddingWorkflowState.Requested;
    private CryptoShreddingWorkflowState _requestedAction = CryptoShreddingWorkflowState.Deleted;
    private string _operatorActorId = "operator-1";
    private string? _correlationId = "01HKBBBBBBBBBBBBBBBBBBBBBB";
    private string? _auditId = "01HKCCCCCCCCCCCCCCCCCCCCCC";
    private DateTimeOffset _submittedAtUtc = new(2026, 5, 18, 0, 0, 0, TimeSpan.Zero);
    private DateTimeOffset _decidedAtUtc = new(2026, 5, 18, 0, 1, 0, TimeSpan.Zero);
    private bool _idempotentReplay;

    /// <summary>Sets the workflow identifier.</summary>
    /// <param name="workflowId">The workflow ULID.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithWorkflowId(string workflowId) {
        _workflowId = workflowId;
        return this;
    }

    /// <summary>Sets the tenant identifier.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithTenant(string tenantId) {
        _tenantId = tenantId;
        return this;
    }

    /// <summary>Sets the domain.</summary>
    /// <param name="domain">The domain.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithDomain(string domain) {
        _domain = domain;
        return this;
    }

    /// <summary>Sets the workflow scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithScope(CryptoShreddingWorkflowScope scope) {
        _scope = scope;
        return this;
    }

    /// <summary>Sets the aggregate identifier.</summary>
    /// <param name="aggregateId">The aggregate.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithAggregate(string? aggregateId) {
        _aggregateId = aggregateId;
        return this;
    }

    /// <summary>Sets the sequence range bounds.</summary>
    /// <param name="fromSequence">Inclusive lower bound.</param>
    /// <param name="toSequence">Inclusive upper bound.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithRange(long fromSequence, long toSequence) {
        _fromSequence = fromSequence;
        _toSequence = toSequence;
        _scope = CryptoShreddingWorkflowScope.Range;
        return this;
    }

    /// <summary>Sets the key reference policy and optional alias fingerprint.</summary>
    /// <param name="policy">The policy.</param>
    /// <param name="fingerprint">Optional fingerprint (must satisfy the 16-hex shape when policy != NoKeyReference).</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithKeyReference(KeyReferencePolicy policy, string? fingerprint = null) {
        _keyReferencePolicy = policy;
        _keyAliasFingerprint = fingerprint;
        return this;
    }

    /// <summary>Sets the workflow state for built decisions.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithState(CryptoShreddingWorkflowState state) {
        _state = state;
        return this;
    }

    /// <summary>Sets the requested terminal action for built requests.</summary>
    /// <param name="requestedAction">The requested action (Invalidated or Deleted).</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithRequestedAction(CryptoShreddingWorkflowState requestedAction) {
        _requestedAction = requestedAction;
        return this;
    }

    /// <summary>Sets the operator actor identifier.</summary>
    /// <param name="operatorActorId">The operator id.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithOperator(string operatorActorId) {
        _operatorActorId = operatorActorId;
        return this;
    }

    /// <summary>Sets the correlation identifier.</summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithCorrelationId(string? correlationId) {
        _correlationId = correlationId;
        return this;
    }

    /// <summary>Sets the audit identifier.</summary>
    /// <param name="auditId">The audit id.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithAuditId(string? auditId) {
        _auditId = auditId;
        return this;
    }

    /// <summary>Sets timestamps.</summary>
    /// <param name="submittedAtUtc">When the workflow was submitted.</param>
    /// <param name="decidedAtUtc">When the most recent decision was recorded.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithTimestamps(DateTimeOffset submittedAtUtc, DateTimeOffset decidedAtUtc) {
        _submittedAtUtc = submittedAtUtc;
        _decidedAtUtc = decidedAtUtc;
        return this;
    }

    /// <summary>Marks the decision as an idempotent replay.</summary>
    /// <param name="idempotentReplay">Whether to flag the decision as a replay.</param>
    /// <returns>The same builder for chaining.</returns>
    public CryptoShreddingWorkflowBuilder WithIdempotentReplay(bool idempotentReplay = true) {
        _idempotentReplay = idempotentReplay;
        return this;
    }

    /// <summary>Builds the identity record.</summary>
    /// <returns>An identity record.</returns>
    public CryptoShreddingWorkflowIdentity BuildIdentity()
        => new(
            _workflowId,
            _tenantId,
            _domain,
            _scope,
            _aggregateId,
            _fromSequence,
            _toSequence,
            _keyReferencePolicy,
            _keyAliasFingerprint);

    /// <summary>Builds the workflow request record.</summary>
    /// <returns>A request record.</returns>
    public CryptoShreddingWorkflowRequest BuildRequest()
        => new(
            BuildIdentity(),
            _requestedAction,
            _operatorActorId,
            _correlationId,
            _submittedAtUtc);

    /// <summary>Builds the workflow decision record.</summary>
    /// <returns>A decision record.</returns>
    public CryptoShreddingWorkflowDecision BuildDecision()
        => new(
            BuildIdentity(),
            _state,
            CryptoShreddingWorkflowDecision.ReasonCodeFor(_state),
            CryptoShreddingWorkflowDecision.NextActionFor(_state),
            _correlationId,
            _auditId,
            _operatorActorId,
            _decidedAtUtc,
            CryptoShreddingWorkflowTransitions.IsIrreversibleDecision(_state),
            _idempotentReplay);
}
