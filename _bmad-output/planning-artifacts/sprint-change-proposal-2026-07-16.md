---
title: Sprint Change Proposal — Shared Payload-Protection Ownership And Parties G5 Parity
date: 2026-07-16
author: Administrator
workflow: bmad-correct-course
mode: batch
scope_classification: major
status: approved
approved: 2026-07-16T01:01:57+02:00
approved_by: Administrator
handoff_status: recorded
trigger: >
  Parties G5 routes a complete shared payload-protection engine to EventStore,
  but EventStore currently owns only provider-neutral hooks, typed unreadable
  outcomes, workflow contracts, and redaction. Resolve ownership, add a
  spec-gated implementation commitment, and preserve the Parties migration gate.
---

# Sprint Change Proposal — Shared Payload-Protection Ownership And Parties G5 Parity

## 1. Issue Summary

Parties Story 8.7 and the Story 8.3 G5 prerequisite matrix require a real shared
payload-protection engine. The required capability includes `pdenc-v2`, stable
authenticated-data binding, legacy reads, policy seams, key mechanics, a
production backend, compatibility proof, release provenance, and exercised
rollback.

EventStore does not currently make that delivery commitment. Historical Stories
22.7a-d delivered the prerequisites but deliberately stopped before a real
provider engine:

| G5 capability area | Current EventStore coverage |
| --- | --- |
| Provider-neutral hooks and protection metadata | Historical Story 22.7a |
| Typed unreadable outcomes | Historical Story 22.7b |
| Provider-neutral key-lifecycle workflow, restored-backup admission, and redaction/recovery contracts | Historical Stories 22.7c-d |
| Shared engine, `pdenc-v2`, policy seams, reusable key mechanics, and a production backend | No implementation story or release package |

The mismatch is visible in the authoritative artifacts:

- `prd.md` section 9.2 excludes full crypto-shredding from the Phase 4 MVP.
- `architecture.md` defers full crypto-shredding and contains no engine/package
  ownership decision.
- `epics.md` says crypto-shredding must remain backlog work and contains no G5
  implementation story.
- `docs/guides/payload-protection-and-crypto-shredding.md` correctly states that
  real providers, KMS/Key Vault integration, and DAPR secret-store integration
  remain deferred, but its historical scope wording can be read as though Stories
  22.7a-d collectively delivered more than hooks, outcomes, workflow contracts,
  and redaction.
- `tools/release-packages.json` contains 14 packages and no optional payload-
  protection engine package.
- Story 1.20 is exclusively the projection/query parity closure for Parties Story
  8.6. Its acceptance boundary does not include Parties G5 or Story 8.7.
- The approved Parties proposal
  `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11-eventstore-additive-api-prerequisite-routing.md`
  records Package B as owner routing only. The current Parties G5 row remains
  `needs-additive-api`.

### Trigger classification

This is a newly explicit cross-repository stakeholder requirement combined with
an ownership contradiction. It is not a defect in Story 1.20, and it does not
invalidate the completed provider-neutral work.

### Core problem statement

Parties assumes EventStore ownership of a reusable payload-protection engine,
while EventStore architecture currently assigns real cryptography and key
lifecycle to providers/operators and has no implementing story. Without a named
owner, security design gate, release artifact, production backend, and proof
packet, the routing request is not an implementable EventStore commitment and
cannot authorize Parties source migration or deletion.

## 2. Impact Analysis

### 2.1 Explicit ownership decision

**Decision: EventStore owns an optional shared payload-protection engine built on
its existing provider-neutral hooks.**

The package is provisionally named `Hexalith.EventStore.PayloadProtection`. The
security ADR may split an ADR-selected production adapter into a companion
package if dependency or credential boundaries require it, but it may not move
the stable engine contract back into Parties or silently count an interface-only
package as the production backend.

| Owner | Binding responsibility |
| --- | --- |
| Hexalith.EventStore | Stable `pdenc-v2` envelope and AAD encoding; backward readers; `IPersonalDataPolicy`, `IErasureStateProvider`, and policy-discovery seams; reusable data-key generation/storage/wrapping/rotation mechanics; audit/retry/circuit-breaker/cache-invalidation/key-zeroing behavior; backend contracts; at least one production-capable adapter; development-backend restrictions; goldens; package/release evidence; G5 proof packet. |
| Production provider/operator | Root-key and KEK custody; KMS/HSM/secret-store accounts and credentials; production availability and access policy; environment-specific rotation/retention configuration; incident and break-glass operation. |
| Hexalith.Parties | Party-specific commands, legal/retention policy, erasure orchestration semantics, certificates/reports, Art.20/Art.30 behavior, and UX/copy unless a separately approved ADR moves a named concern. Parties supplies dual-provider consumer parity and retains its rollback path until G5 closes. |

