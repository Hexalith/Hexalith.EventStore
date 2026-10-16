# P1R canonical reconciliation and publication-readiness work package

The complete source reconciliation and guarded publication wiring are ready for
review in the isolated checkout
`/home/administrator/projects/hexalith/p1r-canonical-reconciliation-20261016`.
**P1R remains unusable; Story 6.1 remains blocked.** This package does not land or
freeze source, reserve/select a version, create authority/approvals, publish,
change acceptance/sprint state, update pins or initialize nested dependencies.

## Concrete reviewable delivery

- [Final reconciliation diff](reviewed-reconciliation.diff), SHA-256
  `006a4bfee5d3e3e9a0eeef8f4a2f23eadf81a567bf99ca9541f5e5517b84b603`,
  contains all 34 source paths, including untracked files. Its reverse apply
  check passes against the isolated checkout without staging.
- [Final source closure](reviewed-source-reconciliation.json) binds all 26
  hardening paths and eight additional authority/test paths to canonical main
  `a2016e88f4d0da9b953fe6f4394f8b8fd4e27f72`. Twenty-three hardening paths retain
  exact reviewed bytes. Three change: preparation CI adds pinned authority
  controls, the executor fixture retains canonical UUID credential generation,
  and the endpoint negatives now generate UUID credentials. The other canonical
  generated-credential test remains byte-identical. All four dated CI helpers
  retain their required exact hashes. The three new hardening Python files are
  present.
- The manifest verifies 2,102 canonical production/security/configuration files
  unchanged, including SDK/package/gitlink pins. Original hardening, recovery,
  enrollment and historical records are preserved rather than re-frozen as
  publication evidence.
- [Independent review and individual triage](independent-review.md) covers ten
  blind findings plus the parent scanner finding. Edge and verification reviewers
  returned no findings. The blind reviewer independently rechecked the authority,
  rerun and fixture fixes: all 17 authority controls and the focused endpoint
  control passed, with no new related defect. Historical audit-entrypoint/layout
  restrictions remain explicit; the four immutable helpers are CI dependencies,
  not new operational qualification drivers.

The final delivery check observed a later canonical test-helper-only fix.
[Refresh applicability](canonical-refresh-applicability.json) and
[review applicability](refreshed-review-applicability.json) prove all 34 delivered
bytes and every reviewed diff hunk unchanged; only the canonical-baseline
preamble changes. Both isolated checkouts now include the canonical fix. Prior
source bindings, diff/closure, analysis and verification are retained under
`pre-canonical-refresh-*`; fresh affected/full Python controls follow the refresh.

## Guarded workflow behavior

The release requires all three successful exact-source **push** proofs, refuses
bypass, and requires real future reservation/issue/owner inputs. An unprotected
job exposes the approved publisher's exact identity and hash for the current
run/attempt before production approval. The protected job refuses old-attempt
candidates before artifact lookup and rederives identical bytes before verify
and publish. Rerunning all jobs and obtaining new authority is mandatory for a
new attempt.

The pinned Builds engine validates the genuine identity-bound, version-bound,
expiring owner authority and consumes it once before the first package write.
Missing/mismatched/expired/replayed authority refuses. The supplemental replay
check ignores forged consumption-shaped comments from untrusted authors.
Existing protected production approval, enablement, credentials/OIDC, destination
checks, npm provenance/signatures, Release build and manifest gates remain.
Nothing was dispatched and no authority comment was written.

## Fresh verification

[Verification index](verification.json) retains literal argv, working directories,
exit codes, timings, logs and hashes. [Final source stability](refreshed-source-stability.json)
compares 6,518 source/dependency inputs in both primary and disposable layouts;
all bytes are stable. The final source manifest also binds the five approved
publisher contract files.

- All **266 Python tests** pass: preparation 35, published qualification 61,
  executor 104, isolation 30, hardening 19, authority 17. Optimized Python passes
  hardening 19 and authority 17 again.
