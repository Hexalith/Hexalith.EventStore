# Accessibility & Support-Safety Review — Hexalith.EventStore Admin

Reviewed: 2026-09-09

## Overall assessment

**Thin and unsafe as the sole WCAG 2.2 AA implementation contract for a regulated/support-critical administration surface.** The spines establish the right posture—FrontComposer/Fluent UI V5 inheritance, text-plus-color status, fail-closed authorization, accepted-versus-confirmed separation, support-safe disclosure, focus restoration, reduced motion, and differentiated announcements—but they do not make several load-bearing behaviors testable. The most consequential omissions are mutation scope freezing, component semantics, auto-refresh control, retry fencing, typed redaction outcomes, responsive reflow, and evidence-expiry behavior.

The contract is also stale by construction. Both spines say `status: final` and `updated: 2026-08-01`, but use unversioned moving source paths; the current source now contains Story 7.20's substantially more exact accessibility, localization, viewport, and retained-evidence obligations. The product mocks are illustrative, yet they are the only product-specific visual references and model keyboard-inoperable primary controls.

## Finding counts

- **Critical:** 1
- **High:** 10
- **Medium:** 7
- **Low:** 1
- **Total:** 19

## Findings

### Critical

#### 1. A destructive or privileged operation can be confirmed against stale or wrong scope

**Location:** `EXPERIENCE.md:94-105`, `EXPERIENCE.md:153-155`, `EXPERIENCE.md:203-214`, `mockups/dashboard-overview.html:302-307`, `mockups/command-investigation.html:258-263`, `../../epics.md:5877-5885`.

**Note:** “Exact target,” permission context, confirmation, and auditability are insufficient for a multi-tenant operations surface. The contract does not require the dialog to freeze and lead with current environment/tenant, acting principal, authoritative state, blast radius, or reversibility; it does not revalidate those facts at submit; and it does not define what happens if authorization, scope, or freshness changes while the dialog is open. Both mocks normalize an “All tenants” context. A fast-moving operator can therefore confirm a retry, archive, replay, snapshot, or role change after context drift and affect the wrong tenant.

**Fix:** Require every mutation dialog to show a fixed labelled block containing acting principal/role, environment, tenant, domain, exact target, authoritative pre-state and observation time, effect, reversibility, blast radius, expected evidence, and audit reason/reference. Freeze these facts at open, revalidate at submit, and turn any mismatch, revocation, expiry, or scope change into a non-submitting conflict state that clears protected/transient input. Require explicit acknowledgement for irreversible or cross-scope actions and prohibit multi-tenant bulk mutation unless separately specified and approved.

### High

#### 2. The “final” spines are not revision-bound and no longer cover their current sources

**Location:** `DESIGN.md:4-13`, `EXPERIENCE.md:3-12`, `EXPERIENCE.md:157-197`, `../../epics.md:5917-6003`.

**Note:** The source references are mutable paths with no commit, digest, or reviewed-at revision. Current Story 7.20 requires closed component/state matrices, exact name-role-value relationships, throttled announcements, locale/pseudo-locale coverage, reflow/zoom evidence, and retained evidence identity; the spines still offer only principles and breakpoints. Downstream consumers cannot know which source revision the `final` label attests to, and old validation can be mistaken for current conformance.

**Fix:** Add source revision/digest and validation timestamp metadata, regenerate traceability against the current PRD/architecture/epics, and make `final` conditional on a closed Story 7.20 matrix. Retained evidence must identify exact repository revision, browser/tool, viewport, locale, theme, assistive-technology mode, and every skip/quarantine/exception with owner and expiry.

#### 3. Named Fluent components have no implementable semantic contract

**Location:** `EXPERIENCE.md:90-107`, `EXPERIENCE.md:146-170`, `../../epics.md:5950-5953`.

**Note:** “Keyboard-operable” and “expose accessible names and roles” do not pin the relationships and state values that wrappers, virtualization, responsive column removal, and custom composition routinely break. The spine never specifies tablist/tab/tabpanel ownership and selection, accordion heading/button/panel relationships, data-grid sort/selection/index/busy semantics, dialog labelling/modality, or whether a status badge is persistent text versus a live status message.

