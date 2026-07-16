---
title: 'Implement Story 1.16/1.20 Sprint Change Proposal'
type: 'chore'
created: '2026-07-16'
status: 'done'
baseline_commit: '4423e03bef8f2e6f9139a143a3fc42ea8c835dfd'
review_loop_iteration: 5
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-16-story-1-16-review-and-story-1-20-proof-closure.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved Story 1.16/1.20 correction is only partially reflected in the implementation artifacts. Without the remaining AD-11, sequencing, identity, and approval gates, readers could mistake a failed candidate or documentation commit for an approved runtime and authorize consumer migration prematurely.

**Approach:** Apply the approved planning and evidence edits incrementally against the current richer artifacts. Preserve every concurrent fact and fail-closed value, and leave conditional Story 1.16 review closure unapplied until a genuine named review exists for the eventual exact runtime.

## Boundaries & Constraints

**Always:** Preserve the failed candidate `85877902f8d60a466ab90cd8b68b53838863db1c` as non-authorizing evidence; keep Story 1.20 `blocked`, sprint/Epic 1 `in-progress`, `tested_runtime_sha: null`, `documentation_commit_sha: null`, `final_decision: still blocked`, `authorize_consumer_migration: false`, and all nine capability rows `still blocked`; retain Story 1.16 `followup_review_recommended: true`; append rather than overwrite deferred work; reconcile against current repository facts and richer evidence.

**Ask First:** Any proposal to clear Story 1.16's review flag, select a tested runtime, approve package or container identity, authorize migration, change a story/epic status, publish an artifact, edit a submodule, or alter runtime code, tests, pins, topology, or persisted data requires new human authority and the missing durable evidence.

**Never:** Fabricate reviewer, approval, SHA, package hash, container digest, platform, provenance, or verification data; restore proposal-era submodule pointers; replace the current proof packet with the proposal's shorter example; modify Parties or remove rollback infrastructure; weaken or bypass a fail-closed guard.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approved incremental update | Rich failed-candidate evidence already exists | Add only missing AD-11, execution-order, identity, and blocker context | Preserve current evidence when proposal snippets are stale |
| Missing exact-runtime review | Story 1.16 has no named, dated, durable approval | Keep the flag `true` and do not add a disposition | Keep Story 1.20 blocked |
| Partial proof | Build subsets passed but live-sidecar failed | Retain candidate only as failed evidence | Never populate approval/runtime fields |
| Concurrent artifact drift | Current facts differ from proposal-era audit | Use current facts without erasing historical evidence | Stop if a change would overwrite unknown work |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- current richer proof ledger and all non-authorizing runtime/package/container fields.
- `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` -- Story 1.20 acceptance boundary, blocker state, and execution sequence.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- append-only lifecycle and build/release corrective-work ledger.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- Epic 1 and Story 1.20 status plus blocker commentary.
- `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md` -- historical filename for active Story 1.16; its follow-up-review flag remains fail closed.
- `_bmad-output/implementation-artifacts/epic-1-context.md` -- workflow-refreshed concurrent context change to preserve.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- preserve every prior loop requirement, then close the demonstrated bypasses: make the exact AD-11 predicate an explicit all-condition conjunction; capture, validate, and authority-bind the installed `Microsoft.NETCore.App` patch alongside SDK/ASP.NET; revalidate authority and source identity immediately before publication; require the exact requested platform set and raw-manifest digest; and require evidence-only A to be the single-parent child of its equal candidate/tested runtime without weakening B's direct-child one-field rule.
- [x] `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` -- preserve all prior loop acceptance, Story 1.19 reconciliation, closure sequencing, durable metadata/expiry enforcement, non-self-referential A/B protocol, current proposal action inventory, and `status: blocked` requirements; keep the A/B check explicitly structural and leave any later authority-bearing transition subject to every independent closure gate.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- preserve the already committed single AD-11 `open-blocking` corrective item and every existing entry; do not duplicate or rewrite them.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- preserve the approved Story 1.20 blocker comments and `in-progress` states, and retain both `last_updated` fields as `2026-07-16`.
- [x] `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md` -- verify structurally and retain exactly one opening-front-matter `followup_review_recommended: true`; make no review-disposition edit without all approved fields and evidence.
- [x] `_bmad-output/implementation-artifacts/epic-1-context.md` -- preserve the restored Sample/Tenants behavior-preservation and release-hygiene constraints, projection-handler compatibility rule, and canonical SDK endpoint paths without expanding this approved correction into unrelated context design.

**Acceptance Criteria:**
- Given the failed candidate and incomplete identities, when the artifacts are updated, then no field, capability row, story, epic, or comment claims approval, availability, completion, or migration authority.
- Given existing concurrent evidence and deferred items, when approved text is applied, then those contents remain intact and the AD-11 work is additive and separately owned.
- Given Story 1.16 lacks an exact-runtime review, when implementation completes, then its flag remains `true` and the Story 1.20 blockers explicitly name that condition.
- Given the proposal is evidence-only, when the diff is reviewed by attribution, then no proposal-owned runtime source, test, pin, package manifest, container, topology, submodule, Parties, or persisted-data edit exists; separately owned concurrent changes remain visible and byte-preserved.
- Given external container publication requires release-owner authority, when the closure order is read, then authority precedes publication and final proof approval remains a later distinct gate.
- Given approvals and commit identities cannot self-reference, when closure is recorded, then durable approvals precede evidence-only commit A, A is the single-parent `_bmad-output/`-only child of its equal candidate/tested runtime, and pointer-only commit B records A as `documentation_commit_sha`; expired or out-of-scope authority keeps the packet blocked.
- Given AD-11 and publication are hard gates, when executable proof commands run, then baseline mismatch or missing/malformed/blank/expired/out-of-scope authority aborts before any build or publication-capable command, and accepted authority evidence is immutably copied, hashed, and bound into provenance.
- Given Bash conditionals can suppress `errexit`, when any exact AD-11 SDK, roll-forward, installed SDK, ASP.NET, or .NET runtime condition mismatches, then the exact-baseline predicate returns failure and cannot bypass the replacement-authority path.
- Given a publication is an external side effect, when it begins, then authority is still unexpired, tracked/untracked source and candidate identity still match, the inspected manifest has exactly `linux/amd64` and `linux/arm64`, and its raw bytes hash to the recorded immutable digest.
- Given fail-closed truth spans several artifacts, when implementation verification runs, then it structurally identifies all nine named blocked capability sections, exact Story 1.16/1.20 front matter, exact sprint Story/Epic states, and the authorized proposal path set while reporting known concurrent paths separately.

