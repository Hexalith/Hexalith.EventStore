# Architecture Readiness Review — Hexalith.EventStore Admin

## Overall assessment

**Not architecture-ready without reconciliation.** The spine pair commits a coherent operational posture—one existing Admin UI host, FrontComposer/Fluent UI V5, typed-client boundaries, projection-backed confirmation, support-safe failures, and honest deferred operations—but a downstream architect or story developer still has to guess at load-bearing integration details. Most seriously, the source list combines an obsolete `ready` report with the current PRD's explicit `blocked` / `reject` status and provides no authority order, baseline, or digest. The remaining high-impact gaps are the FrontComposer/module identity contract, an incomplete route inventory, undefined canonical deep-link behavior, an unbound Admin evidence transport, incomplete fail-closed authentication states, and visual references that encode prohibited legacy token families.

## Finding counts

| Severity | Count |
|---|---:|
| Critical | 1 |
| High | 6 |
| Medium | 5 |
| Low | 2 |
| **Total** | **14** |

## Findings grouped Critical/High/Medium/Low

### Critical

#### 1. The declared source baseline gives contradictory implementation authority

- **Location:** `DESIGN.md:4-13`; `EXPERIENCE.md:3-12`; `_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-05.md:1-12,24-34`; `_bmad-output/planning-artifacts/prd.md:3-11`; `_bmad-output/planning-artifacts/prds/prd-eventstore-2026-07-05/validation-report.md:5-13`.
- **Note:** Both final spines, updated 2026-08-01, cite the 2026-07-05 readiness report. That report records `status: ready`, while the current PRD records `implementation_readiness_status: blocked` and `implementation_readiness_result: reject`; the current PRD validation explicitly says not to use the planning set to authorize implementation, completion, migration, or release. The spines also cite mutable paths without an authority order, reviewed baseline SHA, or content digests. A consumer cannot determine which source state governs and could treat a historical readiness decision as current authorization.
- **Fix:** Replace the July readiness report in both frontmatter blocks with the current readiness/validation authority, add the canonical `_bmad-output/planning-artifacts/ux.md` and UX `index.md`, and record an explicit authority order plus reviewed repository SHA and source digests. State plainly that `status: final` means the UX document is finalized, not that implementation is authorized. Re-run source-drift validation before restoring an architecture-ready verdict.

### High

#### 2. Host, service, container, and FrontComposer module identities are not separated

- **Location:** `DESIGN.md:164,216-219`; `EXPERIENCE.md:19-21,31-35,92-95,298-301`; `_bmad-output/planning-artifacts/ux.md:30-34`; `_bmad-output/planning-artifacts/architecture.md:214-218,317-321`; `src/Hexalith.EventStore.AppHost/Program.cs:93-94`; `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj:9-10,24-29`.
- **Note:** The spines commit the project path, the `eventstore-admin-ui` resource/container identity, and the visible label, but omit the stable FrontComposer module id `event-store-admin` and do not distinguish it from the Admin Server resource `eventstore-admin`. They say “FrontComposer” generically rather than committing the adopted `Hexalith.FrontComposer.Shell` + `Hexalith.FrontComposer.Contracts.UI` package boundary and Builds-catalog family rule. The current Admin UI project references Fluent packages but no FrontComposer package, so this missing handoff is an actual migration prerequisite, not an implementation detail.
- **Fix:** Add a Foundation/host-integration identity table with: project/assembly `Hexalith.EventStore.Admin.UI`; Admin Server resource `eventstore-admin`; UI resource/container `eventstore-admin-ui`; FrontComposer module id `event-store-admin`; label **Event Store Admin**. Name Shell and Contracts.UI as required dependencies, require one catalog-governed FrontComposer family version in source/package modes, and record the catalog-entry prerequisite before adoption.

#### 3. The legacy route inventory drops the live Type Catalog

- **Location:** `EXPERIENCE.md:37-50,54`; `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor:1`; `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor:15-18`; `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs:22-27`; `_bmad-output/planning-artifacts/epics.md:5553,5577-5580`.
- **Note:** The IA claims that every legacy Admin UI feature has a destination, but `/types` is absent. The current host has a live `/types` route, a visible Types nav item, and command-palette entries for event, command, and aggregate types. Even Story 7.14 says the host has 22 routes while its route list repeats the spine's omission. Following the spine can silently delete a live operator capability or force a developer to invent an unowned tab.
- **Fix:** Add `/types` to the canonical route inventory, choose its owning tab and detail/sub-tab disposition, define what happens to `/types?tab=events|commands|aggregates`, and propagate the same correction to UX-DR4 and Story 7.14's route manifest.

