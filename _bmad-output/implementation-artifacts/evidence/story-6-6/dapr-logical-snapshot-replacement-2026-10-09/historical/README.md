# Historical attempts and qualification limits

These are execution-time snapshots. Their successful checks retain the narrower
scope and bytes they ran against; later model, runtime or guard repairs do not
upgrade them. Compressed receipts/logs have exact original and archive SHA-256
inventories. No timeout or compiler failure is counted as a mutation kill.

| Attempt | Result at its bytes | Current qualification limit |
| --- | --- | --- |
| [Initial controls](initial-controls/summary.json) | 32 replacement cases, established classes; control-only, 28 commands, stable recorded root set | Before added retry, recovery and final serving controls |
| [Selection bug](control-only-selection-bug/summary.json) | 38 replacement cases and regressions, 42 commands, zero mutations, stable recorded root set | Runner filtering accidentally selected no mutations despite an intended guard run; explicit control-only evidence, not mutation qualification |
| [Earlier Debug](pre-independent-canonical-debug/summary.json) | 44 replacement cases, 22 kills, 86 commands, stable recorded root set | Before independent exact-desired canonical admission |
| [Pre-fence Debug](pre-final-serving-debug/summary.json) | 46 replacement cases, 23 kills, 90 commands, stable recorded root set | Before the final post-readback source/both-origin serving fence |
| [Pre-fence Release](pre-final-serving-release/summary.json) | Same 46 replacement cases/23 kills, 90 commands, stable recorded root set | Release/packages evidence at the same superseded pre-fence bytes |
| [Debug3 external drift](external-drift-debug3/summary.json) | 51 replacement cases plus 309 regressions, 25 kills, 96 stable copied/helper/DLL command sets | Final owned bytes, but four unrelated whole-root paths changed during execution |

Debug3's exact full-path before/after SHA-256 pairs are in its linked summary.
The four changed paths were
`src/Hexalith.EventStore.Client/Streams/AuthoritativeStreamReadDeadline.cs`,
`src/Hexalith.EventStore.Contracts/Security/IIdentityHistoryCustody.cs`,
`src/Hexalith.EventStore.Server/Security/RetainedIdentityHistorySourceReader.cs`
and its Server Security test. No paths were excluded from the private runtime
copy, and no external source was substituted or modified by this slice.

The subsequent final Debug4 likewise passed 360 controls and 25 kills with all
96 private command sets stable. Its whole-root set changed in three further
unrelated paths, whose exact hashes are retained in
[the final Debug receipt](../final-debug/summary.json). That attempt qualifies
the isolated copied-input and unchanged owned-byte scope. Repeating a full
Debug run indefinitely for unrelated concurrent edits would not establish a
meaningful whole-workspace guarantee; the parent separately owns the final
current-root build window and must disclose its actual input seal.

The earlier snapshot, anchor preparation and anchored continuation packets,
including their parent root addenda, remain separate historical evidence.
Their ownership/fence controls are rerun here where affected, without editing
or relabeling the earlier receipts.
