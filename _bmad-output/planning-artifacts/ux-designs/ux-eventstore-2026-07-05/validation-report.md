# Validation Report - eventstore

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- **Run at:** 2026-09-08T19:42:05+02:00
- **Spines validated:** `status: final`, updated 2026-08-01 (SHA-256 pins in `epics.md:16-18` match)
- **Lenses:** rubric walker, accessibility & support-safety, architecture & readiness

## Overall verdict

The spine pair is structurally sound and traceable: canonical section order, all 16 component names identical across both spines, every `{token}` reference resolving, all `sources` paths present, and six Key Flows each with a named protagonist, numbered steps, a climax beat and a failure path. Every 2026-07-05 blocker (missing `ux.md`, orphaned visuals, taxonomy drift, absent Sample/Tenants flows) is closed. It is not yet a clean extraction contract on the visual side: the `colors` frontmatter names the Fluent v4/FAST recipe family that `DESIGN.md:180`, UX-DR6 and the project UX policy forbid, two named V5 APIs do not exist in the pinned `5.0.0-rc.5` package (`FluentDrawer`, `BadgeColor.Neutral`) and one has already propagated into `epics.md` UX-DR18, and the promoted mockups bind the same forbidden legacy CSS variables under a comment claiming they are Fluent-emitted. Behaviour is buildable from `EXPERIENCE.md` today; visual tokens in `DESIGN.md` cannot be bound without re-deriving the Fluent 2 names.

The two extra lenses shift the picture from "token cleanup" to "safety layer incomplete". Accessibility & support-safety finds the spines strong on state honesty (accepted is never success, `LocalOnly`/`Unknown` never confirmed, deferred is hidden/disabled/501) but silent on the layer around those rules: nothing stops acting on the wrong tenant from an "All Tenants" view, the acting principal is never shown before a confirm, "evidence pending" has no budget and resubmission is not fenced, 401/403/404/501 collapse into ambiguous states, and toasts and auto-refresh are unregulated. Architecture & readiness confirms the 2026-08-01 in-place `Admin.UI` correction is fully reflected, but the tab map drops the live `/types` route, the projection-freshness indicator mandated on Admin tabs has no data source in the Admin transport, and the FrontComposer host integration the spines assume is neither present in the host nor described. Any spine edit changes the three SHA-256 digests in `epics.md` frontmatter (no test reads them, but re-pin in the same change), and `.memlog.md` still records the superseded "EventStore UI service" model, so a memlog-seeded update would regress the spines.

## Category verdicts

- Flow coverage - adequate
- Token completeness - broken
- Component coverage - strong
- State coverage - adequate
- Visual reference coverage - adequate
- Bloat & overspecification - adequate
- Inheritance discipline - adequate
- Shape fit - strong

Reviewer-reported counts: rubric 1 critical / 4 high / 9 medium / 14 low; accessibility & support-safety 1 / 9 / 15 / 4; architecture & readiness 0 / 4 / 12 / 7. Below, findings raised by more than one reviewer are merged and tagged with every source.

## Findings by severity

### Critical (2)

**Token completeness + Architecture** - Colour tokens name Fluent v4/FAST recipes, not Fluent 2 tokens (`DESIGN.md:15-23,32`)
Ten values (`accentFill`, `foregroundOnAccent`, `neutralLayer1/2`, `neutralForeground`, `neutralForegroundHint`, `neutralStroke`, `neutralFill`, `neutralFillLayer`, `focusStroke`) are the `--accent-fill-rest` / `--neutral-layer-1` family that `DESIGN.md:180`, UX-DR6 (`epics.md:200`) and the UX policy forbid; V5 does not emit them, and Story 7.20 must scan them out.
Fix: rename each value to its Fluent 2 token: `app-bar-background: '--colorBrandBackground'`, `app-bar-foreground: '--colorNeutralForegroundOnBrand'`, `canvas: '--colorNeutralBackground1'`, `navigation-background: '--colorNeutralBackground2'`, `content-foreground: '--colorNeutralForeground1'`, `secondary-foreground: '--colorNeutralForeground3'`, `border-subtle: '--colorNeutralStroke1'`, `surface-subtle: '--colorNeutralBackground3'`, `callout-info-background: '--colorNeutralBackground4'` (or `FluentMessageBar Intent=Info`), `focus-ring: '--colorStrokeFocus2'`.