This resolves the historical contradiction by separating the **shared engine and
backend adapter code** owned by EventStore from the **production keys, credentials,
provider service, and operating policy** owned by the provider/operator.

### 2.2 Alternative ownership disposition

A separate shared-security module is not selected for this correction. EventStore
already owns the persistence hooks, protection metadata, unreadable taxonomy,
workflow contracts, redaction, replay/rebuild enforcement, and affected runtime
integration points. Moving the engine to a new module now would add a second
contract authority, a cross-repository release dependency, and likely invert or
duplicate those existing seams. A later extraction requires a new approved ADR
and a compatibility-preserving package migration.

### 2.3 Epic and story impact

- Existing Epics 1-7 remain viable and keep their current ordering.
- Story 1.20 remains projection/query-only and is neither enlarged nor delayed by
  G5.
- Add post-MVP **Epic 8: Shared Payload Protection**. It contains one security-
  design gate and one dedicated implementation story.
- Add Story 8.1, which produces and secures approval for the package/security
  specification before implementation starts.
- Add Story 8.2, **Shared Payload-Protection Engine And Parties G5 Parity**.
- No existing story becomes obsolete, is rolled back, or changes status.
- Parties Story 8.7 remains `backlog`, and its G5 matrix row remains
  `needs-additive-api`, until Story 8.2 is implemented, reviewed, released or
  pinned, and proven through consumer parity and rollback.

### 2.4 Artifact conflicts and required corrections

| Artifact | Impact |
| --- | --- |
| PRD | Add a committed post-MVP requirement and NFR; narrow the existing crypto-shredding exclusion without bringing aggregate/event deletion into Phase 4 MVP. |
| Architecture | Add the ownership/package/backend invariant and distinguish engine ownership from provider/operator key custody. |
| Epics | Add the security gate, Epic 8, Stories 8.1-8.2, and an explicit Story 1.20 exclusion. |
| Sprint status | Add Epic 8 and both stories as `backlog`; change no current status. |
| UX | No EventStore UX change. Parties UX/no-leak behavior remains consumer evidence. |
| Documentation | Correct the payload-protection guide's delivery boundary and remaining deferred work. |
| Release | Add the engine and any ADR-approved production-adapter package to the manifest only when implementation exists; update inventory governance and package-only consumer validation in the same story. |
| Testing | Add EventStore goldens, production-backend integration proof, Parties dual-provider tests, and a rollback rehearsal after `pdenc-v2` writes. |
| Evidence | Add a dedicated G5 proof packet containing exact source, package, backend, review, limitations, and rollback identity. |

### 2.5 Technical impact

The implementation affects public security contracts, persisted formats, JSON
property selection, event and snapshot protection, key lifecycle, provider
availability handling, telemetry, state/actor/reminder naming, DI/startup guards,
package inventory, release provenance, and cross-repository compatibility tests.

`pdenc-v2` is a durable data contract. Byte-stable AAD encoding, historical reads,
downgrade behavior, cache invalidation, key zeroing, and tamper classification
cannot be left as implementation details. The security ADR is therefore a hard
precondition rather than a task inside the coding phase.

### 2.6 Schedule and risk

- **Phase 4 MVP:** no delay; Epic 8 is a committed post-MVP capability.
- **Parties Epic 8:** Story 8.7 remains externally blocked until EventStore Story
  8.2 closes G5.
- **Security design effort:** medium, expected to require focused architecture,
  security, operations, and consumer review.
- **Implementation effort:** high and multi-sprint; a planning range of 4-8 weeks
  after ADR approval is reasonable for engine, one production backend, release,
  integration, dual-provider parity, and rollback proof. External provider setup
  or independent security assessment is additional.
- **Risk:** high because errors can make immutable history unreadable, weaken
  erasure guarantees, leak personal data, or make downgrade impossible.

## 3. Recommended Approach

Use a **hybrid direct adjustment and MVP-boundary clarification**:

1. Record EventStore ownership now in PRD, architecture, epics, and tracking.
2. Keep the engine outside the current Phase 4 MVP rather than silently expanding
   its release gate.
3. Require an approved security ADR/spec before implementation.
4. Implement the engine only in the dedicated Story 8.2, with at least one real
   production backend and no Parties-specific domain logic.
5. Release or pin exact artifacts and produce a dedicated G5 proof packet.
6. Keep Parties' G5 row `needs-additive-api` and retain the local provider until
   dual-provider parity and post-v2-write rollback succeed.

### Alternatives considered

#### Modify Story 1.20

