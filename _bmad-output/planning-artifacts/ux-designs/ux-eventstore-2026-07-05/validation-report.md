# Validation Report - eventstore

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- **Run at:** 2026-07-05T09:13:56+02:00

## Overall verdict

The spine pair is directionally sound for the brownfield EventStore Admin UX: it commits to the single `Event Store Admin` module entry, tabbed child dashboard, FrontComposer/Fluent UI V5, projection-confirmed success, and support-safe operational states.

It is not yet readiness-clean as a downstream contract. The main blockers are concrete token definitions, component-name alignment, visual reference promotion, canonical `ux.md` packaging, and explicit source coverage for Sample UI and Tenants UI.

## Category verdicts

- Flow coverage - thin
- Token completeness - broken
- Component coverage - thin
- State coverage - adequate
- Visual reference coverage - broken
- Bloat & overspecification - adequate
- Inheritance discipline - thin
- Shape fit - adequate

## Findings by severity

### Critical (1)

**Token completeness** - Color tokens are not concrete (`DESIGN.md:14-27`)

Color tokens do not provide hex values, exact inherited CSS variables, or applicable light/dark pairs. The rubric treats this as critical because downstream code mirrors the spine.

Fix: Define each color token as a concrete Fluent UI V5/Fluent 2 token reference with resolved light/dark values where available, or as an exact inherited CSS variable with fallback.

### High (4)

**Flow coverage** - Sample UI and Tenants UI source requirements are implicit (`prd.md:48`, `prd.md:243-244`, `prd.md:352`; `EXPERIENCE.md:156-203`)

The current key flows focus on the consolidated EventStore Admin dashboard. The PRD requires Sample accepted-submission behavior and Tenants projection-confirmed states in the UX artifact.

Fix: Add named flows or a surface coverage table for Sample accepted submission and Tenants projection-confirmed/support-safe behavior.

**Component coverage** - Component taxonomy is inconsistent (`DESIGN.md:49-77`, `DESIGN.md:130-142`, `EXPERIENCE.md:67-83`)

Behavioral components in `EXPERIENCE.md` do not all have matching visual rows in `DESIGN.md`, and several design components are absent or differently named in the experience spine.

Fix: Create one canonical component matrix with identical names in both spines and visual plus behavioral rules for each.

**Visual reference coverage** - Visual artifacts are orphaned (`.memlog.md:8`, `.memlog.md:22`)

Screenshots and key mocks exist under `.working/`, but there are no formal `mockups/` or `wireframes/` assets and no inline spine links describing what each visual illustrates.

Fix: Promote accepted visuals, link them inline from the relevant sections, and state that the spines win on conflicts.

**Architecture/readiness** - Canonical `ux.md` handoff is missing (`prd.md:79`, `prd.md:352`)

The PRD defines `_bmad-output/planning-artifacts/ux.md` as the UX artifact. The current output exists only under `ux-designs/ux-eventstore-2026-07-05/`, so readiness tooling may still report UX not found.

Fix: Create `_bmad-output/planning-artifacts/ux.md` as the canonical handoff, either composed from the two spines or linking to them with an accepted artifact map.

### Medium (12)

**Flow coverage** - Several tabs lack representative journeys (`EXPERIENCE.md:35-46`, `EXPERIENCE.md:156-203`)

Topology, Storage & Snapshots, Settings, and Projections have IA placement but no concrete operator journey.

Fix: Add a tab-to-flow coverage matrix or short journeys for each dashboard tab.

**Token completeness** - Radius and spacing tokens are partly semantic (`DESIGN.md:38-48`)

Values such as "Fluent default small radius" and "shell default content padding" leave implementers guessing.

Fix: Use exact token names, CSS variables, or fixed dimensions for load-bearing values.

**Token completeness** - Contrast targets are not stated (`DESIGN.md:88-98`, `EXPERIENCE.md:114-127`)

The spines inherit Fluent contrast but do not state the target for status badges, banners, disabled states, and stale evidence.

Fix: Add WCAG 2.2 AA contrast expectations and named token combinations for load-bearing states.

**State coverage** - Tab-specific state coverage is thin (`EXPERIENCE.md:85-101`)

