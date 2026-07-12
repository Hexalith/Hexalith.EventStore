# Story 4.2: Resume And Idempotency Integrity

Status: ready-for-dev

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

- [ ] **Task 1 — Introduce exact command identity in pipeline and idempotency state** (AC: 1, 3, 5)
  - [ ] Extend `PipelineState` additively with `MessageId` and normalized `CausationId`; retain `CorrelationId`, `CommandType`, stage, timestamps, event counts, rejection metadata, and legacy `ResultPayload` compatibility. Append optional/defaulted fields so old JSON can deserialize.
  - [ ] Extend `IdempotencyRecord` additively with `MessageId`, `CommandType`, and an application-visible expiration timestamp (or equivalently explicit bounded-retention field). Preserve `CausationId`, `ProcessedAt`, and all eight `CommandProcessingResult` fields from Story 4.1.
  - [ ] Re-key new idempotency entries as `idempotency:{messageId}`. Pass the complete normalized identity into `CheckAsync` / `RecordAsync`; do not continue using a correlation-derived fallback as the key.
  - [ ] Normalize absent direct-actor `CausationId` to `MessageId`, not `CorrelationId`. The normal gateway path already maps `CausationId = MessageId`; preserve explicit non-null causation ids supplied by legitimate callers.
  - [ ] Return a cached terminal result only when `MessageId`, normalized `CausationId`, and `CommandType` all match with ordinal semantics. Add a dedicated support-safe identity-conflict path for mismatches and unverifiable legacy records; never log payloads or raw protected data.
  - [ ] After a new message-key miss, perform at most one bounded legacy lookup at `idempotency:{normalizedCausationId}` when that key differs from the message key. If the legacy record contains the new fields and matches exactly, stage an atomic copy-to-message-key/remove-old-key migration and commit it before returning the cached result. If its identity cannot be proven, preserve it and return `command_identity_conflict`; never delete it and re-execute the command.
  - [ ] Model checker outcomes explicitly (miss, exact terminal duplicate, expired, retryable/recoverable, identity conflict, legacy migration) so `AggregateActor` cannot conflate conflict with a cache miss.
  - [ ] Keep the pipeline actor-state key correlation-addressable for collision discovery unless an equivalent bounded lookup is introduced. A new command reusing a correlation id must still find and classify the old checkpoint rather than strand it silently.

- [ ] **Task 2 — Move tenant validation ahead of every actor-state operation** (AC: 2)
  - [ ] In `AggregateActor.ProcessCommandAsync`, validate `command.TenantId` against `Host.Id` immediately after argument/cancellation/activity setup and before creating or invoking `IdempotencyChecker` / `ActorStateMachine` or reading pending counts.
  - [ ] On tenant mismatch, preserve the current typed/support-safe denial and security telemetry, but perform no idempotency rejection write, pipeline cleanup, status write, or actor-state commit.
  - [ ] Replace the misleading existing tenant test that permits an `IdempotencyRecord` read with assertions that the unauthorized path makes zero actor-state calls. Keep the existing prohibition on aggregate metadata/event reads.
  - [ ] Preserve actor-id parsing through `TenantValidator`; do not query DAPR state or accept a wire assertion to decide tenant ownership.

- [ ] **Task 3 — Make stale checkpoint handling stage-aware and loss-safe** (AC: 1, 3, 5)
  - [ ] Add one exact identity comparison helper used by all resume branches. Do not scatter partially different comparisons through `AggregateActor`.
  - [ ] If a mismatched stale checkpoint is `Processing` (no committed events), stage cleanup, commit it, and process the incoming command normally.
  - [ ] If a mismatched checkpoint can represent committed events (`EventsStored` or a defensive legacy equivalent), do not publish or complete those events with the incoming command envelope. Convert/preserve the old event range on the existing drain/recovery seam under the old command's unique message identity, commit the handoff and checkpoint cleanup, then process the incoming command.
  - [ ] A legacy committed checkpoint without `MessageId`/`CausationId` is not safe to drain under the incoming identity. Do not infer command `MessageId` from an event `MessageId`. Unless companion persisted data proves the complete old command identity unambiguously, preserve the checkpoint/events, return `command_identity_conflict`, and do not execute the incoming command. Add an explicit recovery diagnostic; never clean the checkpoint merely to unblock the new command.
  - [ ] Ensure stale-state cleanup and drain handoff cannot be overwritten when two commands share a correlation id. If `UnpublishedEventsRecord` / reminder identity must move from correlation id to message id to achieve this, make that narrow additive identity change while retaining correlation as metadata. Story 4.4 still owns broader activation/sweep and unrecoverable-publication semantics.
  - [ ] Preserve pending-command accounting: no double increment/decrement, no negative count, and no stale checkpoint causing a valid new command to bypass backpressure or domain invocation.
  - [ ] Add committed-state tests for same identity resume, same-correlation/different-message, same-message/different-command-type, causation mismatch, legacy missing identity, and stale committed-event handoff.