**Not viable.** Story 1.20 closes projection/query parity for Parties Story 8.6.
Adding cryptographic formats, key lifecycle, backend integration, and security
review would destroy its acceptance boundary and provenance meaning.

#### Roll back Stories 22.7a-d

**Not viable.** Their hooks, typed outcomes, workflow contracts, and redaction are
valid prerequisites and compatibility surfaces for the shared engine.

#### Keep the work only in GDPR-1

**Not viable.** GDPR-1 is a planning artifact for aggregate erasure/tombstoning
and explicitly authorizes no runtime crypto-shredding implementation. It cannot
serve as the G5 delivery commitment.

#### Create a separate shared-security module now

**Not selected.** It introduces a second contract/release authority around seams
already owned by EventStore. It remains a future extraction option behind an ADR.

## 4. Detailed Change Proposals

These edits were reviewed as one batch. At review time, only this sprint-change
proposal had been written; the target artifacts were updated only after the user
approved the complete batch.

### 4.1 PRD changes

**Artifact:** `_bmad-output/planning-artifacts/prd.md`

#### Document purpose and traceability counts

**OLD:**

> The approved 2026-07-11 Parties parity correction adds FR36.

**NEW:**

> The approved 2026-07-11 Parties projection/query parity correction adds FR36.
> The approved 2026-07-16 payload-protection ownership correction adds FR37 and
> NFR19 as a committed post-MVP capability; it does not enlarge the Phase 4 MVP.

Update affected `FR1-FR36`/`NFR1-NFR18` traceability counts to
`FR1-FR37`/`NFR1-NFR19` where the document is describing the full committed
baseline. Keep Phase 4 MVP wording explicitly limited to Epics 1-7.

#### New functional requirement

**Section:** Add `6.9 Optional Shared Payload Protection` after section 6.8.

**OLD:** No requirement owns the real engine.

**NEW:**

> **FR37:** EventStore must provide an optional shared payload-protection engine
> package built on `IEventPayloadProtectionService` and the existing provider-
> neutral metadata/outcome/workflow contracts. The engine must implement the
> approved `pdenc-v2` format and byte-stable authenticated-data contract, preserve
> `json+pdenc-v1`, `json-redacted`, legacy-unprotected, and snapshot read
> compatibility, expose `IPersonalDataPolicy` and `IErasureStateProvider`
> extension seams, supply reusable key lifecycle and resilience mechanics behind
> shared contracts, include at least one integration-proven production backend,
> and produce EventStore-owner plus Parties dual-provider parity and rollback
> evidence before G5 is available.

**Done evidence:** approved security ADR; package/API inventory; production-
backend integration; owner goldens; Parties dual-provider compatibility; rollback
after `pdenc-v2` writes; exact source/package/backend provenance and approval.

#### New NFR

**Section:** Add to section 7.

**OLD:** No NFR binds the engine's durable cryptographic format and rollback.

**NEW:**

> **NFR19:** Payload protection must fail closed and preserve byte-stable,
> versioned cryptographic semantics. Deleted, missing, denied, unavailable,
> malformed, tampered, and opaque states remain bounded typed outcomes. Key
> material is zeroed when no longer needed; caches are invalidated on lifecycle
> changes; development-only backends cannot start as production proof; and
> rollout, historical reads, downgrade, and rollback after writing the newest
> format are integration-tested.

#### MVP boundary

**Section:** 9.2 Out Of Scope For MVP.

**OLD:**

> Full GDPR aggregate/event tombstoning, broker-history deletion, backup erasure,
> and crypto-shredding; backlog artifact only.

**NEW:**

> Full GDPR aggregate/event tombstoning, broker-history deletion, physical backup
> erasure, audit-record deletion, and provider/operator key-custody operations
> remain outside the Phase 4 MVP under GDPR-1. The optional shared payload-
> protection engine and Parties G5 parity are a committed post-MVP capability in
> Epic 8; Stories 22.7a-d supplied prerequisites, not that engine.

Add section `9.3 Committed Post-MVP Scope` naming Epic 8 and stating that it does
not block Phase 4 completion but does block Parties Story 8.7 migration.

#### Traceability

Add `FR37 -> Epic 8 - Shared payload protection` and
`NFR19 -> Stories 8.1-8.2`. Update success metrics so full committed traceability
includes FR37/NFR19 while Phase 4 readiness remains scoped to Epics 1-7.

**Rationale:** Resolve ownership without conflating a large security capability
with the current MVP or with GDPR aggregate deletion.

### 4.2 Architecture changes

**Artifact:** `_bmad-output/planning-artifacts/architecture.md`

#### New ownership invariant

