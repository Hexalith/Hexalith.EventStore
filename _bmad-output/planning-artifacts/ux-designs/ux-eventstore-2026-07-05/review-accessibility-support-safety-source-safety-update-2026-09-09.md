# Accessibility & Support-Safety Review — Source-Safety Update 2026-09-09

## Overall verdict

**Adequate, but not ready to finalize without resolving three high-impact gaps.** The draft now has unusually strong fail-closed evidence semantics: it separates acceptance from confirmation, protects tenant and authentication boundaries, gates audit before effect, forbids blind idempotency/catalog/fence retry, and keeps recovery conditional on delivered, environment-ready capability evidence. WCAG 2.2 AA implementation is plausible with the named FrontComposer and Fluent UI V5 primitives, but the contract still leaves validation announcements, support-safe identifier bounds, and two product-surface assumptions open to incompatible implementations.

Finding counts: **critical 0 · high 3 · medium 6 · low 1**.

## High findings

### H1 — Form validation is required but not specified as an accessible interaction

The mutation progression rejects invalid and oversized input, and the accessibility floor says components expose “validation,” but neither defines the error relationship, summary behavior, or focus sequence (`EXPERIENCE.md:157-168`, `EXPERIENCE.md:192`, `EXPERIENCE.md:292-295`). This drops the explicit upstream requirement for inline association plus a polite validation summary (`_bmad-output/planning-artifacts/epics.md:248`, `_bmad-output/planning-artifacts/epics.md:254`, `_bmad-output/planning-artifacts/epics.md:5950-5958`). A downstream implementation could block submission while leaving a screen-reader or keyboard user without the field, message, and correction path.

*Fix:* Add a Validation error state and Operation dialog rule that requires a persistent localized inline message, programmatic association from each field, invalid state exposure, one deduplicated polite summary after submit, and deterministic focus to the summary or first invalid field. Specify that rejected sensitive input is not repeated in either message and add role/name/relationship/focus/live-message assertions.

### H2 — “Support-safe” operational identifiers have no closed display contract

The evidence contract permits a stable operation/audit identity and conditionally renders `MessageId`, `CorrelationId`, and request ID, but calls them merely “support-safe” (`EXPERIENCE.md:149-151`). The support-safety section says identifiers are allow-listed and bounded without naming the allowlist, bounds, truncation, clipboard/export, URL, or accessible-name policy (`EXPERIENCE.md:325-329`). At the same time, the Operation dialog is required to display applicable identities (`DESIGN.md:158`; `EXPERIENCE.md:192`). This leaves the most security-sensitive rendering decision to each component and does not fully operationalize UX-DR38 (`_bmad-output/planning-artifacts/epics.md:264`).

*Fix:* Add a field-level safe-presentation table bound to Story 7.5 DTOs: exact field/type, maximum accepted and rendered length, full versus abbreviated presentation, bidi isolation, permitted URL/query use, accessible-name treatment, copy/export/log policy, and the safe reason class used on rejection. Include operation/audit reference as well as message, correlation, and request identity. Require server-side allow-listed DTO projection so forbidden values never reach browser serialization, prerender/hydration state, or client memory merely to be hidden at render time.

### H3 — Open capability decisions are contradicted by promoted “current” visuals

Restore/import visibility and tenant provisioning are explicitly unresolved assumptions (`EXPERIENCE.md:74`, `EXPERIENCE.md:333`, `EXPERIENCE.md:355-358`; `reconcile-source-safety-update-2026-09-09.md:37-38`, `reconcile-source-safety-update-2026-09-09.md:61-64`). Yet the promoted dashboard mock shows a `system` value in a Tenants evidence row (`mockups/dashboard-overview.html:133-136`), while AD-27 requires reserved `system` to fail before lookup (`EXPERIENCE.md:105`), and the command mock presents `CreateTenant` as completed (`mockups/command-investigation.html:123-126`) while the IA says provisioning is not exposed. The spines-win rule limits normative impact, but DESIGN calls these artifacts “current composition references” (`DESIGN.md:172`), so they can still seed unsafe fixtures, demos, or screenshots.

*Fix:* Resolve both assumptions before finalization. For each capability, record one definitive disposition: hidden with no disclosure, authenticated read-only unavailable surface, or delivered conditional surface with canonical route, state, accessibility, and journey coverage. Replace the mock fixtures with non-reserved, in-scope examples and remove `CreateTenant` unless provisioning is confirmed; keep a visible non-copyable notice but do not rely on it to cure contradictory operational content.

