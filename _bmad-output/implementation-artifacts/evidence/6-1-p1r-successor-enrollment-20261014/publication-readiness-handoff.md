# P1R successor enrollment and publication handoff

Proposed stable version: **3.122.1**. This is verified source enrollment and a gated publication proposal. No successor package has been published, selected or qualified. Four-role acceptance, same-baseline conformance and Solution Architect disposition remain pending; P1R is unusable and Story 6.1 is blocked.

The [reviewed source manifest](reviewed-source.json) identifies the exact enrollment diff and source closure. The [independent controls](verification.json), [compiler evaluations](compiler-enrollment.json), [review triage](review-triage.json) and [preservation check](preservation-result.json) provide the verification boundaries. Prior repair and failed evidence remain at their original paths, including the [verified repair](../6-1-p1r-31210-blocker-repair-20261014/README.md) and its [successor proposal](../6-1-p1r-31210-blocker-repair-20261014/successor-proposal.md).

## Version and observed identities

The [canonical analysis](release-analysis/commit-analysis.json) used the installed `@semantic-release/commit-analyzer` 13.0.1 and canonical release configuration. The current delta after published `v3.122.0` is a docs-only commit and selects no release. A separately identified hypothetical, commitlint-validated future fix selects a patch, hence 3.122.1. This scenario is not a landed commit or reservation. Reanalyze the actual authorized landing before reserving a version; any intervening release or higher bump restarts enrollment and verification for the resulting single version.

[Registry observations](release-analysis/registry-observations.json) show all fourteen manifest package indices exclude 3.122.1 and their direct archive URLs return 404. GitHub tag and release return 404. The authenticated OCI check remains outstanding: unauthenticated `registry.hexalith.com/eventstore:3.122.1` returned **401**, which does not prove absence. Repeat all destination checks immediately before publication.

| Observed role | Full commit identity | Version description / meaning |
| --- | --- | --- |
| Refreshed EventStore HEAD and observed live `origin/main` | `210b90da172cc7ca946fbb06cfd44390bffa707b` | Docs after 3.122.0; detached checkout preserves the original local branch |
| Latest published stable EventStore | `1dac529c4690910c89d4090784a7d46cb9433a9d` | `v3.122.0`; release run 38588597583 and source CI 38588214862 were observed, and cannot prove future-source CI |
| Original local EventStore `main`, retained | `5c10a3c73f381abb75016125192401f516818797` | Contains historical evidence/tooling absent upstream; no commit was discarded |
| Original local EventStore Builds gitlink | `608c164c3637240fed2dd92dbfdaaf581ba9846d` | `v4.30.1-33-g608c164` |
| Refreshed canonical Builds gitlink and actual sibling Builds HEAD | `9f0121c77e3f085c58f5c926057606db8e6511a7` | `v4.31.2-1-g9f0121c`; exact local props bytes are separately hashed |
| Tracked/canonical release execution Builds (`Bexec`) | `22a578b576a515d2af214fe81859447fffc97981` | `v4.24.0-37-g22a578b`; separate publisher execution identity |

These `git describe` strings are observations, not invented stable package-build versions. [Builds observations](release-analysis/builds-identities.json) retain exact commands and props hashes. The source refresh changed the checked-out upstream source and gitlinks without initializing or updating any nested dependency. [Refresh receipt](refresh-result.json) proves all 1,541 carried files retained their bytes. Enrollment adds only 3.122.1; production `src/` equals the refreshed upstream commit, including its existing production snapshot enrollment.

