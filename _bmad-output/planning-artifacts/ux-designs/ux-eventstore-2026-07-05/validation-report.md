# Validation Report — eventstore

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- **Run at:** 2026-09-09T16:10:28+02:00
- **Spines validated:** `status: final`, updated 2026-09-09; neither spine was modified
- **Selected lenses:** rubric walker; accessibility & support-safety; architecture readiness

## Overall verdict

The spine pair is an adequate downstream UX contract: token, component, state, and visual-reference coverage are strong, and all source-defined UX journeys are behaviorally represented. It should not drive implementation unchanged, however, because inheritance and source-snapshot ambiguities remain, including a Detail panel binding that crosses the repository's FrontComposer/Fluent reuse boundary.

The architecture lens materially changes the handoff picture: the authoritative PRD remains `blocked` / `reject`, architecture remains `draft`, and `/health` is assigned both to an anonymous probe and an authenticated Recovery page. Until those upstream and route conflicts are resolved, the UX documents remain planning input—not implementation, release, deployment, migration, or readiness authorization.

No overall grade is assigned. Reviewer-reported counts are retained without deduplicating overlaps:

| Reviewer | Critical | High | Medium | Low | Total |
|---|---:|---:|---:|---:|---:|
| Rubric walker | 0 | 1 | 2 | 1 | 4 |
| Accessibility & support-safety | 0 | 1 | 4 | 0 | 5 |
| Architecture readiness | 2 | 4 | 3 | 0 | 9 |
| **Combined** | **2** | **6** | **9** | **1** | **18** |

## Category verdicts

- Flow coverage — **adequate**
- Token completeness — **strong**
- Component coverage — **strong**
- State coverage — **strong**
- Visual reference coverage — **strong**
- Bloat & overspecification — **adequate**
- Inheritance discipline — **thin**
- Shape fit — **adequate**

## Findings by severity

Repeated findings are retained when another reviewer adds a distinct downstream consequence.

### Critical (2)

**[Architecture readiness] — The authoritative planning baseline forbids an implementation handoff** (`prd.md:3-9,83-85,509-511,548-563`; `architecture.md:8-10,73-77,446-465`; `EXPERIENCE.md:22,35-47`)

The PRD records `implementation_readiness_status: blocked` and `implementation_readiness_result: reject`; OR14 forbids downstream handoff until the PRD, architecture, and epics are reconciled and approved, while architecture remains `draft`.

Fix: close the PRD's blocking refinements, approve one reconciled architecture/epics baseline, rerun implementation readiness, and bind the new result and source identities before implementation handoff.

**[Architecture readiness] — `/health` is both an anonymous probe and an authenticated dashboard route** (`EXPERIENCE.md:60-69,95-106,295`; `architecture.md:193-197`; `Health.razor:1`; `Program.cs:17-18`; `ServiceDefaults/Extensions.cs:156-186`)

The same UI host currently maps the Razor page and an explicitly anonymous health-check endpoint to `/health`. The probe may shadow the UI route; reversing the precedence could expose an authenticated operations surface anonymously.

Fix: give Recovery a distinct canonical UI path; reserve `/health`, `/alive`, and `/ready` for support-safe probes; then update UX-DR4, Story 7.14, redirects, navigation, palette links, and endpoint tests atomically.

### High (6)

**[Rubric / Inheritance discipline] — Detail panel violates the declared reuse boundary** (`DESIGN.md:61,159`; `EXPERIENCE.md:175`; `epics.md:224`; `hexalith-ux-instructions.md:10`)

The binding selects an EventStore-owned labelled `aside` composed with `FluentCard`, while UX-DR18 requires `FluentDrawer`, `FluentDialog`, or a FrontComposer panel and repository guidance permits custom markup only when no equivalent exists.

Fix: bind an approved Fluent/FrontComposer primitive, add a reusable panel to FrontComposer, or explicitly reconcile UX-DR18 and document the no-equivalent exception.

**[Accessibility & support-safety] — Sample accepted-submission UX has no accountable accessibility/conformance owner** (`EXPERIENCE.md:71,227-228,312,357-373`; `epics.md:272,705-743,1480-1529,1627-1675`)

