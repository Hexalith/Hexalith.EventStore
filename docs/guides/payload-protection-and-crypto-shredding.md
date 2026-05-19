# Payload and Snapshot Protection Hooks

EventStore exposes optional infrastructure hooks for protecting event payloads and snapshot state
before they are persisted, and for unprotecting them before they are published or replayed. This
guide covers the **provider-neutral hook contract** introduced in Story 22.7a:

- The shape of the protection metadata that EventStore stamps onto persisted events and snapshots.
- The default no-op behavior and how to register a custom protection provider.
- The fail-closed rules EventStore applies when it encounters legacy, malformed, or unknown
  protection metadata.

> **Out of scope for this guide.** Real encryption providers, key lifecycle (deletion, rotation,
> invalidation), missing-key behavior, crypto-shredding workflows, restored-backup safety, and
> full operational redaction across the Admin UI, CLI, MCP, ProblemDetails, and backup tooling are
> tracked in Stories 22.7b, 22.7c, and 22.7d. This page documents only the hook and metadata
> contract that those follow-up stories build on.

## Default behavior

Without an explicit registration, EventStore uses `NoOpEventPayloadProtectionService`. Every
event payload and snapshot is persisted and published exactly as-is, except that EventStore now
stamps explicit protection metadata onto the persisted envelope so downstream consumers can
distinguish "this row is explicitly unprotected" from a legacy row that predates Story 22.7a.

The no-op metadata record is:

```text
State            = Unprotected
MetadataVersion  = 1
Scheme           = null
KeyAlias         = null
ContentHint      = null
CompatibilityFlags = null
```

## Metadata shape

`EventStorePayloadProtectionMetadata` lives in `Hexalith.EventStore.Contracts.Security` and is a
small immutable record:

| Field | Description |
| --- | --- |
| `State` | `Unprotected`, `Protected`, or `ProviderOpaque`. |
| `MetadataVersion` | Schema version (currently `1`). Unknown future versions fail-closed to `ProviderOpaque`. |
| `Scheme` | Optional provider-neutral scheme identifier (e.g. `"aes-gcm-256"`). Required when `State == Protected`. |
| `KeyAlias` | Optional non-secret key reference. Treated sensitive-by-default by callers. |
| `ContentHint` | Optional content-type hint (e.g. `"application/json"`). |
| `CompatibilityFlags` | Optional small bounded `IReadOnlyDictionary<string, string>` for forward-compatible flags. |

The metadata never carries raw key material, plaintext, IVs/nonces, authentication tags,
provider-private blobs, state-store keys, or connection strings. The carrier validates against a
forbidden-substring/keyword list and length-bounds every field.

## Storage layout

- **Events.** EventStore stamps the JSON-serialized protection metadata into the persisted server
  `EventEnvelope.Extensions` dictionary under the reserved key `eventstore.protection`. Other
  extension entries are preserved unchanged.
- **Snapshots.** `SnapshotRecord` carries an optional `ProtectionMetadata` field of type
  `EventStorePayloadProtectionMetadata?`. Old snapshots that predate Story 22.7a deserialize with
  `null` here and are mapped to the **legacy** compatibility state on load.

The carrier exposes static helpers in `EventStorePayloadProtectionMetadataCarrier`:

- `Read(extensions)` returns the typed metadata for any envelope, falling back to the legacy
  compatibility record when no `eventstore.protection` entry exists.
- `Write(extensions, metadata)` returns a new dictionary with the metadata applied.
- `Read(serialized)` parses a serialized JSON string. Malformed JSON, unknown schema versions,
  forbidden secret-shaped fields, and missing required fields all map to
  `PayloadProtectionState.ProviderOpaque`, never to `Unprotected`.

## State machine

```text
                  ┌───────────────┐
                  │ Unprotected   │  ← no-op provider, legacy reads, post-unprotect
                  └──────▲────────┘
                         │
                         │ (Protect)
                         ▼
                  ┌───────────────┐
                  │   Protected   │  ← provider-emitted (Scheme required)
                  └──────▲────────┘
                         │
                         │ (malformed metadata, unknown version, forbidden shape)
                         ▼
                  ┌────────────────┐
                  │ ProviderOpaque │  ← carry-only; never decrypted or republished
                  └────────────────┘
```

