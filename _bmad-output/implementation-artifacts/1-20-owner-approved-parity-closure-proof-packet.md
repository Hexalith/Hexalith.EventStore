---
schema: hexalith.eventstore.parity-closure-proof-packet/v1
story_id: "1.20"
story_key: 1-20-owner-approved-parity-closure-and-runtime-pin
created: 2026-07-16T05:09:20+02:00
updated: 2026-07-17T07:05:30+02:00
historical_packet: 1-8-projection-query-sdk-owner-proof-packet.md
candidate_source_sha: 85877902f8d60a466ab90cd8b68b53838863db1c
tested_runtime_sha: null
documentation_commit_sha: null
final_decision: still blocked
authorize_consumer_migration: false
---

# Story 1.20 Owner-Approved Parity Closure Proof Packet

## Decision

`still blocked`

This packet authorizes no consumer migration and no removal of consumer-owned
projection, query, rebuild, freshness, erasure, or rollback infrastructure. It is the
successor to the historical Story 1.8 packet; it does not rewrite that packet's decision
or treat its historical runtime SHA as current evidence.

The exact-SHA gate failed before package/container publication and owner review:

1. Story 1.19 is now `done` with an approved review disposition, so the previously
   recorded Story 1.19 prerequisite blocker is resolved.
2. A clean detached checkout of
   `85877902f8d60a466ab90cd8b68b53838863db1c` passed the warning-free Release build and
   broad unit regression lanes, but the full live-sidecar lane failed 2 of 44 tests.
3. The named-projection normal-delivery lifecycle cleanup failure reproduced in an
   isolated class run and again as a single-method run. The candidate therefore is not
   an approvable tested runtime and no parity row is promoted to `available`.
4. The completed Story 1.16 source spec still retains
   `followup_review_recommended: true` without an explicit disposition.
5. The failed candidate was below architecture AD-11's security baseline. Current commit
   `772cdfefa8163704de0f57042af5b0507c1ac771` now passes the exact SDK `10.0.302`,
   ASP.NET `10.0.10`, and installed `Microsoft.NETCore.App` `10.0.10` preflight, but it
   remains non-authorizing because a corrected source-topology provenance gate fails.
6. No named EventStore owner has reviewed completed passing exact-SHA evidence, and no
   manifest-governed package/container identities have been produced or approved.

### Scoped corrective item

Keep the lifecycle-cleanup and AD-11 entries as resolved implementation history: current
commit `772cdfefa8163704de0f57042af5b0507c1ac771` passed the exact former lifecycle
failure, its complete six-test class, the 44-test live-sidecar lane, and the executable
AD-11 preflight. Story 2.7 now owns the reproducible source-topology provenance blocker:
remove or correctly scope stale `orders`/`inventory` sample registrations so one absent
binding cannot suppress the complete admin query-type index, then rerun the corrected
source-mode E2E. After that, rerun every production-path and package/container gate at one
unchanged SHA, explicitly disposition Story 1.16's retained follow-up recommendation,
obtain named EventStore-owner approval, and update this packet.

### Exact-SHA Failure And Readiness Re-Audit — Observed 2026-07-16T15:00:52+02:00

- repository identity observed at: `2026-07-16T15:00:52+02:00`
- repository: `https://github.com/Hexalith/Hexalith.EventStore.git`
- repository root: `/home/administrator/projects/hexalith/eventstore`
- planning/evidence branch observed: `main`
- planning/evidence HEAD observed: `02a93d5a0325dc842ad6a64897a3b8fb2907b9a9`
- tracking state observed: HEAD equalled locally recorded `origin/main`; no claim is made
  about an unfetched remote state
- failed candidate SHA: `85877902f8d60a466ab90cd8b68b53838863db1c`
- detached candidate worktree: clean before and after the attempted gates
- root submodules in the detached candidate: all seven declared entries initialized at
  their committed gitlinks; no nested submodules initialized
- tested runtime SHA: not selected

The planning/evidence HEAD observed above contains later merged lifecycle corrective work,
but it has not passed this packet's unchanged exact-SHA gates and is not selected as the
tested runtime. Its presence does not alter the failed candidate's historical result or
close the lifecycle-cleanup blocker.

The existing exact-SHA completion-attempt log remains the authoritative command-and-result
record. Its reproducible live-sidecar failure disqualifies the candidate despite the
passing Release build and test subsets; those subsets grant no migration authority.

Runtime and documentation identity remain deliberately separate. The
`candidate_source_sha` records the failed candidate, `tested_runtime_sha: null` records
that no unchanged runtime has passed every gate, and `documentation_commit_sha: null`
records that no later evidence-only commit exists. A future documentation commit cannot
substitute for the tested runtime. Package versions and hashes, container digest and
platform provenance, named owner approval, and migration authorization also remain absent.

Architecture AD-11 remains a hard executable precondition before selecting or publishing a
replacement runtime. At current commit `772cdfefa8163704de0f57042af5b0507c1ac771`,
`global.json`, the installed SDK/runtime, and the effective central ASP.NET pins satisfy
the exact `10.0.302` / `10.0.10` baseline. A later candidate must pass the same preflight,
or use a replacement recorded with the named architecture owner, approval date, durable
source, rationale, exact candidate/toolchain/ASP.NET/runtime scope, and an unexpired
`expires_at` value.
The mandatory executable preflight rejects a mismatched exact baseline and any missing,
blank, malformed, expired, or out-of-scope replacement record before candidate gates. The
separately owned corrective item is recorded in `deferred-work.md`; this evidence-only story
changes no runtime or package pin.

## Prerequisite And Review Ledger

| Prerequisite | Current status | Current evidence | Closure disposition |
| --- | --- | --- | --- |
| Story 1.2 platform provenance | `done` | `1-2-domain-query-routing-and-response-provenance.md` adopts the completed routing and route-aware provenance evidence. | Satisfied for sequencing; must still be rerun at the selected runtime SHA. |
| Story 1.14 erasure | `done` | July 15 story-ID crosswalk maps completed historical Story 1.9 implementation, persisted-path evidence, and review. | Satisfied for sequencing; evidence not promoted to this packet without exact-SHA rerun. |
| Story 1.15 coordinated batching | `done` | Crosswalk maps completed historical Story 1.10 implementation and review. | Satisfied for sequencing; evidence not promoted to this packet without exact-SHA rerun. |
| Story 1.16 lifecycle | `done` | Corrective commit `7b73a2f5cde990b0a026ec280f7620d067b3d110` is present in current commit `772cdfef...`; the former exact failing method, its six-test class, and the complete 44-test live-sidecar lane passed in a clean detached checkout. The retained source spec still says `followup_review_recommended: true`. | Implementation and current-runtime verification are complete; the named durable review disposition remains required before approval. |
| Story 1.17 asynchronous multi-projection dispatch | `done` | Crosswalk maps completed historical Story 1.12 and narrows acceptance to AD-19's normalized result. | Satisfied for sequencing; evidence not promoted to this packet without exact-SHA rerun. |
| Story 1.18 delivery idempotency | `done` | Crosswalk maps completed historical Story 1.13 production-path evidence. | Satisfied for sequencing; evidence not promoted to this packet without exact-SHA rerun. |
| Story 1.19 paged rebuild equivalence | `done` | Active Story 1.19 records approval after 13 in-scope patches, one explicit deferral, a 2,620-test Server pass, the real DAPR/Redis paged-rebuild pass, and a warning-free Release build. | Satisfied for sequencing; the paged-rebuild live test also passed at the current candidate SHA, but the cross-cutting live gate did not. |
| Architecture AD-11 security baseline | `implementation-complete/evidence-confirmed` | Commits `d6c849aaf8f77f967377f72b763bd44b3131a713`, `3a43d5e6151ebc51e945bf1b6cecda92fd198a09`, and `8c70efb08b1bf2fcd077ad930c5827d1ab1594da` are present in current commit `772cdfef...`; the executable preflight observed SDK `10.0.302`, ASP.NET `10.0.10`, and installed `Microsoft.NETCore.App` `10.0.10`. | Satisfied for the current readiness audit. Every later candidate must still pass the executable preflight unchanged. |
| Source-topology query provenance | `open-blocking` | The packet's package-mode build omits the conditional `tenants` resource. With the harness corrected to source mode, `QueryResponseProvenanceE2ETests.LiveHandlerRoute_WithCurrentProjectionValidator_NeutralizesProjectionEvidence` failed twice at current commit `772cdfef...` with HTTP 404 / `query_projection_missing`. Base `appsettings.json` still registers `orders` and `inventory`, while the current sample hosts only `counter` and `greeting`; the missing bindings make `AdminOperationalIndexHostedService` skip all index writes, including `admin:query-types:tenants`. | **Hard blocker owned by Story 2.7.** Correct the registration/index boundary, retain fail-closed semantics for genuine metadata failures, and prove the handler route in the live source topology before candidate selection. |
| Story 1.20 owner review | pending | No reviewer, approval date, or durable source exists. | **Hard blocker.** |

## Artifact Identity Pin

The identities below are deliberately separate. A value in one identity domain never
substitutes for a value in another.

### Source identity

- candidate checkout SHA: `85877902f8d60a466ab90cd8b68b53838863db1c`
- approved/tested runtime SHA: not selected
- documentation-only commit SHA: not applicable yet
- clean-tree proof before gates: passed in the detached checkout, including all seven
  root-declared submodules and ignored-input rejection
- clean-tree proof after the failed gate: passed for regular/untracked inputs; generated
  Release outputs remained ignored and were not treated as source inputs
- approval status: pending proof-result review

`85877902f8d60a466ab90cd8b68b53838863db1c` is a failed candidate only. It is not an
approved source pin and does not authorize a consumer gitlink or checkout update.

### Documentation Identity Without Self-Reference

Future closure uses two documentation commits and keeps every intermediate state fail
closed:

1. Final EventStore and release-owner approvals first exist in durable external sources.
   They bind the tested runtime, package hashes, image digest/platform provenance,
   limitations, migration decision, and approved evidence content.
2. Evidence commit **A** is the single-parent child of the equal candidate/tested runtime,
   changes only `_bmad-output/`, and records the completed results and those durable approval
   references. It leaves `documentation_commit_sha: null`, `final_decision: still blocked`, and
   `authorize_consumer_migration: false`; A alone grants no authority.
3. Pointer-only commit **B**, whose direct parent is A, changes only
   `documentation_commit_sha` from `null` to A's 40-hex SHA. The field identifies A, never B,
   so neither commit self-references. Any other semantic or file change invalidates B.
4. The executable structural procedure below verifies A's equal candidate/tested-runtime pin,
   sole parent, and evidence-only path boundary; B's direct parent and pointer to A; and the
   exact one-field packet change. Only after it passes may a separate later status-only
   transition be considered; B itself changes no decision, migration authorization, story
   status, or sprint status.

### NuGet package identities

`tools/release-packages.json` currently names exactly 14 package IDs. No closure package
build was produced, so every version and SHA-256 hash is unresolved.