**Section:** Add after AD-22.

**OLD:** No architecture decision owns the real engine.

**NEW:**

> ### AD-23 - EventStore Owns The Optional Shared Payload-Protection Engine [ADOPTED]
>
> - **Binds:** FR37, NFR1-NFR4, NFR7, NFR9-NFR12, NFR16-NFR17, NFR19
> - **Prevents:** consuming domains implementing reusable cryptography/key
>   lifecycle independently, or operators mistaking provider-neutral hooks or an
>   in-memory backend for production payload protection.
> - **Rule:** EventStore owns the optional `Hexalith.EventStore.PayloadProtection`
>   engine, stable `pdenc-v2`/AAD contract, backward readers, policy/erasure seams,
>   shared key mechanics, backend abstraction, production-backend conformance,
>   goldens, release provenance, and G5 proof. Provider/operators own production
>   root-key custody, credentials, KMS/HSM/secret-store service operation, and
>   environment policy. Parties retains domain legal policy and erasure semantics.
>   At least one ADR-selected non-development backend adapter must be implemented
>   and integration-proven; an interface, LocalDev, or in-memory implementation
>   cannot satisfy production proof.
>
> Implementation is blocked until
> `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`
> records security approval for package boundaries, canonical envelope/AAD bytes,
> read compatibility, policy seams, key/state/actor/reminder/metric names,
> backend restrictions, versioning, rollout, downgrade, and historical data.

#### Structural and capability maps

Add the optional project to the structural seed:

```text
src/Hexalith.EventStore.PayloadProtection/ # optional shared engine and approved backend boundary
```

Add `FR37 Shared payload protection` to the capability map, governed by AD-5,
AD-9-AD-13, AD-22, and AD-23. Update the release convention to state that the
manifest remains at 14 packages until implementation creates an approved packable
project; Story 8.2 then updates the manifest and all inventory/governance checks in
the same slice.

#### Deferred wording

**OLD:**

> Full aggregate/event GDPR tombstoning, broker-history deletion, backup erasure,
> crypto-shredding ... are backlog artifacts for Phase 4 MVP.

**NEW:**

> Full aggregate/event GDPR tombstoning, broker-history deletion, physical backup
> erasure, audit-record deletion, and provider/operator key-custody operations
> remain deferred from Phase 4 MVP. The optional EventStore-owned shared engine is
> a committed post-MVP Epic 8 and remains unavailable until its ADR, implementation,
> production backend, release, G5 proof, and consumer rollback complete.

**Rationale:** Establish one contract authority while preserving operator custody
and the MVP boundary.

### 4.3 Epic and story changes

**Artifact:** `_bmad-output/planning-artifacts/epics.md`

#### New execution gate

Add `Payload-Protection Security Gate` under Implementation Readiness Execution
Gates:

- Story 8.2 cannot start until Story 8.1's exact spec exists and records named
  architecture and security approval.
- No EventStore hook, Story 22.7 artifact, custom provider example, LocalDev
  backend, or interface-only backend counts as the G5 engine.
- Story 1.20 cannot classify G5 and does not block or authorize Story 8.2.
- Parties keeps G5 `needs-additive-api` and its local provider/DI rollback path
  until Story 8.2 proof is released/pinned and consumed successfully.

#### New epic

**OLD:** Epic list ends at Epic 7.

**NEW:**

> ## Epic 8: Shared Payload Protection
>
> **Epic type:** Post-MVP Security Platform Capability
>
> Platform security owners and domain modules can use an optional, reusable,
> production-proven payload-protection engine without duplicating cryptographic
> formats and key-lifecycle mechanics, while providers/operators retain key
> custody and domains retain legal policy.
>
> **FRs covered:** FR37
>
> **Sequencing note:** Story 8.1 is an approval gate for Story 8.2. Epic 8 does not
> block Phase 4 MVP, but Story 8.2 blocks Parties Story 8.7 migration.

#### Story 8.1

