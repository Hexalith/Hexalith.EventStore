# Architecture validation — brownfield ratification and input reconciliation

Date: 2026-09-09. Target: `_bmad-output/planning-artifacts/architecture.md`.

**Verdict: changes required.** Two high findings prevent treating this spine as a reliable description of the current platform boundary and projection contract. Three medium findings concern implementation-state separation, governing-source identity, and memlog continuity. One additional upstream reconciliation defect should be referred to the PRD owner. No critical finding is established by this lens.

This is critique only. The spine, memlog, implementation, tracker, dependencies, and evidence packets were not changed. `[ADOPTED]` is interpreted as an approved decision, not automatically as delivered functionality; findings below require additional misleading current-state language or an actual boundary/contract mismatch.

## High findings

### BR-H1 — AD-19's mandatory normalized result does not exist in the implementation

- **Spine evidence:** `architecture.md:286` defines AD-19; `architecture.md:290` requires the server to emit exactly `ProjectionDispatchResult(Version, Entries)` and `ProjectionDispatchResultEntry` with `ProjectionCheckpointAdvanceState`. The subsequent rules reject alternative normalized shapes. `architecture.md:477` restates that result as the platform persistence convention.
- **Repository evidence:** `src/Hexalith.EventStore.Server/Projections/INamedProjectionDispatchCoordinator.cs:64` returns `Task<bool>` for normal delivery; its documented meaning at line 63 is whether named delivery owned the request, not per-route durable completion. Rebuild methods instead return `NamedProjectionRebuildResult` (lines 11, 26, 34). `NamedProjectionDispatchCoordinator.cs:522` consumes a dictionary of `ProjectionDispatchOutcome`; lines 544 onward perform route finalization, but do not expose the architecture's result. `rg -n 'ProjectionDispatchResult|ProjectionDispatchResultEntry|ProjectionCheckpointAdvanceState' src tests -g '*.cs'` returns no matches.
- **What is real:** `src/Hexalith.EventStore.Contracts/Projections/ProjectionDispatchStatus.cs:6` implements the five exact numeric outcome codes; named v2 dispatch, bounded outcomes, route persistence and retry work exist. This is not a finding that the whole projection design is fictitious, nor proof that persistence is incorrect.
- **Consequence:** a builder obeying the spine will design a consumer against absent types and assume it can observe `Advanced`/`NotAdvanced` per route. Another builder following the code will get an ownership Boolean. Both cannot satisfy the mandatory shared contract.
- **Disposition: discuss, then Update.** Decide whether AD-19 prescribes an outstanding implementation change or should ratify the shipped seam. Preserve the durable checkpoint guarantees either way. If the normalized shape remains required, identify its implementation owner and handoff gate and mark it unavailable until proven; do not silently invent public types during this validation.

### BR-H2 — The real Operations service and its mutation authority are absent from the spine

- **Spine evidence:** the paradigm and first diagram (`architecture.md:56`, `architecture.md:74`), structural seed (`architecture.md:517`), runtime topology (`architecture.md:539`), and capability map (`architecture.md:584`) omit `Hexalith.EventStore.Operations`. The topology routes Admin state mutations to EventStore (`architecture.md:577`) without a dead-letter Operations branch.
- **Repository evidence:** `Hexalith.EventStore.slnx:35` includes the Operations project and line 61 its test project. `src/Hexalith.EventStore.Operations/Hexalith.EventStore.Operations.csproj:6` enables its container, with identity `eventstore-operations` at line 7. `Operations/Program.cs:14` validates the inbound DAPR app-channel credential, line 30 registers `DeadLetterDrainActor`, and lines 41–44 map health, dead-letter, subscription and actor routes. `Operations/Actors/DeadLetterDrainActor.cs:16` declares the durable serialization point for a subscriber dead-letter topic; lines 63–65 access retained actor state. `Operations/Endpoints/DeadLetterOperationsEndpointExtensions.cs:39` maps capture, and lines 45–49 map list/count/retry/skip/archive operations.
- **Cross-boundary evidence:** `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs:26` sets `OperationsAppId = "eventstore-operations"`. `Admin.Server/Services/DaprDeadLetterCommandService.cs:93` invokes that app directly and forwards an authentication bearer token at line 101. This is a concrete Admin-to-Operations mutation edge, not a hypothetical future service.
- **Consequence:** the architecture leaves builders to decide independently whether subscriber dead letters belong to the gateway, Admin.Server, or Operations; which service owns retained payload/replay state; and which credential and tenant boundary protects that mutation route. The omitted service also has no architectural deployment/readiness disposition. Read-only search found no `Operations`/`operations` reference in AppHost Program or Aspire C# wiring, so its presence as a solution project must not be mistaken for proof it is orchestrated.
- **Disposition: discuss, then Update.** Ratify Operations as an explicit platform boundary, including Admin invocation, capture versus replay ownership, durable state, authentication/tenant enforcement and deployment status; or name the approved migration that removes that boundary. Keep the existing release-inventory rule: enabling a container project alone is not evidence that its registry artifact is released.

