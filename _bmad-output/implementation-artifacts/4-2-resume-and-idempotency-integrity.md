---
baseline_commit: 322e3193d22295153c74d16baee32a7e74f6d72a
---

# Story 4.2: Resume And Idempotency Integrity

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator,
I want command pipeline resume and idempotency checks to match the exact command being processed,
so that stale pipeline state cannot hijack a different command or prevent a valid retry.

## Acceptance Criteria

1. **Exact resume identity.** Given a pipeline record exists for an earlier command, when a different command reuses the same correlation id, then resume compares the normalized incoming `MessageId`, `CausationId`, and `CommandType` with the persisted values, and the stale record is drained or ignored without skipping execution of the new command. All three fields must match before any persisted stage is resumed; `CorrelationId` alone is never resume identity.
2. **Tenant-before-state ordering.** Given idempotency or pipeline state could exist for a command, when the incoming tenant does not match the aggregate actor tenant, then tenant validation completes before any idempotency, pipeline, pending-count, event, snapshot, metadata, status, archive, or drain state is read or written. The caller receives the existing support-safe tenant denial and cannot infer cross-tenant command existence.
3. **Retryable failures remain retryable.** Given a command previously failed before event commit because of transient infrastructure failure or an exhausted persistence conflict, when the same `MessageId` is retried with the same `CausationId` and `CommandType`, then the earlier transient outcome does not return as a permanent duplicate and the command may execute again. Accepted/no-op/domain-rejected outcomes remain terminal duplicates. Stored-but-unpublished outcomes are not re-executed; their recovery remains on the drain/publication-recovery path.
4. **Message-id status and archive identity.** Given command status and archive records are written or queried, when their primary state keys are derived, then the identity segment is `{tenant}:{messageId}` (retaining the existing `:status` / `:command` type suffixes), and correlation id is stored and indexed as tracing metadata rather than used as command identity.
5. **Collision and legacy safety.** Given a duplicate record has the same message-id key but its stored causation id or command type differs, or an older record lacks enough identity to prove an exact match, when idempotency is checked, then processing fails closed with a non-retryable support-safe `command_identity_conflict`; the platform neither returns another command's cached result nor re-executes ambiguously. No pipeline/idempotency/status/archive/drain evidence is deleted or overwritten by the conflict. Additive defaults preserve deserialization of older records.
6. **Correlation compatibility is unambiguous.** Given clients or operator surfaces still query by correlation id during migration, when the tenant-scoped correlation index resolves exactly one live message id, then the request may resolve to that message-keyed record; when it resolves multiple message ids, the API does not choose one implicitly and returns a support-safe ambiguity response directing the caller to use `MessageId`. No state-store scan or cross-tenant lookup is introduced.
7. **Persisted evidence and compatibility.** Given the focused unit, contract, generator, and higher-tier tests run, when exact-match, tenant-denial, transient retry, terminal duplicate, stale checkpoint, message-key, correlation-index, and legacy cases execute, then tests assert committed actor/state-store end state and caller-visible responses—not only mock calls or HTTP status—and existing duplicate-result fidelity, publication drain, projection-trigger, gateway `Location`, and ULID behavior remain intact.

## Tasks / Subtasks

- [x] **Task 1 — Introduce exact command identity in pipeline and idempotency state** (AC: 1, 3, 5)
  - [x] Extend `PipelineState` additively with `MessageId` and normalized `CausationId`; retain `CorrelationId`, `CommandType`, stage, timestamps, event counts, rejection metadata, and legacy `ResultPayload` compatibility. Append optional/defaulted fields so old JSON can deserialize.
  - [x] Extend `IdempotencyRecord` additively with `MessageId`, `CommandType`, and an application-visible expiration timestamp (or equivalently explicit bounded-retention field). Preserve `CausationId`, `ProcessedAt`, and all eight `CommandProcessingResult` fields from Story 4.1.
  - [x] Re-key new idempotency entries as `idempotency:{messageId}`. Pass the complete normalized identity into `CheckAsync` / `RecordAsync`; do not continue using a correlation-derived fallback as the key.
  - [x] Normalize absent direct-actor `CausationId` to `MessageId`, not `CorrelationId`. The normal gateway path already maps `CausationId = MessageId`; preserve explicit non-null causation ids supplied by legitimate callers.
  - [x] Return a cached terminal result only when `MessageId`, normalized `CausationId`, and `CommandType` all match with ordinal semantics. Add a dedicated support-safe identity-conflict path for mismatches and unverifiable legacy records; never log payloads or raw protected data.
  - [x] After a new message-key miss, perform at most one bounded legacy lookup at `idempotency:{normalizedCausationId}` when that key differs from the message key. If the legacy record contains the new fields and matches exactly, stage an atomic copy-to-message-key/remove-old-key migration and commit it before returning the cached result. If its identity cannot be proven, preserve it and return `command_identity_conflict`; never delete it and re-execute the command.
  - [x] Model checker outcomes explicitly (miss, exact terminal duplicate, expired, retryable/recoverable, identity conflict, legacy migration) so `AggregateActor` cannot conflate conflict with a cache miss.
  - [x] Keep the pipeline actor-state key correlation-addressable for collision discovery unless an equivalent bounded lookup is introduced. A new command reusing a correlation id must still find and classify the old checkpoint rather than strand it silently.

- [x] **Task 2 — Move tenant validation ahead of every actor-state operation** (AC: 2)
  - [x] In `AggregateActor.ProcessCommandAsync`, validate `command.TenantId` against `Host.Id` immediately after argument/cancellation/activity setup and before creating or invoking `IdempotencyChecker` / `ActorStateMachine` or reading pending counts.
  - [x] On tenant mismatch, preserve the current typed/support-safe denial and security telemetry, but perform no idempotency rejection write, pipeline cleanup, status write, or actor-state commit.
  - [x] Replace the misleading existing tenant test that permits an `IdempotencyRecord` read with assertions that the unauthorized path makes zero actor-state calls. Keep the existing prohibition on aggregate metadata/event reads.
  - [x] Preserve actor-id parsing through `TenantValidator`; do not query DAPR state or accept a wire assertion to decide tenant ownership.