### Fail-closed rules

- Missing metadata → **legacy** compatibility record (`State = Unprotected`, `CompatibilityFlags["legacy"] = "missing"`).
- Malformed JSON, unknown enum value, unknown schema version, missing required `State` or
  `Scheme` for `Protected` → `ProviderOpaque` with `CompatibilityFlags["reason"]`.
- Forbidden secret-shaped fields detected on read → `ProviderOpaque` with a `forbidden` reason.

EventStore never downgrades any of these states to `Unprotected` by inference.

## Hook boundaries

| Hook | Caller | When |
| --- | --- | --- |
| `ProtectEventPayloadAsync` | `EventPersister` | After payload bytes are computed, before `IActorStateManager.SetStateAsync`. |
| `UnprotectEventPayloadAsync` | `EventPublisher` | Once per stored envelope, before constructing the DAPR publish envelope. |
| `ProtectSnapshotAsync` | `SnapshotManager` | Before `IActorStateManager.SetStateAsync` writes the new snapshot. |
| `UnprotectSnapshotAsync` | `SnapshotManager` | After reading the snapshot from storage. |

All four hooks receive the caller's `CancellationToken`. `OperationCanceledException` propagates
without being mapped to an infrastructure failure.

`ProviderOpaque` records are **passed through verbatim**. EventStore never invokes
`UnprotectEventPayloadAsync` or `UnprotectSnapshotAsync` on opaque bytes, never publishes them in
plaintext, and never treats them as safely unprotected.

## Publish-time policy (22.7a + 22.7b)

EventStore continues to call `UnprotectEventPayloadAsync` before DAPR publication so existing
subscribers and domain services see plaintext payloads. The published envelope's
`eventstore.protection` extension carries the metadata returned by the provider, so subscribers
can distinguish protected-at-rest events without inspecting bytes.

**Story 22.7b — fail-closed publish-time policy.** EventStore now refuses to publish events whose
stored metadata is `ProviderOpaque` and refuses to publish events whose provider returns an
`Unreadable` outcome from the new typed entry point. In both cases:

- `EventPublishResult.Success` is `false` and `PublishedCount` reflects only the events that were
  successfully published before the unreadable event was encountered.
- `EventPublishResult.FailureReason` is the stable safe string
  `"Protected payload unavailable for publication. ReasonCode=<kebab-code>"` where `<kebab-code>`
  is one of the values in `UnreadableProtectedDataReasonCodes`. No provider exception text,
  payload bytes, or key alias is ever included.
- A `Stage=PublishUnreadable` log line is emitted carrying only safe envelope metadata
  (`CorrelationId`, `CausationId`, `TenantId`, `Domain`, `AggregateId`, `SequenceNumber`,
  `ReasonCode`).

## Unreadable protected data (22.7b)

When the registered provider cannot return interpretable bytes — missing key, invalidated/deleted
key, transient provider unavailability, malformed metadata, unknown metadata version,
provider-opaque records, bytes/metadata mismatch, or consistency mismatch — EventStore treats the
condition as a **first-class platform state**, not as a deserialization error or provider
exception leak. The classification is provider-neutral and lives in
`Hexalith.EventStore.Contracts.Security.UnreadableProtectedDataReason`.

### Typed unreadable outcomes

Providers signal unreadable conditions through two metadata-aware typed entry points added to
`IEventPayloadProtectionService` as default interface methods:

```csharp
Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(
    AggregateIdentity identity,
    string eventTypeName,
    byte[] payloadBytes,
    string serializationFormat,
    EventStorePayloadProtectionMetadata? metadata,
    CancellationToken cancellationToken = default);

Task<SnapshotUnprotectionOutcome> TryUnprotectSnapshotAsync(
    AggregateIdentity identity,
    object state,
    EventStorePayloadProtectionMetadata? metadata,
    CancellationToken cancellationToken = default);
```

Return `PayloadUnprotectionOutcome.Unreadable(reason)` (or `SnapshotUnprotectionOutcome.Unreadable`)
to report a classified unreadable condition. Throw `OperationCanceledException` for cancellation
only; any other exception thrown from a provider that has not overridden these typed methods is
mapped to `UnreadableProtectedDataReason.ProviderUnavailable` by the default delegating
implementation so EventStore never parses provider exception text.