## Medium findings

### BR-M1 — Current structural views mix delivered components with approved future integrations

- **Spine evidence:** the dependency diagram states `Admin.UI -> FrontComposer.Shell` / `Contracts.UI` (`architecture.md:214`); the UI convention says it composes those dependencies (`architecture.md:482`); the Stack table describes matching source/package consumption (`architecture.md:506`). The Structural Seed topology includes a DAPR `openbao` component and OpenBao backend (`architecture.md:550`), and AD-24 says AppHost provisions the container (`architecture.md:394`).
- **Repository and planning evidence:** `src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj:18`–32 lists current project/package references without either FrontComposer package. `epics.md:5553` explicitly records that absence and Story 7.14's backlog state. `sprint-status.yaml:220` leaves Story 7.6 OpenBao integration in backlog, and line 229 leaves Story 7.14 in backlog. No `openbao` match exists in `deploy/dapr/*.yaml`; no OpenBao wiring appears in AppHost Program. This is consistent with those stories being planned.
- **Consequence:** a future deployment or UI slice can treat a seed diagram as evidence that the infrastructure or shell integration already exists, whereas its owning story still has to introduce it. Approved destination and shipped structure need separate labels.
- **Disposition: autofix in an Update.** Retain AD-21 and AD-24 as adopted decisions, but label the diagrams/rows as target topology and mark Story 7.14/7.6 integrations pending. Record actual current dependencies only in current seed views. AD-23's explicit unavailability and implementation gates provide an existing pattern to follow.

### BR-M2 — The governing OQ8 design is only digest-addressed despite an available commit identity

- **Spine evidence:** `architecture.md:58`–63 names OQ8 version 1.0.0 and its SHA-256 digest but gives no repository, path, or commit for retrieving that authority.
- **Reconciled input evidence:** `prd.md:94` records `github.com/Hexalith/Hexalith.Folders`, `docs/exit-criteria/oq8-idempotency-design.md`, commit `a9cfea91c8a987ef7a836c216e633a92321fc3c2`, and the same digest. It explicitly supersedes the earlier working-tree citation. `prd.md:532` assigns propagation to the architecture owner as OR11, triggered by the next architecture or epics revision.
- **Consequence:** a builder starting from the spine knows how to compare supplied bytes but cannot locate the governing design, and may use an older working-tree document. This matters because AD-25 deliberately inherits a security and durability authority more specific than the local summary.
- **Disposition: autofix in an Update.** Carry the PRD's exact repository/path/commit/digest identity and its limitation that Folders must supply and verify the bytes. Do not claim this review independently retrieved or verified that external design. The local reference update needs no new architectural choice.

### BR-M3 — Memlog replay does not account for the current rendered version baseline

