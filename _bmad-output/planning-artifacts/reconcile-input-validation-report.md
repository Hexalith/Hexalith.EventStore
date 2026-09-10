# Input Reconciliation — Validation Report

- **Input:** `_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md`
- **Target:** `_bmad-output/planning-artifacts/prd.md`
- **Target state reviewed:** `status: final`, `document_status: final`, `implementation_readiness_status: blocked`, `implementation_readiness_result: reject`
- **Reconciliation verdict:** **15/15 critical and high findings represented; no critical/high input-reconciliation gap remains.** All five critical and ten high findings are present as durable requirement text, an explicit mandatory gate, or both. Representation is not implementation closure.

## Critical findings (5/5)

| ID | Source finding | Representation in latest PRD | Fail-closed disposition |
| --- | --- | --- | --- |
| C1 | Planning artifacts are not one approved baseline | §0 and §11.3 distinguish the examined dirty/unapproved snapshot from an authorizing baseline. G-BASELINE requires one manifest binding the final PRD, architecture, detailed UX, epics, sprint status, wrappers, and evidence after substantive reconciliation; OR5, OR8, OR14, and OR15 retain the unresolved artifact, lifecycle, guard, and approval work. | **G-BASELINE FAIL/BLOCKED.** No downstream handoff or `READY` claim. |
| C2 | Normative OQ8 requirements are unavailable inside EventStore | §1.1 binds the external repository, path, commit, and digest, says EventStore cannot reproduce the bytes, and requires a permitted immutable copy, complete approved normative projection, or signed/content-addressed attestation. G-OQ8 requires the same complete identity through all governed artifacts and a passing named pre-review validator; OR11 remains blocking. | **G-OQ8 FAIL/BLOCKED.** Neither OQ8 closure nor Story 4.15 can authorize readiness, release, or consumer handoff. |
| C3 | Generated public APIs contradict the canonical tenant boundary | FR12, FR15, NFR2, §8.2, FR12-C2, G-TENANT, and OR20 require exactly one authorized canonical tenant, the shared grammar/canonicalizer, rejection of every named invalid case before downstream work, a distinct authenticated platform-operation scope, compiled/runtime negatives, and corrective/reopened ownership. Existing `done` labels are expressly non-authorizing. | **G-TENANT FAIL/BLOCKED.** No tenant-safety, readiness, release, or deployment claim. |
| C4 | Public command `Location` can use `CorrelationId` as status identity | FR12 and §8.2 make valid canonical `MessageId` the sole status selector. FR12-C3/C4 require missing, invalid, and divergent-identity behavior; G-STATUS-ID and OR21 require a corrected or reopened Story 2.9 plus compiled/runtime evidence. | **G-STATUS-ID FAIL/BLOCKED.** No FR12 closure or external API release. |
| C5 | Silent concurrent append loss remains unguarded | NFR7, the §9 safety boundary, SM11, G-APPEND, and OR4 preserve the reproduced overwrite as input evidence only. Closure requires provider-portable fencing or a mechanically enforced no-second-writer operating envelope with supported-provider and production-path proof; risk acceptance is not a waiver. | **G-APPEND FAIL/BLOCKED.** Blocks MVP completion, readiness, release, and deployment. |

## High findings (10/10)