> ### Story 8.1: Shared Payload-Protection Security Spec And ADR
>
> **Requirements covered:** FR37, NFR19
> **Classification:** Security/architecture gate; no runtime implementation is
> authorized.
> **Owner / review boundary:** Winston (Architect) with a named Security Reviewer;
> the EventStore owner, Release owner, Operations owner, and Parties maintainer
> approve their boundaries.
>
> As a platform security owner,
> I want the shared payload-protection ownership and durable security contract
> approved before implementation,
> so the engine cannot make story-local choices that strand persisted history or
> weaken key custody.
>
> **Acceptance Criteria:**
>
> **Given** the EventStore-owned optional engine is proposed
> **When** the security specification is approved
> **Then** it fixes package and dependency boundaries, backend ownership, operator
> custody, Parties-retained policy, and whether an ADR-selected adapter requires a
> companion package
> **And** it does not treat an interface-only or development backend as production.
>
> **Given** `pdenc-v2` will persist durable data
> **When** the wire contract is frozen
> **Then** the canonical envelope, AES-GCM parameters, nonce/tag representation,
> algorithm identifiers, byte-stable AAD encoding, canonical property-path
> encoding, and format/version rules are test-vector ready
> **And** AAD binds tenant, domain, aggregate, event/snapshot type, property path,
> key version, and format, or records the explicitly approved equivalent.
>
> **Given** historical data must remain usable
> **When** compatibility is specified
> **Then** `json+pdenc-v1`, `json-redacted`, legacy-unprotected, existing metadata,
> and snapshot reads have explicit routing and typed failure behavior
> **And** rollout, mixed history, downgrade, and rollback-after-v2-write policies
> are defined.
>
> **Given** policy and key lifecycle are shared
> **When** contracts are frozen
> **Then** `IPersonalDataPolicy`, `IErasureStateProvider`, policy discovery
> including `PersonalDataAttribute` or an approved equivalent, key paths, state
> keys, actor/reminder names, metric names, audit fields, and versioning rules are
> exact
> **And** storage, wrapping, rotation, retry, circuit breaking, cache invalidation,
> and key zeroing responsibilities are assigned.
>
> **Given** a production backend is required
> **When** backend selection and restrictions are approved
> **Then** at least one non-development adapter, its integration environment,
> credentials/custody boundary, failure taxonomy, and conformance evidence are
> named
> **And** LocalDev/in-memory startup restrictions outside Development are explicit.
>
> **Given** Story 8.1 completes
> **When** its output is recorded
> **Then** the exact path is
> `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md`
> **And** it records approvers, date, accepted scope, rejected alternatives, open
> decisions, threat model, test vectors, migration posture, and explicit
> authorization for Story 8.2 to start.

#### Story 8.2

> ### Story 8.2: Shared Payload-Protection Engine And Parties G5 Parity
>
> **Requirements covered:** FR37, NFR1-NFR4, NFR7, NFR9-NFR12, NFR16-NFR17,
> NFR19
> **Owner / review boundary:** Amelia (Developer); named EventStore owner and
> Security Reviewer approve implementation; Murat reviews verification; Release
> owner approves artifact provenance; Parties maintainer approves consumer parity.
>
> As a platform security owner,
> I want an optional shared payload-protection engine built on EventStore's
> provider-neutral hooks,
> so domain modules can protect persisted payloads without implementing reusable
> cryptographic and key-lifecycle infrastructure.
>
> **Acceptance Criteria:**
>
> **Given** implementation preflight runs
> **When** Story 8.2 starts
> **Then** the approved Story 8.1 specification exists at the exact required path
> and explicitly authorizes implementation
> **And** code tasks cite the approved sections they satisfy.
>
> **Given** the optional engine is packaged
> **When** release/package validation runs
> **Then** `Hexalith.EventStore.PayloadProtection` and any ADR-approved companion
> adapter are packable, opt-in, centrally versioned, and manifest-governed
> **And** the package cannot replace the current no-op default without explicit DI
> configuration and production-safe option validation.
>
> **Given** a selected event property or snapshot value is written
> **When** `pdenc-v2` protection runs
> **Then** AES-GCM authenticated data binds tenant, domain, aggregate, event or
> snapshot type, canonical property path, key version, and format—or the exact
> approved equivalent—and matches byte-stable golden vectors
> **And** nonce reuse, unbounded input, path ambiguity, and cross-scope ciphertext
> substitution fail safely.
>
> **Given** existing history is read
> **When** the engine encounters `json+pdenc-v1`, `json-redacted`, legacy-
> unprotected data, current Story 22.7 metadata, protected snapshots, or mixed
> version streams
> **Then** every supported form remains readable according to the approved policy
> **And** unreadable history never silently downgrades to plaintext/unprotected.
>
> **Given** protection cannot return plaintext
> **When** a key is deleted, missing, denied, or unavailable, or metadata/ciphertext
> is malformed, tampered, opaque, or version-unknown
> **Then** those conditions remain separate bounded typed outcomes with explicit
> retry/permanence semantics
> **And** logs, traces, metrics, exceptions, ProblemDetails, evidence, exports,
> processing records, certificates, and reports pass no-leak scans.
>
> **Given** reusable key lifecycle is enabled
> **When** keys are created, stored, wrapped, unwrapped, rotated, cached,
> invalidated, erased, retried, audited, or denied
> **Then** generic behavior is supplied behind shared contracts with bounded retry
> and circuit-breaker behavior
> **And** cache invalidation and zeroing of plaintext key buffers are verified on
> success, failure, cancellation, rotation, and erasure paths.
>
> **Given** production proof is requested
> **When** backend conformance runs
> **Then** at least one ADR-selected, pluggable non-development backend is exercised
> against its real service boundary
> **And** LocalDev/in-memory implementations cannot satisfy production proof and
> fail startup outside their allowed environment.
>
> **Given** policy and erasure behavior vary by domain
> **When** `IPersonalDataPolicy`, `IErasureStateProvider`, and approved discovery
> metadata are used
> **Then** the engine remains domain-neutral while Parties can retain its legal
> policy, erasure orchestration, certificates/reports, and UX semantics
> **And** no Parties-specific rule enters EventStore.
>
> **Given** compatibility validation runs
> **When** EventStore owner goldens and Parties dual-provider tests execute
> **Then** protected/redacted/legacy reads, typed unreadable outcomes, key zeroing,
> no-leak diagnostics, Art.20 exports, Art.30 processing records, erasure reports/
> certificates, and persisted state pass through both retained Parties-local and
> shared-provider paths
> **And** HTTP-only, mock-only, or interface-shape evidence cannot close G5.
>
> **Given** rollback is rehearsed
> **When** the shared engine has written `pdenc-v2` data and the approved rollback
> procedure is executed
> **Then** retained software/configuration can read or safely route that history
> according to the ADR without data or metadata loss
> **And** switching DI before any v2 write is explicitly insufficient evidence.
>
> **Given** completion is requested
> **When** the G5 proof packet is reviewed
> **Then** it records exact EventStore source SHA, package IDs/versions/hashes,
> production-backend identity/version, test commands/results, persisted evidence,
> named reviewer approvals, limitations, historical-data policy, and rollback
> instructions
> **And** the packet decision is `available`; otherwise Story 8.2 and Epic 8 remain
> non-`done` and Parties G5 remains `needs-additive-api`.
>
> **Produces:**
> `_bmad-output/implementation-artifacts/8-2-shared-payload-protection-engine-and-parties-g5-parity-proof-packet.md`.

