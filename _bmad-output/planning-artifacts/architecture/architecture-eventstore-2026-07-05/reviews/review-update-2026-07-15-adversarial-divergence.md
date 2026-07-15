# Reviewer Gate - 2026-07-15 Adversarial Divergence And Proposal Reconciliation

## Verdict

**FAIL — changes required before the spine is a convergent build substrate.** One critical wire-contract/proposal mismatch and three high-severity ownership or compatibility gaps still allow independently built downstream units to obey every stated AD while producing incompatible behavior. The updated spine lands the intended UI project/resource and FrontComposer direction, but AD-19 is not yet the deterministic contract the approved proposal requires.

## Scope And Attack Method

Reviewed `_bmad-output/planning-artifacts/architecture.md` as a feature-altitude spine. The attack constructed pairs of independently built stories/components one level down and asked whether both could satisfy every AD while disagreeing at their integration seam. The review concentrated on:

- `/project/v2` response and checkpoint reconciliation (AD-19);
- projection/read-model/checkpoint and Admin mutation ownership;
- consolidated EventStore UI ownership, FrontComposer composition, module identity, and legacy routes (AD-21);
- architecture-directed requirements from the approved `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md`.

Repository reality was used only to test whether the spine ratifies a single existing interpretation. It was not treated as a substitute for a missing invariant.

## Critical Finding

### C1 — AD-19 calls the v2 dispatch response exact and frozen without defining its exact serialized members, and the approved checkpoint-advance member did not land

**Evidence:** AD-19 names `ProjectionDispatchResponse`, `Version = 2`, a list of `ProjectionDispatchOutcome`, `ProjectionType`, and stable `ProjectionDispatchStatus` values, then says the serialized members are frozen (`architecture.md:235-246`). It does not enumerate all request/response/outcome members, their types and optionality, or an outcome member carrying checkpoint-advance state. Instead it says the server *records* an `Advance`/`DoNotAdvance` decision (`architecture.md:242`). The approved proposal explicitly requires the named bounded dispatch **result** to contain “explicit checkpoint-advance state” (`sprint-change-proposal-2026-07-15.md:234-238`).

Current brownfield code demonstrates one permissible interpretation: `ProjectionDispatchResponse` has only `Version` and `Outcomes`, while each `ProjectionDispatchOutcome` has `ProjectionType`, `Status`, optional legacy `State`, and `ReasonCode`; there is no checkpoint-advance member (`src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchResponse.cs:8`, `ProjectionDispatchOutcome.cs:13`). The code also has request members `Request`, `ProjectionTypes`, `DispatchId`, and `CatalogFingerprint` that the alleged frozen v2 architecture contract does not name (`ProjectionDispatchRequest.cs:10`).

**Two compliant but incompatible units:**

- Domain-service unit A emits the current shape `{ version, outcomes: [{ projectionType, status, state, reasonCode }] }`; EventStore server A derives and persists `Advance` from `status` after its local compatibility write. This obeys every sentence of AD-19 because the server records the decision.
- Contract/client unit B reads the approved proposal and emits/expects `{ version, outcomes: [{ projectionType, status, checkpointAdvance }] }`, with checkpoint state explicit on the result. It also obeys the named type, version, ordering, closed status values, and “explicit checkpoint reconciliation” language.

The units cannot exchange responses reliably. A tolerant serializer can hide the break while dropping the very evidence intended to prevent checkpoint loss. The same omission lets one implementation reject more than 32 discovered routes while another silently admits/truncates the first 32; both satisfy “at most ... 32” and “one outcome for each admitted route,” because admission and overflow behavior are not frozen.

**Impact:** contract drift and, more seriously, false checkpoint advancement or stuck retries at the persistence boundary. “No equivalent shape” is unenforceable when the canonical shape is not stated.

