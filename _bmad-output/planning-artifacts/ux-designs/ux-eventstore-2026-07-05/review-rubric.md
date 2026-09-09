# Spine Pair Review — eventstore

## Overall verdict

The pair is a usable but not yet clean downstream contract: its canonical shape, component taxonomy, principal operator flows, state vocabulary, accessibility floor, and semantic FrontComposer/Fluent UI V5 inheritance are largely coherent. The main risks are source drift after the spines' 2026-08-01 finalization, a circular and partly historical source graph, an unreconciled security rule for idempotency keys, incomplete journey/state closure, and visual references that contain prohibited legacy Fluent variables.

## 1. Flow coverage — adequate

The direct source set resolves to `docs/brownfield/architecture.md`, PRD FR1–FR37 and NFR1–NFR19, architecture AD-1–AD-25, epics UX-DR1–UX-DR42, the historical readiness report, and the reachable Fluent UI V5 documentation site. No direct source defines separately numbered UJs. The explicit journey requirements are covered: incident recovery (UX-DR39) maps to Flow 1, tenant-access mutation (UX-DR40) to Flows 2 and 6, command investigation (UX-DR41) to Flow 3, and the paired Sample/Tenants requirement (UX-DR42) to Flows 5 and 6 (`EXPERIENCE.md:225-296`). All six flows have a named protagonist, numbered steps, a climax, and an applicable failure path.

### Findings

- **medium** The spine declares that every target surface supports an administrator/operator journey, but Topology, Storage & Snapshots, and Settings have no Key Flow; Projections is only indirect, while Overview is only a waypoint (`EXPERIENCE.md:41-54,225-296`). UX-DR20, UX-DR21, and UX-DR31 make projection lifecycle gating especially load-bearing. *Fix:* add a projection-rebuild flow and concise operator journeys for the uncovered tabs, or narrow the IA closure rule explicitly.
- **low** General legacy-route/deep-link migration is not demonstrated by a flow; only `/backups` appears in Flow 4's failure path (`EXPERIENCE.md:52,272`; `epics.md:196`). *Fix:* add a bookmarked legacy deep-link arrival to an existing operator flow, including selected module and tab state.

## 2. Token completeness — adequate

The frontmatter defines 18 colors, four typography roles, four radii, six spacing tokens, and 16 component objects (`DESIGN.md:14-155`). All 35 distinct `{path.to.token}` references across both spines resolve. Missing hex values are not treated as critical here: Foundation explicitly inherits FrontComposer and Blazor Fluent UI V5, and the local DESIGN.md spec permits semantic UI-system inheritance. The contract also states WCAG 2.2 AA for text/state labels (`DESIGN.md:170-180`).

### Findings

- **high** The four typography roles encode a local pixel ramp, including 34px page titles and 18px section titles (`DESIGN.md:34-57`), while repository UX governance requires typography through Fluent UI V5 component parameters or Fluent 2 tokens and forbids recreating a heading ramp in CSS (`DESIGN.md:184-188`). *Fix:* express each role as an inherited `FluentText`/FrontComposer parameter or named Fluent 2 typography token, retaining literal values only if the owning UI system exposes them.
- **medium** Semantic color inheritance is valid, but several values are descriptions or recipe-like labels rather than exact, bindable Fluent UI V5 token/parameter names—for example `Fluent theme accentFill`, `neutralLayer1`, and `focusStroke` (`DESIGN.md:15-23,32`). Their resemblance to the forbidden legacy family makes source extraction ambiguous even though the intent is clear. *Fix:* retain semantic inheritance but name the exact Fluent 2 `--color*` token or component parameter/intent for each role.
- **medium** `issue-banner` maps warning and danger appearance through `FluentBadge` background roles even though its declared component is `FluentMessageBar` (`DESIGN.md:117-122`; `EXPERIENCE.md:100`). *Fix:* specify message-bar intent/variant semantics directly and keep badge semantics under `status-badge`.
- **low** The contrast statement covers text and state labels but not load-bearing non-text indicators such as focus, borders, and lifecycle steps (`DESIGN.md:137-139,178`). *Fix:* state the WCAG 2.2 AA 3:1 floor for focus and other required non-text indicators, and pair tracker backgrounds with foreground roles.