**Fix:** Add a component semantics matrix. At minimum: tabs use roving focus, Arrow/Home/End, selection and panel ownership; accordions expose heading/button/expanded/control relationships; grids expose headers, sort, selection, row identity/context, virtualization position, busy state, and explicit row-action activation; dialogs expose label/description/modality, initial focus, contained tab sequence, Escape/cancel, inert background, and deterministic focus return; badges remain persistent text associated with the value they qualify and are not automatically live regions.

#### 4. Live-region priority lacks ownership, deduplication, and persistence rules

**Location:** `EXPERIENCE.md:151`, `EXPERIENCE.md:172-180`, `../../epics.md:5955-5958`.

**Note:** The priority table distinguishes polite and assertive events but defines neither the owning regions nor how SignalR, polling, row rerenders, and dialogs coordinate. Nothing suppresses initial-load chatter, unchanged polls, or per-row announcements; nothing guarantees that terminal messages remain visible after the announcement. During an incident, refresh floods can obscure the failure that matters.

**Fix:** Define one scoped status region per active view and one operation-specific region. Announce only meaningful state transitions, summarize a changed refresh cycle once, suppress initial population and unchanged polls, coalesce repeated SignalR/poll events by canonical state/operation identity, and specify throttle intervals. Keep terminal outcomes persistently visible in the owning dialog/row/banner and prevent badges or virtualized rows from each becoming live regions.

#### 5. Auto-refresh can disrupt reading and has no user control

**Location:** `EXPERIENCE.md:113-116`, `EXPERIENCE.md:146-155`, `EXPERIENCE.md:172-180`, `../../epics.md:5872-5875`.

**Note:** Polling is an authoritative fallback, but the spine sets no cadence, pause/resume or frequency control, and no preservation rule for focus, scroll, row selection, sort, expanded accordion items, or an open dialog. Auto-updating evidence can replace what an assistive-technology user is reading and can silently invalidate confirmation facts.

**Fix:** Specify a visible keyboard-operable pause/resume control or user-controlled refresh frequency, the default/max cadence, and a manual refresh path. Background refresh must preserve focus, scroll, filters, selection, expansion, and open dialogs; changed dialog facts become a conflict, never a silent update. Expose refreshing non-disruptively and make any session/operation timeout adjustable or extendable where WCAG requires it.

#### 6. Timeout recovery makes refresh and mutation retry dangerously ambiguous

**Location:** `EXPERIENCE.md:104-105`, `EXPERIENCE.md:117-118`, `EXPERIENCE.md:153`, `EXPERIENCE.md:274-284`, `../../architecture.md:400-415`, `../../epics.md:5877-5880`.

**Note:** Flow 5 offers “retry/refresh” after an accepted command times out without defining authoritative non-persistence, retryability, or stable operation identity. A late-but-successful command can be submitted twice, while an ambiguous “Retry” label gives screen-reader users no reliable action consequence.

**Fix:** Split the actions. “Refresh status” must never submit. “Retry operation” may appear only after authoritative terminal retryable evidence and must preserve the approved stable operation identity. After a bounded wait, announce and persist “Outcome unknown—do not resubmit,” provide the support-safe tracking reference and evidence timestamp, and route to status/support escalation. Raw idempotency material must never enter UI evidence.

#### 7. State coverage excludes source-owned hosts and conflates materially different failures

**Location:** `EXPERIENCE.md:31-35`, `EXPERIENCE.md:109-145`, `EXPERIENCE.md:274-296`, `../../epics.md:5935-5938`.

**Note:** The per-surface matrix covers only Event Store Admin tabs although the IA and flows include Sample and Tenants dashboards. It merges stale/offline and omits per-surface refreshing, unauthenticated/expired/revoked, provider unavailable, malformed/unknown evidence, conflict, timeout/cancelled, and terminal error states. These states have different disclosure, authority, mutation, focus, and recovery consequences; collapsing them invites empty-state or optimistic-success fallthrough.

**Fix:** Add Sample and Tenants and split the dimensions. For every canonical surface define cold load, refresh, empty-visible-scope, stale, offline/disconnected, unavailable, unauthenticated/expired, denied/wrong scope, revoked mid-action, unknown/malformed evidence, conflict, accepted/pending, timeout/cancelled, terminal failure, focus target, announcement priority, mutation gate, disclosure policy, and recovery action.

#### 8. Redaction is visual language, not an assistive-technology-safe data boundary