### Reason taxonomy

| Reason | Stable code | Retryable | Permanent | Recommended HTTP status |
| --- | --- | --- | --- | --- |
| `MissingKey` | `missing-key` | maybe (operator) | no | 422 |
| `KeyInvalidatedOrDeleted` | `key-invalidated` | no | yes | 410 |
| `ProviderUnavailable` | `provider-unavailable` | yes | no | 503 |
| `ProviderDenied` | `provider-denied` | maybe | maybe | 422 |
| `ConsistencyMismatch` | `consistency-mismatch` | no | yes | 422 |
| `MalformedMetadata` | `malformed-metadata` | no | yes | 422 |
| `UnknownMetadataVersion` | `unknown-metadata-version` | maybe (upgrade) | maybe | 422 |
| `ProviderOpaqueUnsupportedOperation` | `provider-opaque-unsupported` | no | yes | 422 |
| `BytesMetadataMismatch` | `bytes-metadata-mismatch` | no | yes | 422 |

`skip-past-unreadable` is **not** enabled by default for any category. A future audited skip
policy requires explicit product/architecture approval and remains out of scope for 22.7b.

### Pre-domain readability boundary

`AggregateActor` now scans every rehydrated event before constructing
`DomainServiceCurrentState`: stored `ProviderOpaque` envelopes and `Unreadable` provider outcomes
throw `ProtectedDataUnreadableException` (a safe-message exception that carries the reason code
only — no payload bytes, no provider exception text). The exception is routed by the existing
infrastructure failure path to dead-letter, and domain services are never invoked with protected
or opaque bytes.

### Snapshot retention

Protected snapshots that cannot be unprotected are **retained** in storage. `LoadSnapshotAsync`
returns `null` so the caller falls back to event replay (where the pre-domain readability
boundary then handles any unreadable tail events). The existing `RemoveStateAsync` path is
preserved only for non-protected corrupt deserialization (schema drift on plaintext snapshots).

### ProblemDetails surface

The public stream read/replay endpoint (`POST /api/v1/streams/read`) returns the stable
`https://hexalith.io/problems/unreadable-protected-data` ProblemDetails when any event in the
page is `ProviderOpaque`. The response carries only safe envelope metadata (`tenantId`, `domain`,
`aggregateId`, `sequenceNumber`, `reasonCode`, `reasonCategory`, `stage`, `retryable`,
`permanent`). Payload bytes, key alias, provider exception text, and stack traces are
**never** included.

### No-leak proof

Testing surfaces include `ProtectedDataLeakSentinel` (constants for plaintext, snapshot
plaintext, key alias, provider-private blob, state-store key, connection string, provider
exception text) with an `AssertNoLeak(IEnumerable<string?>)` helper. The Server tests inject
these sentinels into protection-service fakes (`FakeUnreadableProtectionService`) and scan every
captured log line, failure reason, ProblemDetails JSON, and exception message.

## Crypto-shredding workflow and restored-backup safety (22.7c)

Story 22.7c lands the **provider-neutral key-lifecycle workflow** and **restored-backup admission**
contracts that build on top of the 22.7a/22.7b foundation. EventStore records auditable workflow
state, restore admission decisions, and a single canonical readability decision result that every
read, publish, replay, rebuild, snapshot-load, backup-admission, admin, CLI, and MCP surface
consumes.

> **Out of scope.** Real encryption providers, key vault / KMS integration, certificate
> management, DAPR secret-store wiring, legal hold UX, jurisdiction-specific compliance
> automation, full operational redaction (Story 22.7d), and a physical backup engine remain
> deferred. Admin backup trigger/validate/restore/export/import operations still return a
> deferred result; the new admission contracts return `DeferredValidation` until the engine
> lands.

### Canonical readability decision

Every fail-closed runtime path produces a `ProtectedDataReadabilityDecision` (in
`Hexalith.EventStore.Contracts.Security`). The decision wraps the 22.7b unreadable taxonomy with
22.7c orchestration outcomes:

