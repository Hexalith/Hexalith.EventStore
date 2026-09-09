# Validation Report — eventstore

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- **Run at:** 2026-09-09T10:51:58+02:00
- **Selected lenses:** rubric walker; accessibility & support-safety; architecture readiness

## Overall verdict

The pair has a strong document shape and coherent operational model, but it is not yet a clean downstream contract. Its principal blockers are unbindable color tokens, contradictory source authority, and component/platform bindings that no longer commit to the published FrontComposer primitives; journey, state, and visual coverage are also incomplete.

The additional lenses materially raise the handoff risk. Architecture cannot safely consume a spine that cites an obsolete `ready` report beside the current blocked/reject baseline, while regulated/support-critical implementation needs explicit scope freezing, component semantics, refresh/retry controls, typed redaction, reflow, and evidence-expiry behavior. The review reports findings, not defects proven in the running product.

No overall grade is assigned. Reviewer-reported counts are retained without deduplicating overlaps:

| Reviewer | Critical | High | Medium | Low | Total |
|---|---:|---:|---:|---:|---:|
| Rubric walker | 1 | 4 | 9 | 6 | 20 |
| Accessibility & support-safety | 1 | 10 | 7 | 1 | 19 |
| Architecture readiness | 1 | 6 | 5 | 2 | 14 |
| **Combined** | **3** | **20** | **21** | **9** | **53** |

## Category verdicts

- Flow coverage — **adequate**
- Token completeness — **broken**
- Component coverage — **thin**
- State coverage — **adequate**
- Visual reference coverage — **thin**
- Bloat & overspecification — **adequate**
- Inheritance discipline — **thin**
- Shape fit — **strong**

## Findings by severity

Repeated findings are retained when another reviewer adds a distinct downstream consequence.

### Critical (3)

**[Rubric / Token completeness] — Color tokens are not mechanically bindable** (`DESIGN.md:14-32`)

All 18 color values are free-text recipes rather than hex values or exact Fluent 2 token/component references. This fails the token contract even though the product correctly avoids a custom palette.

Fix: omit inherited colors or encode exact published Fluent 2 token/parameter bindings in a spec-compliant inheritance form.

**[Architecture readiness] — Declared sources provide contradictory implementation authority** (`DESIGN.md:4-13`; `EXPERIENCE.md:3-12`; `implementation-readiness-report-2026-07-05.md:1-34`; `prd.md:3-11`)

The spines cite a July `ready` report while the current PRD and validation state are `blocked` / `reject`, without an authority order, reviewed SHA, or digests.

Fix: replace or explicitly demote the historical report, declare one-way authority and reviewed revisions/digests, and clarify that `status: final` does not authorize implementation.

**[Accessibility & support-safety] — Privileged operations can be confirmed against stale or wrong scope** (`EXPERIENCE.md:94-105,153-155,203-214`)

Mutation dialogs do not freeze and revalidate acting principal, environment, tenant, target, authoritative pre-state, blast radius, or reversibility; the mocks normalize an “All tenants” context.

Fix: display and freeze those facts, revalidate on submit, convert any mismatch/revocation/expiry to a non-submitting conflict, clear protected transient input, and require explicit acknowledgement for irreversible or cross-scope actions.

### High (20)

**[Rubric / Token completeness] — Local typography ramp conflicts with Fluent UI V5 governance** (`DESIGN.md:34-57,184-188`)

Fix: bind roles to exact `FluentText`/FrontComposer parameters or Fluent 2 typography tokens.

**[Rubric / Component coverage] — Canonical components are not bound to current FrontComposer contracts** (`DESIGN.md:77-155,217-231`)

`Dashboard shell`, tabs, evidence grid, detail panel, and command palette retain generic or multiple alternatives despite published FrontComposer primitives.

Fix: select exact FrontComposer primitives where available and state one bounded fallback condition.

**[Rubric / Inheritance discipline] — Source inheritance is circular and includes historical readiness evidence** (`DESIGN.md:6-11`; `EXPERIENCE.md:6-11`)

Fix: establish one-way authority, remove downstream artifacts from normative sources, and label or replace the old readiness report.

**[Rubric / Inheritance discipline] — Post-final source and platform changes are unreconciled** (`prd.md:303-335`; `architecture.md:370-425`; `EXPERIENCE.md:199-205`)

Fix: reconcile FR37/NFR19/AD-23–25 and current FrontComposer contracts, including the ban on rendering or logging idempotency material.

