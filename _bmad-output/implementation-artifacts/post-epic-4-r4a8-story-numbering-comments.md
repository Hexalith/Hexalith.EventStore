# Post-Epic-4 R4-A8: Story Numbering Comments

Status: done

<!-- Source: epic-4-retro-2026-04-26.md R4-A8 -->
<!-- Source: sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md Proposal 8 -->
<!-- Source: sprint-change-proposal-2026-04-28.md Proposal 6 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **platform maintainer reading Epic 4 recovery and publication code**,
I want stale Story 4.4 / 4.5 source comments normalized to the current Epic 4 story map or neutral feature names,
So that future agents and reviewers do not chase obsolete story numbers while auditing event distribution behavior.

## Story Context

Epic 4 now contains three canonical stories:

- Story 4.1: CloudEvents Publication & Topic Routing.
- Story 4.2: Resilient Publication & Backlog Draining.
- Story 4.3: Per-Aggregate Backpressure.

Older implementation comments still refer to Story 4.4 and Story 4.5 in production code and AppHost DAPR component YAML. The Epic 4 retrospective records this as R4-A8: a low-risk traceability cleanup, not a behavioral change. This story closes only that cleanup. It must not absorb R4-A5 runtime pub/sub proof, R4-A6 drain integrity behavior, or any backlog/security gate.

Current HEAD at story creation: `da59238`.

## Acceptance Criteria

1. **Stale production comments are inventoried before editing.** Run a focused search over `src/` for obsolete Epic 4 references, at minimum:
   - `rg -n "Story 4\\.[45]|4\\.4|4\\.5" src -S`
   - Record the before/after counts in the Dev Agent Record. Classify each match before editing as a source/config comment, YAML comment, test/comment label, generated output, or non-story numeric value. Do not edit a runtime value merely because it contains `4.4` or `4.5`; if the broad grep reveals a non-comment value, record it as a false positive or blocker in the Dev Agent Record instead of changing behavior.

2. **Production source/config comments no longer point to obsolete Story 4.4 or Story 4.5.** After implementation, both `rg -n "Story 4\\.[45]" src -S` and `rg -n "Story 4\\.[45]|4\\.4|4\\.5" src -S` return no production-source comment matches. Replace comments with current story references where clear, or with neutral feature language where a single current story number would be misleading. If any broad-regex match remains because it is not a stale story/comment reference, document the exact path, line, reason, and unchanged value in the Dev Agent Record.

3. **Behavior is unchanged.** The diff contains only comment/documentation wording changes plus required story bookkeeping. No method bodies, constants, options defaults, DAPR component values, topic names, routes, retry policies, access-control scopes, generated files, package/version metadata, or test assertions are changed. YAML edits must be comment-only and preserve indentation, keys, scalar values, and ordering.

4. **Current Epic 4 mapping is explicit where useful.** Drain recovery comments may reference Story 4.2 or the neutral phrase "drain recovery"; publication/topic comments may reference Story 4.1; backpressure comments may reference Story 4.3. Dead-letter comments should prefer neutral feature language such as "dead-letter routing" unless a current story artifact explicitly owns the detail.

5. **Generated artifacts and historical story records are not rewritten.** Do not edit completed story files, retrospectives, planning proposals, or `_bmad-output/process-notes` to erase history. Historical mentions of Story 4.4 / 4.5 remain valid audit trail unless this story's Dev Agent Record needs to cite them.

6. **Tests remain a verification gate, not the primary change.** Do not modify test behavior for this cleanup. Test comments may be updated only when they are stale story-number labels and the edit is comment-only.

7. **Build verification is captured.** Run `dotnet build Hexalith.EventStore.slnx --configuration Release`. If a pre-existing build failure appears, record its exact shape and prove the failure is unrelated to comment-only edits. Also run `git diff --check` and record that the changed source/config hunks are comment-only; if the build is skipped because only BMAD story bookkeeping changed during dev handoff, record that rationale explicitly.

