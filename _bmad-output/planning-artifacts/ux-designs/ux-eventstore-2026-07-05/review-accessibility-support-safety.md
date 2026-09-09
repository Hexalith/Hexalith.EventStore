# Accessibility & Support-Safety Review — Hexalith.EventStore Admin

## Overall verdict

**Adequate with one high-impact handoff gap.** The Admin contract is unusually strong on evidence honesty, tenant non-disclosure, sensitive-data exclusion, focus stability, live-region discipline, reflow, forced colors, and destructive-action revalidation. It is not yet a clean WCAG 2.2 AA handoff across the full declared scope because the Sample consumer flow has no accountable presentation/conformance owner, and several product-specific focus and composite-widget decisions remain implicit.

This is a contract review, not an implementation audit. Story 7.20 is explicitly backlog, so absent browser, assistive-technology, axe, contrast, zoom, or forced-color evidence is not itself a finding (`_bmad-output/planning-artifacts/epics.md:5917-5932`, `:5990-6003`).

## Scope checked

- `.memlog.md`, `DESIGN.md`, and `EXPERIENCE.md`, including Admin, Sample, and Tenants journeys.
- Local frontmatter sources: `docs/brownfield/architecture.md`, the canonical PRD, architecture spine, epic/story plan, and bound PRD validation report. Existing UX review files and consolidated validation reports were not treated as source truth.
- Promoted desktop/mobile Overview and Commands mockups and Fluent UI reference imports. They are illustrative and non-copyable; the spines remain authoritative (`DESIGN.md:172`, `mockups/dashboard-overview.html:9-14`, `mockups/command-investigation.html:9-14`).
- Repository UX baseline: `references/Hexalith.AI.Tools/hexalith-ux-instructions.md:1-39`.
- Official material: [WCAG 2.2](https://www.w3.org/TR/WCAG22/), [APG Grid](https://www.w3.org/WAI/ARIA/apg/patterns/grid/), [Tabs](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/), [Modal Dialog](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/), and [Accordion](https://www.w3.org/WAI/ARIA/apg/patterns/accordion/).

## Critical

None.

## High

### Sample accepted-submission UX has no accountable presentation and accessibility closure owner

**Citations:** `EXPERIENCE.md:71`, `:227-228`, `:312`, `:357-373`; `_bmad-output/planning-artifacts/epics.md:272`, `:705-743`, `:1480-1529`, `:1627-1675`.

**Impact:** The spine correctly specifies that Sample remains accepted/evidence-pending after submit and confirms only after authoritative read-model change, but traceability assigns this generally to “Epic 2 consumer stories.” Tenants has concrete lifecycle, assistive-technology, and localization acceptance; Sample stories only prove host/client/API boundaries. Downstream planning can therefore close every named story while leaving Sample's pending, timeout/no-resubmit, confirmed, focus, and live-announcement behavior unimplemented or untested.

**Fix:** Add a consumer-surface conformance matrix and map Sample's complete accepted → evidence-pending → confirmed/unknown flow to one explicit story owner. Require persistent visible state, polite transition announcements, timeout/no-resubmit behavior, focus stability, keyboard operation, and resource-backed copy. Keep Tenants Story 2.6's existing ownership explicit.

## Medium

### Detail panel versus drawer semantics are contradictory

**Citations:** `DESIGN.md:61-64`, `:136`, `:159-160`; `EXPERIENCE.md:175-176`, `:260-261`, `:266`, `:321`, `:343`; `mockups/command-investigation.html:118-163`.

**Impact:** The binding chooses a labelled non-modal `aside` with `FluentCard`, while journeys call the same surface a “detail drawer,” and the accessibility floor lists drawer semantics without defining modality. An implementer cannot tell whether focus stays in the grid, moves into an inline region, enters a non-modal disclosure, or is trapped in a modal overlay; narrow-screen behavior can therefore obscure focus or expose background controls incorrectly.

**Fix:** Choose one model per viewport/state and name it consistently. For an inline `aside`, specify announcement, entry, dismissal, and return without a focus trap. For an overlay drawer, specify modality, initial focus, `Escape`/close, background inertness, focus-not-obscured behavior, and stable return fallback.

### The WCAG 2.2 AA boundary does not disposition inherited or absent interaction classes

**Citations:** `EXPERIENCE.md:200-205`, `:247`, `:255-268`, `:297`; `DESIGN.md:111-113`, `:132`; `_bmad-output/planning-artifacts/epics.md:5935-5958`, `:5990-6003`; WCAG 2.2 SC [2.4.11](https://www.w3.org/TR/WCAG22/#focus-not-obscured-minimum), [2.5.7](https://www.w3.org/TR/WCAG22/#dragging-movements), and [3.3.8](https://www.w3.org/TR/WCAG22/#accessible-authentication-minimum).

**Impact:** The spine gives strong rules for target size, reflow, contrast, keyboard access, and status messages, but “targets WCAG 2.2 AA” does not identify which requirements are inherited, locally owned, absent/not applicable, or delegated. Authentication recovery is mentioned while interactive OIDC remains deferred, author-created dragging is neither prohibited nor given a single-pointer alternative, and fixed/sticky shell content is not explicitly included in the no-obscuration rule. This is not evidence of a current violation, but it leaves Story 7.20's conformance matrix to invent product policy.

**Fix:** Add a concise WCAG ownership/applicability table, at least for WCAG 2.2 additions and inherited shell/authentication behavior. Prohibit author-created dragging unless an equivalent single-pointer action exists; require shell/navigation/banners/drawers never to fully obscure focus; bind accessible authentication to FrontComposer or the configured identity-provider owner, including paste/password-manager-compatible recovery where authentication UI exists.

### Evidence-grid keyboard behavior is too generic for the product-specific row/action model

**Citations:** `EXPERIENCE.md:168-171`, `:175`, `:247`, `:250`, `:260`, `:266`, `:268`; `DESIGN.md:48-50`, `:155`; `_bmad-output/planning-artifacts/epics.md:5950-5953`; [APG Grid Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/grid/).

**Impact:** “Fully operable” plus inherited `FluentDataGrid` behavior does not settle whether evidence surfaces are static tables with tabbable actions or interactive grids with cell focus, arrow navigation, row selection, and an enter/exit mode for controls. Product-specific row selection, one row-action location, detail opening, paging, refresh, and potential virtualization can diverge by surface even while every control is technically reachable.

**Fix:** Define the canonical grid profile or explicitly adopt the relevant Fluent V5 profile. Specify tab stops, arrow/Home/End behavior when grid semantics are used, row/detail activation, sort/page announcements, entry/exit for embedded controls, selection semantics, and focus fallback when filtering, paging, authorization change, or refresh removes the focused row.

### Route and tab activation lack one deterministic focus-entry rule

**Citations:** `EXPERIENCE.md:75`, `:164-168`, `:246`, `:250`, `:259-263`, `:414-419`; `DESIGN.md:119`; [APG Tabs Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/).

**Impact:** The contract requires a focusable title, route announcements, route-derived tabs, and predictable focus, while bookmarked arrival focuses the title. It does not say whether tab activation retains focus on the selected tab, moves focus to the new title, or varies for deep links, Back/Forward, redirects, and failures. Different reasonable implementations can create unexpected focus movement or duplicate title/route announcements.

**Fix:** Add a focus-entry table for initial/deep-link arrival, tab activation, detail navigation, browser history, compatibility redirects, denial, and route failure. Distinguish programmatically focusable `tabindex="-1"` headings from sequential tab stops, and state when the route live region announces versus when focus movement supplies context.

## Low

None.

## Strengths

- Accepted, evidence-pending, timeout/unknown, terminal, and projection-confirmed outcomes remain distinct; refresh never resubmits (`EXPERIENCE.md:146-156`, `:207-211`, `:248-253`).
- Mutations freeze context, revalidate principal/scope/pre-state/freshness/effect/risk/reversibility, clear protected transient input on mismatch, and preserve safe focus recovery (`EXPERIENCE.md:148-156`, `:174`, `:251-252`).
- Tenant existence protection covers routes, filters, autocomplete, counts, cached rows, timing, and denied outcomes (`EXPERIENCE.md:77-100`, `:200-205`, `:215-228`, `:295`).
- Sensitive-data exclusion spans DOM, accessibility tree/properties, URLs/history, clipboard/export, logs, telemetry, exceptions, and caches (`DESIGN.md:184`; `EXPERIENCE.md:186`, `:289`, `:291-297`).
- The contract covers 320 CSS px, 400% zoom, 200% text, text spacing, grid-contained overflow, forced colors, dark/system themes, reduced motion, 24px minimum targets, and RTL/pseudo-locales (`DESIGN.md:111-113`, `:130-132`; `EXPERIENCE.md:255-288`).
- Live regions are view/operation-scoped, transition-only, deduplicated, and backed by persistent visible terminal outcomes (`EXPERIENCE.md:185`, `:262-263`).

## Mechanical notes

- Severity counts: **Critical 0 · High 1 · Medium 4 · Low 0**.
- No `wireframes/` directory is present. Promoted mocks and imports are referenced by `DESIGN.md`; `.working/` artifacts remain process files.
- The mockups are non-copyable references and do not fully implement inherited Fluent composite-widget behavior. Missing tabpanel linkage, roving tab focus, grid behavior, and production live-region dynamics in those files were not scored as implementation defects.
- FrontComposer/Fluent inheritance is not implementation evidence. WCAG conformance still depends on composition, content, focus management, state updates, and end-to-end testing.