**Location:** `EXPERIENCE.md:98`, `EXPERIENCE.md:123`, `EXPERIENCE.md:199-205`, `../../prd.md:309`, `../../prd.md:335`, `../../architecture.md:370-378`.

**Note:** “Protected” and “redacted” do not require sensitive bytes to be absent from the accessibility tree, DOM attributes, hidden descriptions, tooltips, URLs, clipboard, export, or client logs/telemetry. The contract also collapses deleted, missing, denied, unavailable, malformed, tampered, and opaque payload outcomes, although current source contracts require bounded typed outcomes. A masked secret may still be spoken or copied, and a tamper/unavailable result can be misdiagnosed as intentional protection.

**Fix:** Define a resource-backed, non-secret label, consequence, and safe next action for every typed unreadable outcome. Require sensitive bytes to be absent—not merely visually hidden—from rendered DOM, accessible properties, descriptions, clipboard/export, URLs, analytics, logs, and telemetry. Preserve a safe invariant reason code next to localized copy only when cross-locale support requires it and authorization permits it.

#### 9. Breakpoints do not constitute a WCAG reflow and zoom contract

**Location:** `EXPERIENCE.md:189-197`, `../../epics.md:5970-5988`, `imports/fluent-ui-v5-home-mobile.png`, `mockups/dashboard-overview.html:249-265`, `mockups/command-investigation.html:224-240`.

**Note:** Three width bands do not require 320 CSS-pixel reflow, 400% zoom, text-spacing overrides, enlarged system text, logical reading order, visible/unobscured focus, or containment of necessary two-dimensional scrolling to a labelled grid. The imported mobile image is generic Fluent documentation; neither product mock provides narrow product evidence. Dense grids, tabs, dialogs, and filters can therefore clip or drop critical context while still satisfying the stated breakpoint table.

**Fix:** Bind acceptance to 320 CSS px and 400% zoom, 200% text sizing and WCAG text-spacing overrides, one-dimensional page flow, labelled horizontal grid regions, active-tab visibility, no off-screen/obscured focus, and no loss of tenant/environment/freshness/confirmation context. Produce narrow product references for Overview, Commands/detail, Recovery, tenant access, and destructive/recovery mutation dialogs.

#### 10. The only product mocks contradict the keyboard and semantic contract

**Location:** `DESIGN.md:166`, `EXPERIENCE.md:25`, `mockups/dashboard-overview.html:269-339`, `mockups/command-investigation.html:244-315`.

**Note:** Tabs, filters, Search, navigation, and selectable rows are non-focusable `div` elements. The dashboard hamburger is hidden from assistive technology and is not a button; responsive CSS simply removes navigation. These mocks are labelled reference-only, but they are the sole product-specific visual examples, so downstream implementers can copy an interaction model that is inoperable by keyboard and lacks tab/panel, input, button, and row-action semantics.

**Fix:** Replace primary controls in the mocks with semantic keyboard-operable equivalents and explicit relationships, or add adjacent callouts naming the exact Fluent component and mandatory behavior. Include skip-link, visible focus, accessible navigation toggle, real labels, row action controls, status-region behavior, and narrow-screen captures. Keep the “spines win” note, but remove contradictory implementation cues.

#### 11. Authoritative evidence can remain “Current” without an expiry or observation contract

**Location:** `DESIGN.md:140-147`, `EXPERIENCE.md:96`, `EXPERIENCE.md:104-105`, `EXPERIENCE.md:113-129`, `../../epics.md:5857-5870`.

**Note:** `Current` enables otherwise-authorized mutation, yet the spine does not require every lifecycle/freshness presentation to expose authoritative evidence source, observation time, freshness horizon, clock basis, or transition to `Unknown`/`Stale` when evidence ages out. A long-open tab can retain an apparently current state and permit action after its evidence is no longer safe.

**Fix:** For every evidence-derived status, require canonical source, observed-at time, last-successful-refresh time, and route-owned freshness/expiry semantics. Expired, unparseable, clock-invalid, or provenance-mismatched evidence becomes `Unknown` or `Stale` and disables mutation before submit. Refresh and submit must re-evaluate the same canonical state; local time, ETag, cursor, SignalR, and elapsed UI time must never synthesize authority.

### Medium

#### 12. Focus rules do not close navigation, panel, and disappearing-trigger cases

**Location:** `EXPERIENCE.md:148-150`, `EXPERIENCE.md:162-168`.