Global states are good, but some tabs have unique empty, degraded, partial-permission, and read-only cases.

Fix: Add a compact tab-state matrix.

**Visual reference coverage** - Raw HTML mocks could be mistaken for implementation guidance (`.working/key-dashboard-overview.html`, `.working/key-command-investigation.html`)

The repo requires FrontComposer and Fluent components; raw HTML mocks should stay clearly non-implementation.

Fix: Label them as validation scaffolding or replace them with FrontComposer/Fluent-oriented mock descriptions.

**Inheritance discipline** - Experience rules do not resolve to design tokens (`EXPERIENCE.md:19`, `EXPERIENCE.md:67-83`, `EXPERIENCE.md:114-127`)

The experience spine references `DESIGN.md` generally but does not use token paths for dependent visual decisions.

Fix: Reference exact `DESIGN.md` token paths where behavior relies on visual state.

**Inheritance discipline** - Source terms are paraphrased rather than mapped (`prd.md:76-79`, `prd.md:212-213`, `EXPERIENCE.md:85-101`)

Traceability would be stronger if "Projection-Confirmed Success", "Support-Safe State", `NFR14`, and `NFR15` appeared in a source coverage table.

Fix: Add a traceability table mapping source terms and NFRs to UX sections and flows.

**Accessibility/support-safety** - Localization evidence is too thin (`prd.md:91`, `prd.md:352`, `EXPERIENCE.md:65`)

Whole-string guidance is useful but does not define acceptance evidence for localization.

Fix: Add resource-backed string, locale-check, and no sentence-assembly rules.

**Accessibility/support-safety** - Mobile mutation fallback is underspecified (`EXPERIENCE.md:112`, `EXPERIENCE.md:129-137`, `EXPERIENCE.md:207-209`)

The UX says complex mutations may be desktop-optimized but still accessible; it does not say what happens on small screens.

Fix: Define whether each mutating operation is fully usable, disabled with reason, or desktop-required.

**Accessibility/support-safety** - Denied-state accessible copy and focus need rules (`EXPERIENCE.md:76`, `EXPERIENCE.md:95-96`, `EXPERIENCE.md:181`)

The UX protects tenant existence, but screen-reader users still need unambiguous state and route context.

Fix: Add denied-state accessible name/copy rules and focus restoration for denied filters, denied rows, and denied detail links.

**Architecture/readiness** - Deferred operation visibility needs a stricter policy (`architecture.md:103-107`, `prd.md:212-213`, `EXPERIENCE.md:45`, `EXPERIENCE.md:98`)

The `Deferred & Backlog` tab can be support-safe, but only if visibility and role rules prevent unavailable work from looking functional.

Fix: Define hidden vs disabled vs backlog-visible behavior by role and release state.

**Architecture/readiness** - NFR and AD traceability table is missing (`architecture.md:67-71`, `architecture.md:91-95`, `architecture.md:127-131`, `prd.md:212-213`, `epics.md:131-133`)

The decisions are represented, but not mapped for downstream verification.

Fix: Map each requirement/decision to UX states, components, flows, and test evidence.

### Low (4)

**Component coverage** - Singular/plural naming differs (`DESIGN.md:72`, `DESIGN.md:140`, `EXPERIENCE.md:81`)

`operations-dialog` and "Operation dialog" likely mean the same component.

Fix: Use one slug and display name everywhere.

**Bloat & overspecification** - Deferred & Backlog could become a planning page (`EXPERIENCE.md:45`, `EXPERIENCE.md:195-203`)

The tab should remain an honest operational capability-discovery surface.

Fix: Limit it to unavailable-operation discovery and safe backlog links.

**Shape fit** - Spines are still marked draft (`DESIGN.md:4`, `EXPERIENCE.md:3`)

That is acceptable for validation but not for final readiness handoff.

Fix: Promote status after findings are addressed.

**Accessibility/support-safety** - Live-region priority is not classified (`EXPERIENCE.md:123`)

Command lifecycle, stale data, SignalR disconnect, and evidence confirmation should not all announce with equal urgency.

Fix: Classify updates as polite or assertive and define when focus moves.

## Reviewer files

- `review-rubric.md`
- `review-accessibility-support-safety.md`
- `review-architecture-readiness.md`
