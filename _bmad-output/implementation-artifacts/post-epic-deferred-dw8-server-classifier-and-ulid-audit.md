# Post-Epic Deferred DW8: Server Classifier and ULID Audit

Status: ready-for-dev

<!-- Source: sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md - Proposal C / DW8 -->
<!-- Source: deferred-work.md - drain classifier and identifier-validation routed entries -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an EventStore maintainer,
I want drain failures to use stable operational reason codes and identifier validation to reject GUID-only assumptions,
so that production diagnostics stay actionable and platform ID semantics remain ULID-aligned.

## Story Context

DW8 is the second story from the approved Deferred-Work OPEN cleanup package. It owns two server-correctness follow-ups that were routed out of earlier stories:

- `DrainReasonUnknown` currently remains the residual bucket for every drain exception except `DrainEventCountMismatchException` and `MissingEventException`. DAPR/state-store/transient categories can collapse into `unknown`, which weakens operational triage.
- DW2 live evidence found GUID-shaped seeded-stream `correlationId` evidence while project rules require parsers and validators for `messageId`, `correlationId`, `aggregateId`, and `causationId` to use ULID parsing or explicit non-whitespace `AggregateIdentity` semantics. `Guid.TryParse` on those protected fields is forbidden.

This is a narrow server hardening and audit story. It must not reopen DW1 drain idempotence, DW2 live DAPR/MCP evidence capture, DW3 JSON/debugging work, DW6 deferred-work governance, DW7 Admin UI lifecycle, or DW9 evidence-validator/governance CI polish.

Current HEAD at story creation: `025872ab`.

## Acceptance Criteria

1. **Drain classifier taxonomy is explicit and bounded.** Given a drain attempt fails in `AggregateActor`, when the failure is classified for activity tags or structured logs, then the classifier returns one of the documented stable wire values and reserves `unknown` only for residual uncategorized failures.

2. **New drain infrastructure categories are represented.** Given drain failure paths include state-store persistence failures, DAPR actor/runtime unavailability, and publish failures, when those failures are observed, then the activity tag `eventstore.failure_reason` uses stable codes such as `drain_state_store_failure`, `drain_dapr_unavailable`, and `drain_publish_failed` instead of raw exception text. If a proposed category cannot be observed safely in focused tests, document the reason and keep `unknown` as the explicit fallback. Publish-stage exceptions and unsuccessful publish results should both resolve to `drain_publish_failed` unless an already-established DW1 reason code is more specific.

3. **Existing DW1 reason codes remain stable.** Given DW1 already established `drain_event_count_mismatch`, `drain_missing_event`, `drain_publish_failed`, and `unknown`, when DW8 adds categories, then those existing values keep their exact wire spelling and existing tests continue to pass.

4. **Drain reason-code architecture note exists.** Given drain failure reasons are an observability contract, when DW8 closes, then repository docs include a concise reason-code taxonomy under `docs/architecture/` or another existing architecture/operations location. The note must list each code, when it is emitted, whether it is retryable/operator-actionable, and the compatibility rule for future additions. The note should be a compact table, not a drain lifecycle redesign.

5. **Identifier parser audit covers protected fields.** Given controllers, validators, command/query handlers, and evidence seeders may touch `messageId`, `correlationId`, `aggregateId`, or `causationId`, when DW8 completes, then the Dev Agent Record lists the audited files and confirms no `Guid.TryParse` validation is used for those fields. Valid implementations use `Ulid.TryParse`, `UniqueIdHelper`, or documented non-whitespace `AggregateIdentity` acceptance.

6. **Build-time guard prevents GUID parser regressions.** Add a focused parser audit test, Roslyn-style source scan, or existing-test helper that fails if `Guid.TryParse` or `Guid.Parse` is introduced as validation for protected identifier fields. The guard must avoid false positives for unrelated middleware correlation IDs, temporary file names, `Guid.NewGuid()` implementation details not used as validators, and conversion helpers such as `UniqueIdHelper.ToGuid`. Its failure message should name the protected fields and allowed exclusions so future maintainers can fix the right code path.

