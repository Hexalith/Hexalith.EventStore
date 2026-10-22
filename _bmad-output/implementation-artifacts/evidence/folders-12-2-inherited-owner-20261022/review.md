# Independent review and disposition

The workflow's context-free blind reviewer examined 39.839 kB and used
N = min(floor(sqrt(39.839) + 1), 10) = 7. One review layer was configured;
no additional layers were skipped. The reviewer was read-only and reran no tests.

| Finding | Verdict and disposition | Evidence |
| --- | --- | --- |
| Proof age alone does not establish copy-on-write lineage after migration from replacement generations. | High; patched. | `client-migration-red.*` reproduces both paged and inline acceptance with an absent owner view. Paged evidence now requires an epoch at or after the inherited anchor and no later than the selected generation. Inline evidence has no epoch and requires an anchor of zero. A positive nonzero-anchor regression preserves provable migration. |
| Buffered `StageAsync` with copy-on-write can fail to preserve untouched rows in later sparse generations. | Medium; deferred preexisting staging defect. | `StageAsync` uses the direct-generation batch path; streamed staging uses `BuildCopyOnWriteBatchAsync` and creates pointers. This defect also loses the row before this replay correction and cannot be diagnosed from the retained owner identity alone: that proof does not name domain output keys or the original staging mode. The production named streamed-output path used by Folders is qualified here. No staging protocol or historical metadata was changed. |
| Abort that captured A retains A in the current capture map and bypasses the fix. | False; rejected and withdrawn by reviewer. | `AbortAsync` clears both capture forms. Expanded `client-abort-capture.*` executes abort with A omitted and included, asserts empty map/null chunks and successful original replay. |
| Removing the entire inherited checkpoint can make a previously accepted sequence one appear to be a new owner. | Medium; deferred preexisting state-integrity gap. | With the checkpoint itself absent and no current capture/receipt, the existing continuity rule sees an unknown owner. There is no remaining source reference to distinguish arbitrary checkpoint loss from lawful first delivery. This correction refuses a retained checkpoint with missing identity reference, missing/retired inventory, missing node, corrupt node or wrong source; it does not claim recovery from arbitrary deletion of the entire checkpoint. A separate authoritative source inventory/recovery decision is needed. |
| Empty evolution registry does not exercise an actual upcaster. | Low; rejected as additional coverage outside this duplicate correction. | The new SDK regression describes registry presence only. The retained Folders tiny reproduction additionally registers the real `FolderCreated` type and passes a Web JSON round trip, without an upcaster. Existing versioned-dispatch tests run in the broad DomainService suite. No actual-upcaster coverage is claimed for these new cases. |
| Forcing copy-on-write removes the old replacement-generation live lane. | Medium; patched. | Handlers now default to replacement mode; the original three-process cases remain. Separate four-process cases enable inheritance explicitly. Final qualification executes both modes. |
| Duplicated count/owner cases overwrite evidence filenames. | Medium; patched. | Every invocation now receives a tenant-specific evidence subdirectory. The existing count/owner theory remains a three-process replacement case; the new 10,001 case is a four-process inheritance case. |

The reviewer inspected the final anchor and test-mode changes and reported no
remaining introduced defects. It withdrew the abort claim and agreed that the
preexisting staging and whole-checkpoint-loss issues can be tracked separately.
