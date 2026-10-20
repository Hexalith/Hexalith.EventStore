# Exact P1R 3.125.1 finite tooling enrollment

This evidence records the bounded enrollment repair approved by the
[Projects prerequisite spec](../../../../../../_bmad-output/implementation-artifacts/spec-6-4-prerequisite-p1r-31251-enrollment.md).
It grants no published package qualification, runtime support, P1R acceptance or
Story 6.4 progress. All entry/acceptance gates remain pending as recorded in the
[new draft 3.125.1 handoff](../../../../../../_bmad-output/implementation-artifacts/spec-6-1-p1r-31251-published-qualification-handoff.md).
The earlier 3.125.0 handoff is unchanged.

## Changes and exercised boundaries

[Exact EventStore diff](enrollment.diff) and [changed-file identities](changed-file-identities.json)
bind the nine modified tooling/project/test/README paths. [Projects handoff diff](projects-handoff.diff)
and [handoff audit](handoff-audit.json) bind the new draft, its five actual archive
identities, separate Builds coordinates and explicit owner/graph/binding holds.
No dependency pin, Git ref/checkout/gitlink, production source, generated output,
acceptance, Stack or sprint artifact was edited.

Only exact 3.125.1 is added to the existing compiler, seed, retained fixture
source, strict witness and four final isolation attachment/import boundaries.
[Coverage inventory](coverage.json) records the 22 inherited 3.125.1 strict
snapshot/writer/provider/isolation/timing/cleanup/recovery tests. The existing
import tests exercise all changed qualification boundaries, so the conditional
third test-file edit was unnecessary. Positive seed controls verify that the executor requests nine events, invokes
the manual snapshot API and requests three tail appends for selected
package/source writers. These are synthetic controls of the call sequence.
Missing/substituted retained proof stays nonpassing; neither unit fixtures nor
HostBuilt checks supply provider qualification.

Both 3.125.0-to-3.125.1 and 3.125.1-to-3.125.0 seed substitutions refuse before
provider access, fixture reset or writer start. Unenrolled 3.125.2 and 3.126.0
receive no successor compiler paths and genuine seeds refuse. Host, Domain and
Probe are evaluated for both enrolled and both unenrolled versions (12 real
MSBuild evaluations). Separate real source HostBuilt executions inspect Host and
Domain registrations and retain missing-enrollment negative controls.

## Verification

All five required suites pass in the original edited EventStore checkout:

| Suite | Tests | Result |
| --- | ---: | --- |
| `test_p1r_snapshot_registration.py` | 2 | Pass |
| `test_p1r_published_executor.py` | 151 | Pass |
| `test_p1r_published_qualification.py` | 62 | Pass |
| `test_p1r_isolation.py` | 31 | Pass |
| `test_p1r_isolation_hardening.py` | 24 | Pass |

Initial complete verification: **270 tests passed**. [Final commands/results](final/commands-results.json)
and [summary](verification-summary.json) retain exact arguments, environment,
timestamps, exit codes and log paths. Each suite used:

```sh
python3 -m unittest discover -s tools/tests -p NAME
```

The final retry set `TMPDIR=/dev/shm/p1r-31251-verification-w90ywzqf/tmp` and
`PYTHONDONTWRITEBYTECODE=1` to keep temporary builds off the full filesystem.
Sources and test semantics were unchanged; no source-view copy or alternate
checkout was used. Source Debug HostBuilt checks and package project evaluation
execute no published operational qualification.

The first executor run had six ENOSPC errors, and the first real registration
builds also failed from storage exhaustion. The initial runner then failed to
retain the hardening child's output; no pass is inferred from that attempt.
[Initial command receipts](commands-results.json), native failure logs and
[initial disposition](initial-attempt-disposition.json) remain retained. Only
parent-owned prior-turn scratch was relocated with byte verification; its
[relocation receipt](prior-scratch-relocation.json) records source/destination and
hashes. The passing retry does not erase any failure.

## Required Aspire baseline

Before code edits, `aspire run --non-interactive --detach --apphost
src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json`
built successfully with zero warnings/errors, then failed (CLI exit 2, AppHost
exit 134): the AppHost requires absent nested Tenants host projects before
resources can start. Exact-target `aspire describe` confirmed no AppHost
running. [Baseline receipt](aspire-baseline.json) retains the exact commands,
missing paths and blocker separately from passing tooling verification. No
nested submodule was initialized and no AppHost preflight was weakened.