## Spec Change Log

- Review loop 1: the first review found stale Story 1.19 wording, ambiguous pre-publication authority, an unchanged proof-packet timestamp, an untracked-file whitespace gap, and over-pruned Epic 1 constraints. Tasks and verification now require those corrections, avoiding contradictory blocker narratives, unauthorized publication ordering, weak provenance, incomplete whitespace coverage, and future context-driven compatibility/release regressions. KEEP: preserve the additive AD-11 audit and ledger work, exact-SHA failure evidence, current-fact reconciliation, all existing deferred entries, every fail-closed status/identity/capability guard, Story 1.16's retained flag, and all concurrent lifecycle/submodule changes.
- Review loop 2: the second review found that whitespace verification was not anchored to `baseline_commit`, sprint comments retained an old tracker date, owner-authority exceptions lacked durable metadata, and canonical SDK route names remained over-pruned. Verification and tasks now cover the full tracked baseline diff, tracked-or-untracked spec state, tracker chronology, durable authority records, and endpoint compatibility. KEEP: all loop-1 preservation rules and corrections; preserve concurrent commits and never rewrite their runtime, test, branch, or submodule content.
- Review loop 3: the third review found a self-referential documentation-SHA sequence, missing authority-expiry enforcement, no authority preflight in the executable container procedure, and an incomplete current file inventory. Tasks now require durable approvals before evidence commit A, a pointer-only commit B that pins A, fail-closed authority validation before publication, action-time validity checks, and a proposal-specific inventory separate from historical records. KEEP: every prior loop correction and preservation rule, especially the blocked packet fields and attribution boundary for concurrent work.
- Review loop 4: the fourth review found that AD-11 remained prose-only, publication authority accepted whitespace and was not retained in provenance, the authorized repository was not bound to the actual publish properties, the A/B protocol lacked executable validation, and prose-wide `rg` could miss changed front matter. Tasks and verification now require fail-closed executable preflights, nonblank scoped fields, immutable authority evidence, explicit publish binding, A/B structural commands, and exact opening-front-matter parsing. KEEP: all prior loop corrections and every concurrent-change preservation boundary.
- Review loop 5: the fifth review demonstrated an `errexit`-suppression false pass in the exact AD-11 predicate and found missing .NET-runtime binding, pre-publication revalidation, exact platform/digest checks, tested-runtime commit binding, structural cross-artifact guards, and attribution-aware path verification. Tasks and acceptance now make each condition executable and falsifiable. The A/B procedure remains a structural evidence-only/non-self-reference check, never proof-result approval; local record validation remains an evidence-integrity preflight, never a substitute for human authority, durable-source trust, or registry access control. KEEP: all prior fail-closed fields, the approved trust boundary, the exact five proposal-owned edits, verify-only ledger/Story 1.16 actions, and all explicitly reported concurrent worktree paths.

## Design Notes

The approved proposal contains replacement-style examples based on an earlier checkout. Treat them as semantic requirements merged into the current richer evidence. Repository identity is an observed, timestamped audit fact. Authority validation is an evidence-integrity preflight, not a substitute for registry access control or human authorization; the accepted record and its digest must survive with provenance. After all results exist, named approvals live in durable external sources; evidence commit A records those results and approval references, then pointer-only commit B sets `documentation_commit_sha` to A. B must make no other semantic change. Whole-baseline review includes concurrent work for visibility, but scope acceptance is attribution-based: this evidence-only implementation must not author runtime or dependency changes owned by concurrent commits.

## Verification

**Commands:**

```bash
git diff --check 4423e03bef8f2e6f9139a143a3fc42ea8c835dfd
git diff --cached --check
git ls-files --error-unmatch \
  _bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md \
  >/dev/null 2>&1 || test -z "$(git diff --no-index --check -- /dev/null \
  _bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md 2>&1)"
bash scripts/check-deferred-work.sh _bmad-output/implementation-artifacts/deferred-work.md
```

Expected: exit 0. The baseline-anchored tracked diff, staged diff, and tracked-or-untracked
spec contain no whitespace/conflict errors; the deferred ledger passes its governance check
(pre-existing legacy advisories may remain).