Sample's accepted → evidence-pending → projection-confirmed/unknown flow is specified but not owned by a story that closes visible state, focus, keyboard, live-announcement, timeout, and no-resubmit behavior.

Fix: add a consumer-surface conformance matrix and assign the complete Sample flow to one explicit story owner; retain Tenants Story 2.6 as its separate owner.

**[Architecture readiness] — `/types` is absent from Story 7.14's closed route manifest** (`EXPERIENCE.md:49-52,58-69,77-102`; `epics.md:5539-5549,5577-5585`; `TypeCatalog.razor:1`)

The spine makes `/types` canonical under Streams & Events, but Story 7.14 calls its own route list exact and omits `/types`.

Fix: add `/types` and its `events` / `commands` / `aggregates` inner-tab contract to the machine-validated route manifest, or remove the spine claim through an approved source change.

**[Architecture readiness] — No closed route/action/policy/evidence matrix exists** (`EXPERIENCE.md:77-102,123-156,213-242,291-312`; `epics.md:4822-4837,4960-4993,5842-5845`)

Each query and mutation is not yet bound to a typed facet/outcome, policy, scope, stable identity, terminal/projection/audit evidence, retryability, and availability gate. Story developers would have to invent security and success semantics.

Fix: create one story-owned operation matrix and generate Story 7.5/7.19 tests from it.

**[Architecture readiness] — Sample accepted-submission flow lacks a precise implementation/test owner** (`EXPERIENCE.md:71,301-312,357-364`; `epics.md:272,323-328,705-743,1627-1675`)

Story 1.8 proves host/client/API boundaries but not the full Sample pending, projection-confirmed, timeout/unknown, or no-resubmit contract. Tenants Story 2.6 cannot close the Sample surface.

Fix: assign the Sample behavior to one named story with browser/component acceptance criteria and cite it separately in Source Traceability.

**[Architecture readiness] — Source snapshot is not reproducible under its drift rule** (`DESIGN.md:4-14,89-95`; `EXPERIENCE.md:3-13,35-47`; `epics.md:7-18`)

The spines bind revision `23a722a…` while current `HEAD` is `0994c378…`; the recorded epics digest is `5a5c03d1…` while the current file is `d067c8fb…`. The pre-repin explanation and reopen-on-drift rule do not give consumers a mechanical comparison rule.

Fix: use one immutable baseline manifest or normalized digests that exclude provenance fields; mark reconciliation open until that baseline is bound.

### Medium (9)

**[Rubric / Flow coverage] — Source journey names are not carried forward verbatim** (`epics.md:266,272`; `EXPERIENCE.md:316,366`)

UX-DR39 through UX-DR42 are behaviorally covered, but headings omit the requirement IDs and rename source phrases such as “incident-recovery journey.”

Fix: prefix applicable flows with UX-DR39–42 and retain the exact source journey names; point UX-DR42 explicitly to both consumer flows.

**[Rubric / Inheritance discipline] — Epics digest comparison is ambiguous** (`EXPERIENCE.md:39,44,47`)

The recorded pre-repin digest differs from the current epics file, but the spine also says any later digest change reopens reconciliation.

Fix: bind a normalized digest that excludes downstream UX digest fields or record both values with an explicit comparison rule.

**[Accessibility & support-safety] — Detail panel/drawer semantics are contradictory** (`DESIGN.md:61-64,136,159-160`; `EXPERIENCE.md:175-176,260-261,266,321,343`)

The binding specifies a non-modal `aside`, while journeys call it a drawer and the accessibility floor never resolves modality, focus entry, trapping, dismissal, or stable return.

Fix: choose and specify one interaction model per viewport/state, including focus and background behavior.

**[Accessibility & support-safety] — WCAG 2.2 ownership does not disposition inherited or absent interaction classes** (`EXPERIENCE.md:200-205,247,255-268,297`; `DESIGN.md:111-113,132`; `epics.md:5935-5958,5990-6003`)

The contract does not say which WCAG 2.2 additions are inherited, locally owned, delegated, absent, or not applicable, leaving Story 7.20 to invent policy for focus obscuration, dragging, and authentication.