7. **Seeded-stream identifier evidence is reconciled.** Given DW2-DF5 reported a GUID-formatted seeded-stream `correlationId`, when DW8 closes, then either the seeding flow produces ULID-shaped values or the story documents the precise acceptance rationale and the server-side parser semantics that make the value legal. GUID-shaped examples are acceptable only when they satisfy the documented protected-identifier semantics and do not imply or reintroduce GUID-only validation. Do not silently leave the evidence contradiction unresolved.

8. **Deferred-work dispositions are auditable.** When development starts or completes, update only the DW8-owned entries in `_bmad-output/implementation-artifacts/deferred-work.md`: the drain-classifier entry near the DW1 section and DW2-DF5 identifier-validation entry. Do not sweep unrelated OPEN, STORY, ACCEPTED-DEBT, or legacy unclassified bullets.

9. **Validation is targeted and recorded.** Before moving to `review`, run focused server tests for drain reason codes and the identifier audit guard. If feasible, run `dotnet test tests/Hexalith.EventStore.Server.Tests --configuration Release` or the narrow affected test classes. If Server.Tests is blocked by pre-existing CA2007/build behavior, record the exact blocker and keep the focused guard evidence available.

10. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Verification Status, and Change Log. Move this story and its sprint-status row to `review` only after classifier docs, identifier audit evidence, narrow deferred-work dispositions, and validation evidence are recorded. Move both to `done` only after code-review signoff.

## Scope Boundaries

- Do not redesign the drain retry/reminder/idempotence flow from DW1.
- Do not invent drain reason-code taxonomy during implementation; new public wire values must be named in `DrainReasonCodes`, tested, and documented in the architecture note.
- Do not introduce DAPR-specific subcategories beyond state-store persistence, DAPR actor/runtime unavailable, publish failed, existing DW1 mismatch/missing reasons, and `unknown` unless the extra category is recorded as a deferred decision.
- Do not change public command/query payload contracts unless the identifier audit proves a direct validator defect.
- Do not convert every test `Guid.NewGuid()` identifier to ULID; this story owns parser/validator safety and the routed seeded-stream evidence, not a broad test-data style sweep.
- Do not modify Admin UI, Admin MCP, DAPR component topology, Aspire apphost, or Fluent UI packages.
- Do not perform DW2 live-evidence regeneration unless it is the smallest practical way to reconcile DW2-DF5; documentation reconciliation is acceptable when production parser semantics are proven.
- Do not broaden the audit guard into DW9 governance or generalized CI/static-analysis polish.
- Do not claim live Aspire smoke evidence unless a dev-story run actually captures it.
- Do not initialize or update nested submodules.
- Do not edit generated preflight JSON audit files.

## Party-Mode Review Clarifications

The 2026-05-07 party-mode review tightened this story with the following implementation-facing constraints:

- **Observable contract:** `eventstore.failure_reason` values are an operator-facing observability contract. Existing DW1 values (`drain_event_count_mismatch`, `drain_missing_event`, `drain_publish_failed`, `unknown`) are stable wire values. Additive DW8 values are allowed only when they are represented in `DrainReasonCodes`, focused tests, and the architecture note.
- **Classifier scope:** Classification should be operation-boundary based: state-store persistence, DAPR actor/runtime unavailable, publish failed, existing DW1 mismatch/missing conditions, and residual `unknown`. Avoid localized exception-message substring matching and avoid broad `catch Exception` branches that hide actionable known categories.
- **Architecture note requirements:** The architecture/operations note must include a table with `Reason code`, `Emitted when`, `Retry expectation`, `Operator action`, and `Compatibility rule`. It should identify where the code is observable: activity tag, structured log field, test assertion, and any non-persisted diagnostic surface.
- **Identifier audit scope:** The protected fields are `messageId`, `correlationId`, `aggregateId`, and `causationId`. The audit guard fails only when `Guid.TryParse` or `Guid.Parse` is used to validate one of those protected fields. GUID-form example values, middleware correlation IDs, temporary names, `Guid.NewGuid()`, and `UniqueIdHelper.ToGuid` remain out of scope unless they become protected-ID validators.
- **DW2 evidence reconciliation:** DW2 GUID-form seeded `correlationId` evidence may remain valid historical/test evidence if production validation accepts it through documented non-whitespace `AggregateIdentity` semantics or another approved non-GUID parser path. The reconciliation must make clear that GUID shape is not required and must not be used as a validation gate.

