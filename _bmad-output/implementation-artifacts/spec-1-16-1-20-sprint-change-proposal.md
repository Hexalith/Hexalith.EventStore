---
title: 'Implement Story 1.16/1.20 Sprint Change Proposal'
type: 'chore'
created: '2026-07-16'
status: 'in-progress'
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
  in_front && $0 == "candidate_source_sha: 85877902f8d60a466ab90cd8b68b53838863db1c" { candidate++ }
  in_front && $0 == "tested_runtime_sha: null" { runtime++ }
  in_front && $0 == "documentation_commit_sha: null" { documentation++ }
  in_front && $0 == "final_decision: still blocked" { decision++ }
  in_front && $0 == "authorize_consumer_migration: false" { migration++ }
  END { exit !(candidate == 1 && runtime == 1 && documentation == 1 &&
    decision == 1 && migration == 1) }
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
  /^##(#)? / {
    active = ($0 in expected) ? $0 : ""
    if ($0 in expected) headings[$0]++
    next
  }
  active != "" && /^- classification: / {
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
  in_front && $0 == "followup_review_recommended: true" { flag++ }
  END { exit !(flag == 1) }
' _bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md

awk '
  NR == 1 && $0 == "---" { in_front = 1; next }
  in_front && $0 == "---" { in_front = 0; exit }
  in_front && $0 == "story_id: \"1.20\"" { story++ }
  in_front && $0 == "status: blocked" { status++ }
  END { exit !(story == 1 && status == 1) }
' _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md

awk '
  $0 == "# last_updated: 2026-07-16" { comment_date++ }
  $0 == "last_updated: 2026-07-16" { field_date++ }
  $0 == "  epic-1: in-progress" { epic++ }
  $0 == "  1-20-owner-approved-parity-closure-and-runtime-pin: in-progress" { story++ }
  END { exit !(comment_date == 1 && field_date == 1 && epic == 1 && story == 1) }
' _bmad-output/implementation-artifacts/sprint-status.yaml
```

Expected: exit 0. Story 1.16 has exactly one opening-front-matter true flag; the Story 1.20
file is exactly `blocked`; the sprint Story 1.20 and Epic 1 entries remain exactly
`in-progress`; both tracker dates remain `2026-07-16`.

```bash
bash -c '
  set -euo pipefail
  proposal_owned=(
    _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md
    _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
    _bmad-output/implementation-artifacts/epic-1-context.md
    _bmad-output/implementation-artifacts/spec-1-16-1-20-sprint-change-proposal.md
    _bmad-output/implementation-artifacts/sprint-status.yaml
  )
  concurrent=(
    references/Hexalith.FrontComposer
    src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs
    tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs
  )
  mapfile -d "" -t records < <(git status --porcelain=v1 -z)
  actual=()
  for record in "${records[@]}"; do actual+=("${record:3}"); done
  diff -u \
    <(printf "%s\n" "${proposal_owned[@]}" "${concurrent[@]}" | LC_ALL=C sort) \
    <(printf "%s\n" "${actual[@]}" | LC_ALL=C sort)
  git diff --quiet HEAD -- \
    _bmad-output/implementation-artifacts/deferred-work.md \
    _bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md
'
```

Expected: exit 0. The current worktree contains exactly five proposal-owned changes and the
three explicitly reported concurrent paths; the deferred ledger and Story 1.16 remain
byte-identical to `HEAD`.

```bash
bash -c '
  set -euo pipefail
  packet="$1"
  syntax_file="$(mktemp)"
  trap "rm -f -- \"$syntax_file\"" EXIT
  awk '\''/^```bash$/ { in_bash = 1; next }
    in_bash && /^```$/ { print ""; in_bash = 0; next }
    in_bash { print }'\'' "$packet" > "$syntax_file"
  bash -n "$syntax_file"
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
```

Expected: exit 0. Every packet Bash block parses, the extracted exact AD-11 function passes
the valid tuple, and each individual mismatch fails even when the function is invoked by `if`.

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
  for mutation in '\''.owner = " "'\'' '\''.expires_at = "2026-07-16T12:00:00Z"'\'' \
      '\''.scope.repository = "registry.invalid/eventstore"'\''; do
    jq "$mutation" "$authority_record" > "$mutated_record"
    AUTHORITY_EVIDENCE="$mutated_record"
    if validate_publication_authority "$checked_at" >/dev/null 2>&1; then
      printf "publication-authority false pass: %s\n" "$mutation" >&2
      exit 1
    fi
  done
' _ _bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md
```

Expected: exit 0. The extracted publication-authority predicate accepts the valid scoped,
unexpired fixture and rejects blank owner, action-time expiry, and wrong repository.

```bash
git status --short --branch
```

Expected: the same five proposal-owned and three concurrent paths are visible; no staged,
runtime, dependency, package, topology, submodule-content, Parties, or persisted-data edit is
attributed to this proposal.