| Status | Meaning |
| --- | --- |
| `Readable` | Plaintext available; safe to forward to domain/publish/read. |
| `Unreadable` | Provider reported one of the 22.7b unreadable reasons. |
| `MalformedMetadata` / `UnknownVersion` / `ProviderOpaque` / `ProviderUnavailable` | Convenience aliases mapping to specific 22.7b reasons. |
| `DeferredValidation` | Restore admission cannot prove safety yet (transient unreadable). |
| `RestoreConflict` | Restored backup conflicts with an irreversible workflow watermark. |
| `QuarantineRequired` | Affected data is quarantined awaiting operator inspection. |
| `OperatorDecisionRequired` | A pending workflow or restore conflict needs explicit operator action. |

`ProtectedDataReadabilityDecisionFactory.FromOutcome(...)` / `.FromMetadata(...)` are the only
entry points runtime callers use; they accept an optional `RestoredBackupAdmissionResult` and let
admission conflicts override the per-row readability conclusion.

### Crypto-shredding workflow state machine

```text
            Requested ──approve──▶ Approved ──dispatch──▶ PendingProvider
                │                                              │
                │ reject                                        │ provider terminal outcome
                ▼                                               ▼
              Rejected                          ┌─── Invalidated ──▶ Completed
                                                ├─── Deleted ──────▶ Completed
                                                ├─── VerificationFailed ──▶ OperatorDecisionRequired
                                                └─── (cancel)     ──▶ CancelledBeforeDecision

restored backup admission conflict
   ─▶ Invalidated/Deleted ─▶ RestoreConflict ─▶ OperatorDecisionRequired ─▶ {Quarantined, Completed}
```

- **Idempotency** is value-based on `CryptoShreddingWorkflowIdentity` (workflow ULID, tenant,
  domain, scope, aggregate, sequence range, key reference policy, key alias fingerprint). Repeated
  requests after a terminal state return the existing audit/status with `IdempotentReplay = true`.
- **Cancellation** is allowed only in non-terminal states (`Requested`, `Approved`,
  `PendingProvider`). After `Invalidated`/`Deleted`, cancellation cannot undo the decision; the
  caller receives the existing terminal status.
- **Key references** are always stored as 16-character SHA-256 hex fingerprints. Raw key aliases
  are NEVER persisted, even when the policy permits a reference.

### Restored-backup admission

`IBackupCommandService.AdmitRestoredBackupAsync` accepts a `RestoredBackupAdmissionRequest` (safe
metadata only — tenant, domain, aggregate, sequence range, manifest identity, protection metadata
version, key alias fingerprint, deletion watermark) and returns a `RestoredBackupAdmissionResult`
with one of:

| State | Behavior |
| --- | --- |
| `Accepted` | Protected data may be served. |
| `Blocked` | Restored backup conflicts with an irreversible workflow watermark; reading is blocked. |
| `Quarantined` | Affected data is quarantined until an operator closes the inspection. |
| `OperatorDecisionRequired` | Explicit operator decision is required before progress. |
| `DeferredValidation` | Admission cannot be proved with current evidence; provide more and retry. |

**Current behavior.** Until the backup engine lands, the implementation returns
`DeferredValidation` with the safe watermark conflict code `backup-engine-deferred`. Callers
must NOT serve protected content based on this admission. The admin API surfaces
`POST /api/v1/admin/backups/admissions`,
`POST /api/v1/admin/backups/admissions/{admissionId}/decision`, and
`GET /api/v1/admin/backups/admissions/{admissionId}` so the contract can be exercised end-to-end.

### Operator-facing ProblemDetails

| Type URI | When |
| --- | --- |
| `https://hexalith.io/problems/unreadable-protected-data` | Read/replay/admin path observed unreadable protected data (22.7b). |
| `https://hexalith.io/problems/crypto-shredding-workflow-conflict` | Workflow request conflicts with current state (409) or replays a pending decision (425). |
| `https://hexalith.io/problems/restored-backup-admission-conflict` | Admission state is Blocked/Quarantined/OperatorDecisionRequired (409) or DeferredValidation (503). |

All ProblemDetails responses carry only safe envelope metadata + stable kebab-case reason codes +
operator next-action hints. Payload bytes, snapshot state, raw keys, key alias text, provider
exception messages, state-store keys, connection strings, and stack traces never appear.

### Runtime diagnostic redaction

