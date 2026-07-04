# Sprint Change Proposal - EventStore Architecture Review Remediation

- **Date:** 2026-07-04
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Interactive design-gate decisions; proposal-only (no code changed this session)
- **Change classification:** **Major** (multi-epic replan; several items require new frozen specs)
- **Status:** Approved 2026-07-04 — routed (Major). Phase 0 partially executed (see Execution note); Phases 1–3 and backlog epics routed to Architect/PM.

> **Execution note (2026-07-04):**
> - **CP-1** was already resolved upstream — commit `88af8f27` fixed the OpenAPI 500 and semantic-release shipped **3.32.0**; `origin/main` is green. The "red main" premise (finding C5) was stale; no action taken. The OpenAPI failure was a floating `Microsoft.OpenApi` version now pinned to 2.9.0 via the `Hexalith.Builds` submodule.
> - **CP-2** (H1 `ClearCacheAsync`), **CP-4** (C3 Admin.UI secret strip → `Development` only), **CP-5** (H11 production symmetric-key guard + `AllowInsecureSymmetricKey` break-glass), **CP-6** (H12a tenant-filter parity + Admin-only `ListTenants`) — implemented and verified (full-solution Release + `warnaserror` build clean; targeted unit tests green, incl. 3 new guard tests).
> - **CP-3** (C2) — partial: `[AllowAnonymous]` removed from the three gateway admin controllers (closes the unauthenticated cross-tenant read) and `count` clamped; per-tenant scoping for authenticated callers is deferred to **CP-8**.
> - The 11 remediation epics are registered as `backlog` in `sprint-status.yaml`. Changes are uncommitted on the working tree pending review/commit.

> Formal PRD/epic documents are not maintained under `_bmad-output/planning-artifacts`. Impact
> analysis uses the 2026-07-04 architecture review (six parallel subsystem audits, all Critical and
> load-bearing High findings re-verified against source), `project-context.md`, the current
> `sprint-status.yaml` (Epic D in flight), and code inspection — the same basis used by prior
> proposals in this folder.

---

## 1. Issue Summary

A strict architecture and code review of Hexalith.EventStore (all 19 `src/` projects, samples,
tests, AppHost/DAPR topology, deploy assets; ~74k lines) surfaced **5 Critical, 14 High, ~20 Medium,
and ~30 Low** findings. The load-bearing ones cluster into five themes:

1. **Silent data-loss / correctness windows** in the command pipeline — a stale pipeline record keyed
   only by `CorrelationId` can hijack a different command and skip its execution (C1); the
   infrastructure-failure path commits partially-staged events (H1); there is no store-level
   optimistic-concurrency fence on append (H2); a crash between the event commit and publish loses
   publication permanently (H3).
2. **Authorization / tenant-isolation breaks** — anonymous admin controllers on the public port
   return cross-tenant event data (C2); Admin.UI ships a forgeable global-admin identity in base
   config (C3); a plaintext `dapr-caller-app-id` header mints global-admin with no proof of sidecar
   origin (C4); the domain-service SDK endpoints are unauthenticated and trust a wire-asserted
   `IsGlobalAdmin` flag (H7).
3. **Cost model that grows with stream length** — automatic snapshots nest the entire event history
   (H5); projection delivery is a full-stream replay per event, inside the aggregate actor's turn (H6).
4. **Missing evolution & operational capabilities** — no event schema-versioning/upcasting path (H8);
   no secret store in the production posture (H14); no GDPR/tombstone erasure; deferred admin
   operations presented in the UI as if functional.
5. **Config-and-test posture asserted but not enforced** — main is currently red and the release gate
   is blocked (C5); the security scoping validated by unit tests (`pubsub.yaml`, deny-by-default ACL,
   `keyPrefix`) is not the configuration the AppHost actually loads (H10); the strongest cross-tenant
   and JWT negative tests live in an integration suite that runs in no CI job (H13).