**Accessibility & support-safety** - Nothing prevents acting on the wrong tenant, and irreversible actions need no second acknowledgement (`EXPERIENCE.md:94,97,101,154`; `dashboard-overview.html:305`; `command-investigation.html:260`)
Both mocks default to "All Tenants"; the dialog "requires exact target identity" but no rule puts tenant first, requires it to match or visibly override header scope, forbids cross-tenant multi-select, or demands re-confirmation for archive / role change / snapshot delete / projection reset.
Fix: the operation dialog leads with tenant/domain/target in a fixed labelled block; when header scope is "All tenants" the dialog says so; irreversible actions require explicit acknowledgement (checkbox or typed target id) with the danger appearance; multi-select never spans tenants.

### High (15)

**Token completeness + Architecture** - `FluentBadge Color=Neutral` does not exist in V5 (`DESIGN.md:30-31,114-115,143-150`)
`BadgeColor` in `5.0.0-rc.5` is Brand, Danger, Important, Informative, Severe, Subtle, Success, Warning; five component tokens (Rebuilding / LocalOnly / Unknown / deferred) have no bindable colour.
Fix: commit to `BadgeColor.Subtle` (what Admin.UI already uses) or `Informative`; keep the token name, correct the value.

**Component coverage + Inheritance + Architecture** - `FluentDrawer` is not a V5 component and has propagated into the epics (`DESIGN.md:128,226`; `epics.md:224` UX-DR18; `epics.md:5851`)
The rc.5 catalogue has no `FluentDrawer`; drawers are `IDialogService.ShowDrawerAsync` / `FluentDialog Alignment=End`. Fixing the spine alone leaves UX-DR18 stale.
Fix: replace with "`FluentDialog` in drawer mode (`DialogService.ShowDrawerAsync`, `DialogAlignment.End`)" in both spine locations and file the UX-DR18 wording change with the epics owner in the same change.

**Visual reference coverage + Architecture + Accessibility** - Both promoted mocks bind forbidden v4/FAST CSS variables under a false "Fluent-emitted" comment (`mockups/dashboard-overview.html:12-27`; `mockups/command-investigation.html:16-26`)
`--accent-fill-rest`, `--neutral-layer-1/2`, `--neutral-foreground-rest`, `--neutral-stroke-rest`, `--neutral-fill-*` are exactly the tokens `DESIGN.md:180` bans; dark and high-contrast behaviour of anything copied from the mocks is unverified.
Fix: swap to the `--color*` names from the critical finding (the names Admin.UI already uses) with the same system-colour fallbacks, or strip the `var()` layer and label the mocks "system-colour fallback only".

**Architecture** - Tab map omits the live `/types` route (`EXPERIENCE.md:39-50,54`; `Pages/TypeCatalog.razor:1`; `epics.md:190-192,5573-5576`)
Code has 22 `@page` templates; the spine, UX-DR3/4 and Story 7.14's manifest AC list 21, while Epic 6 stories expect a Type Catalog surface (`epics.md:4522,4602,4665`). A dev following the manifest drops the Type Catalog or invents an unowned tab.
Fix: add `/types` to the tab map with its detail-panel disposition and propagate to UX-DR4 and the 7.14 manifest list.

**Architecture** - Projection freshness indicator has no data source on Admin tabs (`EXPERIENCE.md:105,118,126-129,138`; `DESIGN.md:140-147,229`)
`QueryResponseProvenance` / `ProjectionLifecycleState` exist only on gateway query responses; zero references in `Admin.Abstractions`, `Admin.Server`, `Admin.UI`. The Projections page renders `ProjectionStatusType {Running, Paused, Error, Rebuilding}`, an operator runtime status, not lifecycle evidence.
Fix: add to the freshness-indicator row: "On Admin tabs this indicator renders only where the Story 7.5 typed Admin client exposes provenance + lifecycle on the DTO; until then Admin projection rows show `ProjectionStatusType` and the freshness indicator renders `Unknown`." Keep the two vocabularies visibly distinct.