8. **Story bookkeeping is closed.** At dev handoff, this story status becomes `review`, the sprint-status row becomes `review`, and both `last_updated` fields in `sprint-status.yaml` name R4-A8 and the result. At code-review signoff, both become `done`. Do not touch R4-A5 or R4-A6 status rows.

## Scope Boundaries

- Do not change runtime behavior in `AggregateActor`, event publishing, drain retry, dead-letter routing, DAPR component behavior, or AppHost topology.
- Do not broaden DAPR pub/sub scopes, subscriber definitions, resiliency values, or dead-letter topic patterns.
- Do not add or remove tests unless a build/lint rule requires a comment-only adjustment.
- Do not rewrite completed story artifacts or planning artifacts to hide historical numbering.
- Do not perform R4-A5 Tier 3 runtime proof or R4-A6 drain integrity work here.
- Do not update checked-in generated artifacts, dependency manifests, lock files, or release metadata while chasing the broad `4.4` / `4.5` search results.

## Implementation Inventory

| Area | File | Expected use |
|---|---|---|
| Actor orchestration comments | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | Normalize top-level, drain-recovery, dead-letter, and infrastructure-failure story labels without changing code |
| Drain record comments | `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs` | Replace old drain recovery story label with current Story 4.2 or neutral wording |
| Drain options comments | `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs` | Replace old Story 4.4 wording with current drain-recovery wording |
| Publisher options comments | `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs` | Replace old Story 4.5 wording with neutral dead-letter routing wording |
| Telemetry comments | `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs` | Normalize activity/tag summary comments for drain and dead-letter spans |
| Local pub/sub config comments | `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml` | Normalize dead-letter and per-subscription comment wording only |
| Local resiliency comments | `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml` | Normalize drain recovery comment wording only |
| Sample subscription comments | `src/Hexalith.EventStore.AppHost/DaprComponents/subscription-sample-counter.yaml` | Normalize dead-letter topic convention wording only |
| Optional test comment cleanup | `tests/Hexalith.EventStore.Server.Tests/**` | Comment-only updates if obsolete Story 4.4 / 4.5 labels are present and confusing |

## Tasks / Subtasks

