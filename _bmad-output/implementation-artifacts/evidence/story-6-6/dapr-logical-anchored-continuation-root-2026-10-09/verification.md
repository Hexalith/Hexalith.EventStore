# Current-root anchored continuation check — 2026-10-09

The [anchored continuation packet](../dapr-logical-anchored-continuation-2026-10-09/verification-2026-10-09-anchored-continuation.md) is sealed. I independently matched all 728 packet entries and all 29 owned source/document files to their recorded byte counts and SHA-256 hashes. The exact packet seal SHA-256 is `a38288645cac043c0eb3312195f24412d1b59df408b34270237cda72d262a08a`. The frozen intent and parent story status remain unchanged.

The full current-root command `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` passed at HEAD `07d1e23a6c5b06bbbb1fc8ddb5174cc3382d3d93` with zero warnings and errors. Its recorded root and initialized root-declared submodule input set stayed unchanged.

The actual root Server test assembly passed these Release methods with zero errors, failures, skips or not-run cases:

| Method | Cases | Elapsed | Source and DLL seal |
| --- | ---: | ---: | --- |
| `DaprLogicalAnchoredContinuationTests.ActualHeadSnapshotZeroTailCommitsTerminalReadbackAndExactRetry` | 1 | 17.291 s | unchanged |
| `DaprLogicalAnchoredContinuationTests.ActualAnchoredTailBeginPageReadbackAndRetry` | 2 | 120.523 s | unchanged |

Each exact command, output, timestamps, root input hashes and executed DLL hashes are in the compressed receipt/log pairs indexed by [summary.json](summary.json). The full assembly repeats source and loaded-file pin checks, explaining the longer nonempty-tail control. The independent packet lanes already passed 360 controls and killed 20/20 compiling mutations in both Debug/source and Release/packages; this root check is additional evidence on final bytes, not a replacement for production qualification.

`git diff --check` returned exit 2 because `src/Hexalith.EventStore.Contracts/Streams/SourcePublicationIndexState.cs:19` has a new blank line at EOF. That file is outside the packet's 29-file ownership. The scoped `git diff --check -- <paths from owned-files.json>` returned exit 0. Full outputs are retained in [global-diff-check.log](global-diff-check.log) and [owned-diff-check.log](owned-diff-check.log). No unrelated user file was changed.

This verifies the dormant local M4 slice only. Older-snapshot replacement, checkpoint/rebase, public routing and other M1–M8 work remain open. The approved AD-26 production profile was not found in the local root-declared Platform checkout or cached refs, as recorded in the [Platform search](../platform-production-profile-search-2026-10-08.md). No serving or activation authority is inferred.
