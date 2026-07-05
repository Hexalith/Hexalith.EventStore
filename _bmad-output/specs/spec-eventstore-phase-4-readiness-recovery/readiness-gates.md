# Readiness Gates

## Blocking Gates

| Gate | Required evidence |
| --- | --- |
| Planning baseline | `prd.md`, `architecture.md`, `ux.md`, updated `epics.md`, and this SPEC exist and are mutually referenced. |
| Story slicing | Stories 1.3, 1.6, 2.4, 3.7, 5.6, 7.2, 7.3, 7.4 are split or explicitly accepted as coordinated slices with named owners and validation commands. |
| Backlog story shape | Story 7.5 is reclassified as backlog/planning work or given exact artifact paths and reviewable outputs. |
| Admin request-size safety | Story 5.2 acceptance uses concrete request-size tests or explicitly deferred scope; "tested or documented" is not enough. |
| Spec-first cost/evolution | Stories 6.1, 6.3, and 6.5 produce approved specs at named paths before Stories 6.2, 6.4, and 6.6 implement them. |
| High-risk NFR traceability | NFR1-NFR4, NFR7, NFR10-NFR11, and NFR14-NFR17 map to concrete story coverage and persisted-evidence validation. |
| UX readiness | `ux.md` covers Sample accepted-submission behavior, Tenants projection-confirmed states, Admin unavailable-operation behavior, support-safe states, accessibility/localization evidence, and FrontComposer/Fluent UI V5 governance. |
| Readiness rerun | Implementation readiness is re-run after PRD, architecture, UX, story splits, and high-risk NFR traceability are reconciled. |

## Oversized Or Mixed Stories

| Story | Required treatment |
| --- | --- |
| 1.3 | Split read-model store, cursor codec, test fake/conflict semantics, and Tenants adoption unless accepted as one coordinated slice. |
| 1.6 | Split Sample adoption, Tenants query/read-model adoption, Tenants event/projection adoption, and governance guardrails unless accepted as one coordinated slice. |
| 2.4 | Split Tenants generated contract/API-host work from UI alignment and compatibility validation. |
| 3.7 | Split CI gate migration from supply-chain backlog hardening. |
| 5.6 | Split AppHost sidecar loading, production DAPR component parity, topology tests, and documentation unless accepted as a coordinated topology-hardening slice. |
| 7.2 | Split claims normalization, audit logging, deferred-operation UI/server honesty, and shared typed-client reduction. |
| 7.3 | Split secret-store use, readiness/app-health checks, DAPR resiliency policy, and immutable image tags. |
| 7.4 | Split CI integration-test lane recovery, persisted state evidence assertions, fake/integration test reclassification, and perf-lab workflow setup. |
| 7.5 | Treat as backlog planning or define exact deliverables for GDPR-1, IAM-1, KIT-1, and REST generator hardening. |

## High-Risk NFR Coverage

| NFR | Primary story coverage |
| --- | --- |
| NFR1 | 5.2, 5.3, 5.5, 7.2 |
| NFR2 | 2.4, 5.2, 5.5, 5.6 |
| NFR3 | 5.3 |
| NFR4 | 5.3, 7.3 |
| NFR7 | 4.1, 4.2, 4.4, 4.5, 5.1 |
| NFR10 | 3.1, 7.4 |
| NFR11 | 3.6 |
| NFR14 | 2.3, 2.4 |
| NFR15 | 7.2 |
| NFR16 | 7.4 |
| NFR17 | 5.6, 7.3 |

## Counter-Metrics

- Do not optimize for fewer stories if that preserves unreviewable multi-concern stories.
- Do not count API smoke responses as integration evidence where persisted state-store, read-model, or CloudEvent evidence is required.
- Do not satisfy UI readiness by documenting intent only; UI stories still need component/governance evidence in `ux.md` and tests.
