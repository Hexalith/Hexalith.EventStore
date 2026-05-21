using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.AspNetCore.Mvc;
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
    private const string StreamReadEndpoint = "api/v1/streams/read";
    private const int StreamExportPageSize = 1_000;
    private const string WorkflowIdKeyPrefix = "admin:crypto-shredding-workflows:id:";
    private const string WorkflowScopeKeyPrefix = "admin:crypto-shredding-workflows:scope:";
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

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
        if (_options.MaxStreamExportEvents <= 0) {
            throw new InvalidOperationException("AdminServer:MaxStreamExportEvents must be greater than zero.");
        }

        int exportLimit = _options.MaxStreamExportEvents;
        if (TryValidateExportRequest(request, out string? validationError) is false) {
            return CreateExportFailure(request, "RejectedValidation", validationError!, request.Format, exportLimit);
        }

        string? normalizedFormat = NormalizeExportFormat(request.Format);
        if (normalizedFormat is null) {
            return CreateExportFailure(
                request,
                "RejectedValidation",
                "Unsupported stream export format. Supported formats are JSON and CloudEvents.",
                request.Format,
                exportLimit);
        }

        try {
            StreamReadPage probe = await ReadStreamPageAsync(
                new StreamReadRequest(
                    request.TenantId,
                    request.Domain,
                    request.AggregateId,
                    FromSequence: 0,
                    PageSize: Math.Min(StreamExportPageSize, exportLimit)),
                ct).ConfigureAwait(false);

            if (probe.Metadata.LatestSequence <= 0 || (probe.Events.Count == 0 && probe.Metadata.LastSequenceReturned is null)) {
                return CreateExportFailure(request, "NoExportableEvents", "Stream export failed. ReasonCode=NoExportableEvents.", normalizedFormat, exportLimit);
            }

            long latestSequence = probe.Metadata.LatestSequence;
            bool truncated = latestSequence > exportLimit;
            long firstExportedSequence = truncated ? latestSequence - exportLimit + 1 : 1;
            long readCursor = firstExportedSequence - 1;
            long toSequence = latestSequence;
            var exportedEvents = new List<StreamReadEvent>(Math.Min(exportLimit, StreamExportPageSize));

            if (!truncated) {
                exportedEvents.AddRange(probe.Events.Where(e => e.SequenceNumber >= firstExportedSequence && e.SequenceNumber <= toSequence));
                readCursor = probe.Metadata.LastSequenceReturned ?? readCursor;
            }

            while (readCursor < toSequence && exportedEvents.Count < exportLimit) {
                int pageSize = Math.Min(StreamExportPageSize, exportLimit - exportedEvents.Count);
                StreamReadPage page = await ReadStreamPageAsync(
                    new StreamReadRequest(
                        request.TenantId,
                        request.Domain,
                        request.AggregateId,
                        FromSequence: readCursor,
                        ToSequence: toSequence,
                        PageSize: pageSize),
                    ct).ConfigureAwait(false);

                if (page.Events.Count == 0 || page.Metadata.LastSequenceReturned is null) {
                    break;
                }

                exportedEvents.AddRange(page.Events.Where(e => e.SequenceNumber >= firstExportedSequence && e.SequenceNumber <= toSequence));
                readCursor = page.Metadata.LastSequenceReturned.Value;
            }

            if (exportedEvents.Count == 0) {
                return CreateExportFailure(request, "NoExportableEvents", "Stream export failed. ReasonCode=NoExportableEvents.", normalizedFormat, exportLimit);
            }

            exportedEvents = [.. exportedEvents.OrderBy(e => e.SequenceNumber).Take(exportLimit)];
            if (exportedEvents.Any(static e => e.ProtectionMetadata?.State is PayloadProtectionState.Protected or PayloadProtectionState.ProviderOpaque)) {
                throw new StreamExportFailureException(StreamReplayReasonCodes.ProtectedPayloadUnavailable);
            }

            string content = BuildExportContent(
                request,
                normalizedFormat,
                exportedEvents,
                latestSequence,
                firstExportedSequence,
                toSequence,
                exportLimit,
                truncated);
            string fileName = BuildExportFileName(request, normalizedFormat, DateTimeOffset.UtcNow);

            LogStreamExportSucceeded(request, normalizedFormat, exportedEvents.Count, exportLimit, truncated);

            return new StreamExportResult(
                true,
                request.TenantId,
                request.Domain,
                request.AggregateId,
                exportedEvents.Count,
                content,
                fileName,
                null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            return CreateExportFailure(request, StreamReplayReasonCodes.ServiceUnavailable, $"Stream export failed. ReasonCode={StreamReplayReasonCodes.ServiceUnavailable}.", normalizedFormat, exportLimit);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (StreamExportFailureException ex) {
            return CreateExportFailure(request, ex.ErrorCode, $"Stream export failed. ReasonCode={ex.ErrorCode}.", normalizedFormat, exportLimit);
        }
        catch (HttpRequestException) {
            return CreateExportFailure(request, StreamReplayReasonCodes.ServiceUnavailable, $"Stream export failed. ReasonCode={StreamReplayReasonCodes.ServiceUnavailable}.", normalizedFormat, exportLimit);
        }
        catch (JsonException) {
            return CreateExportFailure(request, StreamReplayReasonCodes.CorruptEvent, $"Stream export failed. ReasonCode={StreamReplayReasonCodes.CorruptEvent}.", normalizedFormat, exportLimit);
        }

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
            .ConfigureAwait(false) ?? throw new KeyNotFoundException("Restored-backup admission record was not found.");
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
        string? auditId) => new(
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

    private StreamExportResult CreateExportFailure(
        StreamExportRequest request,
        string errorCode,
        string message,
        string? format,
        int exportLimit) {
        LogStreamExportFailed(request, format, exportLimit, errorCode);
        return new(false, request.TenantId, request.Domain, request.AggregateId, 0, null, null, message, errorCode);
    }

    private void LogStreamExportSucceeded(
        StreamExportRequest request,
        string format,
        int eventCount,
        int exportLimit,
        bool truncated)
        => _logger.LogInformation(
            "Stream export completed: SubjectId={SubjectId}, CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Format={Format}, EventCount={EventCount}, RequestedCap={RequestedCap}, ExportLimit={ExportLimit}, Truncated={Truncated}, Stage=StreamExport",
            GetAuditSubject(),
            GetAuditCorrelationId(),
            request.TenantId,
            request.Domain,
            request.AggregateId,
            format,
            eventCount,
            exportLimit,
            exportLimit,
            truncated);

    private void LogStreamExportFailed(
        StreamExportRequest request,
        string? format,
        int exportLimit,
        string reasonCode)
        => _logger.LogWarning(
            "Stream export failed: SubjectId={SubjectId}, CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Format={Format}, RequestedCap={RequestedCap}, ReasonCode={ReasonCode}, Stage=StreamExport",
            GetAuditSubject(),
            GetAuditCorrelationId(),
            request.TenantId,
            request.Domain,
            request.AggregateId,
            string.IsNullOrWhiteSpace(format) ? "(none)" : format.Trim(),
            exportLimit,
            reasonCode);

    private string GetAuditSubject()
        => _authContext.GetUserId() ?? "anonymous";

    private string GetAuditCorrelationId()
        => _authContext.GetCorrelationId() ?? "(none)";

    private static bool TryValidateExportRequest(StreamExportRequest request, out string? validationError) {
        if (string.IsNullOrWhiteSpace(request.TenantId)) {
            validationError = "TenantId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Domain)) {
            validationError = "Domain is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.AggregateId)) {
            validationError = "AggregateId is required.";
            return false;
        }

        validationError = null;
        return true;
    }

    private static string? NormalizeExportFormat(string? format) {
        if (string.IsNullOrWhiteSpace(format)) {
            return null;
        }

        string normalized = format.Trim();
        if (string.Equals(normalized, "JSON", StringComparison.OrdinalIgnoreCase)) {
            return "JSON";
        }

        if (string.Equals(normalized, "CloudEvents", StringComparison.OrdinalIgnoreCase)) {
            return "CloudEvents";
        }

        return null;
    }

    private static string BuildExportContent(
        StreamExportRequest request,
        string format,
        IReadOnlyList<StreamReadEvent> events,
        long latestSequence,
        long fromSequence,
        long toSequence,
        int exportLimit,
        bool truncated) {
        DateTimeOffset exportedAtUtc = DateTimeOffset.UtcNow;
        object document = string.Equals(format, "CloudEvents", StringComparison.Ordinal)
            ? new {
                tenantId = request.TenantId,
                domain = request.Domain,
                aggregateId = request.AggregateId,
                format,
                eventCount = events.Count,
                latestSequence,
                fromSequence,
                toSequence,
                exportLimit,
                truncated,
                exportedAtUtc,
                events = events.Select(e => ToCloudEvent(request, e)).ToArray(),
            }
            : new {
                tenantId = request.TenantId,
                domain = request.Domain,
                aggregateId = request.AggregateId,
                format,
                eventCount = events.Count,
                latestSequence,
                fromSequence,
                toSequence,
                exportLimit,
                truncated,
                exportedAtUtc,
                events = events.Select(ToJsonExportEvent).ToArray(),
            };

        return JsonSerializer.Serialize(document, ExportJsonOptions);
    }

    private static object ToJsonExportEvent(StreamReadEvent streamEvent)
        => new {
            sequenceNumber = streamEvent.SequenceNumber,
            eventTypeName = streamEvent.EventTypeName,
            messageId = streamEvent.MessageId,
            correlationId = streamEvent.CorrelationId,
            causationId = streamEvent.CausationId,
            timestamp = streamEvent.Timestamp,
            userId = streamEvent.UserId,
            metadataVersion = streamEvent.MetadataVersion,
            serializationFormat = streamEvent.SerializationFormat,
            protectionState = streamEvent.ProtectionMetadata?.State.ToString() ?? PayloadProtectionState.Unprotected.ToString(),
            payload = ReadPayload(streamEvent),
        };

    private static object ToCloudEvent(StreamExportRequest request, StreamReadEvent streamEvent)
        => new {
            specversion = "1.0",
            id = streamEvent.MessageId,
            source = $"/eventstore/tenants/{request.TenantId}/domains/{request.Domain}/aggregates/{request.AggregateId}",
            type = streamEvent.EventTypeName,
            subject = $"{request.TenantId}/{request.Domain}/{request.AggregateId}#{streamEvent.SequenceNumber}",
            time = streamEvent.Timestamp,
            datacontenttype = IsJsonPayload(streamEvent) ? "application/json" : "application/octet-stream",
            data = ReadPayload(streamEvent),
        };

    private static object ReadPayload(StreamReadEvent streamEvent) {
        if (IsJsonPayload(streamEvent)) {
            using JsonDocument document = JsonDocument.Parse(streamEvent.Payload);
            return document.RootElement.Clone();
        }

        return Convert.ToBase64String(streamEvent.Payload);
    }

    private static bool IsJsonPayload(StreamReadEvent streamEvent)
        => string.Equals(streamEvent.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(streamEvent.SerializationFormat, "application/json", StringComparison.OrdinalIgnoreCase);

    private static string BuildExportFileName(StreamExportRequest request, string format, DateTimeOffset exportedAtUtc) {
        string timestamp = exportedAtUtc.UtcDateTime.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
        return $"{SanitizeFileNamePart(request.TenantId)}_{SanitizeFileNamePart(request.Domain)}_{SanitizeFileNamePart(request.AggregateId)}_{format.ToLowerInvariant()}_{timestamp}.json";
    }

    private static string SanitizeFileNamePart(string value) {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value) {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        }

        return builder.Length == 0 ? "stream" : builder.ToString();
    }

    private async Task<StreamReadPage> ReadStreamPageAsync(StreamReadRequest request, CancellationToken ct) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds));

        using HttpRequestMessage httpRequest = _daprClient.CreateInvokeMethodRequest(
            HttpMethod.Post,
            _options.EventStoreAppId,
            StreamReadEndpoint)
            ?? CreateFallbackRequest(HttpMethod.Post, StreamReadEndpoint, request);
        httpRequest.Method = HttpMethod.Post;
        httpRequest.Content = JsonContent.Create(request, options: ExportJsonOptions);

        string? token = _authContext.GetToken();
        if (token is not null) {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        HttpClient httpClient = _httpClientFactory.CreateClient();
        using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cts.Token).ConfigureAwait(false);
        if (!httpResponse.IsSuccessStatusCode) {
            throw await CreateStreamExportFailureAsync(httpResponse, cts.Token).ConfigureAwait(false);
        }

        return await httpResponse.Content.ReadFromJsonAsync<StreamReadPage>(ExportJsonOptions, cts.Token).ConfigureAwait(false)
            ?? throw new StreamExportFailureException(StreamReplayReasonCodes.InternalError);
    }

    private static async Task<StreamExportFailureException> CreateStreamExportFailureAsync(
        HttpResponseMessage response,
        CancellationToken ct) {
        string? reasonCode = null;
        try {
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ExportJsonOptions, ct).ConfigureAwait(false);
            if (problem?.Extensions.TryGetValue("reasonCode", out object? reasonValue) == true) {
                reasonCode = reasonValue?.ToString();
            }
        }
        catch (JsonException) {
            // Fall back to HTTP status mapping below.
        }
        catch (NotSupportedException) {
            // Fall back to HTTP status mapping below.
        }

        return new StreamExportFailureException(reasonCode ?? response.StatusCode switch {
            HttpStatusCode.NotFound => StreamReplayReasonCodes.MissingStream,
            HttpStatusCode.Unauthorized => StreamReplayReasonCodes.UnauthorizedTenant,
            HttpStatusCode.Forbidden => StreamReplayReasonCodes.ForbiddenReplayScope,
            HttpStatusCode.BadRequest => StreamReplayReasonCodes.InvalidRange,
            HttpStatusCode.Conflict => StreamReplayReasonCodes.OperationInFlight,
            HttpStatusCode.RequestTimeout => StreamReplayReasonCodes.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable => StreamReplayReasonCodes.ServiceUnavailable,
            HttpStatusCode.InternalServerError => StreamReplayReasonCodes.InternalError,
            _ => StreamReplayReasonCodes.InternalError,
        });
    }

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
            // Story 22.7d-4: never echo provider exception text into AdminOperationResult.Message. The
            // exception type stays in the logged warning above, but the operator-facing message is a
            // stable safe string keyed off the endpoint. Error code keeps HTTP/SDK status when available.
            _logger.LogWarning(ex, "Failed to invoke EventStore endpoint '{Endpoint}'.", endpoint);
            return new AdminOperationResult(false, ErrorNoOperation, BuildSafeInvocationFailureMessage(endpoint), GetErrorCode(ex));
        }
    }

    internal static string BuildSafeInvocationFailureMessage(string endpoint)
        => $"EventStore service invocation failed. Endpoint={endpoint}; details redacted from operator-facing message.";

    internal static string GetErrorCode(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        Exception current = exception;
        while (true) {
            if (current is HttpRequestException httpRequestException && httpRequestException.StatusCode is HttpStatusCode statusCode) {
                return ((int)statusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            object? reflectedStatus = current.GetType().GetProperty("StatusCode")?.GetValue(current);
            if (reflectedStatus is HttpStatusCode reflectedHttpStatusCode) {
                return ((int)reflectedHttpStatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (reflectedStatus is int reflectedIntStatusCode and >= 100 and <= 599) {
                return reflectedIntStatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (reflectedStatus is Enum reflectedEnumStatusCode) {
                return $"{reflectedEnumStatusCode.GetType().Name}.{reflectedEnumStatusCode}";
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

    private sealed class StreamExportFailureException(string errorCode) : Exception(errorCode) {
        public string ErrorCode { get; } = errorCode;
    }
}
