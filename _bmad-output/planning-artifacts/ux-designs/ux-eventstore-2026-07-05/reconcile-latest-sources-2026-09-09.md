# Latest-Source Reconciliation — 2026-09-09

## Scope

This update reconciles the UX spines against repository revision
e302432ca6daf3aa0436c3c0011f7baa551bb449,
the latest canonical PRD, architecture, epics, the PRD's bound readiness report,
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

## Open Assumption

[ASSUMPTION] The live /types route belongs to Streams & Events as the Type
Catalog view, preserving its events, commands, and aggregates inner tabs. The
current epics omit this route, so Story 7.14 and UX-DR4 must ratify or replace
the placement before implementation.

## Draft Status

The spines remain draft until mock coverage is confirmed, key-screen artifacts
are regenerated or deliberately retained as non-copyable references, the
optional reviewer gate is decided, and editorial polish completes.