## Advanced Elicitation Clarifications

The 2026-05-07 advanced elicitation pass tightened DW8 with the following implementation-facing constraints:

- **Pre-edit audit baseline:** Before changing identifier code or the audit guard, capture the current `Guid.TryParse` / `Guid.Parse` hit list and classify each hit as protected-field validator, allowed HTTP/middleware correlation identifier, builder/test assertion, conversion helper, or unrelated generated identifier. The Dev Agent Record must preserve this classification so the final guard is reviewable instead of relying on an opaque source scan.
- **Guard precision contract:** The regression guard must fail on a `Guid.TryParse` / `Guid.Parse` call only when the call participates in validating `messageId`, `correlationId`, `aggregateId`, or `causationId` as EventStore command/event metadata. It must report file, line, protected field, and the allowed alternatives (`Ulid.TryParse`, `UniqueIdHelper`, or documented non-whitespace `AggregateIdentity` semantics). Path-only allowlists are acceptable only for explicitly named non-metadata surfaces such as HTTP middleware correlation headers, not for generic test folders.
- **Reason-code compatibility proof:** Tests or documentation must assert the literal values of all stable DW1 reason codes and every additive DW8 reason code. The implementation must not introduce `drain_failure_unknown` as a replacement for the existing `unknown` wire value; any aliasing proposal is a deferred architecture decision.
- **Classifier decision boundary:** If state-store persistence and DAPR runtime failures surface through broad exception types, classify them only when the operation boundary is controlled by the drain code. Do not classify by localized exception-message substrings. If no stable boundary is available without widening production API, record the blocker and keep the failure in `unknown` until a future story adds a deterministic seam.
- **Seeded-evidence closure rule:** DW2-DF5 is closed only when the story cites the exact seeded-stream artifact/value inspected and either updates the seeding path to ULID-shaped metadata or documents the precise server parser path that accepts the GUID-shaped value without using GUID validation. A generic statement that the live smoke passed is not sufficient evidence.
- **Disposition gating:** Do not mark the two DW8-owned deferred-work entries as resolved until the classifier tests, identifier guard, architecture-note table, and DW2 evidence reconciliation are all recorded. If any part is intentionally deferred, update only that entry with an explicit `ACCEPTED-DEBT` or follow-up-story disposition.

## Implementation Inventory

