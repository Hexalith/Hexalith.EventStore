# Recovery focused review — 2026-10-03

All three context-free reviewers were launched before any result was handled. They reviewed the same focused recovery diff preserved in ../review-input.diff.gz; its uncompressed digest is 55eb075109e42d7ee6933edb1948b099265a42f78b115a393d9da438473399db. The active simplification permits zero findings, excludes unrelated committed work and authorizes focused corrections without another additive re-derivation loop. No layer was skipped.

## Blind hunter

- RB1: verify.py:1069 — cleanup intent persisted before deleting the original drain cannot retry when that authenticated source remains present; repeated drain-deletion-readback refusal strands cleanup.
- RB2: verify.py:1191 — live-drain restore persisted before owner advancement cannot retry after lost acknowledgement; repeated legacy-live-drain refusal strands claimed recovery and a new resume is fenced.

## Edge case hunter

- RE1: verify.py:1190–1206 — crash after live-drain creation before owner advancement leaves cleanup/invoke/claimed; the same-ordinal transition refuses and terminal completion has no legal claimed edge. This has the same root as RB2, but was adjudicated separately.

## Verification gap

- RV1: verify.py:702, :867, :922, :1496 — no behavioral assertion supplies a valid fixture-signed foreign-held-identity request. Removing only the identity comparison leaves the complete pre-fix suite passing; the foreign request sends with the guard absent. PD1 explicitly requires bounded request-binding evidence.
- RV2: verify.py:1172, :1431 — no bounded case supplies a different legacy owner. Removing only owner equality leaves the complete pre-fix suite passing; dead-letter-admin then claims a legacy-resume capsule. PD1 explicitly requires legacy owner-fence evidence.
- RV3 (Other finding): verify.py:1069, :1126–1128 — crash after cleanup intent but before drain deletion cannot recover; the existing crash hook fires after deletion. Same root as RB1, adjudicated separately.

## Parent verdicts and routes

| Finding | Verdict and specific evidence | Route |
| --- | --- | --- |
| RB1 | high: parent reran byte-only restart before deletion; intact capsule/control and authoritative source still present, retry refuses and remains cleanup. This blocks mandatory AC3/AC5 recovery. | bad_spec, focused owner repair |
| RB2 | high: parent injected crash after exact live-drain write; two same-ordinal retries refuse unchanged and fresh resume refuses resume_capacity_hold. The persisted intent cannot finish its original recovery. | bad_spec, focused owner repair |
| RE1 | high: verified independently as RB2, same persisted reachable state and stranded transition. | bad_spec, same root as RB2 |
| RV1 | medium: pre-verified gap confirmed by parent probe; signed foreign identity refuses unchanged in original, sends in mutant, mutant full suite passes. The runtime guard exists, but required bounded verification would miss its removal. | patch |
| RV2 | medium: pre-verified gap confirmed by parent probe; wrong owner refuses unchanged in original, claims in mutant, mutant full suite passes. The required legacy owner fence is unobserved. | patch |
| RV3 | high: parent reproduced the specific pre-delete boundary; authoritative remaining source is a valid reachable state, not corrupt evidence. | bad_spec, same root as RB1 |

Every finding received a verdict before grouping. Four groups remain: RB1/RV3, RB2/RE1, RV1, RV2. No frozen intent change, public surface, approval or separate coordination family is needed. The owner repair clarifies retained action idempotence and authenticates exact predecessor/readback; the two bounded guard cases add unchanged-state assertions. The user-authorized simplification overrides the default full-revert/loop-limit process for these focused corrections. Review loop iteration 12 remains historical. No new work is deferred.

The copied probe scripts and pre-fix observed output preserve reproducible evidence of the reviewed failures; their historical assertions are not current acceptance gates. Current post-fix regression evidence is recorded separately after correction.

## Post-fix parent checks

The implementation corrected both persisted legacy action paths in the existing owner and added the two required owner refusal cases, with send and repair coverage for the signed held mismatch. Capsule cleanup now verifies exact remaining source or authenticated absence and finishes deletion/readback. Restore binds the original owner/ordinal, intent address/hash/payload and capsule range/classification, then advances under the exact predecessor transaction; exact lost-ack repeats leave the final bytes/generation unchanged. Before/after deletion, before/after restore and after-advance restarts are asserted for both success and rejection classification; contradictory source/readback and altered intent are refused unchanged.

Parent full verify.py and acceptance.py both exited 0. mutations.py rejected all six approved temporary corruptions after a clean control; focused-regressions.py rejected both newly demonstrated owner-guard removals after a clean control. Actual output is retained in current-focused-regressions.json and the sibling current-verification/current-acceptance/current-mutations JSON files. git diff --check exited 0. All four original correction groups are fixed; the original finding evidence remains above unchanged.

## Targeted reviewer closure

The original blind reviewer independently reran both original crash injections against the corrected bytes with Store.restart(). RB1 now completes the same cleanup intent after a crash immediately before drain deletion; contradictory remaining drain refuses unchanged. RB2 now consumes the same owner/ordinal-bound restore after a crash immediately after the external write, advances exactly once, and returns byte-identically on a repeat; contradictory readback and wrong owner/ordinal refuse unchanged. The corrected legacy_cases group passed. The original reviewer marked RB1 and RB2 resolved. The two verification-gap groups are closed by the parent owning-failure outputs in current-focused-regressions.json. All six individual findings are resolved, with no new deferral.