**[Architecture readiness] — Host, service, container, and module identities are conflated** (`DESIGN.md:164,216-219`; `EXPERIENCE.md:19-35,92-95`)

Fix: add an identity table for assembly, Admin Server `eventstore-admin`, UI container `eventstore-admin-ui`, FrontComposer module `event-store-admin`, and visible label; name Shell/Contracts.UI package prerequisites.

**[Architecture readiness] — The route inventory drops live `/types` behavior** (`EXPERIENCE.md:37-54`; `TypeCatalog.razor:1`)

Fix: assign `/types` and its event/command/aggregate tabs to a canonical destination and propagate the correction to UX-DR4 and Story 7.14.

**[Architecture readiness] — Legacy routes are not a canonical routing contract** (`EXPERIENCE.md:39-54,95,148-152`)

Fix: add a route matrix covering canonical URL, redirects, parameters/bounds, authorization, malformed/denied behavior, selected tab/module, browser history, and router/tab synchronization.

**[Architecture readiness] — Projection lifecycle behavior lacks a bound Admin transport** (`DESIGN.md:140-147,228-229`; `EXPERIENCE.md:104-129`)

Fix: name the typed Admin facet/DTO fields for provenance, lifecycle, evidence time, version, and terminal command evidence; distinguish operational projection status from consumer lifecycle and fall back to `Unknown`.

**[Architecture readiness] — Authentication and revocation transitions are missing** (`EXPERIENCE.md:21-23,119,133-144,199-214`)

Fix: define unauthenticated, expired, denied, wrong-scope, revoked-during-action, and provider-unavailable outcomes, including cache clearing, background-work cancellation, focus/route behavior, safe recovery, and telemetry redaction.

**[Architecture readiness] — Promoted mocks encode prohibited Fluent token families** (`mockups/dashboard-overview.html:8-27`; `mockups/command-investigation.html:8-26`)

Fix: regenerate with Fluent 2/component roles or remove implementation-like token declarations and add an unmistakable non-copy banner.

**[Accessibility & support-safety] — Final spines are not revision-bound to current obligations** (`DESIGN.md:4-13`; `EXPERIENCE.md:3-12,157-197`; `epics.md:5917-6003`)

Fix: record source revisions/digests and validation identity, regenerate current traceability, and close Story 7.20’s component/state/evidence matrix.

**[Accessibility & support-safety] — Named components lack implementable semantics** (`EXPERIENCE.md:90-107,146-170`)

Fix: specify tab, accordion, grid, dialog, and badge name/role/value/relationship, keyboard, focus, modality, selection, busy, row-action, and live-status semantics.

**[Accessibility & support-safety] — Live regions lack ownership, deduplication, throttling, and persistence** (`EXPERIENCE.md:151,172-180`)

Fix: define one scoped view region and one operation region, announce transitions only, suppress initial/unchanged refresh chatter, coalesce events, and keep terminal outcomes visibly persistent.

**[Accessibility & support-safety] — Automatic refresh can disrupt reading** (`EXPERIENCE.md:113-116,146-155,172-180`)

Fix: provide keyboard-operable pause/frequency control, bounded cadence, manual refresh, and preservation of focus, scroll, filters, selection, expansion, and open dialogs.

**[Accessibility & support-safety] — Timeout recovery conflates status refresh with mutation retry** (`EXPERIENCE.md:104-118,153,274-284`)

Fix: separate actions, permit retry only after authoritative retryable terminal evidence, preserve approved operation identity, and persist “Outcome unknown—do not resubmit” with safe tracking evidence.

**[Accessibility & support-safety] — State coverage excludes Sample/Tenants and conflates failure classes** (`EXPERIENCE.md:31-35,109-145,274-296`)

Fix: close every surface across load/refresh/empty/stale/offline/unavailable/auth/denied/revoked/unknown/conflict/pending/timeout/failure, with focus, announcements, mutation gates, disclosure, and recovery.

**[Accessibility & support-safety] — Redaction is not an assistive-technology-safe data boundary** (`EXPERIENCE.md:98,123,199-205`)

Fix: keep sensitive bytes out of DOM/accessibility properties/tooltips/URLs/clipboard/export/logs/telemetry and map each typed unreadable outcome to bounded localized copy and an authorized safe reason code.

**[Accessibility & support-safety] — Breakpoints do not define WCAG reflow and zoom behavior** (`EXPERIENCE.md:189-197`)