- **Spine evidence:** frontmatter `updated: 2026-08-29` (`architecture.md:10`) and source entries through the 2026-08-29 proposal (`architecture.md:36`); AD-11 and Stack currently require SDK `10.0.400` (`architecture.md:492`) and the refreshed Aspire/DAPR/test-tool versions.
- **Working-memory evidence:** `.memlog.md:97` still records the 2026-08-16 SDK seed `10.0.302` and then-current versions. The last decision entries and finalize event are the 2026-08-16 review closure; line 105 appends a 2026-09-08 validation event. There are no intervening version/constraint/decision entries binding the later 2026-08-20/29 changes. Earlier entries explicitly document having had to recover rendered-spine changes missing from the memlog, making this a recurring drift mode.
- **What is real:** `global.json:3` agrees with the rendered SDK `10.0.400`, and the inspected central catalog agrees with the major displayed Aspire, DAPR SDK, Fluent UI, xUnit, Shouldly and NSubstitute pins. This finding is not a demand to downgrade the code or a judgment of latest upstream availability.
- **Consequence:** the skill's next Update, which resumes decisions from the memlog as authority, risks reconstructing an older baseline or requiring another undocumented recovery from the rendered document.
- **Disposition: autofix in an Update.** Append the accepted proposal constraints and verified version facts before re-distilling; preserve existing entries and stable AD IDs. The validation report itself should not silently repair the authority record.

## Upstream reconciliation issue

### BR-U1 — The approved 2026-09-08 ownership fix remains contradictory within the PRD

- **Evidence:** `sprint-change-proposal-2026-09-08.md:61` onward resolves the duplicate primary claims, and `prd.md:541` retires OR12 with that outcome. Yet `prd.md:400` still says SM2 awaits OR12; `prd.md:439`–453 still labels eight FRs as having multiple primary owners; `prd.md:467` still asserts the explicitly refuted FR1/Epic-4 mismatch. The same PRD directs readers to epics as the story ownership authority.
- **Consequence:** a future input-reconciliation pass can re-open resolved ownership questions or import obsolete story mappings unless it follows the approved proposal plus the current epics and PRD closure record.
- **Severity/disposition:** medium upstream defect; refer to PRD owner. Do not count this as evidence that the spine's broad capability map is wrong or change story ownership in the architecture. The requested architecture validation can report it without reauthoring the PRD.

## What passed and relevant limits

- The canonical paradigm ratifies the gateway, domain-service SDK, DAPR aggregate actor, platform projection/read-model seams and external typed-client boundaries present in the tree.
- AD-25 is not classified as an entirely speculative design: `AggregateActor.cs:145` exposes fenced execution, and its processing path checks the execution fence (`AggregateActor.cs:371` and later side-effect boundaries). Stories 4.9–4.13 are tracked done. This source inspection is not a new production-evidence closure or authorization of Story 4.15.
- AD-5 and Deferred correctly distinguish admission fencing from provider-level append write-once enforcement. PRD NFR7 (`prd.md:311`) and the approved 2026-09-08 reconciliation explicitly preserve the undelivered append-race class while recognizing Story 5.1's independent guarded class. No finding here asks to remove the documented provider caveat or implement an unapproved fence.
- The current 14-package release inventory agrees with `tools/release-packages.json`; neither future payload-protection packages nor assistant instruction files are incorrectly counted. SignalR, Testing and Testing.Integration are real package projects omitted from the terse seed, but their mere omission is not a high finding: package inventory is code-owned and the seed is intentionally minimal. Operations is different because its omission hides a live mutation/security boundary.
- AD-11/AD-22 preserve the distinction between rejected release evidence, corrective publication, owner-reviewed parity and consumer deletion authority. Current tracking (`sprint-status.yaml:151`, `:155`) keeps Story 3.15 in progress and Story 3.16 backlog. Architecture validation must not claim positive deployed parity from those planning documents.
- The ratified 2026-09-08 NFR3 rule matches the concrete Production rejection in `JwtBearerAuthenticationContract.cs:118` even when the symmetric-key exception is enabled. Its proposal expressly says no architecture change is required; therefore absence of those option-level details from AD-10 alone is not treated as a source conflict. Story 5.3 remains in progress and this review claims no NFR3/NFR4 delivery closure.
- No broad build or test suite was run: this lens inspects architectural consistency and existing test/source surfaces, and changes only its review artifact. The parent reported deterministic spine lint passing with zero findings; that does not validate implementation conformance. Test project and source existence are evidence of structure, not evidence that a test passed during this review.

## Suggested Update order

1. Resolve BR-H1's actual-versus-required projection contract and BR-H2's Operations ownership.
2. Apply BR-M1 target/current labels, BR-M2 source identity, and BR-M3 append-only memory recovery.
3. Route BR-U1 to the PRD owner and run the independent gate again on the updated spine. No implementation-readiness, release, deployment, migration, or consumer-removal authority follows from this report.