| ID | Source finding | Representation in latest PRD | Fail-closed disposition |
| --- | --- | --- | --- |
| H1 | The future `READY` decision has no executable exit contract | §11.4 defines a computed, all-mandatory-gates decision table with governed requirements, required evidence/validation, evaluator/approval, current result, waiver policy, and invalidation trigger. G-READINESS and OR17 require a new persisted readiness report only after every mandatory row passes. | Aggregate remains **FAIL/BLOCKED — `Reject`**. |
| H2 | Omnibus FR26, FR33, FR34, and NFR17 have no clause-level closure rule | §7.1 assigns stable clause IDs and observable consequences; G-CLAUSE requires each clause to be mirrored in epics, mapped to a primary slice and exact evidence, and enforced by an all-clauses-required validator. NFR17-C5 remains explicitly unassigned; OR7 owns the corrective work. | **G-CLAUSE FAIL/BLOCKED.** PRD text or a story label cannot close a clause. |
| H3 | Acceptance ambiguity extends to FR4, FR5, FR7, and FR12 | §7.1 now decomposes all four into consequence-level clauses and primary slices. G-CLAUSE rejects missing, duplicate, unsupported, partial, or status-only closure. | **G-CLAUSE FAIL/BLOCKED** until every clause has content-bound passing evidence. |
| H4 | NFR8's projection-cost outcome has no measurable bound | NFR8 refuses to infer a bound and requires the named projection-cost/sequence-guard specification to bind a numeric budget, workload/page-size model, pass condition, evidence identity, approval, validation command, and Story 6.4 authorization. G-NFR8 and OR16 preserve every missing item. | **G-NFR8 FAIL/BLOCKED.** Story 6.4 remains unauthorized. |
| H5 | NFR traceability is incomplete | §11.2 now classifies primary and supporting declarations for every NFR1-NFR19, distinguishes literal range endpoints from range-only interiors, and states declaration is not delivery. NFR5 and NFR13 remain visibly unassigned, NFR12's semantic gap remains blocking, and Story 2.8 is correctly excluded from NFR5. NFR18's unassigned documentation outcome is also exposed. | Traceability omissions are repaired in the PRD; uncovered ownership/delivery remains blocking through G-BASELINE, G-CLAUSE, G-NFR18, and the named refinements. |
| H6 | Multi-stakeholder and UI-affecting workflows lack user journeys | §3.3 adds five named, failure-aware journeys: Alex/domain SDK adoption, Morgan/generated API exposure, Priya/projection-confirmed UI success, Nora/operator recovery, and Riley/release plus consumer-removal authorization. | Journey text defines outcomes only and claims no delivered UX or implementation evidence. |
| H7 | Backward compatibility excludes most public platform surfaces | NFR12 inventories source/binary APIs, generator/OpenAPI/HTTP, gateway/domain endpoints, envelopes/serialization, DAPR/CloudEvent wire, and NuGet surfaces, with additive/deprecation/removal/SemVer rules. G-COMPAT and OR22 require executable baselines, representative package-only consumers, an exact lane, evidence, and independent approval. | **G-COMPAT FAIL/BLOCKED.** No compatibility or release claim. |
| H8 | Authentication conformance does not cover every external API host | NFR3 is capability-based and names EventStore, Admin, Sample, Tenants, generated fixtures, and future JWT-binding hosts. G-AUTH-HOSTS and OR23 require one versioned contract/fingerprint plus all-host negative and release evidence; Story 5.3 is explicitly bounded and insufficient. | **G-AUTH-HOSTS FAIL/BLOCKED.** No NFR3, readiness, release, or deployment claim. |
| H9 | Consumer-removal authority is weaker than architecture | The glossary defines the consumer-owner role. FR36 separates capability availability, release/deployment authority, and per-consumer authorization, binding repository/commit, mode matrix, removal-subject digest, authenticated receipt, role registry, issuance/validity, outcome, and invalidation. G-CONSUMER and OR24 retain the missing owner/receipt/evidence work. | **G-CONSUMER FAIL/BLOCKED.** No consumer may remove local infrastructure. |
| H10 | The bound reject result is not a coherent current baseline | Frontmatter preserves the source report's examined-dirty-unapproved validation identity rather than presenting it as a clean approved baseline. §0 states document finality is separate from readiness; §11.3 records stale/conflicting identities; G-BASELINE requires a later atomic approved manifest and G-READINESS requires a fresh persisted evaluation over it. | **`blocked/reject` preserved.** Any changed bound input invalidates the applicable gate and prevents `READY`. |

## Remaining critical/high gaps

**None in source-to-PRD representation.** The following remain open by design and are implementation/evidence blockers, not missing treatment of the input findings:

1. G-BASELINE and G-OQ8 lack an approved reproducible source set and passing validation.
2. G-TENANT, G-STATUS-ID, and G-APPEND lack corrected implementation plus production/compiled evidence.
3. G-CLAUSE, G-NFR8, G-COMPAT, and G-AUTH-HOSTS lack complete ownership, specifications, validators, baselines, or all-host evidence.
4. G-CONSUMER and G-HIGH-RISK lack the required authority and non-authorship controls.

## No false implementation closure

- Frontmatter remains `implementation_readiness_status: blocked` and `implementation_readiness_result: reject`; `status: final` and `document_status: final` apply only to the PRD document.
- §0 prohibits `READY`, MVP-completion, release, deployment, consumer migration, and dependent handoff until every mandatory gate passes on one approved source identity.
- §11.4 currently marks every mandatory gate `FAIL/BLOCKED`, with G-READINESS computing aggregate `Reject`.
- Narrow source/package and frozen-evidence sub-state closures under FR36 do not close deployed-runtime parity or per-consumer authorization. Existing story labels, hash equality, local worktree state, and historical `READY` reports are explicitly non-authorizing.
- Post-MVP G5 remains blocked/`needs-additive-api` and is correctly excluded from the Phase 4 MVP aggregate without authorizing Parties migration.