#### 4. “Legacy source routes” is not a canonical routing and deep-link contract

- **Location:** `EXPERIENCE.md:39-54,95,148-152`; `_bmad-output/planning-artifacts/architecture.md:317-321`; `_bmad-output/planning-artifacts/epics.md:5572-5590`.
- **Note:** The spines list old paths and say deep links “may remain,” but do not identify which URL is canonical, which old URL redirects, how a selected `FluentTabs` item maps to the URL, which tenant/domain/aggregate and filter query parameters are permitted, how malformed or denied parameters resolve, or what browser back/forward must restore. AD-21 and Story 7.14 require one canonical route table and explicit compatibility redirects. This gap leaves routing, authorization, and duplicate-page behavior to individual stories.
- **Fix:** Add a machine-readable route matrix with legacy route, canonical URL, owning tab/detail, path/query/fragment schema and bounds, authorization behavior, redirect status/history behavior, selected-module/tab result, and unknown/malformed/denied outcomes. Commit one direction of synchronization between router state and `FluentTabs` selection.

#### 5. Projection lifecycle semantics have no bound Admin transport contract

- **Location:** `DESIGN.md:140-147,228-229`; `EXPERIENCE.md:60,65-67,104-105,117-129,153`; `_bmad-output/planning-artifacts/architecture.md:227-241`; `_bmad-output/planning-artifacts/epics.md:5842-5845,5857-5870`; `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionStatus.cs:6-22`; `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionStatusType.cs:3-17`; `src/Hexalith.EventStore.Contracts/Queries/QueryResponseProvenance.cs:6-29`; `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecycleState.cs:6-53`.
- **Note:** The UI behavior correctly distinguishes `ProjectionBacked`, `HandlerComputed`, lifecycle values, and fail-safe `Unknown`, but the spine does not say which typed Admin facet/DTO supplies provenance, lifecycle, evidence time, or projection version for each surface. The current Admin projection DTO instead carries the different operational vocabulary `Running`, `Paused`, `Error`, `Rebuilding`, plus lag/throughput/time. Without an explicit adapter/contract decision, a developer can conflate processing status with projection lifecycle, infer currentness from lag or time, or change a public DTO ad hoc.
- **Fix:** Add a per-surface evidence matrix naming the Story 7.5 typed-client facet and DTO fields for provenance, lifecycle, observation/freshness time, version, and terminal command evidence. Keep `ProjectionStatusType` explicitly separate from `ProjectionLifecycleState`. Where the Admin contract lacks authoritative provenance/lifecycle, require `Unknown`, disable mutation by default, and name the owning contract-change story rather than permitting UI inference.

#### 6. Fail-closed behavior does not cover authentication and revocation transitions

- **Location:** `EXPERIENCE.md:21-23,119,133-144,154,199-214,296`; `_bmad-output/planning-artifacts/epics.md:5660-5674,5882-5890`; `_bmad-output/planning-artifacts/architecture.md:147-151`.
- **Note:** The spine has a good denied-resource rule, but its state table has only a generic Access denied row. It does not distinguish unauthenticated, expired session, wrong scope, permission revoked during load/action, or authentication-provider unavailable; nor does it commit clearing protected/transient state, cancelling background retries, or excluding protected values from client logs/telemetry. Current stories require those behaviors while separately keeping interactive OIDC a deferred capability. A developer must otherwise guess whether to show login, retry, empty, stale, or denied state.
- **Fix:** Add an authentication/authorization transition matrix for unauthenticated, expired, denied, wrong-scope, revoked-during-action, and provider-unavailable outcomes. For each, specify cache/transient-state clearing, polling/SignalR cancellation, focus and route behavior, safe next action, and logging/telemetry redaction. State that interactive OIDC/login controls remain unavailable until their separately authorized implementation exists.

#### 7. Promoted visual references encode prohibited Fluent token families

- **Location:** `DESIGN.md:166,170-180`; `EXPERIENCE.md:25`; `mockups/dashboard-overview.html:8-27`; `mockups/command-investigation.html:8-26`; `references/Hexalith.AI.Tools/hexalith-ux-instructions.md:25-39`.
- **Note:** Both spines link the HTML mocks inline as visual references, and both mocks define their colors using `--accent-fill-*`, `--foreground-on-accent-*`, `--neutral-layer-*`, `--neutral-foreground-*`, `--neutral-stroke-*`, and `--neutral-fill-*`. Repository UX policy explicitly bans these legacy Fluent v4/FAST families in favor of Fluent UI V5 component parameters or Fluent 2 tokens. The “spines win” disclaimer establishes precedence but does not stop a story developer from copying the only executable visual reference.
- **Fix:** Regenerate the mocks using current Fluent 2 token names/component roles or remove implementation-like token declarations from them. Add an unmistakable in-artifact non-copy banner, then validate mocks and the implemented UI with the repository's legacy-token scan.

