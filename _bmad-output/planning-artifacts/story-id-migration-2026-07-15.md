---
title: Story ID Migration And Evidence Crosswalk
date: 2026-07-15
status: applied
source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md
plan: _bmad-output/planning-artifacts/epics.md
---

# Story ID Migration And Evidence Crosswalk

This is the audit authority for the approved July 15 epic/story restructuring. Historical artifacts retain their original identifiers; new active files link back through `supersedes`/`superseded_by` notes. A split child inherits `done` only when this crosswalk names implementation, focused tests, and review evidence. Tenants children additionally require maintainer approval and exact Tenants SHA.

## Epic 1

| Old | New | Disposition | Evidence |
| --- | --- | --- | --- |
| 1.1 | 1.1 | `done` | Existing canonical-host implementation/spec; acceptance now explicitly lists `/query`, whose mapping/routing evidence is also covered by completed Story 1.2. |
| 1.2 plus platform scope from 2.8 | 1.2 | `done` | `_bmad-output/implementation-artifacts/1-2-domain-query-handler-routing.md`, `spec-1-2-domain-query-handler-routing.md`, and `2-8-query-response-provenance-contract-and-route-aware-gateway-etag.md`; focused gateway/client tests and completed reviews cover routing, metadata, provenance, and route-aware ETag suppression. |
| 1.3 | 1.3 | `done` | `spec-1-3-generic-read-models-and-query-cursors.md`; production `IReadModelStore`/`DaprReadModelStore`/`ReadModelWritePolicy`, focused Client tests, Release build, and two review passes are recorded. |
| 1.3 | 1.4 | `done` | Same parent spec; `InMemoryReadModelStore` plus deterministic conflict/JSON clone tests and completed review evidence are recorded independently. |
| 1.3 | 1.5 | `done` | Same parent spec; cursor codec/scope, registration, validation/problem-detail tests, Release build, and completed review evidence are recorded. |
| 1.4 | 1.6 | `done` | Scope-only renumber; completed projection/event-consumer implementation evidence remains unchanged. |
| 1.5 | 1.7 | `done` | Scope-only renumber; completed domain-module hosting/observability evidence remains unchanged. |
| 1.6 | 1.8 | `done` | `spec-1-6-sample-and-tenants-domain-centric-adoption.md`; Sample processor removal, SDK/guardrail tests, Release build, and resolved review findings isolate the Sample slice. |
| 1.6 | 1.9 | `review` | Query/read-model/cursor implementation and scoped Tenants tests are recorded in `spec-1-6-sample-and-tenants-domain-centric-adoption.md`, but no Tenants maintainer-approved PR/commit plus exact SHA/accepted scope is recorded. |
| 1.6 | 1.10 | `review` | Projection/event-consumer implementation evidence exists in the parent spec, but the required Tenants maintainer approval, exact SHA, and independent persisted-path review are absent. |
| 1.6 | 1.11 | `done` | Parent adoption spec plus `spec-1-7-domainservice-packaging-and-guardrails.md` record focused domain-module guardrails, tests, and resolved review findings without requiring Tenants source mutation. |
| 1.7 | 1.12 | `done` | Scope-only renumber; completed package/guardrail evidence remains unchanged. |
| 1.8 | 1.13 | `done` | Investigation/proof-only scope preserved; owner packet remains `still blocked` and does not claim parity availability. |
| 1.9 | 1.14 | `done` | Scope-only renumber; `_bmad-output/implementation-artifacts/1-9-read-model-and-projection-checkpoint-erasure.md` retains completed production-path/review evidence. |
| 1.10 | 1.15 | `done` | Scope-only renumber; `_bmad-output/implementation-artifacts/1-10-coordinated-read-model-batch-writes.md` retains completed evidence. |
| 1.11 | 1.16 | `done` | Scope-only renumber; completed lifecycle spec/review evidence remains, with the prerequisite corrected from old 2.8 to Story 1.2. |
| 1.12 | 1.17 | `done` | Scope-only renumber; `_bmad-output/implementation-artifacts/1-12-asynchronous-multi-projection-dispatch.md` retains evidence, while acceptance is narrowed to AD-19's exact normalized result. |
| 1.13 | 1.18 | `done` | Scope-only renumber; `_bmad-output/implementation-artifacts/1-13-projection-handler-delivery-idempotency.md` retains completed production-path evidence. |
| 1.14 | 1.19 | `review` | Active implementation/review state preserved; reissued file links to `_bmad-output/implementation-artifacts/1-14-correct-paged-rebuild-and-replay-equivalence.md`. |
| 1.15 | 1.20 | `ready-for-dev` | Active not-started closure scope preserved; reissued file links to `_bmad-output/implementation-artifacts/1-15-owner-approved-parity-closure-and-runtime-pin.md` and adopts AD-22 artifact-identity rules. |

## Epic 2

