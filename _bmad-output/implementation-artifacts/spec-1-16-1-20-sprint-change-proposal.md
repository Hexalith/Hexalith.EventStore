---
title: 'Implement Story 1.16/1.20 Sprint Change Proposal'
type: 'chore'
created: '2026-07-16'
status: 'done'
baseline_commit: '4423e03bef8f2e6f9139a143a3fc42ea8c835dfd'
review_loop_iteration: 6
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
- [x] `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md` -- preserve every prior loop requirement; evaluate effective ASP.NET pins through MSBuild; revalidate AD-11 and ignored inputs around every gate; retain complete configured-test evidence; bind package/container consumer bytes and provenance; persist hybrid evidence pins; and verify evidence A, pointer-only B, and authorizing C without weakening any fail-closed intermediate state.
- [x] `_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md` -- preserve all prior loop acceptance, Story 1.19 reconciliation, closure sequencing, durable metadata/expiry enforcement, the non-self-referential A/B/C protocol, current proposal action inventory, and `status: blocked` requirements; define C as B's direct child and the sole strictly verified authority-bearing transition.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- preserve the single AD-11 corrective item, its `implementation-complete/evidence-confirmed` reconciliation, the separate open Story 2.7 source-topology prerequisite, and every existing entry without duplication or rewrite.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- preserve the approved Story 1.20 blocker comments and `in-progress` states, and retain both reconciled `last_updated` fields as `2026-07-17`.
- [x] `_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md` -- verify structurally and retain exactly one opening-front-matter `followup_review_recommended: true`; make no review-disposition edit without all approved fields and evidence.
- [x] `_bmad-output/implementation-artifacts/epic-1-context.md` -- preserve the restored Sample/Tenants behavior-preservation and release-hygiene constraints, projection-handler compatibility rule, and canonical SDK endpoint paths without expanding this approved correction into unrelated context design.

**Acceptance Criteria:**
- Given the failed candidate and incomplete identities, when the artifacts are updated, then no field, capability row, story, epic, or comment claims approval, availability, completion, or migration authority.
- Given existing concurrent evidence and deferred items, when approved text is applied, then those contents remain intact and the AD-11 work is additive and separately owned.
- Given Story 1.16 lacks an exact-runtime review, when implementation completes, then its flag remains `true` and the Story 1.20 blockers explicitly name that condition.
- Given the proposal is evidence-only, when the diff is reviewed by attribution, then no proposal-owned runtime source, test, pin, package manifest, container, topology, submodule, Parties, or persisted-data edit exists; separately owned concurrent changes remain visible and byte-preserved.
- Given external container publication requires release-owner authority, when the closure order is read, then authority precedes publication and final proof approval remains a later distinct gate.
- Given approvals and commit identities cannot self-reference, when closure is recorded, then durable approvals precede evidence-only commit A, A commits their exact records and binds the hybrid evidence/artifact identities as the single-parent `_bmad-output/`-only child of its equal candidate/tested runtime, pointer-only commit B records A as `documentation_commit_sha`, and only a strictly reconstructed direct-child C may authorize migration and reconcile the Story 1.20/Epic 1 statuses, tracker date, and blocker comments; incomplete Story 1.16 review, open prerequisites, expired/missing/mismatched authority, or nonliteral artifact identity keeps the packet blocked.
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
- Review loop 6: the chunked code review found fail-open pin discovery, skipped-test credit, incomplete configured suites/log capture, temporary-only evidence, weak consumer byte/provenance binding, pointer-mode drift, stale tracker/ledger assertions, duplicate-platform acceptance, syntactic-only final approvals, and an authorizing transition that did not prove Story 1.16 review, closed prerequisites, literal package IDs, complete container authority provenance, coherent sprint comments, or monotonic chronology. The accepted decisions use hybrid durable evidence and a strict direct-child commit C. Verification now exercises evaluated MSBuild items plus positive and targeted negative A/B/C histories for every added guard. KEEP: A and B non-authorizing, exact source/package/container identity separation, and every prior owner-approval and clean-source boundary.

## Design Notes

The approved proposal contains replacement-style examples based on an earlier checkout. Treat them as semantic requirements merged into the current richer evidence. Repository identity is an observed, timestamped audit fact. Authority validation is an evidence-integrity preflight, not a substitute for registry access control or human authorization; each accepted record and digest must survive with provenance. After all results exist, named approvals live in durable external sources and exact committed copies; evidence commit A records the hybrid evidence, literal artifact identities, results, completed Story 1.16 review, closed prerequisites, and approval records; pointer-only commit B sets `documentation_commit_sha` to A and makes no other semantic change; verified direct-child C performs only the reconstructed authorization/status/date/comment transition. Whole-baseline review includes concurrent work for visibility, but scope acceptance is attribution-based: this evidence-only implementation must not author runtime or dependency changes owned by concurrent commits.

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
  { sub(/\r$/, "") }
  /^# last_updated:/ { comment_key++; comment_ok += ($0 == "# last_updated: 2026-07-19") }
  /^last_updated:/ { field_key++; field_ok += ($0 == "last_updated: 2026-07-19") }
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
`in-progress`; both tracker dates remain `2026-07-17`.

```bash
awk '
  $0 == "## Deferred from: Story 1.20 correct-course readiness audit (2026-07-16)" {
    section++; in_ad11 = 1; next
  }
  in_ad11 && /^## / { in_ad11 = 0 }
  in_ad11 && /^  status:/ {
    status_key++; status_ok += ($0 == "  status: implementation-complete/evidence-confirmed")
  }
  in_ad11 && /^  summary:/ {
    summary_key++
    summary_ok += ($0 == "  summary: Land the architecture AD-11 .NET/ASP.NET security baseline before selecting Story 1.20\047s tested runtime SHA.")
  }
  in_ad11 && /^  closure_evidence:/ { closure_key++ }
  END { exit !(section == 1 && status_key == 1 && status_ok == 1 &&
    summary_key == 1 && summary_ok == 1 && closure_key == 1) }
' _bmad-output/implementation-artifacts/deferred-work.md
```

Expected: exactly one AD-11 readiness section exists, with one
`implementation-complete/evidence-confirmed` status, the approved summary, and one closure-evidence
record; no duplicate ledger entry or stale open status is accepted.

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
  for function_name in fresh_release fresh_debug_source run_xunit_class run_xunit_method \
      run_xunit_all validate_xunit_result assert_ad11_current prepare_publication_action \
      validate_final_owner_record validate_committed_owner_record; do
    awk -v signature="$function_name() {" '\''
      $0 == signature { capture = 1 }
      capture { print }
      capture && /^}$/ { exit }
    '\'' "$packet" > "$function_file"
    bash -n "$function_file"
    case "$function_name" in
      fresh_release|fresh_debug_source|run_xunit_class|run_xunit_method|run_xunit_all)
        test "$(grep -c "^  assert_ad11_current$" "$function_file")" -eq 1
        test "$(grep -c "^  assert_candidate_tree_clean$" "$function_file")" -eq 2
        ;;
      assert_ad11_current)
        test "$(grep -c "^  assert_candidate_identity$" "$function_file")" -eq 1
        ;;
    esac
  done
  test "$(grep -c "^assert_ad11_current$" "$packet")" -ge 6
  test "$(grep -c "^assert_candidate_tree_clean$" "$packet")" -ge 5
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

