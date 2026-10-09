# Historical and failed attempts

These records preserve their actual draft-byte scope. Failed mutation definitions
and timeouts are not behavioral kills or final-byte qualification.

| Attempt | Actual result and retained limit |
| --- | --- |
| [Draft root/focused checks](draft-root/archive-inventory.json) | Root Debug/source Server test builds reported zero warnings/errors. Initial 18, expanded 45 and eight targeted controls passed at their respective draft bytes. Exact commands/transcripts remain historical checks. |
| [First disposable Debug](missing-vector-copy/archive-inventory.json) | 54 anchor and 16 Client-model controls passed. Ten existing snapshot controls failed because the copy omitted older snapshot vectors. The copy inventory was repaired; no runtime refusal was weakened. |
| [Second disposable Debug](noncompiling-mutation/archive-inventory.json) | 55 anchor, 16 Client model, 66 snapshot and 53 reconstruction controls passed; ten mutations were killed. `paired-witness-stage` failed compilation with CA2007 because its replacement await lacked ConfigureAwait(false). This was an invalid definition, not survival/kill; the replacement now compiles. |
| [Third disposable Debug](unbounded-liveness-mutation/archive-inventory.json) | The same 190 positives passed and seventeen mutations were killed. The last early-decision control blocked after guard removal and timed out at its remaining 56-second deadline. The old runner lost partial timeout stdout; the exact command/trace and preceding receipts remain. This attempt is unqualified; timeout is not a kill. |

The repaired early-decision control has a finite two-second observation, always
releases the SDK read barrier and awaits cleanup, then asserts that refusal
occurred before release. The final mutation removes only decision completion.
Both final configurations compile and fail
`refusedBeforeReadRelease.ShouldBeTrue()` with false after cleanup finishes.
The new runner retains partial timeout output with exit 124 and refuses to qualify
such a lane. Earlier successful checks are not promoted to final-byte or
full-workspace evidence.