| Old | New | Disposition | Evidence |
| --- | --- | --- | --- |
| 2.1-2.3 | 2.1-2.3 | `done` | Identifiers and scopes unchanged. |
| 2.4 | 2.4 | `review` | `spec-2-4-tenants-external-api-host-adoption.md` records contract metadata/tests, but lacks the new maintainer-approved PR/commit and exact SHA gate. |
| 2.4 | 2.5 | `review` | Parent spec records dedicated host/AppHost/ACL/runtime tests; exact Tenants approval/SHA evidence is absent. |
| 2.4 | 2.6 | `review` | Parent spec records UI boundary guardrails; independent UX review plus exact approved Tenants SHA is absent. |
| 2.4 | 2.7 | `review` | Parent spec records source/package builds, but the approved repository boundary, exact SHA, and independent compatibility disposition are incomplete. |
| 2.5 | 2.8 | `done` | Scope-only renumber; completed notification implementation/review remains, with deterministic reject-not-clip acceptance. |
| 2.6 | 2.9 | `done` | Scope-only renumber; completed generated command-status policy evidence remains. |
| 2.7 | 2.10 | `done` | Scope-only renumber; completed outbound-header policy evidence remains. |
| 2.8 | 2.11 | `review` | Platform implementation moved to Story 1.2. Existing 2.4/2.8 evidence supports consumer behavior, but the new consumer-only slice still lacks exact Tenants approval/SHA and independent persisted-path review. |

Epic 2 returns to `in-progress` until Stories 2.4-2.7 and 2.11 satisfy their external-authority gates.

## Epic 3

| Old | New | Disposition | Evidence |
| --- | --- | --- | --- |
| 3.1-3.6 | 3.1-3.6 | Preserve current status | Identifiers unchanged; Story 3.1 acceptance now matches `docs/ci.md`: unfiltered deterministic `Server.Tests` plus dedicated `Server.LiveSidecar.Tests`. |
| 3.7 | 3.7 | `done` | `spec-3-7-shared-ci-cd-security-gates-and-supply-chain-backlog.md` records shared caller migration, deterministic/live project split, focused validation, and completed reviews. |
| 3.7 | 3.8 | `done` | Same parent spec records reference/cache/package validation safety, release-secret ordering, manifest tests, and completed reviews. |
| 3.7 | 3.9 | `review` | Parent spec/doc records the remaining supply-chain themes, but the required standalone `_bmad-output/planning-artifacts/backlog/supply-chain-publishing.md` artifact with owner/dependencies/risks/validation has not been completed and reviewed. |
| 3.8 | 3.10 | `done` | Scope-only renumber; active smoke-preflight evidence is reissued and acceptance now always requires persisted event plus read-model/query state. |

## Epics 4-7

| Old | New | Disposition | Evidence |
| --- | --- | --- | --- |
| 4.7 | 4.7 | `backlog` | Identifier preserved; FR15/NFR8/NFR16 mapping and exact Tenants approval/SHA terminal rule added. |
| 5.6 | 5.6-5.9 | `backlog` | Parent had not started; AppHost loading, production YAML/ACLs, drift tests, and operator docs are now independently reviewable. |
| 7.2 | 7.2-7.5 | `backlog` | Parent had not started; claims, audit, deferred operations, and typed client are independently reviewable. |
| 7.3 | 7.6-7.9 | `backlog` | Parent had not started; secret store, health, resiliency, and immutable images are independently reviewable. |
| 7.4 | 7.10-7.13 | `backlog` | Parent had not started; integration CI, persisted evidence, reclassification, and advisory/performance hygiene are independently reviewable. |
| New | 7.14 | `backlog` | Architecture AD-21 and canonical UX define the consolidated `Admin.UI`/FrontComposer migration boundary. |
| 7.5 | 7.15 | `done` | `backlog/gdpr-1-aggregate-erasure.md` contains scope, non-goals, dependencies, risks, and validation expectations; draft capability classification is allowed. |
| 7.5 | 7.16 | `done` | `backlog/iam-1-admin-oidc-login.md` contains the required independent backlog fields. |
| 7.5 | 7.17 | `done` | `backlog/kit-1-aggregate-test-kit.md` contains the required independent backlog fields. |
| 7.5 | 7.18 | `done` | `backlog/rest-generator-hardening.md` contains required fields, resolved-wave history, named remaining targets, and evidence links. |

## Active File Reissue Map

| Historical active file | Reissued active file | Rule |
| --- | --- | --- |
| `1-14-correct-paged-rebuild-and-replay-equivalence.md` | `1-19-correct-paged-rebuild-and-replay-equivalence.md` | Preserve `review`; historical file points forward and new file points back. |
| `1-15-owner-approved-parity-closure-and-runtime-pin.md` | `1-20-owner-approved-parity-closure-and-runtime-pin.md` | Preserve `ready-for-dev`; update prerequisites and AD-22 identity mapping. |
| `2-8-query-response-provenance-contract-and-route-aware-gateway-etag.md` | `2-11-query-provenance-consumption-in-generated-rest-and-tenants.md` | Historical platform evidence is adopted by Story 1.2; reissued Story 2.11 is consumer-only and remains `review`. |
| `3-8-generated-api-dapr-aspire-smoke-preflight.md` | `3-10-generated-api-dapr-aspire-smoke-preflight.md` | Preserve `done`; update exact persisted-evidence acceptance. |

Historical retrospectives, evidence packets, review reports, and commits keep their original identifiers. New artifacts must cite this crosswalk rather than rewriting history.
