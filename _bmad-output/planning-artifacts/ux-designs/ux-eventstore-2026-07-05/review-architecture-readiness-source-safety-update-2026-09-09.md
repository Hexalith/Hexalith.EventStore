# Architecture / Readiness Alignment Review — Source-Safety Update

**Review date:** 2026-09-09  
**Lens:** Architecture authority, implementation slicing, source drift, and readiness-claim safety  
**Reviewed spines:** `DESIGN.md`, `EXPERIENCE.md`  
**Supporting evidence:** `.memlog.md`, `reconcile-source-safety-update-2026-09-09.md`, and every local source declared in the two spines' frontmatter  
**Repository revision:** `0994c37814c37dac7667a209dbd0659125aac49e`

## Overall Verdict

**Blocked for UX finalization; safe as a draft.** The spines are materially aligned with the current architecture and do not claim that adopted decisions, routes, DTOs, projects, or UX finality prove delivery or implementation readiness. They correctly preserve the PRD's current `blocked` / `reject` posture and the architecture's draft state.

Finalization is nevertheless unsafe until three high-impact source conflicts are resolved: tenant provisioning contradicts Story 5.10, `/types` is absent from Story 7.14's supposedly closed route manifest, and the canonical top-level UX handoff/index still advertise the prior final revision and say fresh validation was skipped. Three medium findings require explicit disposition or contract tightening. Counts: **0 critical, 3 high, 3 medium, 0 low**.

This verdict concerns the UX artifact only. It cannot change the product's implementation-readiness result; the PRD remains `blocked` / `reject`, and its blocking baseline, authority, lifecycle, correctness, and exit-gate refinements still require reconciliation and a new readiness run (`_bmad-output/planning-artifacts/prd.md:5-9`, `:85`, `:511-563`).

## Findings By Severity

### High

#### H1 — Removing tenant provisioning contradicts the authoritative Story 5.10 UX obligation

The draft limits **Tenants & Access** to visibility and role changes and repeatedly states that no provisioning control is assumed (`EXPERIENCE.md:65`, `:74`, `:245`, `:357-358`, `:375-383`). Story 5.10, however, explicitly requires the existing **Create Tenant** dialog to apply the reserved-`system` guard, associate an accessible inline error with the tenant-ID field, restore focus, and send no request (`_bmad-output/planning-artifacts/epics.md:4182-4198`, `:4207-4210`, `:4227-4230`). The new reconciliation already identifies this conflict (`reconcile-source-safety-update-2026-09-09.md:37`, `:64`).

**Downstream impact:** Finalizing the current spine would tell Story 7.14/7.19 implementers to omit a surface that Story 5.10 treats as an existing, required security boundary. It would also remove the UI acceptance surface needed to prove the reserved-name guard.

**Fix guidance:** Before finalization, either:

1. include tenant provisioning in **Tenants & Access** as a target/backlog-gated flow whose Create Tenant dialog follows Story 5.10 and AD-27, without implying the guard is delivered; or
2. formally amend/supersede Story 5.10 and its current-UI reconciliation through the product/epic authority chain.

A user preference recorded only in the UX memlog is not sufficient to override the epic acceptance contract.

#### H2 — `/types` is canonical in the UX but absent from Story 7.14's machine-validated route manifest

The draft correctly retains the observed live `/types` surface under **Streams & Events** and explicitly calls the story gap out (`EXPERIENCE.md:51`, `:63`, `:87`, `:107`). Story 7.14 claims ownership of the canonical route migration but its exhaustive acceptance list omits `/types` (`_bmad-output/planning-artifacts/epics.md:5545-5549`, `:5577-5585`). The new reconciliation also marks this as a required upstream correction (`reconcile-source-safety-update-2026-09-09.md:30`, `:43`).

**Downstream impact:** A machine validator implemented literally from Story 7.14 can reject or orphan a route that the UX declares canonical, or create a second route implementation to preserve it. Either result violates AD-21's single consolidated UI rule (`_bmad-output/planning-artifacts/architecture.md:231-235`).

**Fix guidance:** Add `/types` and its `events` / `commands` / `aggregates` inner-tab policy to Story 7.14's manifest acceptance before declaring the UX final, then repin/reapprove the epics input through the governed source-reconciliation path. If upstream authority instead rejects `/types`, revise the UX route, IA, journeys, and legacy redirect disposition together; do not leave the conflict as prose-only implementation advice.

#### H3 — Canonical handoff artifacts still claim the superseded final state and skipped validation