| Exact package ID | Approved version | SHA-256 |
| --- | --- | --- |
| `Hexalith.EventStore.Contracts` | not selected | not produced |
| `Hexalith.EventStore.Client` | not selected | not produced |
| `Hexalith.EventStore.Server` | not selected | not produced |
| `Hexalith.EventStore.SignalR` | not selected | not produced |
| `Hexalith.EventStore.Testing` | not selected | not produced |
| `Hexalith.EventStore.Testing.Integration` | not selected | not produced |
| `Hexalith.EventStore.Aspire` | not selected | not produced |
| `Hexalith.EventStore.ServiceDefaults` | not selected | not produced |
| `Hexalith.EventStore.DomainService` | not selected | not produced |
| `Hexalith.EventStore.RestApi.Generators` | not selected | not produced |
| `Hexalith.EventStore.Gateway` | not selected | not produced |
| `Hexalith.EventStore.Admin.Abstractions` | not selected | not produced |
| `Hexalith.EventStore.Admin.Cli` | not selected | not produced |
| `Hexalith.EventStore.Admin.Server` | not selected | not produced |

### Container identity

- configured candidate repository: `registry.hexalith.com/eventstore`
- approved immutable digest: not selected
- approved platform set: not selected
- release provenance mapping to approved source SHA: absent

The configured repository name is not a deployed identity. A tag, repository, source SHA,
or package version cannot substitute for an immutable image digest and its platform set.

## Candidate Environment Inventory

Captured from the failed exact-SHA gate environment; these values do not identify an
approved runtime:

- OS: Ubuntu 26.04, Linux `x64`
- .NET SDK: `10.0.301` (`96856fd726`)
- .NET host/runtime: `10.0.9` (`901ca94124`)
- Dapr CLI: `1.18.0`
- Dapr runtime: `1.18.1`
- Docker Engine: `29.4.3`
- root submodules: all seven root-declared `references/` submodules were initialized at
  their committed gitlinks in the disposable checkout; no nested submodule was initialized
- Redis/DAPR production-path lane: started by the live-sidecar fixture; persisted erasure,
  batch, cutover, and paged-rebuild tests passed, while named-delivery lifecycle cleanup failed

## Mandatory Exact-SHA Gate Harness

The following repository-root procedure is mandatory before any capability command below.
It creates a detached disposable checkout containing only committed inputs, initializes
only root-declared `references/` submodules, and rejects untracked or ignored inputs before
restore. Before restore, build, test, package, or publication, it accepts only the exact AD-11
baseline or a durable, in-scope, unexpired replacement record; because the AD-11 ASP.NET pins are
now centrally owned by `Hexalith.Builds`, the check first checks out only that submodule to
resolve the effective pins.
It then uses an isolated NuGet/Dotnet cache and defines runners that cannot credit stale
Release assemblies or zero-match filters.

```bash
set -euo pipefail
: "${CANDIDATE_SHA:?export the selected 40-hex EventStore commit SHA}"
[[ "$CANDIDATE_SHA" =~ ^[0-9a-f]{40}$ ]]

SOURCE_REPOSITORY="$(git rev-parse --show-toplevel)"
GATE_ROOT="$(mktemp -d)"
CHECKOUT="$GATE_ROOT/eventstore"
EVIDENCE_ROOT="$GATE_ROOT/evidence"
mkdir -p "$EVIDENCE_ROOT"
git -C "$SOURCE_REPOSITORY" worktree add --detach "$CHECKOUT" "$CANDIDATE_SHA"
cd "$CHECKOUT"

assert_candidate_identity() {
  test "$(git rev-parse --verify --end-of-options 'HEAD^{commit}')" = "$CANDIDATE_SHA"
}

assert_candidate_identity

# Fail closed on AD-11 before any candidate build or publication-capable gate.
AD11_CHECKED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
REPOSITORY_URL="$(git config --get remote.origin.url)"
REPOSITORY_SDK_VERSION="$(jq -er '.sdk.version | select(type == "string" and test("\\S"))' global.json)"
REPOSITORY_SDK_ROLL_FORWARD="$(jq -er '.sdk.rollForward | select(type == "string" and test("\\S"))' global.json)"
INSTALLED_SDK_VERSION="$(dotnet --version)"
# AD-11 ASP.NET pins are centrally owned by Hexalith.Builds (no longer redefined
# locally), so resolve the effective versions from the import graph. Checking out
# only the Builds submodule is a source checkout, not a restore/build/publish gate.
git submodule update --init --checkout -- references/Hexalith.Builds
ASPNET_PIN_SOURCES=(references/Hexalith.Builds/Props/Directory.Packages.props Directory.Packages.props)
discover_effective_aspnet_pin_pairs() {
  sed -nE 's/.*<PackageVersion (Include|Update)="(Microsoft\.AspNetCore\.[^"]+)" Version="([^"]+)" *\/>.*/\2 \3/p' \
    "$@" |
    awk '{ effective[$1] = $2 } END { for (id in effective) print id, effective[id] }' |
    LC_ALL=C sort
}

validate_aspnet_pin_band() {
  local pair
  local package_id
  local version
  local identity_count=0
  local -a regular_versions=()
  local -a unique_versions=()
  for pair in "$@"; do
    read -r package_id version <<< "$pair"
    test -n "$package_id" && test -n "$version" || return 1
    if test "$package_id" = 'Microsoft.AspNetCore.Identity'; then
      test "$version" = '2.3.11' || return 1
      identity_count=$((identity_count + 1))
    else
      [[ "$version" =~ ^10\.[0-9]+\.[0-9]+$ ]] || return 1
      regular_versions+=("$version")
    fi
  done
  test "$identity_count" -eq 1 || return 1
  test "${#regular_versions[@]}" -gt 0 || return 1
  mapfile -t unique_versions < <(printf '%s\n' "${regular_versions[@]}" | LC_ALL=C sort -u)
  test "${#unique_versions[@]}" -eq 1 || return 1
  printf '%s\n' "${unique_versions[0]}"
}

mapfile -t ASPNET_PIN_PAIRS < <(discover_effective_aspnet_pin_pairs "${ASPNET_PIN_SOURCES[@]}")
REPOSITORY_ASPNET_VERSION="$(validate_aspnet_pin_band "${ASPNET_PIN_PAIRS[@]}")"
dotnet --list-runtimes > "$EVIDENCE_ROOT/dotnet-runtimes.txt"
discover_bound_runtime_versions() {
  local runtime_inventory="$1"
  local repository_version="$2"
  local installed_aspnet
  local installed_runtime
  installed_aspnet="$(awk -v version="$repository_version" '
    $1 == "Microsoft.AspNetCore.App" && $2 == version { print $2; exit }
  ' "$runtime_inventory")"
  installed_runtime="$(awk -v version="$repository_version" '
    $1 == "Microsoft.NETCore.App" && $2 == version { print $2; exit }
  ' "$runtime_inventory")"
  test "$installed_aspnet" = "$repository_version" || return 1
  test "$installed_runtime" = "$repository_version" || return 1
  printf '%s\n' "$installed_aspnet" "$installed_runtime"
}
mapfile -t BOUND_RUNTIME_VERSIONS < <(
  discover_bound_runtime_versions "$EVIDENCE_ROOT/dotnet-runtimes.txt" \
    "$REPOSITORY_ASPNET_VERSION"
)
test "${#BOUND_RUNTIME_VERSIONS[@]}" -eq 2
INSTALLED_ASPNET_VERSION="${BOUND_RUNTIME_VERSIONS[0]}"
INSTALLED_RUNTIME_VERSION="${BOUND_RUNTIME_VERSIONS[1]}"

ad11_exact_baseline() {
  test "$REPOSITORY_SDK_VERSION" = '10.0.302' &&
    test "$REPOSITORY_SDK_ROLL_FORWARD" = 'latestPatch' &&
    test "$INSTALLED_SDK_VERSION" = '10.0.302' &&
    test "$REPOSITORY_ASPNET_VERSION" = '10.0.10' &&
    test "$INSTALLED_ASPNET_VERSION" = "$REPOSITORY_ASPNET_VERSION" &&
    test "$INSTALLED_RUNTIME_VERSION" = "$REPOSITORY_ASPNET_VERSION"
}

validate_ad11_replacement() {
  local checked_at="$1"
  local replacement_record="$2"
  jq -e \
    --arg checked_at "$checked_at" \
    --arg repository "$REPOSITORY_URL" \
    --arg source_sha "$CANDIDATE_SHA" \
    --arg sdk_version "$REPOSITORY_SDK_VERSION" \
    --arg sdk_roll_forward "$REPOSITORY_SDK_ROLL_FORWARD" \
    --arg installed_sdk "$INSTALLED_SDK_VERSION" \
    --arg aspnet_version "$REPOSITORY_ASPNET_VERSION" \
    --arg installed_aspnet "$INSTALLED_ASPNET_VERSION" \
    --arg runtime_version "$INSTALLED_RUNTIME_VERSION" '
      def nonblank: type == "string" and test("\\S");
      def version_parts: split(".") | map(tonumber);
      type == "object" and
      .action == "ad11-baseline-replacement" and
      (.owner | nonblank) and
      (.approved_at | nonblank) and
      (.durable_source | nonblank) and
      (.rationale | nonblank) and
      (.expires_at | nonblank) and
      (.scope | type == "object") and
      .scope.repository == $repository and
      .scope.source_sha == $source_sha and
      .scope.sdk_version == $sdk_version and
      .scope.sdk_roll_forward == $sdk_roll_forward and
      .scope.sdk_version == $installed_sdk and
      .scope.aspnet_version == $aspnet_version and
      .scope.aspnet_version == $installed_aspnet and
      .scope.runtime_version == $runtime_version and
      $runtime_version == $aspnet_version and
      (($sdk_version | version_parts) >= ("10.0.302" | version_parts)) and
      (($aspnet_version | version_parts) >= ("10.0.10" | version_parts)) and
      ((($sdk_version | version_parts) > ("10.0.302" | version_parts)) or
       (($aspnet_version | version_parts) > ("10.0.10" | version_parts))) and
      ((.approved_at | fromdateiso8601) <= ($checked_at | fromdateiso8601)) and
      ((.expires_at | fromdateiso8601) > ($checked_at | fromdateiso8601))
    ' "$replacement_record"
}

AD11_PREFLIGHT="$EVIDENCE_ROOT/ad11-preflight.json"
AD11_REPLACEMENT_EVIDENCE="$EVIDENCE_ROOT/ad11-replacement-authority.json"
if ad11_exact_baseline; then
  jq -n \
    --arg checked_at "$AD11_CHECKED_AT" \
    --arg repository "$REPOSITORY_URL" \
    --arg source_sha "$CANDIDATE_SHA" \
    --arg sdk_version "$REPOSITORY_SDK_VERSION" \
    --arg sdk_roll_forward "$REPOSITORY_SDK_ROLL_FORWARD" \
    --arg installed_sdk_version "$INSTALLED_SDK_VERSION" \
    --arg aspnet_version "$REPOSITORY_ASPNET_VERSION" \
    --arg installed_aspnet_version "$INSTALLED_ASPNET_VERSION" \
    --arg runtime_version "$INSTALLED_RUNTIME_VERSION" \
    '{mode: "exact-baseline", checked_at: $checked_at, repository: $repository,
      source_sha: $source_sha, sdk_version: $sdk_version,
      sdk_roll_forward: $sdk_roll_forward, installed_sdk_version: $installed_sdk_version,
      aspnet_version: $aspnet_version, installed_aspnet_version: $installed_aspnet_version,
      runtime_version: $runtime_version}' > "$AD11_PREFLIGHT"
else
  : "${AD11_REPLACEMENT_RECORD:?exact AD-11 baseline absent; set durable replacement JSON path}"
  test -s "$AD11_REPLACEMENT_RECORD"
  test ! -e "$AD11_REPLACEMENT_EVIDENCE"
  cp -- "$AD11_REPLACEMENT_RECORD" "$AD11_REPLACEMENT_EVIDENCE"
  chmod a-w "$AD11_REPLACEMENT_EVIDENCE"
  validate_ad11_replacement "$AD11_CHECKED_AT" "$AD11_REPLACEMENT_EVIDENCE"
  AD11_REPLACEMENT_SHA256="$(sha256sum "$AD11_REPLACEMENT_EVIDENCE" | awk '{print $1}')"
  jq -n \
    --arg checked_at "$AD11_CHECKED_AT" \
    --arg repository "$REPOSITORY_URL" \
    --arg source_sha "$CANDIDATE_SHA" \
    --arg sdk_version "$REPOSITORY_SDK_VERSION" \
    --arg sdk_roll_forward "$REPOSITORY_SDK_ROLL_FORWARD" \
    --arg installed_sdk_version "$INSTALLED_SDK_VERSION" \
    --arg aspnet_version "$REPOSITORY_ASPNET_VERSION" \
    --arg installed_aspnet_version "$INSTALLED_ASPNET_VERSION" \
    --arg runtime_version "$INSTALLED_RUNTIME_VERSION" \
    --arg authority_file "$(basename "$AD11_REPLACEMENT_EVIDENCE")" \
    --arg authority_sha256 "$AD11_REPLACEMENT_SHA256" \
    '{mode: "durable-replacement", checked_at: $checked_at, repository: $repository,
      source_sha: $source_sha, sdk_version: $sdk_version,
      sdk_roll_forward: $sdk_roll_forward, installed_sdk_version: $installed_sdk_version,
      aspnet_version: $aspnet_version, installed_aspnet_version: $installed_aspnet_version,
      runtime_version: $runtime_version, authority_file: $authority_file,
      authority_sha256: $authority_sha256}' > "$AD11_PREFLIGHT"
fi
chmod a-w "$AD11_PREFLIGHT"
sha256sum "$AD11_PREFLIGHT" > "$EVIDENCE_ROOT/ad11-preflight.sha256"

mapfile -t ROOT_SUBMODULES < <(
  git config -f .gitmodules --get-regexp '^submodule\..*\.path$' |
    awk '$2 ~ /^references\// { print $2 }'
)
test "${#ROOT_SUBMODULES[@]}" -gt 0
git submodule update --init --checkout -- "${ROOT_SUBMODULES[@]}"
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"

for repository in . "${ROOT_SUBMODULES[@]}"; do
  test -z "$(git -C "$repository" status --porcelain=v1 --untracked-files=all)"
  test -z "$(git -C "$repository" status --porcelain=v1 --ignored=matching --untracked-files=all)"
done

export DOTNET_CLI_HOME="$GATE_ROOT/dotnet-home"
export NUGET_PACKAGES="$GATE_ROOT/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$GATE_ROOT/nuget-http-cache"
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH"

fresh_release() {
  local project="$1"
  local use_project_references="${2:-false}"
  local project_directory="${project%/*}"
  local gate_status
  assert_candidate_identity
  if rm -rf "$project_directory/bin/Release" "$project_directory/obj/Release" &&
    dotnet restore "$project" \
      --configfile nuget.config \
      --packages "$NUGET_PACKAGES" \
      -p:UseHexalithProjectReferences="$use_project_references" \
      -p:NuGetAudit=false \
      -p:MinVerVersionOverride=1.0.0 &&
    dotnet build "$project" \
      --configuration Release \
      --no-restore \
      -m:1 \
      -p:UseHexalithProjectReferences="$use_project_references" \
      -p:NuGetAudit=false \
      -p:MinVerVersionOverride=1.0.0; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_identity
  return "$gate_status"
}

run_xunit_class() {
  local assembly="$1"
  local class_name="$2"
  local evidence_name="$3"
  local methods="$EVIDENCE_ROOT/$evidence_name.methods.txt"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  local gate_status
  assert_candidate_identity
  if test -f "$assembly" &&
    dotnet "$assembly" -noColor -list methods -class "$class_name" | tee "$methods" &&
    grep -Fq "$class_name." "$methods" &&
    dotnet "$assembly" -noColor -class "$class_name" -xml "$results" &&
    grep -Eq 'total="[1-9][0-9]*"' "$results"; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_identity
  return "$gate_status"
}

run_xunit_method() {
  local assembly="$1"
  local method_name="$2"
  local evidence_name="$3"
  local methods="$EVIDENCE_ROOT/$evidence_name.methods.txt"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  local gate_status
  assert_candidate_identity
  if test -f "$assembly" &&
    dotnet "$assembly" -noColor -list methods -method "$method_name" | tee "$methods" &&
    grep -Fxq "$method_name" "$methods" &&
    dotnet "$assembly" -noColor -method "$method_name" -xml "$results" &&
    grep -Eq 'total="[1-9][0-9]*"' "$results"; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_identity
  return "$gate_status"
}

run_xunit_all() {
  local assembly="$1"
  local evidence_name="$2"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  local gate_status
  assert_candidate_identity
  if test -f "$assembly" &&
    dotnet "$assembly" -noColor -xml "$results" &&
    grep -Eq 'total="[1-9][0-9]*"' "$results"; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_identity
  return "$gate_status"
}
```

