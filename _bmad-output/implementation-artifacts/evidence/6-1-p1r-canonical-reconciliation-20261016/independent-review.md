# Independent review and individual triage

Three context-free reviewers examined `review-v1.diff`, SHA-256
`1e42232b1cdeb7637b2cd628a0822ddd527bdfde12d263955f11934665231c22`,
429,371 bytes. The blind reviewer used the required ten-finding floor. The edge
reviewer returned `[]`; the verification-gap reviewer returned “No verification
gaps found,” after tracing the real consumer/manual-snapshot evidence driver.
Every substantive blind finding receives its own verdict below before grouping.

| ID | Finding | Verdict and evidence | Route |
| --- | --- | --- | --- |
| B1 | Any commenter can forge replay-shaped JSON. | **medium**. New helper checked consumption schema/digest without checking issuer. Reviewer reproduced a valid authority blocked by an untrusted comment. | Direct patch: authenticate the GitHub Actions issuer for the supplemental cross-comment replay check; regress forged-comment and real replay behavior. |
| B2 | Host preparation rejects private-runner interpreter identity. | **false** as a defect in the supported flow. Exact interpreter equality is an intentional qualification boundary. The recovered publication handoff explicitly wraps execution, measure-boundary, prepare and validation in the same pinned runtime. Ordinary host preparation is deliberately refused. | Retain exact identity; handoff restates mandatory wrapper usage. |
| B3 | Linked-worktree Git metadata may lie outside the mounted workspace. | **low**, pre-existing in the unchanged hardening runtime. Such a checkout fails Git/source binding rather than qualifying. This task uses an ordinary independent checkout and a real disposable umbrella. Supporting external Git metadata would add a new mount/trust boundary. | Record the unsupported layout and require a self-contained ordinary checkout for later qualification; no speculative mount expansion. |
| B4 | Failed-jobs-only rerun looks for a candidate from a new attempt. | **low**, real fail-closed operator experience. An old candidate must never be reused. Artifact lookup fails safely, but needs an explicit rerun instruction before lookup. | Direct patch: carry candidate attempt output and explain mismatched-attempt refusal; require rerun-all and fresh authority. |
| B5 | Earliest constructor allocations precede initialization cleanup. | **low**, pre-existing. At the cited point no Docker mutation has occurred; failures can leave only an empty output/scratch directory. A partial-state cleanup redesign adds branches for an uncommon local allocation failure. | Reject added runtime complexity here; retain as a bounded local failure limitation, never usable evidence. |
| B6 | A packet still needs original absolute receipt/binary paths. | **low**, retention documentation gap. The importer deliberately rereads exact physical source/input/execution files and binaries; relocation is outside that evidence identity. No portable-packet claim is made. | Clarify external retention requirements in the new handoff. Preserve invocation trees at original absolute paths. |
| B7 | Historical audit records executor errors without failing valid_evidence. | **low**, pre-existing historical entrypoint limitation. The immutable helper distinguishes evidence integrity from execution/acceptance and always reports P1R unusable. Its result must not be used as a new operational qualification verdict. | Preserve required exact helper hash. Record limitation; future audit must explicitly require no executor errors and all required actual lanes/cleanup. |
| B8 | Historical audit lacks a final live source check after waits. | **medium**, pre-existing historical entrypoint race. Initial check precedes waits; the final result has no second explicit source check. | Preserve required exact helper hash. Do not use this historical entrypoint for new qualification; require fresh final source revalidation in the future independently reviewed audit. |
| B9 | Historical audit proposal lookup assumes its original umbrella layout. | **low**, pre-existing fixed-capture limitation. It is bound to the dated proposal and original layout; importing its regression helpers does not execute its main entrypoint. | Preserve exact helper hash; document historical entrypoint restriction instead of widening its proposal-selection interface. |
| B10 | Simulated lifecycle CI does not prove real pinned-image interoperability. | **low**, verification improvement. Existing hardening retained a real same-runtime smoke; this reconciliation will additionally attempt a fresh harmless real-Docker smoke with exact bytes and owned cleanup. | Retain fresh success or literal environment blocker; no package qualification claim. |
| P1 | Canonical scanner rejects two hardening endpoint fixture literals. | **medium**, independently reproduced by parent `CanonicalReconciliationDelivery_DoesNotContainUsableSecrets`; it names hardening lines448/449. | Generate runtime UUID credentials and retain all endpoint/refusal/nonretention assertions; do not change scanner behavior or historical evidence. |

No reviewer identified an acceptance or sprint-state change. The publication
patches address B1/B4; P1 adjusts only the isolated copy of a test fixture.
Known historical-driver restrictions remain explicit below; their four required
hashes must remain unchanged. Neither those drivers nor local source controls
can accept P1R.

## Retained historical-driver restrictions

The four dated helpers are exact historical CI dependencies. Their entrypoints
are not the operational audit for a future selected successor. A future audit
must bind the new proposal explicitly, require zero executor errors and complete
successful lanes/cleanup, revalidate source after all waits and before sealing,
and retain exact new invocation evidence. Their immutable copies are preserved;
no old audit result is rewritten into a current qualification pass.

Use a self-contained ordinary checkout in a real declared workspace for future
private execution. Linked Git metadata or shared-object alternates outside the
mount are unsupported. A failure/refusal from such a layout remains nonpassing.
Retain the packet and the entire invocation's physical inputs, binaries,
consumer manifests, wrapper/private/execution evidence at their original
absolute paths. Moving/removing them invalidates physical-evidence validation.

## Direct-fix recheck and final evidence

The same blind reviewer independently rechecked B1/B4 and the UUID endpoint
fixture fix. All 17 authority tests and the focused endpoint control passed;
no new related defect was found. Parent final verification separately passed
all 266 Python tests, both optimized suites, 179 snapshot tests, 64 packaging
tests, 778 canonical secret/evidence controls, 24 compiler evaluations and a
fresh real-Docker smoke with successful private/outer cleanup. See
[verification index](verification.json). B6 retention requirements are in the
handoff; B3/B5/B7/B8/B9 remain bounded pre-existing restrictions, not accepted
operational qualification. A separate new-bundle ingestion audit found six
flags; its exact evidence and pending disposition are retained in
[evidence ingestion handoff](evidence-ingestion-handoff.md).
