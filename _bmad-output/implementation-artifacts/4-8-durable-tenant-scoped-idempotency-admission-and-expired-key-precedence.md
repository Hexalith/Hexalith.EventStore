---
baseline_commit: afcc167ef277d9b95566e380228551037e4c3920
source_candidate_commit: 4fd0c34ff24c26dd6435f341eebe969a09bfc929
approved_change: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20-oq8-durable-idempotency-admission.md
---

# Story 4.8: Durable Tenant-Scoped Idempotency Admission And Expired-Key Precedence

Status: ready-for-dev

<!-- The source candidate at commit 4fd0c34f is release-blocked and is the UPDATE baseline, not completed Story 4.8. -->

## Story

As a platform operator,
I want every admitted mutation key to remain durably consumed after its replay result expires,
so that retries, conflicts, crashes, concurrent hosts, and old-key reuse cannot duplicate aggregate, domain, provider, repository, or other external effects.

## Acceptance Criteria

1. **Trusted admission.** A registered server-trusted adapter supplies the versioned canonical-intent descriptor and fixed retention class only after authentication, current authorization, and canonical validation. Public requests and extensions cannot select or override the descriptor bytes, digest, partition, actor state, fence, expiry, adapter authority, descriptor version, operation policy, or retention class.

2. **Tenant/key identity.** Admission actor identity is partitioned by managed tenant, digest-key version, and a protected digest of a caller-supplied opaque idempotency key, independently of command MessageId and aggregate identity. Same-tenant reuse against another operation, aggregate, target, delegated task scope, or behavior-affecting credential scope cannot bypass conflict detection. The same raw key in another tenant remains isolated.

3. **Protected key handling.** Versioned, domain-separated HMAC-SHA-256 derivation produces the tenant/key partition digest, collision-verification tag, and canonical-intent digest. Constant-time comparisons and key-buffer zeroing are used. Raw keys and protected intent never enter actor IDs, state keys or values, command status, archives, correlation indexes, downstream envelopes, logs, traces, metrics, exceptions, Problem Details, or evidence artifacts.

4. **Atomic state and fencing.** Reservation, descriptor comparison, monotonic fence issuance, and every state transition are actor-serialized and durable. Retries and recoverable resumes reuse the persisted current fence; they do not issue another fence. Exactly one current fence may cross the real aggregate, domain-service, provider, repository, projection, audit, scheduling, or other protected side-effect boundary. Crash, concurrency, restart, sidecar replacement, host failover, or mixed-version deployment cannot turn consumed state into fresh work.

5. **Replay, conflict, and expiry.** Every disposition re-evaluates current authorization without mutating admission state on denial. A live equivalent intent replays the exact logical result without aggregate or advisory-store work; a live different intent returns conflict. Expiry is inclusive at now >= expiresAt. Equivalent and different expired reuse return indistinguishable, non-retryable idempotency_key_expired responses before aggregate state, domain service, provider, repository, path, content, audit, projection, or scheduling work.

6. **Separate retention and governed tombstones.** Replay retention starts only at durable terminal finalization; unresolved states never expire into fresh work. Non-commit mutation replay results remain live for exactly 86,400 seconds; commit results use DateTimeOffset.AddYears(7), including leap-day behavior. Effective time is max(lastObservedAt, TimeProvider.GetUtcNow()). Expiry atomically removes the replay payload and live intent digest while replacing them with the approved metadata-only consumed-key tombstone. Consumed evidence remains for the managed-tenant lifetime plus 400 days after approved deletion-workflow entry; legal hold pauses and resumes the remaining interval, and digest keys cannot retire while referenced records remain.

7. **Fail-closed recovery and migration.** Reserved, pending, recoverable, and unknown_provider_outcome states never permit blind execution. Deterministic post-admission outcomes consume and finalize the key; transient failures before a protected effect become recoverable; failures after a protected effect whose outcome cannot be proven become unknown_provider_outcome. Recoverable work resumes only a persisted checkpoint under the existing current fence; unknown outcomes use read-only reconciliation. Unavailable, corrupt, collision, unknown-schema, unknown-key-version, and unsafe or uninventoried legacy state never becomes Missing. Migration preserves consumed-key knowledge atomically across aggregate identities or remains fail closed.