Fix: add a concise WCAG ownership/applicability table and explicit rules for single-pointer alternatives, unobscured focus, and accessible authentication ownership.

**[Accessibility & support-safety] — Evidence-grid keyboard behavior is underspecified** (`EXPERIENCE.md:168-171,175,247,250,260,266,268`; `DESIGN.md:48-50,155`)

The contract does not choose between a static table with tabbable actions and an interactive grid with cell focus, arrow navigation, row selection, and embedded-control modes.

Fix: adopt one Fluent V5 grid profile and define tab stops, arrow/Home/End behavior, activation, announcements, embedded controls, selection, and focus fallback.

**[Accessibility & support-safety] — Route and tab activation lack deterministic focus-entry rules** (`EXPERIENCE.md:75,164-168,246,250,259-263,414-419`; `DESIGN.md:119`)

The contract does not settle whether tab activation retains focus, moves to the title, or varies for deep links, history, redirects, and failures.

Fix: add a focus-entry table and distinguish programmatic heading focus from sequential tab order and live-region announcements.

**[Architecture readiness] — FrontComposer module binding names a nonexistent component parameter** (`DESIGN.md:24-38,124-130`; `EXPERIENCE.md:24-33,164-168`; `FrontComposerNavEntry.cs:3-52`; `FrontComposerNavigation.razor.cs:27-39,106-121`)

`FrontComposerNavigation` has no `module-id` parameter; it renders registry entries whose actual contract includes bounded context, title, href, icon, order, policy, enabled state, localization key, and resource.

Fix: bind the real registry API and define how `event-store-admin` remains a stable external/test identity, or make first-class module ID a prerequisite.

**[Architecture readiness] — Selected destructive-dialog primitive cannot express the confirmation contract** (`DESIGN.md:54-60,156-160`; `EXPERIENCE.md:146-156,174,251-252`; `FcDestructiveConfirmationDialog.razor.cs:18-46`)

The primitive exposes only title, plain body, destructive label, and callbacks, but the spine requires structured principal, environment, tenant, target, pre-state, effect, blast radius, reversibility, authorization, and expected evidence.

Fix: add a story-owned structured-facts/`RenderFragment` API or bind a named EventStore-owned `FluentDialog` composition.

**[Architecture readiness] — Recovery actions are not explicitly gated by unwired Operations/audit prerequisites** (`EXPERIENCE.md:215-225,316-326`; `architecture.md:298-314,446-463`; `epics.md:4695-4710,4748-4756,4816-4830`)

Flow 1 presents Retry as available while `eventstore-operations`, durable poison recovery, audit, authorization/attribution, release identity, and authoritative status evidence remain prerequisites.

Fix: make Retry/Archive unavailable until every prerequisite passes and define the intervening read-only state in the operation matrix.

### Low (1)

**[Rubric / Shape fit] — Canonical DESIGN shape and triggered inspiration section are slightly incomplete** (`DESIGN.md:89`; `.memlog.md:7,10`)

`Contract Scope` precedes the canonical DESIGN sections, while EXPERIENCE omits `Inspiration & Anti-patterns` despite explicit Fluent reference products/imports.

Fix: move contract scope into EXPERIENCE Foundation or another contract section and add a compact inspiration/anti-pattern section.

## Mechanical notes

- All five local frontmatter sources and all eight promoted/imported visual references resolve; no `wireframes/` directory is present.
- All four local spacing tokens and every `{spacing.*}` reference resolve. Empty color, typography, and rounded maps are deliberate FrontComposer/Fluent inheritance, not missing local tokens.
- All 23 product-level component names align across DESIGN frontmatter/prose and EXPERIENCE Component Patterns.
- DESIGN's canonical sections remain in relative order; EXPERIENCE contains every default section and triggered Responsive & Platform.
- No Mermaid block appears in either spine.
- Digest verification matches the brownfield architecture, PRD, architecture, and PRD validation sources; only epics differs under the documented pre-repin cycle.
- No UX spine, authority source, import, or mockup was changed by validation.

## Reviewer files

- `review-rubric.md`
- `review-accessibility-support-safety.md`
- `review-architecture-readiness.md`