**Architecture** - FrontComposer host integration is asserted but unspecified and absent from the host (`EXPERIENCE.md:93,300`; `DESIGN.md:217`; `Hexalith.EventStore.Admin.UI.csproj:24-28`)
The csproj references only Fluent packages; the Builds catalog is at FrontComposer 4.4.0 (architecture.md says 4.1.1) with no `Contracts.UI` entry that AD-21 requires. The registration model (`IFrontComposerRegistry.RegisterDomain(DomainManifest)` + `FrontComposerNavEntry`, worked example in Tenants.UI) is nowhere in the spine.
Fix: add a short "Host integration" subsection: bounded context `event-store-admin`, one `DomainManifest` (label Event Store Admin, localized `NameKey`), one `FrontComposerNavEntry` to `/`, Shell + Contracts.UI at catalog version, and the cross-repo prerequisite (Builds catalog entry for Contracts.UI).

**Accessibility & support-safety** - Live-region scoping and throttling are unspecified (`EXPERIENCE.md:151,172-180`; `dashboard-overview.html:298`)
With polling plus SignalR, every stat-card flip and every lifecycle step of every in-flight command becomes an announcement; nothing forbids a grid re-render from re-announcing rows.
Fix: one status region per dashboard view; announce only transitions of the operation the user initiated and terminal states; background refresh announces at most one summary per cycle and only when something changed; never announce per-row lifecycle steps from a grid.

**Accessibility & support-safety** - Auto-refresh has no cadence, no pause, no state-preservation rule (`EXPERIENCE.md:115-116,151`; `Pages/DaprPubSub.razor:408`)
WCAG 2.2.1/2.2.2 require pause or extend for auto-updating content; nothing says a poll must not reset sort/selection/scroll, collapse an accordion, close a drawer, or swap the row under an open confirmation. Legacy code already polls on a 30 s timer.
Fix: declare the interval and expose pause/resume in the header; refresh preserves focus, selection, scroll, expanded sections and open dialogs; a dialog freezes its target facts at open time and a changed row surfaces as a conflict.

**Accessibility & support-safety** - No rule for date, time, time-zone, duration or number rendering (`EXPERIENCE.md:182-187`; `DESIGN.md:186`; `dashboard-overview.html:281,320`; `StreamTimelineGrid.razor:70`)
Mocks show bare `08:24:12`; legacy pages format in the server culture. UTC/local ambiguity during incident correlation is a real support hazard.
Fix: timestamps render absolute with an explicit zone (operations default UTC, user zone optional secondary) plus relative age; numbers and durations go through culture-aware resource-backed formatters; add to Localization evidence.

**Accessibility & support-safety** - Acting principal is never shown before a confirm (`EXPERIENCE.md:101,205`; `DESIGN.md:218`; `command-investigation.html:313`)
"Mutations must be attributable" but the header shows tenant/environment/connection, not who is signed in; only the mock claims the audit record carries a user id, and Admin OIDC login is deferred.
Fix: header shows the current principal (display name + role set); the operation dialog restates "Acting as <name> with role <role>"; the post-evidence audit reference echoes that principal.

**Accessibility & support-safety** - Server outcomes are not mapped to distinct operator-visible states (`EXPERIENCE.md:114,119,122,211-212`)
404 may mean deferred, denied, or missing; 401 / session expiry mid-dialog is never mentioned. The operator cannot tell whether to request access, file backlog, or escalate.
Fix: add a mapping table: 401 re-authenticate (preserve dialog input, no auto-retry); 403 denied without existence disclosure; 404 not found, neutral copy; 501 "Unavailable in this release"; 5xx/network unavailable with retry; each with icon + text + test id.

**Accessibility & support-safety** - "Evidence pending" is unbounded and resubmission is not fenced (`EXPERIENCE.md:104,117,136,260,284`)
`TimedOut` exists as a tracker state, but no timeout is defined and "retry" after a late-but-accepted mutation duplicates it. Flow 3 gets this right; the generic dialog pattern does not.
Fix: define the evidence-pending budget; after it render "Outcome unknown - do not resubmit; track by correlation id" with refresh/navigate only; enable resubmit solely when command status is terminal non-success.

**Accessibility & support-safety** - Confirmation omits reversibility, blast radius and audit reason (`EXPERIENCE.md:101,138,142,154`)
Replaying a projection degrades consumers; archiving a dead letter means never delivered; role changes are immediate. Regulated support commonly requires a justification or ticket reference.
Fix: dialog states reversible / irreversible and the downstream consequence in one resource-backed sentence; destructive and recovery dialogs capture an optional (or role-required) reason with a length bound, written to the audit record.

