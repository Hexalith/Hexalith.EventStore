# Earlier snapshot attempts

These immutable execution-time receipts retain their original scope. None is
upgraded to final bytes or whole-workspace qualification.

| Attempt | Result and scope |
| --- | --- |
| First root Debug/source build | Failed CS8604 in the new origin capture; exact log retained. |
| Second root Debug/source build | Passed, zero warnings/errors, before strengthened controls; no dynamic input seal. |
| First root snapshot DLL run | 51 passed, errors/failed/skipped/not-run zero; historical assembly, no input seal. |
| control1 | Debug/source: 57 snapshot, 16 Client model, 53 reconstruction; stable recorded sets. |
| debug-guards1 | Same controls, 13 compiling mutants killed. Private/helper/DLL sets stable; three external Security test paths drifted in the root set. |
| control2 | Debug/source: 61/16/53; stable recorded sets. |
| debug-before-return-await | Debug/source: 64/16/53 and 13 compiling mutants; stable recorded sets, before constructed-owner final awaits. |
| debug-return-cleanup | Debug/source: 64/16/53 and two compiling cleanup mutants. Private/helper/DLL sets stable; six external Governance paths drifted in the root set. |

Each subdirectory has a readable summary listing exact drift and exclusions.
XZ files preserve complete original JSON/log bytes; archive-inventory.json
binds both decompressed and retained SHA-256. Disposable copies exclude the
listed external untracked Security paths and retain required external helper
inputs. These are private-copy controls, never whole-workspace regressions.

The formatter invocation against its absent Debug output failed before any edit;
the already available Release formatter was then used. No passing check is
claimed for that invocation.

The release-before-pair-cleanup attempt passed 64/16/53 controls and killed all 15 compiling mutants; its dynamic private/helper/DLL sets stayed stable. Four external Security paths drifted in the root set. It precedes the intermediate pair-return repair. Its own summary lists exact paths/hashes.