The xUnit v3 in-process runner returns exit code zero when a filter matches zero tests.
The `-list methods` plus `grep` checks reject that case before execution, and each XML
result must report a positive `total`. `fresh_release` deletes Release `bin`/`obj`,
restores through the committed `nuget.config` into a new cache, and builds before an
assembly is invoked. After all gates, the runtime-source proof is:

```bash
assert_candidate_identity
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"
```

Ignored `bin`/`obj` evidence output is expected only after the pre-gate ignored-input
check; it is never used as a source or configuration input.

## Parity Capability Matrix

All rows remain `still blocked`. Current source and test paths identify the evidence that
must be rerun; they are not accepted as exact-SHA closure evidence in this packet.

### Read-model and projection-checkpoint erasure

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Client/Projections/IReadModelConditionalEraser.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionEraseCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionReadModelAddressFactory.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs`
  - `src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionLifecycleGateway.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionEraseCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Actors/ProjectionLifecycleActorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionLifecycleGatewayTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionEraseLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
  Hexalith.EventStore.Client.Tests.Projections.DaprReadModelStoreTests \
  erasure-dapr-store

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionEraseCoordinatorTests \
  erasure-coordinator
run_xunit_class \
  tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
  Hexalith.EventStore.Server.Tests.Actors.ProjectionLifecycleActorTests \
  erasure-lifecycle-admission
run_xunit_class \
  tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionLifecycleGatewayTests \
  erasure-lifecycle-transport

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionEraseLiveSidecarTests \
  erasure-live
```

- closure result and persisted read-back: four exact-SHA live erasure tests passed, but
  the result is not promoted because the cross-cutting live gate failed and owner review
  has not occurred
- limitations requiring owner disposition: admission/write-fence races, caller-owned slot
  completeness, authenticated stitched E2E, and global-administrator boundary
- rollback: retain the consumer erasure and checkpoint-reset implementation

### Coordinated batching

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Client/Projections/IReadModelBatchStore.cs`
  - `src/Hexalith.EventStore.Client/Projections/ReadModelBatchProtocol.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelBatchStateAccessor.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelBatchTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/InMemoryReadModelStoreTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelBatchStoreTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
for class_name in \
  Hexalith.EventStore.Client.Tests.Projections.DaprReadModelBatchTests \
  Hexalith.EventStore.Client.Tests.Projections.DaprReadModelStoreTests \
  Hexalith.EventStore.Client.Tests.Projections.InMemoryReadModelStoreTests \
  Hexalith.EventStore.Client.Tests.Projections.ReadModelBatchStoreTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
    "$class_name" \
    "batch-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ReadModelBatchLiveSidecarTests \
  batch-live
```

- closure result and persisted read-back: six exact-SHA live batch tests passed, including
  detail/index/receipt state, abort restoration, checkpoint immutability, cancellation,
  and same-identity convergence; the result remains non-authorizing because the
  cross-cutting live gate failed
- limitations requiring owner disposition: store transaction capability, ETag behavior,
  corrupt/abandoned envelopes, terminal receipt retention, and cross-profile boundaries
- rollback: retain the consumer's coordinated detail/index write path and retry protocol

### Six-state lifecycle and route-bound provenance

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecycleState.cs`
  - `src/Hexalith.EventStore.Contracts/Queries/ProjectionLifecyclePolicy.cs`
  - `src/Hexalith.EventStore.Contracts/Queries/QueryResponseProvenance.cs`
  - `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs`
  - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
  - `src/Hexalith.EventStore/Controllers/QueriesController.cs`
  - `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`
  - `src/Hexalith.EventStore.Server/Actors/CachingProjectionActor.cs`
  - `src/Hexalith.EventStore.RestApi.Generators/RestApiControllerEmitter.cs`
- test paths:
  - `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`
  - `tests/Hexalith.EventStore.Contracts.Tests/Queries/SubmitQueryResponseTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelFreshnessTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`
  - `tests/Hexalith.EventStore.QueryRouting.Tests/HandlerAwareQueryRouterTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Integration/QueryResponseProvenancePersistenceTests.cs`
  - `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`
  - `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiGeneratedControllerErrorSemanticsTests.cs`
  - `tests/Hexalith.EventStore.Sample.Tests/SampleApi/SampleApiGeneratedControllerRuntimeTests.cs`
  - `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryResponseProvenanceE2ETests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj
for class_name in \
  Hexalith.EventStore.Contracts.Tests.Queries.ProjectionAdapterContractTests \
  Hexalith.EventStore.Contracts.Tests.Queries.SubmitQueryResponseTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll \
    "$class_name" \
    "lifecycle-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
for class_name in \
  Hexalith.EventStore.Client.Tests.Projections.ReadModelFreshnessTests \
  Hexalith.EventStore.Client.Tests.Gateway.EventStoreGatewayClientTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
    "$class_name" \
    "lifecycle-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll \
  Hexalith.EventStore.QueryRouting.Tests.HandlerAwareQueryRouterTests \
  lifecycle-query-routing

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Queries.QueryRouterTests \
  Hexalith.EventStore.Server.Tests.Controllers.QueriesControllerTests \
  Hexalith.EventStore.Server.Tests.Actors.CachingProjectionActorTests \
  Hexalith.EventStore.Server.Tests.Integration.QueryResponseProvenancePersistenceTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "lifecycle-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj
for class_name in \
  Hexalith.EventStore.RestApi.Generators.Tests.RestApiControllerGenerationTests \
  Hexalith.EventStore.RestApi.Generators.Tests.RestApiGeneratedControllerErrorSemanticsTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll \
    "$class_name" \
    "lifecycle-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Sample.Tests/bin/Release/net10.0/Hexalith.EventStore.Sample.Tests.dll \
  Hexalith.EventStore.Sample.Tests.SampleApi.SampleApiGeneratedControllerRuntimeTests \
  lifecycle-sample-runtime