bash -c '
  set -euo pipefail
  packet="$1"
  props="$(mktemp --suffix=.props)"
  project="$(mktemp --suffix=.proj)"
  output="$(mktemp)"
  functions="$(mktemp)"
  trap "rm -f -- \"$props\" \"$project\" \"$output\" \"$functions\"" EXIT
  cat > "$props" <<PROPS
<Project>
  <PropertyGroup>
    <MixedVersion>10.0.9</MixedVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Version="10.0.10"
                    Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity" Version="2.3.11" />
    <PackageVersion Condition="\$(UseMixed)"
                    Version="\$(MixedVersion)"
                    Include="Microsoft.AspNetCore.OpenApi" />
  </ItemGroup>
</Project>
PROPS
  cat > "$project" <<PROJECT
<Project>
  <Import Project="$props" />
  <Target Name="WriteEffectivePackageVersions">
    <WriteLinesToFile File="$output"
                      Lines="@(PackageVersion->&apos;%(Identity) %(Version)&apos;)"
                      Overwrite="true" />
  </Target>
</Project>
PROJECT
  for function_name in discover_effective_aspnet_pin_pairs validate_aspnet_pin_band; do
    awk -v signature="$function_name() {" '\''
      $0 == signature { capture = 1 }
      capture { print }
      capture && /^}$/ { exit }
    '\'' "$packet" >> "$functions"
  done
  source "$functions"
  dotnet msbuild "$project" -nologo -t:WriteEffectivePackageVersions -p:UseMixed=false >/dev/null
  mapfile -t exact < <(discover_effective_aspnet_pin_pairs "$output")
  test "$(validate_aspnet_pin_band "${exact[@]}")" = 10.0.10
  dotnet msbuild "$project" -nologo -t:WriteEffectivePackageVersions -p:UseMixed=true >/dev/null
  mapfile -t mixed < <(discover_effective_aspnet_pin_pairs "$output")
  ! validate_aspnet_pin_band "${mixed[@]}" >/dev/null 2>&1
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: exit 0. Every packet Bash block parses, the extracted exact AD-11 function passes
the valid tuple, each individual mismatch fails even when the function is invoked by `if`,
the ASP.NET pin-band validator rejects mixed patch bands, wrong majors, and a changed legacy
Identity exception, and evaluated MSBuild items preserve reordered/multiline attributes while
still rejecting a conditional mixed patch.

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
  replacement_metadata="$(mktemp)"
  inventory="$(mktemp)"
  valid="$(mktemp)"
  mutated="$(mktemp)"
  trap "rm -f -- \"$discovery_function\" \"$replacement_function\" \"$replacement_metadata\" \"$inventory\" \"$valid\" \"$mutated\"" EXIT
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
  printf "%s\n" \
    '\''{"login":"architecture-owner","html_url":"https://github.com/Hexalith/Hexalith.EventStore/pull/1#pullrequestreview-1"}'\'' \
    > "$replacement_metadata"
  jq -n --arg repository "$REPOSITORY_URL" --arg source_sha "$CANDIDATE_SHA" '\''{
    action: "ad11-baseline-replacement", owner: "architecture-owner",
    approved_at: "2026-07-16T11:00:00Z", durable_source: "https://github.com/Hexalith/Hexalith.EventStore/pull/1#pullrequestreview-1",
    rationale: "later security baseline", expires_at: "2026-07-16T13:00:00Z",
    scope: {repository: $repository, source_sha: $source_sha, sdk_version: "10.0.303",
      sdk_roll_forward: "latestPatch", aspnet_version: "10.0.11",
      runtime_version: "10.0.11"}}
  '\'' > "$valid"
  validate_ad11_replacement "$checked_at" "$valid" "$replacement_metadata" >/dev/null
  for mutation in '\''.owner = " "'\'' '\''.approved_at = "2026-07-16T13:00:00Z"'\'' \
      '\''.expires_at = "2026-07-16T12:00:00Z"'\'' \
      '\''.scope.runtime_version = "10.0.10"'\'' \
      '\''.scope.source_sha = "ffffffffffffffffffffffffffffffffffffffff"'\'' \
      '\''.scope.repository = "https://example.invalid/EventStore.git"'\''; do
    jq "$mutation" "$valid" > "$mutated"
    ! validate_ad11_replacement "$checked_at" "$mutated" "$replacement_metadata" >/dev/null 2>&1
  done
  printf "{" > "$mutated"
  ! validate_ad11_replacement "$checked_at" "$mutated" "$replacement_metadata" >/dev/null 2>&1
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
  authority_metadata="$(mktemp)"
  mutated_record="$(mktemp)"
  trap "rm -f -- \"$authority_function\" \"$authority_record\" \"$authority_metadata\" \"$mutated_record\"" EXIT
  awk '\''/^validate_publication_authority\(\) \{$/ { capture = 1 }
    capture { print }
    capture && /^}$/ { exit }'\'' "$packet" > "$authority_function"
  bash -n "$authority_function"
  source "$authority_function"
  IMAGE_REPOSITORY=registry.hexalith.com/eventstore
  IMAGE_TAG=proof-0123456789abcdef0123456789abcdef01234567
  CANDIDATE_SHA=0123456789abcdef0123456789abcdef01234567
  checked_at=2026-07-16T12:00:00Z
  printf "%s\n" \
    '\''{"login":"release-owner","html_url":"https://github.com/Hexalith/Hexalith.EventStore/pull/1#pullrequestreview-2"}'\'' \
    > "$authority_metadata"
  jq -n --arg repository "$IMAGE_REPOSITORY" --arg tag "$IMAGE_TAG" \
    --arg source_sha "$CANDIDATE_SHA" '\''{
      action: "container-publication", owner: "release-owner",
      authorized_at: "2026-07-16T11:00:00Z", durable_source: "https://github.com/Hexalith/Hexalith.EventStore/pull/1#pullrequestreview-2",
      rationale: "proof publication", expires_at: "2026-07-16T13:00:00Z",
      scope: {repository: $repository, tag: $tag, source_sha: $source_sha}}
    '\'' > "$authority_record"
  AUTHORITY_EVIDENCE="$authority_record"
  AUTHORITY_GITHUB_METADATA="$authority_metadata"
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
  printf %s '\''{"manifests":[{"platform":{"os":"linux","architecture":"amd64"}},{"platform":{"os":"linux","architecture":"amd64"}}]}'\'' > "$manifest"
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
matching raw-byte digest and exactly one manifest for each required platform; missing,
duplicated, or extra platform entries fail.

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
  awk '\''/^assert_repository_clean_allow_generated\(\) \{$/ { capture = 1 }
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
  assert_repository_clean_allow_generated "$repository"
  mkdir -p "$repository/src/bin" "$repository/src/obj"
  : > "$repository/src/bin/output.dll"
  : > "$repository/src/obj/assets.json"
  assert_repository_clean_allow_generated "$repository"
  : > "$repository/untracked.txt"
  ! assert_repository_clean_allow_generated "$repository" >/dev/null 2>&1
  rm "$repository/untracked.txt"
  : > "$repository/unapproved.tmp"
  ! assert_repository_clean_allow_generated "$repository" >/dev/null 2>&1
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md

bash -c '
  set -euo pipefail
  packet="$1"
  publication_block="$(mktemp)"
  trap "rm -f -- \"$publication_block\"" EXIT
  awk '\''
    $0 == "- required exact container publication, immutable inspection, and provenance commands:" {
      section = 1; next
    }
    section && /^```bash$/ { capture = 1; next }
    capture && /^```$/ { exit }
    capture { print }
  '\'' "$packet" > "$publication_block"
  bash -n "$publication_block"
  awk '\''
    $0 == "dotnet restore \"$CONTAINER_PROJECT\" \\" { restore = NR }
    $0 == "prepare_publication_action() {" { in_ready = 1; ready_function = NR; next }
    in_ready && $0 == "  AUTHORITY_CHECKED_AT=\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"" {
      action_time = NR
    }
    in_ready && $0 == "  assert_ad11_current" { final_ad11 = NR }
    in_ready && $0 == "    assert_publication_source_clean \"$repository\"" {
      final_clean = NR
    }
    in_ready && $0 == "  validate_publication_authority \"$AUTHORITY_CHECKED_AT\"" {
      authority_call = NR
    }
    in_ready && $0 == "}" {
      ready_end = NR; authority_is_last = (authority_call == NR - 1); in_ready = 0
    }
    $0 == "prepare_publication_action" { ready_call = NR }
    $0 == "if dotnet publish \"$CONTAINER_PROJECT\" \\" {
      publish = NR; readiness_is_immediate = (ready_call == NR - 1)
    }
    $0 == "  assert_publication_source_clean \"$repository\"" { last_clean = NR }
    $0 == "IMAGE_TAG=\"quarantine-proof-${CANDIDATE_SHA}\"" { quarantine_tag = NR }
    $0 == "IMAGE_DIGEST=\"$(cat \"$GENERATED_IMAGE_INDEX_DIGEST\")\"" { generated_digest = NR }
    $0 == "test \"$TAG_IMAGE_DIGEST\" = \"$IMAGE_DIGEST\"" { tag_consistency = NR }
    END { exit !(restore > 0 && ready_function > restore &&
      ready_function < final_ad11 && final_ad11 < final_clean && final_clean < action_time &&
      action_time < authority_call && authority_call < ready_end && authority_is_last &&
      ready_end < ready_call && readiness_is_immediate && quarantine_tag > 0 &&
      quarantine_tag < publish && publish < last_clean && last_clean < generated_digest &&
      generated_digest < tag_consistency) }
  '\'' "$publication_block"
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: the literal package gate accepts the exact filename-to-nuspec mapping and rejects
duplicate embedded identities. The package hash guard accepts exactly 14 portable relative
filenames and rejects partial or absolute-path manifests. The extracted SDK image-index capture
target preserves exact bytes and emits their digest. The cleanliness function accepts a clean repository and generated
`bin`/`obj`, rejects untracked and unapproved ignored inputs, and the structurally isolated
publication block orders a fresh restore, one final AD-11/source/action-time/authority readiness
function immediately before quarantined publication, post-publish cleanliness, SDK-produced
digest capture, and tag consistency. Markers outside that executable block cannot satisfy it.