## Preservation and remaining execution gates

[Before inventory](preservation-before.json) and [result](preservation-result.json)
rehash all 22,982 protected EventStore and 5,919 protected Projects tracked paths:
zero changed/missing. HEAD, five-commit history and gitlinks are unchanged in
both repositories. The six admission/project files normalize exactly to their
baseline bytes after removing only exact 3.125.1 membership; all older branches
and selected-writer equality are therefore preserved. The prior README remains
an unchanged byte prefix. [Diff checks](diff-check.json) pass in both repositories.

The full `validate_execution_inputs` and `observe_inputs` function bodies are
unchanged and hash-bound in preservation-result.json. The new 3.125.1 regression
proves an edited tooling HEAD differing from the selected released source/tag
refuses before consumers. Local HEAD remains
`47e234ccb535a71e7dd4a039fd0e2281083083ae`; released v3.125.1 is
`55432c50d4b6f1666b4bd6d4a60bfc263b2d0e0b`. The tooling edits cannot inherit
that release's immutable proof.

The draft records observed package-build checkout Builds separately from tag,
CI and release-execution identities. Actual archive construction/restore graph
verification, named exact-tuple ownership, restored/locked/loaded graph agreement
and an approved tooling/source binding strategy are required before any
operational execution. Keep
rollback=null, AD-17 drain/freeze/forward recovery, four-role acceptance,
same-baseline conformance, P1R usability and Story 6.4 blocked. This repair
changes no production snapshot policy and qualifies no package.

`manifest.json` hashes every retained file except itself. Local `.gitattributes`
uses `-text` so these exact evidence bytes survive checkout unchanged.


## Review fixes and reproducible preservation

The scoped registration rerun passed both tests; [its exact command and timing](review-fixes/commands-results.json)
and [native output](review-fixes/test_p1r_snapshot_registration.log) are retained
separately from every initial ENOSPC attempt and prior 270-test receipt. The 12
MSBuild evaluations now inspect actual `Content` items as well as `Compile`:
Host copies the evolution manifest only when enrolled, Domain retains its
existing unconditional `PreserveNewest` content behavior, and Probe includes no
fixture manifest. Existing real source HostBuilt controls are unchanged. No
other suite was rerun for these review fixes.

The exact publication source receipt is retained in Projects planning evidence
at its original SHA-256; the supplemental link identifies the observed
`release_31251_job_log` package-build checkout of Builds
`4629ff2d3bd66376b29a553663c241d3a7b87853`. The updated handoff links the
qualification contract, executed case inventory and historical support boundary;
the historical table does not qualify 3.125.1. Actual archive/restore graph
agreement, owner selection and tooling/source binding remain unmeasured or
unapproved. Published qualification and Story 6.4 remain blocked.

[Native Aspire logs](aspire-baseline.json) are retained byte for byte under
`native/`, including hashes and their first/last stamped native records. The
original outer `run` and `describe` invocation timestamps are explicitly
unrecorded. Native timestamps are preserved as written, with no inferred time
zone or invented outer command bounds.

[Read-only preservation procedure](verify-preservation.py) reproduces the
original tracked-file selection, exact six-file normalization, full immutable
guard body comparison, HEAD/branch/history/gitlink checks and post-verification
timing barrier. It writes only JSON to stdout. From the EventStore root, run:

```sh
python3 _bmad-output/implementation-artifacts/evidence/6-4-p1r-31251-enrollment/verify-preservation.py \
  --repository . --workspace ../.. \
  --baseline _bmad-output/implementation-artifacts/evidence/6-4-p1r-31251-enrollment/preservation-before.json \
  --verification-receipts _bmad-output/implementation-artifacts/evidence/6-4-p1r-31251-enrollment/final/commands-results.json \
  --verification-receipts _bmad-output/implementation-artifacts/evidence/6-4-p1r-31251-enrollment/review-fixes/commands-results.json
```

[Exact retained invocation/result](review-fixes/preservation-command.json)
records this post-run audit. To include a later verification, add another
`--verification-receipts PATH` argument, retain the new stdout and refresh the
mutable manifest. `review-0/` preserves previous derived observations and index
bytes; original native receipts, raw publication observations and failed tests
remain unchanged. Scoped `.gitignore` exceptions make required evidence
Git-discoverable. `git-discoverability.json` checks every member of both current
evidence manifests against Git's normal discovery rules, without force-adding.
