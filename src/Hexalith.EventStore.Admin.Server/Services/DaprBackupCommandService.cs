using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IBackupCommandService"/>.
/// All write methods delegate to EventStore via DAPR service invocation — never writes directly.
/// </summary>
public sealed class DaprBackupCommandService : IBackupCommandService {
    private const string AdmissionKeyPrefix = "admin:restore-admissions:";
    private const string AuditKeyPrefix = "admin:crypto-shredding-audit:";
    private const string ErrorNoOperation = "error-no-operation";
    private const string WorkflowIdKeyPrefix = "admin:crypto-shredding-workflows:id:";
    private const string WorkflowScopeKeyPrefix = "admin:crypto-shredding-workflows:scope:";

    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprBackupCommandService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprBackupCommandService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="httpClientFactory">The HTTP client factory for DAPR service invocation.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprBackupCommandService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprBackupCommandService> logger) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authContext);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _authContext = authContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> TriggerBackupAsync(
        string tenantId,
        string? description,
        bool includeSnapshots,
        CancellationToken ct = default)
        => await Task.FromResult(CreateDeferredResult(
            "deferred-backup-trigger",
            "Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.")).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> ValidateBackupAsync(
        string backupId,
        CancellationToken ct = default)
        => await Task.FromResult(CreateDeferredResult(
            "deferred-backup-validate",
            "Backup validation is deferred. EventStore does not yet have an approved backup manifest and validation model.")).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<AdminOperationResult> TriggerRestoreAsync(
        string backupId,
        DateTimeOffset? pointInTime,
        bool dryRun,
        CancellationToken ct = default)
        => await Task.FromResult(CreateDeferredResult(
            "deferred-backup-restore",
            "Restore is deferred. EventStore needs an approved safe restore namespace, idempotency rule, tenant isolation rule, and audit model before this operation can run.")).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<StreamExportResult> ExportStreamAsync(
        StreamExportRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);
        return await Task.FromResult(new StreamExportResult(
            false,
            request.TenantId,
            request.Domain,
            request.AggregateId,
            0,
            null,
            null,
            "Stream export is deferred. EventStore needs an approved bounded export contract, format, and event limit before this operation can run.")).ConfigureAwait(false);

    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> ImportStreamAsync(
        string tenantId,
        string content,
        CancellationToken ct = default)
        => await Task.FromResult(CreateDeferredResult(
            "deferred-backup-import-stream",
            "Stream import is deferred. EventStore needs approved payload validation, idempotency, target namespace, and audit rules before this operation can run.")).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<RestoredBackupAdmissionResult> AdmitRestoredBackupAsync(
        RestoredBackupAdmissionRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.TryValidate(out string? rejectionReason)) {
            throw new ArgumentException(rejectionReason, nameof(request));
        }

        string key = AdmissionKey(request.TenantId, request.AdmissionId);
        RestoredBackupAdmissionResult? existing = await _daprClient
            .GetStateAsync<RestoredBackupAdmissionResult>(_options.StateStoreName, key, cancellationToken: ct)
            .ConfigureAwait(false);
        if (existing is not null) {
            return existing with { IdempotentReplay = true };
        }

        // Story 22.7c: physical backup transport, manifest parsing, and crypto-shredding watermark
        // lookup are deferred until the backup engine lands. Until then, every admission request
        // returns DeferredValidation so callers know the safety check cannot be proven yet — they
        // must NOT serve protected content based on this admission.
        RestoredBackupAdmissionResult result = BuildAdmissionResult(
            request,
            RestoredBackupAdmissionState.DeferredValidation,
            watermarkConflict: "backup-engine-deferred",
            operatorActorId: request.OperatorActorId,
            auditId: null);
        await _daprClient
            .SaveStateAsync(_options.StateStoreName, key, result, cancellationToken: ct)
            .ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<RestoredBackupAdmissionResult> SubmitRestoreAdmissionDecisionAsync(
        string tenantId,
        string admissionId,
        RestoredBackupAdmissionState decision,
        string operatorActorId,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(admissionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorActorId);
        if (!Enum.IsDefined(decision)) {
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown RestoredBackupAdmissionState value.");
        }

        string key = AdmissionKey(tenantId, admissionId);
        RestoredBackupAdmissionResult? existing = await _daprClient
            .GetStateAsync<RestoredBackupAdmissionResult>(_options.StateStoreName, key, cancellationToken: ct)
            .ConfigureAwait(false);
        if (existing is null) {
            throw new KeyNotFoundException("Restored-backup admission record was not found.");
        }

        if (existing.State == decision) {
            return existing with { IdempotentReplay = true };
        }

        if (!RestoredBackupAdmissionTransitions.IsAllowed(existing.State, decision)) {
            throw new InvalidOperationException("The requested restored-backup admission transition is not allowed.");
        }

        string auditId = BuildAuditId("admission", admissionId, decision.ToString());
        RestoredBackupAdmissionResult updated = existing with {
            State = decision,
            ReasonCode = RestoredBackupAdmissionResult.ReasonCodeFor(decision),
            NextAction = RestoredBackupAdmissionResult.NextActionFor(decision),
            AuditId = auditId,
            DecisionActorId = operatorActorId,
            DecidedAtUtc = DateTimeOffset.UtcNow,
            IdempotentReplay = false,
        };

        CryptoShreddingAuditEvent audit = new(
            AuditId: auditId,
            WorkflowId: null,
            AdmissionId: admissionId,
            TenantId: existing.TenantId,
            Domain: existing.Domain,
            AggregateId: existing.AggregateId,
            FromSequence: existing.FromSequence,
            ToSequence: existing.ToSequence,
            ProtectionMetadataVersion: existing.ProtectionMetadataVersion,
            KeyReferencePolicy: existing.KeyReferencePolicy,
            KeyAliasFingerprint: existing.KeyAliasFingerprint,
            WorkflowFromState: null,
            WorkflowToState: null,
            AdmissionFromState: existing.State,
            AdmissionToState: decision,
            DecisionActorId: operatorActorId,
            CorrelationId: existing.CorrelationId,
            DecidedAtUtc: updated.DecidedAtUtc,
            ReasonCode: updated.ReasonCode);
        if (!audit.TryValidate(out string? auditRejectionReason)) {
            throw new InvalidOperationException(auditRejectionReason);
        }

        await _daprClient
            .SaveStateAsync(_options.StateStoreName, AuditKey(auditId), audit, cancellationToken: ct)
            .ConfigureAwait(false);
        await _daprClient
            .SaveStateAsync(_options.StateStoreName, key, updated, cancellationToken: ct)
            .ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public async Task<CryptoShreddingWorkflowDecision> SubmitCryptoShreddingWorkflowAsync(
        CryptoShreddingWorkflowRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.TryValidate(out string? rejectionReason)) {
            throw new ArgumentException(rejectionReason, nameof(request));
        }

        string scopeStateKey = WorkflowScopeKey(request.Identity.ComputeScopeKey());
        CryptoShreddingWorkflowDecision? existing = await _daprClient
            .GetStateAsync<CryptoShreddingWorkflowDecision>(_options.StateStoreName, scopeStateKey, cancellationToken: ct)
            .ConfigureAwait(false);
        if (existing is not null) {
            return existing with { IdempotentReplay = true };
        }

        string auditId = BuildAuditId("workflow", request.Identity.WorkflowId, CryptoShreddingWorkflowState.Requested.ToString());
        CryptoShreddingWorkflowDecision decision = new(
            Identity: request.Identity,
            State: CryptoShreddingWorkflowState.Requested,
            ReasonCode: CryptoShreddingWorkflowDecision.ReasonCodeFor(CryptoShreddingWorkflowState.Requested),
            NextAction: CryptoShreddingWorkflowDecision.NextActionFor(CryptoShreddingWorkflowState.Requested),
            CorrelationId: request.CorrelationId,
            AuditId: auditId,
            DecisionActorId: request.OperatorActorId,
            DecidedAtUtc: DateTimeOffset.UtcNow,
            IrreversibleDecisionRecorded: false,
            IdempotentReplay: false);

        CryptoShreddingAuditEvent audit = new(
            AuditId: auditId,
            WorkflowId: request.Identity.WorkflowId,
            AdmissionId: null,
            TenantId: request.Identity.TenantId,
            Domain: request.Identity.Domain,
            AggregateId: request.Identity.AggregateId,
            FromSequence: request.Identity.FromSequence,
            ToSequence: request.Identity.ToSequence,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: request.Identity.KeyReferencePolicy,
            KeyAliasFingerprint: request.Identity.KeyAliasFingerprint,
            WorkflowFromState: null,
            WorkflowToState: decision.State,
            AdmissionFromState: null,
            AdmissionToState: null,
            DecisionActorId: request.OperatorActorId,
            CorrelationId: request.CorrelationId,
            DecidedAtUtc: decision.DecidedAtUtc,
            ReasonCode: decision.ReasonCode);
        if (!audit.TryValidate(out string? auditRejectionReason)) {
            throw new InvalidOperationException(auditRejectionReason);
        }

        await _daprClient
            .SaveStateAsync(_options.StateStoreName, AuditKey(auditId), audit, cancellationToken: ct)
            .ConfigureAwait(false);
        await _daprClient
            .SaveStateAsync(_options.StateStoreName, WorkflowIdKey(request.Identity.TenantId, request.Identity.WorkflowId), decision, cancellationToken: ct)
            .ConfigureAwait(false);
        await _daprClient
            .SaveStateAsync(_options.StateStoreName, scopeStateKey, decision, cancellationToken: ct)
            .ConfigureAwait(false);
        return decision;
    }

    private static RestoredBackupAdmissionResult BuildAdmissionResult(
        RestoredBackupAdmissionRequest request,
        RestoredBackupAdmissionState state,
        string watermarkConflict,
        string operatorActorId,
        string? auditId) {
        return new RestoredBackupAdmissionResult(
            AdmissionId: request.AdmissionId,
            State: state,
            TenantId: request.TenantId,
            Domain: request.Domain,
            AggregateId: request.AggregateId,
            FromSequence: request.FromSequence,
            ToSequence: request.ToSequence,
            BackupManifestId: request.BackupManifestId,
            ProtectionMetadataVersion: request.ProtectionMetadataVersion,
            KeyReferencePolicy: request.KeyReferencePolicy,
            KeyAliasFingerprint: request.KeyAliasFingerprint,
            WatermarkConflict: watermarkConflict,
            ReasonCode: RestoredBackupAdmissionResult.ReasonCodeFor(state),
            NextAction: RestoredBackupAdmissionResult.NextActionFor(state),
            CorrelationId: request.CorrelationId,
            AuditId: auditId,
            DecisionActorId: operatorActorId,
            DecidedAtUtc: DateTimeOffset.UtcNow,
            IdempotentReplay: false);
    }

    internal static string AdmissionKey(string tenantId, string admissionId)
        => $"{AdmissionKeyPrefix}{tenantId}:{admissionId}";

    internal static string WorkflowIdKey(string tenantId, string workflowId)
        => $"{WorkflowIdKeyPrefix}{tenantId}:{workflowId}";

    private static string AuditKey(string auditId)
        => $"{AuditKeyPrefix}{auditId}";

    private static string WorkflowScopeKey(string scopeKey)
        => $"{WorkflowScopeKeyPrefix}{scopeKey}";

    private static string BuildAuditId(string category, string id, string state) {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{category}|{id}|{state}|{DateTimeOffset.UtcNow:O}"));
        return Convert.ToHexString(hash, 0, 13).ToLowerInvariant();
    }

    private static AdminOperationResult CreateDeferredResult(string operationId, string message)
        => new(false, operationId, message, "Deferred");

    private async Task<AdminOperationResult> InvokeEventStorePostAsync<TRequest>(
        string endpoint,
        TRequest request,
        CancellationToken ct) {
        try {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

            using HttpRequestMessage httpRequest = _daprClient.CreateInvokeMethodRequest(
                _options.EventStoreAppId,
                endpoint,
                request)
                ?? CreateFallbackRequest(HttpMethod.Post, endpoint, request);
            httpRequest.Method = HttpMethod.Post;

            string? token = _authContext.GetToken();
            if (token is not null) {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpClient httpClient2 = _httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse2 = await httpClient2.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);
            _ = httpResponse2.EnsureSuccessStatusCode();
            AdminOperationResult? result = await httpResponse2.Content.ReadFromJsonAsync<AdminOperationResult>(cts.Token).ConfigureAwait(false);

            return result ?? new AdminOperationResult(false, ErrorNoOperation, "Null response from EventStore", "NULL_RESPONSE");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("EventStore endpoint '{Endpoint}' timed out.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, "Service invocation timed out", "TIMEOUT");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to invoke EventStore endpoint '{Endpoint}'.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, ex.Message, GetErrorCode(ex));
        }
    }

    private static string GetErrorCode(Exception exception) {
        Exception current = exception;
        while (true) {
            if (current is HttpRequestException httpRequestException && httpRequestException.StatusCode is HttpStatusCode statusCode) {
                return ((int)statusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            object? reflectedStatus = current.GetType().GetProperty("StatusCode")?.GetValue(current);
            if (reflectedStatus is not null) {
                return reflectedStatus.ToString() ?? current.GetType().Name;
            }

            if (current.InnerException is null) {
                return current.GetType().Name;
            }

            current = current.InnerException;
        }
    }

    private static HttpRequestMessage CreateFallbackRequest<TRequest>(HttpMethod method, string endpoint, TRequest request)
        => new(method, endpoint) {
            Content = JsonContent.Create(request),
        };
}