- [x] Task 0: Baseline and inventory (AC: #1)
  - [x] 0.1 Record current HEAD SHA and confirm this story is still `ready-for-dev`.
  - [x] 0.2 Run `rg -n "Story 4\\.[45]|4\\.4|4\\.5" src -S` and record the matching production files.
  - [x] 0.3 Classify each match as drain recovery, dead-letter routing, pub/sub topic config, generated output, non-story numeric value, or unrelated numeric text before editing.

- [x] Task 1: Normalize production comments (AC: #2, #3, #4)
  - [x] 1.1 Update drain-recovery comments to Story 4.2 or neutral "drain recovery" wording.
  - [x] 1.2 Update dead-letter comments to neutral "dead-letter routing" wording unless a current story reference is plainly correct.
  - [x] 1.3 Update pub/sub and subscription YAML comments without changing YAML values, scopes, metadata, or indentation semantics.
  - [x] 1.4 Update telemetry summary comments only; do not rename activity constants or tag constants.
  - [x] 1.5 If a broad grep match cannot be safely removed because it is not a stale story comment, leave it unchanged and record the false-positive rationale with path and line.

- [x] Task 2: Optional stale test-comment cleanup (AC: #3, #6)
  - [x] 2.1 Search tests for exact stale Epic 4 story labels with `rg -n "Story 4\\.[45]" tests/Hexalith.EventStore.Server.Tests -S`.
  - [x] 2.2 If a test comment is only a stale story label, update it to neutral task/feature wording.
  - [x] 2.3 Do not rewrite unrelated decimal task/AC markers such as `Task 4.4`, `AC#3: 4.4`, or non-Epic-4 story references.
  - [x] 2.4 Do not change assertions, test names, fixtures, test data, or test counts.

- [x] Task 3: Verification gates (AC: #2, #3, #7)
  - [x] 3.1 Re-run `rg -n "Story 4\\.[45]" src -S` and confirm zero production-source matches.
  - [x] 3.2 Review `git diff --check`.
  - [x] 3.3 Review `git diff --name-only` and confirm only expected comment/story-bookkeeping files changed.
  - [x] 3.4 Review the source/config hunks and record that every changed `src/` line is a comment-only change.
  - [x] 3.5 Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.

- [x] Task 4: Story bookkeeping (AC: #8)
  - [x] 4.1 Update this story's Dev Agent Record, File List, Change Log, and Verification Status.
  - [x] 4.2 Move this story and only this story from `ready-for-dev` to `review` in `sprint-status.yaml`.
  - [x] 4.3 Leave R4-A5 and R4-A6 status rows unchanged.

## Dev Notes

### Architecture Guardrails

- ADR-P2 keeps the state store as source of truth and pub/sub as distribution. This story must not touch that ordering.
- D6 topic naming remains `{tenant}.{domain}.events`; dead-letter topic comments must not imply a different runtime pattern.
- Rule 4 still applies: no application-level pub/sub retry loops. DAPR resiliency and drain reminders own retries.
- Rule 5 still applies: comments must not encourage logging event payload data.
- Rule 6 still applies: actor state remains accessed through `IActorStateManager`; do not introduce direct `DaprClient` state access while touching nearby comments.
- DAPR access-control and pub/sub scopes are runtime security boundaries. Comment cleanup must not alter component metadata values.

### Current-Code Intelligence

- The source search at story creation found obsolete labels in `AggregateActor.cs`, `UnpublishedEventsRecord.cs`, `EventDrainOptions.cs`, `EventPublisherOptions.cs`, `EventStoreActivitySource.cs`, and three AppHost DAPR component YAML files.
- The party-mode review source sweep on 2026-05-01 found 24 `src/` matches for `Story 4\.[45]|4\.4|4\.5`; the after-edit production gate should use the same broad regex so raw `4.4` / `4.5` labels cannot remain.
- A broad `tests/` search also matches unrelated task numbers, AC examples, and other-story references. Optional test cleanup should target exact `Story 4.4` / `Story 4.5` Epic 4 labels only.
- The broad production grep is deliberately strict, but not every `4.4` / `4.5` token is automatically a stale Epic 4 story reference. Do not change semantic values, versions, generated output, or unrelated numeric data to satisfy a search gate; record false positives or blockers instead.
- YAML component files under `src/Hexalith.EventStore.AppHost/DaprComponents/` are part of the runtime topology. Only `#` comments may change there; values such as component names, routes, topics, scopes, metadata, and resiliency policies must remain byte-for-byte equivalent unless a later behavioral story owns the change.
- Story 4.2 is the current owner of persist-then-publish failure handling, `UnpublishedEventsRecord`, drain reminders, and backlog draining.
- Story 4.1 is the current owner of CloudEvents publication and tenant-domain topic routing.
- Story 4.3 is the current owner of per-aggregate backpressure. It also mentions drain-pending records because those records affect pending command count; do not rewrite that behavior.
- Dead-letter routing appears in the codebase as infrastructure-failure handling and pub/sub dead-letter topic configuration, but the current Epic 4 story map has no standalone Story 4.5. Neutral wording is safer than inventing a new current story owner.
- R4-A5 is in `review` at story creation and owns Tier 3 command-to-subscriber delivery proof. R4-A6 is `ready-for-dev` and owns drain incomplete-range guard behavior. R4-A8 is only traceability cleanup.

### Testing Standards

- This is a comment-only source cleanup. The primary verification is diff review, grep gates, and release build.
- If test projects are run beyond build, run the repository's Tier 1 unit projects individually.
- Do not run a solution-level `dotnet test`; repository instructions call out project-level test runs.
- `Hexalith.EventStore.Server.Tests` has a known pre-existing CA2007 warning-as-error build issue in some contexts; record if encountered but do not weaken the R4-A8 comment-only gate.

### Latest Technical Information

No external technical research is needed for this story. It does not change DAPR, Aspire, .NET, CloudEvents, or OpenTelemetry behavior; it only aligns comments with the repository's current story map.

### Project Structure Notes

- Expected source edits are limited to comments in `src/`.
- Expected optional test edits are limited to comments in `tests/Hexalith.EventStore.Server.Tests/`.
- Expected BMAD edits are this story file plus `sprint-status.yaml` during dev handoff.
- Keep all completed story artifacts and retrospective documents as historical records.

### References

- [Source: `_bmad-output/implementation-artifacts/epic-4-retro-2026-04-26.md`] - R4-A8 action item: normalize old Story 4.4 / 4.5 source comments to current Epic 4 story numbers.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-26-epic-4-retro-cleanup.md#Proposal-8`] - R4-A8 acceptance criteria: comments reference current story numbers or neutral feature names, no behavior changes, build remains clean.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-28.md#Proposal-6`] - R4-A8 remains low-risk and non-behavioral.
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic-4-Event-Distribution-and-PubSub`] - Current Epic 4 story map has Stories 4.1, 4.2, and 4.3 only.
- [Source: `_bmad-output/implementation-artifacts/4-1-cloudevents-publication-and-topic-routing.md`] - Current publication/topic routing owner.
- [Source: `_bmad-output/implementation-artifacts/4-2-resilient-publication-and-backlog-draining.md`] - Current drain recovery owner.
- [Source: `_bmad-output/implementation-artifacts/4-3-per-aggregate-backpressure.md`] - Current backpressure owner.
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`] - Main source comments with stale labels.
- [Source: `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`] - Local pub/sub dead-letter comments with stale labels.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-03T11:44:06+02:00: Starting HEAD `295a7e76677fb8320a7fec5b6725de5f699aadc6` (`295a7e7`); story row confirmed `ready-for-dev` before moving to `in-progress`.
- 2026-05-03T11:44+02:00: Aspire MCP showed AppHost already running. Healthy resources included `keycloak`, `statestore`, `pubsub`, `eventstore-admin`, `eventstore-admin-ui`, `sample-blazor-ui`, and `tenants`; `eventstore` and `sample` were already finished with exit code `-1` before comment edits.
- Baseline production broad grep: `rg -n "Story 4\\.[45]|4\\.4|4\\.5" src -S` returned 25 matches.
- Baseline production exact story grep: `rg -n "Story 4\\.[45]" src -S` returned 24 matches.
- Baseline optional test exact story grep: `rg -n "Story 4\\.[45]" tests/Hexalith.EventStore.Server.Tests -S` returned 9 matches.
- Baseline classification: `AggregateActor.cs` source comments include drain recovery labels and dead-letter routing labels; `UnpublishedEventsRecord.cs` and `EventDrainOptions.cs` source comments are drain recovery labels; `EventPublisherOptions.cs` source comment is dead-letter routing; `EventStoreActivitySource.cs` XML comments include drain and dead-letter telemetry labels; `pubsub.yaml`, `resiliency.yaml`, and `subscription-sample-counter.yaml` matches are YAML comments for pub/sub topic config, dead-letter routing, or drain recovery. No generated output, non-story numeric value, unrelated numeric text, or runtime value matched the production broad grep.
- After production comment edits, `rg -n "Story 4\\.[45]" src -S` and `rg -n "Story 4\\.[45]|4\\.4|4\\.5" src -S` returned zero matches.
- Optional test-comment cleanup left one exact `tests/` match unchanged at `tests/Hexalith.EventStore.Server.Tests/Configuration/ResiliencyConfigurationTests.cs:27` because it is an assertion custom message, and AC #6 / Task 2.4 forbid assertion changes for this cleanup.
- Verification: `git diff --check` exited 0; output contained only Git line-ending warnings.
- Verification: `git diff --name-only` showed only expected source/config comment files, optional server test comment files, this story file, and `sprint-status.yaml`.
- Verification: `git diff -U0 -- src tests/Hexalith.EventStore.Server.Tests` showed every changed `src/` line was a C# comment/XML doc or YAML `#` comment; optional test changes were comments only.
- Verification: `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeded with 0 warnings and 0 errors.
- Regression tests: `dotnet test tests/Hexalith.EventStore.Server.Tests --configuration Release --no-build` passed 1712/1712; `Client.Tests` passed 334/334; `Contracts.Tests` passed 281/281; `Sample.Tests` passed 63/63; `Testing.Tests` passed 78/78.

### Implementation Plan

- Replace stale drain-recovery comments with Story 4.2 or neutral drain recovery wording.
- Replace stale dead-letter comments with neutral dead-letter routing wording.
- Keep YAML edits to `#` comments only and preserve keys, scalar values, ordering, scopes, topics, and resiliency policy values.
- Update optional stale test comments only when they are exact `Story 4.4` / `Story 4.5` labels; leave assertions, names, fixtures, and numeric examples unchanged.

### Completion Notes List

- Completed Task 0 baseline inventory. All 25 production broad-grep matches were stale source/config/YAML comments, not runtime values.
- Completed Task 1 production normalization. All stale `Story 4.4` / `Story 4.5` and raw `4.4` / `4.5` production-source matches were removed with comment-only edits.
- Completed Task 2 optional test-comment cleanup. Stale exact test comments were normalized; the remaining test match is an assertion message intentionally left unchanged.
- Completed Task 3 verification. Production grep gates, diff check, comment-only diff review, Release build, and selected regression tests all passed.
- Completed Task 4 bookkeeping. Story and sprint status are ready for review; R4-A5 and R4-A6 rows were not changed.

### File List

- `_bmad-output/implementation-artifacts/post-epic-4-r4a8-story-numbering-comments.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`
- `src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml`
- `src/Hexalith.EventStore.AppHost/DaprComponents/subscription-sample-counter.yaml`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Actors/UnpublishedEventsRecord.cs`
- `src/Hexalith.EventStore.Server/Configuration/EventDrainOptions.cs`
- `src/Hexalith.EventStore.Server/Configuration/EventPublisherOptions.cs`
- `src/Hexalith.EventStore.Server/Telemetry/EventStoreActivitySource.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorDomainResultTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/UnpublishedEventsRecordTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Configuration/EventDrainOptionsTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs`

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-05-01 | 0.1 | Created ready-for-dev R4-A8 story-numbering comment cleanup story. | Codex automation |
| 2026-05-01 | 0.2 | Party-mode review tightened production grep gates and test-comment cleanup scope. | Codex automation |
| 2026-05-03 | 0.3 | Advanced elicitation hardened broad grep false-positive handling, comment-only proof, and YAML behavior-preservation rules. | Codex automation |
| 2026-05-03 | 1.0 | Normalized stale Epic 4 Story 4.4 / 4.5 source, config, and test comments; verified comment-only diff, Release build, and selected regression tests. | GPT-5 Codex |

## Verification Status

Done after code review (2026-05-03). Production `src/` grep gates return zero stale Story 4.4 / 4.5 and raw 4.4 / 4.5 matches (re-verified post-patches). `git diff --check`, Release build (dev-time), and selected regression tests passed; changed `src/` hunks are comment-only. Code review applied 3 patches (2 YAML tautology rewrites post-Story-4.5-scrub, 1 EventDrainOptionsTests AC traceability re-link) and recorded 2 deferred follow-ups (orphaned "task 6.7" reference in `AggregateActor.cs`; stale Story 4.4/4.5 references in `deploy/dapr/*.yaml` + `docs/guides/dapr-component-reference.md`). AC #8 historical attribution restored as a comment line in `sprint-status.yaml:33` because two concurrent R9-A5 commits (`5332495`, `079aead`) clobbered the dev-time `last_updated` field update before R4-A8 was committed.

## Party-Mode Review

- Date/time: 2026-05-01T11:19:43+02:00
- Selected story key: `post-epic-4-r4a8-story-numbering-comments`
- Command/skill invocation used: `/bmad-party-mode post-epic-4-r4a8-story-numbering-comments; review;`
- Participating BMAD agents: Bob (Scrum Master), Winston (Architect), Amelia (Developer Agent), Murat (Master Test Architect), Paige (Technical Writer), Sally (UX Designer)
- Findings summary:
  - Bob: The story is appropriately narrow and ready-for-dev, but AC #2 needed to mirror the broader baseline search so raw obsolete `4.4` / `4.5` labels cannot survive in production comments.
  - Winston: The architecture boundary is sound because the story remains comment-only and does not touch DAPR metadata, topic names, retry policy, or actor behavior.
  - Amelia: The implementation path is clear, with one caveat: the developer should not let broad numeric grep output in tests turn into unrelated cleanup.
  - Murat: Verification should include the same broad production grep used for inventory, plus diff review and release build; no new behavioral tests are required for this cleanup.
  - Paige: Documentation/history boundaries are explicit enough; the story should preserve historical artifacts and only normalize live source/config comments.
  - Sally: No UI accessibility or localization work is introduced; adopter-experience risk is traceability confusion, addressed by current-story or neutral feature wording.
- Changes applied:
  - Strengthened AC #2 to require both the exact Story 4.4/4.5 grep and the broader `Story 4\.[45]|4\.4|4\.5` production grep to return no `src/` matches.
  - Narrowed optional test-comment cleanup to exact stale Epic 4 story labels in `tests/Hexalith.EventStore.Server.Tests`.
  - Added Dev Notes warning that broad `tests/` grep includes unrelated task numbers, AC examples, and other-story references that must not be rewritten.
- Findings deferred:
  - No product-scope, architecture-policy, or cross-story decisions deferred. The exact replacement wording remains an implementation detail constrained by AC #3 and AC #4.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- Date/time: 2026-05-03T09:31:45+02:00
- Selected story key: `post-epic-4-r4a8-story-numbering-comments`
- Command/skill invocation used: `/bmad-advanced-elicitation post-epic-4-r4a8-story-numbering-comments`
- Batch 1 method names: Self-Consistency Validation; Red Team vs Blue Team; Failure Mode Analysis; Critique and Refine; Active Recall Testing
- Reshuffled Batch 2 method names: Occam's Razor Application; First Principles Analysis; 5 Whys Deep Dive; Comparative Analysis Matrix; Lessons Learned Extraction
- Findings summary:
  - The main risk is accidental behavior change while chasing a broad `4.4` / `4.5` grep gate, especially in YAML topology files or unrelated numeric/version text.
  - The story needed clearer handling for false positives, generated files, package/version metadata, and proof that source/config edits are comment-only.
  - The cleanup remains appropriately narrow; R4-A5 runtime proof and R4-A6 drain integrity behavior stay outside this story.
- Changes applied:
  - Required pre-edit classification of broad grep matches, including generated output, non-story numeric values, and false positives.
  - Clarified that production gates target stale story/comment references and that non-comment broad-regex matches must be documented rather than behaviorally changed.
  - Strengthened behavior-preservation rules for YAML indentation, keys, scalar values, ordering, generated files, package/version metadata, and DAPR topology.
  - Added verification expectations for `git diff --check` and explicit comment-only hunk review.
- Findings deferred:
  - Dev-story execution must decide exact replacement wording per file after inspecting current source comments.
  - Any remaining broad-regex false positive must be justified with exact path, line, and unchanged value during development rather than resolved by semantic edits.
- Final recommendation: `ready-for-dev`

## Review Findings

Code review run on 2026-05-03 against working-tree diff vs `HEAD`. Three review layers: Blind Hunter (adversarial, diff-only), Edge Case Hunter (boundary walk with project access), Acceptance Auditor (AC conformance). Findings:

- [x] [Review][Decision] AC #8 sprint bookkeeping overwritten by concurrent R9-A5 commits — Resolved with option (b): added a historical comment line for R4-A8 below `last_updated` at `sprint-status.yaml:33`, following the precedent of the existing R4-A6 comment. Preserves R9-A5's `last_updated` attribution and satisfies AC #8's intent without overwriting concurrent work.
- [x] [Review][Patch] Tautology in pubsub.yaml comment after Story 4.5 scrub [`src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml:102`] — Applied: removed trailing "for dead-letter routing".
- [x] [Review][Patch] Tautology in subscription-sample-counter.yaml comment after Story 4.5 scrub [`src/Hexalith.EventStore.AppHost/DaprComponents/subscription-sample-counter.yaml:5`] — Applied: rewrote to `# Dead-letter topic convention: deadletter.{tenant}.{domain}.events`.
- [x] [Review][Patch] AC traceability dropped in EventDrainOptionsTests XML doc [`tests/Hexalith.EventStore.Server.Tests/Configuration/EventDrainOptionsTests.cs:11-13`] — Applied: re-linked to `Story 4.2: EventDrainOptions unit tests (AC: #10).` (Configurable drain timing).
- [x] [Review][Defer] AggregateActor.cs XML doc still references orphaned "task 6.7" [`src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1305`] — deferred, pre-existing. After Story 4.5's `Story 4.5:` prefix was stripped, the surviving sentence "Dead-letter publication happens BEFORE SaveStateAsync (task 6.7)." references a task number from the now-defunct Story 4.5. R4-A8 was scoped to story-number labels; orphan task references are a follow-up cleanup.
- [x] [Review][Defer] Stale `Story 4.4`/`Story 4.5` references remain outside `src/` [`deploy/dapr/*.yaml`, `docs/guides/dapr-component-reference.md`] — deferred, out of R4-A8 scope. R4-A8 AC #2 explicitly scopes the grep gate to `src/`. Operator-facing deployment templates (`deploy/dapr/pubsub-kafka.yaml`, `pubsub-servicebus.yaml`, `pubsub-rabbitmq.yaml`, `resiliency.yaml`) and operator docs still carry obsolete story-number labels. Recommend a follow-up R4-A9 (or doc-team task) to extend the cleanup.

### Dismissed (recorded for transparency)

- Spec narrative still mentions "Story 4.4/4.5" inside this very story file — intentional historical reference (AC scopes to `src/`).
- `ResiliencyConfigurationTests.cs:27` not in diff — Debug Log explicitly classifies it as an intentionally unchanged false positive (assertion message, AC #6/Task 2.4 forbids assertion edits).
- HEAD `da59238` (story creation) vs `295a7e7` (dev start) — not contradictory.
- `sprint-status.yaml` `last_updated` historical comment text claims `in-progress -> review` while the row diff shows `ready-for-dev -> review` — narratively accurate; the in-progress state was an unrecorded intermediate.
- `subscription-sample-counter.yaml` retains `Story 4.3 AC #3` reference — confirmed correct, Story 4.3 still owns per-aggregate backpressure / per-subscription DL routing demo.
- `EventDrainRecoveryTests.cs:25-28` "Story 4.2 ... (AC: #1, #4, #5, #6, #9, #10, #12)" claimed invalid by Edge Case Hunter — false positive, Story 4.2 has 13 ACs and the referenced ones all map correctly.
- `predev-hardening-runs.log` shown as modified by `git status` but `git diff HEAD` returns zero diff — CRLF/file-mode artifact, not an R4-A8 concern.
- Telemetry tag XML docs reworded from `(Story 4.5)` to `used by dead-letter routing` allegedly narrows generic constants — verified via grep, the three constants (`TagExceptionType`, `TagFailureStage`, `TagDeadLetterTopic`) are used only by `DeadLetterPublisher`/`DeadLetterPublisherTests`, so the new wording is accurate.