8. **Production evidence and release identity.** Verification proves persisted before/after state, exact time boundaries, replay compaction, current-fence enforcement, key rotation and collision handling, legacy migration, restart, at least two EventStore hosts sharing the approved production-equivalent DAPR state component, host failover, and leakage constraints. Every later non-execute request causes zero additional or duplicate mutation/side-effect execution; the eligible first writer executes exactly once, and permitted unknown-outcome reconciliation remains read-only. A machine-readable EventStore platform evidence packet binds the OQ8 design digest, source SHA, produced artifact identities, commands and counts, durable observations, environment, date, and approvals. The capability is not considered available to Folders until senior review and a separately authorized release or exact source/package/runtime pin.

## Tasks / Subtasks

- [ ] **Task 1 — Reconcile the approved authority before freezing implementation contracts** (AC: 1-8)
  - [ ] Apply the approved July 20 planning handoff to prd.md, architecture.md, epics.md, and the canonical Phase 4 SPEC companions: strengthen FR27, NFR7, and NFR16; add Story 4.8; add AD-25 and the refined AD-5 command flow; update traceability and readiness gates. Preserve Story 4.2 as done and Story 4.4 as publication-recovery owner.
  - [ ] Record the authority order in the updated artifacts: the approved July 20 change proposal and OQ8 design govern Story 4.8; the pre-change FR27/NFR7/NFR16/architecture text is historical context only.
  - [ ] Resolve the tombstone schema conflict before finalizing the state schema: the canonical OQ8 design describes a minimal tombstone without a fence, while the EventStore work item says to preserve the fence. Architecture and Security must record one approved shape and its minimization/recovery rationale in AD-25; implementation and evidence must use exactly that shape.
  - [ ] Freeze a crash-safe digest-key rotation/promotion protocol and a versioned legacy inventory/migration protocol. Per-actor serialization alone is insufficient when old and new digest versions route to different actor IDs.
  - [ ] Define the approved production-equivalent DAPR component/profile and the evidence packet path. Do not duplicate Folders' canonical oq8-idempotency-evidence.yaml.

- [ ] **Task 2 — Replace caller-authored descriptor authority with a trusted adapter boundary** (AC: 1, 2, 3)
  - [ ] Remove CanonicalIdempotencyDescriptor authority from public JSON and extension metadata. Public callers may supply an opaque idempotency key through the approved transport, but cannot supply canonical bytes, adapter/operation/version/tier authority, digests, fences, states, or expiry.
  - [ ] Add a server-owned adapter interface and registry that derive a descriptor after authentication, current tenant/operation authorization, payload validation, and canonical domain validation. Unknown adapter, operation, descriptor version, or policy fails closed before admission-state access.
  - [ ] Define canonical intent as operation, canonical target, normalized semantic payload/options, policy version, delegated task scope, and behavior-affecting credential scope. Exclude correlation, bearer or provider tokens, clocks, traces, delivery attempts, retry metadata, and other non-semantic transport data.
  - [ ] Make canonical encoding deterministic: versioned, length-prefixed, type-tagged, ordinal, duplicate-property rejecting, and schema-normalized only. Bind adapter ID, operation ID, descriptor version, and fixed tier explicitly rather than assuming the adapter embedded them in opaque canonical bytes.
  - [ ] Re-evaluate current authorization before replay, conflict, pending/recovery, unknown, corrupt, and expired responses. Authentication, authorization, or canonical-validation failures before admission neither consume nor disclose a key and leave existing admission state unchanged.

- [ ] **Task 3 — Separate opaque idempotency keys from EventStore command identity** (AC: 2, 3, 5)
  - [ ] Keep MessageId as the ULID-safe command/status/archive/event identity established by Stories 4.1 and 4.2. Introduce a distinct opaque idempotency-key input or a protected internal mapping; do not redefine MessageId as both identities.
  - [ ] Remove the source candidate's command.MessageId-as-raw-key path and its random GUID downstream MessageId. Persist a stable ULID-safe execution identity before execution when a distinct internal execution identity is required, and reuse it during recovery.
  - [ ] Ensure status, archive, correlation index, activity tracking, Location, error correlation, command envelopes, aggregate actor IDs, and domain-service calls never persist or log the raw opaque key.
  - [ ] Add sentinel-based leakage tests that inject the real raw key at the public/trusted boundary and scan actor/state-store keys and values, status, archive, index, logs, traces, metrics, exceptions, errors, downstream requests, and evidence. Tests that inject only precomputed digests do not satisfy this requirement.