Fix: bind acceptance to 320 CSS px, 400% zoom, 200% text, text-spacing overrides, logical flow, labelled grid overflow, visible focus, and retained context; add narrow product references.

**[Accessibility & support-safety] — Product mocks contradict the keyboard/semantic contract** (`mockups/dashboard-overview.html:269-339`; `mockups/command-investigation.html:244-315`)

Fix: make primary controls semantic and keyboard-operable or annotate exact Fluent components and mandatory behavior, including skip link, focus, navigation toggle, labels, row actions, and status regions.

**[Accessibility & support-safety] — `Current` evidence has no expiry/observation contract** (`DESIGN.md:140-147`; `EXPERIENCE.md:96,104-129`)

Fix: require canonical source, observed-at and last-refresh times, freshness horizon, and clock basis; stale, invalid, expired, or provenance-mismatched evidence becomes `Unknown`/`Stale` and disables mutation.

### Medium (21)

**[Rubric / Flow coverage] — Several IA surfaces lack Key Flows** (`EXPERIENCE.md:41-54,225-296`)

Fix: add projection-rebuild, topology, storage/snapshot, and settings journeys or narrow the closure claim.

**[Rubric / Token completeness] — Issue-banner appearance borrows badge semantics** (`DESIGN.md:117-122`)

Fix: specify `FluentMessageBar` intent/variant semantics directly.

**[Rubric / State coverage] — Sample and Tenants are absent from the surface matrix** (`EXPERIENCE.md:31-35,111-144`)

Fix: add their cold, empty, stale/offline, denied, error, and mutation states.

**[Rubric / State coverage] — Shell/route failure and request timeout/cancellation are implicit** (`EXPERIENCE.md:114,284`)

Fix: add support-safe state rows including focus and selected-navigation behavior.

**[Rubric / State coverage] — Typed payload-protection outcomes collapse to generic redaction** (`prd.md:335`; `EXPERIENCE.md:123,201`)

Fix: map EventStore-visible outcomes and mark consumer-owned/non-UI cases.

**[Rubric / Visual reference coverage] — Mock CSS uses prohibited legacy variables while claiming Fluent emission** (both mock files, lines 8-27)

Fix: replace the variables or mark the CSS as non-implementable illustration.

**[Rubric / Visual reference coverage] — References are not contextual and two PNGs are orphaned** (`DESIGN.md:166`; `EXPERIENCE.md:25`)

Fix: link each promoted artifact beside the rule it illustrates and retain one precedence statement.

**[Rubric / Visual reference coverage] — Mock navigation geometry conflicts with current FrontComposer navigation** (both mock navigation blocks)

Fix: re-render against `FrontComposerShell`/`FrontComposerNavigation` or explicitly exclude navigation geometry.

**[Rubric / Inheritance discipline] — Stable module identity `event-store-admin` is missing** (`DESIGN.md:164,216`; `EXPERIENCE.md:21,91,300`)

Fix: state it beside the separate `eventstore-admin-ui` resource/container identity.

**[Architecture readiness] — IA-to-flow closure is false for several tabs** (`EXPERIENCE.md:37-54,225-296`)

Fix: add projection, topology, storage/snapshot, and settings flows or explicitly classify those tabs as spine-only/read-only.

**[Architecture readiness] — Command/recovery terminal evidence is not source-bound** (`DESIGN.md:135-139`; `EXPERIENCE.md:101,104,117-121,153-154`)

Fix: identify accepted, terminal, projection, audit, observation, timeout/cancellation, and retry evidence for every mutation.

**[Architecture readiness] — Responsive action disposition remains unresolved** (`EXPERIENCE.md:155,189-197,300-302`)

Fix: add a per-tab retained-context/column/overflow/action matrix and align mocks to the 960/1280 contract.

**[Architecture readiness] — Traceability omits current owners and requirement distinctions** (`EXPERIENCE.md:56-69`)

Fix: add UX-DR/decision/owning-story IDs, separate provenance from projection execution and parity proof, and record the extracted baseline digest.

**[Architecture readiness] — “Topology” refers to two operator concepts** (`EXPERIENCE.md:43,46,140`; `NavMenu.razor:29-56`)

Fix: assign the tenant/domain navigator to Streams & Events or retire/rename it; reserve Topology for DAPR/service operations.

**[Accessibility & support-safety] — Focus rules do not close deep-link, panel, tab, or disappearing-trigger cases** (`EXPERIENCE.md:148-150,162-168`)

