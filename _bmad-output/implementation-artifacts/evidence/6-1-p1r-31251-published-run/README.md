# Published EventStore 3.125.1 qualification

The approved candidate is frozen at **3.125.1**, selected as the latest published
family during planning. **The fresh full execution and independent audit are
complete.** All 19 lanes and 163 cases executed: **4,795 of 4,804 main assertions
passed; nine failed**. Qualification and `p1r_usable` remain **false**.

The five nuget.org packages are Client, Contracts, DomainService, Server and
ServiceDefaults. Their selected tag is `v3.125.1`, production source is
`55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`, and tag Builds is
`4629ff2d3bd66376b29a553663c241d3a7b87853`. The execution Builds observation is
separate: `1dfc39e3a3e6ab1c8ce95138490b39d76e7feb2c`. Dapr 1.18.2 and the pinned
PostgreSQL v1 backend remain selected. Comparisons 3.70.1 and 3.110.0 grant no
rollback; `rollback=null` and AD-17 drain/freeze/forward recovery remain unchanged.

No owner acceptance, same-baseline conformance or architect support disposition
has been supplied. Approval to execute this story supplies none of those
decisions. Failed assertions and execution/import errors remain visible.

## Tooling and verification

The source change keeps verifier and exact tagged production/build closures
independent, checks physical source bytes before Git transforms, validates actual
Debug source references and loaded assemblies, and admits only the finite
enrolled versions. It also retains Reminder timing, original callback identity,
inner callback status and exact fresh-output ownership proofs. The concurrently
added optional pinned-tooling route is preserved alongside the selected
verifier/candidate closure route.

All **406** required merged-source test executions passed without skips or source
drift. The [parent suite and matrix audit](reconciliation/verification-ce6d5948928243f8a44978c7d1fef11a/parent-independent-audit.json)
matches discovered tests to all seven literal suite commands and logs, including
optimized isolation hardening and real Host/Domain/Probe evaluations. The
[source application receipt](reconciliation/source-application-0cff688f268642a98eb1bed19d28d1cd.json)
records 17 verified files applied only after matching the shared checkout's
preserved target hashes; HEAD and Git index did not change. The [owning-checkout audit](reconciliation/verification-5de2722d6a404bdd982be7787f2869cd/parent-independent-audit.json)
independently confirms all 406 checks passed again, without skips or source drift,
with the owning checkout's actual paths and initialized dependencies.

The [manual diff inspection record](reconciliation/parent-manual-diff-review-in-progress.json)
distinguishes manually read source/test/documentation hunks from the generated
evidence payloads checked by hashes and independent validators. The full final
diff, final task/acceptance audit and BMAD reviews are recorded separately.

## Retained attempts

| Attempt | Retained result | Disposition |
| --- | --- | --- |
| [Initial full run](initial-attempt/execution-bc3c2bca738541538fdcecdd40c3a6f3/execution.json) | 19 lanes, 163 cases; 4,790 assertions, 7 failed | Failed restore and Reminder measurements retained; host receipt finalization failed on directory ownership. |
| [Completed V3](completed-v3-rejected-import/execution-5d40d0c63beb4512adc644d086b5180e/execution.json) | 19 lanes, 163 cases; 4,808 assertions, 3 failed; restore 10/12, cleanup 7/7 | Full wrapper completed, but its fresh original audit rejected the bounded four-argument readiness GET grammar. No qualification. |
| [Fresh selected V4](final-attempt/canonical-v4-index.json) | 19 lanes, 163 cases; 4,804 assertions, nine failed; separate restore 10/12, cleanup 7/7 | Execution and independent audit completed; failures and pending decisions prevent qualification. |

The [merged-reader compatibility diagnostic](reconciliation/retained-producer-compatibility-proof.json)
subsequently reads all 21 V3 receipts with separate producer and reader closures,
reobserves all three package roles at 89/89 each, and preserves the three failed
post-upgrade restore assertions. This is a nonqualifying diagnostic. It does not
replace V3's original rejected audit or supply the fresh selected V4 results.

Every earlier failed test, helper, smoke, execution and audit attempt remains
retained. Copy manifests bind the [initial](reconciliation/initial-attempt-copy-manifest.json)
and [V3](reconciliation/completed-v3-rejected-import-copy-manifest.json) archives;
the [shared-copy manifest](reconciliation/shared-evidence-copy-0e241ee2e88c4480a4e94317bcfaa2d1.json)
verifies their copied bytes and preserves the owning checkout's separate active
verification. Ignored build outputs remain at their original physical paths.

