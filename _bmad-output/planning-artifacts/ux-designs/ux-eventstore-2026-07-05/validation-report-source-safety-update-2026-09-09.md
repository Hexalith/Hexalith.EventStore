# Validation Report — eventstore source-safety UX update

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-eventstore-2026-07-05/EXPERIENCE.md`
- **Run at:** 2026-09-09T19:17:39+02:00
- **Review set:** rubric walker, accessibility/support safety, architecture/readiness

## Overall verdict

The updated pair is safe as a draft but thin as a final downstream contract. Its token, state, evidence, source-digest, and visual-reference coverage is strong; it also materially improves recovery gating, projection fan-out, tenant-boundary handling, audit phases, erasure claims, and unsafe idempotency/catalog outcomes.

Finalization is blocked by three source or product decisions: Story 5.10 requires a Create Tenant dialog that the draft omits, Story 7.14 omits the canonical `/types` route from its exhaustive manifest, and the canonical `ux.md`/folder index still advertise the superseded final state. The reviewers also found actionable component, validation, identifier-display, mock-fixture, keyboard, live-region, localization, authentication-recovery, and document-shape gaps.

## Category verdicts

- Flow coverage — thin
- Token completeness — strong
- Component coverage — adequate
- State coverage — strong
- Visual reference coverage — strong
- Bloat & overspecification — adequate
- Inheritance discipline — thin
- Shape fit — adequate

## Findings by severity

### Critical (0)

No critical findings.

### High (7)

**Flow coverage / Architecture — Tenant provisioning contradicts Story 5.10** (§ EXPERIENCE.md Information Architecture, Open Questions, Flow 2)

The draft says tenant provisioning is absent, while Story 5.10 explicitly requires the existing Create Tenant dialog to reject reserved `system`, associate a localized inline error, return focus, send no request, and reveal no resource existence.

Fix: either include the backlog-gated Create Tenant flow under Tenants & Access with full component/state/journey coverage, or first amend the authoritative epic contract. A UX-only assumption cannot override Story 5.10.

**Component coverage / Inheritance — Detail panel has no closed primitive** (§ DESIGN.md Components; EXPERIENCE.md Component Patterns and flows)

The component is bound to an EventStore-owned `aside` with `FluentCard`, while journeys call it a drawer and UX-DR18 permits `FluentDrawer`, `FluentDialog`, or a FrontComposer panel.

Fix: select one source-permitted primitive and use one name and one modal/non-modal focus model across frontmatter, both component tables, responsive rules, mocks, and flows.

**Accessibility — Form-validation interaction is incomplete** (§ EXPERIENCE.md Mutation progression and Accessibility Floor)

The contract rejects invalid and oversized input without defining persistent inline errors, programmatic field association, invalid state, a deduplicated localized summary, deterministic focus, or non-echo of rejected sensitive input.

Fix: add one complete accessible validation state and corresponding Operation dialog/test rules.

**Support safety — Operational identifier display is not closed** (§ EXPERIENCE.md Evidence and Mutation Contract; Support-Safe Operations)

`MessageId`, `CorrelationId`, request ID, and operation/audit references are called bounded and support-safe, but their authoritative grammar, display abbreviation, bidi isolation, URL, clipboard/export/log, accessible-name, and server-projection policies are not fully bound.

Fix: add a field-level safe-presentation table sourced from Story 7.5 and AD-32, ensuring forbidden values never reach browser serialization or client memory merely to be hidden.

**Visual references — Current mock fixtures contradict safety assumptions** (§ DESIGN.md visual references; Overview and Commands mocks)

The Overview mock renders `system` as tenant evidence, and the Commands mock renders `CreateTenant` while the draft says provisioning is absent. Although the spines win, these promoted artifacts can seed unsafe tests and demos.

Fix: resolve the capability decision, replace the reserved tenant fixture, and retain `CreateTenant` only if provisioning is confirmed.

**Architecture — `/types` is missing from Story 7.14's exhaustive route manifest** (§ EXPERIENCE.md routing; epics Story 7.14)

The UX correctly keeps the live `/types` catalog under Streams & Events, but the story's machine-validated route list omits it.

Fix: reconcile the upstream epic manifest and its inner-tab policy before treating the pair as a conflict-free final handoff.

**Architecture — Canonical handoff metadata is stale** (§ `_bmad-output/planning-artifacts/ux.md`, folder `index.md`, prior reconciliation)

The spines are draft at revision `0994c37814c37dac7667a209dbd0659125aac49e`, while canonical handoff artifacts still say final at revision `23a722a1ffe29099a9d87df266552be4e3addd82` and say fresh validation was skipped.

Fix: after resolving findings, republish the handoff/index with this validation set and consistent revision/status; retain older reviews as historical.

### Medium (12)

**Bloat — Safety rules repeat across too many sections** (§ EXPERIENCE.md Evidence, Components, States, Interaction, Support Safety, Traceability, Flows)

Fix: keep one normative table per concern and make other sections reference it rather than repeat long field and gate lists.

**Shape — Contract Scope is outside the canonical DESIGN.md spine** (§ DESIGN.md Contract Scope)

Fix: move runtime identity, reviewed revision, readiness authority, and migration debt into EXPERIENCE.md Foundation/Source authority or reconciliation material.

**Shape — Inspiration & Anti-patterns is missing** (§ EXPERIENCE.md)

Fix: add a compact section distinguishing inherited Fluent composition from rejected theme redefinition, copied navigation geometry, and raw interactive HTML.

**Accessibility — Announcement priorities are conflated** (§ EXPERIENCE.md Live status regions and Accessibility Floor)

Fix: define stable polite view/operation regions, a shell-level assertive critical region, and a form-scoped polite validation summary with ownership, clearing, atomicity, and deduplication rules.

**Accessibility — Contrast claims lack version-bound evidence** (§ DESIGN.md Colors; EXPERIENCE.md Accessibility Floor)

Fix: keep the inherited-token contract, identify the Builds-resolved Fluent version in test evidence, and run Story 7.20's light/dark/system/forced-color state matrix. Treat the older RC4 screenshots as composition references only.

**Accessibility — Horizontal tabs and grids need a complete keyboard/reflow profile** (§ DESIGN.md Layout; EXPERIENCE.md Component Patterns and Responsive & Platform)

Fix: bind the resolved Fluent V5 keyboard model, scroll focused/selected items fully into view, expose overflow instructions when needed, preserve both axes, and test at 320 CSS pixels and 400% zoom.

**Localization — Canonical state IDs and localized labels are conflated** (§ DESIGN.md Status badge; EXPERIENCE.md Localization)

Fix: separate invariant typed IDs/selectors from complete localized visible/accessibility labels and consequences.

**Authentication — Recovery and safe return routing are underspecified** (§ EXPERIENCE.md State Patterns and routing)

Fix: name the implemented recovery primitive and permit only same-origin canonical return routes with allow-listed safe state; strip rejected, cross-tenant, wildcard, reserved, oversized, and opaque values.

**Accessibility — Text-spacing evidence lacks the WCAG override profile** (§ EXPERIENCE.md Accessibility Floor)

Fix: require the WCAG 1.4.12 profile—1.5 line height, 2× paragraph spacing, 0.12× letter spacing, and 0.16× word spacing—with no loss, clipping, overlap, or obscured focus.

**Product decision — Restore/import disposition remains open** (§ EXPERIENCE.md Open Questions; Story 7.4)

Fix: confirm whether legacy restore/import controls are removed into the canonical unsupported treatment or whether useful authenticated history receives a read-only owner. No runnable affordance may survive while deferred.

**Architecture — AD-17 absolute `Location` behavior is absent** (§ EXPERIENCE.md command-status evidence and traceability)

Fix: consume a valid gateway-authored absolute `Location` when supplied, tolerate omission, never synthesize or repair it, and retain `MessageId` as the sole status selector.

**Architecture — AD-32 correlation grammar and propagation are absent** (§ EXPERIENCE.md evidence/identifier rules)

Fix: bind `X-Correlation-ID` to 1–128 ASCII alphanumeric or hyphen characters, accept or mint it only at the first public boundary, propagate without reminting, never GUID-parse it, and never use it as a status selector.

### Low (1)

**Accessibility — Route heading ownership is ambiguous** (§ DESIGN.md Typography; EXPERIENCE.md Accessibility Floor)

Fix: require exactly one focusable route `h1`; the selected tab remains a tab rather than a second heading, with subordinate panel/dialog heading levels defined relative to the route title.

## Mechanical notes

- All five local source paths resolve and their SHA-256 values match `EXPERIENCE.md`.
- All intended `{spacing.*}` references resolve; `{tenant}`, `{domain}`, and `{aggregate}` are route placeholders.
- All 23 product-level component names align across DESIGN.md frontmatter and both component tables; the Detail panel implementation/name semantics remain unresolved.
- All eight promoted/imported visual files resolve; there are no wireframes.
- DESIGN.md's canonical visual sections remain in relative order after the extra Contract Scope section; EXPERIENCE.md contains all default sections and Responsive & Platform, but not triggered Inspiration & Anti-patterns.
- No Mermaid blocks are present.
- Consolidated unique findings: critical 0, high 7, medium 12, low 1.

## Reviewer files

- `review-rubric-source-safety-update-2026-09-09.md`
- `review-accessibility-support-safety-source-safety-update-2026-09-09.md`
- `review-architecture-readiness-source-safety-update-2026-09-09.md`

Older `review-*.md` files remain historical and were not treated as validation of this reopened draft.
