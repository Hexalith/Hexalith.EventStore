---
stepsCompleted: ['step-01-document-discovery', 'step-02-prd-analysis', 'step-03-epic-coverage-validation', 'step-04-ux-alignment', 'step-05-epic-quality-review', 'step-06-final-assessment']
readinessStatus: 'READY'
findings: { critical: 0, major: 2, minor: 14, uxDeferrals: 2 }
assessmentInputs:
  prd: prd.md
  architecture: architecture.md
  epics: epics.md
  ux:
    pointer: ux.md
    canonical: ux-designs/ux-eventstore-2026-07-05/index.md
    contracts:
      - ux-designs/ux-eventstore-2026-07-05/DESIGN.md
      - ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-20
**Project:** eventstore

---

## 1. Document Inventory

Systematic discovery across `_bmad-output/planning-artifacts/`. All four required document types were located; no unresolved duplicates and no missing documents.

| Type | Canonical Source (used for assessment) | Notes |
|---|---|---|
| **PRD** | `prd.md` (50,545 B, 2026-07-20) | `prds/prd-eventstore-2026-07-05/` holds working artifacts only (`.memlog.md`, `reconcile-*.md`, `review-*.md`) — **no `index.md`**, not a sharded duplicate. Exact-match export archived 2026-07-05. |
| **Architecture** | `architecture.md` (65,958 B, 2026-07-20) | `architecture/architecture-eventstore-2026-07-05/` holds `.memlog.md` + `reviews/` only — **no `index.md`**, not a sharded duplicate. Export archived 2026-07-05. |
| **Epics & Stories** | `epics.md` (201,281 B, 2026-07-20) | Single whole document; no `epics/` folder → no duplicate. Most recently modified input. |
| **UX** | `ux-designs/ux-eventstore-2026-07-05/` (`index.md`, `DESIGN.md`, `EXPERIENCE.md`) | `ux.md` (1,101 B) is a deliberate **pointer stub** delegating to the sharded canonical source; precedence documented in both files (`DESIGN.md`/`EXPERIENCE.md` win on conflict). Resolved-by-design, not an unresolved duplicate. |

**Duplicate resolution history:** The 2026-07-05 duplicate PRD/architecture exports were dispositioned into `archive/2026-07-05-duplicate-document-exports/` (SHA-256 exact matches, no merge required). UX was normalized 2026-07-09 (top-level handoff superseded by the sharded source).

**Status:** ✅ Inventory confirmed by user. No blocking discovery issues.

---

## 2. PRD Analysis

**Source:** `prd.md` (status: final, updated 2026-07-20). This is a brownfield Phase-4 *readiness-recovery* PRD that owns FR/NFR truth and traceability; `epics.md` owns slicing/sequencing. It carries its own traceability tables (§11), OQ8 authority order (§1.1), MVP/non-goal boundaries (§9), and success metrics (§10).

### 2.1 Functional Requirements (FR1–FR37 — 37 total, contiguous)