**Accessibility & support-safety** - Mock tabs, filters, search and rows are non-interactive `<div>`s (`dashboard-overview.html:282-292,303-308`; `command-investigation.html:251-285`)
No `role="tablist"/"tab"`, no `aria-selected`, no focusability, Search is a `<div class="button">`; contradicts `EXPERIENCE.md:149,164` and the UX policy. The only interactive pattern in the reference mock is inoperable.
Fix: add a header comment naming the Fluent component each region stands in for (`FluentTabs`, `FluentTextInput`, `FluentSelect`, `FluentButton`, `FluentDataGrid`), or replace the strip and toolbar with semantic `<button role="tab">` / `<input>` / `<select>`.

**Accessibility & support-safety** - Toasts and transient notifications are unregulated (`EXPERIENCE.md` silent; `Layout/Breadcrumb.razor:180`)
The brownfield host already emits success toasts. "Command sent" in a toast is the cheapest way to collapse accepted into success, and toasts vanish before a screen-reader user reaches them.
Fix: toasts carry only non-evidence information (link copied, filter saved); mutation outcomes render in the persistent status region and the row/dialog; toasts are dismissible, non-blocking, never success-variant for accepted states.

### Medium (32)

**Flow coverage** - Projection lifecycle has no Key Flow (`EXPERIENCE.md:105,126-129`; `epics.md:1050,228-230,5863-5866`)
The most heavily specified behaviour in the sources exists only as State Pattern rows.
Fix: add Flow 7: Projections tab, rebuild in progress, mutation disabled, last complete model retained, evidence returns `Current`, gate re-opens.

**Flow coverage** - Overview, Topology, Storage & Snapshots and Settings have no journey (`EXPERIENCE.md:41,46,47,50,54`)
The IA closure rule at line 54 is not satisfied by the spine's own flows.
Fix: add short triage journeys for the four tabs or soften the rule to "every mutating surface".

**Token completeness + Architecture** - Typography roles use px values unreachable by allowed means (`DESIGN.md:40-57,188`)
34px and 18px are not on the `FluentText Size` ramp, and the prose forbids a CSS heading ramp.
Fix: express roles as component parameters (`page-title: FluentText As=H1 Size=Size800 Weight=Semibold`, `section-title: FluentText As=H2 Size=Size500 Weight=Semibold`) and drop the pixel literals.

**Token completeness** - `issue-banner` binds badge colour tokens but `FluentMessageBar` is styled by `Intent` (`DESIGN.md:119-121`; `EXPERIENCE.md:25,100`)
Fix: replace the three colour keys with `intent-info: 'MessageBarIntent.Info'`, `intent-warning: 'MessageBarIntent.Warning'`, `intent-danger: 'MessageBarIntent.Error'`.

**Token completeness** - Three component values are uncommitted "or" alternatives (`DESIGN.md:32,102,128,153`)
`evidence-grid`, `detail-panel`, `command-palette` and `focus-ring` cannot be source-extracted.
Fix: commit (`FluentDataGrid`; `FluentDialog` via `ShowDrawerAsync`; `FluentListbox`) and move the FrontComposer fallback into prose as the documented exception path.

**State coverage** - Shell-level failure states required by Story 7.14 have no row (`EXPERIENCE.md:114`; `epics.md:5598-5601`)
Fix: add a "Shell / route failure" row: accessible support-safe state, bounded navigation choices, no false selected tab, no stale authorization.

**State coverage** - Timeout and cancelled outcomes appear only in Flow 5's failure line (`EXPERIENCE.md:284`; `epics.md:4918,5871`)
Fix: add a "Request timeout / cancelled" row to State Patterns with the evidence-pending-or-stale treatment.

**State coverage** - Sample and Tenants surfaces have no rows in the tab-state matrix (`EXPERIENCE.md:34-35,117,131-144`; `epics.md:1658`)
Fix: add two matrix rows for the Sample dashboard and the Tenants dashboard.

**Visual reference coverage** - References linked in one blob, not inline at the section each illustrates (`DESIGN.md:166`; `EXPERIENCE.md:25`)
Fix: move each link beside its section with one clause naming what to look at; keep a single spines-win statement.

**Accessibility & support-safety** - Focus destinations ambiguous for tab activation, deep links and non-modal detail panels (`EXPERIENCE.md:102,150,162,168`)
Fix: user tab activation keeps focus on the tab; deep-link entry focuses the page title; opening a detail panel focuses its heading; closing returns focus to the originating row; state this for drawers explicitly.