- Focused .NET checks pass: packaging 64, snapshot 179, canonical secret/evidence
  controls 778, all without skips. The complete 34-path delivered-source scan
  passes, including untracked Python, helpers and the canonical scanner partial.
- Both Debug/source test-project builds pass with zero warnings/errors. Exact
  external root-declared catalogs/source paths are in their receipts; these
  local builds are not clean-checkout Release CI or published qualification.
- All **24 compiler enrollment evaluations** pass without restoring an unpublished
  package. They preserve finite historical enrollment; analysis-only 3.123.0
  remains excluded.
- A [fresh real-Docker smoke](refresh-live-assessment.json) passed a harmless Python
  command through the real pinned controller, with private process and outer
  container/volume cleanup. Actual persisted evidence is retained under
  [live-isolation-refresh](live-isolation-refresh/). No package lane ran.
- All 37 whitespace checks and Projects Story 6.1 production-authority validation
  pass. That validator is a scope check, not Story 6.1 acceptance/readiness.

The primary standalone preparation attempt refused missing real Builds
provenance; source compiler evaluation passed 21 profiles but refused three
Debug/source imports. Both attempts remain retained. A separate disposable
Projects umbrella used **only root-declared** EventStore/Builds clones with real
absorbed Git directories. Its copied 34 source files are identical; full 35
preparation and all 24 compiler evaluations pass. Its Git objects were copied
inside that workspace before the live container smoke; no external object
alternate or fabricated superproject proof was used.

The original implementation scanner completion was unobserved; that record is
retained without a pass. The parent's broad security/snapshot run completed
862 tests: 861 passed, with one actionable endpoint-fixture scan failure. After
UUID generation fixed that isolated source, the complete delivery scan and all
778 selected canonical secret/evidence controls passed. The broad tracked-file
enumeration passed in that earlier run; it was not rerun or weakened. The
attempted cancellation found the process already absent and sent no signal.
All failed/intermediate results remain distinct from final focused checks.

## Evidence-ingestion boundary and remaining gates

**Bulk evidence landing is still blocked.** A separate [actual canonical-reader
audit](refreshed-evidence-ingestion-audit.json) found nine flags in new retained review
text and registry metadata. [Exact findings and required disposition](evidence-ingestion-handoff.md)
preserve all bytes and scanner behavior. No exemption, retirement approval,
hash re-pin or hidden conversion was manufactured. Source-only landing and
externally retained evidence require an explicit hash-bound boundary; adding
these captures as tracked reusable text requires an authorized reviewed
disposition and a new real ingestion/CI pass.

[Fresh release analysis](release-analysis/release-readiness-analysis.json)
predicts minor 3.123.0 for observed canonical main after published 3.122.0;
3.122.1 remains a historical proposal. Neither is reserved or selected. Public
NuGet/GitHub checks and [authenticated OCI observations](release-analysis/authenticated-registry-observations.json)
currently show both examined versions absent. Earlier unauthenticated 401 results
remain retained; existing Docker credentials resolved that observation blocker,
but actual publisher authentication and all destination checks must be repeated.
At the refreshed observation, canonical CI was in progress while P1R/Commitlint passed; none authorizes this
uncommitted delivery.

[Final preservation](final-preservation.json) verifies all 201,238 starting
EventStore files and 5,799 Projects files unchanged, with both HEADs, branches,
indexes and workspace gitlinks unchanged. This includes the existing uncommitted
hardening package and Projects Story 6.1 draft. [Delivery manifest](delivery-manifest.json)
binds this new bundle separately.

The [publication-readiness handoff](publication-readiness-handoff.md) names the
exact evidence-disposition/landing boundary, actual version reanalysis and finite
enrollment, source freeze, exact-source CI, genuine authority, protected
publication, observed selection and fresh qualification work. Remaining path:
**authorized landing and exact-source CI → guarded publication → observed
successor selection and qualification → P1R acceptance → P0/P2 → P3 → architect
sign-off → P4 → independent READY → Story 6.1 implementation**.