### Medium

#### 8. The IA-to-flow closure assertion is false for several tabs

- **Location:** `EXPERIENCE.md:37-54,225-296`.
- **Note:** The spine says every target surface supports an administrator/operator journey. The six flows directly cover Recovery, Tenants & Access, Commands, Streams & Events, Deferred & Backlog, and external Sample/Tenants hosts; Overview is mainly a waypoint. Projections has no complete rebuild/lag flow, and Topology, Storage & Snapshots, and Settings have no journey. Their action boundaries and climax evidence therefore cannot be derived from the Key Flows.
- **Fix:** Add concise named-protagonist flows for projection rebuild/failure, topology diagnosis, storage/snapshot evidence, and settings—or narrow the closure claim and mark those tabs as explicitly spine-only/read-only with no independent mutation journey.

#### 9. Command and recovery terminal evidence is not source-bound

- **Location:** `DESIGN.md:135-139,228`; `EXPERIENCE.md:101,104,117-118,121,153-154,227-262`; `_bmad-output/planning-artifacts/epics.md:5857-5860,5877-5880,5892-5905`.
- **Note:** The eight command states and accepted → evidence-pending → terminal pattern are strong, but the spine does not define the authoritative producer, observation timestamp, stable operation identity, retry identity policy, or audit/evidence agreement for each terminal state. It also does not explicitly distinguish command `Completed` from a separate projection-confirmed read-model outcome. Story-dev can therefore implement internally consistent but incompatible “success” tests.
- **Fix:** For every mutating/recovery action, specify accepted evidence, terminal command evidence, projection/read-model confirmation when applicable, audit correlation, observation time, timeout/cancellation behavior, and stable retry identity. State explicitly which user-visible outcome is allowed at command `Completed` before projection confirmation.

#### 10. Responsive rules leave action disposition to the implementer

- **Location:** `EXPERIENCE.md:155,189-197,300-302`; `_bmad-output/planning-artifacts/epics.md:5970-5988`; `mockups/dashboard-overview.html:249-263`; `mockups/command-investigation.html:224-234`.
- **Note:** The three spine breakpoints are clear, but “fully usable, disabled, or desktop-required” is not resolved per mutation or tab. It also does not commit which identity/evidence columns remain visible, where two-dimensional grid scrolling is allowed, or how focus/back navigation works from a viewport-sized dialog. The mocks compound the ambiguity by switching layouts at 900px and 1000px rather than the contract's 960px boundary.
- **Fix:** Add a per-tab responsive priority/action matrix that names retained columns/context, detail-panel migration, permitted grid overflow, and each mutation's narrow-screen disposition. Align promoted mocks to the 960/1280 contract and include zoom/reflow and focus-return acceptance evidence.

#### 11. Traceability names concepts but not the current owning requirements and stories

- **Location:** `EXPERIENCE.md:56-69`; `_bmad-output/planning-artifacts/epics.md:190-272,5539-5553,5824-5838,5917-5929`.
- **Note:** Source Traceability covers a useful subset of concepts, but it does not map UX-DR1–UX-DR42 or the current owners (notably Stories 7.4, 7.5, 7.14, 7.19, and 7.20). Its “Evidence expected from stories” column contains no story identifiers, and `FR36 / AD-19 / AD-20` is presented as lifecycle authority even though FR4/AD-15 define the consumer provenance/lifecycle rule and FR36 governs parity closure. A consumer must rediscover ownership from a large mutable epic file.
- **Fix:** Add requirement/decision and owning-story columns with exact current IDs, split provenance/lifecycle authority (FR4/AD-15) from projection execution/rebuild semantics (AD-19/AD-20) and parity proof (FR36), and record the baseline/digest from which the mapping was extracted.

#### 12. “Topology” names two different operator concepts

- **Location:** `EXPERIENCE.md:43,46,140`; `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor:29-56`.
- **Note:** In the proposed IA, Topology means DAPR resources, actors, pub/sub, resiliency, health history, and services. In the current Admin UI, “Topology” is a tenant/domain tree that navigates to filtered Streams. The spine never assigns or retires that current navigator, so migration stories can preserve two Topology concepts or accidentally discard a useful stream filter path.
- **Fix:** Rename or explicitly dispose of the current tenant/domain navigator. If retained, assign it to the Streams & Events filter/drill-in contract; reserve Topology for DAPR/service operational surfaces and update migration terminology/tests accordingly.