Story 22.7d-1 centralizes protected-data-capable runtime diagnostic text behind
`ProtectedDataDiagnosticRedactor` in `Hexalith.EventStore.Server`. Runtime callers must not pass
`exception.Message`, provider result text, metadata dictionaries, command payloads, event payloads,
snapshot state, state-store keys, or connection strings directly into log template parameters,
OpenTelemetry status descriptions/events, command status failure text, publish failure reasons, or
dead-letter diagnostic fields.

| Surface | Safe fields |
| --- | --- |
| Logs and source-generated log properties | `correlationId`, `causationId`, `tenantId`, `domain`, `aggregateId`, `commandType`, `eventTypeName`, `sequenceNumber`, `stage`, `reasonCode`, duration/count metadata. |
| OpenTelemetry status/events | `stage`, `reasonCode`, safe exception type, and `eventstore.protected_data_diagnostic_redacted=true`. |
| Command status / publish failure / dead-letter diagnostics | Deterministic safe fallback text: `Protected data diagnostic details were redacted. ReasonCode=<reason-code>; Stage=<stage>.` |
| Unreadable protected-data ProblemDetails | Stable type/title/status/detail plus `correlationId`, `tenantId`, `domain`, `aggregateId`, `sequenceNumber`, `stage`, `reasonCode`, `reasonCategory`, `metadataVersion`, `retryable`, `permanent`. |

The allow-list rule is constructive: safe output is projected from approved metadata, not produced by
serializing arbitrary objects and scrubbing afterward. Unknown provider exceptions use
`reasonCode=protected-data-diagnostic-redacted`; typed unreadable protected-data exceptions preserve
their stable unreadable reason code.

### Admin API and Web UI redaction

Story 22.7d-2 adds an Admin-facing redacted-content descriptor,
`AdminRedactedContent`, for protected payload/state/diagnostic presentation. Admin DTOs that can
represent protected content expose safe metadata and a typed descriptor (`payload`, `state`,
`eventPayload`, `resultingState`, `failure`, or `diagnostic`) instead of serializing raw-bearing
properties for protected responses.

| Admin surface | Safe projection |
| --- | --- |
| Stream event detail | Event metadata plus `payload` descriptor; protected responses omit `payloadJson`. |
| Aggregate state/snapshot | Tenant/domain/aggregate/sequence/timestamp plus `state` descriptor; protected responses omit `stateJson`. |
| Step debugger and diff/blame values | Event context plus field descriptors; protected responses omit `eventPayloadJson`, `stateJson`, `oldValue`, and `newValue` where redacted. |
| Sandbox result | Command type/outcome/timing plus event/state/error descriptors; protected responses omit command/event/resulting-state raw JSON and unsafe error text. |
| Dead letters and projection errors | Message IDs, tenant/domain/aggregate/correlation/type metadata plus `failure` or `diagnostic` descriptor; provider exception text remains out of Admin-facing JSON. |
| Upstream protected ProblemDetails | Admin.Server preserves known protected-data type URIs, status, safe detail, reason/stage/correlation/tenant/domain/aggregate metadata, retry/permanent flags, and guidance; provider-private extensions are dropped. |
| Web UI JSON rendering | `JsonViewer` renders `ProtectedContentPanel` for descriptors and does not echo invalid raw JSON fallback text. |

The descriptor carries only `contentKind`, deterministic placeholder text, `reasonCode`, `stage`,
`metadataVersion`, `retryable`, `permanent`, safe next action, and safe navigation metadata. It is a
projection boundary, not a wrapper around the original content.

### CLI and MCP redaction

Story 22.7d-3 extends the same `AdminRedactedContent` boundary to `eventstore-admin` and
`Hexalith.EventStore.Admin.Mcp`. CLI JSON, table, CSV, and `--output` file paths render protected
content through descriptor/status fields. MCP result, preview, and error JSON use the same safe
projection so automation clients receive machine-readable metadata without protected payload/state
content.

