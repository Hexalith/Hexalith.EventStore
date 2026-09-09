# Spine Pair Review — eventstore

## Overall verdict

The spine pair is an adequate downstream contract: token, component, state, and visual-reference coverage are strong, and all source-defined UX journeys are behaviorally represented. It should not drive the detail-panel implementation unchanged, however, because that binding crosses the repository's FrontComposer/Fluent reuse boundary; the source-snapshot ambiguity and flow-name traceability should also be resolved before automated extraction relies on the pair.

## 1. Flow coverage — adequate

The source set has no standalone UJ catalog; its explicit journey requirements are UX-DR39 through UX-DR42. Flows 1–3 and 5–6 cover those obligations with named protagonists, numbered steps, a climax, and an applicable failure path; the remaining flows close the ten-tab IA and external Sample/Tenants surfaces.

### Findings

- **medium** The implemented journey requirements are inferable but not carried forward verbatim: the source names UX-DR39's “incident-recovery journey,” UX-DR40's “tenant-access mutation journeys,” UX-DR41's “command-investigation journeys,” and UX-DR42's Sample/Tenants workflow, while the Key Flow headings omit the requirement IDs and rename several journeys (for example, “Incident triage” and “Admin tenant access review”) (`_bmad-output/planning-artifacts/epics.md:266`, `_bmad-output/planning-artifacts/epics.md:272`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:316`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:366`). *Fix:* Prefix the applicable headings with UX-DR39 through UX-DR42 and retain each source journey phrase verbatim, with UX-DR42 explicitly pointing to both consumer flows.

## 2. Token completeness — strong

The frontmatter deliberately inherits all color, typography, and shape tokens from FrontComposer/Fluent UI V5, defines only four local spacing dimensions, and maps 23 product-level components. Every design-token reference resolves to a defined spacing token; route placeholders such as `{tenant}` are code-span URL parameters, not design-token references. The contract states WCAG 2.2 AA text contrast and a 3:1 non-text floor for the load-bearing combinations (`_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:15`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:23`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:103`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:111`).

### Findings

- No material findings.

## 3. Component coverage — strong

All 23 top-level component names in `DESIGN.md` frontmatter have a matching visual row in `DESIGN.md.Components` and an identically named behavioral row in `EXPERIENCE.md.Component Patterns`. Fluent and FrontComposer primitives used inside those compositions are explicitly inherited rather than presented as EventStore-owned components (`_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:23`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:142`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:158`).

### Findings

- No material findings.

## 4. State coverage — strong

The ten dashboard tabs plus the separate Sample UI and Tenants UI are covered by cross-surface cold-load, refresh, empty, stale/offline, unavailable, authentication, authorization, conflict, accepted, pending, timeout, terminal-failure, and unknown states, followed by a per-surface disposition matrix. Focus, background-update preservation, disabled reasons, and safe recovery are specified in Interaction Primitives and Accessibility Floor (`_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:54`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:188`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:213`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:244`).

### Findings

- No material findings.

## 5. Visual reference coverage — strong

The inventory contains two imported Fluent UI V5 captures and six promoted Overview/Commands mock/render files, with no wireframes. `DESIGN.md` links all eight inline, states what each group illustrates, marks them non-copyable, and states once that the spines win; `EXPERIENCE.md` additionally links the two narrow-screen renders where their responsive behavior is specified (`_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:172`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:270`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:281`).

### Findings

- No material findings.

## 6. Bloat & overspecification — adequate

The pair is dense, but its route, evidence, mutation, safety, localization, and responsive tables are load-bearing for a regulated/support-critical operations UI. It avoids restating personas and feature scope as narrative, and delegates visual defaults to the inherited systems; the only material volatility problem is the snapshot issue recorded under inheritance discipline.

### Findings

- No additional material findings.

## 7. Inheritance discipline — thin

All five local frontmatter sources resolve, the official Fluent UI V5 URL is explicitly upstream, product vocabulary is consistent, and all design-token references resolve. FrontComposer and Fluent UI V5 inheritance plus the no-theme-redefinition rule are otherwise explicit.

### Findings

- **high** The `detail-panel` binding selects an EventStore-owned labelled `aside` composed with `FluentCard`, but source requirement UX-DR18 requires `FluentDrawer`, `FluentDialog`, or a FrontComposer panel, and repository guidance permits custom markup only when no equivalent FrontComposer/Fluent component exists (`_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:61`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:175`, `_bmad-output/planning-artifacts/epics.md:224`, `references/Hexalith.AI.Tools/hexalith-ux-instructions.md:10`). *Fix:* Bind Detail panel to an approved Fluent/FrontComposer primitive, add the reusable panel to FrontComposer if that capability is missing, or reconcile UX-DR18 and explicitly document the no-equivalent exception before EventStore implements custom markup.
- **medium** The spine records the Epics input SHA-256 as `5a5c03d1…` “before downstream digest repinning,” while the current file hashes to `d067c8fb…`; the next paragraph nevertheless says any later digest change reopens reconciliation. A downstream consumer cannot mechanically distinguish the acknowledged repin from unreconciled source drift (`_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:39`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:44`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md:47`). *Fix:* Bind a reproducible normalized Epics digest that excludes downstream UX digest fields, or record both pre-repin and final digests with an explicit comparison rule.

## 8. Shape fit — adequate

The canonical DESIGN.md sections are present in their required relative order, and EXPERIENCE.md contains all default sections plus the triggered Responsive & Platform section. Product-specific Evidence and Mutation, Localization, Support-Safe Operations, and Source Traceability sections earn their place.

### Findings

- **low** `DESIGN.md` inserts the noncanonical `Contract Scope` section before `Brand & Style`, while EXPERIENCE.md omits the triggered `Inspiration & Anti-patterns` section even though the memlog and imported captures establish the Fluent documentation site as a visual reference (`_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md:89`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/.memlog.md:7`, `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/.memlog.md:10`). *Fix:* Move contract-scope material into EXPERIENCE.md Foundation or another appropriate contract section, and add a compact Inspiration & Anti-patterns section naming what is inherited from the Fluent reference and what is expressly rejected.

## Mechanical notes

- YAML frontmatter is complete for the inheritance posture: required names/descriptions are present where applicable, local spacing values are CSS dimensions, and component entries are mapping objects.
- All local `sources:` paths and all eight inline visual-reference paths resolve. The external Fluent UI V5 URL is treated as upstream system documentation, not a local token source.
- The 23 product-level component names match across DESIGN.md and EXPERIENCE.md; subordinate Fluent/FrontComposer primitive names are explicit inheritance bindings.
- Every `{spacing.*}` reference resolves. Braced route segments in code spans are URL parameters and were excluded from token resolution.
- No Mermaid block appears in either spine, so Mermaid syntax is not applicable.
- Severity totals: critical 0, high 1, medium 2, low 1.