## 3. Component coverage — adequate

The 16 canonical component names match across DESIGN.md frontmatter, DESIGN.md Components, and EXPERIENCE.md Component Patterns (`DESIGN.md:71-155,212-231`; `EXPERIENCE.md:86-107`). Each canonical component has substantive visual and behavioral rules, and inherited Fluent primitives are correctly treated as UI-system-owned rather than redundantly restated.

### Findings

- **medium** Three load-bearing component bindings remain alternatives rather than decisions: Evidence grid allows `FluentDataGrid or FrontComposer`, Detail panel allows three primitives, and Command palette leaves its list primitive unnamed (`DESIGN.md:101-103,127-130,152-155,222,226,231`). *Fix:* commit the preferred FrontComposer/Fluent UI V5 primitive and document one explicit fallback condition instead of an unrestricted “or”.
- **low** Cold-load skeletons and inline validation are required behaviors but have no visual/behavioral component pair (`EXPERIENCE.md:113,124,179`; `epics.md:236,248`). *Fix:* add paired Skeleton and Inline validation rows, or state explicitly that their Fluent defaults are inherited without delta.

## 4. State coverage — adequate

The ten EventStore dashboard tabs have an explicit empty, stale/offline, permission-denied, and mutation-policy matrix (`EXPERIENCE.md:131-144`). Global patterns additionally cover cold load, API unavailability, SignalR disconnect, acceptance versus projection confirmation, protected payloads, validation, oversized requests, dead letters, deferred work, and projection lifecycle states (`EXPERIENCE.md:109-129`).

### Findings

- **medium** Sample and Tenants are first-class IA surfaces but are absent from the per-surface matrix; Sample has only accepted-command coverage and Tenants is covered indirectly through shared projection rows (`EXPERIENCE.md:31-35,111-144`). *Fix:* add Sample and Tenants rows covering cold, empty, stale/offline, denied, error, and mutation policy.
- **medium** Shell/composition failure, unknown route or failed tab load, and request timeout/cancellation are not explicit State Pattern rows; API unavailable and one flow-specific timeout do not fully define them (`EXPERIENCE.md:114,284`). *Fix:* add bounded, support-safe shell/route failure and timeout/cancelled patterns, including focus and selected-navigation behavior.
- **medium** New NFR19 distinguishes deleted, missing, denied, unavailable, malformed, tampered, and opaque payload-protection outcomes (`prd.md:335`), while the spines only specify a generic protected/redacted payload state (`EXPERIENCE.md:123,201`). AD-23 leaves domain UX/copy with Parties, so not every state necessarily belongs in EventStore Admin, but the inheritance decision is absent. *Fix:* explicitly map the EventStore-visible subset to support-safe states and mark the remaining outcomes as consumer-owned/non-UI.

## 5. Visual reference coverage — thin

`imports/` contains two PNG captures; `mockups/` contains two HTML mocks and their two rendered PNGs; `wireframes/` is absent. Both spines link the two imports and the two HTML files and state that the spines win on conflict (`DESIGN.md:166`; `EXPERIENCE.md:25`).

### Findings

- **medium** Both promoted HTML mocks use prohibited legacy Fluent v4/FAST variables such as `--accent-fill-rest`, `--neutral-layer-*`, `--neutral-foreground-*`, and `--neutral-fill-*`, while claiming they are Fluent-emitted (`mockups/dashboard-overview.html:8-27`; `mockups/command-investigation.html:8-26`). *Fix:* replace them with exact Fluent 2 `--color*` tokens/component styling, or label the CSS as non-implementable illustration and remove the misleading claim.
- **medium** The references are grouped in one Brand/Foundation sentence rather than linked at the relevant IA, component, or flow section with an explanation, and `mockups/dashboard-overview.png` plus `mockups/command-investigation.png` are unlinked (`DESIGN.md:166`; `EXPERIENCE.md:25`). *Fix:* link each HTML and rendered PNG beside the rule it illustrates and state the illustrated decisions; retain one spines-win statement.
- **low** The Overview mock omits the Settings tab, showing nine tabs against the canonical ten (`mockups/dashboard-overview.html:282-292`; `EXPERIENCE.md:41-50`). *Fix:* add Settings or annotate the omission in the mock itself.