| Area | File / artifact | Expected use |
| --- | --- | --- |
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md` | Proposal C / DW8 scope and acceptance direction |
| Deferred source | `_bmad-output/implementation-artifacts/deferred-work.md` | DW8 routed drain-classifier and identifier-validation entries |
| Drain actor | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | primary classifier call site |
| Drain reason constants | `src/Hexalith.EventStore.Server/Actors/DrainReasonCodes.cs` | stable reason-code vocabulary |
| Drain tests | `tests/Hexalith.EventStore.Server.Tests/Actors/Dw1DrainHardeningAtddTests.cs` and `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` | extend/adjust focused reason-code coverage |
| Identifier rules | `CLAUDE.md` and `_bmad-output/planning-artifacts/architecture.md` | R2-A7 and D12 source of truth |
| Identifier precedent | `_bmad-output/implementation-artifacts/post-epic-3-r3a1-replay-ulid-validation.md` and `_bmad-output/implementation-artifacts/post-epic-3-r3a6-tier3-error-contract-update.md` | prior no-`Guid.TryParse` fixes and exceptions |
| Seeded evidence | `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/seeded-stream-summary.json` | DW2-DF5 contradiction to reconcile if present |
| Sprint status | `_bmad-output/implementation-artifacts/sprint-status.yaml` | story status bookkeeping only |
| Run log | `_bmad-output/process-notes/predev-hardening-runs.log` | automation-created run trace |

## Current Code Intelligence

- `AggregateActor.DrainUnpublishedEventsAsync` sets `eventstore.failure_reason` to `DrainReasonCodes.PublishFailed` for `EventPublishResult.Success == false` and calls `ClassifyDrainFailure(ex)` for exceptions.
- `ClassifyDrainFailure` currently maps only `DrainEventCountMismatchException` to `drain_event_count_mismatch`, `MissingEventException` to `drain_missing_event`, and all other exceptions to `unknown`.
- `DrainReasonCodes` currently defines `EventCountMismatch`, `MissingEvent`, `PublishFailed`, and `Unknown`. `Unknown` is currently the literal `unknown`, while the planning proposal also uses the phrase `drain_failure_unknown`; preserve existing wire compatibility unless architecture review deliberately approves a rename.
- `Dw1DrainHardeningAtddTests` already documents the DW1 stable vocabulary and activity-listener pattern. Prefer adding a DW8-focused test class or extending existing focused drain tests rather than creating broad integration-only coverage.
- `EventDrainRecoveryTests` already has passing assertions for `DrainReasonCodes.PublishFailed` and `DrainReasonCodes.EventCountMismatch`; new categories should use constants rather than duplicate string literals.
- R2-A7 states that controllers and validators handling `messageId`, `correlationId`, `aggregateId`, or `causationId` must use `Ulid.TryParse` or accept any non-whitespace string per `AggregateIdentity` rules; `Guid.TryParse` on those fields is forbidden.
- The codebase still uses `Guid.NewGuid()` in many tests and some generated correlation IDs. Do not treat every generator call as a validator defect. The audit guard should focus on parser/validator semantics and protected field names.
- `Directory.Packages.props` pins `Hexalith.Commons.UniqueIds` `2.13.0`; continue using the shared Hexalith helper or existing ULID parser conventions rather than adding another ULID package.

## Tasks / Subtasks

- [ ] Task 0: Baseline the routed defects and scope (AC: #1, #5, #8)
  - [ ] 0.1 Re-read Proposal C / DW8 and the two DW8-owned deferred-work entries.
  - [ ] 0.2 Confirm current `AggregateActor.ClassifyDrainFailure` behavior and current `DrainReasonCodes` values before editing.
  - [ ] 0.3 Locate parser/validator seams for the protected fields and separate parser defects from ordinary `Guid.NewGuid()` test-data patterns.
  - [ ] 0.4 Confirm DW9 evidence-validator/governance CI work remains out of scope.
  - [ ] 0.5 Capture and classify the current `Guid.TryParse` / `Guid.Parse` hit list before implementing the regression guard.

- [ ] Task 1: Harden drain reason classification (AC: #1, #2, #3)
  - [ ] 1.1 Add stable constants for approved new reason codes without renaming existing DW1 constants.
  - [ ] 1.2 Classify DAPR/runtime unavailable exceptions and state-store persistence failures where the exception type or context can be identified deterministically.
  - [ ] 1.3 Preserve raw exception details in structured logs only; activity tags must remain bounded stable codes.
  - [ ] 1.4 Keep `unknown` as the explicit residual bucket and document when it is expected.
  - [ ] 1.5 Assert literal wire values for existing and new reason codes so compatibility regressions are obvious.

- [ ] Task 2: Add focused drain tests (AC: #1, #2, #3, #9)
  - [ ] 2.1 Add activity-listener coverage for each new reason code that can be triggered safely.
  - [ ] 2.2 Keep existing DW1 reason-code tests green and avoid brittle assertions on raw exception messages.
  - [ ] 2.3 If a category cannot be triggered without invasive seams, record the blocker and test the classifier helper directly only if it can be exposed without widening production API.

- [ ] Task 3: Implement the identifier parser audit guard (AC: #5, #6, #7)
  - [ ] 3.1 Build a focused source scan or test that flags `Guid.TryParse` when tied to `messageId`, `correlationId`, `aggregateId`, or `causationId`.
  - [ ] 3.2 Add allowlist comments or scoped exclusions for unrelated middleware/generated correlation IDs and `UniqueIdHelper.ToGuid` conversion paths.
  - [ ] 3.3 Reconcile DW2 seeded-stream `correlationId` evidence by changing the seeding flow to ULID generation or documenting the precise non-whitespace acceptance rationale.
  - [ ] 3.4 Record the final audit hit list and disposition in the Dev Agent Record.
  - [ ] 3.5 Ensure the guard failure output includes file, line, protected field, and the approved parser/semantic alternatives.

- [ ] Task 4: Document the reason-code taxonomy (AC: #4)
  - [ ] 4.1 Add or update an architecture/operations doc with the drain reason-code table.
  - [ ] 4.2 Include code value, trigger condition, retry/operator meaning, and compatibility guidance.
  - [ ] 4.3 Link the doc from this story's Dev Agent Record or Change Log.

- [ ] Task 5: Update deferred-work dispositions narrowly (AC: #8)
  - [ ] 5.1 Update only the drain-classifier entry routed to DW8 after classifier work lands.
  - [ ] 5.2 Update only DW2-DF5 after the identifier audit is complete.
  - [ ] 5.3 Do not modify adjacent DW7 or DW9 entries unless a direct DW8 change resolves them and the rationale is recorded.

- [ ] Task 6: Validate and capture evidence (AC: #9, #10)
  - [ ] 6.1 Run focused drain reason-code tests.
  - [ ] 6.2 Run the identifier audit guard.
  - [ ] 6.3 Run `dotnet test tests/Hexalith.EventStore.Server.Tests --configuration Release` or narrower affected classes if full Server.Tests is blocked.
  - [ ] 6.4 Record exact commands, results, and any pre-existing build blockers.
  - [ ] 6.5 Confirm generated preflight JSON files remain unstaged and no nested submodules were initialized or updated.

- [ ] Task 7: Close story bookkeeping (AC: #10)
  - [ ] 7.1 Update Dev Agent Record, File List, Verification Status, and Change Log.
  - [ ] 7.2 Move this story and `sprint-status.yaml` to `review` only after implementation evidence is present.
  - [ ] 7.3 Move both to `done` only after code-review signoff.

## Dev Notes

### Architecture Guardrails

- Drain activity tags are public observability surface area. Treat reason-code strings like API values: additive changes are acceptable, renames require explicit compatibility notes.
- Keep classifier decisions deterministic. Prefer exception types, known DAPR/status exceptions, or controlled context over substring matching localized exception messages.
- Use `DrainReasonCodes` constants in production and tests. Avoid repeating string literals outside docs and explicit wire-value assertions.
- The identifier audit is about parser/validator semantics. It must not become a wholesale conversion of test data, middleware-generated HTTP correlation IDs, or GUID-to-ULID conversion helper behavior.
- For protected identifiers, the acceptable semantics are: `Ulid.TryParse`, `UniqueIdHelper` generation/validation where available, or documented non-whitespace acceptance per `AggregateIdentity`. `Guid.TryParse` is not acceptable as validation.
- Treat GUID-shaped protected identifier examples as data-shape compatibility evidence, not as permission to add GUID-only parser branches.
- If a state-store or DAPR runtime failure cannot be observed through a stable unit-test seam without widening production API, document the blocker and test the nearest deterministic classifier boundary instead.

### Testing Guidance

- Start with `Dw1DrainHardeningAtddTests` and `EventDrainRecoveryTests` for activity-listener examples and existing drain setup helpers.
- If new exception categories need fake state-manager/publisher behavior, prefer narrow substitutes in Server.Tests over live Aspire tests.
- Add the identifier audit as a normal test if practical so it can run in CI with a clear failure message and a small allowlist.
- Minimum DW8 evidence should include classifier table tests, publish-result failure preservation, one simulated persistence/runtime failure path if injectable today, and the protected-ID audit guard.
- Do not run solution-level `dotnet test`.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-deferred-work-open-cleanup.md#Proposal-C-DW8-Server-Drain-Classifier-ULID-Identifier-Audit`] - DW8 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md#DrainReasonUnknown-will-dominate-production-drain-failures`] - routed drain-classifier deferred item.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md#DW2-DF5-Seeded-stream-correlationId-is-GUID-formatted-while-CLAUDEmd-mandates-ULID-identifier-parsers`] - routed identifier audit deferred item.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`] - drain failure handling and classifier call site.
- [Source: `src/Hexalith.EventStore.Server/Actors/DrainReasonCodes.cs`] - stable reason-code constants.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Actors/Dw1DrainHardeningAtddTests.cs`] - activity listener and stable-code regression pattern.
- [Source: `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`] - existing drain recovery coverage.
- [Source: `CLAUDE.md#ID-validation-rule`] - R2-A7 no-`Guid.TryParse` protected identifier rule.
- [Source: `_bmad-output/planning-artifacts/architecture.md#D12-ULID-Everywhere-HexalithCommonsUniqueIds`] - ULID source-of-truth and package guidance.
- [Source: `_bmad-output/implementation-artifacts/post-epic-3-r3a1-replay-ulid-validation.md`] - prior production replay validator fix.
- [Source: `_bmad-output/implementation-artifacts/post-epic-3-r3a6-tier3-error-contract-update.md`] - prior test-layer GUID parser correction and documented exceptions.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-07T05:57:38Z`, result `pass`.
- Party-mode preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-07T06:57:59Z`, result `fail` only for working tree cleanliness with stdout `_bmad-output/test-artifacts/test-design-progress.md`, `_bmad-output/test-artifacts/test-design/archive/`, and `_bmad-output/test-artifacts/test-design/test-design-epic-1.md`; classified as a soft working-tree warning outside BMAD pre-dev story-operation paths.
- Create-story activation: resolved workflow customization with no prepend/append steps; no `project-context.md` file was present in the workspace.
- Aspire pre-edit baseline attempt: `aspire run --detach --non-interactive --apphost src\Hexalith.EventStore.AppHost\Hexalith.EventStore.AppHost.csproj --format Json` failed to build. First blocker in `C:\Users\JeromePiquot\.aspire\logs\cli_20260507T055837606_detach-child_b13122c60ff54506bf1214503905369d.log`: `CS0009 Metadata file 'D:\Hexalith.EventStore\src\Hexalith.EventStore.Client\obj\Debug\net10.0\ref\Hexalith.EventStore.Client.dll' could not be opened -- PE image doesn't contain managed metadata`, followed by missing `Hexalith.EventStore.Client` and `Hexalith.EventStore.SignalR` namespace/type errors in Sample, Server, Sample.BlazorUI, and Admin.UI projects. No apphost code was changed by this story creation run.

