# Spine Pair Review — eventstore

## Overall verdict

The updated pair is **thin** as a final downstream contract. Tokens, states, visual references, and most named journeys are mechanically strong, but finalization is blocked by a source-required tenant-provisioning journey that the draft explicitly omits and by an unresolved detail-panel component binding that neither follows the source component set nor stays name-stable across the spine.

## 1. Flow coverage — thin

The source set contains no standalone user-journey section. The explicit journey requirements UX-DR39 through UX-DR42 are represented by named protagonists, numbered steps, climax beats, and applicable failure paths in Flows 1–3 and 5–6; the remaining flows close the declared IA surfaces. One source-required UI journey remains absent.

### Findings

- **[high]** Story 5.10 requires an accessible **Create Tenant** dialog rejection flow for reserved `system`, but the IA, Open Questions, and tenant-access journey explicitly assume that tenant provisioning is absent (`epics.md:4182-4236`; `EXPERIENCE.md:65,74,357-358,375-383`). A downstream consumer cannot implement the source requirement and the spine simultaneously. *Fix:* decide the provisioning surface before finalization; if retained, add its canonical route/entry point, component/state coverage, and a named-protagonist flow with inline-error, focus-return, zero-request, and non-disclosure failure behavior; if removed, reconcile Story 5.10 and its UI acceptance criteria first.

## 2. Token completeness — strong

All four local spacing tokens are concrete CSS dimensions and every intended token reference resolves. Empty `colors`, `typography`, and `rounded` maps are explicitly justified by FrontComposer/Fluent UI V5 inheritance; route placeholders such as `{tenant}` are not token references. Load-bearing contrast targets are stated for text and non-text combinations.

### Findings

None.

## 3. Component coverage — adequate

The 23 canonical component keys in DESIGN.md frontmatter have corresponding, substantive rows in both DESIGN.md Components and EXPERIENCE.md Component Patterns. One component identity and implementation binding is not closed.

### Findings

- **[high]** The canonical component is named **Detail panel** and bound to an EventStore-owned labelled `aside` with `FluentCard`, while journeys and responsive rules call it a drawer, and UX-DR18 limits detail panels to `FluentDrawer`, `FluentDialog`, or a FrontComposer panel (`DESIGN.md:61-62,159`; `EXPERIENCE.md:193,306,367,389`; `epics.md:224`). This also conflicts with the inherited-system rule to use an existing FrontComposer/Fluent component when one exists. *Fix:* choose one source-permitted primitive, record that exact binding in DESIGN.md frontmatter, and use one identical component name in both component tables, responsive rules, and every flow.

## 4. State coverage — strong

Every IA tab plus the Sample and Tenants consumer surfaces has an explicit empty/stale/unavailable, authorization/scope, and mutation disposition. Cross-surface rules cover cold load, refresh/focus preservation, offline/stale, API and identity failures, denial, revocation, conflict, accepted/pending/timeout/failure, protected outcomes, and unsafe catalog/idempotency evidence; route-specific failure rules add the nested deep-link cases.

### Findings

None.

## 5. Visual reference coverage — strong

All eight files under `imports/` and `mockups/` are linked inline from the relevant DESIGN.md component section and described by what they illustrate. EXPERIENCE.md additionally links the mobile behavior references. There are no files under `wireframes/`, no orphaned artifacts, and the spines-win-on-conflict rule is stated once.

### Findings

None.

## 6. Bloat & overspecification — adequate

The operations and safety material is largely load-bearing, and tab/route/state tables are more extractable than narrative restatement. Repetition still makes the behavioral contract harder to maintain.

### Findings

- **[medium]** The same mutation gates, prohibited sensitive fields, retry rules, projection fan-out rule, and erasure limits recur across Evidence and Mutation Contract, Component Patterns, State Patterns, Interaction Primitives, Support-Safe Operations, Source Traceability, and Key Flows (`EXPERIENCE.md:130-173,176-204,206-273,275-285,323-353,362-449`). This invites clause drift and obscures which section is normative. *Fix:* keep one normative rule/table per concern and make component, state, and flow entries reference that named rule rather than restating long field lists.

## 7. Inheritance discipline — thin

All five local source paths resolve, their recorded SHA-256 values match current bytes, and the Fluent UI V5 URL resolves. Token paths and the 22 unaffected component names remain consistent. The tenant-provisioning contradiction and detail-panel binding/name drift identified above prevent clean source inheritance.

### Findings

No additional findings beyond §§1 and 3.

## 8. Shape fit — adequate

EXPERIENCE.md contains every default section and the triggered Responsive & Platform section; its product-specific safety, localization, traceability, and evidence sections earn their place. DESIGN.md keeps the eight canonical sections in relative order, but one extra section is outside the design spine and one triggered experience section is missing.

### Findings

- **[medium]** `DESIGN.md` inserts `Contract Scope` before the canonical visual spine and uses it for runtime identity, reviewed revision, readiness authority, and migration debt (`DESIGN.md:89-95`). Those are behavioral/source-governance concerns, not visual identity content, and the DESIGN.md spine does not authorize invented sections. *Fix:* move the material to EXPERIENCE.md Foundation/Source authority or a reconciliation artifact, leaving DESIGN.md to the canonical visual sections.
- **[medium]** `Inspiration & Anti-patterns` is absent even though the memlog names the Fluent UI V5 documentation site as the visual baseline and records explicit rejected approaches such as theme redefinition and raw interactive HTML (`.memlog.md:7-10,26,35`; `EXPERIENCE.md`, no such heading). *Fix:* add a compact section that distinguishes what is inherited or learned from the Fluent reference from what must not be copied, without duplicating Brand & Style.

## Mechanical notes

- Source hashes in EXPERIENCE.md match all five local frontmatter sources; the remote Fluent UI V5 source returned HTTP 200.
- All intended `{spacing.*}` references resolve. `{tenant}`, `{domain}`, and `{aggregate}` occur only as route placeholders.
- DESIGN.md canonical visual sections are otherwise correctly ordered. EXPERIENCE.md has all eight default sections and the applicable responsive section.
- The component frontmatter and both component tables contain 23 aligned contract components; **Detail panel/detail drawer** is the only name/binding inconsistency found.
- All eight promoted/imported visual files resolve; no wireframes are present.
- Neither spine contains Mermaid, so there is no Mermaid syntax to validate.