The current spines are `status: draft` at revision `0994c378…` (`DESIGN.md:4-7`; `EXPERIENCE.md:3-6`), and the memlog records that the prior finalization was reopened (`.memlog.md:31-38`). In contrast, `_bmad-output/planning-artifacts/ux.md` still says `Status: final`, pins revision `23a722…`, and is the canonical handoff named by PRD/architecture (`ux.md:3-7`, `:19-25`; `_bmad-output/planning-artifacts/prd.md:91-96`; `_bmad-output/planning-artifacts/architecture.md:14-20`). The folder `index.md` also says `Status: final`, pins `23a722…`, and states that fresh multi-lens validation was skipped (`index.md:3-8`, `:39-41`). `reconcile-latest-sources-2026-09-09.md` carries the same superseded final/skipped-review claim (`:75-85`).

**Downstream impact:** Architecture and implementation consumers that follow the declared canonical `ux.md` can ingest a finality/revision/validation state that directly conflicts with the authoritative spines and this review run.

**Fix guidance:** Keep `ux.md`, `index.md`, and `reconcile-latest-sources-2026-09-09.md` historical or explicitly mark the update in progress while findings are being resolved. At finalization, republish the handoff/index against revision `0994c378…` (or the then-current reviewed revision), name this fresh validation and its synthesized result, and make every status agree with the two spines. Do not overwrite historical review meaning without labelling supersession.

### Medium

#### M1 — Restore/import disposition is source-compatible but still an unresolved UX phase decision

The draft assumes restore and import remain hidden because neither has a separate canonical route or useful read-only surface (`EXPERIENCE.md:74`, `:333`, `:357`, `:395-403`). Story 7.4 permits hidden treatment when no useful tracking context exists, but it also requires a closed inventory covering backup restore and stream import/export, with every legacy deep link and retained endpoint assigned a canonical dashboard destination and disposition (`_bmad-output/planning-artifacts/epics.md:4879-4905`, `:4907-4925`). The brownfield `/backups` implementation contains restore/import controls, so this is a migration decision, not absence of legacy scope.

**Downstream impact:** Without an explicit decision, Story 7.4 and 7.14 cannot close their control/route inventories deterministically; implementers can disagree about whether history belongs on `/backups`, Deferred & Backlog, or nowhere.

**Fix guidance:** Confirm one disposition before finalization. If hidden, state that existing `/backups` restore/import controls are removed and direct/legacy entry attempts resolve to the canonical unsupported/read-only treatment. If useful history exists, specify its read-only owner and exact “Unavailable in this release.” treatment. No runnable form, file picker, dialog, job, progress, accepted state, or mutation may survive while deferred.

#### M2 — AD-17 traceability omits its defining absolute-`Location` contract

The draft correctly says `MessageId` is the sole command-status lookup key, `CorrelationId` is diagnostic, and UI code never constructs a status URL from an identifier (`EXPERIENCE.md:151`, `:343`). It does not state AD-17's defining external response rule: a generated accepted command emits a trusted, gateway-authoritative **absolute** `Location` only when valid, otherwise omits it; generated hosts never map the status endpoint (`_bmad-output/planning-artifacts/architecture.md:199-204`; `_bmad-output/planning-artifacts/prd.md:214-218`). Calling the current row “AD-17 — command-status authority” therefore overstates coverage.

**Downstream impact:** A Sample/Tenants UX or typed-client implementer could synthesize a relative/dangling link, treat a missing `Location` as failure, or accidentally make correlation select command status.

**Fix guidance:** Extend the evidence/navigation contract and traceability row: consume a valid absolute gateway-authored `Location` when the applicable generated API supplies it; tolerate omission; never synthesize or repair it in the UI; and retain `MessageId` as the only status selector.

#### M3 — AD-32's exact correlation boundary is not captured

The draft says correlation is bounded, support-safe, non-status identity and rejects malformed/over-limit identifiers (`EXPERIENCE.md:149-151`, `:385-393`). AD-32 is more exact: `X-Correlation-ID` is 1–128 ASCII alphanumeric or hyphen characters, is accepted or minted only at the first public boundary, is propagated without reminting, is never GUID-parsed, and never selects status (`_bmad-output/planning-artifacts/architecture.md:316-320`).

**Downstream impact:** UI validation and typed-client behavior can diverge from the shared boundary, especially around punctuation, Unicode, reminting, and replayed support links.

**Fix guidance:** Add the exact grammar and propagation/no-remint rule to the Evidence and Mutation Contract or command-investigation route contract, while continuing to avoid echoing rejected input. Add AD-32 explicitly to source traceability.

## Aligned Decisions