- [ ] **Task 4 — Implement the versioned key ring, collision safety, and rotation protocol** (AC: 2, 3, 4, 7)
  - [ ] Replace active-key-only options access with an injectable versioned digest-key provider/key ring supporting one active writer, retained reader versions, explicit retirement, and support-safe unavailable/invalid-key outcomes. Retrieve production key material through the approved secret boundary; never expose it in configuration diagnostics.
  - [ ] Preserve the candidate's separate domain strings for tenant derivation, key partition, collision verification, and canonical intent. Continue HMAC-SHA-256, constant-time comparison, and zeroing of temporary key/plaintext buffers.
  - [ ] Look up retained reader-key actor identities before creating fresh active-version authority. Implement the approved crash-safe promotion/redirect protocol so mixed-version hosts cannot admit the same logical tenant/key through two actors.
  - [ ] Detect a matching partition digest with a mismatched verification tag as a dedicated fail-closed collision/corrupt outcome. Do not fall through to conflict, missing, or execution.
  - [ ] Refuse digest-key retirement while live records, tombstones, migration inventory, or legal-hold records reference that version; prove refusal and later success.

- [ ] **Task 5 — Complete the durable state machine, fencing, and execution integration** (AC: 4, 5, 7)
  - [ ] Preserve durable states reserved, pending, recoverable, unknown_provider_outcome, terminal, and expired, but implement the approved behavior matrix:
    - reserved equivalent returns the existing reservation/wait evidence; different intent conflicts;
    - pending equivalent returns accepted/task evidence; different intent conflicts;
    - recoverable resumes only a persisted checkpoint under the existing/current fence;
    - unknown_provider_outcome performs read-only reconciliation before any transition;
    - terminal returns exact logical replay after current authorization;
    - expired returns idempotency_key_expired before intent comparison for both equivalent and different intent.
  - [ ] Issue and persist the monotonic fencing token when execution authority is first durably granted. Equivalent retries and recoverable resumes reuse the existing persisted current fence; reject zero, stale, missing, or forged fences. Revocation or fence reissuance requires an explicit approved OQ8 design amendment.
  - [ ] Carry the current fence through an internal-only execution context and validate it immediately before AggregateActor, domain-service, provider, repository, projection, audit, or scheduling side effects and again before terminal completion. A handler-side BeginAsync call alone is not fence enforcement.
  - [ ] Keep orchestration unidirectional; do not create admission-actor/aggregate-actor cyclic calls or hold an actor turn across unbounded external I/O. Use a persisted bounded executor/checkpoint protocol if the admission actor cannot own the full protected turn.
  - [ ] Require the current fence before projection-activation outbox creation or scheduling because projection work is a protected side-effect boundary. Preserve the existing invariant that aggregate commit cannot become visible without recoverable projection activation.
  - [ ] Preserve AggregateActor-owned event mutation, exact committed sequence-range checkpoints, pending counts, stored-but-unpublished drain handoff, stable event MessageId, global-position behavior, and Story 4.4 recovery ownership.