```bash
PACKET='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
awk '
  NR == 1 && $0 == "---" { in_front = 1; next }
  in_front && $0 == "---" { in_front = 0; exit }
  in_front && index($0, "candidate_source_sha:") == 1 {
    candidate_key++; candidate_ok += ($0 == "candidate_source_sha: 85877902f8d60a466ab90cd8b68b53838863db1c")
  }
  in_front && index($0, "tested_runtime_sha:") == 1 {
    runtime_key++; runtime_ok += ($0 == "tested_runtime_sha: null")
  }
  in_front && index($0, "documentation_commit_sha:") == 1 {
    documentation_key++; documentation_ok += ($0 == "documentation_commit_sha: null")
  }
  in_front && index($0, "final_decision:") == 1 {
    decision_key++; decision_ok += ($0 == "final_decision: still blocked")
  }
  in_front && index($0, "authorize_consumer_migration:") == 1 {
    migration_key++; migration_ok += ($0 == "authorize_consumer_migration: false")
  }
  END { exit !(candidate_key == 1 && candidate_ok == 1 && runtime_key == 1 &&
    runtime_ok == 1 && documentation_key == 1 && documentation_ok == 1 &&
    decision_key == 1 && decision_ok == 1 && migration_key == 1 && migration_ok == 1) }
' "$PACKET"

awk '
  BEGIN {
    expected["### Read-model and projection-checkpoint erasure"] = 1
    expected["### Coordinated batching"] = 1
    expected["### Six-state lifecycle and route-bound provenance"] = 1
    expected["### Duplicate and out-of-order production-handler safety"] = 1
    expected["### Full paged-rebuild equivalence"] = 1
    expected["### Cursor compatibility"] = 1
    expected["### Asynchronous projection persistence"] = 1
    expected["### Multiple projections per domain"] = 1
    expected["## Cross-Cutting Compatibility And Release Gate"] = 1
  }
  /^```/ { fenced = !fenced; next }
  !fenced && /^##(#)? / {
    active = ($0 in expected) ? $0 : ""
    if ($0 in expected) headings[$0]++
    next
  }
  !fenced && active != "" && /^- classification: / {
    classifications[active]++
    if ($0 == "- classification: `still blocked`") blocked[active]++
  }
  END {
    for (heading in expected)
      if (headings[heading] != 1 || classifications[heading] != 1 ||
          blocked[heading] != 1) bad = 1
    exit bad
  }
' "$PACKET"
```

Expected: exit 0. Opening front matter contains each exact fail-closed identity/decision guard
once, and each of the nine named capability sections exists once with exactly one classification,
which is `still blocked`.

```bash
awk '
  NR == 1 && $0 == "---" { in_front = 1; next }
  in_front && $0 == "---" { in_front = 0; exit }
  in_front && index($0, "followup_review_recommended:") == 1 {
    flag_key++; flag_ok += ($0 == "followup_review_recommended: true")
  }
  END { exit !(flag_key == 1 && flag_ok == 1) }
' _bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md

awk '
  NR == 1 && $0 == "---" { in_front = 1; next }
  in_front && $0 == "---" { in_front = 0; exit }
  in_front && index($0, "story_id:") == 1 {
    story_key++; story_ok += ($0 == "story_id: \"1.20\"")
  }
  in_front && index($0, "status:") == 1 {
    status_key++; status_ok += ($0 == "status: blocked")
  }
  END { exit !(story_key == 1 && story_ok == 1 && status_key == 1 && status_ok == 1) }
' _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md

awk '
  /^# last_updated:/ { comment_key++; comment_ok += ($0 == "# last_updated: 2026-07-16") }
  /^last_updated:/ { field_key++; field_ok += ($0 == "last_updated: 2026-07-16") }
  /^  epic-1:/ { epic_key++; epic_ok += ($0 == "  epic-1: in-progress") }
  /^  1-20-owner-approved-parity-closure-and-runtime-pin:/ {
    story_key++; story_ok += ($0 == "  1-20-owner-approved-parity-closure-and-runtime-pin: in-progress")
  }
  END { exit !(comment_key == 1 && comment_ok == 1 && field_key == 1 && field_ok == 1 &&
    epic_key == 1 && epic_ok == 1 && story_key == 1 && story_ok == 1) }
' _bmad-output/implementation-artifacts/sprint-status.yaml
```

Expected: exit 0. Story 1.16 has exactly one opening-front-matter true flag; the Story 1.20
file is exactly `blocked`; the sprint Story 1.20 and Epic 1 entries remain exactly
`in-progress`; both tracker dates remain `2026-07-16`.

```bash
awk '
  $0 == "## Deferred from: Story 1.20 correct-course readiness audit (2026-07-16)" {
    section++; in_ad11 = 1; next
  }
  in_ad11 && /^## / { in_ad11 = 0 }
  in_ad11 && /^  status:/ {
    status_key++; status_ok += ($0 == "  status: open-blocking")
  }
  in_ad11 && /^  summary:/ {
    summary_key++
    summary_ok += ($0 == "  summary: Land the architecture AD-11 .NET/ASP.NET security baseline before selecting Story 1.20\047s tested runtime SHA.")
  }
  END { exit !(section == 1 && status_key == 1 && status_ok == 1 &&
    summary_key == 1 && summary_ok == 1) }
' _bmad-output/implementation-artifacts/deferred-work.md
```

Expected: exactly one AD-11 readiness section exists, with one `open-blocking` status and
the approved summary; no duplicate ledger entry is accepted.

```bash
bash -c '
  set -euo pipefail
  audited_head=02a93d5a0325dc842ad6a64897a3b8fb2907b9a9
  audited_parent=f9d1f1986d87fa375ddab22ccd2cccde96209ec5
  finalized_head=ac60e831ba3521315f6046d29bbb7abde91c1628
  proposal_at_parent=(
    _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md
    _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
    _bmad-output/implementation-artifacts/epic-1-context.md
    _bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md
    _bmad-output/implementation-artifacts/sprint-status.yaml
  )
  concurrent_at_parent=(
    references/Hexalith.FrontComposer
    references/Hexalith.Memories
  )
  audited_head_paths=(
    _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
    _bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md
  )
  finalized_head_paths=(
    _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
    _bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md
  )
  git merge-base --is-ancestor "$audited_head" HEAD
  git merge-base --is-ancestor "$finalized_head" HEAD
  test "$(git rev-parse "${audited_head}^")" = "$audited_parent"
  diff -u \
    <(printf "%s\n" "${proposal_at_parent[@]}" "${concurrent_at_parent[@]}" | LC_ALL=C sort) \
    <(git diff-tree --no-commit-id --name-only -r "$audited_parent" | LC_ALL=C sort)
  diff -u \
    <(printf "%s\n" "${audited_head_paths[@]}" | LC_ALL=C sort) \
    <(git diff-tree --no-commit-id --name-only -r "$audited_head" | LC_ALL=C sort)
  diff -u \
    <(printf "%s\n" "${finalized_head_paths[@]}" | LC_ALL=C sort) \
    <(git diff-tree --no-commit-id --name-only -r "$finalized_head" | LC_ALL=C sort)