- [x] **Task 3 — Make stale checkpoint handling stage-aware and loss-safe** (AC: 1, 3, 5)
  - [x] Add one exact identity comparison helper used by all resume branches. Do not scatter partially different comparisons through `AggregateActor`.
  - [x] If a mismatched stale checkpoint is `Processing` (no committed events), stage cleanup, commit it, and process the incoming command normally.
  - [x] If a mismatched checkpoint can represent committed events (`EventsStored` or a defensive legacy equivalent), do not publish or complete those events with the incoming command envelope. Convert/preserve the old event range on the existing drain/recovery seam under the old command's unique message identity, commit the handoff and checkpoint cleanup, then process the incoming command.
  - [x] A legacy committed checkpoint without `MessageId`/`CausationId` is not safe to drain under the incoming identity. Do not infer command `MessageId` from an event `MessageId`. Unless companion persisted data proves the complete old command identity unambiguously, preserve the checkpoint/events, return `command_identity_conflict`, and do not execute the incoming command. Add an explicit recovery diagnostic; never clean the checkpoint merely to unblock the new command.
  - [x] Ensure stale-state cleanup and drain handoff cannot be overwritten when two commands share a correlation id. If `UnpublishedEventsRecord` / reminder identity must move from correlation id to message id to achieve this, make that narrow additive identity change while retaining correlation as metadata. Story 4.4 still owns broader activation/sweep and unrecoverable-publication semantics.
  - [x] Preserve pending-command accounting: no double increment/decrement, no negative count, and no stale checkpoint causing a valid new command to bypass backpressure or domain invocation.
  - [x] Add committed-state tests for same identity resume, same-correlation/different-message, same-message/different-command-type, causation mismatch, legacy missing identity, and stale committed-event handoff.

- [x] **Task 4 — Bound idempotency retention and classify outcomes correctly** (AC: 3, 5)
  - [x] Add centrally registered idempotency retention options; default terminal retention is 86,400 seconds (24 hours) and validation rejects any value shorter than the configured status/archive TTL. Keep package versions centralized and do not add a new dependency. Use `TimeProvider` for deterministic expiration tests if time abstraction is needed (`Microsoft.Extensions.TimeProvider.Testing` is already centrally available).
  - [x] Treat accepted, accepted-no-op, and domain-rejected results as terminal deduplicated outcomes. Their records receive the configured bounded expiration.
  - [x] Do not persist pre-commit transient infrastructure failures or exhausted persistence-conflict results as terminal duplicate records. Preserve `StateManager.ClearCacheAsync()` before staging failure cleanup so partially staged events remain uncommitted.
  - [x] Keep publish failures / stored-but-unpublished results on a recoverable path that prevents domain re-execution; do not turn a post-commit publication failure into a clean idempotency miss. Preserve the existing drain record and stable persisted event `MessageId` behavior needed by Story 4.4.
  - [x] Make application-level `ExpiresAt` checking authoritative. Do not rely solely on DAPR actor-state TTL: the repository does not enable `ActorStateTTL`, and DAPR 1.18 documents that actor SDK caches can retain expired state until deactivation. Expired records are staged for removal and treated as misses only after exact identity and safety rules are applied. A legacy record without `ExpiresAt` is not treated as expired; exact new identity fields are required for safe migration, otherwise AC 5 applies.
  - [x] Update `IdempotencyCheckerTests`, `AggregateActorIdempotencyTests`, and failure-path tests to prove transient retry, terminal duplicate fidelity, expiration, mismatch conflict, and preservation of all result fields.
  - [x] Reconcile Story 4.1's newly strengthened rejected-duplicate actor test: keep its eight-field fidelity assertion, but do not use a wrong-tenant envelope to reach the cache after tenant validation moves first. Seed a terminal domain rejection under the correct tenant and test tenant mismatch separately as a zero-state-access denial.

- [x] **Task 5 — Re-key status/archive storage and add a tenant-scoped correlation index** (AC: 4, 6, 7)
  - [x] Change `ICommandStatusStore`, `ICommandArchiveStore`, DAPR implementations, constants, and in-memory fakes so primary operations name and use `messageId`; new keys are `{tenant}:{messageId}:status` and `{tenant}:{messageId}:command`.
  - [x] Add `MessageId` and `CorrelationId` to stored status/archive data additively where required so records are self-describing and can populate responses/indexes without treating the key argument as correlation.
  - [x] Add a bounded, tenant-scoped correlation index whose entries can represent one correlation id mapping to multiple message ids. Default capacity is 128 live entries per `(tenant, correlationId)` and each entry carries its own expiry aligned with the status/archive TTL. Do not use `DaprClient.QueryStateAsync`, direct Redis scans, or an unbounded in-memory list.
  - [x] Make `SubmitCommandHandler` the single index-write owner. It invokes the index once per submission after attempting the authoritative message-keyed status/archive writes; neither store independently updates the same index. Duplicate message ids are idempotent no-ops.
  - [x] Update the shared index with ETag optimistic concurrency and a bounded default of three retries. Before add or resolution, prune expired entries; resolution may also prune entries whose authoritative message-primary record no longer exists. If capacity remains full, set/preserve an overflow marker and fail correlation compatibility safely without evicting an arbitrary live message. Primary message-id lookup remains available.
  - [x] Define partial-failure semantics: the message-keyed record is authoritative; index maintenance is advisory/rebuildable and cannot make a valid message-id lookup fail. After retry exhaustion, log only support-safe identity metadata and continue the primary command path; make the in-memory fake expose equivalent observable behavior and deterministic conflict injection.
  - [x] New writes use message-id primary keys and correlation-index writes; do not continue dual-writing new correlation-primary status/archive records. A bounded legacy read fallback may read pre-existing correlation-primary records only after tenant authorization and only when no message-primary/index result exists.
  - [x] When a correlation lookup is ambiguous, return a deterministic support-safe conflict rather than selecting newest/first. Never disclose message ids belonging to an unauthorized tenant.
  - [x] Update `SubmitCommandHandler` to write/read status and archive by `request.MessageId` while preserving actual `request.CorrelationId` as metadata and activity-tracing input.
  - [x] Carry message identity through `ConcurrencyConflictException` and `ConcurrencyConflictExceptionHandler`; their advisory status write cannot remain correlation-primary after the store contract changes.