**Disposition: discuss, then mandatory fix.** Amend AD-19 to enumerate the exact v2 request, response, and outcome wire members, JSON names, types, required/optional rules, bounds, unknown-member/version behavior, route-admission/overflow behavior, and closed numeric values. In accordance with the approved proposal, add explicit per-outcome checkpoint-advance state and bind its invariant to status (`Advance` only with `Completed`/`AlreadyCompleted` after required durable work; otherwise `DoNotAdvance`). If the architectural choice is instead to keep checkpoint decisions server-internal and derived, the approved proposal must be amended explicitly; the current documents cannot both remain authoritative.

## High Findings

### H1 — Projection shared-data ownership and the commit/fencing boundary remain split between the domain service and EventStore server

**Evidence:** AD-7 says read models use platform-owned lifecycle/write contracts and erasure includes read-model plus delivery/rebuild/checkpoint keys as one logical operation (`architecture.md:88-92`). AD-8 spans dispatcher, handler, persistence, marker, and checkpoint (`architecture.md:94-98`). AD-19 says dispatch and persistence are asynchronous, outcomes represent independently durable work, and “the server” records a reconciliation decision (`architecture.md:239-243`). None names the sole writer for the read model, delivery marker, per-route checkpoint, reconciliation row, rebuild staging state, or promotion marker, nor the required ordering/fence between those writes.

**Two compliant but incompatible units:**

- Projection-handler unit A commits the read model and a handler-owned checkpoint through platform `IReadModelStore`/batch seams, then returns `Completed`; it treats its transaction as the durable completion boundary.
- EventStore-server unit B treats the handler as a read-model writer only, then owns and advances the per-route delivery checkpoint/reconciliation record after the response and any legacy actor write.

Each uses platform seams, preserves durable siblings, and only reports success after its own required persistence. Combined, they create two checkpoint authorities with different failure windows. Conversely, pairing a handler that writes no checkpoint with a server expecting handler-owned checkpoint state can return `Completed` but never establish resumable evidence.

**Impact:** duplicate processing, skipped work, irreconcilable checkpoints, erasure races, and a false `AlreadyCompleted` after a partial failure.

**Disposition: mandatory architecture fix.** Add a shared-data ownership/mutation matrix naming the authoritative writer and permitted readers for: event log/snapshots/recovery state, read-model detail/index data, delivery reservations/markers, per-route checkpoints, reconciliation work, rebuild staging/promotion, lifecycle/freshness, and command status/archive. For projection delivery, bind the exact durable completion fence: what must be committed before `Completed`, who converts the explicit result into `Advance`, how that decision is persisted relative to the checkpoint, and how retry/lease identity prevents a stale writer from advancing.

### H2 — Admin mutation authority is contradictory across the paradigm, ADs, and topology, so two state-mutating Admin implementations remain compliant

**Evidence:** the paradigm diagram routes Admin “delegated writes and safe reads” through the gateway (`architecture.md:38-40`), and AD-3 says Admin command/query entry points delegate to the gateway and do not call state stores directly (`architecture.md:64-68`). Yet AD-10 explicitly permits attributable Admin state mutations without naming their coordinator (`architecture.md:106-110`), while the structural topology gives `eventstore-admin` a direct edge to `statestore` (`architecture.md:348-353`). AD-5 gives `AggregateActor` sole durable event/command-state mutation ownership, but does not settle projection erase/rebuild, lifecycle, status/archive, or Admin operational-state ownership.

**Two compliant but incompatible units:**

- Admin-server unit A uses its direct state-store topology edge to erase projection/checkpoint keys and mutate operational state, attaching an audit record to satisfy AD-10.
- Admin-server unit B treats AD-3 and the paradigm diagram as authoritative, calling a gateway/server coordinator that owns lifecycle fencing, erasure, rebuild, status, and audit writes.

Both are attributable, support-safe, and avoid editing persisted events. They disagree on authorization enforcement, lifecycle fencing, state-key semantics, and who can race projection delivery.

**Impact:** split-brain writes to shared projection/control-plane data, bypassed lifecycle fencing, incomplete erasure, inconsistent audit evidence, and accidental security-policy bypass.