'
```

Expected: exit 0. Audited HEAD `02a93d5a...` changed exactly packet+spec; its direct parent
`f9d1f198...` changed exactly five proposal paths plus the two concurrent submodule pointers.
The committed finalization `ac60e831...` also changed exactly packet+spec, and both historical
revisions are ancestors of the reviewed checkout. No assertion depends on a now-obsolete
working-tree shape.

```bash
bash -c '
  set -euo pipefail
  syntax_file="$(mktemp)"
  trap "rm -f -- \"$syntax_file\"" EXIT
  for document in "$1" "$2"; do
    awk '\''/^```bash$/ { in_bash = 1; next }
      in_bash && /^```$/ { print ""; in_bash = 0; next }
      in_bash { print }'\'' "$document" > "$syntax_file"
    bash -n "$syntax_file"
  done
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md \
  _bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md

bash -c '
  set -euo pipefail
  packet="$1"
  function_file="$(mktemp)"
  trap "rm -f -- \"$function_file\"" EXIT
  for function_name in fresh_release run_xunit_class run_xunit_method run_xunit_all; do
    awk -v signature="$function_name() {" '\''
      $0 == signature { capture = 1 }
      capture { print }
      capture && /^}$/ { exit }
    '\'' "$packet" > "$function_file"
    bash -n "$function_file"
    test "$(grep -c "^  assert_candidate_identity$" "$function_file")" -eq 2
  done
  test "$(grep -c "^assert_candidate_identity$" "$packet")" -ge 11
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md

bash -c '
  set -euo pipefail
  packet="$1"
  function_file="$(mktemp)"
  trap "rm -f -- \"$function_file\"" EXIT
  awk '\''/^ad11_exact_baseline\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$function_file"
  bash -n "$function_file"
  source "$function_file"
  set_exact() {
    REPOSITORY_SDK_VERSION=10.0.302
    REPOSITORY_SDK_ROLL_FORWARD=latestPatch
    INSTALLED_SDK_VERSION=10.0.302
    REPOSITORY_ASPNET_VERSION=10.0.10
    INSTALLED_ASPNET_VERSION=10.0.10
    INSTALLED_RUNTIME_VERSION=10.0.10
  }
  set_exact
  ad11_exact_baseline
  for mismatch in repository_sdk roll_forward installed_sdk repository_aspnet \
      installed_aspnet installed_runtime; do
    set_exact
    case "$mismatch" in
      repository_sdk) REPOSITORY_SDK_VERSION=10.0.301 ;;
      roll_forward) REPOSITORY_SDK_ROLL_FORWARD=minor ;;
      installed_sdk) INSTALLED_SDK_VERSION=10.0.301 ;;
      repository_aspnet) REPOSITORY_ASPNET_VERSION=10.0.9 ;;
      installed_aspnet) INSTALLED_ASPNET_VERSION=10.0.9 ;;
      installed_runtime) INSTALLED_RUNTIME_VERSION=10.0.9 ;;
    esac
    if ad11_exact_baseline; then
      printf "AD-11 false pass for %s\n" "$mismatch" >&2
      exit 1
    fi
  done
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md

bash -c '
  set -euo pipefail
  packet="$1"
  function_file="$(mktemp)"
  trap "rm -f -- \"$function_file\"" EXIT
  awk '\''/^validate_aspnet_pin_band\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$function_file"
  bash -n "$function_file"
  source "$function_file"
  exact=(
    "Microsoft.AspNetCore.Authentication.JwtBearer 10.0.10"
    "Microsoft.AspNetCore.Identity 2.3.11"
    "Microsoft.AspNetCore.SignalR.Client 10.0.10"
  )
  test "$(validate_aspnet_pin_band "${exact[@]}")" = 10.0.10
  mixed=("${exact[@]}")
  mixed[0]="Microsoft.AspNetCore.Authentication.JwtBearer 10.0.9"
  ! validate_aspnet_pin_band "${mixed[@]}" >/dev/null 2>&1
  wrong_major=("${exact[@]}")
  wrong_major[0]="Microsoft.AspNetCore.Authentication.JwtBearer 9.0.10"
  ! validate_aspnet_pin_band "${wrong_major[@]}" >/dev/null 2>&1
  wrong_legacy=("${exact[@]}")
  wrong_legacy[1]="Microsoft.AspNetCore.Identity 10.0.10"
  ! validate_aspnet_pin_band "${wrong_legacy[@]}" >/dev/null 2>&1
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: exit 0. Every packet Bash block parses, the extracted exact AD-11 function passes
the valid tuple, each individual mismatch fails even when the function is invoked by `if`,
and the ASP.NET pin-band validator rejects mixed patch bands, wrong majors, and a changed
legacy Identity exception.