### Completion Notes List

- Created ready-for-dev story from first backlog row in the Post-Epic Deferred Work OPEN Cleanup package.
- Scoped DW8 to the server drain-classifier taxonomy and protected-identifier parser audit entries.
- Recorded current code intelligence for `AggregateActor.ClassifyDrainFailure`, `DrainReasonCodes`, DW1 drain tests, R2-A7, D12, and the DW2 seeded-stream evidence contradiction.
- Applied party-mode review clarifications for classifier compatibility, operation-boundary classification, audit-guard false-positive limits, DW2 correlationId reconciliation, and compact architecture-note requirements.
- Applied advanced-elicitation clarifications for pre-edit parser-hit classification, guard diagnostics, reason-code literal compatibility proof, deterministic classifier boundaries, DW2 seeded-evidence closure, and deferred-work disposition gating.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw8-server-classifier-and-ulid-audit.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight passed before story creation.
- Party-mode review completed on 2026-05-07; all participating agents recommended `needs-story-update`, and low-risk clarifications were applied without changing the story status.
- Advanced elicitation completed on 2026-05-07; low-risk story clarifications were applied without changing product scope, architecture policy, or story status.
- AppHost baseline run attempted before edits but blocked by the existing Debug ref assembly metadata failure described in Debug Log References.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, or submodules.