#### Story 1.20 boundary clarification

**OLD:** Story 1.20 acceptance criteria enumerate projection/query parity but do
not explicitly name G5 as excluded.

**NEW:** Add:

> **Explicit exclusion:** Story 1.20 closes the EventStore projection/query SDK
> prerequisite for Parties Story 8.6 only. It does not deliver or approve the G5
> payload-protection engine, `pdenc-v2`, key mechanics, production backend, or
> Parties Story 8.7 migration. Only Story 8.2's approved `available` proof packet
> may close G5.

**Rationale:** Prevent a broad “Parties parity” label from being misread as G5
delivery.

### 4.4 Sprint status changes

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:** No Epic 8 or G5 implementation story exists. Story 1.20 is
`ready-for-dev` for projection/query parity.

**NEW:** Preserve Story 1.20's current status and add its explicit G5-exclusion
comment. Add:

```yaml
  epic-8: backlog
  # Security/architecture gate; authorizes 8.2 but no runtime implementation.
  8-1-shared-payload-protection-security-spec-and-adr: backlog
  # Post-MVP G5 implementation; blocked until the approved 8.1 spec exists.
  8-2-shared-payload-protection-engine-and-parties-g5-parity: backlog
  epic-8-retrospective: optional
```

No current story or epic status advances because this proposal records ownership
and backlog commitment, not delivery.

### 4.5 Payload-protection guide changes

**Artifact:** `docs/guides/payload-protection-and-crypto-shredding.md`

#### Opening delivery boundary

**OLD:**

> Out of scope for this guide. Real encryption providers, key lifecycle ... are
> tracked in Stories 22.7b, 22.7c, and 22.7d. This page documents only the hook and
> metadata contract that those follow-up stories build on.

**NEW:**

> **Delivery boundary.** Stories 22.7a-d delivered provider-neutral protection
> hooks and metadata, typed unreadable outcomes, key-lifecycle/restore workflow
> contracts, and fail-closed redaction/recovery behavior. They did **not** deliver
> a real encryption engine, `pdenc-v2`, personal-data policy seams, reusable key
> storage/wrapping/rotation mechanics, KMS/HSM/secret-store integration, or a
> production backend. Those capabilities remain unavailable until post-MVP Epic
> 8 is implemented, released or pinned, and proven. Without explicit engine
> registration, the no-op provider remains the default.

#### Remaining deferred work