```bash
bash -c '
  set -euo pipefail
  packet="$1"
  inventory_function="$(mktemp)"
  expected="$(mktemp)"
  packages="$(mktemp -d)"
  trap "rm -f -- \"$inventory_function\" \"$expected\"; rm -rf -- \"$packages\"" EXIT
  awk '\''/^validate_literal_package_inventory\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$inventory_function"
  bash -n "$inventory_function"
  source "$inventory_function"
  printf "%s\n" Alpha Beta > "$expected"
  python3 - "$packages" <<"PY"
import pathlib
import sys
import zipfile

directory = pathlib.Path(sys.argv[1])
for package_id in ("Alpha", "Beta"):
    with zipfile.ZipFile(directory / f"{package_id}.1.0.0.nupkg", "w") as package:
        package.writestr(f"{package_id}.nuspec", f"<package><metadata><id>{package_id}</id><version>1.0.0</version></metadata></package>")
PY
  validate_literal_package_inventory "$packages" 1.0.0 "$expected"
  python3 - "$packages/Beta.1.0.0.nupkg" <<"PY"
import pathlib
import sys
import zipfile

with zipfile.ZipFile(pathlib.Path(sys.argv[1]), "w") as package:
    package.writestr("Beta.nuspec", "<package><metadata><id>Alpha</id><version>1.0.0</version></metadata></package>")
PY
  ! validate_literal_package_inventory "$packages" 1.0.0 "$expected" >/dev/null 2>&1
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md

bash -c '
  set -euo pipefail
  packet="$1"
  discovery_function="$(mktemp)"
  replacement_function="$(mktemp)"
  inventory="$(mktemp)"
  valid="$(mktemp)"
  mutated="$(mktemp)"
  trap "rm -f -- \"$discovery_function\" \"$replacement_function\" \"$inventory\" \"$valid\" \"$mutated\"" EXIT
  awk '\''/^discover_bound_runtime_versions\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$discovery_function"
  awk '\''/^validate_ad11_replacement\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$replacement_function"
  bash -n "$discovery_function"
  bash -n "$replacement_function"
  source "$discovery_function"
  source "$replacement_function"
  printf "%s\n" \
    "Microsoft.AspNetCore.App 10.0.11 [/dotnet/shared/Microsoft.AspNetCore.App]" \
    "Microsoft.NETCore.App 10.0.11 [/dotnet/shared/Microsoft.NETCore.App]" > "$inventory"
  mapfile -t bound < <(discover_bound_runtime_versions "$inventory" 10.0.11)
  test "${#bound[@]}" -eq 2
  test "${bound[0]}" = 10.0.11
  test "${bound[1]}" = 10.0.11
  sed "s/Microsoft.NETCore.App 10.0.11/Microsoft.NETCore.App 10.0.10/" \
    "$inventory" > "$mutated"
  ! discover_bound_runtime_versions "$mutated" 10.0.11 >/dev/null 2>&1

  REPOSITORY_URL=https://github.com/Hexalith/Hexalith.EventStore.git
  CANDIDATE_SHA=0123456789abcdef0123456789abcdef01234567
  REPOSITORY_SDK_VERSION=10.0.303
  REPOSITORY_SDK_ROLL_FORWARD=latestPatch
  INSTALLED_SDK_VERSION=10.0.303
  REPOSITORY_ASPNET_VERSION=10.0.11
  INSTALLED_ASPNET_VERSION=10.0.11
  INSTALLED_RUNTIME_VERSION=10.0.11
  checked_at=2026-07-16T12:00:00Z
  jq -n --arg repository "$REPOSITORY_URL" --arg source_sha "$CANDIDATE_SHA" '\''{
    action: "ad11-baseline-replacement", owner: "architecture-owner",
    approved_at: "2026-07-16T11:00:00Z", durable_source: "approval://ad11/1",
    rationale: "later security baseline", expires_at: "2026-07-16T13:00:00Z",
    scope: {repository: $repository, source_sha: $source_sha, sdk_version: "10.0.303",
      sdk_roll_forward: "latestPatch", aspnet_version: "10.0.11",
      runtime_version: "10.0.11"}}
  '\'' > "$valid"
  validate_ad11_replacement "$checked_at" "$valid" >/dev/null
  for mutation in '\''.owner = " "'\'' '\''.approved_at = "2026-07-16T13:00:00Z"'\'' \
      '\''.expires_at = "2026-07-16T12:00:00Z"'\'' \
      '\''.scope.runtime_version = "10.0.10"'\'' \
      '\''.scope.source_sha = "ffffffffffffffffffffffffffffffffffffffff"'\'' \
      '\''.scope.repository = "https://example.invalid/EventStore.git"'\''; do
    jq "$mutation" "$valid" > "$mutated"
    ! validate_ad11_replacement "$checked_at" "$mutated" >/dev/null 2>&1
  done
  printf "{" > "$mutated"
  ! validate_ad11_replacement "$checked_at" "$mutated" >/dev/null 2>&1
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: the actual runtime-discovery function binds matching installed ASP.NET/.NET runtime
patches and rejects mismatch; the actual AD-11 replacement predicate accepts a valid later
baseline and rejects blank, malformed, future, expired, wrong-runtime, wrong-source, and
wrong-repository records.

```bash
bash -c '
  set -euo pipefail
  packet="$1"
  authority_function="$(mktemp)"
  authority_record="$(mktemp)"
  mutated_record="$(mktemp)"
  trap "rm -f -- \"$authority_function\" \"$authority_record\" \"$mutated_record\"" EXIT
  awk '\''/^validate_publication_authority\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$authority_function"
  bash -n "$authority_function"
  source "$authority_function"
  IMAGE_REPOSITORY=registry.hexalith.com/eventstore
  IMAGE_TAG=proof-0123456789abcdef0123456789abcdef01234567
  CANDIDATE_SHA=0123456789abcdef0123456789abcdef01234567
  checked_at=2026-07-16T12:00:00Z
  jq -n --arg repository "$IMAGE_REPOSITORY" --arg tag "$IMAGE_TAG" \
    --arg source_sha "$CANDIDATE_SHA" '\''{
      action: "container-publication", owner: "release-owner",
      authorized_at: "2026-07-16T11:00:00Z", durable_source: "approval://record/1",
      rationale: "proof publication", expires_at: "2026-07-16T13:00:00Z",
      scope: {repository: $repository, tag: $tag, source_sha: $source_sha}}
    '\'' > "$authority_record"
  AUTHORITY_EVIDENCE="$authority_record"
  validate_publication_authority "$checked_at" >/dev/null
  for mutation in '\''.action = "container-inspection"'\'' '\''.owner = " "'\'' \
      '\''.authorized_at = "2026-07-16T13:00:00Z"'\'' \
      '\''.durable_source = " "'\'' '\''.rationale = " "'\'' \
      '\''.expires_at = "2026-07-16T12:00:00Z"'\'' \
      '\''.scope.repository = "registry.invalid/eventstore"'\'' \
      '\''.scope.tag = "proof-wrong"'\'' \
      '\''.scope.source_sha = "ffffffffffffffffffffffffffffffffffffffff"'\''; do
    jq "$mutation" "$authority_record" > "$mutated_record"
    AUTHORITY_EVIDENCE="$mutated_record"
    if validate_publication_authority "$checked_at" >/dev/null 2>&1; then
      printf "publication-authority false pass: %s\n" "$mutation" >&2
      exit 1
    fi
  done
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md

