# Source State

The evidence was captured from baseline `0776785f494fcefc8ad933b5b17b9c8d5cbe0513` with the Story 4.5
test, fixture, documentation, report, and evidence changes present in the worktree. The
`2026-08-25` UTC loop-4 re-capture supersedes every earlier capture; these hashes are the worktree
inputs of that run.

## Production invariants inspected

The production append and conflict surfaces remain byte-for-byte unchanged from the baseline:

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`: `catch (InvalidOperationException)` at baseline lines 686, 842, 2624, 2971, and 3048.
- `src/Hexalith.EventStore.Server/Configuration/CommandConcurrencyOptions.cs`: `DefaultMaxPersistenceConflictRetries = 1` at baseline line 10. The live capture recorded one allocation attempt and zero retries, so the budget is classified **inconclusive / not exercised**; no reachability claim is generalized to another provider.
- `src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs`: nullable `ETag` member at baseline lines 8-9.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`: append metadata is staged with `ETag = null` at baseline line 137.
- No Story 4.5 change touches `src/` or `.github/`.

`main` has advanced through Stories 3.13-3.15 and 4.8-4.15 since the baseline, so a plain
baseline-to-`HEAD` diff now reports those unrelated stories and is no longer a Story 4.5 gate.
`commands.md` carries `story_4_5_ac6`, which **derives** the candidate commits from `git log` rather
than a hand-maintained SHA list, and also inspects untracked files. Two of those commits are shared
with another story and are declared there with the owner of their production change: `86308550`
carries Story 4.4's `src/` implementation ("recover committed events whose publication was never
scheduled"), and `ba0c367e` carries Story 3.14's Hexalith.Builds SHA rotation in
`.github/workflows/release.yml` — it touched the Story 4.5 spec only because that is where the
loop-4 review findings were written. An undeclared `src/`/`.github/` change fails the gate.

Re-run from the repository root:

```bash
# see commands.md for story_4_5_ac6
git diff --name-only HEAD -- src .github
git status --porcelain --untracked-files=all -- src .github
rg -n 'catch \(InvalidOperationException|MaxPersistenceConflictRetries|new AggregateMetadata|ETag' src/Hexalith.EventStore.Server/Actors/AggregateActor.cs src/Hexalith.EventStore.Server/Configuration/CommandConcurrencyOptions.cs src/Hexalith.EventStore.Server/Events/EventPersister.cs src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs
```

Both of the first two commands are expected to print no paths.

## Evidence-relevant worktree inputs

The following SHA-256 values bind every changed harness, focused test, specification,
architecture/public documentation, and report input needed to reconstruct and interpret this
capture. Evidence-directory files are intentionally excluded from this table to avoid circular
hashing; `evidence-sha256.txt` covers them instead.

`validate-evidence.py` hashes these paths in the **worktree** and enforces a required-path floor, so
omitting a row cannot loosen the binding. It follows that any later edit to a listed file
invalidates the packet until it is re-sealed, and that this table binds the spec and the report --
the two files each review loop must write to. Seal last. No CI step runs the validator, so that
decay is silent. Both facts are carried on the deferred-work ledger.

| Repository-relative path | SHA-256 |
| --- | --- |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorConcurrencyConflictTests.cs` | `3b6c79975faef73691f2366b90d77f6a4a71f92636e3165c5cb790488cc8042a` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs` | `958cb991439aa0e110eae9adf65ed15e6649aef60750131c77371c0c35fa1e27` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceClassifierTests.cs` | `b7b6d6a0e2341b091e5276685948f24f3dc38ad19b36edee37cf8e106b13b2fd` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityFinalShapeClassifierTests.cs` | `895a7cfd44bc27f23c0f667103f363aae6548b37e6a779982496b315eb154256` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/DaprStateErrorParserTests.cs` | `fff9abe8e3dd75def87b6936b32145078fcf5e32ca36a686aaf401a098619d29` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/StateStoreComponentCanonicalizationTests.cs` | `7506c075facf74d4128d77d1d81ad4ddd8966d5b9e30dee514ebf3abc608381e` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/Story45MutationSwitchTests.cs` | `6b1490047e7155925223a6b4d31babb1b2588c7dfb4b692f2901fe74ee7015bf` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs` | `28a89849a864014f4e18ab0bd791c4e905cc662799c8a58fce7e9627762dde9a` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceControl.cs` | `8407099b345237653ca0c28d8b9b60b1e2b8b8e192d5bcf52cc7f20b76aa7a82` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceSession.cs` | `bbecaa742556cf82bd211e7bc8132b3b79109ce0a8d790b90e45a8a8a635e538` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveSidecarGlobalPositionAllocator.cs` | `9874b680ad90be4b4f63ff75bfbe9baaba20c59a2b45626b93c074da64f0efa5` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceClassifier.cs` | `d8839b99faa7229ab31e622af6678ce292dbc8f0bd54fa1dc5bf4976c666532e` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityFinalShapeClassifier.cs` | `4421179897d90d701febf4a989dd31726f04ff374c176e279a5dcc0ccf5e6971` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprStateErrorParser.cs` | `6f828114d5ca58914c118bf61bc4f3f762f203fd1a099ea3b242611805603cd4` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Story45MutationSwitch.cs` | `d1687564734601582497825a3c84ce434f81754b909470ae877b2efc16e1def0` |
| `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md` | `42e8d3ac26f896f026175d032011f893b7ca61c596f5ccf01960a4a6c4ae06b0` |
| `_bmad-output/implementation-artifacts/4-5-append-durability-race-evidence.md` | `db9fcba46bf4402383a988d9dd9cd14cac253ab6057b1ab0399afa97bf9e0906` |
| `_bmad-output/planning-artifacts/architecture.md` | `9a20ba5c6860f124ca52a8801e531132a96dd0a761856fdc4684390d848f4101` |
| `docs/ci.md` | `c027bd132efa296edbc106738fc22558b51a126656c16d66d562357d55fc4571` |
| `docs/concepts/architecture-overview.md` | `0a47913a3f491276c962bd97f7271039ed4fc38aec168529efb776383a9a7c94` |
| `docs/concepts/event-envelope.md` | `0bd4ecfc76b32b49bcf9a03ff993ad35e9679e42e1b972bebf7125a756c83e5f` |
| `docs/reference/problems/concurrency-conflict.md` | `d2af8a26a51b2d3f089dfc689cd7e73757a5ed88fcb196f04a939dd4991347f9` |