- [x] **Task 6 — Carry the message tracking key through gateway, replay, generated REST, and admin consumers** (AC: 4, 6, 7)
  - [x] Add an additive canonical message/status tracking field to `SubmitCommandResult` and public `SubmitCommandResponse`; keep the real correlation id available for tracing/backward-compatible clients. The single status key used by `Location` becomes `MessageId` (or an explicitly named `StatusKey` resolving to it), not a correlation value hidden under the old name.
  - [x] Map actor idempotency identity conflict to a dedicated `CommandIdentityConflictException`/problem type at the gateway: HTTP `409`, no `Retry-After`, no cached rejection/status mutation, and a support-safe detail telling the caller to submit the correct tuple or a new `MessageId`. Do not expose stored command identity values or translate it to a generic `500`.
  - [x] Update `CommandsController`, `CommandStatusController`, `ReplayController`, `CommandStatusResponse`, and `ReplayCommandResponse` so primary routes/lookups/locations use message id. Preserve a bounded tenant-authorized correlation compatibility path per AC 6.
  - [x] Update `RestApiControllerEmitter` and client status-location logic to use the canonical tracking field while preserving AD-17: absolute gateway-owned `Location` when configured, no `Location` when unconfigured, and no external-host status endpoint.
  - [x] Update `ArchivedCommandExtensions` so replay creates a new ULID `MessageId` and preserves a separately meaningful correlation chain. Remove the existing `Guid.NewGuid()` command-message generation on this touched path; EventStore message/correlation/causation identifiers are ULID-safe.
  - [x] Reconcile `AdminTraceQueryController`, activity/status consumers, and the duplicated `AdminStateStoreKeys` helper/test contract with message-primary identity. Keep admin correlation search as indexed search, not primary storage identity.
  - [x] Preserve `SubmitCommandHandler`'s newer projection behavior: trigger projection update only after an accepted result with events, keep it advisory, and do not change event publication's `triggerProjectionUpdate: false` ownership.

- [x] **Task 7 — Prove the complete behavior and regression boundaries** (AC: 1-7)
  - [x] Actor/state-machine tests: exact resume match, correlation collision, causation/type mismatch, legacy record, committed-event drain handoff, pending-count balance, and committed checkpoint cleanup.
  - [x] Security tests: unauthorized tenant causes no actor-state/idempotency/pipeline read or write; status/archive/index queries search only authorized tenants and produce indistinguishable not-found behavior across tenants.
  - [x] Retry/idempotency tests: transient infrastructure and exhausted pre-commit conflict retry successfully; terminal accepted/no-op/domain rejection returns the original eight-field result; post-commit publish failure does not re-run domain/event persistence.
  - [x] Store/fake tests: message-id key shape, TTL metadata/expiry, self-describing records, one-to-many correlation index, ambiguity, concurrent index update, legacy fallback, and DAPR/in-memory parity.
  - [x] Gateway/contract/generator tests: response carries distinct message and correlation ids, `Location` uses the message/status key, status/replay resolve by message id, correlation compatibility is explicit, and generated external APIs remain gateway delegators.
  - [x] Higher-tier evidence: inspect persisted actor state and DAPR state-store keys/records for tenant + message identity, stale checkpoint cleanup/drain handoff, retry outcome, index state, and absence of unauthorized reads. HTTP `202`/`200` and mock invocation counts alone are insufficient.
  - [x] Run the per-project validation commands in Testing Requirements. Record exact pass/fail/skip counts and any Microsoft.Testing.Platform/xUnit v3 fallback; never weaken a gate or use solution-level `dotnet test`.

## Dev Notes

### Implementation Boundary And Locked Semantics

- This is a coordinated end-to-end identity migration. Changing only `CommandStatusConstants` / `CommandArchiveConstants` would leave actor idempotency, pipeline resume, gateway responses, generated `Location`, replay, fakes, and operator consumers inconsistent.
- Exact command identity for this story is the tuple `(MessageId, normalized CausationId, CommandType)`. Correlation is tracing/grouping metadata and may legitimately be shared by multiple commands.
- The actor pipeline checkpoint remains discoverable by correlation so a collision can be classified, but checkpoint contents decide whether resume is legal. An equivalent bounded collision index is acceptable only if it preserves that behavior.
- A stale pre-commit checkpoint may be cleaned and ignored. A stale committed-event checkpoint must be handed to the original command's drain/recovery identity; never publish old events with the new command envelope.
- Pre-commit transient failures are retryable and must not become permanent duplicate results. Post-commit publication failures are recoverable and must not re-run the domain command. This distinction prevents both permanent blockage and duplicate events.
- New idempotency records have bounded retention. Application-level expiry is authoritative because actor-state TTL is not enabled in the current topology and DAPR documents an active-actor cache caveat.
- Primary status/archive identity is message id. Correlation compatibility is one-to-many and tenant-scoped; ambiguity is an explicit result, never a hidden “first/latest” choice.
- Legacy causation-keyed idempotency receives one direct fallback read, never a scan. Only an exact self-describing record migrates; unverifiable records and legacy committed checkpoints remain preserved and fail closed.
- `SubmitCommandHandler` owns one correlation-index update per submission. The index is 128-entry bounded, entry-expiring, ETag-updated with three retries, and advisory; message-primary lookup is always authoritative.
- `command_identity_conflict` is a non-retryable, no-mutation HTTP `409` contract. It never becomes a cached domain rejection and never exposes the stored tuple.
- Preserve Story 4.1's shipped `IdempotencyRecord` field fidelity and stable event `MessageId`. Story 4.1 reached `review` during this story-creation run and added complete accepted/rejected actor-boundary result assertions; retain that coverage while adapting its rejected fixture to the new tenant-before-idempotency ordering.
- No UI component or UX flow changes are required. If any UI/admin text is touched, it must call the identifier a message/status key accurately and remain support-safe; command acceptance is not projection-confirmed success.

### Current Baseline Read During Story Creation

Baseline inspected: `322e3193d22295153c74d16baee32a7e74f6d72a` on 2026-07-12. `main` matched `origin/main`. During creation, concurrent Story 4.1 work advanced to `review`, modified `AggregateActorIdempotencyTests.cs`, and recorded green focused/full Server, Testing, and Release build gates. The shared worktree also contains changes to `sprint-status.yaml`, `references/Hexalith.Memories`, and `references/Hexalith.Tenants`; preserve all of them.