bash -c '
  set -euo pipefail
  packet="$1"
  manifest_function="$(mktemp)"
  manifest="$(mktemp)"
  platforms="$(mktemp)"
  trap "rm -f -- \"$manifest_function\" \"$manifest\" \"$platforms\"" EXIT
  awk '\''/^validate_published_manifest\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$manifest_function"
  bash -n "$manifest_function"
  source "$manifest_function"
  printf %s '\''{"manifests":[{"platform":{"os":"linux","architecture":"amd64"}},{"platform":{"os":"linux","architecture":"arm64"}}]}'\'' > "$manifest"
  IMAGE_DIGEST="sha256:$(sha256sum "$manifest" | awk '\''{print $1}'\'')"
  test "$(validate_published_manifest "$manifest" "$IMAGE_DIGEST" "$platforms")" \
    = "${IMAGE_DIGEST#sha256:}"
  ! validate_published_manifest "$manifest" \
    sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
    "$platforms" >/dev/null 2>&1
  printf %s '\''{"manifests":[{"platform":{"os":"linux","architecture":"amd64"}}]}'\'' > "$manifest"
  IMAGE_DIGEST="sha256:$(sha256sum "$manifest" | awk '\''{print $1}'\'')"
  ! validate_published_manifest "$manifest" "$IMAGE_DIGEST" "$platforms" >/dev/null 2>&1
  printf %s '\''{"manifests":[{"platform":{"os":"linux","architecture":"amd64"}},{"platform":{"os":"linux","architecture":"arm64"}},{"platform":{"os":"linux","architecture":"s390x"}}]}'\'' > "$manifest"
  IMAGE_DIGEST="sha256:$(sha256sum "$manifest" | awk '\''{print $1}'\'')"
  ! validate_published_manifest "$manifest" "$IMAGE_DIGEST" "$platforms" >/dev/null 2>&1
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: exit 0. The extracted publication-authority predicate accepts the valid scoped,
unexpired fixture and rejects wrong action, blank fields, future authorization, action-time
expiry, and wrong repository/tag/source. The extracted manifest validator accepts only the
matching raw-byte digest and exact two-platform set.