fresh_release tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj true
run_xunit_class \
  tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll \
  Hexalith.EventStore.IntegrationTests.ContractTests.QueryResponseProvenanceE2ETests \
  lifecycle-provenance-e2e
```

- closure result and persisted read-back: contract, client, query-routing, server,
  generator, and Sample regression projects passed at the candidate SHA; the required
  query-provenance E2E lane was not reached after the live regression failure
- compatibility decision awaiting approval: preserve `Unknown`, all six operational values,
  the legacy metadata ABI, and the rule that ETag never supplies lifecycle or version
- rollback: retain consumer freshness adapters and fail closed on non-projection evidence

### Duplicate and out-of-order production-handler safety

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryIdempotencyCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryReconciler.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionDeliveryCutover.cs`
  - `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryRetryScheduler.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionActivationOutbox.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryStateStore.cs`
  - `docs/operations/projection-delivery-v2-evidence.md`
- test paths:
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryIdempotencyCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryReconcilerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryCutoverTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionDeliveryRetrySchedulerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionActivationOutboxTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionDeliveryCutoverLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionDeliveryIdempotencyCoordinatorTests \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionDeliveryReconcilerTests \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionDeliveryCutoverTests \
  Hexalith.EventStore.Server.Tests.Projections.NamedProjectionDispatchCoordinatorTests \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionDeliveryRetrySchedulerTests \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionActivationOutboxTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "delivery-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.NamedProjectionDispatchLiveSidecarTests \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionDeliveryCutoverLiveSidecarTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
    "$class_name" \
    "delivery-${class_name##*.}"
done
```

- closure result and persisted read-back: FAILED at the candidate SHA. The full live lane
  failed two named-dispatch tests; the isolated six-method class retained the normal-delivery
  lifecycle record (5 passed, 1 failed), and the single-method run reproduced it (0 passed,
  1 failed). The candidate cannot close duplicate/order production-handler safety.
- compatibility decision awaiting approval: v2 protects exact named routes; legacy
  `/project` remains outside the v2 no-mixed-writer guarantee
- rollback: preserve the complete pre-cutover fleet and backup boundary; never roll one
  writer back across the global v2 marker

### Full paged-rebuild equivalence

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionLifecycleGateway.cs`
  - `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`
- test paths:
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildProductionPathTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildCheckpointStoreTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Actors/ProjectionLifecycleActorTests.cs`
  - `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.DomainProjectionDispatcherV2Tests \
  rebuild-domain-dispatcher

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionRebuildProductionPathTests \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionUpdateOrchestratorTests \
  Hexalith.EventStore.Server.Tests.Projections.ProjectionRebuildCheckpointStoreTests \
  Hexalith.EventStore.Server.Tests.Actors.ProjectionLifecycleActorTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "rebuild-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_method \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.NamedProjectionDispatchLiveSidecarTests.PagedRebuild_MoreThanTwoPages_PersistsEquivalentRedisActorDetailIndexAndCheckpoints \
  rebuild-live-persisted-equivalence
```

- closure result and persisted read-back: the real DAPR/Redis multi-page rebuild test
  passed at the candidate SHA and Story 1.19 review is closed, but the enclosing
  named-dispatch live gate failed and therefore cannot be approved as a complete lane
- limitations requiring owner disposition: complete-prefix memory/byte bounds, coordinated
  promotion boundary, cancellation/resume semantics, and every deferred Story 1.19 review item
- rollback: retain the last complete live model and consumer rebuild path; never promote
  page-only or incomplete staged output

### Cursor compatibility

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`
  - `src/Hexalith.EventStore.Client/Queries/QueryCursorCodec.cs`
  - `src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs`
  - `src/Hexalith.EventStore.Client/Registration/QueryCursorCodecServiceCollectionExtensions.cs`
  - `src/Hexalith.EventStore.DomainService/EventStoreDataProtectionServiceCollectionExtensions.cs`
  - `src/Hexalith.EventStore.DomainService/DaprXmlRepository.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Queries/QueryCursorCodecTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Queries/QueryCursorScopeTests.cs`
  - `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDataProtectionTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
for class_name in \
  Hexalith.EventStore.Client.Tests.Queries.QueryCursorCodecTests \
  Hexalith.EventStore.Client.Tests.Queries.QueryCursorScopeTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
    "$class_name" \
    "cursor-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.EventStoreDataProtectionTests \
  cursor-persisted-key-ring
```

- closure result and persisted read-back: Client and DomainService regression projects
  passed at the candidate SHA; the result remains non-authorizing pending the failed live
  gate, release identities, and owner review
- limitation: routine retained-key rotation is distinct from key-ring loss/replacement
- rollback: preserve opaque cursor handling and the existing consumer compatibility path

### Asynchronous projection persistence

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.DomainService/IAsyncDomainProjectionHandler.cs`
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore.DomainService/ReadModelBatchProjectionResultMapper.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionDeliveryRetryScheduler.cs`
  - `src/Hexalith.EventStore.Server/Projections/DaprProjectionActivationOutbox.cs`
- test paths:
  - `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionDeliveryRetrySchedulerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionActivationOutboxTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.DomainProjectionDispatcherV2Tests \
  async-domain-dispatcher

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Projections.NamedProjectionDispatchCoordinatorTests \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionDeliveryRetrySchedulerTests \
  Hexalith.EventStore.Server.Tests.Projections.DaprProjectionActivationOutboxTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "async-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.NamedProjectionDispatchLiveSidecarTests \
  async-live
```

- closure result and persisted read-back: FAILED as a complete production lane because
  named-delivery lifecycle cleanup did not converge in the full, class, or method reruns
- limitations requiring owner disposition: hand-written state writes, stable work identity,
  dead-letter/terminal cleanup, and null metadata behavior
- rollback: retain the consumer's asynchronous persistence handoff

### Multiple projections per domain

- classification: `still blocked`
- source paths:
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore.Client/Projections/ProjectionRouteCatalogFingerprint.cs`
  - `src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/INamedProjectionDispatchCoordinator.cs`
  - `src/Hexalith.EventStore.Server/Projections/NamedProjectionRouteCatalog.cs`
- test paths:
  - `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionDispatcherV2Tests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Indexes/AdminOperationalIndexHostedServiceTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionDispatchCoordinatorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/NamedProjectionRouteCatalogTests.cs`
  - `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs`
- required exact commands:

```bash
fresh_release tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.DomainProjectionDispatcherV2Tests \
  multiprojection-domain-dispatcher

fresh_release tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll \
  Hexalith.EventStore.Client.Tests.Indexes.AdminOperationalIndexHostedServiceTests \
  multiprojection-catalog-publication

fresh_release tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
for class_name in \
  Hexalith.EventStore.Server.Tests.Projections.NamedProjectionDispatchCoordinatorTests \
  Hexalith.EventStore.Server.Tests.Projections.NamedProjectionRouteCatalogTests
do
  run_xunit_class \
    tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll \
    "$class_name" \
    "multiprojection-${class_name##*.}"
done

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_class \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.NamedProjectionDispatchLiveSidecarTests \
  multiprojection-live
```

- closure result and persisted read-back: FAILED as a complete production lane. Four of
  six named-dispatch methods passed in the full live run, but normal delivery retained a
  lifecycle row and the concurrent scenario reported an unreleased lifecycle lease
- compatibility decision awaiting approval: keep versioned `/project/v2`, legacy
  `/project`, exact `(Domain, ProjectionType)` routing, and bounded AD-19 results
- rollback: retain consumer-owned detail/index handlers and route composition

## Cross-Cutting Compatibility And Release Gate

- classification: `still blocked`
- production source paths:
  - `Directory.Build.targets`
  - `tools/release-packages.json`
  - `tools/pack-release-packages.py`
  - `scripts/pack-release-packages.py`
  - `scripts/validate-nuget-packages.py`
  - `scripts/validate-consumer-package-references.py`
  - `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs`
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore/Hexalith.EventStore.csproj`
- focused compatibility and packaging test paths:
  - `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs`
  - `tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs`
  - `tests/Hexalith.EventStore.DomainService.Tests/DomainProjectionHandlerCompatibilityTests.cs`
  - `tests/Hexalith.EventStore.RestApi.Generators.Tests/RestApiControllerGenerationTests.cs`
- required exact build and test commands, run only inside the mandatory detached harness:

```bash
find . -type d \( -path '*/bin/Release' -o -path '*/obj/Release' \) \
  -prune -exec rm -rf {} +
assert_candidate_identity
if dotnet restore Hexalith.EventStore.slnx \
    --configfile nuget.config \
    --packages "$NUGET_PACKAGES" \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0 &&
  dotnet build Hexalith.EventStore.slnx \
    --configuration Release \
    --no-restore \
    -m:1 \
    -warnaserror \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0; then
  solution_gate_status=0
else
  solution_gate_status=$?
fi
assert_candidate_identity
test "$solution_gate_status" -eq 0

while IFS='|' read -r project assembly evidence_name; do
  test -n "$project"
  fresh_release "$project"
  run_xunit_all "$assembly" "$evidence_name"
done <<'TEST_PROJECTS'
tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj|tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll|crosscut-contracts
tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj|tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll|crosscut-client
tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj|tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll|crosscut-domain-service
tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj|tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll|crosscut-query-routing
tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj|tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll|crosscut-server
tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj|tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll|crosscut-rest-generator
tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj|tests/Hexalith.EventStore.Sample.Tests/bin/Release/net10.0/Hexalith.EventStore.Sample.Tests.dll|crosscut-sample
tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj|tests/Hexalith.EventStore.Testing.Tests/bin/Release/net10.0/Hexalith.EventStore.Testing.Tests.dll|crosscut-testing
TEST_PROJECTS

fresh_release tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj true
run_xunit_class \
  tests/Hexalith.EventStore.IntegrationTests/bin/Release/net10.0/Hexalith.EventStore.IntegrationTests.dll \
  Hexalith.EventStore.IntegrationTests.ContractTests.QueryResponseProvenanceE2ETests \
  crosscut-focused-integration

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
run_xunit_all \
  tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll \
  crosscut-live-sidecar

run_xunit_class \
  tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll \
  Hexalith.EventStore.Contracts.Tests.Packaging.ReleasePackageManifestTests \
  crosscut-release-manifest
run_xunit_class \
  tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll \
  Hexalith.EventStore.DomainService.Tests.DomainProjectionHandlerCompatibilityTests \
  crosscut-domain-compatibility
```

- required exact package commands:

