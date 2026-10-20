# Review-two SDK qualification

Ready for parent independent review. Owner spec remains in-progress; Folders adoption is parent-owned.
The approved baseline is `d4d05f871991ec2af305267f59dcefeae3f127ed`. Effective external HEADs are
recorded in `qualification.json` and `final-repository-state.json`; this phase performed no Git mutations.

| Gate | Result |
| --- | --- |
| Client focused | 215 passed, zero failures/skips |
| Client full | 1,756 passed, zero failures, two existing optional skips (non-acceptance) |
| Production dispatcher focused | 62 passed, zero failures/skips |
| Domain full default parallel | 1,056 passed, one process sweep-renewal fault assertion failure, zero skips |
| Domain full same binary, serialized | 1,057 passed, zero failures/skips |
| Expiry/scratch live first attempt | 34 passed, three COW negative-control assertion failures, zero skips |
| Affected final COW/process live | Nine passed, zero failures/skips |
| Expiry/scratch live accepted union | 37 unique cases; no all-37 single-pass claim |
| Existing batch/epoch live | Six and eight passed, zero failures/skips |
| Final distinct-process workers | Twelve passed, zero failures/skips, six exited-owner/recovery PID pairs |

`commands.json` retains exact commands, environments, results and runtime binary groups. The failed
complete attempts remain intact. The process fixture's ten-second clock advance coincides with its
operation timeout; scheduling is the inspected-code inference, not a reason to erase the failed gate.
The COW correction asserts the existing exact public unavailable exception before expiry. Inclusive
expiry still requires available/null after replacement clients/stores/coordinators.

`live-expiry-case-union.json` identifies every accepted case and its actual run. Only three Live test
files changed after the initial qualification: the negative-control assertion and raw process output.
`test-only-live-source-delta.json`, preserved earlier sources, both PDB audits and binary crosswalk
retain exact groups without repeating unaffected Client/Domain gates. All 3,751 final source/config/
fixture hashes are bound; runtime Client/Domain portable PDB checks match all 273/179 documents.

`final-process-pairs.json` and `final-process-workers.json` retain exact PIDs, commands, original pending
marker/manifest/fence evidence, raw Open epochs, Aborted sessions and physical private-key/receipt
absence. Each owner exits before a distinct recovery worker uses the same real fixture sidecar/state.
Production named dispatch proves preserved selection, two-owner ordinary delivery and a fresh rebuild.
`raw-persisted-observations.json` records physical TTL, Redis absence, COW and legacy compensation
observations. `cleanup-summary.json` records every exact disposable AppId namespace and zero leftovers.

Original SDK and review-one packets remain unchanged; the parent's historical audit also includes the
root Stage 7 packet. No Folders source, retained events, dependencies, pins, publication or deployment
was changed. Production Redis remains Resumable. Forced TransactionQualified remains a provider partial-
write/refusal diagnostic, with no atomicity/rollback claim. Parent review, Folders adoption, package/PD10
and the separately owned greater-than-10,000 identity prerequisite remain outside this handoff.