## Party-Mode Review

- ISO date and time: `2026-05-07T09:01:02+02:00`
- Selected story key: `post-epic-deferred-dw8-server-classifier-and-ulid-audit`
- Command / skill invocation used: `/bmad-party-mode post-epic-deferred-dw8-server-classifier-and-ulid-audit; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: The story is close to ready, but the reviewers found ambiguity around final drain reason-code compatibility semantics, operation boundaries for state-store/DAPR/publish failures, protected-ID audit-guard scan scope, DW2 GUID-form `correlationId` reconciliation, and architecture-note size/observable surface.
- Changes applied: Clarified publish exceptions vs unsuccessful publish results, stable DW1 wire-value compatibility, operation-boundary classifier scope, compact architecture-note table requirements, protected-ID audit exclusions, DW2 evidence reconciliation semantics, minimum focused validation evidence, and non-goals for DW1/DW2/DW3/DW6/DW7/DW9 scope.
- Findings deferred: Formal public diagnostic-schema governance, deeper DAPR exception subcategories, centralized protected-ID parser API, broader identifier normalization, DW9 CI/static-analysis polish, and any drain retry/lifecycle redesign.
- Final recommendation: `needs-story-update`

## Advanced Elicitation

- ISO date and time: `2026-05-07T10:16:00+02:00`
- Selected story key: `post-epic-deferred-dw8-server-classifier-and-ulid-audit`
- Command / skill invocation used: `/bmad-advanced-elicitation post-epic-deferred-dw8-server-classifier-and-ulid-audit`
- Batch 1 methods: Self-Consistency Validation; Red Team vs Blue Team; Architecture Decision Records; Security Audit Personas; Failure Mode Analysis
- Batch 2 methods: Chaos Monkey Scenarios; Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Lessons Learned Extraction
- Findings summary: The story was ready to implement, but elicitation found review risks around opaque parser scans, over-broad allowlists, accidental `unknown` wire-value renames, exception-message-based classification, incomplete DW2 seeded-evidence closure, and premature deferred-work disposition updates.
- Changes applied: Added advanced-elicitation clarifications, pre-edit parser-hit classification, literal reason-code compatibility proof, guard diagnostic requirements, deterministic classifier-boundary guidance, seeded-evidence closure criteria, disposition gating, and matching task updates.
- Findings deferred: Central diagnostic-schema governance, a shared protected-identifier parser API, deterministic DAPR/state-store seams that require production API widening, broad ULID test-data sweeps, and DW9 generalized CI/static-analysis governance.
- Final recommendation: `ready-for-dev`

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-07 | 0.3 | Applied advanced elicitation and tightened parser audit, reason-code compatibility, deterministic classifier, and evidence-closure guidance. | Codex automation |
| 2026-05-07 | 0.2 | Recorded party-mode review and clarified classifier contract, protected-ID audit scope, and DW2 evidence reconciliation. | Codex automation |
| 2026-05-07 | 0.1 | Created ready-for-dev DW8 server classifier and ULID audit story. | Codex automation |