**Note:** The spine promises a focusable title and return after dialogs/failures but not whether tab activation retains focus, where deep links land, whether a non-modal detail panel receives focus, or what replaces the initiator when refresh removes its row. Implementations can strand focus or overcorrect by jumping to the page title on every tab change.

**Fix:** Specify: Arrow-key tab changes retain focus in the tablist; route/deep-link entry focuses the selected view title; opening a non-modal panel focuses its heading; close returns to the row action or nearest stable grid control; validation-summary links focus the first invalid field; background updates never move focus; removal of the initiating row uses a documented stable fallback.

#### 13. Contrast requirements omit non-text indicators and forced-color behavior

**Location:** `DESIGN.md:168-180`, `mockups/dashboard-overview.html:15-28`, `mockups/command-investigation.html:15-27`, `../../epics.md:5940-5943`.

**Note:** Text/status contrast and non-color meaning are covered, but not non-text contrast for focus indicators, active tabs, grid boundaries, icons, lifecycle graphics, or disabled-state affordances across light, dark, system, and forced-color themes. The mocks also use legacy Fluent v4/FAST-style token names that the baseline explicitly forbids.

**Fix:** Require WCAG non-text contrast for required controls/state indicators and verify every state in light, dark, system, and forced colors. Specify forced-color fallbacks for focus and lifecycle distinctions. Replace legacy mock tokens with Fluent 2 roles or mark each fallback as static/non-copyable and point to the required Fluent V5 component parameter.

#### 14. Dense controls have no target-size floor

**Location:** `DESIGN.md:71-105`, `mockups/dashboard-overview.html:202-210`, `mockups/command-investigation.html:113-121`.

**Note:** Some rows/tabs have 36–40 px heights, but icon utilities, dismiss buttons, row actions, accordion toggles, tab overflow, and mobile navigation have no minimum target. The mocks normalize 32 px primary controls. Dense UI can therefore meet tokens while failing WCAG 2.5.8 for compact actions.

**Fix:** Require at least 24×24 CSS px or a documented WCAG spacing/equivalent exception for every pointer target, with 44×44 preferred for touch-critical narrow-screen actions. Include icon-only, dismiss, row, accordion, tab-overflow, and navigation controls in conformance evidence.

#### 15. Reduced-motion covers transitions but not loading and movement primitives

**Location:** `EXPERIENCE.md:113`, `EXPERIENCE.md:169`, `../../epics.md:5955-5958`.

**Note:** “Show final state directly” omits skeleton shimmer, progress animation, drawer/dialog transitions, auto-scrolling tabs, focus animation, and maintaining static progress when animation is removed. Reduced-motion users can still receive continuous motion, or lose the only progress cue.

**Fix:** Require `prefers-reduced-motion` to disable nonessential shimmer, auto-scroll, and transitions while retaining static progress/state text. Motion may never carry sole meaning. Test loading, accepted, pending, confirmed, freshness, denial, validation, and terminal failure transitions with reduced motion enabled.

#### 16. Disabled safety reasons can become inaccessible dead ends

**Location:** `EXPERIENCE.md:105-106`, `EXPERIENCE.md:126-144`, `EXPERIENCE.md:155`, `EXPERIENCE.md:207-214`, `../../epics.md:5950-5953`.

**Note:** Many actions are disabled for stale, unknown, unsupported, or narrow-screen reasons, but the explanation need not be visible and programmatically associated. A native disabled control is usually not focusable, so keyboard and screen-reader operators may never learn the reason or reach the next safe action.

**Fix:** Render persistent adjacent reason text or an issue banner, associate it through an enabled explanatory control/wrapper and `aria-describedby`, and provide a reachable next action. Never rely only on hover, `title`, color, or a disabled control's accessible description.

#### 17. Localization omits accessibility-only copy and support-safe formatting details

**Location:** `EXPERIENCE.md:71-84`, `EXPERIENCE.md:182-187`, `../../epics.md:5965-5968`.

**Note:** Resource-backed visible strings are required, but accessible names/descriptions, live announcements, validation summaries, sort/state announcements, and visually hidden helper text are not explicitly included. Time zones, durations, plurals, pseudo-locale expansion, RTL/LTR identifiers, and accessible expansion of truncated values are also ungoverned.