The review also confirmed genuine strengths that bound the blast radius and inform sequencing:
tenant **keying** is disciplined (every state key/actor-id/topic derives from validated, colon-free
`AggregateIdentity` components — no path drops TenantId), the events+metadata+snapshot+checkpoint
**single-transaction commit** is correct, ETag-CAS checkpoint stores are correct, polymorphic
deserialization is allow-list-only (no `Type.GetType`), and the ULID rule holds in the gateway.

### Design decisions taken (this session)

The following gates were decided interactively before drafting; the proposal reflects them:

| # | Gate | Decision |
|---|------|----------|
| 1 | Append durability (H2) | **Verify-first** — add a two-writer race test and confirm the real DAPR guarantee/exception before designing fencing |
| 2 | Subscriber delivery contract (M1, M12) | **At-least-once, unordered** — document it; subscribers dedupe by MessageId, order by SequenceNumber |
| 3 | Trust model (C2, C4, H7, H10, M14) | **Defense-in-depth** — app-layer token/authz on internal + domain + admin surfaces, not network posture alone |
| 4 | Crypto-shred boundary (M5, H5) | **Broker + snapshots INSIDE the plaintext boundary** — document; ensure retention + shredding procedures cover them |
| 5 | GDPR/tombstone erasure (M16) | **Backlog** as a tracked future epic |
| 6 | Deferred admin operations (M16) | **Hide/disable until real** — remove from UI, return 501 |
| 7 | Admin.UI identity (H12) | **Defer OIDC** to its own epic; fix the committed secrets (C3) now |
| 8 | GlobalPosition allocator (M2) | **Shard now** — renegotiates the frozen `global-event-ordering` spec |
| — | `system` tenant name (Low) | Default: **reserve** it at provisioning |
| — | AOT/trimming (Low) | Default: **explicitly not a target** — document the reflection-convention constraint |

---

## 2. Impact Analysis

### Epic Impact

- **Epic D (REST API generator)** — *in flight* (D-5 in-progress; D-6/7/8 in review). Largely
  **orthogonal** to this remediation; only two review items touch it (generator incrementality M19,
  generated-controller authz M19) and both land in the deferred/hardening backlog it already tracks
  (`deferred-work.md`). **Epic D is not resequenced by this proposal** — Phase 0 unblocks the shared
  red main that is currently also blocking D's release gate.
- **New epics proposed** (see §3): SEC-1 (trust boundary & auth), COR-1 (append/pipeline
  correctness), COR-2 (replay/dispatch/versioning), PERF-1 (snapshot/projection cost), REL-1
  (delivery & poison handling), OPS-1 (admin plane), CFG-1 (topology/deploy), TEST-1 (test & CI
  recovery). Plus **backlog epics**: GDPR-1 (erasure), IAM-1 (admin OIDC login), KIT-1 (aggregate
  test-kit).

### Story Impact

No existing story's *status* changes. Future event-store stories inherit these as **invariants**
once the corresponding phase lands (mirroring how the 2026-07-02 global-ordering proposal set
invariants):

- Pipeline resume matches on `MessageId`/`CausationId`, never `CorrelationId` alone.
- Command status/archive are keyed by `MessageId`; `CorrelationId` is an indexed field only.
- The infrastructure-failure path clears staged state before persisting a rejection.
- Event→Apply dispatch resolves on a `.`-boundary with ambiguity detection; a single shared
  `JsonSerializerOptions` governs all payload (de)serialization.
- Subscriber contract is at-least-once/unordered; dedupe by MessageId, order by SequenceNumber.
- Internal, domain-service, and admin-computation endpoints require an app-layer credential; no
  endpoint trusts a wire-asserted admin flag.

### Artifact Impact (primary source locations)

