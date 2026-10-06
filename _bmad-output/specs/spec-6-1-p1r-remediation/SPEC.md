---
id: SPEC-6-1-p1r-remediation
companions:
  - compatibility-matrix.md
  - qualification-contract.md
  - runtime-evidence.md
  - ../../project-context.md
  - ../../planning-artifacts/architecture.md
  - ../../../../projects/_bmad-output/planning-artifacts/architecture/architecture-projects-2026-07-15/ARCHITECTURE-SPINE.md
  - ../../../../projects/_bmad-output/implementation-artifacts/6-1-p1r-compatibility-scenarios.md
  - ../../../../projects/_bmad-output/implementation-artifacts/spec-6-1-p2-supply-supported-query-security-projection-capabilities.md
sources:
  - ../../../../projects/_bmad-output/implementation-artifacts/spec-6-1-p1r-remediation.md
  - ../../implementation-artifacts/spec-6-1-p1r-remediation-runtime.md
---

> **Canonical contract.** This SPEC and its companions define what to build, test, and qualify. Sources provide audit traceability. Recorded source results in `runtime-evidence.md` do not grant package qualification or owner acceptance.

# 6.1-P1R Compatibility Remediation and Supported Recovery

## Why

The accepted EventStore 3.110.0 / Builds 4.29.1 prerequisite remains unusable after seven measured compatibility failures. EventStore operators and dependent Projects work need safe persisted-history handling, supported authority and capability boundaries, and recovery that preserves committed writes. Source remediation now has recorded regression and live actor evidence; immutable published binaries, replacement-package qualification, and exact owner decisions remain separate obligations.

## Capabilities

- **CAP-1**
  - **intent:** Supported readers and writers preserve retained history and tenant isolation.
  - **success:** Legacy missing floor defaults to 1; floor 5/head 12/snapshot 9 reconstructs state 12, append 13 preserves floor and prior hashes, and fresh restart reconstructs state 13 without changing the second tenant. Invalid or uncovered history causes no domain mutation.

- **CAP-2**
  - **intent:** Supported replay refuses malformed or unsupported evidence before domain application while preserving valid replay.
  - **success:** Invalid floor, unreadable payload, protection mismatch, unknown type, or metadata version 987, including a late-invalid event, causes zero Apply/Handle calls and domain writes with unchanged committed inventories; valid legacy, protected, and registered evolution inputs still succeed.

- **CAP-3**
  - **intent:** Protected queries retain authenticated actor, workload, and delegation authority across supported routes.
  - **success:** JSON/DataContract and handler forwarding preserve actor, workload, delegation, scopes, audience, and signed admission proof, or the incapable route refuses access before disclosure. Legacy unknown fields confer no authority.

- **CAP-4**
  - **intent:** Projections and cursors retain exact durable freshness and authorized scope.
  - **success:** Global position 987 survives independently of aggregate sequence 12; cursors bind the persisted read-model watermark and tenant/domain/query/caller/filter scope. Unknown, non-positive, changed, or tampered authoritative bindings fail closed.

- **CAP-5**
  - **intent:** Compatible legacy and supported newer actor operations report actual outcomes.
  - **success:** Valid fenced and trusted effects persist; stale, forged, unauthorized, or unsupported operations cause no authorized effect or false completion. Recovery null/false/true remains distinct through transport.

- **CAP-6**
  - **intent:** Operators recover without losing committed writes or allowing incompatible mutation.
  - **success:** A qualified writable rollback preserves retained metadata, history, and tenants through actual restore, append, and restart; otherwise the approved Projects AD-17 envelope fences and drains ingress, freezes mutation, and recovers forward. No pre-upgrade restore discards later committed writes.

- **CAP-7**
  - **intent:** Qualification establishes compatibility for the exact supported source and published package scope.
  - **success:** All seven families and the adopted seventeen-scenario contract have enforced dispositions; selected additions have independent coverage; actual archives, signatures, assets/lock graphs, and physically loaded assemblies bind to immutable candidate/rollback/Builds inputs with no unexpected EventStore source dependencies.

- **CAP-8**
  - **intent:** Verification preserves reproducible evidence and safely releases invocation-owned resources.
  - **success:** Required lanes retain bound receipts and persisted inventories; startup, success, failure, timeout/cancellation, and repeated cleanup leave no owned resources and preserve shared identities/state. Missing, failed, skipped, zero-assertion, unavailable, or incompatible required lanes remain nonpassing.

- **CAP-9**
  - **intent:** Owners can decide an exact candidate and recovery transition from independent evidence.
  - **success:** EventStore, Builds, Solution, and Test decisions plus same-baseline conformance precede any validated coordinated record/guard/catalog/Stack/pin transition; independent prerequisite and release gates retain their own decisions.

## Constraints

- Preserve fixed acceptance JSON v1, October 1 tuple/rollback decisions, completed investigation, sealed packets/fixtures, and old protected-hash validators. Never rewrite event history, reseal old evidence, weaken assertions, or replace an existing published version's bytes.
- Keep one writer and fence/drain incompatible ingress. RPO 0 forbids restoring away committed writes; historical 3.70.1 is not a qualified writable rollback for newer retained streams.
- Preserve additive public compatibility, legacy constructors/deconstruction/defaults, common calls, supported protection/evolution, cancellation, typed failures, and detached replay state through the existing EventStore/P2 seams in `compatibility-matrix.md`.
- Authority derives from authenticated evidence; workload cannot substitute for actor. Freshness derives from committed, durably applied global positions; allocator state, time, local sequence, and ETag cannot substitute.
- Local Debug/project-reference results and isolated Release/published-package results are distinct. New source or package inputs require new bound evidence; test counts cannot substitute for measured assertions.
- Repository-local scope, publication, deployment, tuple/rollback selection, pins, and acceptance retain the owners and entry criteria in `qualification-contract.md`. This spec creates no such authorization.
- Use root-declared repositories and preserve unrelated changes. Project-wide conventions remain adopted companions; Projects and EventStore architecture decision numbers have separate namespaces.
- P1R completion alone cannot complete P0/P2/P3/P4, G-6, independent readiness, Story 6.1, or Story 8.11. Existing usability remains false and readiness remains NOT_READY until the separate entry chain passes.

## Non-goals

- Implementing Projects list/open, changing PRD/UX scope, or adding/resequencing epics or stories.
- Reopening historical acceptance/investigation or retroactively repairing immutable old binaries.
- Selecting dependency versions, publishing, deploying, or changing acceptance/readiness/pins during spec derivation.
- Absorbing independent P2 denial qualification or the six pre-existing review deferrals recorded in `runtime-evidence.md`.

## Success signal

Every required finding has an enforced, tested treatment in an owner-approved supported envelope, actual published inputs are independently verified, and recovery preserves all committed writes. Exact four-role decisions and same-baseline conformance make a coordinated prerequisite transition reviewable; separate P0/P2/P3/P4/readiness and release gates still determine downstream usability.

## Open Questions

- Which exact repaired candidate/Builds tuple and publication scope are owner-authorized, and which capable rollback family is selected if writable rollback is proposed?
- Which runtime/backend and capability inventory do Solution/Platform owners select for candidate and operational recovery qualification? Historical PostgreSQL/Dapr 1.18.2 and current Redis/Dapr 1.18.4 proof have different scopes.
- Which instrumented assertion-count mechanism will the Test Architect accept for published qualification? Existing xUnit counts leave runtime assertions unmeasured.