**Fix:** Include all accessibility-tree and live-region text in the closed resource inventory. Require culture-aware date, number, duration, and plural formatting with explicit operations time zone; keep identifiers invariant, direction-isolated, and separately labelled; test supported locales, fallback, pseudo-locale, and RTL expansion. Reveal a full truncated value only when support-safe and authorized.

#### 18. Toast behavior is absent despite explicit false-success risk

**Location:** `EXPERIENCE.md:90-107`, `EXPERIENCE.md:172-180`, `../../epics.md:5857-5860`, `../../epics.md:5877-5880`.

**Note:** Neither component/state patterns nor the announcement table governs toast purpose, duration, dismissal, pause, focus, or persistence. Source requirements explicitly say a toast cannot prove completion. A transient “Command sent” success toast can collapse acceptance into success, vanish before reading, or duplicate a live-region message.

**Fix:** Add a toast contract: evidence and terminal operation state never exist only in a toast; accepted uses neutral/pending language; persistent dialog/row/status evidence remains authoritative; timed toasts pause on hover/focus, are dismissible, never steal focus, and deduplicate with live regions. Reserve success toasts for non-authoritative convenience actions such as “Link copied.”

### Low

#### 19. `data-testid` is presented as accessibility evidence

**Location:** `EXPERIENCE.md:157-170`, `../../epics.md:5960-5963`.

**Note:** Stable selectors belong in testing/conformance, not the Accessibility Floor. A `data-testid` carries no accessible name, role, relationship, value, state, focus, or announcement behavior; selector-only tests can remain green while the accessibility tree regresses.

**Fix:** Move selector policy to testing/conformance and require each selector-based checkpoint to assert user-facing semantics: role, accessible name, value/state, relationships, focus behavior, and live message where applicable.

## What is already strong

- The spines distinguish command acceptance, evidence pending, and projection-confirmed success; HTTP 202 and SignalR are not presented as proof (`EXPERIENCE.md:117-118`, `EXPERIENCE.md:151-153`, `EXPERIENCE.md:274-284`).
- Denial is explicitly fail-closed and must not confirm hidden resource existence (`EXPERIENCE.md:119-120`, `EXPERIENCE.md:167-168`, `EXPERIENCE.md:241-250`, `EXPERIENCE.md:286-296`).
- Status and lifecycle meaning is textual rather than color-only (`DESIGN.md:162`, `DESIGN.md:178-180`, `EXPERIENCE.md:99`, `EXPERIENCE.md:165`).
- The support-safe ban already covers bearer tokens, decoded claims, raw payloads and metadata, cursors, ETags, stack traces, secrets, and unbounded SignalR metadata (`DESIGN.md:242`, `EXPERIENCE.md:199-205`).
- Failure paths exist for all six named journeys and generally preserve non-success honestly (`EXPERIENCE.md:227-296`).
- FrontComposer and Blazor Fluent UI V5 inheritance matches the repository baseline and correctly rejects a local design system (`DESIGN.md:168-180`, `references/Hexalith.AI.Tools/hexalith-ux-instructions.md:5-39`).
- The spine explicitly covers focus return after denial/failure, reduced motion, status announcements, resource-backed complete strings, and three responsive bands (`EXPERIENCE.md:157-197`).

## Reviewer scope

This lens reviewed `DESIGN.md`, `EXPERIENCE.md`, `.memlog.md`, `index.md`, all files under `imports/` and `mockups/`, the Hexalith UX baseline at `references/Hexalith.AI.Tools/hexalith-ux-instructions.md`, and accessibility/support-safety requirements in the frontmatter sources. It checked contract sufficiency for downstream architecture, story development, and conformance evidence—not implementation code and not a claim that the running product currently has each defect.

Behavioral interpretation was grounded in WCAG 2.2 AA and the WAI-ARIA Authoring Practices for tabs, grids, accordions, and modal dialogs. Particular checks included keyboard order and activation, name/role/value/relationship exposure, focus entry/return, status-message behavior, auto-updating content, target size, non-text contrast, reduced motion, 320 CSS-pixel reflow/400% zoom, localization and bidi behavior, responsive mutation disposition, failure recovery, privileged/destructive confirmation, revocation and fail-closed denial, authoritative evidence freshness, retry/idempotency safety, and support-safe disclosure.

No UX spine, source, import, or mockup was modified. This file is review evidence only.
