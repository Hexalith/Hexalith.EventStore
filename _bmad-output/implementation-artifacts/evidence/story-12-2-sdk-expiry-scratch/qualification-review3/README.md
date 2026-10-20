# Review-three SDK qualification

Status: ready for parent independent review. The owner spec remains in-progress with all 39 SDK implementation/qualification tasks checked. This packet does not complete Folders consumer adoption.

The implementation preserves immutable source deadlines, settles interrupted private scratch through SDK recovery, keeps delayed legacy workers fenced after cleanup, and applies all review-three corrections. Bounded payload-free original legacy Aborted markers and fingerprint/digest fences intentionally survive payload cleanup. Fresh retained scratch uses a distinct deterministic scope-hash identity within the unchanged public component limit; exact-marker recovery and receipt reclamation inspect both known identities. Compatible readers must precede deadline-bearing writers; see the authoring guide for rollout and rollback constraints.

| Accepted group | Result |
| --- | --- |
| Client focused | 294 passed, zero failed/skipped |
| Client complete | 1,814 passed, zero failed; two optional isolated-endpoint skips |
| Production Domain focused | 64 passed, zero failed/skipped |
| Domain complete | 1,059 passed, zero failed/skipped; `-parallel none` |
| Real Redis review-three cuts | 5 passed, zero failed/skipped |
| Complete expiry/scratch Live class | 42 passed, zero failed/skipped |
| Existing read-model batch Live class | 6 passed, zero failed/skipped |
| Existing shared-epoch Live class | 8 passed, zero failed/skipped |
| Distinct owner/recovery workers | 12 passed, zero failed/skipped; six exited-owner/recovery pairs |

The accepted live union is 56 distinct cases; the five review-three cuts overlap the complete 42-case class. Client, Domain and Live builds have zero warnings/errors. All 34 changed source/guide files use LF. Final fixture namespaces contain zero remaining keys after their sidecars stop.

`qualification.json` is the machine-readable entry point. `commands.json` records exact commands, environments, timestamps, results and log/XML hashes. `accepted-command-source-groups.json` binds each command to its accepted freeze and binary group. The Client/Domain group retains its original freeze; a sole later Live diagnostic test change is fully described by `source-group-reuse.json`. All 3,760 other frozen files remain identical, and the final Live group freezes all 3,761 files without drift. Portable PDB documents, current accepted binary files and generated API XML members match the recorded groups; see `pdb-summary.json`, `pdb-commands.json`, `public-api-xml-audit.json` and `binary-group-crosswalk.json`.

`raw-persisted-observations.json`, `final-process-workers.json`, `final-process-pairs.json` and the raw `process-workers/` JSON/XML files preserve state and distinct PIDs. The workers share their fixture sidecar/state; no sidecar restart is claimed. `cleanup-summary.json` records namespace cleanup while preserving pre-existing namespaces.

The real-provider cuts distinguish accepted historical absent-key stale-CAS recreation from safe post-await settlement before compaction, and separately prove exact newer marker/row preservation despite provider version reuse. The forced TransactionQualified diagnostic deliberately sends a stale guarded participant to Redis: native rejection leaves a partial model and Completed physical marker, while SDK read-back remains Indeterminate on both the first call and retry. Exact newer guard and marker bytes, legacy retention and remaining deadline TTL are asserted. Production Redis remains Resumable; forced-profile evidence does not qualify atomicity or rollback.

All failed/interrupted intermediate attempts remain under `diagnostics/`, including the first failing Redis gate, the 41/42 forced-diagnostic failure and its provisional Conflict assertion. `diagnostic-results-index.json` and `diagnostics/README.md` identify them. Historical SDK packets are independently verified in `historical-packets-unchanged.json` without rewriting them. `positive-snapshot-reuse.json` records bounded positive reuse.

No Folders source, Git history/index, dependency/package pin, retained event, publication or deployment changed in this SDK phase. Parent review, consumer restoration/adoption, package/PD10 and separately owned >10,000-identity/performance work remain outside this handoff. `SHA256SUMS` seals every other packet file, including diagnostics and the exact owner-spec snapshot.
