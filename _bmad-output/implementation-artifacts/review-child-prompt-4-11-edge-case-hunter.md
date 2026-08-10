Read `/home/administrator/projects/hexalith/eventstore/_bmad/render/bmad-build/eventstore-5ec6a32020fe/949c1652f308ba6a0e7e/review-prompts/edge-case-hunter.md` completely and follow it as your review instructions.

Review content:

The working-tree diff since baseline commit `5bcfdbc8b28ac2706053075cc4e71160ee029ad8` changes Story 4.11 as follows:

1. `_bmad-output/implementation-artifacts/sprint-status.yaml`: changes `last_updated` from `08-09-2026 10:18` to `08-09-2026` and changes `4-11-admission-state-machine-and-current-fence-enforcement` from `ready-for-dev` to `review`.

2. `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionStateTransitions.cs` is added. It defines `IdempotencyAdmissionStateTransitions.IsAllowed(from, to)` and permits: Reserved→Pending, Reserved→Recoverable, Pending→Recoverable, Pending→UnknownProviderOutcome, Pending→Terminal, Recoverable→Pending, Recoverable→UnknownProviderOutcome, Recoverable→Terminal, and UnknownProviderOutcome→Terminal. All other transitions are denied.

3. `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs`: Begin, Complete, and MarkRecovery now use the centralized transition matrix. `LoadRequiredAsync` now rejects missing or structurally invalid records before mutation.

4. `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionActorTests.cs`: adds theory coverage for approved and denied transition edges and a test proving a structurally corrupt record fails before state mutation.

5. `_bmad-output/implementation-artifacts/spec-4-11-admission-state-machine-and-current-fence-enforcement.md`: records the approved Story 4.11 intent, code map, acceptance criteria, verification commands, baseline commit, and completed tasks; status is `in-review`.

Do not invoke any skill. If the instruction file is unreadable, report that exact failure and stop. Return only the review result.