## Medium findings

### M1 — The two-region live-announcement model conflates incompatible priorities

The draft assigns one view region to polite route/refresh/freshness messages and one operation region to both polite accepted/pending/confirmed messages and assertive failure/denial/rejection (`EXPERIENCE.md:203`, `EXPERIENCE.md:294-295`). A single stable live node cannot reliably switch between polite and assertive behavior across assistive technologies, and route-level access denial may occur before an operation region exists. Validation summaries are not assigned at all. This is narrower than UX-DR33’s distinct polite and assertive responsibilities (`_bmad-output/planning-artifacts/epics.md:254`).

*Fix:* Define stable announcement destinations by priority and scope—for example, view/operation status regions with polite behavior, a shell-available critical alert region for denial and terminal destructive failure, and a form-scoped polite validation summary. State ownership, `role`/`aria-live`/atomicity, clearing, deduplication, and visible-message linkage so the same transition is not announced twice.

### M2 — Contrast and forced-color claims are requirements, not verified evidence

DESIGN requires AA text contrast, 3:1 non-text contrast, forced-color boundaries, and non-color state cues (`DESIGN.md:105-113`), and EXPERIENCE extends this across light, dark, system, and forced/high-contrast modes (`EXPERIENCE.md:289-300`). That is the correct contract, but the visual set contains only default light-theme static renders and cannot verify hover/focus/disabled/selected states, dark mode, forced colors, or component contrast. The imported documentation screenshot identifies Fluent `5.0.0-RC.4`, while the owning Builds catalog currently pins `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.5-26219.1` (`references/Hexalith.Builds/Props/Directory.Packages.props:226-227`). Story 7.20 correctly remains backlog and acknowledges missing conformance (`_bmad-output/planning-artifacts/epics.md:5931`, `_bmad-output/planning-artifacts/epics.md:5940-5943`).

*Fix:* Keep the empty local color map and inherited-token posture, but record the resolved Builds-catalog Fluent version in test evidence and run the Story 7.20 component/browser matrix on that version. Capture computed foreground/background/boundary/focus combinations for every semantic badge, banner, selected state, disabled state, and lifecycle mapping in light, dark, system, and forced colors. Treat the RC4 imports as composition references only or regenerate them against the resolved package/docs build.

### M3 — Horizontal tabs and grids lack a complete keyboard/reflow contract

The draft permits horizontally scrolling tabs and grid-contained two-dimensional overflow (`DESIGN.md:130`, `DESIGN.md:152`; `EXPERIENCE.md:186`, `EXPERIENCE.md:297-309`), but does not require the newly focused/selected tab or cell to scroll into view, define Home/End/arrow behavior, expose overflow instructions, or preserve the user’s scroll position on route/refresh. The narrow renders visibly clip the tab strip and evidence columns, so this behavior is load-bearing rather than theoretical (`mockups/dashboard-overview-mobile.png`; `mockups/command-investigation-mobile.png`).

*Fix:* Bind tabs and grid navigation to the resolved Fluent V5 keyboard model, require focus/selection to be scrolled fully into view without motion when reduced motion is active, provide an accessible overflow cue/instruction where native discovery is insufficient, and preserve both axes across refresh and detail-panel moves. Test keyboard-only and screen-reader browse/interaction modes at 320 CSS px and 400% zoom.

### M4 — Canonical state text and localized labels are not separated

Status badges are required to expose “canonical state text” (`DESIGN.md:156`; `EXPERIENCE.md:190`), while every visible and accessible-name/status string must be resource-backed (`EXPERIENCE.md:315-321`; `_bmad-output/planning-artifacts/epics.md:256-258`). It is unclear whether `Current`, `EvidencePending`, `TimedOut`, and safe reason classes are invariant identifiers or localized labels. Implementers could either expose English enum names to assistive technology or translate machine state identifiers used by tests and diagnostics.

*Fix:* Define a two-layer status contract: invariant canonical ID in typed state and stable selectors, plus a complete localized visible/accessibility label and localized consequence. Never build accessible names by concatenating the ID, count, and translated fragments; use complete resource strings with safe bounded arguments.

### M5 — Authentication recovery and safe return routing are underspecified

Unauthenticated/session-expired/provider-unavailable states clear protected state and forbid fake login, but the “configured authentication recovery” is unnamed (`EXPERIENCE.md:219-224`). Deep links restore filters and route state (`EXPERIENCE.md:78-83`, `EXPERIENCE.md:460-467`), creating an implementation choice about whether a denied or expired session persists a return URL containing tenant/filter context. The contract does not say which route state may survive authentication recovery.

