# Retained attempts and limits

Failed and superseded attempts are preserved independently of final current-byte lanes. None is counted as a qualified final control or behavioral mutation kill.

| Attempt | Result and reason |
| --- | --- |
| Draft source build | CA1822 on AnchorKey; changed the fixed key to a constant. Second narrow source build passed. |
| control1 | Test CS1061: ledger property ActualCount does not exist; corrected to Count. |
| control2 | Test build passed; ordinary reconstruction failed because nullable array-to-memory conversion made absent optional hash present/empty. Explicit nullable absence repaired the shared codec. |
| control3 | Root full-test assembly run capped/terminated at about 60 seconds, exit 143, startup only. Finite repeated origin/source/loaded-file pin verification was subsequently observed; this attempt proves neither a deadlock nor a passing control. Required fences were retained. |
| control4 | Narrow root build passed; isolated actual zero-tail passed in 18.258 seconds on draft bytes. This is a historical root assembly cross-check, not a final-byte gate. |
| control5 | Test CS0051: internal enum used in public theory signature; changed theory parameter to object and cast internally. |
| control6 | Four int/long Shouldly type mismatches and CA1062 in a public vector method; repaired test signatures/admission. |
| control7 | Eight operation methods passed (17 cases); codec method filter accidentally changed Server.Tests namespace and discovered Total: 0. Failed/unqualified control; corrected suffix-only namespace construction. |
| control8 | Control-only: 41 new cases plus established controls passed; no mutation qualification. |
| control9 | Control-only: 45 new cases plus established controls passed; no mutation qualification. |
| guard-anchor | Positive controls and first twelve compiling mutants passed/killed; initial covered-progress mutation had an ambiguous three-occurrence anchor. Narrowed to the actual genesis-record fragment. |
| redundant-clear-mutant | Positive controls and fifteen kills; decoded-selection-clearing survived correctly because hash fields slice the retained initial image. Removed redundant zero loop and mutant; retained direct image clearing/reference-drop controls. |
| missing-test-import | New entry controls failed compile on NSubstitute extension methods; added the existing test package namespace import. |
| pre-exact-type Debug | 360 positive controls and seventeen compiling kills; exact-entry-token-before-owner-work survived because the control accepted any exception and an uncontended gate hid waiting. Unqualified lane. Final controls require the anchored InvalidOperationException and synchronous refusal while the gate is deliberately held, releasing it before awaiting cleanup. |

The historical pre-entry Debug/Release packets each passed 279 controls and sixteen compiling mutants with unchanged dynamic copied/imported/DLL sets. They predate the entry token repair. Debug root drift was limited to external SourceNamespaceSnapshotReader and its test; Release root drift to external DaprSourcePublicationNamespaceSource and SourcePublicationNamespaceTests. Exact full before/after hashes remain in their readable summaries and compressed receipts. No whole-workspace qualification is inferred.

Minimal copied-assembly controls preserve every runtime source and configured substitute callback, actor durable/cache bytes and explicit read/save/Apply counters. The fixture clears only NSubstitute diagnostic received-call history at configured cache-discard points. This avoids unbounded diagnostic accumulation; it does not bypass runtime owner/source/codec/assembly-file checks.