```bash
bash -c '
  set -euo pipefail
  packet="$1"
  hash_function="$(mktemp)"
  expected="$(mktemp)"
  complete="$(mktemp)"
  partial="$(mktemp)"
  absolute="$(mktemp)"
  trap "rm -f -- \"$hash_function\" \"$expected\" \"$complete\" \"$partial\" \"$absolute\"" EXIT
  awk '\''/^validate_package_hash_manifest\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$hash_function"
  bash -n "$hash_function"
  source "$hash_function"
  for number in $(seq -w 1 14); do
    printf "Package%s.1.0.0.nupkg\n" "$number"
  done > "$expected"
  while IFS= read -r filename; do
    printf "%064d  %s\n" 0 "$filename"
  done < "$expected" > "$complete"
  validate_package_hash_manifest "$complete" "$expected"
  sed -n "1,13p" "$complete" > "$partial"
  ! validate_package_hash_manifest "$partial" "$expected" >/dev/null 2>&1
  awk '\''{ print $1 "  /tmp/" $2 }'\'' "$complete" > "$absolute"
  ! validate_package_hash_manifest "$absolute" "$expected" >/dev/null 2>&1
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md

bash -c '
  set -euo pipefail
  packet="$1"
  capture_targets="$(mktemp --suffix=.targets)"
  fixture_project="$(mktemp --suffix=.proj)"
  generated_index="$(mktemp)"
  generated_digest="$(mktemp)"
  trap "rm -f -- \"$capture_targets\" \"$fixture_project\" \"$generated_index\" \"$generated_digest\"" EXIT
  python3 - "$packet" "$capture_targets" <<"PY"
import pathlib
import sys

lines = pathlib.Path(sys.argv[1]).read_text(encoding="utf-8").splitlines()
marker = "cat > \"$CAPTURE_TARGETS\" <<" + chr(39) + "MSBUILD" + chr(39)
start = lines.index(marker) + 1
end = lines.index("MSBUILD", start)
pathlib.Path(sys.argv[2]).write_text("\n".join(lines[start:end]) + "\n", encoding="utf-8")
PY
  cat > "$fixture_project" <<EOF
<Project>
  <Import Project="$capture_targets" />
  <Target Name="_PublishMultiArchContainers">
    <PropertyGroup>
      <GeneratedImageIndex>{"schemaVersion":2,"manifests":[]}</GeneratedImageIndex>
      <ContainerImageIndexOutputPath>$generated_index</ContainerImageIndexOutputPath>
      <ContainerImageIndexDigestOutputPath>$generated_digest</ContainerImageIndexDigestOutputPath>
    </PropertyGroup>
  </Target>
</Project>
EOF
  dotnet msbuild "$fixture_project" -nologo -t:_PublishMultiArchContainers
  expected_digest="sha256:$(printf %s '\''{"schemaVersion":2,"manifests":[]}'\'' | sha256sum | awk '\''{print $1}'\'')"
  test "$(cat "$generated_digest")" = "$expected_digest"
  test "sha256:$(sha256sum "$generated_index" | awk '\''{print $1}'\'')" = "$expected_digest"
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md

bash -c '
  set -euo pipefail
  packet="$1"
  clean_function="$(mktemp)"
  repository="$(mktemp -d)"
  trap "rm -f -- \"$clean_function\"; rm -rf -- \"$repository\"" EXIT
  awk '\''/^assert_publication_source_clean\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$clean_function"
  bash -n "$clean_function"
  source "$clean_function"
  git -C "$repository" init -q
  git -C "$repository" config user.email proof@example.invalid
  git -C "$repository" config user.name Proof
  printf "%s\n" bin/ obj/ "*.tmp" > "$repository/.gitignore"
  git -C "$repository" add .gitignore
  git -C "$repository" commit -qm baseline
  assert_publication_source_clean "$repository"
  mkdir -p "$repository/src/bin" "$repository/src/obj"
  : > "$repository/src/bin/output.dll"
  : > "$repository/src/obj/assets.json"
  assert_publication_source_clean "$repository"
  : > "$repository/untracked.txt"
  ! assert_publication_source_clean "$repository" >/dev/null 2>&1
  rm "$repository/untracked.txt"
  : > "$repository/unapproved.tmp"
  ! assert_publication_source_clean "$repository" >/dev/null 2>&1
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md

awk '
  $0 == "dotnet restore \"$CONTAINER_PROJECT\" \\" { restore = NR }
  $0 == "  assert_publication_source_clean \"$repository\"" {
    if (first_clean == 0) first_clean = NR
    last_clean = NR
  }
  $0 == "AUTHORITY_CHECKED_AT=\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"" { action_time = NR }
  $0 == "validate_publication_authority \"$AUTHORITY_CHECKED_AT\"" { authority_call = NR }
  $0 == "assert_candidate_identity" { last_identity = NR }
  $0 == "if dotnet publish \"$CONTAINER_PROJECT\" \\" {
    publish = NR; immediate_identity = (last_identity == NR - 1)
  }
  $0 == "IMAGE_DIGEST=\"$(cat \"$GENERATED_IMAGE_INDEX_DIGEST\")\"" { generated_digest = NR }
  $0 == "test \"$TAG_IMAGE_DIGEST\" = \"$IMAGE_DIGEST\"" { tag_consistency = NR }
  END { exit !(restore > 0 && restore < first_clean && first_clean < action_time &&
    action_time < authority_call && authority_call < publish && immediate_identity &&
    publish < last_clean && last_clean < generated_digest && generated_digest < tag_consistency) }
' _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: the literal package gate accepts the exact filename-to-nuspec mapping and rejects
duplicate embedded identities. The package hash guard accepts exactly 14 portable relative
filenames and rejects partial or absolute-path manifests. The extracted SDK image-index capture
target preserves exact bytes and emits their digest. The cleanliness function accepts a clean repository and generated
`bin`/`obj`, rejects untracked and unapproved ignored inputs, and the publication block orders a
fresh isolated restore, clean source, fresh action timestamp, authority validation, immediate
candidate identity, post-publish cleanliness, SDK-produced digest capture, and tag consistency.

```bash
bash -c '
  set -euo pipefail
  packet="$1"
  verification_script="$(mktemp)"
  repository="$(mktemp -d)"
  trap "rm -f -- \"$verification_script\"; rm -rf -- \"$repository\"" EXIT
  awk '\''$0 == "### Evidence Commit A And Pointer-Only Commit B Verification" {
      section = 1; next
    }
    section && /^```bash$/ { capture = 1; next }
    capture && /^```$/ { exit }
    capture { print }'\'' "$packet" > "$verification_script"
  bash -n "$verification_script"
  git -C "$repository" init -q
  git -C "$repository" config user.email proof@example.invalid
  git -C "$repository" config user.name Proof
  mkdir -p "$repository/src"
  packet_path="$repository/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md"
  story_path="$repository/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md"
  sprint_path="$repository/_bmad-output/implementation-artifacts/sprint-status.yaml"
  write_story_guard() {
    local status="$1"
    mkdir -p "$(dirname "$story_path")"
    printf "%s\n" "---" "status: $status" "---" "" "# Story 1.20" "" \
      "Status: $status" > "$story_path"
  }
  write_sprint_guard() {
    local epic_status="$1"
    local story_status="$2"
    printf "%s\n" "development_status:" "  epic-1: $epic_status" \
      "  1-20-owner-approved-parity-closure-and-runtime-pin: $story_status" > "$sprint_path"
  }
  printf "stable runtime\n" > "$repository/src/runtime.txt"
  write_story_guard blocked
  write_sprint_guard in-progress in-progress
  git -C "$repository" add src/runtime.txt _bmad-output
  git -C "$repository" commit -qm runtime
  runtime="$(git -C "$repository" rev-parse HEAD)"
  write_packet() {
    local documentation="$1"
    local candidate="${2:-$runtime}"
    local tested="${3:-$runtime}"
    mkdir -p "$(dirname "$packet_path")"
    printf "%s\n" "---" "candidate_source_sha: $candidate" \
      "tested_runtime_sha: $tested" "documentation_commit_sha: $documentation" \
      "final_decision: still blocked" "authorize_consumer_migration: false" "---" \
      "# Evidence" > "$packet_path"
  }
  write_packet null
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm evidence-a
  evidence_a="$(git -C "$repository" rev-parse HEAD)"
  write_packet "$evidence_a"
  git -C "$repository" add _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
  git -C "$repository" commit -qm pointer-b
  pointer_b="$(git -C "$repository" rev-parse HEAD)"
  (cd "$repository" && EVIDENCE_COMMIT_A="$evidence_a" POINTER_COMMIT_B="$pointer_b" \
    bash "$verification_script")

  git -C "$repository" checkout -qb extra-b "$evidence_a"
  write_packet "$evidence_a"
  printf "extra\n" > "$repository/_bmad-output/extra.txt"
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm extra-b
  extra_b="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$evidence_a" POINTER_COMMIT_B="$extra_b" \
    bash "$verification_script" >/dev/null 2>&1)

  git -C "$repository" checkout -qb non-evidence-a "$runtime"
  write_packet null
  printf "untested\n" > "$repository/src/untested.txt"
  git -C "$repository" add _bmad-output src/untested.txt
  git -C "$repository" commit -qm non-evidence-a
  bad_a="$(git -C "$repository" rev-parse HEAD)"
  write_packet "$bad_a"
  git -C "$repository" add _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
  git -C "$repository" commit -qm bad-pointer-b
  bad_b="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$bad_a" POINTER_COMMIT_B="$bad_b" \
    bash "$verification_script" >/dev/null 2>&1)

  git -C "$repository" checkout -qb unequal-identities-a "$runtime"
  unequal_candidate=0000000000000000000000000000000000000000
  write_packet null "$unequal_candidate" "$runtime"
  git -C "$repository" add _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
  git -C "$repository" commit -qm unequal-identities-a
  unequal_a="$(git -C "$repository" rev-parse HEAD)"
  write_packet "$unequal_a" "$unequal_candidate" "$runtime"
  git -C "$repository" add _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
  git -C "$repository" commit -qm unequal-identities-b
  unequal_b="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$unequal_a" POINTER_COMMIT_B="$unequal_b" \
    bash "$verification_script" >/dev/null 2>&1)

  git -C "$repository" checkout -qb premature-status-a "$runtime"
  write_packet null
  write_story_guard done
  write_sprint_guard done done
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm premature-status-a
  premature_a="$(git -C "$repository" rev-parse HEAD)"
  write_packet "$premature_a"
  git -C "$repository" add _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
  git -C "$repository" commit -qm premature-status-b
  premature_b="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$premature_a" POINTER_COMMIT_B="$premature_b" \
    bash "$verification_script" >/dev/null 2>&1)
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: the extracted actual A/B verifier accepts runtime → evidence-only A → pointer-only B,
rejects an extra-file B, rejects A when it bundles a non-`_bmad-output/` path, rejects unequal
candidate/tested-runtime identities, and rejects A when it prematurely completes Story 1.20 or
Epic 1.

