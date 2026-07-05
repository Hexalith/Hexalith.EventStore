# Accessibility And Support-Safety Review - eventstore

## Overall verdict

The UX spines have a solid support-safety posture: they distinguish accepted from projection-confirmed success, ban raw diagnostic exposure, require keyboard-operable Fluent components, and name stale/denied/deferred states explicitly. The remaining gaps are evidence-level gaps: localization and mobile mutation behavior need clearer acceptance evidence before implementation.

## Findings

- **medium** Localization evidence is too thin for the PRD requirement (`prd.md:91`, `prd.md:352`; `EXPERIENCE.md:65`). "Use whole strings suitable for localization" is directionally correct, but it does not define how UI stories prove localization readiness. *Fix:* Add a localization evidence rule covering resource-backed strings, no runtime sentence assembly, EN/FR or configured locale checks if applicable, and test selectors that do not depend on translated text.
- **medium** Mobile/narrow behavior allows desktop-first mutations without defining the fallback contract (`EXPERIENCE.md:112`, `EXPERIENCE.md:129-137`, `EXPERIENCE.md:207-209`). The text says complex mutations may remain desktop-optimized but must not become inaccessible; implementers need to know whether a mutation is blocked with a message, simplified, or fully available on small screens. *Fix:* Add a mobile action policy: read/triage always works; each mutating operation is either fully usable, disabled with a reason, or redirected to a safe desktop-required state.
- **medium** Permission-denied and tenant-filter behavior is support-safe but should specify accessible copy and focus handling for empty-vs-denied outcomes (`EXPERIENCE.md:76`, `EXPERIENCE.md:95-96`, `EXPERIENCE.md:181`). The current rule protects tenant existence, but screen-reader users also need an unambiguous state and route context. *Fix:* Add denied-state accessible name/copy rules and focus restoration for denied filters, denied rows, and denied detail links.
- **low** Live-region coverage is named but not prioritized (`EXPERIENCE.md:123`). Command lifecycle, stale data, SignalR disconnect, and evidence confirmation should not all announce with equal urgency. *Fix:* Classify live updates as polite or assertive and define which state transitions move focus or only update a status region.

## Strengths

- Projection-confirmed success is treated as a state evidence problem, not as copy polish (`EXPERIENCE.md:93-94`, `EXPERIENCE.md:110`).
- Raw tokens, JWTs, protected payloads, cursors, ETags, stack traces, and secrets are explicitly banned from UI rendering (`EXPERIENCE.md:139-145`).
- Keyboard coverage names tabs, grids, dialogs, filters, command palette, and accordions (`EXPERIENCE.md:105-112`).
- Status is explicitly not color-only (`DESIGN.md:95`, `EXPERIENCE.md:122`).