- [ ] **Task 6 — Make replay, recovery, expiry, and public errors exact and support-safe** (AC: 5, 6, 7)
  - [ ] Classify failures at their durable boundary: deterministic post-admission outcomes consume/finalize the key; transient pre-effect failures become recoverable; and unprovable post-effect failures become unknown_provider_outcome. Persist the classification before returning or throwing.
  - [ ] Persist enough authoritative admission result data to replay the exact original logical success or deterministic failure without reading advisory status/archive stores. Preserve all CommandProcessingResult fields and the original payload-withholding decision.
  - [ ] Rehydrate typed domain rejection, concurrency conflict, backpressure, no-op, accepted result, withheld result payload, and other deterministic outcomes through their existing public mappings. Do not turn them into generic InvalidOperationException responses.
  - [ ] Implement bounded fenced recovery and reconciliation for reserved, pending, recoverable, and unknown outcomes. Persist the execution identity and checkpoint required for restart; never manufacture a new command identity after a crash.
  - [ ] Preserve HTTP 409 idempotency_key_expired with retryable=false, clientAction=refresh_state_then_submit_with_new_key, no Retry-After, metadata-only RFC 9457 detail, and current-request correlation only. Expired-equivalent and expired-different responses must be indistinguishable.
  - [ ] Preserve a distinct idempotency_conflict contract and add stable, typed, support-safe mappings for unavailable, corrupt/collision, unsafe legacy, pending/recovery, and unknown-provider outcomes. Project the stable code/category/retryable/clientAction fields through the typed client rather than leaving required semantics only in an untyped extension bag.

- [ ] **Task 7 — Implement governed compaction, deletion, legal hold, and legacy migration** (AC: 6, 7)
  - [ ] Preserve inclusive expiry and monotonic effective time. Add exact tests for one tick before, exactly at, and one tick after expiry; clock rollback; 86,400-second mutation retention; seven-year AddYears behavior; and Feb-29 completion.
  - [ ] Atomically replace the live replay result and intent digest with the approved minimal tombstone. Never delete first, and never let a compaction race produce Missing.
  - [ ] Add governed tenant-deletion lifecycle state: tenant lifetime, approved deletion-workflow entry, 400-day post-deletion countdown, legal-hold pause/resume with remaining interval, final purge, and key-retirement coordination.
  - [ ] Inventory versioned legacy aggregate-local raw-key/full-result records across tenant/key scope. Migrate only when consumed-key knowledge and exact logical result are preserved across aggregate identities; unknown, corrupt, cross-target-ambiguous, or uninventoried legacy state remains fail closed.
  - [ ] Keep the existing aggregate-local IdempotencyChecker expiry terminal so old paths cannot execute expired work while migration is incomplete. Do not remove or overwrite legacy evidence until migration is durably proven.

- [ ] **Task 8 — Prove production behavior and produce the platform evidence packet** (AC: 1-8)
  - [ ] Add unit/contract/client tests for trusted provenance, canonical equivalence, fixed policy tiers, tenant isolation, every state transition, legal/illegal fences, exact replay mappings, stable Problem Details, configuration validation, rotation, collision, migration, lifecycle, and leakage.
  - [ ] Build a dedicated multi-host live-sidecar fixture with at least two EventStore hosts/sidecars sharing the approved durable state component. The existing single-host direct-actor fixture remains supporting evidence only.
  - [ ] Prove concurrent equivalent and different first writers, crash after reservation, crash after fence issuance, failure at the side-effect boundary, crash after aggregate result and before terminal completion, application/sidecar restart, host failover, compaction races, mixed-version rotation and interrupted promotion, collision mismatch, unavailable/corrupt/unknown state, cross-aggregate migration, tenant deletion/legal hold, and key retirement.
  - [ ] Inspect persisted before/after state and assert zero additional or duplicate aggregate/domain/provider/repository/projection/audit/scheduling mutation for every later replay, conflict, expired, denied, unavailable, corrupt, unsafe legacy, pending, recoverable-without-checkpoint, or unreconciled unknown request. Prove exactly one total protected execution for the eligible first writer; permit only explicitly approved read-only reconciliation for unknown outcomes.
  - [ ] Create a machine-readable EventStore platform evidence packet containing the OQ8 design version and SHA-256, EventStore source SHA, package/container identities, actor/state schema, state component/profile, fixed tiers, exact boundary observations, test commands and pass/fail/skip counts, environment, leakage results, durable observations, date, reviewers, and approvals.
  - [ ] Run adversarial code, security, and verification-gap review. Do not publish, push, change a Folders pin, or claim OQ8 closed without separate explicit authority; record the reviewed release/pin handoff instead.