| CLI/MCP surface | Safe projection |
| --- | --- |
| CLI JSON output | Protected DTO responses keep descriptor fields such as `payload` and `state`; raw names such as `payloadJson` and `stateJson` are omitted when descriptors are present. |
| CLI table/CSV output | Raw-bearing columns are replaced by safe descriptor/status columns. Non-protected values may still render through the safe formatter when no descriptor is present. |
| CLI stderr and API errors | Protected-data ProblemDetails preserve allow-listed scalar metadata (`reasonCode`, `stage`, `metadataVersion`, retry/permanent flags, tenant/domain/aggregate/sequence/correlation identifiers) and drop raw bodies, nested provider metadata, stack traces, and provider exception text. |
| MCP tool results | Raw-capable JSON fields are projected to safe descriptor names such as `payload`, `state`, `oldContent`, `newContent`, `diagnostic`, or status descriptors. |
| MCP previews and errors | Approval previews and error JSON use safe operation/status metadata only; payload JSON, snapshot state, connection strings, provider-private metadata, and exception text are redacted before serialization. |

Replay/rebuild/backup-validation artifact scans remain Story 22.7d-4. CLI/MCP callers that consume
those future results must continue to use descriptor/status fields rather than reintroducing raw
artifact text.

### Audit evidence

`CryptoShreddingAuditEvent` records every workflow transition and admission decision. Each entry
carries: tenant, domain, aggregate or stream pattern, sequence range, protection metadata
version, key reference policy + alias fingerprint (policy-permitted), workflow command identity,
correlation, decision actor identity, timestamp, decision outcome enum, and reason code. Validation
rejects records lacking required fields, and forbidden secret-shaped fields are caught at
construction by `CryptoShreddingAuditEvent.TryValidate`.

### Out of scope

- Real encryption providers, key vault integration, cloud KMS integration, DAPR secret-store
  setup, certificate management, provider-specific cryptographic algorithms.
- Physical backup engine implementation — trigger/validate/restore/export/import operations
  remain deferred.
- Deleting immutable event records, snapshots, or audit records as the crypto-shredding
  mechanism. Crypto-shredding records and enforces consequences; it never deletes audit-relevant
  state.
- Legal hold, data-subject UX, approval workflow UI, jurisdiction-specific compliance automation.
- Replay/rebuild/backup-validation artifact scans remain delegated to Story 22.7d-4.
- Skip-past-shredded replay/rebuild — no skip policy is added.

### Recovery and evidence redaction matrix (22.7d-4)

Story 22.7d-4 extends the protected-data no-leak guarantee to replay/read responses, projection
rebuild artifacts, backup validation, restored-backup admission, deferred recovery paths, and
operational evidence files. The matrix below summarizes which surface owns each output and which
existing primitive enforces redaction. The same fail-closed and safe-fallback rules apply: no raw
payload bytes, snapshot plaintext, provider exception text, provider-private metadata, state-store
keys, connection strings, or key aliases reach operator-facing fields.

| Surface | Output | Redaction primitive |
| --- | --- | --- |
| `StreamsController.ReadStreamAsync` unreadable path | `UnreadableProtectedDataProblem` ProblemDetails | `ProtectedDataDiagnosticRedactor.BuildUnreadableProblemExtensions(decision)` |
| `StreamsController.ReadStreamAsync` readable path | `StreamReadEvent.Payload` (already-readable bytes returned to caller) | Append-only event-store contract; bytes are intentionally returned |
| `AggregateReplayer.Replay` contract | `AggregateReconstructionResult.StateJson`, `Timeline[].StateJson`, `Message`, `FailedEventType` | `StateJson` is intentionally returned only after upstream readability has passed; no-leak scans cover diagnostics, evidence captures, and failure text, not the raw readable replay contract |
| `ProjectionUpdateOrchestrator` unreadable event | Checkpoint `Failed` + reason code via `ResetAsync` | `UnreadableProtectedDataReasonCodes.From(decision)`; `LastAppliedSequence` never advances past the unreadable sequence |
| `ProjectionRebuildCheckpoint` / `ProjectionRebuildOperation` | Public status DTO | Safe-by-construction record fields (`OperationId`, `Status`, `FailureReasonCode`, `LastAppliedSequence`, `UpdatedAt`); no raw exception or payload field exists on the contract |
| `AdminProjectionRebuildController` failure mapping | RFC 7807 ProblemDetails | `MapSaveFailure(reasonCode)` returns stable status + reason code with no exception body |
| Projection apply HTTP response | Logged status / reason code only | `ReadProjectResponseAsync` reads only status code + content type + charset; never reads response body for operator feedback |
| `DaprBackupCommandService.Trigger/Validate/Restore/Export/Import` | `AdminOperationResult` / `StreamExportResult` | `CreateDeferredResult(operationId, deterministicMessage)` — honest, no-leak, deferred |
| `DaprBackupCommandService.InvokeEventStorePostAsync` exception path | `AdminOperationResult.Message` | `BuildSafeInvocationFailureMessage(endpoint)` — deterministic safe string. Exception type is still logged via `ILogger.LogWarning(ex, ...)` (its redaction is owned by Story 22.7d-1) |
| `RestoredBackupAdmissionResult` | Persisted + returned admission record | Safe-by-construction record fields (no raw alias, no provider blob); `KeyAliasFingerprint` is the only key-derived field, SHA-256 hex prefix only |
| `CryptoShreddingWorkflowDecision` | Persisted + returned workflow record | Safe-by-construction record fields; no raw alias, no key material |
| `CryptoShreddingAuditEvent` | Audit log | `TryValidate` enforces 14 structural invariants and rejects raw secret shapes at construction |
| Test/evidence artifacts (files, directories, captured stdout/stderr) | Captured strings, written files | `ProtectedDataLeakSentinel.AssertNoLeak`, `AssertNoLeakInFile`, `AssertNoLeakInDirectory` |