| File / seam | Current state | Story action / preservation constraint |
| --- | --- | --- |
| `Actors/AggregateActor.cs` | Reads `idempotency:{causationId}` and correlation-keyed pipeline state before tenant validation; resumes any `EventsStored` record found under the correlation. Infrastructure/conflict paths cache rejection results; publication failures create correlation-keyed drains. | **UPDATE:** reorder validation, exact-match resume, stage-aware stale handling, outcome classification, message-key status calls. Preserve actor-owned atomic event mutation, `ClearCacheAsync`, drain recovery, pending counts, event identity, and projection sequencing. |
| `Actors/PipelineState.cs` | Stores correlation, stage, command type, start time, counts/rejection metadata; no message/causation identity. | **UPDATE:** append compatible identity fields; keep legacy deserialization safe. |
| `Actors/ActorStateMachine.cs`, `IActorStateMachine.cs` | Checkpoint/load/cleanup derive pipeline key from correlation. | **UPDATE:** centralize exact identity/state handling while retaining collision discovery; committed cleanup remains staged until actor save. |
| `Actors/IdempotencyChecker.cs`, `IIdempotencyChecker.cs` | Keys by causation id and returns any record without validating exact command identity or expiry. | **UPDATE:** message key, complete identity compare, expiry/removal, safe conflict. |
| `Actors/IdempotencyRecord.cs` | Preserves the complete eight-field `CommandProcessingResult` plus causation/processed time with additive defaults. | **UPDATE:** append message/type/expiry/classification metadata; do not regress existing mapping or JSON compatibility. |
| `Actors/UnpublishedEventsRecord.cs` and drain helpers | Drain state/reminders use correlation identity. | **UPDATE only as required:** prevent correlation reuse from overwriting the old command's drain; retain correlation as metadata and preserve Story 4.4's recovery seam. |
| `Commands/{I,Dapr}CommandStatusStore*`, `CommandStatusConstants.cs` | Primary key and API parameter are correlation id; DAPR writes already use 24-hour TTL metadata. | **UPDATE:** message primary key, self-describing record/index, tenant-safe compatibility. Preserve advisory error handling and cancellation propagation. |
| `Commands/{I,Dapr}CommandArchiveStore*`, `CommandArchiveConstants.cs` | Primary key and API parameter are correlation id; same TTL option/store as status. | **UPDATE:** message primary key and correlation index/metadata. Preserve payload confidentiality and advisory behavior. |
| `Pipeline/SubmitCommandHandler.cs` | Writes/reads status/archive by correlation; triggers projection updates only after accepted event-producing results. | **UPDATE:** message primary identity and distinct tracking result. Preserve activity correlation and the newer advisory projection-trigger behavior. |
| `Contracts/Commands/{ArchivedCommand,CommandStatusRecord,SubmitCommandResponse}.cs` | Records lack a canonical message/status tracking field; public response describes correlation as status identity. | **UPDATE additively:** carry distinct message and correlation identity without breaking old JSON construction unnecessarily. One C# type per file and full XML docs. |
| `Server/Commands/ArchivedCommandExtensions.cs` | Replay creates `MessageId` with `Guid.NewGuid()` and uses one input as replay correlation. | **UPDATE:** use `UniqueIdHelper` ULID and preserve separate message/correlation chain. |
| `EventStore/Controllers/{Commands,CommandStatus,Replay}Controller.cs` | Gateway `Location`, polling, and replay paths are correlation-primary. | **UPDATE:** message/status-key primary routes plus bounded authorized correlation compatibility; preserve authorization, support-safe problems, result-payload cap, and absolute Location. |
| `Server/Commands/ConcurrencyConflictException.cs`, `EventStore/ErrorHandling/ConcurrencyConflictExceptionHandler.cs` | Conflict status recovery has only correlation context and writes advisory status through the correlation-primary store. | **UPDATE:** carry/use message identity without weakening the existing 409, `Retry-After`, and support-safe problem-details behavior. |
| `Models/{SubmitCommandResponse,CommandStatusResponse,ReplayCommandResponse}.cs` | Compatibility response types expose correlation as tracking identity. | **UPDATE additively:** distinguish message/status key from correlation. |
| `RestApi.Generators/RestApiControllerEmitter.cs`, Client status-location seam | Generated `Location` reads `SubmitCommandResponse.CorrelationId`. | **UPDATE:** consume canonical message/status field; preserve configured-absolute / unconfigured-absent AD-17 behavior. |
| `Testing/Fakes/InMemoryCommand{Status,Archive}Store.cs` | Mirrors correlation keys and TTL but has no correlation-index semantics. | **UPDATE:** mirror message keys, index, expiry, ambiguity, and observable partial-failure behavior deterministically. |
| `Testing/Fakes/FakeActorStateMachine.cs` | Mirrors the correlation-only pipeline record/key API. | **UPDATE:** preserve fake/production parity for exact identity and stale-state behavior. |
| `Admin.Server/Helpers/AdminStateStoreKeys.cs`, `AdminTraceQueryController` | Helper/docs and trace status read assume correlation-primary server keys. | **UPDATE:** align primary message key and route correlation searches through the tenant-scoped index; do not introduce direct scans. |
| `Admin.Server/Services/KnownActorTypes.cs` | Tracks actor-state key families used by admin operations. | **UPDATE only if drain identity/key shape changes:** keep admin key recognition aligned without exposing cross-tenant data. |
| Focused tests under `Server.Tests/{Actors,Commands,Pipeline,Security}` | Cover current resume/status/archive behavior but tenant tests permit an idempotency read and transient failures are cached. Story 4.1 now asserts complete cached results; its rejected fixture currently uses a wrong-tenant envelope and relies on the old cache-before-tenant order. | **UPDATE:** preserve eight-field fidelity while reversing outdated ordering assertions and adding committed-state exact-match/retry/key/index evidence. |
| Contracts, Client, Generator, Testing, Admin, Integration tests | Encode correlation-primary response and key assumptions. | **UPDATE where behavior changes:** preserve old construction/JSON compatibility while proving new canonical identity. |

### Architecture Compliance