```bash
PACKAGE_OUTPUT="$GATE_ROOT/packages"
PACKAGE_VERSION="999.1.20-proof.$(git rev-parse --short=12 "$CANDIDATE_SHA")"
EXPECTED_PACKAGE_IDS="$EVIDENCE_ROOT/expected-package-ids.txt"
cat > "$EXPECTED_PACKAGE_IDS" <<'PACKAGE_IDS'
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
PACKAGE_IDS

validate_literal_package_inventory() {
  python3 - "$1" "$2" "$3" <<'PY'
import pathlib
import sys
import xml.etree.ElementTree as ET
import zipfile

package_directory = pathlib.Path(sys.argv[1])
expected_version = sys.argv[2]
expected_ids = pathlib.Path(sys.argv[3]).read_text(encoding="utf-8").splitlines()
if not expected_ids or any(not package_id for package_id in expected_ids) or len(expected_ids) != len(set(expected_ids)):
    raise SystemExit("literal package inventory contains blank or duplicate IDs")
expected_names = {f"{package_id}.{expected_version}.nupkg" for package_id in expected_ids}
packages = sorted(package_directory.glob("*.nupkg"))
actual_names = {package.name for package in packages}
if actual_names != expected_names or len(packages) != len(expected_ids):
    raise SystemExit("package filenames do not match the literal approved inventory")

actual_ids = []
for package in packages:
    with zipfile.ZipFile(package, "r") as archive:
        nuspec_names = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise SystemExit(f"{package.name}: expected exactly one nuspec")
        with archive.open(nuspec_names[0]) as nuspec:
            root = ET.parse(nuspec).getroot()
    namespace = root.tag[1:].split("}", 1)[0] if root.tag.startswith("{") else ""
    prefix = f"{{{namespace}}}" if namespace else ""
    metadata = root.find(f"{prefix}metadata")
    package_id = metadata.findtext(f"{prefix}id") if metadata is not None else None
    version = metadata.findtext(f"{prefix}version") if metadata is not None else None
    if package_id not in expected_ids or version != expected_version or package.name != f"{package_id}.{version}.nupkg":
        raise SystemExit(f"{package.name}: unexpected nuspec identity {package_id}/{version}")
    actual_ids.append(package_id)

if len(actual_ids) != len(set(actual_ids)) or set(actual_ids) != set(expected_ids):
    raise SystemExit("embedded nuspec IDs do not match the literal approved inventory")
PY
}

assert_candidate_identity
if rm -rf "$PACKAGE_OUTPUT" &&
  find src -type d \( -path '*/bin/Release' -o -path '*/obj/Release' \) \
    -prune -exec rm -rf {} + &&
  python3 scripts/pack-release-packages.py "$PACKAGE_OUTPUT" "$PACKAGE_VERSION" &&
  validate_literal_package_inventory "$PACKAGE_OUTPUT" "$PACKAGE_VERSION" "$EXPECTED_PACKAGE_IDS" &&
  python3 scripts/validate-nuget-packages.py "$PACKAGE_OUTPUT" &&
  python3 scripts/validate-consumer-package-references.py "$PACKAGE_OUTPUT" &&
  (cd "$PACKAGE_OUTPUT" && sha256sum -- *.nupkg | LC_ALL=C sort -k2) \
    > "$EVIDENCE_ROOT/nuget-sha256.txt" &&
  test "$(wc -l < "$EVIDENCE_ROOT/nuget-sha256.txt")" -eq 14; then
  package_gate_status=0
else
  package_gate_status=$?
fi
assert_candidate_identity
test "$package_gate_status" -eq 0
```

- required exact container publication, immutable inspection, and provenance commands:

```bash
set -euo pipefail
: "${RELEASE_AUTHORITY_RECORD:?set the durable release-owner authority JSON path}"
test -s "$RELEASE_AUTHORITY_RECORD"

PUBLISH_CONTAINER_REGISTRY='registry.hexalith.com'
PUBLISH_CONTAINER_REPOSITORY='eventstore'
IMAGE_REPOSITORY="$PUBLISH_CONTAINER_REGISTRY/$PUBLISH_CONTAINER_REPOSITORY"
IMAGE_TAG="proof-${CANDIDATE_SHA}"
test "$IMAGE_REPOSITORY" = 'registry.hexalith.com/eventstore'

# Freeze the authority bytes before the first publication-capable command.
AUTHORITY_EVIDENCE="$EVIDENCE_ROOT/release-owner-publication-authority.json"
AUTHORITY_CHECKED_AT_EVIDENCE="$EVIDENCE_ROOT/release-owner-publication-authority.checked-at.txt"
test ! -e "$AUTHORITY_EVIDENCE"
test ! -e "$AUTHORITY_CHECKED_AT_EVIDENCE"
cp -- "$RELEASE_AUTHORITY_RECORD" "$AUTHORITY_EVIDENCE"
chmod a-w "$AUTHORITY_EVIDENCE"

validate_publication_authority() {
  local checked_at="$1"
  jq -e \
    --arg checked_at "$checked_at" \
    --arg repository "$IMAGE_REPOSITORY" \
    --arg tag "$IMAGE_TAG" \
    --arg source_sha "$CANDIDATE_SHA" '
      def nonblank: type == "string" and test("\\S");
      type == "object" and
      .action == "container-publication" and
      (.owner | nonblank) and
      (.authorized_at | nonblank) and
      (.durable_source | nonblank) and
      (.rationale | nonblank) and
      (.expires_at | nonblank) and
      (.scope | type == "object") and
      .scope.repository == $repository and
      .scope.tag == $tag and
      .scope.source_sha == $source_sha and
      ((.authorized_at | fromdateiso8601) <= ($checked_at | fromdateiso8601)) and
      ((.expires_at | fromdateiso8601) > ($checked_at | fromdateiso8601))
    ' "$AUTHORITY_EVIDENCE"
}

assert_publication_source_clean() {
  local repository="$1"
  local ignored_entry
  local ignored_path
  test -z "$(git -C "$repository" status --porcelain=v1 --untracked-files=all)" || return 1
  while IFS= read -r -d '' ignored_entry; do
    ignored_path="${ignored_entry:3}"
    case "$ignored_path" in
      */bin/|*/bin/*|*/obj/|*/obj/*) ;;
      *) printf 'unexpected ignored publication input: %s:%s\n' \
           "$repository" "$ignored_path" >&2; return 1 ;;
    esac
  done < <(git -C "$repository" status --porcelain=v1 --ignored=matching \
    --untracked-files=all -z)
}

validate_published_manifest() {
  local manifest_file="$1"
  local image_digest="$2"
  local platforms_file="$3"
  local manifest_sha256
  [[ "$image_digest" =~ ^sha256:[0-9a-f]{64}$ ]] || return 1
  jq -e '.manifests | type == "array" and length > 0' "$manifest_file" \
    >/dev/null || return 1
  manifest_sha256="$(sha256sum "$manifest_file" | awk '{print $1}')" || return 1
  test "sha256:$manifest_sha256" = "$image_digest" || return 1
  jq -r '.manifests[].platform |
    "\(.os)/\(.architecture)\(if .variant then "/" + .variant else "" end)"' \
    "$manifest_file" | sort -u > "$platforms_file" || return 1
  cmp --silent <(printf '%s\n' 'linux/amd64' 'linux/arm64') "$platforms_file" || return 1
  printf '%s\n' "$manifest_sha256"
}

# Restore into the isolated cache and capture the exact SDK-generated multi-architecture
# image-index bytes/digest. The capture target observes the same GeneratedImageIndex string
# that the SDK hands to its registry publisher; provenance never derives identity from a tag.
CONTAINER_PROJECT='src/Hexalith.EventStore/Hexalith.EventStore.csproj'
CONTAINER_PROJECT_DIRECTORY="${CONTAINER_PROJECT%/*}"
CAPTURE_TARGETS="$EVIDENCE_ROOT/capture-generated-image-index.targets"
GENERATED_IMAGE_INDEX="$EVIDENCE_ROOT/generated-image-index.json"
GENERATED_IMAGE_INDEX_DIGEST="$EVIDENCE_ROOT/generated-image-index.digest.txt"
cat > "$CAPTURE_TARGETS" <<'MSBUILD'
<Project>
  <UsingTask TaskName="CaptureGeneratedImageIndex"
             TaskFactory="RoslynCodeTaskFactory"
             AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
    <ParameterGroup>
      <ImageIndex ParameterType="System.String" Required="true" />
      <OutputPath ParameterType="System.String" Required="true" />
      <Digest ParameterType="System.String" Output="true" />
    </ParameterGroup>
    <Task>
      <Using Namespace="System" />
      <Using Namespace="System.IO" />
      <Using Namespace="System.Security.Cryptography" />
      <Using Namespace="System.Text" />
      <Code Type="Fragment" Language="cs"><![CDATA[
        byte[] bytes = new UTF8Encoding(false).GetBytes(ImageIndex);
        File.WriteAllBytes(OutputPath, bytes);
        using (SHA256 sha256 = SHA256.Create())
        {
          byte[] hash = sha256.ComputeHash(bytes);
          Digest = "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
      ]]></Code>
    </Task>
  </UsingTask>
  <Target Name="CapturePublishedImageIndex" AfterTargets="_PublishMultiArchContainers">
    <Error Condition="'$(GeneratedImageIndex)' == ''"
           Text="The .NET container SDK did not expose GeneratedImageIndex." />
    <CaptureGeneratedImageIndex ImageIndex="$(GeneratedImageIndex)"
                                OutputPath="$(ContainerImageIndexOutputPath)">
      <Output TaskParameter="Digest" PropertyName="CapturedImageIndexDigest" />
    </CaptureGeneratedImageIndex>
    <WriteLinesToFile File="$(ContainerImageIndexDigestOutputPath)"
                      Lines="$(CapturedImageIndexDigest)"
                      Overwrite="true"
                      Encoding="UTF-8" />
  </Target>
</Project>
MSBUILD
chmod a-w "$CAPTURE_TARGETS"
CAPTURE_TARGETS_SHA256="$(sha256sum "$CAPTURE_TARGETS" | awk '{print $1}')"
rm -rf "$CONTAINER_PROJECT_DIRECTORY/bin/Release" "$CONTAINER_PROJECT_DIRECTORY/obj/Release"
dotnet restore "$CONTAINER_PROJECT" \
  --configfile nuget.config \
  --packages "$NUGET_PACKAGES" \
  -p:UseHexalithProjectReferences=false \
  -p:NuGetAudit=false \
  -p:MinVerVersionOverride=1.0.0

# Immediately before publication, recheck source identity/cleanliness and authority validity.
assert_candidate_identity
for repository in . "${ROOT_SUBMODULES[@]}"; do
  assert_publication_source_clean "$repository"
done
AUTHORITY_CHECKED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf '%s\n' "$AUTHORITY_CHECKED_AT" > "$AUTHORITY_CHECKED_AT_EVIDENCE"
chmod a-w "$AUTHORITY_CHECKED_AT_EVIDENCE"
validate_publication_authority "$AUTHORITY_CHECKED_AT"
AUTHORITY_SHA256="$(sha256sum "$AUTHORITY_EVIDENCE" | awk '{print $1}')"
AUTHORITY_CHECKED_AT_SHA256="$(sha256sum "$AUTHORITY_CHECKED_AT_EVIDENCE" | awk '{print $1}')"
assert_candidate_identity
if dotnet publish "$CONTAINER_PROJECT" \
    --configuration Release \
    --no-restore \
    -p:PublishProfile=DefaultContainer \
    -p:UseHexalithProjectReferences=false \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0 \
    -p:ContainerRegistry="$PUBLISH_CONTAINER_REGISTRY" \
    -p:ContainerRepository="$PUBLISH_CONTAINER_REPOSITORY" \
    -p:ContainerImageTag="$IMAGE_TAG" \
    "-p:ContainerRuntimeIdentifiers=linux-x64;linux-arm64" \
    -p:CustomAfterMicrosoftCommonTargets="$CAPTURE_TARGETS" \
    -p:ContainerImageIndexOutputPath="$GENERATED_IMAGE_INDEX" \
    -p:ContainerImageIndexDigestOutputPath="$GENERATED_IMAGE_INDEX_DIGEST" \
    2>&1 | tee "$EVIDENCE_ROOT/container-publish.txt"; then
  publication_gate_status=0
else
  publication_gate_status=$?
fi
assert_candidate_identity
test "$publication_gate_status" -eq 0
for repository in . "${ROOT_SUBMODULES[@]}"; do
  assert_publication_source_clean "$repository"
done
test "$(sha256sum "$CAPTURE_TARGETS" | awk '{print $1}')" = "$CAPTURE_TARGETS_SHA256"
test -s "$GENERATED_IMAGE_INDEX"
test -s "$GENERATED_IMAGE_INDEX_DIGEST"
IMAGE_DIGEST="$(cat "$GENERATED_IMAGE_INDEX_DIGEST")"
[[ "$IMAGE_DIGEST" =~ ^sha256:[0-9a-f]{64}$ ]]
test "sha256:$(sha256sum "$GENERATED_IMAGE_INDEX" | awk '{print $1}')" = "$IMAGE_DIGEST"
jq -e '.manifests | type == "array" and length > 0' "$GENERATED_IMAGE_INDEX" >/dev/null
assert_candidate_identity
if docker buildx imagetools inspect "$IMAGE_REPOSITORY:$IMAGE_TAG" \
  | tee "$EVIDENCE_ROOT/container-inspect.txt"; then
  inspect_tag_gate_status=0
else
  inspect_tag_gate_status=$?
fi
assert_candidate_identity
test "$inspect_tag_gate_status" -eq 0
TAG_IMAGE_DIGEST="$({ awk '$1 == "Digest:" { print $2; exit }' \
  "$EVIDENCE_ROOT/container-inspect.txt"; } || true)"
test "$TAG_IMAGE_DIGEST" = "$IMAGE_DIGEST"
assert_candidate_identity
if docker buildx imagetools inspect "$IMAGE_REPOSITORY@$IMAGE_DIGEST" --raw \
  > "$EVIDENCE_ROOT/container-manifest.json"; then
  inspect_digest_gate_status=0
else
  inspect_digest_gate_status=$?
fi
assert_candidate_identity
test "$inspect_digest_gate_status" -eq 0
MANIFEST_SHA256="$(validate_published_manifest \
  "$EVIDENCE_ROOT/container-manifest.json" "$IMAGE_DIGEST" \
  "$EVIDENCE_ROOT/container-platforms.txt")"
test "$(sha256sum "$AUTHORITY_EVIDENCE" | awk '{print $1}')" = "$AUTHORITY_SHA256"
test "$(sha256sum "$AUTHORITY_CHECKED_AT_EVIDENCE" | awk '{print $1}')" = "$AUTHORITY_CHECKED_AT_SHA256"
test "$(cat "$AUTHORITY_CHECKED_AT_EVIDENCE")" = "$AUTHORITY_CHECKED_AT"
jq -n \
  --arg source_sha "$CANDIDATE_SHA" \
  --arg repository "$IMAGE_REPOSITORY" \
  --arg tag "$IMAGE_TAG" \
  --arg digest "$IMAGE_DIGEST" \
  --arg manifest_sha256 "$MANIFEST_SHA256" \
  --arg container_registry "$PUBLISH_CONTAINER_REGISTRY" \
  --arg container_repository "$PUBLISH_CONTAINER_REPOSITORY" \
  --arg runtime_identifiers 'linux-x64;linux-arm64' \
  --arg authority_file "$(basename "$AUTHORITY_EVIDENCE")" \
  --arg authority_sha256 "$AUTHORITY_SHA256" \
  --arg authority_checked_at "$AUTHORITY_CHECKED_AT" \
  --arg authority_checked_at_file "$(basename "$AUTHORITY_CHECKED_AT_EVIDENCE")" \
  --arg authority_checked_at_sha256 "$AUTHORITY_CHECKED_AT_SHA256" \
  --arg capture_targets_file "$(basename "$CAPTURE_TARGETS")" \
  --arg capture_targets_sha256 "$CAPTURE_TARGETS_SHA256" \
  --arg generated_index_file "$(basename "$GENERATED_IMAGE_INDEX")" \
  --rawfile platforms "$EVIDENCE_ROOT/container-platforms.txt" \
  '{source_sha: $source_sha, repository: $repository, tag: $tag, digest: $digest,
    manifest_sha256: $manifest_sha256,
    sdk_generated_index: {
      file: $generated_index_file,
      digest: $digest,
      capture_targets_file: $capture_targets_file,
      capture_targets_sha256: $capture_targets_sha256
    },
    platforms: ($platforms | split("\n") | map(select(length > 0))),
    publish_properties: {
      container_registry: $container_registry,
      container_repository: $container_repository,
      container_image_tag: $tag,
      container_runtime_identifiers: $runtime_identifiers
    },
    publication_authority: {
      file: $authority_file,
      sha256: $authority_sha256,
      checked_at: $authority_checked_at,
      checked_at_file: $authority_checked_at_file,
      checked_at_sha256: $authority_checked_at_sha256
    }}' \
  > "$EVIDENCE_ROOT/container-provenance.json"
assert_candidate_identity
```