**Disposition: mandatory architecture fix.** Decide and state the mutation path per Admin operation. Prefer a named platform coordinator/gateway seam as the sole writer for EventStore-owned projection, checkpoint, recovery, and command-status data; restrict any direct Admin state-store access to explicitly enumerated Admin-owned operational read models with their own key namespace. Update the topology diagram to distinguish read-only state evidence from mutation or remove the direct edge. Require authorization, audit, and persisted end-state evidence at the chosen owner.

### H3 — AD-21 deliberately allows incompatible legacy-route behaviors and does not bind a stable FrontComposer module identity

**Evidence:** AD-21 says legacy routes may be either dashboard deep links **or** compatibility redirects while keeping a single “Event Store Admin” module entry selected (`architecture.md:254-258`). It fixes only a display label, not a stable module ID, route prefix, registration owner, canonical dashboard route, alias table, redirect status/history behavior, or preservation of tenant/query/fragment state. The existing Admin UI has a root router and many absolute routes such as `/`, `/streams`, `/streams/{TenantId}/{Domain}/{AggregateId}`, `/projections`, `/commands`, `/health`, and `/dapr/*`; these are real compatibility seams, not one abstract route (`src/Hexalith.EventStore.Admin.UI/Components/Routes.razor:3` and `Pages/*.razor`).

**Two compliant but incompatible units:**

- Migration unit A retains every old route as a directly rendered FrontComposer deep link and registers module key `eventstore-admin`.
- Shell/navigation unit B moves pages under `/event-store-admin/*`, registers module key `event-store-admin`, and redirects old routes to the dashboard root, optionally dropping route parameters/query/fragment.

Both retain one UI host/resource, compose the required packages, keep one display entry selected, and implement an allowed legacy-route choice. They cannot agree on active-module selection, bookmarks, browser history, deep-linked entity identity, or automated route contracts.

**Impact:** broken operator bookmarks and links, duplicate or unselected navigation entries, lost tenant/aggregate context, and route behavior that varies by independently implemented slice.

**Disposition: mandatory architecture fix.** Bind a stable FrontComposer module identifier and registration owner, canonical route prefix/root, and an explicit legacy route map. For each legacy route family, choose direct alias or redirect—not an open alternative—and define parameter/query/fragment preservation, redirect/history semantics, unknown-route behavior, and active-module selection. Story 7.14 can own the map, but downstream UI/navigation/test stories must consume one contract rather than choose locally.

## Medium Findings

### M1 — “Typed-client consumer” does not establish the shared Admin client contract or the Gateway/Admin.Server responsibility split

**Evidence:** the paradigm diagram labels an unnamed “typed admin client” between Admin UI and Admin Server (`architecture.md:38`); AD-21 says UI command/query flows remain typed-client consumers (`architecture.md:258`). The dependency diagram names FrontComposer packages but no Admin client package/interface (`architecture.md:152-176`). The proposal separately creates Story 7.5 “Shared Typed Admin Client” and Story 7.14 for UI consolidation (`sprint-change-proposal-2026-07-15.md:215-222`), so these are intentionally independently reviewable units.

**Two compliant but incompatible units:** Story 7.5 can put a shared client in `Admin.Abstractions` targeting only `Admin.Server`, while Story 7.14 can consume `IEventStoreGatewayClient` directly for EventStore command/query flows and create a small local Admin client for operational calls. Alternatively, Story 7.5 can define one façade proxying both authorities and Story 7.14 can expect that façade. Both remain typed-client consumers and host no MVC controllers, but their DI registrations, auth forwarding, provenance metadata, and error contracts do not compose.

**Disposition: autofix in the spine.** Name the shared typed Admin client package/interface as the only Admin UI transport seam and enumerate which operations terminate at Admin.Server versus the gateway. Bind tenant/auth/provenance propagation and prohibit a Story 7.14-local duplicate client.

### M2 — The mutation convention governs business-event repair but not the wider control-plane state machine

**Evidence:** the Mutation convention prohibits editing/deleting persisted events and requires compensating commands (`architecture.md:267`), but active architecture also includes projection erasure, rebuild staging/promotion, delivery reconciliation, lifecycle changes, status/archive, and Admin mutations. These are mutable control-plane/read-model data with no general rule for optimistic concurrency, idempotency key, audit owner, or lifecycle admission.