**Accessibility & support-safety** - Row-click is the only drill-in affordance; icon-only actions have no naming or target-size rule (`EXPERIENCE.md:98`; `DESIGN.md:105,218`)
Fix: every evidence row exposes a focusable open action; icon-only controls carry an accessible name and a >=24 px target; add the target-size floor to DESIGN.md Components.

**Accessibility & support-safety** - Contrast covers text only; disable reason is tooltip-only (`DESIGN.md:32,178`; `EXPERIENCE.md:213-214`)
Fix: add a 3:1 non-text target; render the disable reason as adjacent enabled text or a `FluentMessageBar` associated via `aria-describedby`.

**Accessibility & support-safety** - Lifecycle codes used as both enums and labels with no invariant/translated split (`EXPERIENCE.md:104-105`; `command-investigation.html:272,296-300`)
Fix: render a localized label plus the invariant code (monospace, never translated); list which tokens are invariant identifiers.

**Accessibility & support-safety** - Pluralization has a prohibition but no mechanism (`EXPERIENCE.md:121,142,185`)
Fix: name per-plural-category resource keys or a shared plural formatter; forbid `count + " items"` in the conformance checklist.

**Accessibility & support-safety** - RTL, text expansion and truncation are silent (`EXPERIENCE.md:194`; `DESIGN.md:200`; `dashboard-overview.html:318`)
Fix: state the RTL stance (Fluent `dir` mirroring, identifiers stay LTR), a 30 % expansion tolerance for tabs/badges/tracker steps, and a truncation rule: full value always available via accessible name or copy action.

**Accessibility & support-safety** - Server error text passthrough is not governed (`EXPERIENCE.md:114,125,239`)
Fix: UI renders only resource-backed strings keyed by server error code; ProblemDetails `detail` is never rendered outside an allowlisted set; publish the classification vocabulary.

**Accessibility & support-safety** - URL persistence, clipboard and export are ungoverned support-safe channels (`EXPERIENCE.md:97,152,201`; `Breadcrumb.razor:172-180`; `CorrelationTraceMap.razor:392`)
Fix: URL state carries only tenant/domain/status/identifier filters, never cursors, ETags or payload fragments; clipboard/export limited to an allowlist of safe identifiers; a shared link opened without access renders the denied state.

**Accessibility & support-safety** - Masking undefined for configuration, topology, dead-letter reasons and end-user PII (`EXPERIENCE.md:45,46,50,121,123,201`)
Fix: configuration and component viewers show key names and secret references only; dead-letter reason text goes through the error classification; user identifiers display per role with no bulk listing/export by default.

**Accessibility & support-safety** - Styled deferred-operation token invites a decorative placeholder; legacy fake capability stays reachable during migration (`DESIGN.md:148-151`; `EXPERIENCE.md:106,122`; `Pages/Backups.razor:265-456`)
Fix: the placeholder is a disabled grid row or `FluentMessageBar`, never a card; legacy deferred pages are removed or 501-backed in the first migration story.

**Accessibility & support-safety** - Decorative success-coloured banner and mis-toned warning in the mocks (`dashboard-overview.html:43-51,269`; `command-investigation.html:175-180`)
Fix: drop the notice or make it neutral; give the warning banner warning styling end to end.

**Accessibility & support-safety** - Two label vocabularies for one lifecycle plus bare timestamps in the mocks (`dashboard-overview.html:281,315,320,323`; `command-investigation.html:272,279`)
Fix: use the spine's tracker/freshness vocabulary verbatim in both mocks; render every timestamp with a zone.

**Accessibility & support-safety** - No conflict / already-handled state (`EXPERIENCE.md:111-129`)
Fix: add "Conflict - target changed since you opened this action; refresh and re-evaluate" with no automatic retry, assertive priority, focus return to the row.

**Accessibility & support-safety** - Bulk actions and command-palette "act" have no safety bound (`EXPERIENCE.md:107`; `DESIGN.md:231`)
Fix: palette actions always open the same operation dialog; shortcuts are modifier-based or remappable; bulk actions, if allowed, are per-tenant, per-tab, and confirm exact count and identities.

**Architecture** - "Topology" already names a different thing in code (`Layout/NavMenu.razor:29-56`; `EXPERIENCE.md:46`)
The existing Topology nav category is a tenant/domain stream navigator; the spine's Topology tab is DAPR/services.
Fix: name the disposition of the tenant/domain navigator and rename one of the two concepts.

