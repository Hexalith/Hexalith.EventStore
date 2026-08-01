---
title: August Story ID Migration And Evidence Crosswalk
date: 2026-08-01
status: applied
source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01.md
prior_crosswalk: _bmad-output/planning-artifacts/story-id-migration-2026-07-15.md
plan: _bmad-output/planning-artifacts/epics.md
---

# August Story ID Migration And Evidence Crosswalk

This is the audit authority for the approved 2026-08-01 implementation-readiness correction. It supplements rather than rewrites the 2026-07-15 crosswalk. Historical artifacts retain their original identifiers and claims. A focused child receives only the status justified below; checked tasks in an umbrella file are evidence inputs, not inherited completion.

## Dependency And Scope Corrections

| Prior scope or dependency | Current owner/disposition | Status and evidence rule |
| --- | --- | --- |
| Story 1.20 source/package plus deployed-runtime parity | Story 1.20 remains source/package closure; new Story 3.13 owns deployed-runtime parity after completed Stories 1.20 and 3.12. | 1.20 remains `done`; 3.13 is `backlog`. Story 3.13 cannot gate or reopen either predecessor and must independently prove OCI index, child image/config, package-hash, and release-provenance identity. |
| Story 2.6 implicitly relying on Story 2.11 production provenance | Story 2.6 owns deterministic presentation behavior and fixtures; Story 2.11 exclusively owns generated REST/Tenants production provenance. | 2.6 remains `done` on its UI/presentation evidence; 2.11 remains independently `done` on its recorded production-provenance evidence. No forward dependency remains. |
| Story 2.10 registration wording implied DAPR transport selection | Story 2.10 remains the completed typed-client composition correction under AD-18. | 2.10 remains `done`; the accepted contract is `AddEventStoreGatewayClient(...)` for the typed client only plus explicit last/innermost `.AddEventStoreDaprServiceInvocation(appId, apiToken)` when DAPR is required. |
| Story 3.5 ecosystem package-catalog scope | Builds, EventStore, Commons, FrontComposer, Memories, PolymorphicSerializations, and Tenants are the closed inventory. | 3.5 remains `done`; newly discovered repositories require a follow-up rather than reopening its evidence. |
| Story 3.11 ecosystem audit scope | Builds revision `9dc0fe1ffbf33269fddf195fd12317def86728f0`, 284 packages, 139 families, and the five recorded changed groups are the closed audit family. | Preserve `awaiting-operator`; later rows or repository families require a follow-up or superseding full audit, not silent scope expansion. |
| Untracked reserved tenant constraint | New Story 5.10 owns normalized `system` tenant rejection at the provisioning boundary. | 5.10 is `backlog`; no prior story status is inherited. |
| Epic 6 specification work counted as implementation progress | Stories 6.1, 6.3, and 6.5 are enablers; Stories 6.2, 6.4, and 6.6 are runtime implementation. | Existing statuses remain unchanged. Enabler completion authorizes its child but never counts as runtime implementation progress; 6.2 additionally proves the approved numeric snapshot-envelope bound. |
| Story 7.6 prose-only secret-store criteria | Story 7.6 keeps its identity and scope but now uses four BDD scenarios. | Existing status remains unchanged; completion requires startup failure, runtime loss/recovery, acknowledged rotation, and real-OpenBao least-privilege evidence. |

## Story 4.8 Durable-Admission Decomposition

Story 4.8 is now a non-executable evidence ledger. Its implementation artifact remains at `_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md` solely to preserve the original acceptance criteria, source-candidate history, task checkboxes, and evidence links.

| Prior ledger work | Executable child | Migrated status | Evidence/disposition |
| --- | --- | --- | --- |
| Tasks 2-3: trusted adapter, canonical intent, opaque-key identity, leakage boundary | 4.9 Trusted Admission Contract And Protected Identity | `review` | The 4.8 record identifies implemented source and focused test evidence, but no child-focused review grants `done`. |
| Task 4: digest key ring, directory, promotion/rotation, collision, retirement | 4.10 Digest Directory Rotation And Key Retirement | `review` | The 4.8 record identifies implementation and 24/24 focused tests; focused review and child acceptance remain required. |
| Task 5 plus replay/reconciliation portion of Task 6 | 4.11 Admission State Machine And Current-Fence Enforcement | `ready-for-dev` | Work is explicitly unchecked in the ledger; no completion evidence is inherited. |
| Expiry/public-response portion of Task 6 plus compaction/deletion/legal-hold portion of Task 7 | 4.12 Expiry Compaction And Tombstone Retention | `backlog` | Unchecked umbrella work is moved intact to a focused implementation/review boundary. |
| Legacy inventory and migration portion of Task 7 | 4.13 Legacy Admission Migration And Fail-Closed Reconciliation | `backlog` | No independently closed legacy-inventory/migration evidence exists. |
| Task 8 multi-host production proof and packet production | 4.14 OQ8 Multi-Host Production Evidence | `backlog` | Owns `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml`; HTTP/unit/mock-only proof is insufficient. |
| Task 8 review/release handoff and final documentation reconciliation | 4.15 OQ8 Platform Closure And Handoff | `backlog` | Requires 4.14 packet plus senior/security/test review and separately authorized identity handoff; Folders retains cross-repository final closure. |

