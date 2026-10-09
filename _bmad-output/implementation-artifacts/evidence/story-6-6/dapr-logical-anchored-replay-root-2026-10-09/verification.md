# Current-workspace checks for dormant logical anchor preparation

At HEAD `07d1e23a6c5b06bbbb1fc8ddb5174cc3382d3d93`, the full solution
built in Release/packages mode with `-m:1 -warnaserror` and zero warnings or
errors before and after concurrent Streams edits. Both successful builds had
stable root input sets. The later build qualifies the current workspace bytes.
The [sealed private packet](../dapr-logical-anchored-replay-2026-10-09/verification-2026-10-09-anchor-preparation.md)
separately passed 191 controls and 18 compiling mutations in each dependency
mode, plus the established Release snapshot lane's 135 controls and 16
mutations. Its 473 retained packet files and 16 owned source/document files
were rehashed against their inventories without mismatch.

After the final full build, current Release assemblies passed 56/56 anchor,
119/119 snapshot/reconstruction and 1,568/1,568 full Client tests. All had zero
errors, failures, skips and not-run cases, and stable root input and executed
DLL sets. The final anchor and snapshot/reconstruction commands executed the
same 118-DLL Server-test directory image. The root Server-test project also
built with zero warnings/errors and stable inputs before the later full build.

Earlier root anchor commands each passed 56/56 against unchanged executed
DLLs, but their whole-root input seals caught concurrent Streams changes: the
first in `ISourcePublicationOperationAuthority.cs`,
`SourcePublicationIndexActor.cs` and `DaprSourcePublicationIndexStoreTests.cs`,
the second in `DaprSourcePublicationIndexStoreTests.cs` alone. A transient full
solution rebuild between them failed with `CS8620` at that Streams test's line
553 (`Task<SourcePublicationCut?>` versus `Task<SourcePublicationCut>`); its
input set was stable during that failed command. A later concurrent edit
resolved the compile error, and the subsequent full build and Client suite
passed. No Story 6.6 owned source changed during these root checks.

The [summary](summary.json) identifies every command, exit code, exact changed
path and before/after hash. Nine compressed receipt/log pairs beside it retain
the exact argv, UTC times, complete root and executed-DLL hashes, and output.
The unstaged [owned review diff](owned-review.diff.gz) is 3,949 lines with
uncompressed SHA-256
`e00ebc13ae5b2abc02f742857a16d4bb7785401e6ba6209e99e1cb8ee47a1553`;
tracked owned paths passed `git diff --check`. The broad parent diff and M1–M8
acceptance audit remain incomplete. This addendum qualifies dormant local
preparation, not anchored replay serving, live Dapr behavior or production
activation. The next local M4 dependency and missing production authority are
listed in the sealed packet; all parent tasks and O rows remain open.