- [ ] **Task 9 — Update operator and developer documentation** (AC: 1-8)
  - [ ] Update docs/concepts/command-lifecycle.md so authentication, current authorization, canonical validation, and trusted admission precede execution; remove stale causation-keyed/delete-on-expiry descriptions.
  - [ ] Update docs/concepts/architecture-overview.md with the admission authority, unidirectional fencing/recovery flow, and relationship to AggregateActor.
  - [ ] Update docs/reference/command-api.md to separate MessageId from the opaque idempotency key and document stable conflict/expired/unavailable/corrupt/unsafe-legacy outcomes.
  - [ ] Update docs/guides/configuration-reference.md with adapter registration, fixed operation tiers, digest-key provider/readers/retirement, tombstone retention, tenant deletion/legal hold, and support-safe startup failures.
  - [ ] Keep UX unchanged. If an admin surface is later added for lifecycle operations, it requires a separate UX-owned story and must not expose raw keys, digests, protected intent, fences, or secret material.

## Dev Notes

### Authority, Current Baseline, And Completion Boundary

- The approved July 20 change proposal is the normative Story 4.8 contract. The current epics.md, prd.md, architecture.md, and canonical Phase 4 SPEC still contain the pre-OQ8 baseline and must be reconciled under Task 1.
- HEAD was clean at afcc167e during story creation. Commit 4fd0c34f added a substantial source candidate across 50 files, but its own approved work item marks the capability release-blocked. Treat all candidate files as UPDATE baselines; do not create parallel actors, protectors, coordinators, or public problem contracts.
- The source candidate is not proof of completion. Its documented release gates are the trusted-adapter boundary, separate opaque-key identity, reader-key rotation/promotion, cross-aggregate legacy migration, exact replay/recovery, multi-host/failover evidence, governed deletion/legal hold, senior review, and authorized release/pin.
- Story 4.8 can enter review only after all eight acceptance criteria have implementation and evidence. It cannot be marked done merely because focused unit tests pass or because the current candidate types exist.
- Folders owns its adapter, Contract Spine equivalence, generated SDK/C13 matrix, REST/CLI/MCP projection, canonical OQ8 evidence manifest, final OQ8 closure, and two unrelated NFR traceability failures. Do not mutate the Folders repository or build a parallel Folders DAPR ledger in this story.

### Current Candidate: UPDATE Map And Preservation Constraints

| File / seam | Current state | Story action and behavior to preserve |
| --- | --- | --- |
| Contracts/Commands/SubmitCommandRequest.cs and Models/SubmitCommandRequest.cs | Public JSON accepts CanonicalIdempotencyDescriptor, canonical bytes, and tier. | UPDATE: public input carries only approved opaque-key material; derive descriptor server-side. Preserve additive compatibility only where it does not retain caller authority. |
| Contracts/Commands/CanonicalIdempotencyDescriptor.cs | Domain-neutral descriptor shape exists. | UPDATE or move behind the trusted seam; bind adapter/operation/version/tier explicitly and keep one type per file. |
| Controllers/CommandsController.cs and request validator | Authenticates, validates, then forwards request.Idempotency unchanged. | UPDATE: resolve trusted adapter after current authorization/canonical validation. Preserve tenant auth, extension sanitization, request-size limits, absolute status Location, and support-safe responses. |
| Commands/IdempotencyKeyProtector.cs | Uses separate HMAC domains, active key only, and active-key-version actor IDs. | UPDATE: injectable key ring, retained readers, rotation promotion, stable authority, collision outcome, secret-safe diagnostics. Preserve HMAC-SHA-256, constant-time comparisons, and buffer zeroing. |
| Commands/IdempotencyAdmissionCoordinator.cs and session | Routes to the actor, uses MessageId as raw key, and generates an unpersisted GUID execution ID. | UPDATE: separate key/message identity, ULID-safe persisted execution identity, reader lookup/promotion, bounded recovery, and real fence propagation. |
| Actors/IdempotencyAdmissionActor.cs and record/request/result/state types | Durable reservation, monotonic observed time, inclusive expiry, fixed tier expiry, compaction, and basic fence equality exist. New records always use fence 1. | UPDATE: monotonic fence protocol, complete state matrix, exact replay/recovery, lifecycle/migration schemas, stable failures. Preserve atomic SaveStateAsync ownership and existing correct expiry behavior. |
| Pipeline/SubmitCommandHandler.cs | Admission precedes command routing; recovery states are recorded, but pending/recoverable/unknown/corrupt become generic failures and the fence is not checked by AggregateActor. | UPDATE: trusted context, exact replay, bounded reconciliation, current-fence execution. Preserve projection activation write-ahead, advisory status/archive/index behavior, and projection trigger sequencing. |
| Actors/AggregateActor.cs and IdempotencyChecker.cs | Story 4.2 exact identity and tenant-before-state behavior remain; expired local records now return terminal expired. | UPDATE only for internal fence/migration integration. Preserve event mutation, exact sequence ranges, duplicate fidelity, pending counts, drain recovery, stable event IDs, and expired-before-domain behavior. |
| Error handlers, ProblemTypeUris, GatewayProblemDetailsExtensions | Conflict and expired 409 mappings exist. | UPDATE additively for governed typed outcomes and client projection. Preserve expired code/action/retryability/current correlation and no secret/protected hints. |
| Server.Tests and LiveSidecar idempotency tests | Useful actor/handler/expiry/restart coverage exists; live fixture is one host and raw-key leakage sentinel is not passed through the real boundary. | UPDATE and extend; do not count direct protected-digest actor tests as end-to-end leakage or multi-host proof. |
| docs/superpowers/specs/2026-07-19-durable-idempotency-admission.md | Approved implementation work item lists the remaining release gates. | UPDATE as gates close; keep it honest and never describe the candidate as released before reviewed artifact identity exists. |