The authority check revalidates the immutable evidence copy at a fresh action timestamp after
candidate/source-cleanliness checks and before the first registry write. The checked-at file,
both hashes, exact authorized repository/tag/source SHA, raw-manifest digest, platform set, and
actual publish properties survive in provenance. This is an evidence-integrity preflight only:
it does not create human
release authority, replace registry authentication/authorization, or substitute for the
later proof-result approval and distinct release-owner disposition.

| Gate | Required result | Evidence target | Required disposition owner | Current result |
| --- | --- | --- | --- | --- |
| AD-11 readiness | exact SDK `10.0.302` plus matching ASP.NET and installed `Microsoft.NETCore.App` runtime `10.0.10`, or complete, scoped, unexpired replacement authority binding those versions before candidate gates | `ad11-preflight.json`, runtime inventory, its SHA-256, and replacement-authority copy/hash when used | Architecture owner and EventStore build/release maintainer | PASS in the 2026-07-17 current-HEAD readiness audit at `772cdfef...`; every later selected candidate must repeat the preflight |
| Exact committed source | Same 40-hex SHA before and after gates; clean regular and ignored inputs before restore | pre/post status, submodule SHAs, environment inventory | EventStore owner | PASS for failed candidate `85877902f8d60a466ab90cd8b68b53838863db1c` |
| Release build and tests | warning-free solution build; positive xUnit totals for every listed project/filter | build log plus XML and method-list files under `$EVIDENCE_ROOT` | EventStore owner | **FAIL / non-authorizing**; at current commit `772cdfef...`, the Release build, broad lanes, former lifecycle method/class, and full live-sidecar passed, but the corrected source-mode query-provenance E2E failed twice with 404 / `query_projection_missing` |
| NuGet inventory | exact 14-ID set, one approved version, 14 SHA-256 values, package-only consumer success | package listing, validator logs, `nuget-sha256.txt`, consumer assets/tool-install log | EventStore release owner | NOT RUN |
| Container runtime | freshly revalidated pre-publication release-owner authority; clean candidate source; immutable registry digest equal to the raw-manifest SHA-256; exact `linux/amd64` and `linux/arm64`; digest-to-tested-SHA provenance | immutable authority and checked-at copies/hashes, `container-inspect.txt`, raw manifest/hash, exact platform set, `container-provenance.json` | EventStore release owner | NOT RUN |
| Limitations and migration | every matrix-row limitation accepted or rejected by a named reviewer in a durable source | signed review record or PR URL and date | EventStore owner | NOT RUN |

- compatibility boundary: additive public APIs, the legacy query metadata ABI, legacy
  `/project`, versioned `/project/v2`, and the manifest-owned 14-package inventory remain
  unchanged by this evidence-only packet
- owner disposition: pending; the candidate failed before package/container gates and no
  named owner has accepted the row limitations
- rollback: do not change package pins, image deployment, consumer gitlinks, or consumer
  infrastructure from this packet

## Owner Review

- reviewer: not assigned
- approval date: not recorded
- durable approval source or PR: not recorded
- accepted scope: none
- accepted limitations: none
- migration decision: `authorize_consumer_migration: false`

Story creation, implementation review of an individual prerequisite, and authorship of
this blocked packet are not proof-result owner approval.

### Evidence Commit A And Pointer-Only Commit B Verification

Run this repository-root procedure only after the completed proof results and distinct
EventStore-owner and release-owner dispositions exist in durable external sources. It
proves that evidence commit A is a single-parent, `_bmad-output/`-only child of the equal
candidate/tested runtime while remaining fail closed, and that its direct child B changes
exactly one front-matter field to point back to A:

```bash
set -euo pipefail
PACKET='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
STORY='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md'
SPRINT_STATUS='_bmad-output/implementation-artifacts/sprint-status.yaml'
: "${EVIDENCE_COMMIT_A:?set the evidence commit A SHA}"
: "${POINTER_COMMIT_B:?set the pointer-only commit B SHA}"

EVIDENCE_COMMIT_A="$(git rev-parse --verify --end-of-options "${EVIDENCE_COMMIT_A}^{commit}")"
POINTER_COMMIT_B="$(git rev-parse --verify --end-of-options "${POINTER_COMMIT_B}^{commit}")"
[[ "$EVIDENCE_COMMIT_A" =~ ^[0-9a-f]{40}$ ]]
[[ "$POINTER_COMMIT_B" =~ ^[0-9a-f]{40}$ ]]
test "$(git rev-list --parents -n 1 "$POINTER_COMMIT_B" | awk '{print NF}')" -eq 2
test "$(git rev-parse --verify --end-of-options "${POINTER_COMMIT_B}^")" = "$EVIDENCE_COMMIT_A"

A_PACKET="$(mktemp)"
B_PACKET="$(mktemp)"
EXPECTED_B_PACKET="$(mktemp)"
A_STORY="$(mktemp)"
A_SPRINT_STATUS="$(mktemp)"
git show "$EVIDENCE_COMMIT_A:$PACKET" > "$A_PACKET"
git show "$POINTER_COMMIT_B:$PACKET" > "$B_PACKET"
git show "$EVIDENCE_COMMIT_A:$STORY" > "$A_STORY"
git show "$EVIDENCE_COMMIT_A:$SPRINT_STATUS" > "$A_SPRINT_STATUS"

front_value() {
  local packet="$1"
  local key="$2"
  awk -v key="$key" '
    NR == 1 && $0 == "---" { in_front = 1; next }
    in_front && $0 == "---" { in_front = 0; exit }
    in_front && index($0, key ": ") == 1 {
      value = substr($0, length(key) + 3)
      count++
    }
    END {
      if (count != 1) exit 1
      print value
    }
  ' "$packet"
}

A_TESTED_RUNTIME_SHA="$(front_value "$A_PACKET" tested_runtime_sha)"
[[ "$A_TESTED_RUNTIME_SHA" =~ ^[0-9a-f]{40}$ ]]
A_CANDIDATE_SOURCE_SHA="$(front_value "$A_PACKET" candidate_source_sha)"
test "$A_CANDIDATE_SOURCE_SHA" = "$A_TESTED_RUNTIME_SHA"
A_TESTED_RUNTIME_COMMIT="$(git rev-parse --verify --end-of-options \
  "${A_TESTED_RUNTIME_SHA}^{commit}")"
test "$A_TESTED_RUNTIME_COMMIT" = "$A_TESTED_RUNTIME_SHA"
test "$(git rev-list --parents -n 1 "$EVIDENCE_COMMIT_A" | awk '{print NF}')" -eq 2
test "$(git rev-parse --verify --end-of-options "${EVIDENCE_COMMIT_A}^")" \
  = "$A_TESTED_RUNTIME_COMMIT"
mapfile -t A_CHANGED_FILES < <(
  git diff --name-only "$A_TESTED_RUNTIME_COMMIT" "$EVIDENCE_COMMIT_A"
)
test "${#A_CHANGED_FILES[@]}" -gt 0
for changed_file in "${A_CHANGED_FILES[@]}"; do
  case "$changed_file" in
    _bmad-output/*) ;;
    *) printf 'evidence commit A changed non-evidence path: %s\n' \
         "$changed_file" >&2; exit 1 ;;
  esac
done
test "$(front_value "$A_PACKET" documentation_commit_sha)" = 'null'
test "$(front_value "$A_PACKET" final_decision)" = 'still blocked'
test "$(front_value "$A_PACKET" authorize_consumer_migration)" = 'false'
test "$(front_value "$A_STORY" status)" = 'blocked'
test "$(awk '
  NR == 1 && $0 == "---" { in_front = 1; next }
  in_front && $0 == "---" { in_front = 0; after_front = 1; next }
  after_front && /^Status: / { print substr($0, 9); exit }
' "$A_STORY")" = 'blocked'
awk '
  /^  epic-1:/ { epic_count++; epic_ok += ($0 == "  epic-1: in-progress") }
  /^  1-20-owner-approved-parity-closure-and-runtime-pin:/ {
    story_count++
    story_ok += ($0 == "  1-20-owner-approved-parity-closure-and-runtime-pin: in-progress")
  }
  END { exit !(epic_count == 1 && epic_ok == 1 && story_count == 1 && story_ok == 1) }
' "$A_SPRINT_STATUS"
test "$(front_value "$B_PACKET" tested_runtime_sha)" = "$A_TESTED_RUNTIME_SHA"
test "$(front_value "$B_PACKET" documentation_commit_sha)" = "$EVIDENCE_COMMIT_A"
test "$(front_value "$B_PACKET" final_decision)" = 'still blocked'
test "$(front_value "$B_PACKET" authorize_consumer_migration)" = 'false'

mapfile -t B_CHANGED_FILES < <(git diff --name-only "$EVIDENCE_COMMIT_A" "$POINTER_COMMIT_B")
test "${#B_CHANGED_FILES[@]}" -eq 1
test "${B_CHANGED_FILES[0]}" = "$PACKET"
awk -v evidence_commit_a="$EVIDENCE_COMMIT_A" '
  NR == 1 && $0 == "---" { in_front = 1; print; next }
  in_front && $0 == "---" { in_front = 0; print; next }
  in_front && $0 == "documentation_commit_sha: null" && changed == 0 {
    print "documentation_commit_sha: " evidence_commit_a
    changed = 1
    next
  }
  { print }
  END { if (changed != 1) exit 1 }
' "$A_PACKET" > "$EXPECTED_B_PACKET"
cmp --silent "$EXPECTED_B_PACKET" "$B_PACKET"
```

A missing or non-unique opening field, unequal candidate/runtime identity, unresolved tested
runtime, merge commit A, any A parent other than that runtime, an A path outside
`_bmad-output/`, merge commit B, wrong B parent/pointer, changed decision/migration guard,
extra B file, or any change beyond that exact one-line pointer update exits nonzero. This is
only a structural evidence-only/non-self-reference check. Neither A nor B grants migration
authority or substitutes for complete proof, durable approvals, or every independent gate on
a later status-only transition.

## Consumer Handoff Guard

All placeholders below intentionally fail closed. They may be replaced only from a future
`available` packet carrying named owner approval and the matching evidence files.

### Source consumer procedure

```bash
set -euo pipefail
APPROVED_EVENTSTORE_SHA='NOT_SELECTED'
[[ "$APPROVED_EVENTSTORE_SHA" =~ ^[0-9a-f]{40}$ ]]
EVENTSTORE_SUBMODULE='references/Hexalith.EventStore'

GITLINK_SHA="$(git ls-tree --object-only HEAD "$EVENTSTORE_SUBMODULE")"
CHECKOUT_SHA="$(git -C "$EVENTSTORE_SUBMODULE" \
  rev-parse --verify --end-of-options 'HEAD^{commit}')"
test "$GITLINK_SHA" = "$APPROVED_EVENTSTORE_SHA"
test "$CHECKOUT_SHA" = "$APPROVED_EVENTSTORE_SHA"
test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"
test -z "$(git -C "$EVENTSTORE_SUBMODULE" \
  status --porcelain=v1 --untracked-files=all)"
test -z "$(git -C "$EVENTSTORE_SUBMODULE" \
  status --porcelain=v1 --ignored=matching --untracked-files=all)"
```

A dirty consumer, dirty or ignored EventStore source/configuration input, or any approved
SHA/gitlink/checkout mismatch keeps source migration blocked.

### NuGet consumer procedure

The future approval record must supply `APPROVED_PACKAGE_VERSION`, the directory containing
the exact 14 `.nupkg` files, and a `sha256sum --check` file whose filenames are relative to
that directory. The approved ID inventory is literal, not merely a count:

```bash
set -euo pipefail
APPROVED_PACKAGE_VERSION='NOT_SELECTED'
APPROVED_PACKAGE_DIRECTORY='NOT_SELECTED'
APPROVED_PACKAGE_HASHES='NOT_SELECTED'
CONSUMER_PROJECT='NOT_SELECTED'
test "$APPROVED_PACKAGE_VERSION" != 'NOT_SELECTED'
test -d "$APPROVED_PACKAGE_DIRECTORY"
test -f "$APPROVED_PACKAGE_HASHES"
test -f "$CONSUMER_PROJECT"
APPROVED_PACKAGE_DIRECTORY="$(realpath "$APPROVED_PACKAGE_DIRECTORY")"
APPROVED_PACKAGE_HASHES="$(realpath "$APPROVED_PACKAGE_HASHES")"
CONSUMER_PROJECT="$(realpath "$CONSUMER_PROJECT")"

EXPECTED_IDS="$(mktemp)"
cat > "$EXPECTED_IDS" <<'PACKAGE_IDS'
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
PACKAGE_IDS
sort -u -o "$EXPECTED_IDS" "$EXPECTED_IDS"
test "$(wc -l < "$EXPECTED_IDS")" -eq 14
EXPECTED_PACKAGE_FILES="$(mktemp)"
awk -v version="$APPROVED_PACKAGE_VERSION" '{ print $0 "." version ".nupkg" }' \
  "$EXPECTED_IDS" | LC_ALL=C sort > "$EXPECTED_PACKAGE_FILES"

ACTUAL_IDS="$(mktemp)"
ACTUAL_PACKAGE_FILES="$(mktemp)"
find "$APPROVED_PACKAGE_DIRECTORY" -maxdepth 1 -type f -name '*.nupkg' -printf '%f\n' \
  | LC_ALL=C sort > "$ACTUAL_PACKAGE_FILES"
diff -u "$EXPECTED_PACKAGE_FILES" "$ACTUAL_PACKAGE_FILES"
sed -n "s/\.${APPROVED_PACKAGE_VERSION//./\\.}\.nupkg$//p" "$ACTUAL_PACKAGE_FILES" \
  | sort -u > "$ACTUAL_IDS"
test "$(find "$APPROVED_PACKAGE_DIRECTORY" -maxdepth 1 -type f -name '*.nupkg' | wc -l)" -eq 14
diff -u "$EXPECTED_IDS" "$ACTUAL_IDS"
validate_package_hash_manifest() {
  local hash_file="$1"
  local expected_files="$2"
  local hash_filenames
  hash_filenames="$(mktemp)"
  if ! awk '
    NF == 2 && length($1) == 64 && $1 !~ /[^0-9a-f]/ && $2 !~ /\// {
      print $2
      valid++
    }
    END { if (NR != 14 || valid != 14) exit 1 }
  ' "$hash_file" | LC_ALL=C sort -u > "$hash_filenames"; then
    rm -f "$hash_filenames"
    return 1
  fi
  if test "$(wc -l < "$hash_filenames")" -ne 14 ||
    ! diff -u "$expected_files" "$hash_filenames"; then
    rm -f "$hash_filenames"
    return 1
  fi
  rm -f "$hash_filenames"
}
validate_package_hash_manifest "$APPROVED_PACKAGE_HASHES" "$EXPECTED_PACKAGE_FILES"
(cd "$APPROVED_PACKAGE_DIRECTORY" && sha256sum --check "$APPROVED_PACKAGE_HASHES")

CONSUMER_GATE="$(mktemp -d)"
cat > "$CONSUMER_GATE/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="approved-eventstore" value="$APPROVED_PACKAGE_DIRECTORY" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="approved-eventstore">
      <package pattern="Hexalith.EventStore*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF
dotnet restore "$CONSUMER_PROJECT" \
  --configfile "$CONSUMER_GATE/nuget.config" \
  --packages "$CONSUMER_GATE/packages" \
  --force-evaluate \
  -p:UseHexalithProjectReferences=false
ASSETS_FILE="$(dirname "$CONSUMER_PROJECT")/obj/project.assets.json"
jq -e --arg version "$APPROVED_PACKAGE_VERSION" '
  [.libraries | to_entries[] |
    select(.key | startswith("Hexalith.EventStore") or startswith("hexalith.eventstore"))] as $eventstore |
  ($eventstore | length > 0) and
  all($eventstore[];
    .value.type == "package" and
    ((.key | ascii_downcase) | endswith(("/" + $version) | ascii_downcase)))
' "$ASSETS_FILE"
dotnet build "$CONSUMER_PROJECT" \
  --configuration Release \
  --no-restore \
  -p:UseHexalithProjectReferences=false
```

The filename/version check proves the full approved inventory is present; the asset-file
check proves every EventStore dependency actually selected by the consumer resolves as a
package at that exact version rather than as a project reference.

### Container consumer procedure

```bash
set -euo pipefail
APPROVED_IMAGE_REPOSITORY='NOT_SELECTED'
APPROVED_IMAGE_DIGEST='NOT_SELECTED'
APPROVED_PLATFORM_SET_FILE='NOT_SELECTED'
test "$APPROVED_IMAGE_REPOSITORY" != 'NOT_SELECTED'
[[ "$APPROVED_IMAGE_DIGEST" =~ ^sha256:[0-9a-f]{64}$ ]]
test -s "$APPROVED_PLATFORM_SET_FILE"

IMMUTABLE_IMAGE="$APPROVED_IMAGE_REPOSITORY@$APPROVED_IMAGE_DIGEST"
INSPECT_OUTPUT="$(mktemp)"
RAW_MANIFEST="$(mktemp)"
ACTUAL_PLATFORMS="$(mktemp)"
docker buildx imagetools inspect "$IMMUTABLE_IMAGE" | tee "$INSPECT_OUTPUT"
test "$(awk '$1 == "Digest:" { print $2; exit }' "$INSPECT_OUTPUT")" \
  = "$APPROVED_IMAGE_DIGEST"
docker buildx imagetools inspect "$IMMUTABLE_IMAGE" --raw > "$RAW_MANIFEST"
jq -e '.manifests | type == "array" and length > 0' "$RAW_MANIFEST"
test "sha256:$(sha256sum "$RAW_MANIFEST" | awk '{print $1}')" = "$APPROVED_IMAGE_DIGEST"
jq -r '.manifests[].platform | "\(.os)/\(.architecture)\(if .variant then "/" + .variant else "" end)"' \
  "$RAW_MANIFEST" | sort -u > "$ACTUAL_PLATFORMS"
test -s "$ACTUAL_PLATFORMS"
diff -u <(sort -u "$APPROVED_PLATFORM_SET_FILE") "$ACTUAL_PLATFORMS"
```