### Low

#### 13. Canonical state casing is inconsistent

- **Location:** `EXPERIENCE.md:65-67,105,129`; `DESIGN.md:229`; `src/Hexalith.EventStore.Contracts/Queries/QueryResponseProvenance.cs:11-29`; `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecycleState.cs:11-53`.
- **Note:** The Source Traceability table says invalid provenance renders lowercase `unknown`, while the component and state contracts use canonical `Unknown`. This is small, but exact state identifiers feed localization keys, selectors, fixtures, and analytics.
- **Fix:** Use the exact contract identifiers `ProjectionBacked`, `HandlerComputed`, `Unknown`, `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` everywhere; separately specify localized display strings.

#### 14. Adopted architecture decisions remain labelled as assumptions

- **Location:** `EXPERIENCE.md:298-302`; `_bmad-output/planning-artifacts/architecture.md:317-321`.
- **Note:** The in-place Admin UI integration point and `eventstore-admin-ui` identity are listed under Non-Blocking Assumptions even though AD-21 is adopted and makes them binding. Conversely, “tab names may change” weakens a ten-tab naming contract used by UX-DR3 and Story 7.14 without naming an approval mechanism.
- **Fix:** Promote adopted host/identity/navigation decisions into Foundation and IA. Leave only genuinely unresolved items under assumptions, and require an explicit UX/architecture update before canonical tab names or groupings change.

## Architecture-ready decisions

- `src/Hexalith.EventStore.Admin.UI` is the single brownfield EventStore UI target; it evolves in place and retains resource/container identity `eventstore-admin-ui` (`DESIGN.md:164`; `EXPERIENCE.md:21`).
- EventStore presents one host-level module entry labelled **Event Store Admin**, opening one dashboard rather than feature-by-feature host navigation (`DESIGN.md:164,216`; `EXPERIENCE.md:29,92`).
- FrontComposer and Blazor Fluent UI V5 are mandatory; existing platform/component primitives take precedence over hand-rolled UI (`DESIGN.md:170-180,200`; `EXPERIENCE.md:19,93`).
- Interactive UI hosts remain typed-client consumers and do not host generated or hand-written per-message MVC command/query controllers (`EXPERIENCE.md:62`; `_bmad-output/planning-artifacts/architecture.md:111-115`).
- HTTP `202`, transport success, and SignalR are not completion evidence. SignalR is only a freshness nudge; polling/query evidence must confirm visible state (`EXPERIENCE.md:64,117-118,151-153`).
- Projection confirmation requires projection-backed provenance plus authoritative `Current`; handler-computed, missing, or invalid provenance renders `Unknown`, and `LocalOnly` never confirms success (`EXPERIENCE.md:65-67,105,126-129`).
- Mutation defaults are fail-safe: stale/non-current evidence disables action unless a documented consumer-owned exception exists; sensitive actions are role-gated, confirmed, attributable, and support-safe (`EXPERIENCE.md:105,115,154,205-214`).
- Deferred capabilities are hidden or explicitly disabled/read-only with “Unavailable in this release.”; reachable unavailable server paths return `501`, and no fake operational form is permitted (`EXPERIENCE.md:122,207-214`).
- The spine commits WCAG 2.2 AA behavior, keyboard-operable Fluent components, focus restoration, live-region priorities, complete resource-backed strings, and three responsive viewport bands (`EXPERIENCE.md:157-197`).
- The command lifecycle vocabulary—`Received`, `Processing`, `EventsStored`, `EventsPublished`, `Completed`, `Rejected`, `PublishFailed`, `TimedOut`—and the projection lifecycle vocabulary are stable enough for downstream component and fixture design (`EXPERIENCE.md:104-105`).

## Reviewer scope

Read-only architecture-handoff review of `DESIGN.md`, `EXPERIENCE.md`, their direct local sources, canonical UX handoff/index, current PRD validation state, current architecture and epic ownership, promoted HTML mockups, repository UX policy, and targeted brownfield implementation evidence in `Admin.UI`, `Admin.Abstractions`, contracts, and AppHost. The lens tested source currency/traceability, host/module identity, FrontComposer/Fluent UI V5 inheritance, IA/flow closure, components/states/actions, routing/deep links, responsive behavior, evidence/freshness/telemetry semantics, fail-closed security, visual-reference drift, and terminology. It did not modify the spines or sources, grade visual aesthetics, run the application, or claim implementation readiness beyond what the inspected artifacts prove.
