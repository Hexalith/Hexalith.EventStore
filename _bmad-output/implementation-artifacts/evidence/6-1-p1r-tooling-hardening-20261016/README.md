# Separate P1R tooling-hardening package

This package addresses the nine gaps recorded after completed source recovery.
It does not accept or qualify a successor. **P1R remains unusable; Story 6.1
remains blocked.** No commit, staging, push, publication, branch/gitlink movement,
dependency update, nested submodule initialization or prerequisite-state change
was performed. The existing Projects Story 6.1 draft is preserved.

The [work package spec](../../spec-6-1-p1r-tooling-hardening.md) is complete only
for this source-only hardening. The [publication-readiness handoff](publication-readiness-handoff.md)
gives the concrete canonical reconciliation, CI, authority, publication,
observed-successor selection and qualification gates still required.

## What is reviewable

- [Nine-gap coverage](nine-gap-coverage.json) maps every recorded gap to executed
  regression controls: image/create provenance; SDK launch/input binding;
  unrelated mutations; private processes and cleanup; tool identities; distinct
  ordered observations; remote TLS refusal; startup bounds; real caller
  finalization.
- [Reviewed source closure](reviewed-source-hardening.json) freezes 26 paths:
  seventeen unchanged recovered paths, six hardened paths and three new Python
  files. The original 23-path manifest and all recovery evidence are unchanged.
- [Independent review](independent-review.md) retains all twenty separately
  classified findings and their closure. Three context-free reviewers examined
  the diff and rechecked the fixes. No finding was deferred.
- [Final diff](review-v4.diff) and its [identity](review-v4-diff.json) include
  tracked and untracked tooling changes without staging them.
- [Delivery manifest](delivery-manifest.json) binds the complete retained bundle,
  spec and final source closure. This directory's `.gitattributes` preserves
  exact evidence bytes on later checkout.

## Fresh verification

[Final parent verification](parent-verification-v4/verification.json) passed all
249 tests: preparation 35, published qualification 61, published executor 104,
private isolation 30 and hardening 19. The nineteen hardening tests also pass
under optimized Python. Both repositories' whitespace checks, explicit checks
for all three new Python files, and Projects production-authority validation for
Story 6.1 pass. Each command retains literal argv, working directory, exit code,
log and hash. Before/after source bindings are equal. The exact driver body is
retained alongside its receipt.

The [post-review live smoke](parent-verification-v3/live-isolation-smoke.json)
ran only a harmless Python command through the real pinned tools and completed
both process and outer cleanup observations. It observed Docker client/server
29.8.2, SDK 10.0.401 and Python 3.12.12. The [applicability comparison](final-source-applicability.json)
confirms runtime, fixtures and other source inputs are unchanged after that
smoke; the sole later change strengthened coherent identity negatives in the
hardening tests. [Binary provenance](tool-binary-provenance.json) binds the
Docker/dotnet guards to those unchanged manifest-pinned image bytes.

The actual supervisor controls launch real registered child processes and
import their emitted identity/ownership/cleanup proof through the real
controller and finalizer. Compiled omissions/regressions prove sensitivity to
missing finalization, root/timing/version evidence, session rules and protected
PID checks. Semantic receipt mutations are rehashed before refusal checks.
Late-discovered owned processes are removed and accepted; previously absent
identities returning remain refused.

The required [Aspire baseline](aspire-baseline.json) built successfully, then
startup exited 134 on absent nested Tenants host projects before any resource
started. No nested dependencies were initialized. This environment limitation
does not replace the focused tooling results or grant operational qualification.

All implementation/review-fix attempts and earlier parent runs are retained.
The intermediate `parent-verification-final` attempt remains nonpassing because
the driver initially treated Git's difference exit code for new files as a
whitespace failure. [Correction evidence](verification-driver-correction.json)
shows empty diagnostic logs; the final driver accepts only the literal expected
0/1 codes with no diagnostics. Original driver bodies match their recorded
hashes and are retained. No old result was rewritten into a pass.

## Preservation and remaining work

[Final preservation](final-preservation.json) checks all 26,150 starting tracked
files, both HEADs, branches and exact index bytes, and the scope of new files.
Only the six intended tracked tooling/test/CI/documentation paths differ.
Projects tracked bytes, including its existing draft and final nine ledger
entries, are unchanged. Production source, historical evidence, recovery spec,
acceptance, dependency/runtime/backend pins and sprint state retain their
starting bytes.

Canonical source has not been reconciled or landed, exact-source CI has not been
obtained, publication authority is not wired, and no successor was published or
qualified. The handoff keeps the path explicit: **published successor
qualification and P1R acceptance → P0/P2 → P3 → architect sign-off → P4 →
independent READY → Story 6.1 implementation**.