- **Readiness honesty:** The spines distinguish UX-document finality from implementation, release, deployment, migration, or readiness and preserve the current PRD `blocked` / `reject` posture (`DESIGN.md:91-95`; `EXPERIENCE.md:22`, `:35-47`). This aligns with the PRD and architecture (`prd.md:85`, `:511-563`; `architecture.md:73-77`, `:446-465`).
- **Identity and ownership:** `src/Hexalith.EventStore.Admin.UI`, assembly `Hexalith.EventStore.Admin.UI`, Admin API `eventstore-admin`, UI service/resource/DAPR/container `eventstore-admin-ui`, module `event-store-admin`, and label **Event Store Admin** are separated correctly (`DESIGN.md:91`; `EXPERIENCE.md:24-33`). This matches the brownfield inventory and Story 7.14 (`docs/brownfield/architecture.md:70-80`; `epics.md:5557-5570`).
- **Single-host route model:** One FrontComposer module, route-derived tabs, canonical history/deep-link behavior, and no duplicate page/router are consistent with AD-21 and Story 7.14 (`EXPERIENCE.md:55-113`; `architecture.md:231-235`; `epics.md:5567-5605`), subject to H2.
- **Delivery and recovery gating:** Dead-letter retry/archive is conditional on actual route delivery plus active-environment topology, capture-before-ack, authorization, audit, catalog, and evidence gates; route, DTO, or project existence is not treated as capability proof (`DESIGN.md:186`; `EXPERIENCE.md:53`, `:157-168`, `:248`, `:362-373`). This safely reconciles Story 7.19's action language with AD-31's unwired/non-production service status (`architecture.md:310-314`, `:462`; `epics.md:5892-5895`).
- **Audit phases and attribution:** The bounded operator/service/delegation facts, `prepare` before effect, `effect`, `commit`, and `recovery`, fail-closed audit uncertainty, and status-only ambiguous-outcome recovery align with AD-29 and Story 7.3 (`EXPERIENCE.md:159-168`, `:192`, `:226-230`; `architecture.md:298-302`; `epics.md:4834-4867`).
- **Projection fan-out and rebuild:** Each configured route exposes its checkpoint plus `advanced`, `not advanced`, `retry`, or `failure`; partial fan-out is never aggregate success, and paged rebuild retains the last complete live model (`DESIGN.md:155`; `EXPERIENCE.md:146`, `:170-174`, `:189`, `:233`, `:423-431`). This aligns with AD-19 and AD-20 (`architecture.md:219-229`).
- **Erasure boundary:** MVP projection read-model/checkpoint removal is kept separate from full erasure, and overall full/GDPR completion is blocked by any pending, unknown, or failed facet (`EXPERIENCE.md:170-174`, `:234`, `:331`, `:349`). This matches AD-7 and AD-30 (`architecture.md:127-131`, `:304-308`).
- **Tenant boundary:** Routes, filters, dialogs, requests, and failure behavior apply one explicit lowercase 1–64-character AD-27 tenant, reject reserved `system`, and forbid inferred wildcard scope before lookup/disclosure (`EXPERIENCE.md:85`, `:89`, `:105`, `:277`, `:327`). This matches AD-27 (`architecture.md:286-290`), subject to H1's missing provisioning surface.
- **Idempotency/catalog/fence safety:** Expired-key, unknown/corrupt/ambiguous catalog or fence, active-generation/readiness, no-raw-material, no-blind-retry, and fail-closed mutation behavior are present (`EXPERIENCE.md:127-128`, `:147`, `:162-168`, `:204`, `:231-232`, `:266-273`, `:329`). These are directionally aligned with AD-25, AD-26, AD-28, and AD-33 (`architecture.md:257-284`, `:292-296`, `:322-328`).
- **Deferred and protected capability honesty:** Story 7.4's exact unavailable copy and zero-fake-workflow rule are preserved, and post-MVP payload protection/full erasure vocabulary does not claim delivery (`EXPERIENCE.md:200`, `:243-250`, `:273`, `:333`; `epics.md:4879-4940`).

## Source-Drift Notes

- The SHA-256 values recorded in `EXPERIENCE.md:39-45` match the current local bytes of all five declared local sources. The reviewed repository revision also matches `HEAD` at review time.
- The canonical PRD is `status: final` as a requirements document but has separate machine-readable `implementation_readiness_status: blocked` and `implementation_readiness_result: reject` fields (`prd.md:1-9`). The UX preserves that distinction.
- Architecture is currently `status: draft` (`architecture.md:1-10`). Adopted AD labels are decisions, not implementation evidence (`architecture.md:73-77`). The UX correctly avoids using adoption as delivery proof.
- The bound PRD validation report is snapshot evidence of `Poor` / `Reject`, not current clause authority (`validation-report.md:1-13`). The draft's source-authority paragraph treats it accordingly (`EXPERIENCE.md:35-47`).
- The epics digest repin is documented as metadata-only in `reconcile-source-safety-update-2026-09-09.md:20-24`, but that does not close the PRD's OR14 approval-chain blocker (`prd.md:548`). The UX correctly makes no downstream-readiness claim from the digest match.
- Story 5.2 lifecycle remains disputed across planning/tracker evidence under PRD OR15 (`prd.md:549`). The UX should continue to treat authorization/body-limit behavior as a target contract and must not describe Story 5.2 as delivered until the owning sources reconcile.
- Historical review files and the prior finalization record must remain historical. This fresh report evaluates the reopened `status: draft` source-safety update only.