Story 4.8 itself has no sprint execution status. It must never appear as `ready-for-dev`, `in-progress`, `review`, or `done` after this migration.

## Story 7.14 Admin UI Decomposition

| Prior scope | Current child | Migrated status | Evidence/disposition |
| --- | --- | --- | --- |
| Shell, module, canonical routes, and legacy redirects | 7.14 Admin Shell And Canonical Route Migration | `backlog` | Retains only the shell/route boundary in existing `src/Hexalith.EventStore.Admin.UI`. |
| Typed-client wiring and projection-confirmed/unavailable/support-safe states | 7.19 Admin Typed-Client And Evidence-State Integration | `backlog` | New focused story; no prior implementation evidence grants progress. |
| Accessibility, localization, responsive behavior, and conformance evidence | 7.20 Admin Accessibility Localization And Responsive Conformance | `backlog` | New focused story; no performance budget is introduced. |

All three stories evolve the existing Admin UI in place using FrontComposer and Fluent UI Blazor V5; none may create a second UI host.

## Story 8.2 Payload-Protection Decomposition

Story 8.1 remains `in-progress`, the authoritative security specification remains `NOT AUTHORIZED`, and no approval record exists. Therefore every implementation child is `backlog` and blocked until its recorded predecessor passes. No former Story 8.2 umbrella status or planning approval authorizes code, packages, provider resources, Parties changes, or G5 availability.

| Former umbrella concern | Executable child | Migrated status | Gate |
| --- | --- | --- | --- |
| Contracts and owner goldens | 8.2 Payload-Protection Contracts And Golden Vectors | `backlog` | Story 8.1 named approvals and matching normative digest explicitly authorize 8.2. |
| Provider-neutral cryptographic engine | 8.3 pdenc-v2 Core Cryptographic Engine | `backlog` | 8.2 contracts/goldens approved. |
| Historical and mixed-format readers | 8.4 Compatibility Readers And Mixed-History Routing | `backlog` | 8.3 complete; may proceed in parallel with 8.5. |
| Policy and durable key lifecycle | 8.5 Policy And Key-Lifecycle Mechanics | `backlog` | 8.3 complete; may proceed in parallel with 8.4. |
| Real production adapter | 8.6 Azure Key Vault Production Adapter Conformance | `backlog` | Both 8.4 and 8.5 complete. |
| EventStore persistence/snapshot hooks | 8.7 Server Persistence And Snapshot Integration | `backlog` | 8.6 complete. |
| Two packages, manifest, provenance, package-only proof | 8.8 Package And Release Integration | `backlog` | 8.7 complete; sole authority for atomic `tools/release-packages.json` 14-to-16 transition. Assistant entry points remain unchanged. |
| Separately authorized consumer migration/proof | 8.9 Parties Dual-Provider Parity | `backlog` | 8.8 complete plus exact Parties authority/SHA. |
| Rollback after newest-format writes | 8.10 Post-v2-Write Rollback Rehearsal | `backlog` | 8.9 complete. |
| Content- and identity-bound G5 decision | 8.11 G5 Evidence And Approval Closure | `backlog` | 8.10 complete plus all named approvals; only this story can record `available` and unblock Parties Story 8.7. |

The authoritative implementation sequence is `8.1 approval -> 8.2 -> 8.3 -> (8.4 and 8.5) -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10 -> 8.11`.

## Active Artifact Disposition

| Artifact | Disposition |
| --- | --- |
| `_bmad-output/implementation-artifacts/4-8-durable-tenant-scoped-idempotency-admission-and-expired-key-precedence.md` | Retained and reclassified as the Story 4.8 evidence ledger; its checked tasks support 4.9/4.10 review only. |
| `_bmad-output/implementation-artifacts/8-1-shared-payload-protection-security-spec-and-adr.md` | Retained at `in-progress`; handoff rebound to Stories 8.2-8.11. |
| `_bmad-output/implementation-artifacts/spec-shared-payload-protection-engine.md` | Retained as the single normative security authority; ownership/sequence rebound and normative digest recomputed, while authorization remains `NOT AUTHORIZED`. |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | Migrated to remove executable Story 4.8 status and add all focused children with the statuses above. |

Historical reports, retrospectives, commits, test logs, and evidence keep their original story identifiers. New work cites this crosswalk rather than rewriting history.
