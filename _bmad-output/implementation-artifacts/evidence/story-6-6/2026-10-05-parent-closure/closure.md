# Story 6.6 build checkpoint

Story 6.6 remains `in-progress`. All eight M1–M8 tasks are open, the frozen
intent is unchanged, and V2/proof-dependent activation remains fenced. This
checkpoint records completed local repairs and the next dependency blocker;
it does not grant story acceptance or production readiness.

The [repair report](../verification-2026-10-05-v1-intake-and-compatibility.md)
records bounded V1 result admission, corrected payload/reservation ownership,
additive provenance with legacy refusal, the restored twelve-member drain-record
ABI and CI controls. Its final checkpoint passed 24 reader tests without skips,
all eleven timed source mutations and the warning-free Release solution build.
Earlier broad suites had no failures; Server retained 25 existing skips. Both
package fixtures passed against all fourteen standard packages. The explicit
private HotReload-disabled Development profile passed eleven live tests.

Seven specific deferred findings are now done in the
[ledger](../../../deferred-work.md): two duplicate reservation-accounting rows,
the fence-spoof row, the bounded factory-call source-scan row, two duplicate
metadata-admission/snapshot-control rows and the prior drain-record ABI row.
Their resolution entries retain the original finding and cite the proven scope.
Broader ingress, API/wire/fleet, consumer and production requirements remain open.

The parent inspected the repair changes and final controls, then verified all
377 final repair inventory entries and 250 loader inventory entries. The final
Release Server DLL differs from the earlier retained packaged DLL, so the
compiled-consumer gate was rerun against its exact bytes. It passed, with both
independently removed-member controls rejected. The executed final build hash is
`ae4768edb3e8d41f4a30775b96f8dfd55214000655bc3df1bd525b5690be0798`;
the separate passing packaged artifact remains
`5bc3ed0a97344f8c26330217c5c83445e53510fd1bd755ab2d0a6ba839c3501d`.
See [current ABI result](compiled-current-release/result.json),
[commands](commands.json) and [parent checks](checks.json). The deferred-work
checker and `git diff --check` passed; existing ledger advisories are retained.

The [six-scenario loader qualification](../dependency-loader-qualification-2026-10-05.md)
failed for the dedicated-context-plus-observation candidate. Ordinary undeclared
loads refused, but explicit managed/native loads performed owned effects before
host capability rejection. The [updated decision](../dependency-loader-decision.md)
names the next M1 input: an enforceable policy for explicit loads and nested
effects inside admitted catalog code, together with authoritative manifest/pin
inputs. Missing production profile, Dapr control/proof and fleet qualification
remain later acceptance requirements. The exact production-profile existence
check returned 1 because `deploy/dapr/production-profile.yaml` is absent.

No commit, staging, push, dependency update, registration migration, deployment
or global runtime change was performed. The canonical workflow baseline remains
`1329b35e52852952ecb2c94aabf100674e9691e3`; observed HEAD remains
`c447573fe4613fc3f9035dc4c5d001a89c0b5fbe`. Completion review was not entered
because the story tasks and acceptance matrix remain incomplete.