## 6. Bloat & overspecification — adequate

At 243 and 302 lines, the spines are mostly decision-dense. The invented Source Traceability, Support-Safe Operations, and Non-Blocking Assumptions sections carry real downstream value, and EXPERIENCE.md avoids decorative editorial narration.

### Findings

- **low** Deferred-operation policy and the single-host identity decision are repeated across several state, component, flow, assumption, and index locations (`DESIGN.md:164`; `EXPERIENCE.md:21,63,106,122,143,207-214,300`). *Fix:* keep one authoritative policy table/identity statement and cross-reference it from shorter rows.

## 7. Inheritance discipline — thin

All five local `sources` paths resolve and the external Fluent UI V5 URL returned HTTP 200. Glossary/state vocabulary and all 35 cross-spine token references are mechanically consistent; the 16 canonical component names are identical. The weakness is authority and freshness, not missing files.

### Findings

- **high** The source graph is circular and includes a contradicted historical readiness report: the spines source architecture and epics (`DESIGN.md:6-11`; `EXPERIENCE.md:6-11`), while architecture and epics source/input the spines; the included 2026-07-05 report says `status: ready`, while the current PRD says readiness is `blocked` / `reject` as assessed 2026-09-09 (`implementation-readiness-report-2026-07-05.md:1-22`; `prd.md:1-10`). *Fix:* declare a one-way authority order, remove downstream artifacts from normative spine sources, and replace or explicitly label the old readiness report as historical evidence.
- **high** The spines were finalized on 2026-08-01, but direct sources were subsequently updated with FR37/NFR19 and AD-23–AD-25 (`DESIGN.md:4-5`; `EXPERIENCE.md:4-5`; `prd.md:303-335`; `architecture.md:370-425`). Most importantly, AD-25 forbids raw idempotency keys from status, archive, errors, and evidence, but the Support-Safe banned-data list does not name idempotency keys (`architecture.md:406-415`; `EXPERIENCE.md:199-205`). *Fix:* reconcile each post-final source change, add idempotency keys to the no-render/no-log contract, and record explicit no-UX impact where appropriate.
- **medium** UX-DR2 requires stable FrontComposer module identity `event-store-admin`, while the spines name only the distinct resource/container identity `eventstore-admin-ui` and the visible label (`epics.md:190-194`; `DESIGN.md:164,216`; `EXPERIENCE.md:21,91,300`). The canonical top-level `ux.md` distinguishes both identities but is not a direct spine source (`ux.md:30-34`). *Fix:* state both identifiers and their separate purposes in Foundation and Module entry; add the canonical `ux.md`/index handoff to the source chain without creating another cycle.
- **low** The fail-safe state is lowercase `unknown` once while the source and all other occurrences use `Unknown` (`EXPERIENCE.md:66`; `DESIGN.md:229`). *Fix:* normalize to `Unknown` so state identifiers/selectors remain stable.

## 8. Shape fit — strong

DESIGN.md follows the locked canonical order: Brand & Style, Colors, Typography, Layout & Spacing, Elevation & Depth, Shapes, Components, Do's and Don'ts (`DESIGN.md:158-243`). EXPERIENCE.md contains all eight required defaults plus correctly triggered Responsive & Platform and Inspiration & Anti-patterns sections (`EXPERIENCE.md:17,27,71,86,109,146,157,189,216,225`). The product-specific Source Traceability and Support-Safe Operations sections earn their place.

### Findings

- None.

## Mechanical notes

- Frontmatter: both spines have `name`, `status`, `created`, `updated`, and the same six direct sources; DESIGN.md also has the required `description` and token objects.
- Token resolution: 35/35 distinct `{path.to.token}` references resolve; semantic Fluent/FrontComposer inheritance is valid under the local spec, so absence of hex values is not itself a defect.
- Components: 16/16 canonical names align across both spines; inherited Fluent primitives were assessed as implementation bindings rather than additional canonical product components.
- Visual inventory: two imports and four mockup files; no wireframes directory. The two mockup PNGs are orphans.
- No Mermaid blocks occur in either spine.
- Finding counts: critical 0, high 3, medium 10, low 6.