**Architecture** - `/health` placed under Recovery while the code's Health page is a DAPR component-status dashboard (`EXPERIENCE.md:48`; `CommandPaletteCatalog.cs:14-15`)
Fix: split explicitly: component health to Topology, health summary + dead letters + consistency to Recovery; say which page owns shared components.

**Architecture** - `FluentTabs` is not a router and has no overflow scrolling (`EXPERIENCE.md:95,150,194`)
URL-addressable tabs and scrolling tab rows need custom wiring the spine does not acknowledge.
Fix: state that tab selection is bound to the route by the shell and that tab-row overflow is an allowed layout-only CSS exception.

**Architecture** - Command investigation identifier underspecified; AD-17 absent from traceability (`EXPERIENCE.md:58-69,255`; `CommandStatusController.cs:23`; `Pages/Commands.razor:52-72`)
Fix: state `MessageId` is the primary lookup key, `CorrelationId` the trace key; add an AD-17 row saying the UI never constructs a status URL itself.

**Architecture** - Lifecycle colour mapping conflicts with the shipped Tenants implementation (`DESIGN.md:141-147`; `ProjectionLifecycleBadge.razor:28-33`)
Tenants renders Stale as Severe, Rebuilding as Informative, LocalOnly as Important; the spine claims to own the Tenants flow.
Fix: adopt the Tenants mapping as platform standard or record the divergence as an accepted per-module exception.

**Architecture** - Flow 5 (Sample) has no open owner and contradicts the shipped Sample host (`EXPERIENCE.md:34,274-284`; `CounterCommandForm.razor:29-33`; `Sample MainLayout.razor:11-14`)
The Sample renders "Last command: X" in success colour after a 202 and is a standalone four-item host, not a module entry.
Fix: mark the Sample row as "reference host, not module-shell-governed; accepted-state copy is a documented gap" or name the story that will re-open it.

**Architecture** - No owning-story column; sources still cite the superseded readiness report (`EXPERIENCE.md:11,58-69`; `DESIGN.md:12`)
Fix: add an "Owning story" column (7.14 shell/tabs/routes, 7.4 deferred honesty, 7.5 typed client, 7.19 evidence states, 7.20 conformance, 2.6/4.7 Tenants) and point sources at `implementation-readiness-report-2026-08-01-post-correction.md`.

**Architecture** - Three questions a dev must ask before building the Commands tab (`EXPERIENCE.md:95,150,170`)
Which Admin DTO field carries provenance/lifecycle; what the tab URL scheme is and whether `ActiveTabId` drives or follows navigation; what the `data-testid` naming pattern is.
Fix: answer all three in the spine, one line each.

**Architecture** - `.memlog.md` still records the superseded "EventStore UI service" model (`.memlog.md:11,15,25`)
A UX update seeded from the memlog would regress the 2026-08-01 correction.
Fix: append dated decision entries: "2026-08-01: Admin.UI evolves in place under `eventstore-admin-ui`; module id `event-store-admin`; no second host" and "2026-08-01: 7.14 split into 7.14/7.19/7.20".

### Low (25)

**Flow coverage** - Bookmarked legacy deep-link migration has no flow step (`EXPERIENCE.md:272`; `epics.md:196,5573-5581`). Fix: add one step to Flow 3 (Lea arrives via a bookmarked `/streams/{tenant}/{domain}/{aggregate}` link).

**Token completeness** - No 3:1 non-text contrast floor for focus ring, borders and tracker fills (`DESIGN.md:137-139,178`). Fix: one sentence stating the 3:1 floor and pairing each tracker background with its `status-*-foreground`.

**Token completeness** - Six frontmatter tokens are never referenced (`DESIGN.md:20,32,46,64,68,69`). Fix: reference from `dashboard-header`/`dashboard-shell` or delete.

**Component coverage** - Loading skeletons and inline validation have no component home (`EXPERIENCE.md:113,124,179`). Fix: add `skeleton` and `inline-validation` rows or note they inherit Fluent defaults with no delta.

**State coverage** - Command palette has no state rows (`epics.md:5593-5596`). Fix: one "Command palette" row in State Patterns.

**Visual reference coverage** - The two mock PNGs are linked from nowhere (`index.md:28-29`). Fix: link as "rendered capture" beside each HTML link, or delete.

**Visual reference coverage** - Overview mock omits the Settings tab (`dashboard-overview.html:283-291` vs `EXPERIENCE.md:41-50`). Fix: add the tab or note the omission in the mock header.

