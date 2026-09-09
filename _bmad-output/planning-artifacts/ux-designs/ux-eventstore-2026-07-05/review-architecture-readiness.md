# Architecture Readiness and Implementability Review — Hexalith.EventStore Admin

## Overall verdict

**Not implementation-ready.** The spine pair is unusually honest about being a target contract rather than delivery evidence, and its evidence, command-status, security, responsive, and localization principles are strong. However, the authoritative PRD is explicitly `blocked` / `reject`, the architecture is `draft`, and two route-contract conflicts plus missing per-operation bindings would force architecture and story-development consumers to invent safety-critical behavior.

The UX documents can continue as planning input, but they must not authorize implementation, release, deployment, migration, or a readiness claim. Resolve the critical upstream gate and route collision first, then reconcile the high-severity contract gaps and rerun readiness against one approved source baseline.

## Scope checked

- `.memlog.md`, `DESIGN.md`, `EXPERIENCE.md`, the UX index/handoff, promoted mockups/imports, and the complete artifact inventory.
- Every local source named in spine frontmatter: `docs/brownfield/architecture.md`, `prd.md`, `architecture.md`, `epics.md`, and the bound PRD validation report.
- Repository baseline instructions and `references/Hexalith.AI.Tools/hexalith-ux-instructions.md`.
- Tracked Admin UI host/routes, ServiceDefaults health mapping, Builds package catalog, FrontComposer 4.4.0 source/contracts, and Fluent UI V5 package APIs. Existing UX review files were not treated as authority.

## Critical findings

### 1. The authoritative planning baseline forbids an implementation handoff

**Citations:** `_bmad-output/planning-artifacts/prd.md:3-9`, `:83-85`, `:509-511`, `:548-563`; `_bmad-output/planning-artifacts/architecture.md:8-10`, `:73-77`, `:446-465`; `EXPERIENCE.md:22`, `:35-47`.

**Impact:** The PRD records `implementation_readiness_status: blocked` and `implementation_readiness_result: reject`; OR14 forbids downstream handoff until PRD, architecture, and epics are reconciled and approved, and the architecture remains `draft`. Starting Stories 7.4/7.5/7.14/7.19/7.20 from these spines would contradict the authority chain even if the UX documents themselves are `final`.

**Fix:** Close the PRD's blocking refinements, approve the reconciled architecture and epics baseline, rerun implementation readiness, and bind the new result/source identities before treating this UX pair as an implementation input. Preserve the current sentence that UX finality is not readiness.

### 2. `/health` is assigned to both an anonymous probe and an authenticated dashboard page

**Citations:** `EXPERIENCE.md:60-69`, `:95-106`, `:295`; `_bmad-output/planning-artifacts/architecture.md:193-197`; `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor:1`; `src/Hexalith.EventStore.Admin.UI/Program.cs:17-18`; `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs:156-186`.

**Impact:** The same `eventstore-admin-ui` host currently maps the Razor page and the explicitly anonymous health-check endpoint to `/health`. The exact health endpoint can shadow the UI route; resolving the ambiguity in the opposite direction could expose an authenticated operational surface anonymously. The canonical ten-tab route contract therefore cannot be implemented safely as written.

**Fix:** Give the Recovery dashboard view a distinct canonical UI path, reserve `/health`, `/alive`, and `/ready` exclusively for support-safe probes, and update `EXPERIENCE.md`, UX-DR4, Story 7.14's route manifest, legacy redirects, navigation/palette links, and endpoint tests atomically.

## High findings

### 1. `/types` is canonical in the spine but absent from Story 7.14's closed route manifest

**Citations:** `EXPERIENCE.md:49-52`, `:58-69`, `:77-102`; `_bmad-output/planning-artifacts/epics.md:5539-5549`, `:5577-5585`; `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor:1`.

**Impact:** The spine says Story 7.14 implements `/types` under Streams & Events, while that story's acceptance criteria call their route list exact and omit `/types`. A developer can satisfy the story while orphaning the live Type Catalog route or creating an unreviewed duplicate/redirect.

**Fix:** Add `/types` and its `events` / `commands` / `aggregates` inner-tab contract to Story 7.14's machine-validated manifest and tests, or remove the spine claim through an approved source change. Keep one implementation and define compatibility redirects explicitly.

### 2. No closed route/action/policy/evidence matrix exists

**Citations:** `EXPERIENCE.md:77-102`, `:123-156`, `:213-242`, `:291-312`; `_bmad-output/planning-artifacts/epics.md:4822-4837`, `:4960-4993`, `:5842-5845`; `docs/brownfield/architecture.md:170-184`.

