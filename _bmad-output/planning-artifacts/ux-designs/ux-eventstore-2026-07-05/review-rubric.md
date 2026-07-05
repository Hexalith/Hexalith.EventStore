# Spine Pair Review - eventstore

## Overall verdict

The spine pair is directionally sound for the brownfield EventStore Admin UX: it commits to the single `Event Store Admin` module entry, tabbed child dashboard, FrontComposer/Fluent UI V5, projection-confirmed success, and support-safe operational states. It is not yet readiness-clean as a downstream contract because token definitions are not concrete enough, component names do not line up across the two spines, visual references are not promoted or linked, and source-required Sample/Tenants UI coverage is still implicit.

## 1. Flow coverage - thin

Checked PRD UI artifact requirements, architecture UI decisions, epics/stories, and the IA and Key Flows in `EXPERIENCE.md`.

### Findings

- **high** Source-required Sample UI and Tenants UI behavior is not covered by explicit key flows (`prd.md:48`, `prd.md:243-244`, `prd.md:352`; `EXPERIENCE.md:156-203`). The current flows focus on the consolidated admin dashboard. Story-driving UI requirements for Sample accepted-submission behavior and Tenants projection-confirmed success are present only as generic state rules or an admin tenant-access flow. *Fix:* Add named key flows for "Sample accepted submission" and "Tenants projection-confirmed update" or add an explicit UI-surface coverage table that maps these source requirements to concrete steps, climax, and failure states.
- **medium** Several dashboard tabs have IA placement but no representative operator journey (`EXPERIENCE.md:35-46`, `EXPERIENCE.md:156-203`). Topology, Storage & Snapshots, Settings, and Projections are important support surfaces, but only Recovery, Tenants & Access, Commands, and Deferred operations get full flows. *Fix:* Add short journeys or a tab-to-flow coverage matrix for every dashboard tab so implementation agents can see which states and actions are expected.

## 2. Token completeness - broken

Checked YAML tokens in `DESIGN.md`, `{path.to.token}` references, and whether load-bearing color and contrast choices can be implemented without guesswork.

### Findings

- **critical** Color tokens do not provide hex values or light/dark token pairs (`DESIGN.md:14-27`). The rubric treats missing hex or applicable light/dark color pairs as critical because downstream code mirrors the spine. The prose says to inherit Fluent, which is correct, but the token contract still leaves implementers to guess exact variables or runtime aliases. *Fix:* Define each color token as a concrete Fluent UI V5/Fluent 2 design-token reference with resolved light/dark values where available, or explicitly mark the token as an inherited CSS variable with its exact variable name and fallback.
- **medium** Radius and spacing tokens are partly semantic rather than implementable dimensions (`DESIGN.md:38-48`). Examples include "Fluent default small radius" and "Fluent/FrontComposer shell default content padding." *Fix:* Convert the load-bearing values to exact token names, CSS variables, or fixed dimensions such as `8px`, while keeping platform-native dynamic behavior where it is truly owned by Fluent or FrontComposer.
- **medium** Contrast targets for load-bearing state combinations are not stated (`DESIGN.md:88-98`, `EXPERIENCE.md:114-127`). Accessibility inherits from Fluent, but status badges, warning banners, disabled/deferred actions, and stale states need minimum contrast expectations. *Fix:* Add a short contrast contract: WCAG 2.2 AA for text and non-text indicators, badge text not color-only, and Fluent token combinations that satisfy the target.

## 3. Component coverage - thin

Checked component names in `DESIGN.md` frontmatter/prose and `EXPERIENCE.md` Component Patterns.

### Findings

- **high** The component taxonomy is not normalized across the two spines (`DESIGN.md:49-77`, `DESIGN.md:130-142`, `EXPERIENCE.md:67-83`). `EXPERIENCE.md` defines behavioral components such as Detail panel, Command lifecycle tracker, Projection freshness indicator, Deferred operation placeholder, and Command palette that do not all have matching visual rows in `DESIGN.md.Components`. `DESIGN.md` names Dashboard header, Filter bar, Status badge, and Multi-section panel, but these are absent or differently named in `EXPERIENCE.md.Component Patterns`. *Fix:* Create one canonical component matrix with identical names in both spines and a visual plus behavioral rule for each component.
- **low** Naming alternates between singular/plural and UI-system names (`DESIGN.md:72`, `DESIGN.md:140`, `EXPERIENCE.md:81`). `operations-dialog` and "Operation dialog" likely mean the same component, but downstream agents should not have to infer that. *Fix:* Use one slug and one display name everywhere.

