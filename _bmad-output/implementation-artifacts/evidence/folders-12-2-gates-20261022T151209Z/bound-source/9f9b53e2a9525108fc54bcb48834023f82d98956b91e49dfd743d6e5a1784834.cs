using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Testing.Builders;

/// <summary>
/// Story 22.7c — fluent builder for <see cref="RestoredBackupAdmissionRequest"/> and
/// <see cref="RestoredBackupAdmissionResult"/> test fixtures.
/// </summary>
public sealed class RestoredBackupAdmissionBuilder {
    private string _admissionId = "01HKADADADADADADADADADADAD";
    private string _tenantId = "tenant-1";
    private string _domain = "orders";
    private string? _aggregateId = "agg-1";
    private long? _fromSequence;
    private long? _toSequence;
    private string _manifestId = "manifest-1";
    private readonly DateTimeOffset _backupCreatedAtUtc = new(2026, 5, 17, 0, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _restoreRequestedAtUtc = new(2026, 5, 18, 0, 0, 0, TimeSpan.Zero);
    private int _protectionMetadataVersion = 1;
    private KeyReferencePolicy _keyReferencePolicy = KeyReferencePolicy.NoKeyReference;
    private string? _keyAliasFingerprint;
    private DateTimeOffset? _deletionWatermarkUtc;
    private string? _correlationId = "01HKBBBBBBBBBBBBBBBBBBBBBB";
    private string _operatorActorId = "operator-1";
    private RestoredBackupAdmissionState _state = RestoredBackupAdmissionState.DeferredValidation;
    private string? _watermarkConflict = "backup-engine-deferred";
    private string? _auditId;
    private readonly DateTimeOffset _decidedAtUtc = new(2026, 5, 18, 0, 1, 0, TimeSpan.Zero);
    private bool _idempotentReplay;

    /// <summary>Sets the admission identifier.</summary>
    /// <param name="admissionId">The id.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithAdmissionId(string admissionId) {
        _admissionId = admissionId;
        return this;
    }

    /// <summary>Sets the tenant.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithTenant(string tenantId) {
        _tenantId = tenantId;
        return this;
    }

    /// <summary>Sets the domain.</summary>
    /// <param name="domain">The domain.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithDomain(string domain) {
        _domain = domain;
        return this;
    }

    /// <summary>Sets the aggregate identifier.</summary>
    /// <param name="aggregateId">The aggregate.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithAggregate(string? aggregateId) {
        _aggregateId = aggregateId;
        return this;
    }

    /// <summary>Sets the affected sequence range.</summary>
    /// <param name="fromSequence">Inclusive lower bound.</param>
    /// <param name="toSequence">Inclusive upper bound.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithRange(long fromSequence, long toSequence) {
        _fromSequence = fromSequence;
        _toSequence = toSequence;
        return this;
    }

    /// <summary>Sets the backup manifest identifier.</summary>
    /// <param name="manifestId">The manifest id.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithManifest(string manifestId) {
        _manifestId = manifestId;
        return this;
    }

    /// <summary>Sets the protection metadata version observed in the manifest.</summary>
    /// <param name="metadataVersion">The version.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithMetadataVersion(int metadataVersion) {
        _protectionMetadataVersion = metadataVersion;
        return this;
    }

    /// <summary>Sets the key reference policy and optional alias fingerprint.</summary>
    /// <param name="policy">The policy.</param>
    /// <param name="fingerprint">Optional fingerprint.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithKeyReference(KeyReferencePolicy policy, string? fingerprint = null) {
        _keyReferencePolicy = policy;
        _keyAliasFingerprint = fingerprint;
        return this;
    }

    /// <summary>Sets the crypto-shredding decision watermark to compare against.</summary>
    /// <param name="watermarkUtc">The watermark.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithDeletionWatermark(DateTimeOffset? watermarkUtc) {
        _deletionWatermarkUtc = watermarkUtc;
        return this;
    }

    /// <summary>Sets the operator actor identifier.</summary>
    /// <param name="operatorActorId">The operator id.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithOperator(string operatorActorId) {
        _operatorActorId = operatorActorId;
        return this;
    }

    /// <summary>Sets the correlation identifier.</summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithCorrelationId(string? correlationId) {
        _correlationId = correlationId;
        return this;
    }

    /// <summary>Sets the admission state for built results.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithState(RestoredBackupAdmissionState state) {
        _state = state;
        return this;
    }

    /// <summary>Sets the watermark conflict description.</summary>
    /// <param name="conflict">The kebab-case description.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithWatermarkConflict(string? conflict) {
        _watermarkConflict = conflict;
        return this;
    }

    /// <summary>Sets the audit identifier referenced by the built result.</summary>
    /// <param name="auditId">The audit id.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithAuditId(string? auditId) {
        _auditId = auditId;
        return this;
    }

    /// <summary>Marks the decision as an idempotent replay.</summary>
    /// <param name="idempotentReplay">Whether to flag the decision as a replay.</param>
    /// <returns>The same builder for chaining.</returns>
    public RestoredBackupAdmissionBuilder WithIdempotentReplay(bool idempotentReplay = true) {
        _idempotentReplay = idempotentReplay;
        return this;
    }

    /// <summary>Builds the admission request record.</summary>
    /// <returns>An admission request.</returns>
    public RestoredBackupAdmissionRequest BuildRequest()
        => new(
            _admissionId,
            _tenantId,
            _domain,
            _aggregateId,
            _fromSequence,
            _toSequence,
            _manifestId,
            _backupCreatedAtUtc,
            _restoreRequestedAtUtc,
            _protectionMetadataVersion,
            _keyReferencePolicy,
            _keyAliasFingerprint,
            _deletionWatermarkUtc,
            _correlationId,
            _operatorActorId);

    /// <summary>Builds the admission result record.</summary>
    /// <returns>An admission result.</returns>
    public RestoredBackupAdmissionResult BuildResult()
        => new(
            _admissionId,
            _state,
            _tenantId,
            _domain,
            _aggregateId,
            _fromSequence,
            _toSequence,
            _manifestId,
            _protectionMetadataVersion,
            _keyReferencePolicy,
            _keyAliasFingerprint,
            _watermarkConflict,
            RestoredBackupAdmissionResult.ReasonCodeFor(_state),
            RestoredBackupAdmissionResult.NextActionFor(_state),
            _correlationId,
            _auditId,
            _operatorActorId,
            _decidedAtUtc,
            _idempotentReplay);
}