Two stories can therefore both obey the convention while one treats erase/rebuild/status as ordinary DAPR state writes and another requires coordinator-owned compare-and-set operations. H1 and H2 identify the immediate unsafe seams; this broader omission will recur in future operations.

**Disposition: fold into the H1 ownership matrix.** Extend the Mutation convention to distinguish immutable domain event state, derived read models, and mutable operational/control-plane state, with one mutation authority and concurrency/idempotency policy for each class.

## Approved-Proposal Input Reconciliation

### Architecture-directed requirement that did not land

- **Missing/contradictory:** proposal section 4.7 requires every ordered dispatch outcome to contain explicit checkpoint-advance state. AD-19 moved the decision to a server-side record and the current response type contains no such member. This is C1 and must be reconciled before the proposal and architecture can both be called implemented.

### Architecture-directed requirements that did land

- `src/Hexalith.EventStore.Admin.UI` is named as the in-place consolidated UI owner.
- The AppHost/container identity `eventstore-admin-ui` is retained and additional EventStore UI hosts are prohibited.
- `Hexalith.FrontComposer.Shell`, `Hexalith.FrontComposer.Contracts.UI`, and Fluent UI V5 are in AD-21, the dependency diagram, conventions, and stack.
- Legacy-route compatibility and the single **Event Store Admin** module entry are mentioned, though H3 shows the landed rule is not deterministic enough to prevent divergence.
- Story 7.14 is bound to AD-21.
- Quantitative UI performance budgets are explicitly deferred without weakening accessibility, responsive layout, evidence-state, or support-safety.
- AD-19 names `/project/v2`, `ProjectionDispatchResponse`, `ProjectionDispatchOutcome`, version 2, ordered per-route outcomes, and stable status values, though C1 shows the result shape is still incomplete.

No other architecture-directed requirement in proposal sections 4.7 or 4.10 was absent. The proposal's PRD/UX “no change” direction is preserved.

## Tail

No low-severity findings. The strongest existing invariants remain effective: aggregate event mutation stays actor-owned (AD-5), gateway policy remains mandatory for external command/query entry points (AD-3), domain modules stay infrastructure-free (AD-2), and UI host/resource/package ownership is substantially clearer after AD-21. Those strengths do not close the projection and Admin shared-state seams above.

## Re-review 2026-07-15

### Verdict

**FAIL — no critical findings remain, but two high-severity divergence points remain.** Revised AD-19 now provides an explicit normalized checkpoint result and makes the server responsible for checkpoint save/normalization after durable work. Revised AD-21 fixes the UI owner, module ID, canonical-route-table owner, redirect direction, and matching FrontComposer version boundary. The AdminServer topology now clearly separates support-safe direct reads from mutations routed through EventStore. Those changes resolve prior C1, H1, H2, and H3 at critical/high severity; the earlier typed-client and broad control-plane-convention observations remain medium only.

### HIGH — AD-19 does not define the normalized status for failure paths, so its exact result still permits incompatible retry behavior

AD-19 requires every normalized entry to contain both `ProjectionDispatchStatus` and `ProjectionCheckpointAdvanceState`, and fixes the latter as `NotAdvanced` for missing, malformed, duplicate, unrequested, transport-failed, or otherwise unsuccessful work (`architecture.md:240-246`). It does not define the `ProjectionDispatchStatus` emitted for those cases, nor the status for a handler-reported `Completed`/`AlreadyCompleted` outcome when required persistence, the legacy write, or the checkpoint save subsequently fails.

Two server units can therefore obey every revised rule while emitting incompatible results:

- server A preserves handler `Completed` but emits `NotAdvanced` when checkpoint save fails; it maps missing/malformed outcomes to `Failed` and transport failures to `Retryable`;
- server B maps any post-handler persistence/checkpoint failure and any missing/transport outcome to `Indeterminate`, always with `NotAdvanced`.