### Expected NEW Seams

Exact type names are implementation-owned. Prefer focused types in the existing owning packages:

- A trusted canonical-intent adapter interface, adapter registry, trusted request/context, and registry/options validator.
- A versioned digest-key provider/key ring with active and retained reader versions plus retirement governance.
- A crash-safe key-version locator/promotion or redirect protocol.
- A versioned legacy inventory/migration protocol with an explicit unsafe-cross-aggregate outcome.
- Internal fenced execution context plus bounded resume/reconciliation contracts.
- Stable typed admission failures and gateway/client projections for unavailable, corrupt/collision, unsafe legacy, pending/recovery, and unknown outcomes.
- Tenant deletion, legal-hold, post-deletion retention, purge, and digest-key retirement lifecycle contracts.
- A dedicated multi-host live-sidecar fixture if extending the current fixture would destabilize unrelated tests.
- A machine-readable EventStore platform evidence packet under _bmad-output/implementation-artifacts. Do not duplicate the Folders manifest.

Do not add a new project, package, AppHost resource, DAPR component, UI host, or third-party dependency unless the approved design proves it necessary. Reuse the current Contracts, Server, Client, gateway host, Server.Tests, and Server.LiveSidecar.Tests projects.

### Architecture Compliance

- **AD-3:** The gateway remains the command/query/status policy boundary. Public/generator/UI/Admin callers never invoke admission or aggregate actors directly.
- **AD-5 refined by AD-25:** The admission actor owns tenant/key serialization and fences; AggregateActor remains the durable event-mutation coordinator after admission.
- **AD-6:** Preserve gapless aggregate sequence, stable event MessageId, non-zero global position, and complete duplicate-result fidelity.
- **AD-9:** If actor registration, sidecar configuration, state component, ACL, resiliency, or topology changes, update AppHost, DAPR YAML, and topology tests together and restart Aspire.
- **AD-10:** Authentication and current tenant/operation authorization precede state disclosure. Never trust public descriptors, wire-asserted admin flags, DAPR ACLs alone, or caller-selected tiers.
- **AD-12 / NFR7 / NFR16:** Persisted production-path evidence is mandatory. HTTP status, mock calls, unit fakes, source scans, and single-host direct-actor tests are supporting evidence only.
- **AD-18:** Preserve handler-owned replacement of outbound DAPR routing headers; do not add per-host header logic.
- **NFR12:** Evolve public contracts additively where safe, but compatibility cannot preserve a security-authority bug.