## 4. State coverage - adequate

Checked IA surfaces against expected cold load, empty, stale/offline, permission-denied, validation, and terminal failure states.

### Findings

- **medium** State patterns are strong globally but thin per surface (`EXPERIENCE.md:85-101`). Topology, Storage & Snapshots, Settings, and Deferred & Backlog have unique empty, partial-permission, degraded, and read-only cases that are not named by tab. *Fix:* Add a compact tab-state matrix that shows the required cold, empty, stale/offline, denied, and terminal states for each dashboard tab.

## 5. Visual reference coverage - broken

Checked `mockups/`, `wireframes/`, `imports/`, `.working/`, and inline references from the spines.

### Findings

- **high** Visual references are orphaned in `.working/` and are not linked from either spine (`.memlog.md:8`, `.memlog.md:22`; `DESIGN.md`, `EXPERIENCE.md`). The run folder contains rendered Fluent reference screenshots and key mock screenshots, but no `mockups/` or `wireframes/` assets and no inline links explaining what each visual illustrates. *Fix:* Promote accepted screenshots to `mockups/` or `wireframes/`, link them from the relevant sections, and state that `DESIGN.md` and `EXPERIENCE.md` win on conflicts.
- **medium** The key mocks are raw standalone HTML artifacts (`.working/key-dashboard-overview.html`, `.working/key-command-investigation.html`). This is acceptable as validation scaffolding, but it can be mistaken for implementation guidance in a repo that requires FrontComposer and Fluent components. *Fix:* Label raw HTML mocks as non-implementation artifacts or replace them with FrontComposer/Fluent-oriented mock descriptions.

## 6. Bloat & overspecification - adequate

The spines are compact and mostly decision-oriented. They avoid decorative narrative and do not restate the entire PRD. The biggest risk is underspecification in some matrices, not bloat.

### Findings

- **low** The `Deferred & Backlog` tab could become a catch-all planning page rather than an operational surface (`EXPERIENCE.md:45`, `EXPERIENCE.md:195-203`). *Fix:* Keep it limited to honest unavailable-operation discovery, role-gated where appropriate, and link to backlog status without turning the admin UI into planning documentation.

## 7. Inheritance discipline - thin

Checked frontmatter source resolution, glossary/requirement naming, component naming, and whether `EXPERIENCE.md` resolves its visual references through `DESIGN.md`.

### Findings

- **medium** `EXPERIENCE.md` references `DESIGN.md` generally but does not use resolved token references for visual dependencies (`EXPERIENCE.md:19`, `EXPERIENCE.md:67-83`, `EXPERIENCE.md:114-127`). *Fix:* Where behavioral rules depend on visual tokens, reference the exact `DESIGN.md` token path, such as `{colors.status-warning}` or `{components.status-badge}`.
- **medium** Source requirement names are paraphrased rather than mapped verbatim (`prd.md:76-79`, `prd.md:212-213`, `EXPERIENCE.md:85-101`). The meaning is mostly preserved, but traceability would be stronger if "Projection-Confirmed Success", "Support-Safe State", `NFR14`, and `NFR15` appeared in a source coverage table. *Fix:* Add a traceability table that maps source terms and NFRs to UX sections and flows.

## 8. Shape fit - adequate

`DESIGN.md` uses the required section order. `EXPERIENCE.md` includes Foundation, Information Architecture, Voice and Tone, Component Patterns, State Patterns, Interaction Primitives, Accessibility Floor, Key Flows, plus applicable Responsive and Inspiration/Anti-pattern sections.

### Findings

- **low** Both spines remain marked `status: draft` (`DESIGN.md:4`, `EXPERIENCE.md:3`). That is fine for review, but not for readiness handoff. *Fix:* After addressing validation findings, promote the status to `reviewed` or the repository's accepted final status.

## Mechanical notes

- Sources in frontmatter resolve.
- `imports/` exists but is empty.
- No `mockups/` or `wireframes/` directory exists in the run folder.
- `.working/` contains useful evidence but is not part of the formal visual reference set.
- No non-ASCII characters were found in the generated spine and mock files during validation.