| ID | Requirement (extracted) |
| --- | --- |
| FR1 | Domain modules must be domain-centric (aggregates, commands, events, projections, query handlers, validators, contracts); platform boilerplate supplied by EventStore libraries. |
| FR2 | Domain-service SDK with `AddEventStoreDomainService`/`UseEventStoreDomainService`/`MapEventStoreDomainService` → canonical host shape. |
| FR3 | SDK exposes canonical DAPR endpoints `/process`, `/replay-state`, `/query`, `/project`, `/admin/operational-index-metadata`. |
| FR4 | Domain query-handler seam (`IDomainQueryHandler`), discovery, dispatch, operational metadata, gateway query-type capture, handler-aware routing, end-to-end `QueryResponseMetadata` propagation (freshness/version/ETag/served-at/degraded/paging) with provenance classification (projection-backed/handler-computed/unknown); projection-backed responses preserve lossless lifecycle (`Current`/`Stale`/`Rebuilding`/`Degraded`/`Unavailable`/`LocalOnly`); no lifecycle inference from ETags. |
| FR5 | Generic persisted read-model lifecycle + write contracts: ETag-aware read/write, coordinated read-model + sequence/checkpoint erasure, detail/index batch writes with defined partial-failure/idempotency/ordering/flush/concurrency/DAPR/in-memory semantics. |
| FR6 | Reusable DataProtection-backed query cursor codec: scope validation, payload limits, tamper/key-rotation handling, caller-supplied purpose isolation. |
| FR7 | Async cancellation-aware projection-handler seam (multiple named projections, coordinated detail/index), generic domain-event consumer pipeline w/ dedup + endpoint mapping; tolerate duplicate/out-of-order events via real handler path; full rebuilds correct across paging. |
| FR8 | Aspire/telemetry/health extensions for domain modules incl. `AddEventStoreDomainModule`, convention telemetry, DAPR state-store health checks. |
| FR9 | Sample + Tenants domains adopt SDK seams; remove/reduce duplicated routers, projection actors, cursor codecs, state-store plumbing, telemetry, health checks, Aspire wiring. |
| FR10 | EventStore package set includes DomainService + ServiceDefaults as publishable; release publishes only manifest-governed set. |
| FR11 | REST source-generator contract seam: `ICommandContract`, `IQueryContract`, optional `RestRouteAttribute`, assembly-level `RestApiAttribute`. |
| FR12 | Generator discovers contracts, emits typed OpenAPI controllers delegating to `IEventStoreGatewayClient`, forwards query-metadata headers; test suite covers discovery/routing/diagnostics/output/headers/`304`/problem-detail; accepted commands emit absolute gateway-authoritative status `Location` (omit if absent/invalid). |
| FR13 | Generated controllers live in dedicated external API hosts, not interactive UI hosts; UI hosts consume client libraries directly. |
| FR14 | Sample proof: contracts-only Sample contracts library + external Sample API host; move shared contracts; prove generated query + command controllers through external host. |
| FR15 | Tenants proof: generated controllers to external Tenants API host; Tenants UI consumes client libs, no hand-written per-message controllers; freshness/version/ETag/paging via platform metadata path. |
| FR16 | Projection-changed transport adds additive metadata-rich detail path (optional group scope, bounded metadata, scoped SignalR groups, optional DAPR notification) preserving signal-only compatibility. |
| FR17 | Live DAPR sidecar tests tagged + removed from per-push release gate; run in dedicated integration workflow w/ warm-up + readiness retry. |
| FR18 | `DaprETagService` allows overridable actor request timeout, preserving production default. |
| FR19 | Root-declared submodules live under `references/`; solution/project/docs/Aspire/LLM paths resolve through that layout. |
| FR20 | Aspire Keycloak resource named `security` (Keycloak still the tech); fixtures/lookups updated. |
| FR21 | Cross-repo Hexalith deps use source project refs only when `UseHexalithProjectReferences=true` + source exists; unset/false → package refs everywhere (incl. Debug); every source-owned NuGet version declared in `Hexalith.Builds/.../Directory.Packages.props`, no local `PackageVersion`/override/fallback. |
| FR22 | Restore/build/test/pack/semantic-release commands assert package-reference mode; avoid packaging submodule projects. |
| FR23 | Persisted events get non-zero actor-allocated global positions; CloudEvent IDs use event `MessageId`; duplicate command replies preserve original result fields. |
| FR24 | Global-position allocation renegotiated toward per-tenant/domain sharding; frozen global-ordering spec updated before implementation. |
| FR25 | Workflows use shared Hexalith.Builds security gates via `@main`, SHA-pin third-party actions through shared workflows, define publish scope in `tools/release-packages.json`. |
| FR26 | Phase 0 safe fixes: clear staged state on infra failure, protect anonymous admin endpoints, strip committed admin secrets, enforce prod auth guards, tenant-filter parity, gate admin Swagger, destructive-CLI confirmation, ULID-safe admin correlation middleware, correct stale test-baseline docs. |
| FR27 | Idempotency/pipeline remediation: exact command identity for resume; EventStore-owned tenant-scoped durable admission contract (trusted versioned canonical-intent descriptor + fixed retention tier); reject live conflicting intent; non-retryable `idempotency_key_expired` for expired-key reuse before execution; separate replay-result retention from consumed-key evidence; never convert consumed/unavailable/corrupt/unsafe legacy state into fresh miss. (OQ8-governed; see §1.1.) |
| FR28 | Trust-boundary remediation: app-layer credentials for internal/domain-service/projection-notification/admin-computation endpoints; remove trust in wire-asserted admin flags. |
| FR29 | Replay/dispatch: event apply-method resolution boundary-safe + ambiguity-detecting; single shared `JsonSerializerOptions` path for command/rehydrate/project/pubsub serialization. |
| FR30 | Crash recovery detects committed-but-unpublished events and completes/drains/recovers them without resubmission under same correlation ID. |
| FR31 | Append durability starts with live-sidecar two-writer race test + DAPR conflict-exception spike before choosing optimistic-concurrency fencing design. |
| FR32 | Runtime topology remediation: AppHost-loaded DAPR pub/sub, ACL, key-prefix posture matches tests + production deploy templates. |
| FR33 | Cost/evolution: folded snapshots, reduced projection replay cost, projection sequence guards, event schema versioning/upcasting, event-metadata identity validation, cancellation-token seams on published processing/query/projection interfaces. |
| FR34 | Delivery/admin/deploy remediation: document at-least-once unordered delivery, poison/dead-letter handling, bounded in-memory dedup, normalized admin claims, audit every state-mutating admin action, hide deferred admin ops, OpenBao DAPR secret-store config, Secrets-API retrieval, restrict K8s Secrets to bootstrap only, readiness/app-health checks, restore IntegrationTests CI coverage. |
| FR35 | Backlog capability tracking: GDPR aggregate erasure/tombstoning, Admin OIDC login, aggregate test kit, REST generator hardening. |
| FR36 | Before a consuming module deletes local projection/query infra, EventStore produces owner-reviewed parity packet proving every capability via production paths, records approved runtime SHA, requires consumer checkout SHA match. |
| FR37 | Optional shared payload-protection engine package on `IEventPayloadProtectionService`: `pdenc-v2` format + byte-stable AAD, preserve `json+pdenc-v1`/`json-redacted`/legacy/snapshot read compat, `IPersonalDataPolicy`/`IErasureStateProvider` seams, reusable key-lifecycle/resilience, ≥1 integration-proven backend, dual-provider parity + rollback evidence before G5. |

**Total FRs: 37** (FR1–FR37, no gaps).

### 2.2 Non-Functional Requirements (NFR1–NFR19 — 19 total, contiguous)

