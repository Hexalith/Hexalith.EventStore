# Readiness Gates

## Blocking Gates

| Gate | Required evidence |
| --- | --- |
| Planning baseline | `prd.md`, reviewed `architecture.md`, canonical `ux.md`, restructured `epics.md`, and this SPEC exist and are mutually referenced; PRD and canonical UX remain unchanged by the July 15 replan. |
| Dependency direction | Story 1.2 owns the platform provenance prerequisite; Stories 1.16 and 1.20 depend only on Epic 1 work, while Story 2.11 consumes the contract for generated REST/Tenants behavior. |
| Focused story slicing | Former Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, 7.4, and the four-product 7.5 umbrella are replaced by focused children with one owner, one review boundary, deterministic acceptance, and focused validation. |
| Migration audit | A story-ID/evidence crosswalk preserves old-to-new identities, statuses, active-file supersession, implementation evidence, focused tests, review results, and external approval/SHA evidence. |
| Tenants authority | Stories 1.9-1.10, 2.4-2.7, and 4.7 cannot become `done` without maintainer-approved PR/commit evidence, exact Tenants SHA, accepted scope, source/package-mode validation, and an explicit no-approval disposition. |
| Consumer parity | Story 1.20 closes only after Stories 1.14-1.19 are reviewed and the parity packet maps approved EventStore source SHA to exact package hashes or deployed image digest as applicable. |
| Admin UI ownership | Story 7.14 evolves `Hexalith.EventStore.Admin.UI` in place under `eventstore-admin-ui`, matching FrontComposer `3.2.2` Shell/Contracts.UI and Fluent UI V5; no second UI host or duplicate legacy page implementation is created. |
| Backlog story shape | Stories 7.15-7.18 independently govern GDPR-1, IAM-1, KIT-1, and REST generator hardening artifacts. |
| Admin request-size safety | Story 5.2 acceptance uses concrete limits: `1_048_576` bytes for representative admin JSON write/sandbox bodies and `10 * 1024 * 1024` bytes for `AdminBackupsController.ImportStream`; "tested or documented" is not enough. |
| Spec-first cost/evolution | Stories 6.1, 6.3, and 6.5 produce approved specs at `_bmad-output/implementation-artifacts/spec-folded-snapshot.md`, `_bmad-output/implementation-artifacts/spec-projection-cost-sequence-guard.md`, and `_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` before Stories 6.2, 6.4, and 6.6 implement them. |
| High-risk NFR traceability | NFR1-NFR4, NFR7, NFR10-NFR11, and NFR14-NFR17 map to concrete story coverage and persisted-evidence validation. |
| UX readiness | `ux.md` covers Sample accepted-submission behavior, Tenants projection-confirmed states, Admin unavailable-operation behavior, support-safe states, accessibility/localization evidence, and FrontComposer/Fluent UI V5 governance. |
| Readiness rerun | A fresh assessment reports complete FR1-FR36/NFR1-NFR18 coverage and no forward-dependency or oversized-parent structural blocker before broad remaining Phase 4 work resumes. |

## Approved Story Migration

| Old story | New story or stories |
| --- | --- |
| 1.3 | 1.3 persisted store/policy; 1.4 deterministic fake; 1.5 protected cursor codec |
| 1.6 | 1.8 Sample adoption; 1.9 Tenants query/read-model; 1.10 Tenants projection/consumer; 1.11 guardrails |
| 1.4-1.15 | 1.6-1.7 and 1.12-1.20 per the approved crosswalk |
| 2.4 | 2.4 contracts/routes; 2.5 external host; 2.6 UI/UX; 2.7 compatibility/package-mode |
| 2.5-2.8 | 2.8-2.11, with 2.11 consumer-only provenance |
| 3.7 | 3.7 caller migration; 3.8 reference/validation safety; 3.9 supply-chain backlog |
| 3.8 | 3.10 generated API smoke preflight |
| 5.6 | 5.6 AppHost loading; 5.7 production DAPR parity; 5.8 drift tests; 5.9 operator docs |
| 7.2 | 7.2 claims; 7.3 audit; 7.4 deferred operations; 7.5 typed client |
| 7.3 | 7.6 secret store; 7.7 readiness/app health; 7.8 resiliency; 7.9 immutable images |
| 7.4 | 7.10 integration CI; 7.11 persisted evidence; 7.12 test reclassification; 7.13 advisory/performance workflow |
| 7.5 | 7.15 GDPR; 7.16 Admin OIDC; 7.17 aggregate test kit; 7.18 REST generator hardening |
| New | 7.14 consolidated EventStore Admin dashboard migration |

## High-Risk NFR Coverage

| NFR | Primary story coverage |
| --- | --- |
| NFR1 | 5.2, 5.3, 5.5, 7.2, 7.7 |
| NFR2 | 1.9-1.10, 2.4-2.7, 5.2, 5.5, 5.7-5.8 |
| NFR3 | 5.3 |
| NFR4 | 5.3, 7.6 |
| NFR7 | 4.1, 4.2, 4.4, 4.5, 5.1 |
| NFR10 | 3.1, 7.10, 7.12 |
| NFR11 | 3.6 |
| NFR14 | 2.3, 2.5-2.6, 7.14 |
| NFR15 | 7.4, 7.14 |
| NFR16 | 1.14-1.20, 2.11, 3.10, 5.8, 7.11-7.12 |
| NFR17 | 5.6-5.9, 7.6-7.9 |

## Counter-Metrics

- Do not optimize for fewer stories if that preserves unreviewable multi-concern stories.
- Do not count API smoke responses as integration evidence where persisted state-store, read-model, or CloudEvent evidence is required.
- Do not satisfy UI readiness by documenting intent only; UI stories still need component/governance evidence in `ux.md` and tests.
- Do not grant `done` to a split child from parent status alone; require the evidence crosswalk.
- Do not compare the consuming repository commit to the approved EventStore runtime SHA.