Rename `Deferred to Story 22.7d` to `Still deferred after Stories 22.7a-d` and
replace the real-provider bullet with a link to Epic 8 / Story 8.2. Keep physical
backup implementation, aggregate deletion, provider/operator credentials/key
custody, and jurisdiction/legal automation explicitly outside the engine story.

**Rationale:** State historical delivery accurately and remove the implication
that hooks/workflows equal a production engine.

### 4.6 Release, inventory, and proof changes

**Artifacts:** `tools/release-packages.json`, package governance tests,
`AGENTS.md`, package reference docs, release/package-only consumer validation.

**OLD:** 14 manifest packages; no payload-protection package.

**NEW:** Story 8.2 updates the manifest only after an approved project exists.
The exact inventory becomes 15 packages if the ADR approves one new package, or
the ADR-recorded count if a companion production adapter is required. All package
inventory statements, exact-output tests, dependency metadata checks, SBOM/
provenance evidence, and package-only consumer tests change atomically.

The dedicated proof packet is the only EventStore completion artifact allowed to
classify G5 `available`. Existing Story 1.20 proof and historical Story 22.7
evidence may be referenced as prerequisites but cannot substitute for it.

### 4.7 Parties tracking and consumer gate

**External artifacts:** Parties Story 8.3 prerequisite matrix, Story 8.7 spec,
and sprint status.

**OLD:** G5 is `needs-additive-api`; Story 8.7 is `backlog`; the retained local
provider is the rollback path.

**NEW:** Unchanged until Story 8.2 is implemented and proven. After EventStore
release/pin, Parties records the exact EventStore source/package/backend identity,
named approvals, dual-provider results, and post-v2-write rollback before moving
G5 to `available` or deleting local code.

No Parties repository or submodule is modified by this EventStore planning
workflow.

### 4.8 UX changes

**OLD:** Parties already owns erased, restricted, unavailable, export, processing-
record, certificate/report, and no-leak behavior.

**NEW:** No EventStore UX artifact change. Those behaviors become mandatory
consumer-compatibility evidence in Story 8.2 and remain Parties-owned unless a
separate ADR changes them.

## 5. Implementation Handoff

### Scope classification

**Major.** This change creates a new public optional security package, a durable
encrypted format, production backend integration, release inventory changes, and
cross-repository migration authority. It requires Product Manager, Architect,
Security Reviewer, Developer, Test Architect, Release/Operations owner, EventStore
owner, and Parties maintainer coordination.

### Recipients and responsibilities

| Recipient | Responsibility |
| --- | --- |
| Product Manager / Product Owner | Approve the post-MVP commitment and keep it separate from Phase 4 MVP completion; keep Parties Story 8.7 gated. |
| Winston / Architect | Apply AD-23, own Story 8.1, freeze package/format/backend/rollout boundaries, and reject implementation before approval. |
| Named Security Reviewer | Review threat model, AES-GCM/AAD vectors, key lifecycle, typed failures, backend restrictions, zeroing, no-leak posture, and downgrade/rollback. |
| Amelia / Developer | Implement only after the spec gate; keep the engine domain-neutral; update package/release artifacts atomically. |
| Murat / Test Architect | Require owner goldens, real-backend integration, persisted evidence, no-leak scanning, Parties dual-provider parity, and rollback after v2 writes. |
| Release / Operations owner | Approve package inventory, immutable provenance, backend identity/config boundary, startup restrictions, and operator runbooks. |
| EventStore owner | Review the completed G5 packet and issue the only EventStore `available` decision. |
| Parties maintainer | Retain local security provider and domain policy; consume only exact approved artifacts; execute dual-provider compatibility and rollback before deletion. |

### Sequenced handoff

1. Apply the approved planning edits to PRD, architecture, epics, sprint status,
   and guide.
2. Execute Story 8.1 and obtain named security/architecture/owner approvals.
3. Execute Story 8.2 against the frozen spec.
4. Run EventStore goldens and real production-backend integration.
5. Release/pin exact packages and record source/package/backend provenance.
6. Run Parties dual-provider parity and post-v2-write rollback.
7. Have the EventStore owner approve the G5 packet as `available`.
8. Only then may Parties change G5 status, begin Story 8.7 migration, and remove
   retained local code after its own completion gates pass.

### Success criteria

1. AD-23 records EventStore engine ownership and provider/operator key custody.
2. Story 1.20 remains projection/query-only.
3. Story 8.1 has an approved exact security spec before Story 8.2 starts.
4. Story 8.2 supplies the optional engine, stable `pdenc-v2`, historical readers,
   typed outcomes, shared key mechanics, required seams, and a real production
   backend.
