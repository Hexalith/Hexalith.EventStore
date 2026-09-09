# Source-Safety Update Reconciliation — 2026-09-09

## Scope

This reconciliation accompanies the draft [DESIGN.md](DESIGN.md) and [EXPERIENCE.md](EXPERIENCE.md) update at repository revision `0994c37814c37dac7667a209dbd0659125aac49e`. It updates source-derived safety behavior without changing the established visual direction, information-architecture grouping, canonical host identity, routes, or promoted mock set.

It is a reconciliation record, not a validation report. No UX or PRD validation was rerun, and the existing review and validation files were not edited.

## Source Disposition

| Input | SHA-256 / identity | Disposition |
|---|---|---|
| [Brownfield architecture](../../../../docs/brownfield/architecture.md) | `3cddf6eb593fb28d90b8dd9d54562cb28bb4ea2a4f5f15c6a51b77f06bf5a0a3` | Retained as observed legacy/runtime scope, not target readiness authority. |
| [PRD](../../prd.md) | `b99effdb414209da373433d9b4d2075072ca950e89534b22b81155236f650662` | Retained for product intent, scope, and current blocked/reject posture. |
| [Architecture](../../architecture.md) | `7e3dbc7bd335034bd9b98cadfed8b14650b7d811321b326b8f32e1b280960d51` | Retained for AD-7, AD-19, AD-21, and AD-25–AD-33 safety decisions and production gates. |
| [Epics](../../epics.md) | `d067c8fbffce47d7d0518396265f862093ec1a513cab73cb9fea1e88185cf33b` | Retained for implementation slices and acceptance responsibility. Drift from `5a5c03d1205ee3741978dd96501d345867860cfd13a737dfecc8c011805e1009` is metadata-only. |
| [Bound PRD validation report](../../prds/prd-eventstore-2026-07-05/validation-report.md) | `17f378d0686d9840938cf835ea9227a2163c39ba210597b2ada40f57aac801d3`; baseline `1b6f08d41de040615d3b08675d98e46cfa5bab0c`; run `2026-09-09T08:18:44+02:00` | Snapshot evidence only that the captured baseline received `Reject`. Its detailed findings are stale at the individual-finding level and do not override current sources. |
| Official Fluent UI Blazor V5 reference | `https://fluentui-blazor-v5.azurewebsites.net/` | Retained as the upstream component-system reference; it does not authorize a local theme. |

## Epics Drift Check

The epics digest changed from `5a5c03d1205ee3741978dd96501d345867860cfd13a737dfecc8c011805e1009` to `d067c8fbffce47d7d0518396265f862093ec1a513cab73cb9fea1e88185cf33b`. A direct comparison against repository revision `23a722a1ffe29099a9d87df266552be4e3addd82` shows changes only in frontmatter `inputDocumentDigests` for the PRD and UX inputs. No requirement, UX-DR, epic, story, acceptance criterion, or implementation-status body changed.

The new digest is pinned in `EXPERIENCE.md`; it does not justify a qualitative UX change by itself.

## Required Source Corrections

These upstream corrections remain necessary; this draft records the safe UX posture without editing those sources:

1. Story 7.14's machine-validated route-manifest acceptance list omits the live `/types` route. Add it under Streams & Events while preserving the existing `events`, `commands`, and `aggregates` inner tabs and one route implementation.
2. Recovery requirements and journeys that describe retry/archive as available must incorporate AD-31 delivery/production-wiring gates and AD-29 fail-closed audit attribution. A route, DTO, project, or planning story is not capability evidence.
3. Projection requirements must expose AD-19 per-route checkpoint outcomes (`advanced`, `not advanced`, `retry`, `failure`) and forbid aggregate success for partial fan-out.
4. Erasure language must preserve the AD-7 projection-removal boundary and AD-30 per-facet full-erasure rule. Projection removal is not GDPR/event/broker/backup/cryptographic erasure.
5. Tenant-bearing UX requirements must bind AD-27's single explicit lowercase 1–64-character tenant grammar, reserved `system` rejection, and prohibition on wildcard inference.
6. Mutation requirements must carry AD-29 bounded human subject/service principal/delegation, reason, issuer/expiry, request/correlation/message identities, `prepare`/`effect`/`commit`/`recovery` phases, and fail-closed audit behavior.
7. Admission, topology, and realtime UX requirements must cover expired idempotency, unknown/corrupt/ambiguous catalog or fence evidence, activated catalog generation/readiness, and over-limit/overflow SignalR metadata without raw keys, digests, fences, configuration, or blind retry.
8. Story 5.10 assumes a Create Tenant dialog, while the current UX information architecture exposes Tenants & Access for visibility and access-role changes only. Resolve that scope conflict before UX finalization.
9. Restore and import are named as deferred capabilities but have no canonical routes or confirmed useful read-only surfaces. Confirm whether they stay hidden or require a route/trackable surface before UX finalization.