| ID | Category | Requirement (extracted) |
| --- | --- | --- |
| NFR1 | Security (fail-closed) | Fail closed for public/internal/domain-service/projection-notification/admin surfaces; no reliance on network posture or caller admin flags. Only anonymous exception: `/health`,`/alive`,`/ready` (AD-16), support-safe. |
| NFR2 | Tenant isolation | Preserved across state keys, actor IDs, topics, admin queries, generated APIs, SignalR groups, deploy config; reject reserved `system` tenant name. |
| NFR3 | Auth (production) | Reject insecure symmetric-key unless break-glassed; require HTTPS metadata; pin accepted JWT algorithms. |
| NFR4 | Secrets hygiene | Committed config must not contain forgeable admin signing keys, credentials, bearer tokens, decoded JWT payloads, or secrets. |
| NFR5 | SignalR bounding | Detail metadata bounded + metadata-only; framework logs don't expose metadata values above Debug. |
| NFR6 | Delivery semantics | At-least-once unordered; dedup by `MessageId`, order only where `SequenceNumber` meaningful; proven through production dispatcher/handler/persistence/marker/checkpoint path. |
| NFR7 | No silent data loss | Guard/recover staged-state flushes, stale pipeline records, append races, committed-but-unpublished; prevent duplicate side effects across reservation/fencing/execution/recovery/expiry/compaction/restart/concurrent hosts; consumed key never becomes fresh work. |
| NFR8 | Bounded cost + lifecycle authority | Bounded snapshot/projection cost, avoid unnecessary full replay, expose freshness/version via query metadata (authoritative only for projection-backed provenance); paged rebuild == canonical replay, never overwrite complete live model with page-only. |
| NFR9 | Release reproducibility | Independent of local submodule checkout; Release uses package refs unless intentionally overridden. |
| NFR10 | CI separation | Separate deterministic release-gate tests from live-sidecar/integration, preserving live-sidecar coverage in dedicated lane. |
| NFR11 | Manifest publishing | Manifest-driven; no submodule packages or packages outside EventStore release inventory. |
| NFR12 | Backward compat | Preserved for additive changes (signal-only projection notifications, existing generic gateway APIs). |
| NFR13 | Generated-code quality | Builds clean under warnings-as-errors; follows style/nullable/ULID/`ConfigureAwait(false)` rules. |
| NFR14 | UI controller boundary | Interactive UI hosts expose no generated/hand-written per-message MVC controllers; UI uses client libraries. |
| NFR15 | Admin honesty | No deferred backup/restore/import/compaction presented as functional; unavailable ops hidden/disabled or return `501`. |
| NFR16 | Persisted-evidence testing | Integration/higher-tier tests assert persisted state-store/read-model/end-state (not just status/mock counts); erasure/batch-recovery/idempotency/rebuild need persisted detail/index/marker/lifecycle/checkpoint evidence; durable-admission proves restart survival, multi-host serialization, inclusive expiry, atomic tombstone compaction, leakage constraints, zero downstream execution. |
| NFR17 | OpenBao hardening | Canonical DAPR `openbao` for prod operational + application secrets; `secretKeyRef` w/ `auth.secretStore: openbao`; app uses Secrets API; per-app default-deny; K8s Secrets only for bootstrap fallback; app-health, readiness-tagged health, resiliency targets, immutable image tags, crypto-shred boundaries. |
| NFR18 | AOT/trimming | Explicitly NOT a target while reflection conventions load-bearing; constraint documented. |
| NFR19 | Payload protection | Fail closed, byte-stable versioned crypto; deleted/missing/denied/unavailable/malformed/tampered/opaque → bounded typed outcomes; zero key material, invalidate caches on lifecycle change; dev-only backends not prod proof; rollout/historical/downgrade/rollback integration-tested. |

**Total NFRs: 19** (NFR1–NFR19, no gaps).

### 2.3 Additional Requirements, Constraints & Guardrails