**Impact:** The route table names safe filters and generic failure behavior, while the surface table uses open phrases such as “an approved support action” and “only explicitly implemented current operations.” It never binds each query and mutation to a Story 7.5 typed facet/outcome, `AdminReadOnly` / `AdminOperator` / `AdminFull` policy, stable operation/audit identity, authoritative terminal/projection/audit evidence, retryability, and current/deferred gate. Story 7.19 explicitly requires that per-boundary inventory, so implementers must invent security and success semantics to satisfy it.

**Fix:** Add a closed, story-owned operation matrix covering every canonical route and action. For each row bind typed client facet/method, authorization policy and scope, request/status/audit identities, accepted/pending/terminal outcomes, evidence required for confirmation, refresh versus retry behavior, idempotency/audit obligations, and current availability/dependency gate. Generate Story 7.5/7.19 tests from the same inventory.

### 3. The Sample accepted-submission flow has no precise implementation/test owner

**Citations:** `EXPERIENCE.md:71`, `:301-312`, `:357-364`; `_bmad-output/planning-artifacts/epics.md:272`, `:323-328`, `:705-743`, `:1627-1675`.

**Impact:** `EXPERIENCE.md` maps UX-DR42 generically to “Epic 2 consumer stories,” but the Sample-specific Story 1.8 only asserts typed-client host boundaries; it does not accept-test `Accepted` → `EvidencePending` → authoritative read-model change, unknown timeout, or no-resubmit behavior. Story 2.6 supplies Tenants presentation evidence, not the Sample flow. A cross-module requirement can therefore remain unimplemented while all cited stories pass.

**Fix:** Assign the full Sample behavior to one named story and add browser/component acceptance criteria for accepted, pending, projection-confirmed, timeout/unknown, and no-automatic-resubmit paths. Update Source Traceability to cite that story separately from the Tenants owner.

### 4. The source snapshot is not reproducible under its own drift rule

**Citations:** `DESIGN.md:4-14`, `:89-95`; `EXPERIENCE.md:3-13`, `:35-47`; `_bmad-output/planning-artifacts/epics.md:7-18`.

**Impact:** Both spines identify reviewed revision `23a722a…`, while current `HEAD` is `0994c378…`. `EXPERIENCE.md` records epics SHA-256 `5a5c03d1…`, but current tracked `epics.md` is `d067c8fb…`; epics now records the current spine digests. The table calls this a pre-repin capture, yet the next paragraph says any later digest change reopens reconciliation. Consumers cannot mechanically distinguish expected repinning from semantic drift, so “final” is not independently reproducible.

**Fix:** Replace the cyclic document-to-document hash scheme with one immutable baseline manifest, or normalized digests that exclude provenance fields. Bind the repository revision and source bytes once and have both spines and epics reference it. Until then, mark source reconciliation open and do not repin hashes alone.

## Medium findings

### 1. The FrontComposer module binding names a component parameter that does not exist

**Citations:** `DESIGN.md:24-38`, `:124-130`; `EXPERIENCE.md:24-33`, `:164-168`; `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Registration/FrontComposerNavEntry.cs:3-52`; `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Layout/FrontComposerNavigation.razor.cs:27-39`, `:106-121`.

**Impact:** DESIGN binds `module-navigation` to `FrontComposerNavigation` with `module-id: event-store-admin`, but `FrontComposerNavigation` has no module-id parameter; it renders entries from `IFrontComposerRegistry`. The actual registration contract is `FrontComposerNavEntry(BoundedContext, Title, Href, Icon, Order, RequiredPolicy, Enabled, DisabledReason, TitleKey, Resource)`. Developers must guess whether `event-store-admin` is a bounded context, entry identity, route segment, or test-only identifier, and may omit policy/localization fields.

**Fix:** Bind the single module entry to the real registry API, including exact `BoundedContext`, `Title`, `Href`, `Order`, `RequiredPolicy`, `TitleKey`, and resource marker, and specify how `event-store-admin` remains a stable external/test identity. If FrontComposer needs a first-class module ID, make that an explicit prerequisite.

### 2. The selected destructive-dialog primitive cannot render the required structured confirmation contract

**Citations:** `DESIGN.md:54-60`, `:156-160`; `EXPERIENCE.md:146-156`, `:174`, `:251-252`; `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Components/Forms/FcDestructiveConfirmationDialog.razor.cs:18-46`.

