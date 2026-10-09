# Current-workspace checks for dormant logical snapshot intake

At HEAD `07d1e23a6c5b06bbbb1fc8ddb5174cc3382d3d93`, the full solution built twice in Release with `-m:1 -warnaserror -p:UseHexalithProjectReferences=false`; both builds exited 0 with zero warnings/errors and unchanged root input sets. The second build followed concurrent Streams source edits and qualifies the later current-workspace bytes. The [sealed private snapshot packet](../dapr-logical-snapshot-2026-10-09/verification-2026-10-09-logical-snapshot.md) independently passed 66 snapshot, 16 Client model and 53 reconstruction controls with 16 killed mutations. Its 267 packet-file hashes and 20 owned-source hashes were rechecked against the sealed inventory without mismatch.

Sequential tests from the root Release assemblies gave these results:

| Command scope | Result | Input seal |
| --- | --- | --- |
| Server snapshot, reconstruction and replay classes | 158/158 passed | Eight concurrent, unrelated Streams source paths changed; executed DLLs stayed unchanged |
| Full Client suite, before and after the second build | 1,528/1,529 passed both times | Stable inputs and DLLs in both runs |
| Snapshot class after the second build | 66/66 passed | Stable inputs and DLLs |

The same Client failure both times is `Streams.DirectoryAtomicAppendTests.OutOfScopeCommittedWriteAcceptsOrdinalZero`: the test expected receipt status `Accepted` and received `Blocked`. It is outside this snapshot-owned diff and remains unresolved in concurrent Streams work. The first Server command's eight changed paths are enumerated in [summary.json](summary.json); the test process itself exited 0, while its input-seal wrapper returned 2. The later focused snapshot command exited 0 with a stable full-root input set. Exact commands, UTC times, complete before/after source and DLL hashes, and logs are retained beside the summary as compressed receipts.

The unstaged [snapshot-owned review diff](owned-review.diff.gz) is 3,584 lines with uncompressed SHA-256 `54657868bcd7b01eb8eaa018f2c7c364024cf48ca230193d66733676f31f885c`; tracked owned paths passed `git diff --check`. These checks qualify local candidate intake only. Same-save snapshot pair issuance, anchor-aware replay, checkpoint/rebase owners, HTTP response ownership, production catalogs/keys/profile and live topology/SDK qualification remain open. Story 6.6 and all M1–M8/O rows remain in progress.