```bash
bash -c '
  set -euo pipefail
  packet="$1"
  verification_script="$(mktemp)"
  contract_copy="$(mktemp)"
  awk '\''$0 == "### Evidence Commit A, Pointer-Only Commit B, And Authorizing Commit C Verification" {
      section = 1; next
    }
    section && /^```bash$/ { capture = 1; next }
    capture && /^```$/ { exit }
    capture { print }'\'' "$packet" > "$verification_script"
  bash -n "$verification_script"
  required_contract_markers=(
    "cmp --silent \"\$RUNTIME_EXECUTABLE_BLOCKS\" \"\$A_EXECUTABLE_BLOCKS\""
    "verify_committed_github_role_record"
    "story-1-16-followup-review.github.json"
    "EXPECTED_A_FOLLOWUP_SPEC"
    "critical-evidence-expected-files.txt"
    "A_RAW_EVIDENCE_BUNDLE_SHA256"
    "A_EXPECTED_RESULT_FILES"
    "validate_source_state"
    "A_APPROVAL_SUBJECT_SHA256"
    "A_EXPECTED_LIMITATION_IDS"
    "A_EXPECTED_PREREQUISITE_STATUSES"
    "implementation-complete/evidence-confirmed"
    "docker buildx imagetools inspect"
    "\$A_CONTAINER_REPOSITORY@\$child_digest"
    "test-evidence-identities.tsv"
    "raw_evidence_immutability_proof_sha256"
    "verify_container_platform_smoke"
    "test-identity-contract-start"
    "authorization-phase-contract-start"
    "container-smoke-verifier-contract-start"
    "AUTHORIZATION_VERIFICATION_PHASE"
    "CURRENT_AUTHORITY_PACKET"
    "A_RUNTIME_EFFECTIVE_PACKAGES_RELEASE"
    "decision_section_changed"
    "owner_migration_changed"
    "final_section_changed"
  )
  check_contract() {
    local contract="$1"
    local marker
    bash -n "$contract"
    for marker in "${required_contract_markers[@]}"; do
      grep -Fq "$marker" "$contract"
    done
  }
  check_contract "$verification_script"
  for marker in "${required_contract_markers[@]}"; do
    awk -v marker="$marker" '\''index($0, marker) == 0 { print }'\'' \
      "$verification_script" > "$contract_copy"
    ! check_contract "$contract_copy" >/dev/null 2>&1
  done
  packet_blocks="$(mktemp -d)"
  awk -v directory="$packet_blocks" '\''
    /^```bash$/ { inside = 1; block++; next }
    inside && /^```$/ { inside = 0; next }
    inside { print >> (directory "/block-" block ".sh") }
  '\'' "$packet"
  for block in "$packet_blocks"/*.sh; do
    bash -n "$block"
  done

  fixture_root="$(mktemp -d)"
  trap "rm -f -- \"$verification_script\" \"$contract_copy\"; rm -rf -- \"$packet_blocks\" \"$fixture_root\"" EXIT
  extract_marker_contract() {
    local start_marker="$1"
    local end_marker="$2"
    local output="$3"
    awk -v start_marker="$start_marker" -v end_marker="$end_marker" '\''
      $0 == start_marker { capture = 1; next }
      $0 == end_marker { exit }
      capture { print }
    '\'' "$packet" > "$output"
    test -s "$output"
  }

  xunit_contract="$fixture_root/xunit-contract.sh"
  extract_marker_contract \
    "# xunit-result-contract-start" "# xunit-result-contract-end" \
    "$xunit_contract"
  bash -n "$xunit_contract"
  . "$xunit_contract"
  xunit_good="$fixture_root/xunit-good.xml"
  printf "%s\n" \
    "<assemblies><assembly name=\"Fixture.Tests.dll\" total=\"1\" passed=\"1\" failed=\"0\" errors=\"0\" skipped=\"0\" not-run=\"0\"><collection><test type=\"Fixture.Tests.Case\" method=\"Passes\" result=\"Pass\" /></collection></assembly></assemblies>" \
    > "$xunit_good"
  validate_xunit_result "$xunit_good"
  xunit_mutated="$fixture_root/xunit-mutated.xml"
  sed "s/skipped=\"0\"/skipped=\"1\"/" "$xunit_good" > "$xunit_mutated"
  ! validate_xunit_result "$xunit_mutated" >/dev/null 2>&1
  sed "s/result=\"Pass\"/result=\"Fail\"/" "$xunit_good" > "$xunit_mutated"
  ! validate_xunit_result "$xunit_mutated" >/dev/null 2>&1
  sed "s/total=\"1\"/total=\"2\"/" "$xunit_good" > "$xunit_mutated"
  ! validate_xunit_result "$xunit_mutated" >/dev/null 2>&1
  printf "%s\n" \
    "<assemblies total=\"1\" passed=\"1\" failed=\"0\" errors=\"0\" skipped=\"0\" not-run=\"0\"><assembly name=\"Fixture.Tests.dll\"><collection><test type=\"Fixture.Tests.Case\" method=\"Passes\" result=\"Pass\" /></collection></assembly></assemblies>" \
    > "$xunit_mutated"
  ! validate_xunit_result "$xunit_mutated" >/dev/null 2>&1

  identity_contract="$fixture_root/test-identity-contract.py"
  extract_marker_contract \
    "# test-identity-contract-start" "# test-identity-contract-end" \
    "$identity_contract"
  identity_pairs="$fixture_root/full-identity-pairs.tsv"
  sed -n "/^full_assemblies = {$/,/^}$/p" "$identity_contract" |
    awk -F\" '\''/^    "/ { print $2 "|" $4 }'\'' > "$identity_pairs"
  test -s "$identity_pairs"
  identity_root="$fixture_root/identity-results"
  mkdir -p "$identity_root"
  identity_rows="$fixture_root/test-evidence-identities.tsv"
  filtered_names="$fixture_root/filtered-names.txt"
  full_names="$fixture_root/full-names.txt"
  : > "$identity_rows"
  : > "$filtered_names"
  : > "$full_names"
  while IFS="|" read -r result_name assembly; do
    printf "%s|%s|all|\n" "$result_name" "$assembly" >> "$identity_rows"
    printf "%s\n" "$result_name" >> "$full_names"
    printf "%s\n" "Fixture.Case.$result_name" \
      > "$identity_root/$result_name.methods.txt"
    printf "%s\n" \
      "<assemblies><assembly name=\"${assembly##*/}\"><collection><test type=\"Fixture.Case\" method=\"$result_name\" result=\"Pass\" /></collection></assembly></assemblies>" \
      > "$identity_root/$result_name.xml"
  done < "$identity_pairs"
  python3 "$identity_contract" \
    "$identity_root" "$identity_rows" "$filtered_names" "$full_names"
  first_result="$(head -n 1 "$full_names")"
  cp -- "$identity_root/$first_result.xml" "$fixture_root/identity-good.xml"
  sed "s/<assembly name=\"[^\"]*\"/<assembly name=\"Wrong.Tests.dll\"/" \
    "$fixture_root/identity-good.xml" > "$identity_root/$first_result.xml"
  ! python3 "$identity_contract" \
    "$identity_root" "$identity_rows" "$filtered_names" "$full_names" \
    >/dev/null 2>&1
  cp -- "$fixture_root/identity-good.xml" "$identity_root/$first_result.xml"
  printf "%s\n" "Fixture.Case.Unexecuted" \
    >> "$identity_root/$first_result.methods.txt"
  ! python3 "$identity_contract" \
    "$identity_root" "$identity_rows" "$filtered_names" "$full_names" \
    >/dev/null 2>&1

  worm_contract="$fixture_root/provider-worm-contract.sh"
  extract_marker_contract \
    "# provider-worm-contract-start" "# provider-worm-contract-end" \
    "$worm_contract"
  bash -n "$worm_contract"
  . "$worm_contract"
  RAW_EVIDENCE_PROVIDER_ADAPTER_ID=fixture-provider-v1
  RAW_EVIDENCE_PROVIDER_ADAPTER_SHA256="$(printf "a%.0s" {1..64})"
  RAW_EVIDENCE_BUNDLE_URL=https://evidence.example/raw.tgz
  RAW_EVIDENCE_BUNDLE_OBJECT_VERSION=fixture-version-1
  RAW_EVIDENCE_BUNDLE_SHA256="$(printf "b%.0s" {1..64})"
  write_worm_fixture() {
    local authenticated="$1"
    local locked="$2"
    local output="$3"
    printf "%s\n" \
      "{\"schema\":\"hexalith.eventstore.provider-worm-object-proof/v2\",\"provider\":{\"adapter_id\":\"$RAW_EVIDENCE_PROVIDER_ADAPTER_ID\",\"adapter_sha256\":\"$RAW_EVIDENCE_PROVIDER_ADAPTER_SHA256\",\"authenticated_api\":$authenticated},\"object\":{\"url\":\"$RAW_EVIDENCE_BUNDLE_URL\",\"version\":\"$RAW_EVIDENCE_BUNDLE_OBJECT_VERSION\",\"sha256\":\"$RAW_EVIDENCE_BUNDLE_SHA256\"},\"policy\":{\"mode\":\"WORM\",\"locked\":$locked,\"retention_until\":\"$RAW_EVIDENCE_BUNDLE_RETENTION_UNTIL\"}}" \
      > "$output"
  }
  worm_proof="$fixture_root/worm-proof.json"
  RAW_EVIDENCE_BUNDLE_RETENTION_UNTIL=2033-07-19T00:00:00Z
  write_worm_fixture true true "$worm_proof"
  validate_provider_worm_proof "$worm_proof" 2026-07-19T00:00:00Z
  write_worm_fixture false true "$worm_proof"
  ! validate_provider_worm_proof "$worm_proof" 2026-07-19T00:00:00Z \
    >/dev/null 2>&1
  write_worm_fixture true false "$worm_proof"
  ! validate_provider_worm_proof "$worm_proof" 2026-07-19T00:00:00Z \
    >/dev/null 2>&1
  RAW_EVIDENCE_BUNDLE_RETENTION_UNTIL=2033-07-18T23:59:59Z
  write_worm_fixture true true "$worm_proof"
  ! validate_provider_worm_proof "$worm_proof" 2026-07-19T00:00:00Z \
    >/dev/null 2>&1

  authorization_contract="$fixture_root/authorization-phase-contract.sh"
  extract_marker_contract \
    "# authorization-phase-contract-start" "# authorization-phase-contract-end" \
    "$authorization_contract"
  bash -n "$authorization_contract"
  . "$authorization_contract"
  authorization_repository="$fixture_root/authorization-repository"
  git init -q "$authorization_repository"
  git -C "$authorization_repository" config user.email proof@example.invalid
  git -C "$authorization_repository" config user.name Proof
  git -C "$authorization_repository" commit -qm runtime --allow-empty
  EVIDENCE_COMMIT_A="$(git -C "$authorization_repository" rev-parse HEAD)"
  git -C "$authorization_repository" commit -qm pointer --allow-empty
  POINTER_COMMIT_B="$(git -C "$authorization_repository" rev-parse HEAD)"
  git -C "$authorization_repository" commit -qm authorize --allow-empty
  AUTHORIZATION_COMMIT_C="$(git -C "$authorization_repository" rev-parse HEAD)"
  OFFICIAL_MAIN_SHA="$POINTER_COMMIT_B"
  AUTHORIZATION_VERIFICATION_PHASE=pre-merge
  (cd "$authorization_repository" && verify_authorization_phase)
  OFFICIAL_MAIN_SHA="$AUTHORIZATION_COMMIT_C"
  AUTHORIZATION_VERIFICATION_PHASE=official-main
  (cd "$authorization_repository" && verify_authorization_phase)
  OFFICIAL_MAIN_SHA="$EVIDENCE_COMMIT_A"
  AUTHORIZATION_VERIFICATION_PHASE=pre-merge
  ! (cd "$authorization_repository" && verify_authorization_phase) \
    >/dev/null 2>&1
  OFFICIAL_MAIN_SHA="$POINTER_COMMIT_B"
  AUTHORIZATION_VERIFICATION_PHASE=official-main
  ! (cd "$authorization_repository" && verify_authorization_phase) \
    >/dev/null 2>&1

  smoke_contract="$fixture_root/container-smoke-contract.sh"
  extract_marker_contract \
    "# container-smoke-contract-start" "# container-smoke-contract-end" \
    "$smoke_contract"
  bash -n "$smoke_contract"
  . "$smoke_contract"
  DOCKER_CALLS="$fixture_root/docker-calls.txt"
  : > "$DOCKER_CALLS"
  docker() {
    printf "%s\n" "$*" >> "$DOCKER_CALLS"
    case "$1" in
      run) printf "%s\n" fixture-container ;;
      inspect)
        if test "${2:-}" = -f; then printf "%s\n" true; else return 0; fi
        ;;
      port) printf "%s\n" 127.0.0.1:49152 ;;
      logs) return 0 ;;
      rm) test "${CLEANUP_FAIL:-0}" -eq 0 ;;
      *) return 1 ;;
    esac
  }
  curl() { test "${CURL_FAIL:-0}" -eq 0; }
  sleep() { :; }
  seq() { printf "%s\n" 1; }
  CURL_FAIL=0
  CLEANUP_FAIL=0
  smoke_published_platform fixture.example/repository \
    sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa \
    linux/amd64 "$fixture_root/smoke-amd64.log"
  smoke_published_platform fixture.example/repository \
    sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa \
    linux/arm64 "$fixture_root/smoke-arm64.log"
  grep -Fq -- "--platform linux/amd64" "$DOCKER_CALLS"
  grep -Fq -- "--platform linux/arm64" "$DOCKER_CALLS"
  CURL_FAIL=1
  ! smoke_published_platform fixture.example/repository \
    sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa \
    linux/amd64 "$fixture_root/smoke-failed-health.log" >/dev/null 2>&1
  CURL_FAIL=0
  CLEANUP_FAIL=1
  ! smoke_published_platform fixture.example/repository \
    sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa \
    linux/amd64 "$fixture_root/smoke-failed-cleanup.log" >/dev/null 2>&1
  exit 0

  # Historical synthetic-graph fixture retained below for mutation provenance only. The
  # reachable behavior fixtures above supersede its pre-GitHub-API evidence schema.
  repository="$(mktemp -d)"
  trap "rm -f -- \"$verification_script\"; rm -rf -- \"$repository\"" EXIT
  awk '\''$0 == "### Evidence Commit A, Pointer-Only Commit B, And Authorizing Commit C Verification" {
      section = 1; next
    }
    section && /^```bash$/ { capture = 1; next }
    capture && /^```$/ { exit }
    capture { print }'\'' "$packet" > "$verification_script"
  bash -n "$verification_script"
  git -C "$repository" init -q
  git -C "$repository" config user.email proof@example.invalid
  git -C "$repository" config user.name Proof
  git -C "$repository" remote add origin https://github.com/Hexalith/Hexalith.EventStore.git
  mkdir -p "$repository/src" "$repository/tools"
  packet_path="$repository/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md"
  story_path="$repository/_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md"
  sprint_path="$repository/_bmad-output/implementation-artifacts/sprint-status.yaml"
  followup_path="$repository/_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md"
  deferred_path="$repository/_bmad-output/implementation-artifacts/deferred-work.md"
  evidence_root="$repository/_bmad-output/implementation-artifacts/evidence/story-1-20"
  write_story_guard() {
    local status="$1"
    mkdir -p "$(dirname "$story_path")"
    printf "%s\n" "---" "status: $status" "---" "" "# Story 1.20" "" \
      "Status: $status" > "$story_path"
  }
  write_sprint_guard() {
    local epic_status="$1"
    local story_status="$2"
    local updated="${3:-2026-07-17}"
    printf "%s\n" "# last_updated: $updated" "last_updated: $updated" > "$sprint_path"
    if test "$story_status" = in-progress; then
      printf "%s\n" \
        "# Story 1.19 review is complete. Story 1.20 remains in progress while:" \
        "# - fixture prerequisites remain under verification;" \
        "# \`final_decision: still blocked\` cannot transition this story or Epic 1 to done." \
        >> "$sprint_path"
    else
      printf "%s\n" \
        "# Story 1.20 owner-approved parity closure is complete; authorizing commit C" \
        "# verified every pinned artifact, approval, prerequisite, and migration decision." \
        >> "$sprint_path"
    fi
    printf "%s\n" "development_status:" "  epic-1: $epic_status" \
      "  1-20-owner-approved-parity-closure-and-runtime-pin: $story_status" >> "$sprint_path"
  }
  write_followup_guard() {
    local recommended="$1"
    mkdir -p "$(dirname "$followup_path")"
    printf "%s\n" "---" "followup_review_recommended: $recommended" "---" \
      "" "# Story 1.16" > "$followup_path"
    if test "$recommended" = false; then
      printf "%s\n" "" "### 2026-07-17 — Follow-up review disposition" "" \
        "- reviewed_runtime_sha: $runtime" \
        "- reviewer: Independent Reviewer" \
        "- durable_source: https://approvals.example/story-1-16" \
        "- scope: integrated lifecycle runtime" \
        "- findings_and_resolutions: all findings resolved" \
        "- verification: exact runtime gates passed" \
        "- residual_limitations: none" \
        "- disposition: approved" >> "$followup_path"
    fi
  }
  write_deferred_guard() {
    local status="$1"
    mkdir -p "$(dirname "$deferred_path")"
    printf "%s\n" \
      "# Deferred Work" "" \
      "## Deferred from: Story 1.20 fixture prerequisite" "" \
      "- source_spec: fixture" \
      "  status: $status" > "$deferred_path"
  }
  write_blocked_packet() {
    local documentation="${1:-null}"
    local decision="${2:-still blocked}"
    local authorize="${3:-false}"
    mkdir -p "$(dirname "$packet_path")"
    cat > "$packet_path" <<PACKET
---
candidate_source_sha: null
tested_runtime_sha: null
documentation_commit_sha: $documentation
evidence_manifest_sha256: null
raw_evidence_bundle_url: null
raw_evidence_bundle_sha256: null
eventstore_owner_approval_url: null
eventstore_owner_approval_sha256: null
release_owner_disposition_url: null
release_owner_disposition_sha256: null
approved_package_version: null
approved_package_hash_manifest_sha256: null
approved_container_repository: null
approved_container_digest: null
final_decision: $decision
authorize_consumer_migration: $authorize
---
# Evidence
PACKET
    for _ in $(seq 1 9); do
      echo "- classification: \`still blocked\`" >> "$packet_path"
    done
  }
  printf "stable runtime\n" > "$repository/src/runtime.txt"
  cat > "$repository/tools/release-packages.json" <<JSON
{"packages":[
{"id":"Hexalith.EventStore.Admin.Abstractions"},
{"id":"Hexalith.EventStore.Admin.Cli"},
{"id":"Hexalith.EventStore.Admin.Server"},
{"id":"Hexalith.EventStore.Aspire"},
{"id":"Hexalith.EventStore.Client"},
{"id":"Hexalith.EventStore.Contracts"},
{"id":"Hexalith.EventStore.DomainService"},
{"id":"Hexalith.EventStore.Gateway"},
{"id":"Hexalith.EventStore.RestApi.Generators"},
{"id":"Hexalith.EventStore.Server"},
{"id":"Hexalith.EventStore.ServiceDefaults"},
{"id":"Hexalith.EventStore.SignalR"},
{"id":"Hexalith.EventStore.Testing"},
{"id":"Hexalith.EventStore.Testing.Integration"}
]}
JSON
  write_story_guard blocked
  write_sprint_guard in-progress in-progress
  write_followup_guard true
  write_deferred_guard open-blocking
  write_blocked_packet
  git -C "$repository" add src/runtime.txt tools/release-packages.json _bmad-output
  git -C "$repository" commit -qm runtime
  runtime="$(git -C "$repository" rev-parse HEAD)"
  zeros="$(printf "0%.0s" {1..64})"
  raw_hash="$(printf "a%.0s" {1..64})"
  package_version=1.2.3
  package_ids=(
    Hexalith.EventStore.Admin.Abstractions
    Hexalith.EventStore.Admin.Cli
    Hexalith.EventStore.Admin.Server
    Hexalith.EventStore.Aspire
    Hexalith.EventStore.Client
    Hexalith.EventStore.Contracts
    Hexalith.EventStore.DomainService
    Hexalith.EventStore.Gateway
    Hexalith.EventStore.RestApi.Generators
    Hexalith.EventStore.Server
    Hexalith.EventStore.ServiceDefaults
    Hexalith.EventStore.SignalR
    Hexalith.EventStore.Testing
    Hexalith.EventStore.Testing.Integration
  )
  manifest_json='\''{"manifests":[{"platform":{"os":"linux","architecture":"amd64"}},{"platform":{"os":"linux","architecture":"arm64"}}]}'\''
  image_hash="$(printf %s "$manifest_json" | sha256sum | awk '\''{print $1}'\'')"
  write_evidence() {
    durable="$evidence_root/$runtime"
    rm -rf "$durable"
    mkdir -p "$durable"
    printf "%s\n" "{\"url\":\"https://evidence.example/raw.tgz\",\"sha256\":\"$raw_hash\"}" \
      > "$durable/raw-evidence-bundle.json"
    for package_id in "${package_ids[@]}"; do
      printf "%s  %s.%s.nupkg\n" "$zeros" "$package_id" "$package_version"
    done > "$durable/nuget-sha256.txt"
    package_manifest_hash="$(sha256sum "$durable/nuget-sha256.txt" | cut -d " " -f 1)"
    printf %s "$manifest_json" > "$durable/generated-image-index.json"
    printf %s "$manifest_json" > "$durable/container-manifest.json"
    printf "%s\n" "sha256:$image_hash" > "$durable/generated-image-index.digest.txt"
    printf "%s\n" linux/amd64 linux/arm64 > "$durable/container-platforms.txt"
    printf "%s\n" "<Project />" > "$durable/capture-generated-image-index.targets"
    printf "%s\n" 2026-07-17T12:00:00Z \
      > "$durable/release-owner-publication-authority.checked-at.txt"
    cat > "$durable/release-owner-publication-authority.json" <<JSON
{"action":"container-publication","owner":"Release Owner","authorized_at":"2026-07-17T11:00:00Z","durable_source":"https://approvals.example/publication","rationale":"proof fixture","expires_at":"2026-07-17T13:00:00Z","scope":{"repository":"registry.hexalith.com/eventstore","tag":"quarantine-proof-$runtime","source_sha":"$runtime"}}
JSON
    cat > "$durable/container-provenance.json" <<JSON
{"source_sha":"$runtime","repository":"registry.hexalith.com/eventstore","tag":"quarantine-proof-$runtime","digest":"sha256:$image_hash","manifest_sha256":"$image_hash","sdk_generated_index":{"file":"generated-image-index.json","digest":"sha256:$image_hash","capture_targets_file":"capture-generated-image-index.targets","capture_targets_sha256":"$(sha256sum "$durable/capture-generated-image-index.targets" | awk '\''{print $1}'\'')"},"platforms":["linux/amd64","linux/arm64"],"publish_properties":{"container_registry":"registry.hexalith.com","container_repository":"eventstore","container_image_tag":"quarantine-proof-$runtime","container_runtime_identifiers":"linux-x64;linux-arm64"},"publication_authority":{"file":"release-owner-publication-authority.json","sha256":"$(sha256sum "$durable/release-owner-publication-authority.json" | awk '\''{print $1}'\'')","checked_at":"2026-07-17T12:00:00Z","checked_at_file":"release-owner-publication-authority.checked-at.txt","checked_at_sha256":"$(sha256sum "$durable/release-owner-publication-authority.checked-at.txt" | awk '\''{print $1}'\'')"}}
JSON
    cat > "$durable/eventstore-owner-proof-approval.json" <<JSON
{"action":"story-1-20-proof-approval","owner":"EventStore Owner","approved_at":"2026-01-01T00:00:00Z","durable_source":"https://approvals.example/eventstore","accepted_scope":"complete proof fixture","limitations":[],"decision":"authorize-consumer-migration","scope":{"repository":"https://github.com/Hexalith/Hexalith.EventStore.git","tested_runtime_sha":"$runtime","raw_evidence_bundle_sha256":"$raw_hash","package_version":"$package_version","package_hash_manifest_sha256":"$package_manifest_hash","container_repository":"registry.hexalith.com/eventstore","container_digest":"sha256:$image_hash"}}
JSON
    cat > "$durable/release-owner-final-disposition.json" <<JSON
{"action":"story-1-20-release-disposition","owner":"Release Owner","approved_at":"2026-01-01T00:00:00Z","durable_source":"https://approvals.example/release","accepted_scope":"release identities fixture","limitations":[],"decision":"approve-release-identities","scope":{"repository":"https://github.com/Hexalith/Hexalith.EventStore.git","tested_runtime_sha":"$runtime","raw_evidence_bundle_sha256":"$raw_hash","package_version":"$package_version","package_hash_manifest_sha256":"$package_manifest_hash","container_repository":"registry.hexalith.com/eventstore","container_digest":"sha256:$image_hash"}}
JSON
    owner_hash="$(sha256sum "$durable/eventstore-owner-proof-approval.json" | cut -d " " -f 1)"
    release_hash="$(sha256sum "$durable/release-owner-final-disposition.json" | cut -d " " -f 1)"
    (
      cd "$durable"
      find . -maxdepth 1 -type f ! -name "critical-evidence-sha256.txt" -printf "%P\\0" \
        | LC_ALL=C sort -z | xargs -0 sha256sum --
    ) > "$durable/critical-evidence-sha256.txt"
    evidence_manifest_hash="$(sha256sum "$durable/critical-evidence-sha256.txt" | cut -d " " -f 1)"
  }
  write_complete_packet() {
    local documentation="$1"
    local decision="${2:-still blocked}"
    local authorize="${3:-false}"
    local candidate="${4:-$runtime}"
    local tested="${5:-$runtime}"
    mkdir -p "$(dirname "$packet_path")"
    cat > "$packet_path" <<PACKET
---
candidate_source_sha: $candidate
tested_runtime_sha: $tested
documentation_commit_sha: $documentation
evidence_manifest_sha256: $evidence_manifest_hash
raw_evidence_bundle_url: https://evidence.example/raw.tgz
raw_evidence_bundle_sha256: $raw_hash
eventstore_owner_approval_url: https://approvals.example/eventstore
eventstore_owner_approval_sha256: $owner_hash
release_owner_disposition_url: https://approvals.example/release
release_owner_disposition_sha256: $release_hash
approved_package_version: $package_version
approved_package_hash_manifest_sha256: $package_manifest_hash
approved_container_repository: registry.hexalith.com/eventstore
approved_container_digest: sha256:$image_hash
final_decision: $decision
authorize_consumer_migration: $authorize
---
# Evidence
PACKET
    for _ in $(seq 1 9); do
      echo "- classification: \`available\`" >> "$packet_path"
    done
  }
  write_followup_guard false
  write_deferred_guard implementation-complete/evidence-confirmed
  write_evidence
  write_complete_packet null
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm evidence-a
  evidence_a="$(git -C "$repository" rev-parse HEAD)"
  write_complete_packet "$evidence_a"
  git -C "$repository" add _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
  git -C "$repository" commit -qm pointer-b
  pointer_b="$(git -C "$repository" rev-parse HEAD)"
  write_complete_packet "$evidence_a" available true
  write_story_guard done
  write_sprint_guard done done 2026-07-18
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm authorization-c
  authorization_c="$(git -C "$repository" rev-parse HEAD)"
  (cd "$repository" && EVIDENCE_COMMIT_A="$evidence_a" POINTER_COMMIT_B="$pointer_b" \
    AUTHORIZATION_COMMIT_C="$authorization_c" \
    bash "$verification_script")

  finish_rejected_chain() {
    local rejected_a="$1"
    local label="$2"
    local c_date="${3:-2026-07-18}"
    local candidate="${4:-$runtime}"
    local tested="${5:-$runtime}"
    local rejected_b
    local rejected_c
    write_complete_packet "$rejected_a" "still blocked" false "$candidate" "$tested"
    git -C "$repository" add "$packet_path"
    git -C "$repository" commit -qm "$label-pointer-b"
    rejected_b="$(git -C "$repository" rev-parse HEAD)"
    write_complete_packet "$rejected_a" available true "$candidate" "$tested"
    write_story_guard done
    write_sprint_guard done done "$c_date"
    git -C "$repository" add _bmad-output
    git -C "$repository" commit -qm "$label-authorization-c"
    rejected_c="$(git -C "$repository" rev-parse HEAD)"
    ! (cd "$repository" && EVIDENCE_COMMIT_A="$rejected_a" \
      POINTER_COMMIT_B="$rejected_b" AUTHORIZATION_COMMIT_C="$rejected_c" \
      bash "$verification_script" >/dev/null 2>&1)
  }

  git -C "$repository" checkout -qb mode-b "$evidence_a"
  write_complete_packet "$evidence_a"
  chmod +x "$packet_path"
  git -C "$repository" add "$packet_path"
  git -C "$repository" commit -qm mode-b
  mode_b="$(git -C "$repository" rev-parse HEAD)"
  write_complete_packet "$evidence_a" available true
  write_story_guard done
  write_sprint_guard done done 2026-07-18
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm mode-c
  mode_c="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$evidence_a" POINTER_COMMIT_B="$mode_b" \
    AUTHORIZATION_COMMIT_C="$mode_c" bash "$verification_script" >/dev/null 2>&1)

  git -C "$repository" checkout -qb extra-c "$pointer_b"
  write_complete_packet "$evidence_a" available true
  write_story_guard done
  write_sprint_guard done done 2026-07-18
  printf "extra\n" > "$repository/_bmad-output/extra.txt"
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm extra-c
  extra_c="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$evidence_a" POINTER_COMMIT_B="$pointer_b" \
    AUTHORIZATION_COMMIT_C="$extra_c" \
    bash "$verification_script" >/dev/null 2>&1)

  git -C "$repository" checkout -qb missing-packet-a "$runtime"
  write_evidence
  git -C "$repository" add "$evidence_root"
  git -C "$repository" commit -qm missing-packet-a
  bad_a="$(git -C "$repository" rev-parse HEAD)"
  write_blocked_packet "$bad_a"
  git -C "$repository" add "$packet_path"
  git -C "$repository" commit -qm bad-pointer-b
  bad_b="$(git -C "$repository" rev-parse HEAD)"
  write_blocked_packet "$bad_a" available true
  write_story_guard done
  write_sprint_guard done done 2026-07-18
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm bad-authorization-c
  bad_c="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$bad_a" POINTER_COMMIT_B="$bad_b" \
    AUTHORIZATION_COMMIT_C="$bad_c" \
    bash "$verification_script" >/dev/null 2>&1)

  git -C "$repository" checkout -qb candidate-mismatch-a "$runtime"
  write_followup_guard false
  write_deferred_guard implementation-complete/evidence-confirmed
  write_evidence
  wrong_candidate=ffffffffffffffffffffffffffffffffffffffff
  write_complete_packet null "still blocked" false "$wrong_candidate" "$runtime"
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm candidate-mismatch-a
  mismatch_a="$(git -C "$repository" rev-parse HEAD)"
  finish_rejected_chain "$mismatch_a" candidate-mismatch 2026-07-18 \
    "$wrong_candidate" "$runtime"

  git -C "$repository" checkout -qb runtime-drift-a "$runtime"
  write_followup_guard false
  write_deferred_guard implementation-complete/evidence-confirmed
  write_evidence
  write_complete_packet null
  printf "drift\n" >> "$repository/src/runtime.txt"
  git -C "$repository" add src/runtime.txt _bmad-output
  git -C "$repository" commit -qm runtime-drift-a
  runtime_drift_a="$(git -C "$repository" rev-parse HEAD)"
  finish_rejected_chain "$runtime_drift_a" runtime-drift

  git -C "$repository" checkout -qb semantic-b "$evidence_a"
  write_complete_packet "$evidence_a"
  printf "semantic drift\n" >> "$packet_path"
  git -C "$repository" add "$packet_path"
  git -C "$repository" commit -qm semantic-b
  semantic_b="$(git -C "$repository" rev-parse HEAD)"
  write_complete_packet "$evidence_a" available true
  write_story_guard done
  write_sprint_guard done done 2026-07-18
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm semantic-b-c
  semantic_b_c="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$evidence_a" POINTER_COMMIT_B="$semantic_b" \
    AUTHORIZATION_COMMIT_C="$semantic_b_c" bash "$verification_script" >/dev/null 2>&1)

  git -C "$repository" checkout -qb non-direct-c "$pointer_b"
  printf "intermediate\n" > "$repository/_bmad-output/intermediate.txt"
  git -C "$repository" add _bmad-output/intermediate.txt
  git -C "$repository" commit -qm non-direct-intermediate
  write_complete_packet "$evidence_a" available true
  write_story_guard done
  write_sprint_guard done done 2026-07-18
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm non-direct-c
  non_direct_c="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$evidence_a" POINTER_COMMIT_B="$pointer_b" \
    AUTHORIZATION_COMMIT_C="$non_direct_c" bash "$verification_script" >/dev/null 2>&1)

  git -C "$repository" checkout -qb stale-approval-a "$runtime"
  write_followup_guard false
  write_deferred_guard implementation-complete/evidence-confirmed
  write_evidence
  jq '\''.approved_at = "2999-01-01T00:00:00Z"'\'' \
    "$durable/eventstore-owner-proof-approval.json" \
    > "$durable/eventstore-owner-proof-approval.mutated.json"
  mv "$durable/eventstore-owner-proof-approval.mutated.json" \
    "$durable/eventstore-owner-proof-approval.json"
  owner_hash="$(sha256sum "$durable/eventstore-owner-proof-approval.json" | cut -d " " -f 1)"
  (
    cd "$durable"
    find . -maxdepth 1 -type f ! -name "critical-evidence-sha256.txt" -printf "%P\\0" \
      | LC_ALL=C sort -z | xargs -0 sha256sum --
  ) > "$durable/critical-evidence-sha256.txt"
  evidence_manifest_hash="$(sha256sum "$durable/critical-evidence-sha256.txt" | cut -d " " -f 1)"
  write_complete_packet null
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm stale-approval-a
  stale_approval_a="$(git -C "$repository" rev-parse HEAD)"
  finish_rejected_chain "$stale_approval_a" stale-approval

  git -C "$repository" checkout -qb open-prerequisite-a "$runtime"
  write_followup_guard false
  write_deferred_guard open-blocking
  write_evidence
  write_complete_packet null
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm open-prerequisite-a
  open_prerequisite_a="$(git -C "$repository" rev-parse HEAD)"
  finish_rejected_chain "$open_prerequisite_a" open-prerequisite

  git -C "$repository" checkout -qb incomplete-review-a "$runtime"
  write_followup_guard true
  write_deferred_guard implementation-complete/evidence-confirmed
  write_evidence
  write_complete_packet null
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm incomplete-review-a
  incomplete_review_a="$(git -C "$repository" rev-parse HEAD)"
  finish_rejected_chain "$incomplete_review_a" incomplete-review

  original_runtime="$runtime"
  git -C "$repository" checkout -qb unrelated-package-runtime "$runtime"
  jq '\''.packages[0].id = "Unrelated.Package"'\'' \
    "$repository/tools/release-packages.json" \
    > "$repository/tools/release-packages.mutated.json"
  mv "$repository/tools/release-packages.mutated.json" \
    "$repository/tools/release-packages.json"
  git -C "$repository" add tools/release-packages.json
  git -C "$repository" commit -qm unrelated-package-runtime
  runtime="$(git -C "$repository" rev-parse HEAD)"
  write_followup_guard false
  write_deferred_guard implementation-complete/evidence-confirmed
  write_evidence
  write_complete_packet null
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm unrelated-package-a
  unrelated_package_a="$(git -C "$repository" rev-parse HEAD)"
  finish_rejected_chain "$unrelated_package_a" unrelated-package
  runtime="$original_runtime"

  git -C "$repository" checkout -qb missing-container-authority-a "$runtime"
  write_followup_guard false
  write_deferred_guard implementation-complete/evidence-confirmed
  write_evidence
  jq '\''del(.publication_authority)'\'' "$durable/container-provenance.json" \
    > "$durable/container-provenance.mutated.json"
  mv "$durable/container-provenance.mutated.json" "$durable/container-provenance.json"
  (
    cd "$durable"
    find . -maxdepth 1 -type f ! -name "critical-evidence-sha256.txt" -printf "%P\\0" \
      | LC_ALL=C sort -z | xargs -0 sha256sum --
  ) > "$durable/critical-evidence-sha256.txt"
  evidence_manifest_hash="$(sha256sum "$durable/critical-evidence-sha256.txt" | cut -d " " -f 1)"
  write_complete_packet null
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm missing-container-authority-a
  missing_container_authority_a="$(git -C "$repository" rev-parse HEAD)"
  finish_rejected_chain "$missing_container_authority_a" missing-container-authority

  git -C "$repository" checkout -qb regressing-date-c "$pointer_b"
  write_complete_packet "$evidence_a" available true
  write_story_guard done
  write_sprint_guard done done 2026-07-16
  git -C "$repository" add _bmad-output
  git -C "$repository" commit -qm regressing-date-c
  regressing_date_c="$(git -C "$repository" rev-parse HEAD)"
  ! (cd "$repository" && EVIDENCE_COMMIT_A="$evidence_a" POINTER_COMMIT_B="$pointer_b" \
    AUTHORIZATION_COMMIT_C="$regressing_date_c" \
    bash "$verification_script" >/dev/null 2>&1)
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: the extracted verifier and every packet Bash block parse. Structural mutation checks
require the immutable-harness comparison, GitHub role records, exact critical/raw evidence
inventories, AD-11/source-state parsing, approval subject and limitation binding, closed deferred
status, live registry child-manifest re-query, and all three body-level C transitions. Reachable
fixtures then execute the packet's exact xUnit result and identity contracts, rejecting skips,
failed children, incomplete totals, wrong assemblies, and incomplete full-run method listings;
execute the WORM contract, rejecting unauthenticated, unlocked, and sub-seven-year retention;
exercise valid and invalid pre-merge/official-main ancestry; and exercise both target container
platforms plus health and cleanup failure paths. Only after those fixtures pass does the terminal
exit run. The older synthetic graph remains after it solely as historical mutation provenance;
its pre-GitHub-API evidence schema is not executed.

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