Future `S` (landed frozen source), `Btag` (that source/tag's gitlink), construction Builds/version/props, approved publication `Bexec`, release/tag objects, package repository commits and archive hashes are **not yet observed**. Obtain these after authorized landing/publication. A dirty detached source checkout is not the publication source or an immutable qualification selection.

The publication closure includes the [four unchanged dated CI helper sources](required-ci-test-dependencies.json) imported by the existing tests; they must land at their exact recorded paths and hashes. The present outer Docker controller uses a local Unix endpoint. The pre-existing remote TLS-authenticated controller limitation is [deferred in the Projects ledger](../../../../../../_bmad-output/implementation-artifacts/deferred-work.md); no remote TLS profile was qualified.

## Required gates before dispatch

1. Separately authorize landing and reconcile with then-current main. Retain this reviewed delta, the original snapshot fixtures/controls, private Docker wrapper and all historical evidence. Validate the real Conventional Commit subjects and fresh release analysis. Freeze full canonical `S`, reviewed closure and exact `Btag`, construction Builds inputs and separately approved `Bexec`. Every later byte change restarts source verification/CI and version reservation.
2. Require successful **push** `ci.yml` for exact current-main `S`, successful `p1r-qualification.yml` including the new controls and existing isolation suite, and successful Commitlint. Default CI includes folded-snapshot compatibility, process packages, Contracts and configured individual suites/consumer validation. The observed old CI cannot satisfy this gate. Dispatch with `bypass-validation=false`.
3. **Publication is currently blocked:** both tracked and canonical `release.yml` set `HEXALITH_RELEASE_REQUIRE_AUTHORITY=false`. Before source freeze, later authorized, reviewed workflow wiring must set it to `true` and supply `HEXALITH_RELEASE_RESERVED_VERSION=3.122.1`, the real `HEXALITH_RELEASE_AUTHORITY_ISSUE_URL` and `HEXALITH_RELEASE_AUTHORITY_OWNER`. Verify missing, mismatched, expired and replayed authority are refused. Retain a real release-owner comment with schema `hexalith.release-publication-authority.v1`, exact publication-identity hash, rationale, times and nonce. The protected production environment remains required; it does not replace one-use authority. This handoff does not enable that workflow gate or create authority.
4. Require protected `production` approval, publication-enable variable, NuGet trusted-publisher identity/OIDC key, Zot credentials, npm provenance/signature audit, SDK restore, Release build with `-warnaserror`, authoritative fourteen-package manifest validation, exact-source CI/current-main revalidation and authenticated registry absence/version-floor checks. Verify/publish/container preflight must bind the same frozen identity. Consume the single-use authority before the first package write.
5. Retain release run/attempt, GitHub tag object and peeled source, all fourteen package identities, signatures, hashes and repository commits, OCI multi-platform/child-digest provenance and liveness smokes, plus complete success/failure release evidence. Never replace bytes at an existing version. Independently observe the five P1R archives and their lock/assets/physical assembly proofs; require explicit owner selection of the observed new tuple before qualification.

## Exact commands after authorization and gate wiring

Run from the final frozen EventStore checkout. Supply `S` from authorized landing, not from this dirty workspace. The following commands are a handoff and were not dispatched here.

```bash
set -euo pipefail
: "${S:?exact authorized frozen source SHA required}"
test "$(git rev-parse HEAD)" = "$S"
test "$(gh api repos/Hexalith/Hexalith.EventStore/git/ref/heads/main --jq .object.sha)" = "$S"
gh api --method GET repos/Hexalith/Hexalith.EventStore/actions/workflows/ci.yml/runs \
  -f branch=main -f event=push -f head_sha="$S" -f status=success -f per_page=100 \
  --jq '[.workflow_runs[] | select(.head_sha == "'"$S"'" and .head_branch == "main" and .event == "push" and .status == "completed" and .conclusion == "success")] | length > 0' \
  | python3 -c 'import json,sys; assert json.load(sys.stdin) is True'
gh workflow run release.yml --repo Hexalith/Hexalith.EventStore --ref main \
  -f bypass-validation=false
```

Once the exact dispatched run is observed, verify `headSha == S`, run/attempt and completed success. Download into a new path:

```bash
set -euo pipefail
: "${RELEASE_RUN:?observed exact release run ID required}"
: "${RELEASE_ATTEMPT:?observed exact release attempt required}"
: "${NEW_RELEASE_EVIDENCE:?new evidence directory required}"
gh run view "$RELEASE_RUN" --repo Hexalith/Hexalith.EventStore \
  --json headSha,event,status,conclusion,url
gh run download "$RELEASE_RUN" --repo Hexalith/Hexalith.EventStore \
  --name "release-evidence-${RELEASE_RUN}-${RELEASE_ATTEMPT}" \
  --dir "$NEW_RELEASE_EVIDENCE"
```

The workflow invokes `npx semantic-release`; canonical hooks validate secrets/preflight, pack and validate fourteen packages, then call publish-phase preflight before signed NuGet publication and the pinned container publisher. [The exact internal commands](release-analysis/release-analysis-report.json) are retained for audit; do not bypass the guarded workflow with a direct push command.

## Exact qualification commands after publication and owner selection

Prepare a **new** planning observation from independent public archive/provenance checks. It must include proposed 3.122.1's actual tag/source, `tag_builds_commit`, construction Builds identity, five archive/content hashes and repository commits. The observation command requires source HEAD and the published tag to equal the observed selected source. Keep the Debug/source consumer separate. The public CLI automatically enters the private Docker runner; it publishes no runner ports and mounts no host socket.

```bash
set -euo pipefail
: "${P1R_NEW:?new invocation parent inside the workspace required}"
: "${P1R_PLANNING:?new independently observed planning JSON required}"
: "${OWNER_SELECTION_REFERENCE:?explicit new tuple-selection reference required}"
python3 tools/p1r-published-executor.py observe --out "$P1R_NEW/observed" \
  --planning-observations "$P1R_PLANNING" \
  --selection-reference "$OWNER_SELECTION_REFERENCE"
```

Review the emitted proposal and obtain explicit owner selection of its exact bytes. Only then provide `OWNER_SELECTED_INPUTS`; no old inputs or synthetic fixtures may substitute. Require rollback=null, comparisons 3.70.1/3.110.0, Dapr 1.18.2, complete PostgreSQL v1 profile with `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`, `p1r-executed-checks-v1`, both adopted additions, single-writer drain, mutation freeze and forward recovery.

```bash
set -euo pipefail
: "${OWNER_SELECTED_INPUTS:?explicitly selected observed owner-inputs JSON required}"
python3 tools/p1r-published-executor.py run --out "$P1R_NEW/run" \
  --inputs "$OWNER_SELECTED_INPUTS"
test ! -e "$P1R_NEW/run-receipts.nul"
python3 - "$P1R_NEW/run/execution.json" > "$P1R_NEW/run-receipts.nul" <<'PY'
import json, pathlib, sys
execution = json.loads(pathlib.Path(sys.argv[1]).read_text())
receipts = execution['receipts']
assert receipts and len(receipts) == len(set(receipts))
assert all(pathlib.Path(name).is_file() for name in receipts)
for name in receipts:
    sys.stdout.buffer.write(str(pathlib.Path(name).absolute()).encode() + b'\0')
PY
mapfile -d '' -t RECEIPTS < "$P1R_NEW/run-receipts.nul"
RECEIPT_ARGS=()
for receipt in "${RECEIPTS[@]}"; do RECEIPT_ARGS+=(--receipt "$receipt"); done
python3 tools/p1r-isolated-runner.py --out "$P1R_NEW/boundary-isolation" -- \
  python3 "$PWD/tools/p1r-published-qualification.py" measure-boundary \
  --inputs "$OWNER_SELECTED_INPUTS" "${RECEIPT_ARGS[@]}" \
  --out "$P1R_NEW/support-boundary.json"
: "${OWNER_DECISIONS:?exact-tuple owner decision/conformance file required}"
python3 tools/p1r-isolated-runner.py --out "$P1R_NEW/packet-isolation" -- \
  python3 "$PWD/tools/p1r-published-qualification.py" prepare --out "$P1R_NEW/packet" \
  --inputs "$OWNER_SELECTED_INPUTS" --decisions "$OWNER_DECISIONS" \
  --candidate-evidence "$P1R_NEW/run/consumers/candidate" \
  --comparison-evidence "$P1R_NEW/run/consumers/comparison-3.70.1" \
  --comparison-evidence "$P1R_NEW/run/consumers/comparison-3.110.0" \
  "${RECEIPT_ARGS[@]}" --support-boundary "$P1R_NEW/support-boundary.json"
python3 tools/p1r-isolated-runner.py --out "$P1R_NEW/packet-validation-isolation" -- \
  python3 "$PWD/tools/p1r-published-qualification.py" validate "$P1R_NEW/packet"
```

Use absolute workspace paths for these variables. Preparation and independent validation deliberately use the same digest-pinned runner and Python path/version/hash as execution: interpreter identity is part of the exact source binding. Preparing on the host with its different interpreter would be refused. Each extra wrapper also requires successful final owned cleanup; preserve its evidence and every nonpassing attempt.

Require all seventeen canonical lanes plus Reminder recovery and logical-event evolution, every contributing cleanup/restore receipt and no historical receipt substitution. All six formerly failed cases remain supported. Require actual nine-event/manual-version-one creation/three-tail append, persisted fresh restore, append/restart thirteen, floor five, old event/snapshot and second-tenant preservation, and all corruption/protection/uncovered controls. Independently audit commands, probes, SQL/configuration/source bindings, complete nonempty shared-resource equality, wrapper/outer teardown and actual absence of exact owned IDs. Preserve unexpected inner resources and every failed/interrupted attempt at new paths; never remove unowned resources.

The boundary's disposition remains pending until the Solution Architect supplies it. Missing authority must be refused; pending decisions must remain nonaccepted. EventStore Owner, Builds Owner, Solution Architect and Test Architect must decide the same exact tuple and provide conformance. Structural packet validation cannot grant usability or qualify a package by source proof. Separate Projects acceptance, P0/P2/P3, same-baseline sign-off, P4 clean checkout and independent readiness exactly `READY` still govern Story 6.1.