- **§1.1 OQ8 authority order:** For Story 4.8, the 2026-07-20 OQ8 proposal + Architecture/Security/Test-approved OQ8 design v1.0.0 (SHA-256 `1a55b030…f08dcd8`) govern; pre-change FR27/NFR7/NFR16/architecture wording is historical only.
- **§8.1 Repo/build:** `.slnx` only; per-project tests (no solution `dotnet test`); Builds catalog owns versions; explicit `UseHexalithProjectReferences=true` for source intent; SDK container support (no Dockerfiles); single immutable OCI index for exactly `linux/amd64`+`linux/arm64` (fail closed otherwise); no nested submodules.
- **§8.2 Identity/authz:** ULID-safe IDs; `Guid.TryParse` forbidden for message/correlation/causation/aggregate IDs; tenant validated before disclosure; app-layer creds for internal/admin endpoints.
- **§8.3 UI governance:** FrontComposer + Fluent UI V5 only; no theme-primitive redefinition; `FluentAccordion` for multi-section; support-safe rendering; Sample UI submission ≠ downstream proof; Tenants UI preserves projection-confirmed success; Admin hides/disables deferred (else `501`).
- **§9 MVP boundary:** 7 Phase-4 epics in scope; Epic 8 (payload-protection/G5) is committed **post-MVP** (does not block MVP but Story 8.2 gates Parties Story 8.7); GDPR full tombstoning, Admin OIDC, test-kit, REST-hardening are backlog-only; AOT out of scope.
- **§10 Success metrics:** SM1–SM7 (primary SM1–SM3), counter-metrics SM-C1–SM-C3 (don't over-consolidate stories; don't count smoke as integration evidence; don't satisfy UI readiness by intent only).

### 2.4 PRD Completeness Assessment (initial)

- ✅ **Strong internal traceability:** PRD ships its own FR→Epic table (§11.1) and high-risk NFR→story table (§11.2). This is unusually complete — **but these are PRD *claims* to be verified against `epics.md`, not accepted as-is.**
- ✅ **Contiguous numbering:** FR1–FR37 and NFR1–NFR19 with no gaps; each maps to a named epic in §11.1.
- ⚠️ **Verification targets for later steps:** (a) confirm §11.1 FR→epic claims actually resolve to real epics/stories in `epics.md`; (b) confirm §11.2 high-risk NFR story IDs (e.g., 5.2/5.3/5.5, 4.1/4.2/4.4/4.5/4.8, 3.11/3.12, 7.6–7.9) exist and cover the stated NFR; (c) confirm the §11.3 story-renumbering (2026-07-15 migration) is fully reflected in `epics.md`; (d) confirm Epic 8 post-MVP classification is explicit in `epics.md`; (e) confirm Story 4.8 OQ8 authority (§1.1) is represented.
- ✅ **Scope discipline:** Explicit non-goals + counter-metrics guard against silent scope creep and evidence-quality erosion.

---

## 3. Epic Coverage Validation

**Method:** Triangulated three independent maps — (a) PRD §11.1 FR→Epic table, (b) `epics.md` "FR Coverage Map" (line 295), (c) `epics.md` per-epic "**FRs covered**" declarations — and confirmed every referenced epic exists as a real `## Epic N` section (Epics 1–8 all present).

### 3.1 Coverage Matrix (FR → Epic)

| FR | PRD §11.1 says | epics FR-Map says | Epic "FRs covered" list | Epic header exists | Status |
| --- | --- | --- | --- | --- | --- |
| FR1–FR10 | Epic 1 | Epic 1 | Epic 1 (FR1–10) | ✅ | ✅ Covered |
| FR11–FR16 | Epic 2 | Epic 2 | Epic 2 (FR11–16) | ✅ | ✅ Covered |
| FR17–FR22 | Epic 3 | Epic 3 | Epic 3 (FR17–22) | ✅ | ✅ Covered |
| FR23 | Epic 4 | Epic 4 | Epic 4 | ✅ | ✅ Covered |
| FR24 | Epic 4 | Epic 4 | Epic 4 | ✅ | ✅ Covered |
| FR25 | Epic 3 | Epic 3 | Epic 3 | ✅ | ✅ Covered |
| FR26 | Epic 5 | Epic 5 | Epic 5 | ✅ | ✅ Covered |
| FR27 | Epic 4 | Epic 4 | Epic 4 | ✅ | ✅ Covered |
| FR28 | Epic 5 | Epic 5 | Epic 5 | ✅ | ✅ Covered |
| FR29 | Epic 4 | Epic 4 | Epic 4 | ✅ | ✅ Covered |
| FR30 | Epic 4 | Epic 4 | Epic 4 | ✅ | ✅ Covered |
| FR31 | Epic 4 | Epic 4 | Epic 4 | ✅ | ✅ Covered |
| FR32 | Epic 5 | Epic 5 | Epic 5 | ✅ | ✅ Covered |
| FR33 | Epic 6 | Epic 6 | Epic 6 | ✅ | ✅ Covered |
| FR34 | Epic 7 | Epic 7 | Epic 7 | ✅ | ✅ Covered |
| FR35 | Epic 7 | Epic 7 | Epic 7 | ✅ | ✅ Covered |
| FR36 | Epic 1 | Epic 1 | Epic 1 (FR36) | ✅ | ✅ Covered |
| FR37 | Epic 8 (post-MVP) | Epic 8 | Epic 8 (FR37) | ✅ | ✅ Covered |

**Reverse check (FRs in epics but not in PRD):** None. Both documents enumerate exactly FR1–FR37. No orphan FRs, no double-primary-mapping.

### 3.2 Missing Requirements

**None at epic level.** Every PRD FR (FR1–FR37) has a declared, self-consistent primary epic that exists in `epics.md`. Epic 8 (FR37) is correctly and explicitly labeled **Post-MVP Security Platform Capability** with the Story 8.1→8.2 gate and the "does not block MVP; Story 8.2 blocks Parties 8.7" note — matching PRD §9.3.

### 3.3 Watch-Items Carried to Story-Quality Step (NOT coverage gaps)

Two inventory restatements in `epics.md` are *narrower* than the PRD's authoritative FR text. The obligations are covered elsewhere, but story-level acceptance criteria must carry the fuller intent:

1. **FR12 — command-status `Location` clause:** The `epics.md` Requirements-Inventory line (174) omits the PRD's "absolute, gateway-authoritative `Location` URI (omit if absent/invalid)" clause. Per project record this is AD-17 / Story 2.6 territory — confirm Story 2.6 acceptance criteria carry it.
2. **FR34 — secret-store specificity:** `epics.md` inventory line (218) says the generic "add secret-store-backed configuration," while PRD FR34 + NFR17 mandate the **canonical OpenBao** component, Secrets-API retrieval, and default-deny. Confirm Epic 7 stories (7.6–7.9 per PRD §11.2) carry the OpenBao specifics rather than a generic secret store.

These are traceability-fidelity notes for later steps, not epic-coverage failures.

### 3.4 Coverage Statistics

- **Total PRD FRs:** 37
- **FRs covered by a valid epic:** 37
- **Epic-level FR coverage:** **100%**
- **Orphan FRs (in epics, not PRD):** 0
- **Multi-primary-mapped FRs:** 0
- **Post-MVP-classified FRs:** 1 (FR37 → Epic 8, correctly gated)

> Note: This step validates **epic-level** FR coverage only. Story-level completeness, NFR high-risk story coverage, and story quality are validated in later steps.

---

## 4. UX Alignment Assessment

### 4.1 UX Document Status

✅ **Found.** Canonical sharded source `ux-designs/ux-eventstore-2026-07-05/` (`index.md` + `DESIGN.md` visual contract + `EXPERIENCE.md` behavior spine), with `ux.md` as the pointer handoff. Status: final. Both PRD (§3.3, glossary) and `epics.md` (line 293) reference it as the authoritative UI-governance artifact; PRD deliberately keeps only UI-*facing* requirements and delegates detailed UX here.

### 4.2 UX ↔ PRD Alignment — ✅ Strong

- **`EXPERIENCE.md` ships its own Source Traceability table** mapping each UX pattern to PRD requirements and architecture decisions: Projection-Confirmed Success, Support-Safe State, **NFR14**/AD-4, **NFR15/FR34**/AD-10, AD-8, AD-14, **AD-15**, **FR36**/AD-19/AD-20, FrontComposer/Fluent-V5 governance, and accessibility/localization.
- **UI-facing FRs are honored:** FR13/NFR14 (UI hosts consume client libs, no per-message MVC controllers) → Foundation + Component Patterns; FR15 (Tenants projection-confirmed) → Flow 6; FR34/NFR15 (deferred ops hidden/`501`) → Deferred & Backlog tab + Flow 4; FR4/FR36 six-state lifecycle (`Current`/`Stale`/`Rebuilding`/`Degraded`/`Unavailable`/`LocalOnly` + fail-safe `Unknown`) → Projection freshness indicator.
- **No orphan UX requirements:** every UX behavior traces to a PRD requirement or an adopted architecture decision. The 2026-07-11 Parties parity correction (six-state lifecycle) is reflected consistently in PRD FR4, the UX indicator, and the architecture lifecycle.

### 4.3 UX ↔ Architecture Alignment — ✅ Strong

- Every architecture decision the UX cites **exists and is ADOPTED**: AD-4, AD-8, AD-10, AD-14, AD-15, AD-16, AD-19, AD-20, AD-21 (verified — no dangling cross-refs).
- Architecture provides the concrete supports the UX depends on: **AD-15** (route provenance gates `Current`/`Stale` rendering; ETag is an opaque validator, never projection evidence), **six-state projection lifecycle** preserved without collapsing to a stale Boolean, **AD-16** (`/health`,`/alive`,`/ready` anonymous + support-safe — the sole fail-closed exception), **AD-21** (single consolidated `Admin.UI` / `eventstore-admin-ui`, module `event-store-admin`), and the **FrontComposer Shell/Contracts.UI → Fluent UI Blazor V5** dependency chain pinned to the Builds catalog `HexalithFrontComposerVersion` `4.0.1` (+ Fluent `5.0.0-rc.4-26180.1`).
- **Deferred capabilities are rendered honestly, not faked:** snapshots (Epic 6, spec-gated), payload protection (Epic 8, post-MVP), GDPR erasure / Admin OIDC / test-kit / generator-hardening (backlog) all surface as disabled/hidden/`501` in the UX — architecture and UX agree the underlying capability need not exist for the UI to be support-safe.

### 4.4 Alignment Issues

**None material.** UX, PRD, and architecture are mutually consistent on the load-bearing rules (fail-closed, tenant isolation, projection-confirmed success, support-safe rendering, provenance-gated lifecycle, deferred-operation honesty, FrontComposer/Fluent governance).

### 4.5 Warnings (documented deferrals, not blocking gaps)

- ⚠️ **No quantitative UI performance budget.** Architecture explicitly defers numeric EventStore-UI performance gates ("No production baseline supports a numerical release gate yet"; a future UX-performance backlog item may set measured budgets). The PRD sets no numeric UI-latency NFR either (NFR8 governs snapshot/projection cost, not UI latency). This is **consistent across all three artifacts** — an accepted, documented deferral, not a misalignment. Flag only so a future perf backlog item is not forgotten.
- ⚠️ **UX-surfaced-but-deferred capabilities need "disabled/`501`" build evidence.** Because several UX surfaces (Storage & Snapshots, protected-payload redaction, Deferred & Backlog) front capabilities that are spec-gated/post-MVP/backlog, the story-quality and NFR steps must confirm the *deferred rendering path* (hidden/disabled/`501`, per NFR15/AD-10) is actually implemented and tested where the underlying capability is not yet built. Carried forward. → **Confirmed present** in Story 7.4 (Honest Deferred Admin Operations) and Story 7.14 in the Epic Quality Review (§5).

---

## 5. Epic Quality Review

**Method:** All 87 stories across all 8 epics were reviewed against create-epics-and-stories standards (user value, epic independence, dependency direction, story sizing, AC testability, FR traceability) via four parallel per-epic reviews, calibrated to this brownfield developer-platform + ops-hardening product (operators/maintainers are first-class users) and to the plan's own Execution-Gates authority (gate-authorized sequencing is not a violation). Candidate-Critical findings were re-verified against source before acceptance (per the false-positive-CRITICAL rule).

### 5.1 Severity Summary

| Severity | Count | Items |
| --- | --- | --- |
| 🔴 Critical | **0** | (one Epic-1 candidate verified and downgraded — see 5.2) |
| 🟠 Major | **2** | Story 1.20 cross-epic pin (traceability/self-consistency); Stories 2.6/2.11 double-owned AC |
| 🟡 Minor | **~11** | sizing/metadata/AC-polish across Epics 1, 3, 4, 6, 7 |

**Baseline verdict: the plan is implementation-ready on structure.** 100% FR traceability (87/87 stories name their FR/NFRs), pervasive Given/When/Then ACs with error/edge + fail-closed coverage, correct dependency direction throughout (every inter-story link is later-depends-on-earlier or gate-authorized), strong anti-vacuous-test guardrails ("mock call counts alone do not close the story"; NFR16 persisted-state evidence), and explicit scope exclusions.

### 5.2 🟠 Major Findings

**MAJ-1 — Story 1.20 cross-epic runtime-pin dependency is not traceable from the Execution-Gates authority, and its header contradicts its own AC.**
- Story 1.20 header (line 1101): *"**Epic 1 cannot reach `done` until Story 3.12 delivers a conforming release** … This forward dependency is deliberate."* This couples Epic 1 completion to a **later epic** (Epic 3 / Story 3.12).
- **Verified as legitimate in substance:** recording a *deployed image digest* runtime identity genuinely requires the conforming two-platform container that Story 3.12 produces — a physical necessity, declared and reasoned, not a hidden/accidental forward dependency. Hence **downgraded from Critical to Major.**
- **The real defects (both fixable, documentation-level):**
  1. **Traceability gap:** the Execution-Gates section (lines 54–147) enumerates the 1.13→1.14-1.19→1.20 parity chain but **omits** the 1.20→3.12 pin; the disposition lives only in the separate 2026-07-20 correct-course proposal, so the dependency is not self-evident from the gate authority.
  2. **Header/AC contradiction:** the unconditional header conflicts with AC line 1131 (*"…deployed EventStore image digest **where applicable**"*) and line 1135 (source/package modes close independently). As written a reader cannot tell whether Epic 1 can close in source/package mode without 3.12.
- **Remediation:** (a) add a "Cross-Epic Runtime-Pin Gate" entry to the Execution-Gates section naming the 1.20→3.12 dependency (mirroring the Parties gate); (b) scope the header note to **deployed mode only** ("Epic 1 cannot reach `done` *in deployed mode* until Story 3.12…"), consistent with the "where applicable" AC.

**MAJ-2 — Stories 2.6 and 2.11 double-own the same Tenants-UI provenance-rendering acceptance criterion.**
- 2.6 (lines 1319–1322) and 2.11 (lines 1488–1491) both assert the "render `Unknown` for handler-computed/missing/invalid provenance; never derive lifecycle from ETag/HTTP/payload/SignalR" rule, reviewed by **different owners** (2.6: Sally + Tenants maintainer; 2.11: consumer-test owner) against **different proof bars** (2.11 additionally forbids mock-only closure and requires the real-gateway path).
- Not a dependency/gate violation (both consume the earlier Story 1.2 contract, both gate-authorized) — but the verbatim-overlapping DoD creates "which story governs / which owner signs off" ambiguity and double-review/rework risk.
- **Remediation:** scope 2.6 to client-library alignment + UX evidence-state conformance; delegate provenance-preservation-and-proof exclusively to 2.11, cross-referencing rather than restating.

### 5.3 🟡 Minor Findings (polish; none blocking)

| ID | Story | Observation | Remediation |
| --- | --- | --- | --- |
| MIN-1 | 1.2 | Largest slice in Epic 1 (10 ACs, 4 layers). Gate-authorized as single-owner contract; each AC independently testable — a review-risk concentration, not a defect. | Track internally as two review boundaries (routing/dispatch vs metadata-merge/provenance); no identifier re-split. |
| MIN-2 | 1.6 | Post-split identifier reuse (former parent "1.6→1.8-1.11" vs active Story 1.6); crosswalk-dispositioned. | Add a one-line pointer from Story 1.6 to `story-id-migration-2026-07-15.md`. |
| MIN-3 | 1.7 | Health-check AC asserts positive case only. | Add a negative/unreachable-state-store probe AC. |
| MIN-4 | 1.14–1.20 | Newly split parity children omit the per-story `Owner / review boundary` + `Focused validation` blocks that 1.3-1.5/1.8-1.11 carry. | Add per-story owner/validation lines for parity. |
| MIN-5 | 3.4 | Aspire security rename is happy-path-weighted (no failure AC). | Add a "no stale `keycloak` name resolves anywhere" scan AC. |
| MIN-6 | 3.5 | Heaviest story in Epics 2–3 (11 AC blocks); cohesive + authorized. | If it stalls in review, cleave mode-selection vs catalog-migration vs governance-scan. |
| MIN-7 | 3.11 / 3.12 | Large but cohesive; 3.12 size already dispositioned 2026-07-20. | No action required. |
| MIN-8 | 4.2 | Enforces cross-tenant isolation but names only FR27 (sibling 4.8 names NFR7/NFR16 for the same surface). | Add the isolation NFR to 4.2's trace line (cosmetic; 4.2 already done). |
| MIN-9 | 4.6 | Bundles global-ordering spec renegotiation + sharding implementation in one story, diverging from Epic 6's spec/impl split pattern (not on the 2026-07-15 oversized list). | Confirm the sharding slice is small, or split spec from allocation change. |
| MIN-10 | 4.8 | ACs are extraordinarily compound (OQ8-governed multi-slice; decomposition deferred to its dedicated story file). | Ensure the dedicated story-file task/coverage map (FR27/NFR7/NFR16) exists before the dev loop consumes it. |
| MIN-11 | Epic 6 | Epic intro promises "explicit global-position scaling," but no 6.1–6.6 AC delivers it (all trace to FR33 only). | Add a covering AC or strike the phrase so narrative matches deliverable scope. |
| MIN-12 | 6.6 | Bundles a cross-cutting **breaking public-contract** change (cancellation-token seams on `IDomainProcessor`/`/query`/`/project`) onto the versioning implementation. | Track the cancellation seam as an explicit sub-slice in the 6.5 spec / 6.6 review boundary (or split). |
| MIN-13 | 7.4 | ACs say "each/every enumerated deferred operation" but scope text adds "and other deferred capabilities" — "enumerated" left ambiguous (AC1 does derive from route inventory). | Point the AC at the authoritative deferred-operation route-scan inventory as the closed source of truth. |
| MIN-14 | 7.6 | AC7 requires a real DAPR-to-OpenBao integration read; needs a documented CI lane home. | Confirm the lane has an owned CI home consistent with Story 7.10's lane taxonomy. |

### 5.4 Best-Practices Compliance (per epic)

| Epic | User value | Independent* | Stories sized | No forbidden fwd-dep | Clear/testable ACs | FR traceability |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | ✅ | ⚠️ (MAJ-1 deployed-mode pin to 3.12) | ✅ (MIN-1 watch) | ✅ (MAJ-1 is declared+physical) | ✅ | ✅ 20/20 |
| 2 | ✅ | ✅ | ✅ | ✅ | ⚠️ (MAJ-2 overlap) | ✅ |
| 3 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| 4 | ✅ | ✅ | ✅ (MIN-9/10 watch) | ✅ | ✅ | ✅ |
| 5 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| 6 | ✅ | ✅ | ✅ (MIN-12 watch) | ✅ (spec-gated backward) | ✅ | ✅ (MIN-11 narrative) |
| 7 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| 8 | ✅ (post-MVP) | ✅ | ✅ (honest sizing note) | ✅ (8.1→8.2 backward) | ✅ | ✅ |

\* *Independence is assessed for a platform product: gate-authorized backward/spec-gated dependencies are compliant; only MAJ-1's undocumented cross-epic pin is flagged.*

### 5.5 Structural Strengths (notable)

- **Self-governing plan:** the 2026-07-15 correction already split all eight oversized parents into focused children (one owner, one review boundary, deterministic ACs) — directly satisfying SM4.
- **Anti-vacuous-test discipline** repeated across stories (1.3/1.9/1.15/4.8/5.8): mock-only proof cannot close a story; NFR16 persisted-state evidence is enforced in ACs, not deferred.
- **Forward-dependency avoidance is explicit** — Story 2.7 states "Story 1.20 authorization is not a prerequisite for review of this pre-authorization correction," inverting what would otherwise be a forward dependency.
- **Cross-repo changes fail closed** on missing maintainer approval + exact SHA (1.9/1.10/1.20/2.12/3.12) — mature brownfield governance.
- **Deferred-operation honesty (NFR15/AD-10)** and **spec-gating** (Epic 6, Epic 8) are encoded in ACs with exact output paths; Story 8.1's spec and the four Epic-7 backlog artifacts already exist on disk.

---

## 6. Summary and Recommendations

### 6.1 Overall Readiness Status

## ✅ READY — with a short documentation punch-list (2 Major, non-blocking-for-start)

The Phase-4 planning package (PRD + Architecture + UX + Epics/Stories) is complete, internally consistent, and traceable. The original blocker that motivated this recovery PRD — *"no standalone PRD exists for PRD-to-epic traceability"* (SM1) — is closed. **Zero Critical structural defects survived verification.** The two Major findings are documentation/self-consistency issues, not plan-blocking gaps: they do not create an un-completable story, a hidden forward dependency, or a coverage hole. Broad Phase-4 execution may begin; the punch-list should be cleared early in execution.

### 6.2 Success-Metric Scorecard (PRD §10)

| Metric | What it validates | Status |
| --- | --- | --- |
| **SM1** | Readiness re-run no longer reports missing PRD | ✅ PASS — `prd.md` final; FR1-FR36 + NFR1-NFR18 traceable, FR37/NFR19 gated |
| **SM2** | Every FR1-FR37 maps to ≥1 epic/story; Epic 8 post-MVP | ✅ PASS — 100% epic coverage; Epic 8 explicitly post-MVP |
| **SM3** | High-risk NFRs (NFR1-4, 7, 10-11, 14-17) map to concrete stories | ✅ PASS — every §11.2 story ID resolves to a real story (0 dangling) |
| **SM4** | Oversized stories split or explicitly accepted | ✅ PASS — 8 parents split into focused children (2026-07-15) |
| **SM5** | Architecture + UX artifacts exist and referenced by epic plan | ✅ PASS — both exist, bidirectionally referenced |
| **SM6** | Parity packet owner-approved `available` before Parties 8.6 | ⏳ GATED (execution) — Story 1.20 closure; correctly built into the plan |
| **SM7** | G5 packet approved before Parties 8.7 | ⏳ GATED (execution) — Epic 8 post-MVP (8.1→8.2); not a planning defect |

> SM1–SM5 are **planning-readiness** metrics — all PASS. SM6/SM7 are **execution-gate** metrics that are correctly deferred to their in-plan gates and are out of scope for planning readiness.

### 6.3 Issues Requiring Action (do not soften — these are real)

**Before or early in execution — Major (documentation/traceability):**
1. **MAJ-1 (Story 1.20):** Add the 1.20→3.12 cross-epic runtime-pin to the **Execution-Gates** section (it currently lives only in the 2026-07-20 correct-course proposal), and scope the header's "Epic 1 cannot reach `done` until 3.12" to **deployed mode only** to match the "where applicable" AC. *Without this, a reader cannot tell whether Epic 1 can close in source/package mode without Epic 3.*
2. **MAJ-2 (Stories 2.6 / 2.11):** De-duplicate the double-owned Tenants-UI provenance-rendering AC — scope 2.6 to client-library + UX evidence conformance; delegate provenance-preservation-and-proof exclusively to 2.11. *Without this, two owners sign off the same behavior against different proof bars.*

**No Critical issues.** The 14 Minor items in §5.3 are polish (AC negative-cases, per-story metadata parity, narrative-vs-scope alignment, breaking-change sub-slicing) — fix opportunistically; none blocks start.

### 6.4 Recommended Next Steps

1. **Clear MAJ-1 and MAJ-2 in `epics.md`** (small, surgical edits) — these are the only findings touching plan self-consistency; a `correct-course` pass is the right vehicle.
2. **Sweep the highest-value Minors before their stories are picked up:** MIN-3 (1.7 negative health AC), MIN-4 (1.14–1.20 owner/validation blocks), MIN-11 (Epic 6 "global-position scaling" narrative), MIN-12 (6.6 cancellation-token breaking-change sub-slice), MIN-13 (7.4 deferred-op enumeration source-of-truth).
3. **Preserve the execution gates as-is** — the parity chain (1.14-1.20), spec-gates (6.1→6.2, 6.3→6.4, 6.5→6.6), and payload-protection gate (8.1→8.2) are correct and should not be collapsed for speed (SM-C1).
4. **Hold the NFR16 line during dev:** integration/Tier-2+ ACs must assert persisted state-store/read-model/CloudEvent evidence, never 202/mock counts (SM-C2) — the plan already encodes this; enforce it in review.
5. **Watch the two documented UX deferrals** (§4.5): no quantitative UI performance budget (accepted), and confirm deferred-capability surfaces render disabled/`501` where the underlying capability isn't built yet.

### 6.5 Final Note

This assessment reviewed **all 87 stories across 8 epics** and evaluated **6 categories** (document inventory, PRD FR/NFR extraction, epic FR coverage, UX↔PRD↔Architecture alignment, epic/story quality, success metrics). It identified **16 quality findings (0 Critical, 2 Major, 14 Minor)** plus **2 documented UX deferrals** — a low defect density for a plan of this size, reflecting a mature, heavily-reviewed, self-governing artifact set. **The package is READY for Phase-4 implementation.** Address the two Major documentation items early; the Minor items are polish. You may proceed as-is or fold these corrections in first via `correct-course`.

### 6.6 Reconciliation With The Earlier Same-Date Assessment (important)

This report **supersedes the 4-line stub** previously at this path. The project record shows an **earlier 2026-07-20 readiness assessment that returned NOT READY, citing 16 findings / 5 Critical**, followed by a `correct-course` run (`sprint-change-proposal-2026-07-20.md`) that dispositioned part of that set. My verdict must be read against that history:

- **3 of the earlier 5 criticals were named and dispositioned** by the correct-course, and I independently confirm their current state:
  1. *FrontComposer `3.2.2` drift* → **fixed** (`architecture.md` was already correct at Builds-catalog `HexalithFrontComposerVersion` `4.0.1`; `epics.md` aligned). Confirmed consistent in §4.3.
  2. *1.20↔3.12 forward dependency* → the correct-course added the note to **Story 1.20** but **not** to the Execution-Gates section. That residual traceability gap is exactly my **MAJ-1** — so this item is *partially* closed, and I re-raise the remainder at Major.
  3. *Epic-sized stories 3.12 / 4.8 / 8.2* → accepted as cohesive/governed (no split). My reviewers concur (MIN-7, MIN-10, 8.2 honest sizing note).
- **2 of the earlier 5 criticals were never enumerated on disk** (open item A-1). **My independent full re-review of all 87 stories did not surface any Critical defect.** Two readings are possible: (a) those two were subsumed by items 1–3 / subsequently-landed edits, or (b) they were never substantiated.

**Therefore the READY verdict carries one explicit condition:** the originator of the earlier "5 Critical" assessment should confirm that the **2 unenumerated criticals are closed** (or identify them). If they were real and remain unaddressed, this verdict must be revisited. Absent that enumeration — and on the full evidence I *can* inspect — the package is READY. This closes readiness item **A-1** by making the gap explicit and actionable rather than latent.

---

**Assessment date:** 2026-07-20
**Assessor:** Product Manager (Implementation Readiness workflow)
**Inputs:** `prd.md`, `architecture.md`, `epics.md`, `ux-designs/ux-eventstore-2026-07-05/` (DESIGN.md + EXPERIENCE.md)
**Verdict:** ✅ READY (2 Major documentation corrections recommended; 0 Critical)