**Impact:** The spine requires separately perceivable principal, environment, tenant, target, pre-state, effect, blast radius, reversibility, authorization, and expected evidence, but `FcDestructiveConfirmationDialog` exposes only `Title`, plain-string `Body`, `DestructiveLabel`, and callbacks. Using it directly forces sentence assembly or loses structured labels/associations; replacing it ad hoc defeats the claimed FrontComposer binding.

**Fix:** Either define and story-own an additive FrontComposer structured-facts/RenderFragment API, or bind these operations to a named EventStore-owned `FluentDialog` composition and reserve `FcDestructiveConfirmationDialog` for cases its public surface can express. Add API-surface and accessibility tests.

### 3. Recovery actions are not explicitly gated by unwired Operations and audit prerequisites

**Citations:** `EXPERIENCE.md:215-225`, `:316-326`; `_bmad-output/planning-artifacts/architecture.md:298-314`, `:446-463`; `_bmad-output/planning-artifacts/epics.md:4695-4710`, `:4748-4756`, `:4816-4830`.

**Impact:** Flow 1 presents Retry as an available choice, while AD-31 says `eventstore-operations` is not production-wired and Stories 7.1 and 7.3 remain backlog. The general finality disclaimer prevents a current delivery claim, but it does not give story developers the exact UI enablement gate once partial implementation begins.

**Fix:** In the operation matrix, make Retry/Archive unavailable until Story 7.1 durable poison transfer/recovery, Story 7.3 durable audit, AD-28/AD-29 authorization/attribution, AD-31 wiring/release identity, and authoritative status evidence all pass. Define the read-only state shown before that gate.

## Low findings

None.

## Strengths

- Readiness honesty is explicit: UX finality is separated from implementation, release, deployment, migration, and production readiness (`DESIGN.md:91-95`; `EXPERIENCE.md:20-22`, `:47`).
- Runtime identities are clearly separated across assembly, Admin service, UI service/resource/container, module identity, and visible label (`EXPERIENCE.md:24-33`).
- Command identity is corrected to `MessageId` for status lookup and `CorrelationId` for tracing, matching AD-17 rather than the stale brownfield description (`EXPERIENCE.md:139-144`; `_bmad-output/planning-artifacts/architecture.md:199-203`; `docs/brownfield/architecture.md:152`).
- Projection provenance, lifecycle, observation time, refresh time, clock basis, and version are rigorously separated; ETag, SignalR, elapsed time, and `LocalOnly` cannot manufacture success (`EXPERIENCE.md:125-144`).
- Authorization, non-disclosure, unknown outcomes, no-resubmit behavior, localization, responsive behavior, and test seams are concrete enough to drive strong negative tests (`EXPERIENCE.md:188-297`).
- FrontComposer/Fluent V5 inheritance and no-theme-redefinition align with repository UX rules (`DESIGN.md:97-140`, `:174-185`; `references/Hexalith.AI.Tools/hexalith-ux-instructions.md:3-37`). Most named FrontComposer primitives exist in tracked 4.4.0 source/package.

## Mechanical notes

- Digest check: brownfield `3cddf6eb…`, PRD `b99effdb…`, architecture `7e3dbc7b…`, and PRD validation `17f378d0…` match `EXPERIENCE.md`; epics does not (`5a5c03d1…` recorded versus `d067c8fb…` current). Current `DESIGN.md`, `EXPERIENCE.md`, and `ux.md` digests match values recorded by `epics.md`.
- Artifact inventory: 6 promoted mockup files, 2 imported Fluent captures, and 12 `.working/` files. Promoted/imported references resolve, `DESIGN.md:172` says the spines win on conflict, and `.working/` remains non-authoritative scratch evidence.
- API verification used tracked FrontComposer 4.4.0 source and package inspection. `FcPageTabs.ActiveTabId/ActiveTabIdChanged`, `FcPageHeader` metadata/actions, `FcStatusBadge`, grid helpers, loading/empty components, palette, and Fluent V5 tabs/grid APIs are real; the two binding gaps above are the material exceptions.
- The Builds catalog owns FrontComposer 4.4.0 and currently lists Contracts/Shell but not `Hexalith.FrontComposer.Contracts.UI` (`references/Hexalith.Builds/Props/Directory.Packages.props:9`, `:53-57`). Story 7.14 correctly treats adding Contracts.UI to the same catalog family as prerequisite work (`_bmad-output/planning-artifacts/epics.md:5562-5565`).
- No spine or source file was changed. Only this report was overwritten.