Both use the exact named result, stable enum values, and safe checkpoint state, but clients that use status for retry/terminal classification behave differently; `Completed + NotAdvanced` is especially ambiguous. Fix by freezing a normalization matrix for every source/failure case, declaring `ProjectionCheckpointAdvanceState` authoritative for checkpoint movement, and prohibiting impossible or ambiguous status/advance pairs. The matrix should state whether post-handler persistence/checkpoint failure is `Retryable` or `Indeterminate`, and how missing, malformed, duplicate, unrequested, transport, and cancellation cases are represented or omitted.

### HIGH — AD-22 conflates the EventStore evidence SHA with the consuming repository checkout SHA and leaves package/deployment identity uncheckable

AD-22 says the parity packet records the exact **EventStore runtime commit SHA**, then says “the consumer checkout must match that approved SHA” (`architecture.md:262-266`). A Tenants or other consumer repository checkout cannot equal an EventStore repository commit. In package mode it may have no EventStore source checkout at all; in deployment mode the running EventStore artifact may be identified by a package version, OCI digest, or attestation rather than a checkout.

Two adoption units can obey plausible readings but disagree:

- unit A verifies that its EventStore source/project-reference checkout equals the packet's EventStore SHA, then removes local infrastructure;
- unit B verifies the consumer repository commit approved by the consumer maintainer, while trusting a package built from the recorded EventStore SHA, or can never satisfy the literal equality in package mode.

This makes the removal gate either bypassable or permanently blocking and does not fully preserve the proposal's consumer-maintainer/exact-consumer-SHA authority. Fix by separating identities: (1) EventStore runtime evidence commit SHA plus immutable package/artifact identity and provenance mapping; (2) consumer maintainer-approved consumer commit SHA containing the removal; and (3) validation that the tested/deployed EventStore dependency artifact resolves to the approved EventStore evidence identity in both source and package modes. Name which checkout each SHA belongs to.

### Re-tested With No Remaining Critical/High Finding

- **AD-21:** one in-place UI owner, stable `event-store-admin` module identity, one canonical route-table owner, preservation of existing canonical deep links, redirects for non-canonical legacy routes, and matching `3.2.2` Shell/Contracts.UI boundaries prevent the earlier independently chosen host/module/route/version alternatives. Exact redirect parameter/history details may remain story/route-table seed without reopening cross-unit ownership.
- **Admin topology:** `AdminServer -> StateStore` is now labeled support-safe operational reads only, while state-mutating actions route to EventStore. Together with AD-3, AD-5, AD-7, and AD-10, this closes the earlier direct-mutation versus gateway-coordinator ambiguity at critical/high severity.
- **AD-19 ownership:** the server now owns normalization and records `Advanced` only after durable persistence, legacy compatibility write, and route checkpoint save. This resolves the prior dual checkpoint-authority attack at critical/high severity; only the normalization-matrix gap above remains.

## Final Closure Re-review 2026-07-15

**PASS — no critical or high findings remain.**

- **Prior AD-19 HIGH resolved:** the binding matrix at `architecture.md:248-257` now fixes both the normalized `ProjectionDispatchStatus` and `ProjectionCheckpointAdvanceState` for successful durable completion, post-handler persistence/actor/checkpoint failure, retryable conflict/incomplete work, transport/exception/missing/duplicate/unrequested/malformed/over-limit outcomes, deterministic validation failure, and cancellation. The earlier `Completed + NotAdvanced` and failure-classification alternatives are no longer compliant.
- **Prior AD-22 HIGH resolved:** `architecture.md:273-283` now separates the approved EventStore source/runtime SHA from the consumer repository SHA and binds source mode to the EventStore submodule SHA, package mode to exact manifest-governed package versions and hashes, and deployed mode to an image digest with release provenance back to the approved EventStore SHA. The consumer checkout is explicitly never compared with the EventStore SHA, closing both the bypass and impossible-gate interpretations.

The focused re-attacks found no remaining critical/high divergence in revised AD-19, AD-21, AD-22, or the AdminServer topology labels. Earlier medium observations remain advisory and do not prevent handoff of the architecture spine.