| Area | Key files |
|------|-----------|
| Command pipeline / actor | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `PipelineState.cs`, `IdempotencyChecker.cs`, `Events/EventPersister.cs`, `Events/EventPublisher.cs`, `Commands/CommandStatusConstants.cs` |
| Replay / dispatch | `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`, `Aggregates/EventStoreProjection.cs`, `Aggregates/EventStoreAggregate.cs` |
| Contracts / versioning | `src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs`, `Messages/MessageType.cs`, `Results/DomainServiceWireResult.cs`, `Queries/QueryEnvelope.cs` |
| Gateway auth | `src/Hexalith.EventStore/Authentication/*`, `Controllers/AdminStreamQueryController.cs`, `AdminTraceQueryController.cs`, `AdminCommandsQueryController.cs`, `ProjectionNotificationController.cs`, `Program.cs` |
| Domain-service SDK | `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`, `EventStoreDataProtectionServiceCollectionExtensions.cs` |
| Admin plane | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`, `Registration/ServiceCollectionExtensions.cs`, `Admin.Server/Services/Dapr*CommandService.cs`, `Admin.UI/appsettings.json`, `Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs`, `Admin.Cli/*` |
| Topology / deploy | `src/Hexalith.EventStore.AppHost/Program.cs`, `DaprComponents/pubsub.yaml`, `deploy/dapr/*.yaml`, `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`, `ServiceDefaults/Extensions.cs` |
| Tests / CI | `tests/Hexalith.EventStore.IntegrationTests/*`, `tests/Hexalith.EventStore.Server.Tests/*`, `.github/workflows/ci.yml`, `CLAUDE.md`, `perf/*` |

### Technical / release Impact

- **Release gate is currently blocked** (main red since the Gateway extraction) — semantic-release
  cannot run until Phase 0 lands. This is the highest-urgency item independent of everything else.
- **Frozen-spec renegotiation:** the GlobalPosition sharding decision (CP-11) requires updating
  `spec-dapr-global-event-ordering.md`, whose `frozen-after-approval` block lists "requires a new
  external database/index" and per-tenant scoping under **Ask First**. Treat as a spec change, not a
  drive-by edit.
- **Public NuGet contracts:** COR-2/versioning (H8) and the cancellation-token seam (M8) alter
  published interfaces (`IDomainProcessor`, `EventMetadata`, wire DTOs) — do these **before** the
  packages harden to avoid a breaking-change wave later.

---

## 3. Recommended Approach

**Direct Adjustment with staged replan.** Do not attempt a single sweep. Sequence into an
unblock phase (safe, direct-to-dev) followed by correctness, cost, and hardening epics, with the two
Large capabilities and the frozen-spec change gated behind their own specs.

### Phasing

| Phase | Goal | Findings | Scope class | Route |
|-------|------|----------|-------------|-------|
| **0 — Unblock & safe fixes** | Green main + close the cheapest Critical/High risks | C5, H1, C2, C3, H11, H12a, M13, plus quick Lows (Assert.Skip, CLI `--confirm`, gate Swagger, ULID admin middleware, doc correction) | **Minor** | Developer, direct |
| **1 — Pipeline correctness** | Remove silent data-loss windows | C1, H1(done in P0), H3, H4, H9, M4, M6, status re-key; H2 verify-first (test+spike); H7/C4 token; H10 config-drift | **Moderate** | Dev + Architect review |
| **2 — Cost & evolution** | Bound per-command/per-projection cost; enable schema change | H5, H6, PERF projection seq-guard, M2 (shard, spec renegotiation), H8 (versioning/upcasting) | **Major** | Architect specs → Dev |
| **3 — Delivery, admin, deploy** | Poison handling, admin authz/audit, prod posture | M1(doc), M3, M12, H12(b/c), OPS deferred-ops hide, H14, M17, M18, prod ACL/keyPrefix parity | **Moderate** | Dev + Ops |
| **Backlog epics** | Large capabilities & harness | GDPR-1 (erasure), IAM-1 (admin OIDC), KIT-1 (test-kit), deferred-ops build (not now) | **Major** | PM/Architect |

**Rationale:** Phase 0 is ~a few days, all reversible, and removes the two most dangerous items with
one-line-to-small fixes (H1 is literally one `ClearCacheAsync()`; C2 is `[Authorize]` + a clamp).
Phase 1 addresses the confirmed silent-loss cluster; H2 is intentionally *verify-first* per your
decision, so it starts with a test and a DAPR-guarantee spike rather than committing to a fencing
design blind. Phases 2–3 are epic-sized and specced individually. The two Large capabilities and the
frozen-spec shard are deliberately isolated so they don't block the correctness/security work.

---

## 4. Detailed Change Proposals

Grouped by phase. Each CP lists the findings it closes, the decision it reflects, effort, and primary
files. **No code is changed by this proposal.**

### Phase 0 — Unblock & safe fixes (Minor, direct-to-dev)

**CP-1 — Restore green main / unblock release (C5).**
Fix `GET /openapi/v1.json` 500 (regression from Gateway extraction / OpenApi-target removal) and the
`Contracts_package_pins_commons_unique_ids...` package-pin test; institute a red-main freeze norm.
*Files:* `src/Hexalith.EventStore/OpenApi/*`, `Program.cs`, `tests/Hexalith.EventStore.Contracts.Tests/Packaging/*`. *Effort:* S–M.

**CP-2 — One-line data-corruption fix (H1).**
Add `StateManager.ClearCacheAsync()` at the top of `HandleInfrastructureFailureAsync` (the
concurrency-conflict path already does this at `AggregateActor.cs:469/1975`), so a Step-5 exception
cannot flush staged events together with a Rejected result. *Effort:* S. *Test:* throw from
`ShouldCreateSnapshotAsync` after staging; assert stream metadata unchanged.

**CP-3 — Close anonymous cross-tenant admin endpoints (C2).**
Replace `[AllowAnonymous]` on `AdminStreamQueryController`, `AdminTraceQueryController`,
`AdminCommandsQueryController` with `[Authorize]` + `GlobalAdministrator` (or the DaprInternal scheme
per CP-8); clamp `AdminCommandsQueryController` `count`; add `[RequestSizeLimit]` to admin
write/sandbox bodies. *Effort:* M. *Test:* unauthenticated and tenant-A-token → 401/403 for tenant B.

**CP-4 — Strip forgeable admin identity from committed config (C3).**
Remove `SigningKey`/`Username`/`Password`/`GlobalAdmin:true` from
`src/Hexalith.EventStore.Admin.UI/appsettings.json` (base); move dev-only values to
`appsettings.Development.json`; fail-fast at startup when the key/credentials are absent outside
Development. *Effort:* S.

**CP-5 — Production auth guards (H11).**
In `ValidateEventStoreAuthenticationOptions` (both gateway and Admin.Server.Host), fail startup when
`IsProduction()` and `Authority` is empty (symmetric-key mode), require `RequireHttpsMetadata==true`
in prod, and pin `TokenValidationParameters.ValidAlgorithms`. *Effort:* S.

**CP-6 — Admin tenant-filter parity + quick Lows (H12a + Lows).**
Add `AdminTenantAuthorizationFilter` to `AdminTenantsController`'s `{tenantId}`/`{tenantId}/users`
GETs and scope/gate `ListTenants` (the only tenant-parameterized admin controller missing the
filter). Bundle: gate admin Swagger outside Development; CLI `--confirm`/`--yes` on destructive
commands; replace `return;` with `Assert.Skip` in `DaprAccessControlE2ETests`; port the gateway's
ULID-safe `CorrelationIdMiddleware` to `Admin.Server.Host` (removes the R2-A7 `Guid.TryParse`
violation) and fix its test; fix the Kestrel 1 MB body-limit no-op (M13); correct the CLAUDE.md
test-baseline section (Server.Tests builds and runs blocking; IntegrationTests runs in no CI job).
*Effort:* S each.

### Phase 1 — Pipeline correctness (Moderate)

**CP-7 — Resume/idempotency integrity (C1, H4, M4, status re-key).**
Store `MessageId`/`CausationId`/`CommandType` in `PipelineState`; resume only when it matches the
incoming command, else drain the orphan and process normally (C1). Record only terminal domain
outcomes in the idempotency store; give transient infra/conflict results a TTL or retryable flag, and
add TTL to all idempotency records (H4). Move tenant validation ahead of the idempotency read (M4).
Re-key command status/archive by `{tenant}:{messageId}`, keep correlation as an indexed field (server
#16). *Effort:* M. *Tests:* different command reusing a correlation id → domain service invoked +
orphan drained; same-MessageId retry after transient failure can still succeed.

**CP-8 — Defense-in-depth trust boundary (C4, H7, M14, part of H10).**
Require the DAPR app-api-token (or an mTLS/loopback-bound internal listener) before the
`DaprInternalAuthenticationHandler` mints `global_admin` (C4). Apply an app-layer credential check to
the SDK endpoint group (`/process`, `/query`, `/replay-state`, `/project`,
`/admin/operational-index-metadata`) and remove `IsGlobalAdmin` from the wire envelope in favor of a
gateway-verified claim (H7). Require the sidecar/pubsub caller identity on
`POST /projections/changed` (M14). *Effort:* M.

**CP-9 — Replay & dispatch determinism (H9, M6).**
Fix `TryResolveApplyMethod` (and the duplicate in `EventStoreProjection`): require a `.` boundary,
throw on multiple suffix candidates, register the FQN as a second dictionary key (H9). Route every
payload (de)serialization through one shared `JsonSerializerOptions` (promote
`DomainProcessorStateRehydrator.SerializerOptions`) so camelCase/case-sensitivity can't silently drop
properties across the command/rehydrate/project/pubsub paths (M6). *Effort:* S. *Test:* two events
where one type name suffixes the other → correct dispatch + ambiguity error.

**CP-10 — Crash-recovery of committed-but-unpublished events (H3).**
On actor activation (or a periodic sweep), detect a persisted pipeline record at
`EventsStored`/`EventsPublished` and complete publication or convert it to a drain record + reminder —
removing the "recovery requires the identical CorrelationId to be resubmitted" gap. *Effort:* M.

**CP-11 — Append durability: verify-first (H2) [your Decision 1].**
Add a LiveSidecar two-writer race test driving two `EventPersister` writers at the same stream key
against real Redis; confirm exactly one wins per sequence and the stream stays gapless. Concurrently,
verify what exception the Dapr actor-state transaction actually throws under conflict (the current
`catch (InvalidOperationException)` may never fire — making `MaxPersistenceConflictRetries` dead
code). **Decision on ETag fencing is deferred until this evidence exists.** *Effort:* M (test+spike).

**CP-12 — Config drift → tested posture becomes runtime posture (H10).**
Pass `pubSubComponentPath` from the AppHost so daprd loads the scoped/dead-letter `pubsub.yaml`
(today it loads a bare generated component); add tests asserting *what the AppHost passes to Aspire*;
add `keyPrefix` and `tenants` scope/ACL to the production `deploy/dapr/*` templates so local and prod
agree. *Effort:* S–M.

### Phase 2 — Cost & evolution (Major; specs required)

**CP-13 — Folded snapshots (H5).**
Make automatic snapshots store folded state (reuse the `/replay-state` reconstruction the manual path
already uses) instead of nesting the full history via `DomainServiceCurrentState`. Unifies the two
snapshot shapes under `SnapshotKey`. *Spec required.* *Effort:* M–L. *Test:* snapshot payload size
stays bounded as events grow.

**CP-14 — Projection delivery cost (H6 + projection sequence guard).**
Short-circuit when the checkpoint equals the head (one metadata read); deliver only the tail once
`/project` handlers are incremental; add a source-sequence guard to `EventReplayProjectionActor` so a
cross-replica out-of-order write can't regress projection state; add poller identity eviction. **Do
the sequence guard before the short-circuit** (the always-full-replay currently masks the race).
*Spec required.* *Effort:* M.

**CP-15 — GlobalPosition allocator sharding (M2) [your Decision 8 — frozen-spec renegotiation].**
Shard allocation per tenant (or per domain) to remove the single cluster-wide actor bottleneck/SPOF;
document GlobalPosition as gappy (burned on retry) and not strictly commit-ordered. **Requires
updating `spec-dapr-global-event-ordering.md` (frozen-after-approval).** *Effort:* M.

**CP-16 — Event schema versioning & upcasting (H8).**
Persist the kebab `IEventContract.EventType` + an explicit payload version (reuse `MessageType`) in
`EventMetadata`; add an `IEventUpcaster` chain in the rehydrator/replayer; keep CLR-name resolution as
legacy fallback. Also apply `AggregateIdentity`-component validation in `EventMetadata` (M20) and add
the `CancellationToken` seam to `IDomainProcessor`/`/query`/`/project` (M8) — both are public-contract
changes best done here. *Spec required.* *Effort:* L.

### Phase 3 — Delivery, admin, deploy (Moderate)

**CP-17 — Delivery contract + poison handling (M1, M3, M12) [your Decisions 2].**
Document the at-least-once/unordered subscriber contract (dedupe by MessageId, order by
SequenceNumber). Add max-retry/max-age → dead-letter and exponential backoff to the drain path (M3).
Bound the in-memory dedup set and return in-progress duplicates as retryable (M12). *Effort:* M.

**CP-18 — Admin plane authz, audit, and honesty (H12b/c, M16-hide) [your Decisions 6].**
Register the claims transformation that normalizes `tenants`/`permissions` on Admin.Server (so the
Operator role works and null tenant scope means *deny*, not all-tenant); emit structured audit
records (via `IAdminAuthContext.GetUserId()`) for every state-mutating admin action; hide/disable the
deferred backup/restore/import/compaction operations in the UI and return 501; extract the
triplicated admin API clients into a shared typed client. *Effort:* M (L for the shared client).

**CP-19 — Production deploy posture (H14, M17, M18) [your Decision 4 doc].**
Add secret-store components + `secretKeyRef` (replace `{env:...}` plaintext) (H14); register a
`ready`-tagged state-store health check and enable DAPR app health checks (M17); add resiliency `apps`
targets for the domain services the code assumes are covered (M18); document the crypto-shred boundary
(broker + snapshots inside it) and ensure retention + shredding procedures cover them (M5); default
images to an immutable (git-SHA) tag. *Effort:* M.

**CP-20 — Test & CI recovery (H13) [supports all phases].**
Get the infra-free subset of `IntegrationTests` into CI (or stand up the Aspire-in-CI host); fix the
rotted ULID middleware tests; retrofit state-store end-state assertions onto the "202 smoke test"
integration tests (extract `PubSubDeliveryProofTests`'s Redis read-back helpers into
`Testing.Integration`); delete/rewrite the fake-simulated 409 "integration" test; restore a
`workflow_dispatch` perf-lab workflow. *Effort:* M.

### Backlog epics (own specs; not scheduled now)

- **GDPR-1 — Aggregate erasure/tombstone** (M16) [Decision 5: backlog]. Crypto-shred + tombstone
  path, retention design, shred coverage of broker/snapshots (per Decision 4). *L.*
- **IAM-1 — Admin interactive OIDC login** (H12d) [Decision 7: deferred]. Authorization-code + PKCE,
  forward the end-user token, drop the ROPC/self-mint service identity. *L.*
- **KIT-1 — Aggregate test-kit** (H13d). `Given(events).When(command).Then(events)` fixture with
  replay-determinism/Apply-idempotency checks, in a package that depends only on Contracts/Client;
  split server-actor fakes so `Testing` drops its Server dependency. *M–L.*
- **Generator hardening** (M19 + existing `deferred-work.md` items) — folds into Epic D's backlog.

### Low-severity batch (fold into the phase that touches each file)

Domain-vs-tenant case-comparison inconsistency; `TopicOverrides` dropping the tenant prefix + reserve
`system` tenant name (Default decision); projection actor-id omitting Domain; empty-stream contract
inconsistency (`CurrentSequence==0`); `AggregateIdentity` per-access recompute; `ValidateModelFilter`
per-request reflection + double validation; gateway query triple-serialization + missing response cap;
reflection `Invoke` per replayed event + document the no-AOT stance (Default decision); Client NuGet
shipping without HTTP resilience; unsealed wire records / `object? CurrentState` duck-typing / public
`const MaxLength`; keycloak realm plaintext passwords + `sslRequired:none`; placement/scheduler
null-resolution + 120 s prerequisite probe; unconditional test-fault env plumbing; multi-domain
telemetry last-write-wins; blanket `CS86xx/CS0618` test NoWarn; permanently-red advisory CI lane + 25
never-driven ATDD skips.

---

## 5. Implementation Handoff

**Change classification: Major.** Routing is mixed by phase:

- **Phase 0 → Developer, direct.** Finalized CP-1..CP-6. Deliverable: green main + the merged safe
  fixes. Can start immediately.
- **Phase 1 → Developer + Architect review.** CP-7..CP-12. Each becomes a story with the finding IDs
  and acceptance tests named above. CP-11 (H2) produces evidence that feeds an architecture decision
  on fencing.
- **Phase 2 → Architect (specs) → Developer.** CP-13, CP-14, CP-16 each need a `frozen-after-approval`
  spec before implementation. CP-15 needs the global-ordering spec renegotiated first.
- **Phase 3 → Developer + Ops.** CP-17..CP-20.
- **Backlog epics → PM/Architect** for scheduling (GDPR-1, IAM-1, KIT-1).

### Success Criteria

- Main is green and the release gate is unblocked (CP-1).
- No unauthenticated endpoint returns cross-tenant data; no wire-asserted admin flag is trusted
  (CP-3, CP-8); no committed secret grants admin (CP-4, CP-5).
- No path commits staged events on a rejection; resume cannot execute a different command; a
  same-MessageId retry after a transient failure can make progress (CP-2, CP-7).
- Event→Apply dispatch is boundary-safe and ambiguity-detecting; one `JsonSerializerOptions` governs
  all payloads (CP-9).
- The two-writer race test exists and passes, and the real Dapr conflict exception is known (CP-11).
- The pubsub/ACL/keyPrefix posture the tests assert is the posture the AppHost loads (CP-12).
- Snapshot size is bounded as events grow; projections don't full-replay when already current
  (CP-13, CP-14).
- Every state-mutating admin action is attributable to a user (CP-18).

### Follow-Ups

- Renegotiate `spec-dapr-global-event-ordering.md` before CP-15.
- Author specs for CP-13, CP-14, CP-16 (public-contract changes in CP-16 must precede package
  hardening).
- Track GDPR-1 / IAM-1 / KIT-1 in `sprint-status.yaml` as backlog epics.

---

## 6. Checklist Summary

- [x] Change trigger confirmed — 2026-07-04 architecture review (six audits; Criticals/Highs verified against source).
- [x] Artifacts loaded — project-context.md, sprint-status.yaml, implementation-artifacts (specs, deferred-work), prior proposals.
- [x] Impact assessed — Epic D orthogonal (not resequenced); eight new epics + three backlog epics proposed.
- [x] Design gates decided — 8 interactive + 2 defaults (recorded in §1).
- [x] Change proposals drafted — CP-1..CP-20 + backlog + Low batch, phased and scope-classified.
- [x] Handoff defined — Phase 0 direct-to-dev; Phases 1–3 dev/architect/ops; backlog to PM/Architect.
- [x] **Approved** 2026-07-04 by Administrator — Phase 0 authorized to route to a Developer.
- [N/A] Code changes — none this session by decision (proposal-only).