Fix: define focus entry, tab retention, panel focus, validation targeting, background-update stability, and stable return fallbacks.

**[Accessibility & support-safety] — Non-text contrast and forced-color behavior are unspecified** (`DESIGN.md:168-180`)

Fix: require WCAG non-text contrast across light/dark/system/forced-colors with explicit focus/lifecycle fallbacks.

**[Accessibility & support-safety] — Dense controls lack a target-size floor** (`DESIGN.md:71-105`)

Fix: require 24×24 CSS px or a documented WCAG exception, with 44×44 preferred for touch-critical actions.

**[Accessibility & support-safety] — Reduced-motion rules omit loading and movement primitives** (`EXPERIENCE.md:113,169`)

Fix: disable nonessential shimmer, auto-scroll, and transitions while retaining static progress/state text.

**[Accessibility & support-safety] — Disabled safety reasons can become unreachable** (`EXPERIENCE.md:105-106,126-155,207-214`)

Fix: provide persistent, programmatically associated reason text plus a reachable safe next action.

**[Accessibility & support-safety] — Localization omits accessibility-only and support-safe formatting rules** (`EXPERIENCE.md:71-84,182-187`)

Fix: resource all accessible/live text, define locale/time-zone/plural/duration formatting, isolate identifiers, and test fallback, pseudo-locale, RTL, and safe truncation.

**[Accessibility & support-safety] — Toast behavior is absent despite false-success risk** (`EXPERIENCE.md:90-107,172-180`)

Fix: keep authoritative state outside toasts, use neutral accepted copy, and make toasts pauseable, dismissible, focus-safe, persistent enough, and deduplicated.

### Low (9)

**[Rubric / Flow coverage] — No journey demonstrates a general legacy deep-link arrival** (`EXPERIENCE.md:52,272`)

Fix: add a bookmarked arrival including selected module/tab state.

**[Rubric / Token completeness] — Non-text contrast is not explicit for focus, borders, and lifecycle indicators** (`DESIGN.md:137-139,178`)

Fix: state the WCAG 2.2 AA 3:1 floor and define tracker foregrounds.

**[Rubric / Component coverage] — Skeleton and inline-validation behaviors lack paired component rows** (`EXPERIENCE.md:113,124,179`)

Fix: add rows or state explicit Fluent-default inheritance.

**[Rubric / Visual reference coverage] — The Overview mock omits Settings** (`dashboard-overview.html:282-292`)

Fix: add the tab or annotate the omission.

**[Rubric / Bloat] — Host identity and deferred-operation policy repeat** (multiple spine sections)

Fix: keep one authoritative definition and cross-reference it.

**[Rubric / Inheritance discipline] — Canonical state casing drifts** (`EXPERIENCE.md:66`)

Fix: normalize lowercase `unknown` to `Unknown`.

**[Architecture readiness] — Canonical state casing is inconsistent** (`EXPERIENCE.md:65-67,105,129`)

Fix: use exact contract identifiers everywhere and localize display strings separately.

**[Architecture readiness] — Adopted decisions remain labelled as assumptions** (`EXPERIENCE.md:298-302`)

Fix: promote adopted host/identity/navigation rules and require an explicit UX/architecture update before changing tab names.

**[Accessibility & support-safety] — `data-testid` is presented as accessibility evidence** (`EXPERIENCE.md:157-170`)

Fix: move selector policy to testing/conformance and pair it with assertions of accessible role, name, value/state, relationships, focus, and live messages.

## Mechanical notes

- All six direct sources resolve; no direct source defines separately numbered user journeys.
- All 35 distinct `{path.to.token}` references resolve syntactically. Route placeholders `{tenant}`, `{domain}`, and `{aggregate}` are not design-token references; resolved colors still fail type/binding validation.
- All 16 canonical component names align across DESIGN frontmatter/prose and EXPERIENCE Component Patterns.
- DESIGN sections follow the required canonical order. EXPERIENCE contains all required defaults and the triggered Responsive & Platform and Inspiration & Anti-patterns sections.
- Visual inventory: two imports, two HTML mocks, two rendered mock PNGs, and no wireframes. The two PNG twins are not linked inline.
- No Mermaid blocks occur in either spine.
- Validation did not modify `DESIGN.md`, `EXPERIENCE.md`, their sources, imports, or mockups.

## Reviewer files

- `review-rubric.md`
- `review-accessibility-support-safety.md`
- `review-architecture-readiness.md`