## Corrections Applied To The Draft Spines

- The PRD validation report is described as snapshot `Reject` evidence only; no detailed finding is treated as current authority.
- `/types` remains under Streams & Events, and the Story 7.14 manifest omission is explicit.
- Recovery controls are conditional on delivered capability plus active-environment readiness, catalog, capture, authorization, audit, and evidence gates; otherwise they are hidden or read-only unavailable.
- Operation dialogs and mutation progression carry bounded attribution, reason, issuer/expiry, request/correlation/message identities, phase state, audit preflight, and status-only recovery after ambiguous effects.
- Projection fan-out is per-route; partial fan-out and incomplete erasure facets cannot become aggregate success.
- Tenant routes, filters, dialogs, and requests use the AD-27 canonical boundary and fail before lookup on unsafe input.
- Expired idempotency, unsafe catalog/fence evidence, oversized input, and SignalR metadata overflow use bounded fail-closed states with no sensitive material or blind mutation retry.
- Topology may show only safe activated route/idempotency catalog generation and readiness.
- Source traceability now says `UX contract coverage` and `Source implementation / gate reference`; it makes no authoritative ownership claim.
- FR37 is stated as committed post-MVP: prerequisite-spec approval does not deliver the engine/package, production backend, dual-provider parity, rollback proof, or G5.

## Retained Decisions

- `src/Hexalith.EventStore.Admin.UI` evolves in place as `eventstore-admin-ui`; one `event-store-admin` FrontComposer module remains labelled **Event Store Admin**.
- The ten dashboard tabs, route groupings, `/types` placement, and Sample/Tenants external-module boundaries remain unchanged.
- FrontComposer plus Blazor Fluent UI V5 remains the visual system. Theme roles, compact operational density, dense evidence grids, support-safe status treatment, and light/dark/system/forced-color inheritance remain unchanged.
- Overview and Commands remain the complete key-screen visual set. The promoted dashboard and command-investigation desktop/mobile artifacts remain illustrative and non-copyable; the spines win on conflict.
- The UX still invents no numeric freshness horizon, treats SignalR as a refetch signal rather than authority, and never treats HTTP `202`, a toast, elapsed time, or transport success as completion.

## Open Assumptions

- [ASSUMPTION] Restore and import remain hidden because no canonical route or useful read-only surface is currently defined. Backup and compaction remain the only Deferred & Backlog routes. User confirmation is required before finalization.
- [ASSUMPTION] Tenant provisioning is absent from the current information architecture. Tenants & Access covers authorized tenant visibility and access-role changes only. User confirmation is required before finalization.

## Qualitative Disposition

No qualitative user idea was dropped. The existing operations-first Fluent direction, consolidated dashboard, tab order, administrative surface scope, evidence honesty, responsive behavior, accessibility floor, promoted mocks, and support-safe tone are retained. The changes narrow unsafe interpretations and add explicit source gaps; they do not introduce a new visual direction or remove a requested experience.

## Validation Status

Only mechanical coverage checks were run for required sections, frontmatter fields and hashes, token references, cross-spine component names, and local artifact links. The rubric, accessibility, architecture, PRD, and other qualitative validation lenses were not rerun. Existing review files and consolidated validation reports remain historical evidence and must not be read as validation of this draft.