- **AD-3:** The gateway remains the command/status policy boundary. Generated REST and admin surfaces must not read actor/state-store data directly.
- **AD-5:** `AggregateActor` remains the only durable event mutation coordinator. Stale-resume handling cannot move event writes into controllers, stores, or domain code.
- **AD-6:** Stable persisted event `MessageId`, gapless aggregate sequence, non-zero global position, and complete duplicate replies remain unchanged.
- **AD-10:** Tenant authorization precedes disclosing status, idempotency, archive, index, or pipeline data. No cross-tenant existence oracle is acceptable.
- **AD-12 / NFR7 / NFR16:** Prove actor/state-store end state for stale cleanup, retry, key/index writes, and tenant denial; status codes and mock calls are not sufficient.
- **AD-17:** Generated command-status `Location` stays absolute and gateway-authoritative when configured and absent when unconfigured. Story 4.2 changes its tracking identity to message id transparently; it does not add an external-host status endpoint.
- **NFR12:** Prefer additive record/response evolution and bounded compatibility reads. Do not silently overload correlation fields with message-id values.

### Library / Framework Requirements And Latest Technical Notes

- Keep repository-pinned .NET SDK `10.0.302`, target `net10.0`, DAPR .NET SDK `1.18.4`, Aspire `13.4.6`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`. No package update is required; versions remain centralized.
- DAPR 1.18 supports per-state `ttlInSeconds` only for stores that support it. The status/archive DAPR stores already send this metadata; keep their TTL and the in-memory fake aligned. [DAPR state TTL documentation](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-store-ttl/)
- DAPR actor-state TTL requires the `ActorStateTTL` feature and the actor API warns that SDK caches can retain expired state until actor restart/deactivation. The current repo has no `ActorStateTTL` configuration, so use application-visible expiry for idempotency correctness and treat native actor TTL only as optional secondary cleanup. [DAPR actors API, actor-state TTL](https://docs.dapr.io/reference/api/actors_api/)
- Do not bypass `IActorStateManager` for aggregate actor state and do not introduce `DaprClient.QueryStateAsync`/Redis scans for correlation resolution. Preserve actor atomic staging + `SaveStateAsync` ownership.
- All new public/internal members need XML documentation; one C# type per file; file-scoped namespaces; `ConfigureAwait(false)` on production awaits; no `Guid.TryParse`/`Guid.NewGuid()` for EventStore message, correlation, causation, or aggregate identifiers.

### Previous Story Intelligence

- Story 4.1 identifies the existing eight-field duplicate-result mapping as shipped production behavior and fences FR27 re-keying/order/retry work into this story.
- Story 4.1 reached review during this run. Its focused lane passed 81/81, full Server passed 2303 with 25 skipped, Testing passed 144/144, and Release build recorded zero warnings/errors; its only production-adjacent change is test coverage in `AggregateActorIdempotencyTests.cs`.
- Preserve the new whole-result assertions. Adapt the rejected case away from `TenantMismatch`/wrong-tenant cache access because Story 4.2 intentionally makes that path unreachable before tenant denial.
- Preserve `3ccb1054` event identity/result fidelity, `b7823e4f` infrastructure-failure cache clearing, and `e0ad0fbe` post-actor projection-trigger ownership.
- Do not touch global-position allocation, CloudEvent id selection, global sharding (Story 4.6), deterministic replay serialization (Story 4.3), or broader committed-event activation/sweep recovery (Story 4.4) except for the minimum identity handoff needed by this story.

### Git Intelligence

- The last five commits at baseline (`322e3193`, `acc45f14`, `7e4bfef6`, `67b462de`, `0c325238`) concern projection erasure, batch ETag protection, releases, and BMad workflow changes; none resolves FR27.
- Recent relevant history is `3ccb1054` (global identity/idempotency fidelity), `b7823e4f` (clear staged state before infrastructure rejection), and `e0ad0fbe` (projection-trigger sequencing). Inspect those diffs before editing overlapping methods.
- Preserve the current user/shared worktree modifications. Do not change or initialize nested submodules, and do not modify `references/Hexalith.*` content for Story 4.2.

### Testing Requirements

- Test frameworks: xUnit v3 + Shouldly; NSubstitute for mocks. Use PascalCase scenario names and deterministic time. Do not introduce raw `Assert.*`.
- Build with the `.slnx`; run tests per project only. Package-reference mode is the Release/default evidence. Restore again if dependency mode changes.
- Deterministic gates:

```bash
dotnet build Hexalith.EventStore.slnx --configuration Release \
  -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false