### Library / Framework Requirements And Current Technical Notes

- Keep the centralized current baseline: .NET SDK 10.0.302, target net10.0, Dapr .NET SDK 1.18.4, Aspire 13.4.6, MediatR 14.2.0, Microsoft.Extensions.TimeProvider.Testing 10.8.0, xUnit v3 3.2.2, Shouldly 4.3.0, and NSubstitute 6.0.0. Story 4.8 needs no package change unless implementation proves otherwise.
- DAPR actor turn serialization applies per actor ID. Key rotation currently changes the actor ID, so DAPR alone cannot serialize old-version and new-version identities; the rotation/promotion protocol is an application responsibility. [DAPR Actors overview](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/)
- DAPR actor state requires a transactional state store and a distributed backend with strong consistency. Verify the approved component/profile rather than assuming default eventual state semantics. [DAPR state management overview](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/)
- Use HMACSHA256.HashData for domain-separated digests, CryptographicOperations.FixedTimeEquals for value-independent comparison, and CryptographicOperations.ZeroMemory for temporary secret/plaintext buffers. [HMACSHA256](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256.hashdata?view=net-10.0), [FixedTimeEquals](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.fixedtimeequals), [ZeroMemory](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-10.0)
- Problem Details is RFC 9457, which supersedes RFC 7807. Keep details machine-readable and support-safe; do not turn them into implementation-debug dumps. [RFC 9457](https://datatracker.ietf.org/doc/html/rfc9457)
- All production awaits use ConfigureAwait(false); public/protected/internal members need XML docs; one C# type per file; file-scoped namespaces; Allman braces; no copyright header; package versions stay out of project files.

### Previous Story Intelligence

- Story 4.1 established stable persisted event identity, CloudEvent MessageId identity, and all-eight-field CommandProcessingResult duplicate fidelity. Preserve those behaviors through terminal replay and migration.
- Story 4.2 established exact identity as MessageId plus normalized CausationId plus CommandType, tenant-before-state ordering, retryable/recoverable separation, message-primary status/archive storage, and exact committed-range checkpoints.
- Story 4.2 review found that deriving a committed range from the mutable stream tail caused data loss/duplicate publication; the fix persists exact start/end sequences. Do not reconstruct or overwrite those checkpoints during admission recovery.
- Story 4.2 explicitly deferred recoverable-record expiry and actor-level expired mutation coverage. Story 4.8 closes the broader tenant/key residual; an AggregateActor-only return-on-expired patch is insufficient.
- Stories 4.3-4.7 do not gate Story 4.8. Story 4.4 retains publication-recovery ownership, Story 4.5's append-race evidence remains separate, and Story 4.7 is unrelated Tenants query-provenance work.
- Historical test counts in prior story files are context, not current Story 4.8 evidence.

### Git Intelligence

- Relevant sequence: 9e7fde9d strengthened Story 4.1 fidelity; 298e191a introduced exact idempotency identity; ddccb9b1 implemented Story 4.2; 66334657 fixed committed-range and correlation-index review findings; 4fd0c34f added the current admission candidate.
- Current last five commits are afcc167e, d5b2001c, 4fd0c34f, 409731ba, and 4af84a18. Only 4fd0c34f changes admission code; afcc167e records the approved major-scope proposal and release-blocked boundary.
- Inspect 4fd0c34f before modifying overlapping files. Preserve unrelated user changes if the worktree changes after this story was created.

### Testing Requirements

Use xUnit v3, Shouldly, NSubstitute, FakeTimeProvider, ActorHost.CreateForTest, and ActorStateManagerTestHelper. Unit tests assert staged state shape and SaveStateAsync; negative paths assert no state and no downstream calls. Live tests use the LiveSidecar category and inspect the actual durable store.

Run projects individually; do not run solution-level dotnet test. Use package-reference mode for Release evidence and restore after changing dependency mode.

Focused deterministic lanes:

    dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
      --configuration Release -p:UseHexalithProjectReferences=false \
      --filter "FullyQualifiedName~IdempotencyAdmissionActorTests|FullyQualifiedName~SubmitCommandHandlerIdempotencyAdmissionTests|FullyQualifiedName~AggregateActorIdempotencyTests|FullyQualifiedName~IdempotencyCheckerTests|FullyQualifiedName~CommandIdentityConflictTests|FullyQualifiedName~SubmitCommandRequestValidatorTests"

    dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj \
      --configuration Release -p:UseHexalithProjectReferences=false \
      --filter "FullyQualifiedName~SubmitCommandRequestTests|FullyQualifiedName~CommandEnvelopeTests"

    dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj \
      --configuration Release -p:UseHexalithProjectReferences=false \
      --filter "FullyQualifiedName~EventStoreGatewayClientTests"

Broad deterministic gates:

    dotnet build Hexalith.EventStore.slnx --configuration Release \
      -p:UseHexalithProjectReferences=false

    dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
      --configuration Release -p:UseHexalithProjectReferences=false

    dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj \
      --configuration Release -p:UseHexalithProjectReferences=false

    dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj \
      --configuration Release -p:UseHexalithProjectReferences=false

Higher-tier gates, outside the deterministic release lane:

    dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj \
      --configuration Release -p:UseHexalithProjectReferences=false \
      --filter "FullyQualifiedName~IdempotencyAdmission"

    dotnet test tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj \
      --configuration Release -p:UseHexalithProjectReferences=false

The existing LiveSidecar command is insufficient for AC8 until the multi-host/failover fixture and evidence exist. If Microsoft.Testing.Platform rejects a project-level filter, build the project and invoke the built xUnit v3 assembly with single-dash class or method selectors. Record the broad-gate blocker separately from focused results.

### Project Structure Notes

- Admission contracts remain under src/Hexalith.EventStore.Contracts/Commands.
- Actor state/contracts remain under src/Hexalith.EventStore.Server/Actors.
- Coordination, key protection, migration, and recovery remain under src/Hexalith.EventStore.Server/Commands unless an existing feature folder is a closer owner.
- Public controller/problem mappings remain under src/Hexalith.EventStore.
- Client problem projection remains under src/Hexalith.EventStore.Client/Gateway.
- Fakes stay behaviorally aligned under Hexalith.EventStore.Testing when a deterministic fake is introduced or changed.
- No UI work and no references submodule mutation are in scope.

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20-oq8-durable-idempotency-admission.md — approved Story 4.8 contract, AD-25, tasks, evidence, ownership, and success criteria]
- [Source: docs/superpowers/specs/2026-07-19-durable-idempotency-admission.md — implemented candidate and release gates]
- [Source: _bmad-output/planning-artifacts/epics.md — Epic 4 and Stories 4.1-4.7 context]
- [Source: _bmad-output/implementation-artifacts/4-1-event-identity-and-duplicate-result-fidelity.md — stable identity/result fidelity]
- [Source: _bmad-output/implementation-artifacts/4-2-resume-and-idempotency-integrity.md — exact identity, expiry residual, review learnings, and file patterns]
- [Source: _bmad-output/planning-artifacts/architecture.md — AD-3, AD-5, AD-6, AD-9, AD-10, AD-12, AD-18]
- [Source: _bmad-output/project-context.md — technology, identity, actor, testing, and workflow rules]
- [Source: global.json and references/Hexalith.Builds/Props/Directory.Packages.props — current SDK and centralized package versions]
- [Source: src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs, src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs, src/Hexalith.EventStore.Server/Commands/IdempotencyKeyProtector.cs, and src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs — current candidate flow]
- [Source: tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionLiveSidecarTests.cs and Fixtures/DaprTestContainerFixture.cs — current single-host evidence boundary]

## Dev Agent Record

### Agent Model Used

To be recorded by the implementing agent.

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created
- Story file created from the approved change proposal, current source candidate, architecture, prior-story review intelligence, current official platform documentation, and exhaustive affected-file analysis.
- Implementation is not complete. Commit 4fd0c34f remains a release-blocked source candidate until every acceptance criterion and production evidence gate is satisfied.

### File List

### Change Log

- 2026-07-20: Created the context-filled Story 4.8 implementation and evidence guide; status set to ready-for-dev.