- [ ] **Task 4 — Bound idempotency retention and classify outcomes correctly** (AC: 3, 5)
  - [ ] Add centrally registered idempotency retention options; default terminal retention is 86,400 seconds (24 hours) and validation rejects any value shorter than the configured status/archive TTL. Keep package versions centralized and do not add a new dependency. Use `TimeProvider` for deterministic expiration tests if time abstraction is needed (`Microsoft.Extensions.TimeProvider.Testing` is already centrally available).
  - [ ] Treat accepted, accepted-no-op, and domain-rejected results as terminal deduplicated outcomes. Their records receive the configured bounded expiration.
  - [ ] Do not persist pre-commit transient infrastructure failures or exhausted persistence-conflict results as terminal duplicate records. Preserve `StateManager.ClearCacheAsync()` before staging failure cleanup so partially staged events remain uncommitted.
  - [ ] Keep publish failures / stored-but-unpublished results on a recoverable path that prevents domain re-execution; do not turn a post-commit publication failure into a clean idempotency miss. Preserve the existing drain record and stable persisted event `MessageId` behavior needed by Story 4.4.
  - [ ] Make application-level `ExpiresAt` checking authoritative. Do not rely solely on DAPR actor-state TTL: the repository does not enable `ActorStateTTL`, and DAPR 1.18 documents that actor SDK caches can retain expired state until deactivation. Expired records are staged for removal and treated as misses only after exact identity and safety rules are applied. A legacy record without `ExpiresAt` is not treated as expired; exact new identity fields are required for safe migration, otherwise AC 5 applies.
  - [ ] Update `IdempotencyCheckerTests`, `AggregateActorIdempotencyTests`, and failure-path tests to prove transient retry, terminal duplicate fidelity, expiration, mismatch conflict, and preservation of all result fields.
  - [ ] Reconcile Story 4.1's newly strengthened rejected-duplicate actor test: keep its eight-field fidelity assertion, but do not use a wrong-tenant envelope to reach the cache after tenant validation moves first. Seed a terminal domain rejection under the correct tenant and test tenant mismatch separately as a zero-state-access denial.

- [ ] **Task 5 — Re-key status/archive storage and add a tenant-scoped correlation index** (AC: 4, 6, 7)
  - [ ] Change `ICommandStatusStore`, `ICommandArchiveStore`, DAPR implementations, constants, and in-memory fakes so primary operations name and use `messageId`; new keys are `{tenant}:{messageId}:status` and `{tenant}:{messageId}:command`.
  - [ ] Add `MessageId` and `CorrelationId` to stored status/archive data additively where required so records are self-describing and can populate responses/indexes without treating the key argument as correlation.
  - [ ] Add a bounded, tenant-scoped correlation index whose entries can represent one correlation id mapping to multiple message ids. Default capacity is 128 live entries per `(tenant, correlationId)` and each entry carries its own expiry aligned with the status/archive TTL. Do not use `DaprClient.QueryStateAsync`, direct Redis scans, or an unbounded in-memory list.
  - [ ] Make `SubmitCommandHandler` the single index-write owner. It invokes the index once per submission after attempting the authoritative message-keyed status/archive writes; neither store independently updates the same index. Duplicate message ids are idempotent no-ops.
  - [ ] Update the shared index with ETag optimistic concurrency and a bounded default of three retries. Before add or resolution, prune expired entries; resolution may also prune entries whose authoritative message-primary record no longer exists. If capacity remains full, set/preserve an overflow marker and fail correlation compatibility safely without evicting an arbitrary live message. Primary message-id lookup remains available.
  - [ ] Define partial-failure semantics: the message-keyed record is authoritative; index maintenance is advisory/rebuildable and cannot make a valid message-id lookup fail. After retry exhaustion, log only support-safe identity metadata and continue the primary command path; make the in-memory fake expose equivalent observable behavior and deterministic conflict injection.
  - [ ] New writes use message-id primary keys and correlation-index writes; do not continue dual-writing new correlation-primary status/archive records. A bounded legacy read fallback may read pre-existing correlation-primary records only after tenant authorization and only when no message-primary/index result exists.
  - [ ] When a correlation lookup is ambiguous, return a deterministic support-safe conflict rather than selecting newest/first. Never disclose message ids belonging to an unauthorized tenant.
  - [ ] Update `SubmitCommandHandler` to write/read status and archive by `request.MessageId` while preserving actual `request.CorrelationId` as metadata and activity-tracing input.
  - [ ] Carry message identity through `ConcurrencyConflictException` and `ConcurrencyConflictExceptionHandler`; their advisory status write cannot remain correlation-primary after the store contract changes.