*Fix:* Define the implemented recovery primitive, accessible name, busy/failure behavior, and focus destination. Permit only same-origin canonical routes and allow-listed safe state in a return target; strip rejected, cross-tenant, wildcard, reserved, oversized, and opaque paging values before persistence. If interactive OIDC remains deferred, specify the exact non-interactive recovery/escalation action instead of a generic placeholder.

### M6 — Text-spacing conformance lacks the testable override profile

The accessibility floor says text-spacing overrides preserve content and controls (`EXPERIENCE.md:297`), but gives no values. That is not enough for consistent WCAG 2.2 SC 1.4.12 evidence across dense grids, badges, tabs, dialogs, and truncated metadata.

*Fix:* Name the test profile: line height at least 1.5 times font size, paragraph spacing at least 2 times font size, letter spacing at least 0.12 times font size, and word spacing at least 0.16 times font size. Require no loss, clipping, overlap, inaccessible truncation, or off-screen focus under those overrides, including localized long strings and RTL.

## Low findings

### L1 — “Page or selected-tab title” permits ambiguous heading ownership

DESIGN says `FcPageHeader` renders page and selected-tab titles and exposes exactly one focusable heading (`DESIGN.md:119`); EXPERIENCE says “one focusable page or selected-tab title” (`EXPERIENCE.md:291`). This could produce two visible titles with unclear heading levels or let the selected `tab` substitute for the route’s `h1`, weakening landmarks and deep-link focus behavior.

*Fix:* State that each canonical route has exactly one `h1` route title and focus target; a selected tab remains a tab with selected/current semantics and is not a second page heading. Define subordinate panel/dialog heading levels relative to that route title.

## Strengths

- The evidence and mutation state machine is exceptionally clear about accepted versus confirmed, status refresh versus mutation retry, audit `prepare` before effect, and recovery after an ambiguous effect (`EXPERIENCE.md:155-168`).
- Tenant isolation fails before routing, autocomplete, lookup, state access, or existence disclosure and explicitly rejects reserved `system` and wildcard inference (`EXPERIENCE.md:83-105`, `EXPERIENCE.md:327`).
- Expired idempotency and unknown/corrupt/ambiguous catalog or fence evidence have bounded, non-retryable outcomes with raw keys, digests, signatures, fences, and prior intent excluded (`EXPERIENCE.md:231-234`, `EXPERIENCE.md:266-273`).
- Projection fan-out and erasure claims remain per-route/per-facet and cannot collapse partial or unknown evidence into success (`EXPERIENCE.md:170-174`, `EXPERIENCE.md:233-234`).
- Focus stability, dialog modality/return, reduced motion, burst coalescing, persistent terminal outcomes, 320 CSS px reflow, zoom, and forced-color perception are all explicitly recognized (`EXPERIENCE.md:281-300`).
- The named Fluent V5 inheritance points are implementable at the API level: inspection of the pinned RC5 package found `FluentTabs.ActiveTabId`, `FluentDataGrid` focus/sort/loading/pagination APIs, modal `FluentDialog`, and the expected `BadgeColor` values (`Brand`, `Danger`, `Important`, `Informative`, `Severe`, `Subtle`, `Success`, `Warning`). All named FrontComposer components in DESIGN frontmatter were also found in the declared local FrontComposer source.

## Verification notes

- Scope was the current draft `DESIGN.md` and `EXPERIENCE.md`, `.memlog.md`, frontmatter sources, the 2026-09-09 source-safety reconciliation, promoted mocks/imports, repository UX instructions, and the resolved Fluent UI V5/FrontComposer API surface. Older reviews and consolidated reports were treated as historical only.
- This was a read-only contract review. No spine, memlog, source, mock/import, existing review, report, product code, or test was changed.
- No browser, axe, screen-reader, keyboard, zoom, forced-color, dark-theme, localization, or contrast suite was run. Accordingly, this review validates contract coverage and implementability—not implemented WCAG conformance. Story 7.20 itself records those implementation/evidence gates as backlog (`_bmad-output/planning-artifacts/epics.md:5931`, `_bmad-output/planning-artifacts/epics.md:5990-6003`).
- Current Fluent verification used the repository’s authoritative Builds-catalog pin `5.0.0-rc.5-26219.1`; the imported documentation images are older RC4 captures and cannot verify that package’s rendered accessibility.
