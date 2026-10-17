# P1R bulk-evidence owner decision — prepared, not approved

This packet prepares a separate owner disposition. The 2026-10-17 authorization permits Story 6.4 qualification and preparation of this decision; it does not approve P1R evidence ingestion, a scanner exemption, retirement, publication or prerequisite acceptance. No approval record is issued here.

## Exact review subject

The [scope manifest](p1r-owner-decision-scope.json), SHA-256 `59dc0f17416f980a5c2b071f659d959fb88c5fee8c744fb4d55430d837953efe`, identifies **77 unique files and 98 historical reported locations** by full path, file bytes/SHA-256, line number and line SHA-256. The two cohorts overlap: Source6 reported 89 locations in 77 files; the reconciliation audit reported nine locations in six of those same files. Every Source6 file and reported line still matches its retained historical hash. These are historical scanner outcomes, not a fresh native scanner result or a finding that all spans are inert.

The [historical landing inventory](p1r-historical-landing-inventory.json) records the 3,188 P1R evidence paths in external revision delta `3dde45104b4be4eab8408cbaa0ce89b04a5d9052` → `74b8a6711400fc2853acf6e63b9ea4c751078d58`, with their observed current file hashes. It does not claim those current hashes were measured at the historical commit. The broader [preservation capture](preservation-before.json) binds all 11,027 present P1R files (2,748,220,309 bytes), separately from 409 historical Story 6.4 evidence files. These inventories define review and preservation boundaries; neither is a directory-wide exception request.

The [current policy scope](p1r-current-policy-scope.json) binds the six existing scanner source files, fourteen sealed historical-file entries with 21 line hashes, and twelve accepted embedded executor-source hashes. The existing policy is preserved. Its technical controls do not establish an authenticated owner disposition for the bulk import.

## Decision to be made by the authorized owner

Record an authenticated, content-bound decision on retention and tracked ingestion of this exact subject, explicitly distinguishing the 77 flagged-file review scope, the 3,188-path historical landing, the complete preserved P1R inventory, and the existing scanner implementation. The decision must name the owner identity and role authority, issuance time, exact manifest and policy digests, reviewed classification/basis, and approved disposition per affected content category. A decision on one scope does not approve another.

The concrete proposed boundary is to **retain all historical bytes and permit only separately reviewed, content-bound recognition of the exact archived source fragments/lines named by the manifest**, while continuing canonical detection on the rest of each file and on changed or adjacent content. This proposal is pending owner/security assessment. It does not assert that any reported credential is usable or inert; no value has been printed, authenticated or tested. If that assessment cannot support recognition, record a separate external-retention/landing boundary with retained hashes and canonical detection guarantees. This task does not move, delete, redact, re-pin, convert or retire any evidence.

Before an approved boundary can support a gate, the exact owning tracked-ingestion scanner and negative controls must run against that boundary: changed file/source bytes, adjacent credential content, near-miss paths and unrecognized source must remain detected. A passing present-day scan cannot retrospectively approve the bulk landing. Any new path, file/hash, policy or scope requires a new reviewed decision. No blanket P1R directory exclusion, silent binary conversion or retirement approval is proposed.

The October 6 single-file approval applies only to `6-1-p1r-remediation/source-candidate.diff` at its sealed hash; it supplies no authority for this bulk subject. The [owning reconciliation handoff](../../6-1-p1r-canonical-reconciliation-20261016/evidence-ingestion-handoff.md) expressly requires an authorized disposition before bulk landing and preserves raw bytes and detection guarantees. Its 34-path source-only completion, six/nine historical evidence flags and later imported evidence remain distinct.

## Evidence and effect

The scope manifest hashes the original native Source6 failure, syntactic context audit, separate Source9 synthetic regression-source failure, reconciliation bundle audit, single-file exemption specification and owning reconciliation specification. It reports source/template context and hash integrity only. The separate Source9 failure is not added to the 98-location P1R decision scope.

Even a future valid decision changes no historical native result and grants no P1R acceptance, Story 6.1 readiness, NFR8 acceptance, CI seal, source freeze, release or maintenance cutover. Story 6.4 remains in progress until its own complete acceptance gates pass. The new native qualification outcome will be linked by the final qualification handoff without rewriting these historical findings.

## Fresh technical context from this pass

The [fresh scanner context](p1r-fresh-native-scanner-context.json) binds the actual `TrackedReusableContent_DoesNotContainUsableSecrets` XML witness inside the new [full Server Debug frame](../qualification-20261017-server-debug-full.json) at `77d9af18a54d92506e310fe5a45021a395e66f60`: **Pass**, 724.444 seconds. The full suite passed 5,792 cases with the exact 25 declared skips. All six preserved scanner source hashes match that frame's actual inputs; captured source, asset and runtime identities stayed unchanged during execution. This resolves the native scanner result for that specific candidate and policy. It neither changes either historical red cohort nor supplies the separate authenticated bulk-evidence owner disposition requested above.