- [ ] **Task 6 — Carry the message tracking key through gateway, replay, generated REST, and admin consumers** (AC: 4, 6, 7)
  - [ ] Add an additive canonical message/status tracking field to `SubmitCommandResult` and public `SubmitCommandResponse`; keep the real correlation id available for tracing/backward-compatible clients. The single status key used by `Location` becomes `MessageId` (or an explicitly named `StatusKey` resolving to it), not a correlation value hidden under the old name.
  - [ ] Map actor idempotency identity conflict to a dedicated `CommandIdentityConflictException`/problem type at the gateway: HTTP `409`, no `Retry-After`, no cached rejection/status mutation, and a support-safe detail telling the caller to submit the correct tuple or a new `MessageId`. Do not expose stored command identity values or translate it to a generic `500`.
  - [ ] Update `CommandsController`, `CommandStatusController`, `ReplayController`, `CommandStatusResponse`, and `ReplayCommandResponse` so primary routes/lookups/locations use message id. Preserve a bounded tenant-authorized correlation compatibility path per AC 6.
  - [ ] Update `RestApiControllerEmitter` and client status-location logic to use the canonical tracking field while preserving AD-17: absolute gateway-owned `Location` when configured, no `Location` when unconfigured, and no external-host status endpoint.
  - [ ] Update `ArchivedCommandExtensions` so replay creates a new ULID `MessageId` and preserves a separately meaningful correlation chain. Remove the existing `Guid.NewGuid()` command-message generation on this touched path; EventStore message/correlation/causation identifiers are ULID-safe.
  - [ ] Reconcile `AdminTraceQueryController`, activity/status consumers, and the duplicated `AdminStateStoreKeys` helper/test contract with message-primary identity. Keep admin correlation search as indexed search, not primary storage identity.
  - [ ] Preserve `SubmitCommandHandler`'s newer projection behavior: trigger projection update only after an accepted result with events, keep it advisory, and do not change event publication's `triggerProjectionUpdate: false` ownership.

- [ ] **Task 7 — Prove the complete behavior and regression boundaries** (AC: 1-7)
  - [ ] Actor/state-machine tests: exact resume match, correlation collision, causation/type mismatch, legacy record, committed-event drain handoff, pending-count balance, and committed checkpoint cleanup.
  - [ ] Security tests: unauthorized tenant causes no actor-state/idempotency/pipeline read or write; status/archive/index queries search only authorized tenants and produce indistinguishable not-found behavior across tenants.
  - [ ] Retry/idempotency tests: transient infrastructure and exhausted pre-commit conflict retry successfully; terminal accepted/no-op/domain rejection returns the original eight-field result; post-commit publish failure does not re-run domain/event persistence.
  - [ ] Store/fake tests: message-id key shape, TTL metadata/expiry, self-describing records, one-to-many correlation index, ambiguity, concurrent index update, legacy fallback, and DAPR/in-memory parity.
  - [ ] Gateway/contract/generator tests: response carries distinct message and correlation ids, `Location` uses the message/status key, status/replay resolve by message id, correlation compatibility is explicit, and generated external APIs remain gateway delegators.
  - [ ] Higher-tier evidence: inspect persisted actor state and DAPR state-store keys/records for tenant + message identity, stale checkpoint cleanup/drain handoff, retry outcome, index state, and absence of unauthorized reads. HTTP `202`/`200` and mock invocation counts alone are insufficient.
  - [ ] Run the per-project validation commands in Testing Requirements. Record exact pass/fail/skip counts and any Microsoft.Testing.Platform/xUnit v3 fallback; never weaken a gate or use solution-level `dotnet test`.

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

- Keep repository-pinned .NET SDK `10.0.301`, target `net10.0`, DAPR .NET SDK `1.18.4`, Aspire `13.4.6`, xUnit v3 `3.2.2`, Shouldly `4.3.0`, and NSubstitute `6.0.0-rc.1`. No package update is required; versions remain centralized.
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

{{agent_model_name_version}}

### Implementation Plan / Decisions

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed - comprehensive developer guide created

### File List

### Change Log