**Sentinel categories.** Seven sentinel values are defined in
`ProtectedDataLeakSentinel`: protected payload plaintext, protected snapshot plaintext, key alias,
provider-private blob, state-store key, connection string, and provider exception text. Tests inject
these values into protection-service fakes, protection metadata, and exception messages, then assert
no captured string from a public surface contains any sentinel. Failure messages report the sentinel
**index** (not value) and the offending file path.

**What is intentionally NOT in scope of this story.** No new encryption providers, no KMS / Key
Vault / DAPR secret-store integration, no physical backup engine, no skip-past-unreadable replay
policy, no DW4 validator schema changes, and no mutation of immutable persisted event payloads.

## Deferred to Story 22.7d

- ~~Replay/rebuild/backup-validation artifact scans — Story 22.7d-4.~~ **Completed by Story 22.7d-4.**
- Real encryption provider, key vault integration, cloud KMS, DAPR secret-store integration.

## Registering a custom provider

```csharp
public sealed class MyProtectionService : IEventPayloadProtectionService {
    public Task<PayloadProtectionResult> ProtectEventPayloadAsync(
        AggregateIdentity identity,
        IEventPayload eventPayload,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default) {
        // Encrypt payloadBytes (provider-private) and return the cipher with a Protected metadata
        // record describing the scheme and a non-secret key alias.
        var protectedBytes = MyCrypto.Encrypt(payloadBytes, cancellationToken);
        var metadata = new EventStorePayloadProtectionMetadata(
            State: PayloadProtectionState.Protected,
            MetadataVersion: 1,
            Scheme: "myco-aead-v1",
            KeyAlias: $"tenant:{identity.TenantId}:event-payload",
            ContentHint: serializationFormat,
            CompatibilityFlags: null);
        return Task.FromResult(new PayloadProtectionResult(protectedBytes, serializationFormat, metadata));
    }

    public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default) {
        // Decrypt and return Unprotected metadata so the published envelope advertises plaintext.
        var plain = MyCrypto.Decrypt(payloadBytes, cancellationToken);
        return Task.FromResult(new PayloadProtectionResult(
            plain,
            serializationFormat,
            EventStorePayloadProtectionMetadata.Unprotected()));
    }

    // Snapshot hooks: implement ProtectSnapshotAsync and UnprotectSnapshotAsync (typed) or
    // continue using ProtectSnapshotStateAsync / UnprotectSnapshotStateAsync (default delegating
    // implementations exist on the interface for backward compatibility).
}
```

Register the service before the default registration:

```csharp
services.AddSingleton<IEventPayloadProtectionService, MyProtectionService>();
services.AddEventStoreServer(builder.Configuration);
```

## Doing nothing changes nothing

If you do not register a custom protection service, EventStore behaves exactly as it did before
Story 22.7a — the only observable change is the presence of an explicit
`eventstore.protection` entry on newly persisted event envelopes describing the no-op state.
Existing rows that predate Story 22.7a remain readable and project to the legacy compatibility
record on load.
