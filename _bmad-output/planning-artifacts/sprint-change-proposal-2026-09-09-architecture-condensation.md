---
title: 'Architecture spine condensation — restored and retired clauses'
date: '2026-09-09'
type: 'sprint-change-proposal'
status: 'recorded'
subject: '_bmad-output/planning-artifacts/architecture.md'
raised_by: 'code review of story-4.15 Group F'
---

# Architecture Spine Condensation — Restored And Retired Clauses

## Why this record exists

Commit `e302432c` condensed `architecture.md` from 609 to 439 lines. `.memlog.md:106` records the
intent as "preserve AD-1 through AD-25 and add only new stable IDs", and
`reviews/reconcile-update-2026-09-09.md:5` states the update "preserves `AD-1` through `AD-25`". In
fact only the AD identifier register was preserved; `reviews/review-update-2026-09-09-rubric-walker.md:127`
scores the same property **Fail** ("AD-22, AD-24, and AD-25 lost load-bearing authorization/ownership/protocol
detail").

This file reconciles that contradiction: it lists what was restored to the spine and what is
deliberately retired, so no clause silently disappears.

## Restored to the spine (2026-09-09)

These were restored because their absence can cause a wrong action, not merely a less detailed document.

| Clause | Where | Why it is fail-closed |
|---|---|---|
| OQ8 evidence ownership: Stories 4.14-4.15 assemble `4-8-eventstore-oq8-platform-evidence.yaml`; Folders retains `oq8-idempotency-evidence.yaml` and OQ8 closure | AD-25 | The spine's only statement of the EventStore-vs-Folders split that Story 4.15's Approach and AC2 depend on. Without it, platform completion reads as OQ8 closure. |
| Omitting `AddEventStoreDaprServiceInvocation` is **currently fail-open** — no compile error, startup validation, or runtime diagnostic; structural host scans required | AD-18 rule 6 | Removing the disclosure does not remove the fail-open behavior; it only removes the reason anyone scans for it. |
| Handler registered last/innermost; no per-host DAPR routing handler; no bare `TryAddWithoutValidation` | AD-18 | An outer bearer or forwarding handler otherwise wins. |
| 14-package inventory and the Story 8.8 atomic 14→16 transition; `UseHexalithProjectReferences` unset/false is package intent in every configuration including Debug | AD-11 | Release validation rejects missing or extra `.nupkg` files against this exact count. |
| Release tags, conforming and failed, are never re-pointed or deleted; `v3.75.0` and `v3.94.1` remain resolvable as non-authorizing failed evidence | AD-11 | Nothing else forbids re-pointing a failed release tag. |
| Epic 3 story dispositions (3.13 rejected `v3.94.1`, 3.14 corrective, 3.15 positive closure, 3.16 open) and "Planning or story status never authorizes release, deployment, consumer removal, or positive `v3.94.1` closure" | Design Paradigm | A fail-closed authorization rule, not story procedure. Epic 3 is still `in-progress` and FR36 is open. |
| FR/NFR identifiers in the Capability To Architecture Map's first column | Capability map | The frontmatter declares `binds: FR1-FR37`; without the column no requirement traced to an area. |

## Deliberately retired (not restored)

Retired as genuine condensation. Each remains recoverable at `git show 12d2dfc1:_bmad-output/planning-artifacts/architecture.md`.

- **AD-22 dated scoped exceptions** — the 2026-07-27 Story 2.12 identity-gate relief and the 2026-08-16
  Story 3.13 `v3.94.1` amendment. Both stories are closed; the generic AD-22 rule ("planning status confers
  no removal authority") carries the live constraint. The dated records live in
  `sprint-change-proposal-2026-08-16-*.md` and the Story 2.12 spec.
- **AD-11 prose expansion** — the full OCI descriptor-graph, provenance-label, and `ReleaseIdentity`
  enumerations. The shape has one authority (the SHA-pinned shared Builds publisher/validator); restating it
  in the spine created a second copy that could drift.
- **AD-16 `DevelopmentHealthResponseWriter` restriction and AD-17 rule 4** (no `Location` on mapped
  gateway failures) — behavior-level detail owned by the implementing code and its guardrail tests.
- **AD-21 `event-store-admin` module identity, `eventstore-admin-ui` resource identity, and the
  FrontComposer catalog-variable rule** — the catalog rule is enforced by
  `references/Hexalith.Builds/Props/Directory.Packages.props` and the UX rubric review (UX-DR2), which
  still asks the UX spine to state both identifiers. **Open item:** `Hexalith.FrontComposer.Contracts.UI`
  has no catalog entry, so the cross-repo prerequisite is real and is now recorded only here.
- **Stack table rows** — `Aspire.Hosting.Keycloak` / `Aspire.Hosting.Kubernetes`
  `13.5.3-preview.1.26425.3`, `Microsoft.CodeAnalysis` `5.9.0`, `Hexalith.Commons.UniqueIds`,
  `NBomber` / `NBomber.Http`, and the `OpenTelemetry.Instrumentation.StackExchangeRedis` prerelease note.
  All pins remain live and governed by the Builds catalog, which is their single source of truth.
- **AD-12/AD-13 enumerations** — the AD-25 evidence obligation list and the Stories 1.18/1.19
  baseline-before-6.3/6.4 sequencing. Both are carried by the sprint tracker and the OQ8 crosswalk.

## Related corrections in the same pass

- `architecture.md` frontmatter returned to `status: draft`, matching the three reviews committed with it
  (`reconcile-update` CHANGES REQUIRED, `rubric-walker` CHANGES REQUIRED, `adversarial-divergence` FAIL).
- AD-26 relabelled `[ASSUMPTION]`, closing reconciliation finding R9. It selects a production target
  (self-managed Kubernetes, `state.postgresql` v1, `oq8-postgresql-v1`, OpenBao) that is not delivered
  reality; the production actor state-store provider decision remains open.

## Authority

This record grants no release, deployment, consumer-migration, package, pin, submodule, or external
repository authority, and does not close any Epic 3, Epic 4, or OQ8 gate.
