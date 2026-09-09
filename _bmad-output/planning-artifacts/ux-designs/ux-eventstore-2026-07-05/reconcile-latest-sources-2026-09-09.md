# Latest-Source Reconciliation — 2026-09-09

## Scope

This update reconciles the UX spines against repository revision
`23a722a1ffe29099a9d87df266552be4e3addd82` plus the current input-snapshot
digests for the canonical PRD, architecture, epics, the PRD's bound readiness report,
the existing UX memlog, and the 2026-09-09 UX validation findings.

## Source Disposition

| Input | Disposition |
|---|---|
| docs/brownfield/architecture.md | Retained as observed brownfield scope. |
| _bmad-output/planning-artifacts/prd.md | Current product and readiness authority; digest recorded in EXPERIENCE.md. |
| _bmad-output/planning-artifacts/architecture.md | Current technical-decision authority; digest recorded in EXPERIENCE.md. |
| _bmad-output/planning-artifacts/epics.md | Current story and UX-DR ownership authority; digest recorded in EXPERIENCE.md. |
| PRD validation report dated 2026-09-09 | Added as the bound blocked / reject readiness evidence. |
| Implementation-readiness report dated 2026-07-05 | Removed from normative UX sources as historical and superseded. |
| Official Fluent UI Blazor V5 URL | Retained as upstream component-system reference; it does not authorize local theme redefinition. |
| Existing UX validation and reviews | Reconciled as review evidence, not promoted to upstream source authority. |

## Findings Rolled Into The Drafts

- Token completeness: inherited color, typography, and radius values are no
  longer restated as local tokens; the source-required density spacing remains.
- Component coverage: shell, navigation, page, tabs, grid, status, loading,
  dialog, palette, refresh, and protected-outcome patterns bind to named
  FrontComposer or Fluent V5 primitives in both spines.
- Source authority: current digests, reviewed revision, and the distinction
  between UX finality and implementation readiness are explicit.
- Identity: Admin API eventstore-admin, UI identity eventstore-admin-ui,
  FrontComposer module event-store-admin, assembly, label, and package
  prerequisites are separated.
- Routes: all Story 7.14 routes plus live /types are mapped; router/tab/history,
  malformed, denied, and cross-tenant behavior are defined.
- Evidence: FR4/NFR8 own provenance/lifecycle; FR36 is parity closure. Source,
  observed-at, last-refresh, horizon, clock basis, version, terminal evidence,
  and stable operation identity are required without inventing a numeric horizon.
- Safety: privileged actions freeze and revalidate scope; refresh and mutation
  retry are separate; unknown outcomes prohibit resubmission.
- State coverage: auth expiry, wrong scope, revocation, provider failure,
  conflict, cancellation, timeout, and Unknown join the existing load, empty,
  stale, denied, unavailable, accepted, pending, and terminal states.
- Protected data: typed deleted, missing, denied, unavailable, malformed,
  tampered, and opaque outcomes replace generic redaction. Sensitive and
  idempotency material is prohibited across every client channel.
- Accessibility: exact component semantics, two owned live regions, refresh
  stability, disabled reasons, target-size floor, forced colors, reduced motion,
  320 CSS pixel reflow, 400% zoom, 200% text, and localization test conditions
  are explicit.
- Flow coverage: named flows now cover projections, topology, storage/snapshots,
  settings, and general deep-link arrival in addition to the prior six journeys.

## Superseded Or Dropped Ideas

- Historical readiness evidence as current authority.
- A local EventStore palette, typography ramp, or radius system.
- Free-text color recipes presented as mechanically bindable tokens.
- Generic component alternatives where a current primitive exists.
- Runnable forms, jobs, progress, or success states for deferred operations.
- Treating SignalR, HTTP 202, ETags, elapsed time, or toasts as completion.
- Copying legacy Fluent v4/FAST variables or navigation geometry from promoted
  mockups.

No qualitative user idea was dropped; the established operations-first Fluent
direction and full administrative surface scope remain intact.

## Finalization Decisions

The live `/types` route belongs to Streams & Events as the Type Catalog view,
preserving its events, commands, and aggregates inner tabs. Story 7.14 and
UX-DR4 must implement the placement without a duplicate route implementation.

Overview and Commands remain the key-screen visual set. Every other dashboard
surface is fully specified by the design and experience spines without a
separate mock. The optional fresh multi-lens validation was skipped; existing
review files remain historical pre-update evidence.

## Final Status

The Overview and Commands key-screen artifacts have been regenerated as
non-copyable desktop and narrow-screen references. Both spines are final as UX
contracts; this status authorizes no implementation, release, deployment,
migration, or readiness claim.