## Physical retention and cleanup scope

Receipt validators reread absolute source, package, output and binary paths.
Keep all original `/tmp/p1r-31251-*` producer and tagged-source checkouts intact;
the copied receipt archives do not relocate those identities. The completed V4
producer is `/tmp/p1r-31251-verifier-import-c237110a4d04422f85ed2794536848d4`
and tagged source is
`/tmp/p1r-31251-tagged-source-import-da30571ce90c4296ba51b1d98b3c1775`.
Its frozen host binding is
`1782c66728aab9c6f99d810ca188b7a173aaaee34122f659e04f9e30fc06f1b5`.
The current working tooling and compatibility reader have separately retained
identities; they are not relabelled as that frozen producer.

Private owned-resource absence and repeated cleanup observations have bounded
scope. External host activity changed shared container identities during earlier
attempts; whole-host preservation observations remain false where measured.
No shared container was excluded, removed or altered to make those observations
pass. The final run measured thirteen shared containers before and nine after:
four removed, none added or changed. Against the original five-container
baseline, two were removed and six added. Both preservation observations remain
false; owned private cleanup does not establish whole-host preservation.

## Final technical result

The [parent independent raw audit](reconciliation/selected-final-parent-audit-9895165b155e48f2852f55b91c9bea35.json)
recomputed every case/check counter and failed-check ID, matched all 21 receipt
hashes to the [fresh private audit](final-attempt/audit-v2-8357499d6c4d4206b86a4a7decbfeb37/audit.json),
rederived the actual tagged Debug source-consumer proof and checked all private
and outer wrappers. Literal observe, run and audit commands exited zero; executor
and audit errors are empty. Package signatures and fresh independent package
roles passed (89 assertions per role). The full audit also passed all 38 real
process-control assertions. [The copy manifest](reconciliation/final-attempt-copy-manifest-c9205b1f4c0340c694245b1de5adce6b.json)
binds all 889 copied files to their retained physical producer paths.

| Measurement | Result |
| --- | --- |
| Source/package checkout comparison | 694/700; six failed checks in source snapshot-tail |
| Post-upgrade restore lane | 58/61; three failed checks |
| Reminder addition | 149/149 |
| Logical event evolution addition | 193/193 |
| Separate candidate restore / owned cleanup | 10/12 and 7/7 |
| Boundary measurement / strict prepare / strict validate | exits 0 / 2 / 2 |

The source snapshot-tail failure starts at the source-phase tenant-B seed's
first append: committed zero, accepted false, redacted diagnostic Stage=Processing.
Seeding stops after the two actual append checks fail; four subsequent append
assertions are unattempted. No missing attempts were counted as passes. See the
[minimal source response proof](final-attempt/source-tenant-b-failure-observation-v4.json).
The restore failures follow a refused Increment12 (sequence12, Stage=EventsStored)
and failed restart Assert13 (sequence12, Stage=Processing); see the
[minimal restore proof](final-attempt/restore-failure-observation-v4.json).
These observations do not identify a production cause.

The measured support boundary remains pending disposition. Strict preparation refused the missing measured owner disposition. The
subsequent validation also refused the incomplete invocation; both exited two. All
four owner decisions and same-baseline conformance remain pending. These are
technical measurements, not acceptance or a capable rollback.

A subsequent SDK compatibility regression reproduced missing tagged SDK selection
in both Debug and Release consumers of the concurrent pinned-tooling route. The
[red result](reconciliation/sdk-regression-red-6f9002e7ee5b4840be31d320e32e0a7d/command.json)
and [repair record](reconciliation/sdk-compatibility-repair-8cf36a7818714a88a130acf05b3e3523.json)
are retained. Both explicit routes now copy the source SDK pin before MSBuild;
the frozen V4 route already did so and was unchanged. Complete verification of
the current merged tooling is running separately from the frozen producer.

The [current merged-reader diagnostic](reconciliation/retained-producer-diagnostic-07a87c1c6a634dc4bbadbbbe1fca24bf/diagnostic.json)
also independently admits all 21 finalized V4 receipts with separate producer
and current-reader identities, reobserves the three package roles, and preserves
all nine main failures. It remains nonqualifying and does not replace the
fresh audit performed inside the frozen private environment.