```bash
git diff --check
```

Expected: exit 0. Current review work has no whitespace or conflict-marker errors; historical
proposal attribution is verified by immutable revisions above rather than by a transient
working-tree snapshot.

## Suggested Review Order

**Fail-Closed Closure Design**

- Start with the approved intent and immutable safety boundaries.
  [spec-1-16-1-20-sprint-change-proposal.md:15](./spec-1-16-1-20-sprint-change-proposal.md#L15)

- Confirm Story 1.20 requires complete evidence without promoting current state.
  [1-20-owner-approved-parity-closure-and-runtime-pin.md:22](./1-20-owner-approved-parity-closure-and-runtime-pin.md#L22)

- Follow the authority, evidence-A, pointer-B, and later-transition sequence.
  [1-20-owner-approved-parity-closure-and-runtime-pin.md:78](./1-20-owner-approved-parity-closure-and-runtime-pin.md#L78)

- Verify the packet remains explicitly blocked and non-authorizing.
  [1-20-owner-approved-parity-closure-proof-packet.md:17](./1-20-owner-approved-parity-closure-proof-packet.md#L17)

**Executable Identity and Publication Gates**

- Inspect exact AD-11, runtime-binding, and per-gate candidate identity enforcement.
  [1-20-owner-approved-parity-closure-proof-packet.md:199](./1-20-owner-approved-parity-closure-proof-packet.md#L199)

- Review package, publication-authority, manifest-digest, and exact-platform validation.
  [1-20-owner-approved-parity-closure-proof-packet.md:953](./1-20-owner-approved-parity-closure-proof-packet.md#L953)

- Check evidence-only A and pointer-only B against self-reference or code drift.
  [1-20-owner-approved-parity-closure-proof-packet.md:1258](./1-20-owner-approved-parity-closure-proof-packet.md#L1258)

**State and Compatibility Preservation**

- Confirm Story 1.20 and Epic 1 remain in progress.
  [sprint-status.yaml:83](./sprint-status.yaml#L83)

- Confirm canonical routes and additive projection-handler compatibility remain explicit.
  [epic-1-context.md:35](./epic-1-context.md#L35)

**Verification Evidence**

- Run structural, mutation, attribution, and temporary commit-graph fixtures.
  [spec-1-16-1-20-sprint-change-proposal.md:83](./spec-1-16-1-20-sprint-change-proposal.md#L83)