5. EventStore goldens and Parties dual-provider tests pass.
6. Rollback is exercised after writing `pdenc-v2` data.
7. The proof packet records exact SHA, packages/hashes, backend identity, approvals,
   limitations, historical-data policy, and rollback instructions.
8. Parties G5 remains `needs-additive-api` until the complete packet is approved
   and exact artifacts are consumed.

## 6. Change-Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [x] Done | Parties Story 8.7 / Story 8.3 G5 exposed the missing delivery commitment. |
| 1.2 Core problem | [x] Done | New stakeholder requirement plus contradictory ownership; no real engine story/package exists. |
| 1.3 Evidence | [x] Done | PRD, architecture, epics, guide, manifest, Story 1.20, Parties routing, and current G5 matrix inspected. |
| 2.1 Current epic viability | [x] Done | Epics 1-7 remain viable; Story 1.20 remains intact. |
| 2.2 Required epic changes | [x] Done | Added post-MVP Epic 8 and Stories 8.1-8.2. |
| 2.3 Remaining epic impact | [x] Done | No existing epic/story invalidated or resequenced. |
| 2.4 New epic need | [x] Done | A new spec-gated security epic prevents silent Phase 4 MVP expansion. |
| 2.5 Priority/order | [x] Done | 8.1 precedes 8.2; G5 stays independent from Story 1.20. |
| 3.1 PRD conflict | [x] Done | Narrowed out-of-scope wording and added FR37/NFR19 as committed post-MVP scope. |
| 3.2 Architecture conflict | [x] Done | Added AD-23 and the package/backend/key-custody split. |
| 3.3 UX impact | [N/A] Skip | No new EventStore UI; Parties UX remains compatibility proof. |
| 3.4 Other artifacts | [x] Done | Updated epics, sprint status, and guide; release inventory, tests, proof packet, and Parties gate are explicitly deferred to Story 8.2 delivery. |
| 4.1 Direct adjustment | [x] Viable | High effort/high risk; selected with a spec gate and post-MVP placement. |
| 4.2 Potential rollback | [x] Not viable | Completed 22.7 prerequisites remain valid; Parties local provider is the desired future rollback path. |
| 4.3 MVP review | [x] Viable | Clarify—not reduce or expand—the current Phase 4 MVP boundary. |
| 4.4 Recommended path | [x] Done | Hybrid direct adjustment + MVP-boundary clarification. |
| 5.1 Issue summary | [x] Done | Section 1. |
| 5.2 Impact analysis | [x] Done | Section 2. |
| 5.3 Recommended path | [x] Done | Section 3. |
| 5.4 MVP/action plan | [x] Done | Phase 4 unaffected; Epic 8 and sequenced handoff defined. |
| 5.5 Agent/role handoff | [x] Done | Section 5. |
| 6.1 Checklist review | [x] Done | All applicable items analyzed; pending edits are explicit. |
| 6.2 Proposal accuracy | [x] Done | Cross-checked against current EventStore and Parties artifacts. |
| 6.3 User approval | [x] Done | Administrator approved the complete proposal on 2026-07-16. |
| 6.4 Sprint status update | [x] Done | Added Epic 8 and Stories 8.1-8.2 as `backlog`; preserved existing statuses. |
| 6.5 Next steps/handoff | [x] Done | Roles, sequence, and completion criteria are explicit. |

## 7. Approval State

This proposal was **approved** by Administrator on 2026-07-16. The authorized
planning and tracking corrections in section 4 were applied. Approval does not
start Story 8.2 implementation, modify Parties, provision a production backend,
change the current 14-package release manifest, or delete any rollback code.

## 8. Workflow Execution And Handoff Log

| Field | Recorded outcome |
| --- | --- |
| Issue addressed | EventStore had no ownership decision or delivery story for the Parties G5 shared payload-protection engine. |
| Change scope | Major; post-MVP security platform capability. |
| Artifacts modified | `prd.md`, `architecture.md`, `epics.md`, `sprint-status.yaml`, payload-protection guide, and this approved sprint-change proposal. |
| Artifacts intentionally unchanged | Parties repository, `tools/release-packages.json`, source projects, tests, runtime configuration, DAPR topology, and persisted data. |
| Routed to | Product Manager/Product Owner, Winston/Architect, named Security Reviewer, Amelia/Developer, Murat/Test Architect, Release/Operations owner, EventStore owner, and Parties maintainer. |
| Immediate next step | Execute Story 8.1 and obtain the approved security specification before Story 8.2 starts. |
| Completion gate | Story 8.2's G5 packet is `available`, exact artifacts/backend are recorded, EventStore goldens and Parties dual-provider parity pass, and rollback succeeds after `pdenc-v2` writes. |