```

- Dedicated higher-tier gates (require the documented Docker/DAPR/Aspire environment; keep outside the deterministic release lane):

```bash
dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false
```

- If Microsoft.Testing.Platform blocks project-level filtering, build the target test project and invoke its built xUnit v3 test executable/assembly with supported single-dash `-class` or `-method` selectors. Record the exact fallback and broad-gate blocker separately.

### Project Structure Notes

- No new project, package, AppHost resource, DAPR component, UI component, or external API host is expected.
- New identity/index/option/exception types belong in the existing owning package and each gets its own same-named `.cs` file. Do not place a second type beside an existing record for convenience.
- Expected new seams include a typed idempotency-check outcome, idempotency retention options/validator, tenant-scoped correlation-index record/store/fake, and command-identity conflict exception/handler/problem type. Reuse an existing equivalent only if all locked semantics above remain explicit and tested.
- Server actor state stays under `src/Hexalith.EventStore.Server/Actors`; status/archive/index contracts and stores stay under `...Server/Commands`; public additive contracts stay under `...Contracts/Commands`; in-memory parity stays under `...Testing/Fakes`.
- Correlation index storage must be tenant-scoped, bounded, TTL-aligned, and safe under concurrent writers. It is not a reason to add a general state-query abstraction.
- No `references/` submodule changes are part of this story.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 4 and Story 4.2, lines 1471-1532]
- [Source: `_bmad-output/planning-artifacts/prd.md` — FR27 and Event Correctness/Recovery done evidence, lines 147-160]
- [Source: `_bmad-output/planning-artifacts/prd.md` — NFR7/NFR16 and tenant-before-data rule, lines 205-224 and 241-243]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — AD-3, AD-5, AD-6, AD-10, AD-12, AD-17]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-04.md` — CP-7 Resume/idempotency integrity]
- [Source: `_bmad-output/implementation-artifacts/4-1-event-identity-and-duplicate-result-fidelity.md` — Story 4.1 boundaries, baseline, and prior-story intelligence]
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` — `ProcessCommandAsync`, resume, failure, terminal, drain, and advisory-status paths]
- [Source: `src/Hexalith.EventStore.Server/Actors/{PipelineState,ActorStateMachine,IdempotencyChecker,IdempotencyRecord}.cs`]
- [Source: `src/Hexalith.EventStore.Server/Commands/{DaprCommandStatusStore,DaprCommandArchiveStore,CommandStatusConstants,CommandArchiveConstants}.cs`]
- [Source: `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`]
- [Source: `src/Hexalith.EventStore/Controllers/{CommandsController,CommandStatusController,ReplayController}.cs`]
- [DAPR state TTL documentation](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-store-ttl/)
- [DAPR actors API reference](https://docs.dapr.io/reference/api/actors_api/)
- [Source: `_bmad-output/project-context.md` — EventStore technology, identity, testing, and workflow rules]

## Dev Agent Record

### Agent Model Used

OpenAI Codex (GPT-5)

### Implementation Plan / Decisions

- Implement each task in story order with per-task red/green/refactor and full Server regression gates before advancing.
- Represent exact command identity as `(MessageId, normalized CausationId, CommandType)` and keep correlation-addressed pipeline lookup only for collision discovery.
- Use explicit idempotency outcomes so misses, exact duplicates, recovery records, expiry, conflicts, and legacy migration cannot be conflated.

### Debug Log References

- Task 1 RED: Server test project failed to compile because the new exact-identity API/types did not exist.
- Task 1 GREEN: focused identity/idempotency lanes 93/93; full Server suite 2333 total, 2308 passed, 25 skipped, 0 failed.
- Task 2 RED: focused tenant/security lanes observed five actor-state calls before denial.
- Task 2 GREEN: focused tenant/security lanes 25/25; full Server suite 2333 total, 2308 passed, 25 skipped, 0 failed.
- Task 3 RED: committed-collision tests exposed correlation-only resume and missing message-keyed drain identity.
- Task 3 GREEN: focused resume/drain lanes 69/69; full Server suite 2336 total, 2311 passed, 25 skipped, 0 failed.
- Task 4 RED: retention option types were absent and failure-path tests observed terminal caching of transient/conflict outcomes.
- Task 4 GREEN: focused retention/retry/recovery lanes 84/84; full Server suite 2343 total, 2318 passed, 25 skipped, 0 failed.
- Task 5 GREEN: focused handler/store/index lanes 177/177; full Server suite 2346 total, 2321 passed, 25 skipped, 0 failed.
- Task 6 GREEN: focused gateway/controller/generator/error lanes 208/208; full Server suite 2362 total, 2337 passed, 25 skipped, 0 failed.
- Task 7 RELEASE: package-reference Release build succeeded with 0 warnings/errors. Server 2364 total, 2339 passed, 25 skipped; Client 637/637; REST generators 124/124; Testing 150/150; Admin Server 735 total, 717 passed, 18 skipped; LiveSidecar 29/29.
- Task 7 CONTRACTS: 694 total, 692 passed, 2 unrelated baseline policy failures in unchanged root guidance (`AGENTS.md` package inventory and `.github/copilot-instructions.md` commitlint text); non-Packaging contract lane 670/670.
- Task 7 HIGHER-TIER: Story 4.2 command/status/replay/OpenAPI/tenant-isolation integration lane 50/50. The unfiltered integration command was attempted twice but the external live-routing lane did not terminate within 15 minutes; the first attempt exposed unrelated query/dead-letter timeouts before cancellation. No Microsoft.Testing.Platform/xUnit v3 fallback was required.

### Completion Notes List

Ultimate context engine analysis completed - comprehensive developer guide created

- Task 1: Added additive pipeline/idempotency identity and expiry fields, explicit checker outcomes, message-key storage, exact ordinal matching, safe legacy migration, conflict preservation, and MessageId causation normalization.
- Task 2: Moved typed tenant validation ahead of all actor-state helpers and proved mismatch performs zero actor-state or advisory-status operations.
- Task 3: Added exact stage-aware resume classification, fail-closed legacy/same-message conflicts, atomic committed-range handoff, and message-keyed drain/reminder identity with legacy compatibility.
- Task 4: Added validated 24-hour retention with deterministic application expiry, terminal/recoverable classification, retryable pre-commit failures, and non-reexecuting post-commit recovery.
- Task 5: Re-keyed authoritative status/archive storage by MessageId, added self-describing records and a bounded ETag correlation index with advisory failure semantics, and carried message identity through concurrency conflicts.
- Task 6: Added canonical message tracking across responses, locations, status/replay/admin consumers, generated REST, ULID replay, and dedicated support-safe identity/correlation conflict responses.
- Task 7: Added committed-state, tenant-isolation, DAPR/fake parity, gateway contract, replay, OpenAPI, and evidence-preservation coverage; validated all deterministic story lanes and documented unrelated baseline/environment gate exceptions.

### File List

- src/Hexalith.EventStore.Server/Actors/AggregateActor.cs
- src/Hexalith.EventStore.Server/Actors/CommandProcessingIdentity.cs
- src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs
- src/Hexalith.EventStore.Server/Actors/IdempotencyCheckOutcome.cs
- src/Hexalith.EventStore.Server/Actors/IdempotencyCheckResult.cs
- src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs
- src/Hexalith.EventStore.Server/Actors/IdempotencyRecordDisposition.cs
- src/Hexalith.EventStore.Server/Actors/IdempotencyRetentionOptions.cs
- src/Hexalith.EventStore.Server/Actors/ValidateIdempotencyRetentionOptions.cs
- src/Hexalith.EventStore.Server/Actors/IIdempotencyChecker.cs
- src/Hexalith.EventStore.Server/Actors/PipelineState.cs
- src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs
- src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIdempotencyTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTenantValidationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/BackpressureTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyCheckerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyRecordTests.cs
- tests/Hexalith.EventStore.Server.Tests/Security/TenantInjectionPreventionTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/DeadLetterRoutingTests.cs
- tests/Hexalith.EventStore.Server.Tests/Configuration/IdempotencyRetentionOptionsTests.cs
- tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs
- src/Hexalith.EventStore.Admin.Server/Helpers/AdminStateStoreKeys.cs
- src/Hexalith.EventStore.Contracts/Commands/ArchivedCommand.cs
- src/Hexalith.EventStore.Contracts/Commands/CommandStatusRecord.cs
- src/Hexalith.EventStore.Contracts/Commands/SubmitCommandResponse.cs
- src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs
- src/Hexalith.EventStore.Server/Commands/ArchivedCommandExtensions.cs
- src/Hexalith.EventStore.Server/Commands/CommandArchiveConstants.cs
- src/Hexalith.EventStore.Server/Commands/CommandCorrelationIndexAddOutcome.cs
- src/Hexalith.EventStore.Server/Commands/CommandCorrelationIndexConstants.cs
- src/Hexalith.EventStore.Server/Commands/CommandCorrelationIndexEntry.cs
- src/Hexalith.EventStore.Server/Commands/CommandCorrelationIndexOptions.cs
- src/Hexalith.EventStore.Server/Commands/CommandCorrelationIndexRecord.cs
- src/Hexalith.EventStore.Server/Commands/CommandCorrelationResolution.cs
- src/Hexalith.EventStore.Server/Commands/CommandCorrelationResolutionOutcome.cs
- src/Hexalith.EventStore.Server/Commands/CommandIdentityConflictException.cs
- src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs
- src/Hexalith.EventStore.Server/Commands/ConcurrencyConflictException.cs
- src/Hexalith.EventStore.Server/Commands/DaprCommandArchiveStore.cs
- src/Hexalith.EventStore.Server/Commands/DaprCommandCorrelationIndex.cs
- src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs
- src/Hexalith.EventStore.Server/Commands/ICommandArchiveStore.cs
- src/Hexalith.EventStore.Server/Commands/ICommandCorrelationIndex.cs
- src/Hexalith.EventStore.Server/Commands/ICommandStatusStore.cs
- src/Hexalith.EventStore.Server/Pipeline/Commands/SubmitCommand.cs
- src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs
- src/Hexalith.EventStore.Testing/Fakes/InMemoryCommandArchiveStore.cs
- src/Hexalith.EventStore.Testing/Fakes/InMemoryCommandCorrelationIndex.cs
- src/Hexalith.EventStore.Testing/Fakes/InMemoryCommandStatusStore.cs
- src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs
- src/Hexalith.EventStore/Controllers/CommandStatusController.cs
- src/Hexalith.EventStore/Controllers/CommandsController.cs
- src/Hexalith.EventStore/Controllers/ReplayController.cs
- src/Hexalith.EventStore/ErrorHandling/CommandIdentityConflictExceptionHandler.cs
- src/Hexalith.EventStore/ErrorHandling/ConcurrencyConflictExceptionHandler.cs
- src/Hexalith.EventStore/ErrorHandling/ProblemTypeUris.cs
- src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs
- src/Hexalith.EventStore/Models/CommandStatusResponse.cs
- src/Hexalith.EventStore/Models/ReplayCommandResponse.cs
- src/Hexalith.EventStore/Models/SubmitCommandResponse.cs
- src/Hexalith.EventStore/OpenApi/CommandDocumentationTransformer.cs
- src/Hexalith.EventStore/OpenApi/ErrorReferenceEndpoints.cs
- tests/Hexalith.EventStore.Contracts.Tests/Commands/SubmitCommandResponseTests.cs
- tests/Hexalith.EventStore.IntegrationTests/EventStore/CommandStatusIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/EventStore/CommandsControllerTests.cs
- tests/Hexalith.EventStore.IntegrationTests/EventStore/OpenApiIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/EventStore/ReplayIntegrationTests.cs
- tests/Hexalith.EventStore.IntegrationTests/Helpers/JwtAuthenticatedWebApplicationFactory.cs
- tests/Hexalith.EventStore.IntegrationTests/Security/MultiTenantStorageIsolationTests.cs
- tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs
- tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs
- tests/Hexalith.EventStore.Server.Tests/Actors/EventPublicationIntegrationTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/CommandIdentityConflictTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/ConcurrencyConflictExceptionHandlerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/DaprCommandArchiveStoreTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/DaprCommandCorrelationIndexTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/DaprCommandStatusStoreTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/ReplayControllerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerArchiveTests.cs
- tests/Hexalith.EventStore.Server.Tests/Commands/SubmitCommandHandlerStatusTests.cs
- tests/Hexalith.EventStore.Server.Tests/Controllers/CommandsControllerResultPayloadTests.cs
- tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs
- tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiSpecTests.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerResultPayloadTests.cs
- tests/Hexalith.EventStore.Server.Tests/Pipeline/SubmitCommandHandlerTests.cs
- tests/Hexalith.EventStore.Testing.Tests/Fakes/AdjustableTimeProvider.cs
- tests/Hexalith.EventStore.Testing.Tests/Fakes/InMemoryCommandCorrelationIndexTests.cs

### Change Log

- 2026-07-12: Implemented Story 4.2 exact resume/idempotency integrity, message-primary command tracking, bounded correlation compatibility, safe replay/gateway propagation, and complete regression coverage.

### Review Findings

_Adversarial code review 2026-07-12 (baseline `322e3193` → working tree; layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor). All 7 ACs judged satisfied by the Acceptance Auditor. Post-resolution: 5 patch, 5 deferred, 8 dismissed. 2 decision-needed items were resolved 2026-07-12 (D1 → fix now in 4.2; D2 → accepted as-is)._

**Decision-needed (resolved 2026-07-12)**

- D1 — Resume & handoff reconstruct the wrong committed-event range (stream-tail assumption). **Resolution: fix now in 4.2** (moved to Patch below). The current `startSequence = CurrentSequence - EventCount + 1` tail-derivation in `LoadPersistedEventsForResumeAsync` (`AggregateActor.cs:1759-1760`) and `HandoffStaleCommittedCheckpointAsync` (`:1597-1598`) mis-publishes when an interleaved different-correlation command advances `CurrentSequence`, causing silent event loss + duplicate publication. Found independently by Blind Hunter + Edge Case Hunter; the added `StateMachineIntegrationTests` case only covers `CurrentSequence == staleEndSequence`.
- D2 — Legacy idempotency records fail closed with a permanent, never-expiring 409; `LegacyMigration` path is dead code (`IdempotencyChecker.ClassifyAsync:89-95` precedes expiry-removal at line 97; migration branch at line 106 never fires because 4.2 always writes at `idempotency:{messageId}`, line 77). **Resolution: accepted as-is** — spec-compliant AC5 fail-closed behavior; pre-release, no production legacy idempotency records. Dismissed.

**Patch**

- [x] [Review][Patch] (applied 2026-07-12) Persist the actual committed sequence range in the pipeline checkpoint and use it in both resume and handoff (resolves D1) [`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1597-1598,1759-1760`; `Actors/PipelineState.cs`] — replace the `CurrentSequence - EventCount + 1` tail-derivation by storing start/end sequence additively at the `EventsStored` checkpoint; fail-closed for legacy checkpoints lacking the range. Add a committed-state test where a different-correlation command advances `CurrentSequence` between crash and resume/handoff.
- [x] [Review][Patch] (applied 2026-07-12) Correlation `ResolveAsync` writes on the read path and fails GETs closed — `src/Hexalith.EventStore.Server/Commands/DaprCommandCorrelationIndex.cs:137-156`. Resolution performs a prune `TrySaveAsync`; on ETag-retry exhaustion it returns `Ambiguous` (→ HTTP 409) and a `TrySaveAsync` transport exception bubbles to 500. A correlation-compat status/replay/trace GET can therefore spuriously 409 under write contention or 500 on a maintenance-write error, contradicting the advisory/message-primary-authoritative contract. Fix: compute resolution from the already-read entries irrespective of the maintenance write; never let a maintenance write conflict/exception fail the read.
- [x] [Review][Patch] (applied 2026-07-12) `HandoffStaleCommittedCheckpointAsync` throws uncaught → actor poison loop — `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1592-1602` (call site 249-253). Handoff runs before the method's try/finally and dead-letter seam; an `InvalidOperationException` (metadata missing / invalid range) faults the actor turn with no dead-lettering, so DAPR redelivers into the same fault. Fix: route handoff failures through the existing infrastructure-failure/dead-letter path.
- [x] [Review][Patch] (applied 2026-07-12) Whitespace (non-null) CausationId not normalized → actor fault — `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:111`. `command.CausationId ?? command.MessageId` rescues only null; an empty/whitespace causation reaches `CommandProcessingIdentity.Validate()` (`CommandProcessingIdentity.cs:42-44`, before the try block) and throws `ArgumentException` uncaught. Fix: normalize with `string.IsNullOrWhiteSpace(command.CausationId) ? command.MessageId : command.CausationId`.
- [x] [Review][Patch] (applied 2026-07-12) `CommandCorrelationIndexOptions` has no `ValidateOnStart` — `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` (options binding) + `DaprCommandCorrelationIndex.ResolveAsync:117`. `AddAsync` guards invalid options at request time (line 37) but `ResolveAsync` does not, so a misconfigured `MaxConcurrencyRetries < 0` / `Capacity <= 0` makes the resolve loop never run and every resolution returns `Ambiguous` (409) with no startup error. Fix: add an `IValidateOptions`/`ValidateOnStart` mirroring `ValidateIdempotencyRetentionOptions`.

**Deferred**

- [x] [Review][Defer] Verification-coverage gaps on new fail-closed / drain-identity paths [`AggregateActor.cs:~1199,~1282,173-176`; `SubmitCommandHandler.cs:65-71,117-124`] — deferred: (a) message-keyed drain handoff + advisory-status identity only tested where `messageId ≡ correlationId`; (b) actor-level commit of a staged Expired-outcome idempotency mutation undriven; (c) SubmitCommandHandler fail-closed identity guards untested. Add focused committed-state tests before final acceptance.
- [x] [Review][Defer] AdminTraceQueryController correlation-resolution path untested + inherent advisory-index dependency [`src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs:59-78`; `Dw3TestUtilities.cs:185` builds a null index] — deferred: the resolve→ambiguity-409→message-primary-read branch is entirely unexercised; not-found-when-index-missing is inherent to an advisory index queried by correlationId (a state scan is forbidden). Add the resolution/ambiguity test.
- [x] [Review][Defer] Overflow marker never clears for a hot shared correlationId [`DaprCommandCorrelationIndex.cs:81`] — deferred: `OverflowExpiresAt` is refreshed each over-capacity `AddAsync`, so a steadily-loaded correlationId stays `Ambiguous` (409) indefinitely; minor, a saturated correlation is legitimately ambiguous.
- [x] [Review][Defer] Recoverable (stored-but-unpublished) idempotency records expire after 24h → possible domain re-execution [`IdempotencyChecker.cs:97-120`] — deferred to Story 4.4: bounded `ExpiresAt` applies to `Recoverable` records too; a retry after the retention window would be treated as a miss. Within the documented bounded-retention design.
- [x] [Review][Defer] Drain activity `eventstore.message_id` tag set to correlationId for legacy drain records [`AggregateActor.cs` `DrainUnpublishedEventsAsync`] — deferred: telemetry-accuracy only; the tracking id (a correlationId) is tagged as `message_id` before the real message id is added.

**Dismissed (noise / verified non-issues):** D2 legacy-record permanent 409 / dead migration path (accepted as-is, spec-compliant AC5 fail-closed, pre-release); archive-after-routing non-replayability (forced by conflict-before-write; advisory write); backpressure "double count" after handoff (correct accounting — old command genuinely still pending); SubmitCommandHandler identity-guard 409 for a null/foreign record (defensive; ULID key-collision ≈0); ConcurrencyConflictExceptionHandler dropping advisory status when `MessageId` null (all live constructions pass a non-null messageId — `SubmitCommandHandler.cs:254`; serialization ctors off-path); CommandStatusController cross-tenant legacy 409 (near-unreachable, no cross-tenant disclosure); conditional/after-routing "Received" status (advisory observability only); replay ULID not distinctly pinned by tests (behavior is correct — `UniqueIdHelper`; negligible regression risk).