**Visual reference coverage** - `.working/` holds six never-promoted leftovers. Fix: delete `.working/` or note in `index.md` that it is scratch.

**Bloat** - 110-character `fontFamily` stack repeated four times (`DESIGN.md:35,41,47,53`). Fix: single inheritance note in prose.

**Bloat** - Deferred-operation policy stated in four places, single-host decision in five. Fix: keep the visibility table (`EXPERIENCE.md:209-214`) as single source; cross-reference elsewhere.

**Bloat** - Spines-win-on-conflict stated four times across the workspace. Fix: keep in `index.md` and one spine.

**Inheritance** - Casing drift `unknown` vs `Unknown` (`EXPERIENCE.md:66`). Fix: `Unknown`.

**Inheritance** - Foundation's visual-dependency references point at the two tokens currently resolving to v4 names (`EXPERIENCE.md:25`). No separate fix; closes with the critical finding.

**Shape fit** - `EXPERIENCE.md` frontmatter lacks `description`. Fix: copy the one-liner from `DESIGN.md:3`.

**Accessibility & support-safety** - Narrow reflow stops at <960px; skeleton shimmer unregulated (`EXPERIENCE.md:113,169,191-195`; `command-investigation.html:131`). Fix: grids scroll in their own container or stack at <=400 px, tabs overflow to a menu, shimmer honours `prefers-reduced-motion`.

**Accessibility & support-safety** - Brownfield literal strings have no migration gate (`EXPERIENCE.md:184`; `AdminResources.resx` unused). Fix: per-tab localization conformance checklist or an allowlisted literal-string backlog mirroring the token rule.

**Accessibility & support-safety** - Mock structure gaps: `<p>` panel title, unstyled `<summary>` focus, `aria-hidden` hamburger, unconditional Deferred tab (`command-investigation.html:219-222,290`; `dashboard-overview.html:254-256,271,291`). Fix: `h2` titles, native focus styling, nav toggle as a button, annotate the tab as role-gated.

**Accessibility & support-safety** - Read-side audit is silent (`EXPERIENCE.md:205`). Fix: state whether protected-detail views are audited; if not, say so.

**Architecture** - Host title "Hexalith EventStore Admin" vs label "Event Store Admin"; breadcrumb and dev role switcher unmentioned (`MainLayout.razor:23,26-38`; `EXPERIENCE.md:92`). Fix: state whether breadcrumbs survive under tabs and that the role switcher is a non-production utility.

**Architecture** - Existing "optimized for wider screens" alert contradicts the <960px promise and is not marked for retirement (`MainLayout.razor:49-51`; `app.css:235-296`). Fix: add an "existing behaviour to retire" note.

**Architecture** - Legacy token inventory (`app.css:5-10,32-37,53-58`; `ProtectedContentPanel.razor:20`) not listed as migration backlog in DESIGN.md. Fix: add a "legacy token inventory to retire" line under Colors.

**Architecture** - FR36 cited as primary authority; post-correction readiness names FR4 (`EXPERIENCE.md:67`; `DESIGN.md:229`). Fix: relabel to "FR4 / AD-15 / AD-19 / AD-20 (FR36 parity proof)".

**Architecture** - Spine edits change the three SHA-256 digests in `epics.md:16-18`. Verified today: no tool or test reads `inputDocumentDigests`. Fix: re-pin in the same change as any spine edit.

**Architecture** - Story 7.20 reconciliation text says "no .resx resources" but `AdminResources.resx` now exists (`epics.md:5927`). Fix: none in the spine; note for the 7.20 story file.

**Architecture + Rubric** - `index.md` lists the 2026-07-05 reports as current audit artifacts. Superseded by this run; the prior `validation-report.*` and all three `review-*.md` are now regenerated. Fix: none further.

## Resolved since 2026-07-05

All nine prior findings checked by the extra reviewers are confirmed resolved: canonical `ux.md`, Sample/Tenants flows, deferred visibility matrix, source traceability table, readiness report refresh, localization evidence at string level, mobile mutation fallback, denied-state copy and focus, live-region priority table. The rubric confirms the prior critical (non-concrete tokens) is replaced by a narrower critical: the tokens are now concrete but name the wrong token family.

## Reviewer files

- `review-rubric.md`
- `review-accessibility-support-safety.md`
- `review-architecture-readiness.md`
