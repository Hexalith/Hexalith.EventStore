# Source State

The evidence was captured from baseline `0776785f494fcefc8ad933b5b17b9c8d5cbe0513` on branch `feat/story-4-5-append-durability-race-evidence` with the Story 4.5 test, fixture, documentation, report, and evidence changes present in the worktree.

## Production invariants inspected

The production append and conflict surfaces remain byte-for-byte unchanged from the baseline:

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`: `catch (InvalidOperationException)` at baseline lines 686, 842, 2624, 2971, and 3048.
- `src/Hexalith.EventStore.Server/Configuration/CommandConcurrencyOptions.cs`: `DefaultMaxPersistenceConflictRetries = 1` at baseline line 10. The live capture recorded one allocation attempt and zero retries, so the budget is classified **inconclusive / not exercised**; no reachability claim is generalized to another provider.
- `src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs`: nullable `ETag` member at baseline lines 8-9.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs`: append metadata is staged with `ETag = null` at baseline line 137.
- No path under `src/` or `.github/workflows/` differs from the baseline.

Re-run from the repository root:

```bash
git diff --name-only 0776785f494fcefc8ad933b5b17b9c8d5cbe0513 -- src .github/workflows
rg -n 'catch \(InvalidOperationException|MaxPersistenceConflictRetries|new AggregateMetadata|ETag' src/Hexalith.EventStore.Server/Actors/AggregateActor.cs src/Hexalith.EventStore.Server/Configuration/CommandConcurrencyOptions.cs src/Hexalith.EventStore.Server/Events/EventPersister.cs src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs
```

The first command is expected to print no paths.

## Evidence-relevant worktree inputs

The following SHA-256 values bind every changed harness, focused test, specification, architecture/public documentation, and report input needed to reconstruct and interpret this capture. Evidence-directory files are intentionally excluded from this table to avoid circular hashing; `evidence-sha256.txt` covers them instead.

| Repository-relative path | SHA-256 |
| --- | --- |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/ActorConcurrencyConflictTests.cs` | `9a47abc5a8facb9fceb20190ff543c5e8312a544190d09e0070187f3f2a21598` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceLiveSidecarTests.cs` | `12d22e6c4f7800506ecb289c7734f10513627491ef02c6f5c3c600c6e175e550` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/AppendDurabilityRaceClassifierTests.cs` | `ad0ef2071331ffdad3141eb7403101bcb62ffd3f2b80870fb6be10f82559a6ab` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/DaprStateErrorParserTests.cs` | `19baf343bd462d666568634d900609477179ef987d2434615b52f3a00b897d84` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs` | `0da109af79cb0ded1c9e7377c4140a561ef682f1cf23b7c0fbb6284b7401c216` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceControl.cs` | `8407099b345237653ca0c28d8b9b60b1e2b8b8e192d5bcf52cc7f20b76aa7a82` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceSession.cs` | `bbecaa742556cf82bd211e7bc8132b3b79109ce0a8d790b90e45a8a8a635e538` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/LiveSidecarGlobalPositionAllocator.cs` | `9874b680ad90be4b4f63ff75bfbe9baaba20c59a2b45626b93c074da64f0efa5` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/AppendDurabilityRaceClassifier.cs` | `2f5f2f8e4dbfab0e4713f9bae0cba0f78f7d58ad6b2941d5f8b81d76246a72af` |
| `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprStateErrorParser.cs` | `6f828114d5ca58914c118bf61bc4f3f762f203fd1a099ea3b242611805603cd4` |
| `_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md` | `ac3e732fa041ba42685f7c714c4456d82978c0e76d3197f718243099e0d3c936` |
| `_bmad-output/implementation-artifacts/4-5-append-durability-race-evidence.md` | `07ef6ac95496950ab28a13e8bd58be2e840a88de501b667c2388b312f52b7cea` |
| `_bmad-output/planning-artifacts/architecture.md` | `0b632a9193a1e776ab93c7d6720cca45eb2b99cd6261a8c965a589fe20c2455f` |
| `docs/ci.md` | `dd1b6ee75270c42850ee254fbbe1bf78212a3a865ed5bc4459234b282da22a8c` |
| `docs/concepts/architecture-overview.md` | `7a533f575819419320d1d2a00f6d06021581a560727e69aa61ab7439b40ac00e` |
| `docs/concepts/event-envelope.md` | `0bd4ecfc76b32b49bcf9a03ff993ad35e9679e42e1b972bebf7125a756c83e5f` |
| `docs/reference/problems/concurrency-conflict.md` | `d2af8a26a51b2d3f089dfc689cd7e73757a5ed88fcb196f04a939dd4991347f9` |