The deployment reference must be the verified `repository@sha256:...` value, never a tag.
Until all three procedures have real approved pins and pass in the consumer repository,
this packet authorizes no consumer repository, package pin, deployment, or rollback change.

### Downstream story routing

These are adoption owners. Registration creates no migration authority and changes no
dependency pointer. Story 2.7 additionally contains a narrowly scoped pre-authorization
EventStore registration/harness correction required to make Story 1.20's live provenance
gate valid; its later dependency-adoption phase remains downstream.

| Repository | Adoption owner | Current state | Activation/closure boundary |
| --- | --- | --- | --- |
| FrontComposer | Story 11.24, `Adopt the Owner-Approved EventStore Runtime Identity` | Registered `backlog`; no story file | Remains backlog until this packet is `available` and authorizes migration; then prove exact source/package identities, Pact provider verification, dual-mode builds, and live Aspire command/query/realtime smoke. |
| Memories | Epic 28 / Story 28.1, `Adopt Owner-Approved EventStore Runtime Identity` | Registered `backlog`; no story file | Remains backlog until this packet is `available` and authorizes migration; then prove exact source/package identities while preserving the DAPR ingestion registration chain and live persistence/search/dedup behavior. |
| Tenants | Existing EventStore Story 2.7, `Tenants Compatibility And Package-Mode Validation` | `review` | No duplicate Tenants-local story. Correct the stale sample registration/source-topology proof now without changing dependency identities; after authorization, align the source/package graph, including conditional Gateway ownership, and record exact Tenants maintainer evidence. |
| Builds | Existing platform pin ownership | No new story | AD-11 SDK/runtime and central ASP.NET pins are already landed. A later EventStore package-version update is release-owner work only after approved artifacts exist. |

FrontComposer and Memories story files must not be created before activation because their trackers
interpret a created story file as `ready-for-dev`. Tenants Story 2.7 remains the existing fail-closed
consumer review owner. Builds must not speculate a future EventStore package version.

## Verification Log

### Discovery-only identity and environment

| Command | Result |
| --- | --- |
| `git rev-parse --verify --end-of-options 'HEAD^{commit}'` | PASS; candidate discovery SHA `26842d284f2da91399b7891bf7b5880ce2f6b561`. |
| `git status --porcelain=v1 --untracked-files=all --ignore-submodules=none` | CLOSURE BLOCKED; the Story 1.20 specification was already modified, so `HEAD` was not selected as tested runtime. |
| `git submodule status` | PASS for inventory capture; two root-declared submodules were uninitialized and no nested submodules were initialized. |
| `dotnet --info` | PASS; SDK `10.0.301`, host/runtime `10.0.9`, Ubuntu 26.04 `linux-x64`. |
| `dapr --version` | PASS; CLI `1.18.0`, runtime `1.18.1`. |

### Exact release-package inventory check

The following check passed and compares the manifest with the literal approved inventory,
not merely with a count:

```bash
set -euo pipefail
EXPECTED_IDS="$(mktemp)"
cat > "$EXPECTED_IDS" <<'PACKAGE_IDS'
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
PACKAGE_IDS
ACTUAL_IDS="$(mktemp)"
jq -r '.packages[].id' tools/release-packages.json | sort -u > "$ACTUAL_IDS"
test "$(jq '.packages | length' tools/release-packages.json)" -eq 14
test "$(wc -l < "$ACTUAL_IDS")" -eq 14
diff -u "$EXPECTED_IDS" "$ACTUAL_IDS"
```

Result: PASS; all and only the 14 listed package IDs are present.

### Reproducible cited-path check

This command extracted every cited repository source, test, documentation, release-tool,
solution, and restore-config path, deduplicated them, and failed on any absent path:

```bash
set -euo pipefail
PACKET='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
CITED_PATHS="$(mktemp)"
perl -nle 'while (m{(?<![A-Za-z0-9_.-])((?:src|tests|docs|scripts|tools)/[A-Za-z0-9_./-]+\.(?:csproj|cs|md|py|json)|Directory\.Build\.targets|Hexalith\.EventStore\.slnx|nuget\.config)}g) { print $1 }' \
  "$PACKET" | sort -u > "$CITED_PATHS"
MISSING_PATHS="$(mktemp)"
while IFS= read -r path; do
  test -e "$path" || printf '%s\n' "$path"
done < "$CITED_PATHS" > "$MISSING_PATHS"
test ! -s "$MISSING_PATHS"
test "$(wc -l < "$CITED_PATHS")" -eq 105
```

Result: PASS; all 105 unique cited repository paths exist.

### xUnit v3 filter and zero-test guard check

The installed xUnit v3 in-process runner was probed directly. A nonexistent class returned
exit code zero and XML `total="0"`, proving that exit status alone is insufficient:

```bash
set -euo pipefail
ASSEMBLY='tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll'
ZERO_XML="$(mktemp --suffix=.xml)"
set +e
dotnet "$ASSEMBLY" -noColor \
  -class Hexalith.EventStore.Client.Tests.DoesNotExist \
  -xml "$ZERO_XML"
ZERO_STATUS=$?
set -e
test "$ZERO_STATUS" -eq 0
grep -Eq 'total="0"' "$ZERO_XML"

METHODS="$(mktemp)"
dotnet "$ASSEMBLY" -noColor -list methods \
  -class Hexalith.EventStore.Client.Tests.Queries.QueryCursorScopeTests > "$METHODS"
grep -Fq 'Hexalith.EventStore.Client.Tests.Queries.QueryCursorScopeTests.' "$METHODS"
```

Result: PASS; zero-match returned `0`/`total="0"`, while the positive list filter found six
method rows. This was a runner-behavior probe against an existing assembly, not parity
closure evidence; the mandatory harness still requires a fresh Release build and positive
XML total for every credited run.

### Packet whitespace checks

Because this packet is untracked, both tracked and no-index checks are required:

```bash
set -euo pipefail
PACKET='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
git diff --check
set +e
NO_INDEX_OUTPUT="$(git diff --no-index --check /dev/null "$PACKET" 2>&1)"
NO_INDEX_STATUS=$?
set -e
test "$NO_INDEX_STATUS" -eq 1
test -z "$NO_INDEX_OUTPUT"
```

Result: PASS; the tracked diff and the complete untracked packet contain no whitespace
errors. Exit `1` is the expected `--no-index` content-difference status.

### 2026-07-16 exact-SHA completion attempt

Candidate: `85877902f8d60a466ab90cd8b68b53838863db1c` in a clean detached checkout with isolated
NuGet/Dotnet caches and all seven root-declared submodules initialized at their gitlinks.

- Release solution build: PASS, 0 warnings and 0 errors.
- Broad regression: PASS across 18 unit/focused projects. Key totals were Contracts 702,
  Client 673, DomainService 143, QueryRouting 6, REST generator 124, Sample 117,
  Testing 150, and Server 2,620 passed / 25 skipped.
- Additional regression: PASS; Testing.Integration 44/44, AppHost 48/48, and Admin UI E2E
  39 passed / 2 skipped.
- Full live-sidecar: **FAIL**, 42 passed / 2 failed. The persisted batching, erasure,
  cutover, and paged-rebuild scenarios passed.
- Isolated `NamedProjectionDispatchLiveSidecarTests`: **FAIL**, 5 passed / 1 failed.
- Single `NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger`:
  **FAIL**, 0 passed / 1 failed. The persisted lifecycle hash remained present instead of
  returning to the idle/absent baseline.
- Post-attempt source identity and regular/untracked cleanliness: PASS at the same SHA.

The query-provenance E2E, package build/consumer, container publish/inspect, and owner
disposition gates were not run after the reproducible live regression failure. They cannot
turn a failing runtime into an approved pin.

### 2026-07-17 current-HEAD readiness re-audit

This audit preserves the failed-candidate history above and does not promote current HEAD
to `candidate_source_sha` or `tested_runtime_sha`.

- Exact clean detached source: `772cdfefa8163704de0f57042af5b0507c1ac771`, with only
  root-declared submodules initialized and isolated NuGet/Dotnet caches.
- AD-11 executable preflight: PASS; repository and installed SDK `10.0.302`, effective
  ASP.NET `10.0.10`, and installed `Microsoft.NETCore.App` `10.0.10`.
- Warning-free Release build and broad regression lanes: PASS through the Server project.
- Former lifecycle blocker: PASS; the exact previously failing normal-delivery method was
  1/1, `NamedProjectionDispatchLiveSidecarTests` was 6/6, and the full live-sidecar lane was
  44/44.
- Packet harness defect: the original integration build forced
  `UseHexalithProjectReferences=false`, so the AppHost omitted its conditional `tenants`
  resource and the test timed out waiting for a topology that had not been compiled. The
  harness now builds this source-topology E2E with `UseHexalithProjectReferences=true`.
- Corrected source-topology E2E: FAIL reproducibly, 0/1 twice. The exact method
  `LiveHandlerRoute_WithCurrentProjectionValidator_NeutralizesProjectionEvidence` returned
  HTTP 404 with reason `query_projection_missing` instead of executing `list-tenants`.
- Root cause boundary: Tenants' operational metadata calls returned 200, including the
  `tenants` and `global-administrators` bindings. EventStore also merged stale base
  registrations for `orders` and `inventory`, but the current sample service only discovers
  `counter` and `greeting`. At least one requested binding therefore returned no matching
  domain metadata; the atomic fail-closed load logged Event 6101 and skipped every admin
  index write. Consequently `admin:query-types:tenants` was absent, the handler-aware router
  fell back to the projection actor, and the live request returned
  `query_projection_missing`.
- Ownership: existing Story 2.7 owns this EventStore/Tenants compatibility closure. No
  duplicate Tenants-local story is created. The fix must remove/scope stale registrations
  or otherwise reconcile absent configured bindings without weakening the fail-closed rule
  for genuine metadata corruption or transport failure.
- NuGet inventory, package consumer, container publication/inspection, Story 1.16 named
  follow-up disposition, and owner approvals remain incomplete for an authorizing runtime.

## Final Decision

`still blocked`

Story 1.20 and Epic 1 remain `in-progress`. The lifecycle and AD-11 implementation blockers
are cleared at current commit `772cdfef...`, but the corrected source-topology provenance
gate fails reproducibly. Every parity row remains non-authorizing; source/package/container
identities are unapproved or unresolved, Story 1.16's named follow-up disposition is open,
and owner approval is absent.
