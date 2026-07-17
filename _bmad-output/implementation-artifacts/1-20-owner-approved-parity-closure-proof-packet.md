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
evidence_manifest_sha256: null
approval_subject_sha256: null
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
5. The failed candidate was below architecture AD-11's security baseline. Commit
   `772cdfefa8163704de0f57042af5b0507c1ac771` passes the exact SDK `10.0.302`,
   ASP.NET `10.0.10`, and installed `Microsoft.NETCore.App` `10.0.10` preflight. Story 2.7's
   correction is committed at `fd8ab24d110e605dfd64487cda84a301126d337d`, is recorded
   `done`, and passes the source-topology provenance prerequisite,
   but this packet remains non-authorizing until every gate is rerun at one exact clean
   committed candidate SHA.
6. No named EventStore owner has reviewed completed passing exact-SHA evidence, and no
   manifest-governed package/container identities have been produced or approved.

### Scoped corrective item

Keep the lifecycle-cleanup and AD-11 entries as resolved implementation history: commit
`772cdfefa8163704de0f57042af5b0507c1ac771` passed the exact former lifecycle failure, its
complete six-test class, the 44-test live-sidecar lane, and the executable AD-11 preflight.
Story 2.7's committed `fd8ab24d110e605dfd64487cda84a301126d337d` correction removes the stale base `orders`/`inventory`
registrations and passes the corrected source-mode E2E with freshly persisted
`admin:query-types:tenants` state. Rerun that proof together with every remaining
production-path and package/container gate at one unchanged clean committed SHA, explicitly
disposition Story 1.16's retained follow-up recommendation, obtain named EventStore-owner
approval, and update this packet.

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
separately owned corrective item is recorded in
`_bmad-output/implementation-artifacts/deferred-work.md`; this evidence-only story
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
| Source-topology query provenance | `implementation-complete/reverification-required` | Story 2.7 is `done`; committed correction `fd8ab24d110e605dfd64487cda84a301126d337d` removes the stale base sample bindings. The exact Debug source-topology E2E passes 1/1, returns `HandlerComputed` provenance, and reads freshly persisted `admin:query-types:tenants` state containing `list-tenants`. | Story 2.7's implementation prerequisite is satisfied. Rerun it at the exact clean committed candidate SHA selected for Story 1.20 before authorization. |
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
   changes only `_bmad-output/`, and records the completed results, hybrid evidence manifest,
   raw-log bundle URL/hash, artifact identities, and durable approval references. It leaves
   `documentation_commit_sha: null`, `final_decision: still blocked`, and
   `authorize_consumer_migration: false`; A alone grants no authority.
3. Pointer-only commit **B**, whose direct parent is A, changes only
   `documentation_commit_sha` from `null` to A's 40-hex SHA. The field identifies A, never B,
   so neither commit self-references. Any other semantic or file change invalidates B.
4. The executable structural procedure below verifies A's equal candidate/tested-runtime pin,
   sole parent, evidence-only path boundary, completed capability rows, durable evidence and
   approval pins; B's direct parent and pointer to A; and the exact one-field packet change.
5. Authorizing commit **C** must be B's direct child. Its verifier accepts only the packet,
   story, and sprint-status files; requires their file modes to remain unchanged; reconstructs
   the exact permitted decision/status/date/blocker-comment edits; and revalidates A's completed
   Story 1.16 review, closed Story 1.20 prerequisites, evidence identities, and approvals before
   migration authorization can become true.

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
printf '%s\n' "$CANDIDATE_SHA" > "$EVIDENCE_ROOT/candidate-source-sha.txt"

assert_candidate_identity() {
  test "$(git rev-parse --verify --end-of-options 'HEAD^{commit}')" = "$CANDIDATE_SHA"
}

assert_candidate_identity

APPROVAL_ROLE_ALLOWLIST='_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json'
test -s "$APPROVAL_ROLE_ALLOWLIST"
jq -e '
  .schema == "hexalith.eventstore.github-approval-role-allowlist/v1" and
  .repository == "Hexalith/Hexalith.EventStore" and
  (.roles | type == "object") and
  (.roles | keys | sort) ==
    ["architecture_owner", "eventstore_owner", "release_owner", "story_1_16_reviewer"] and
  all(.roles[]; type == "array" and length > 0 and length == (unique | length) and
    all(.[]; type == "string" and test("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$")))
' "$APPROVAL_ROLE_ALLOWLIST" >/dev/null

validate_github_approval_api_url() {
  local api_url="$1"
  [[ "$api_url" =~ ^https://api\.github\.com/repos/Hexalith/Hexalith\.EventStore/(issues/comments/[0-9]+|pulls/[0-9]+/reviews/[0-9]+)$ ]]
}

fetch_github_role_record() {
  local api_url="$1"
  local role="$2"
  local record_output="$3"
  local metadata_output="$4"
  local response
  local login
  local html_url
  local body_sha256
  local -a curl_headers=(
    -H 'Accept: application/vnd.github+json'
    -H 'X-GitHub-Api-Version: 2022-11-28'
  )
  validate_github_approval_api_url "$api_url"
  response="$(mktemp)"
  if test -n "${GITHUB_TOKEN:-}"; then
    curl_headers+=(-H "Authorization: Bearer $GITHUB_TOKEN")
  fi
  curl --fail --silent --show-error --location \
    "${curl_headers[@]}" "$api_url" > "$response"
  login="$(jq -er '.user.login | select(type == "string" and test("\\S"))' "$response")"
  html_url="$(jq -er '.html_url | select(type == "string" and startswith("https://github.com/Hexalith/Hexalith.EventStore/"))' "$response")"
  jq -e --arg role "$role" --arg login "$login" '
    .roles[$role] | type == "array" and index($login) != null
  ' "$APPROVAL_ROLE_ALLOWLIST" >/dev/null
  jq -jer '.body | select(type == "string" and test("\\S"))' "$response" > "$record_output"
  test -s "$record_output"
  jq -e 'type == "object"' "$record_output" >/dev/null
  body_sha256="$(sha256sum "$record_output" | awk '{print $1}')"
  jq -n \
    --arg api_url "$api_url" \
    --arg html_url "$html_url" \
    --arg login "$login" \
    --arg role "$role" \
    --arg body_sha256 "$body_sha256" \
    '{api_url: $api_url, html_url: $html_url, login: $login,
      role: $role, body_sha256: $body_sha256}' > "$metadata_output"
  rm -f -- "$response"
}

verify_github_role_record() {
  local record="$1"
  local metadata="$2"
  local expected_role="$3"
  local fetched_record
  local fetched_metadata
  local api_url
  test -s "$record"
  test -s "$metadata"
  api_url="$(jq -er --arg role "$expected_role" '
    select(.role == $role) | .api_url
  ' "$metadata")"
  fetched_record="$(mktemp)"
  fetched_metadata="$(mktemp)"
  fetch_github_role_record "$api_url" "$expected_role" \
    "$fetched_record" "$fetched_metadata"
  cmp --silent "$record" "$fetched_record"
  cmp --silent "$metadata" "$fetched_metadata"
  rm -f -- "$fetched_record" "$fetched_metadata"
}

# Fail closed on AD-11 before any candidate build or publication-capable gate.
AD11_CHECKED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
REPOSITORY_URL="$(git config --get remote.origin.url)"
test "$REPOSITORY_URL" = 'https://github.com/Hexalith/Hexalith.EventStore.git'
REPOSITORY_SDK_VERSION="$(jq -er '.sdk.version | select(type == "string" and test("\\S"))' global.json)"
REPOSITORY_SDK_ROLL_FORWARD="$(jq -er '.sdk.rollForward | select(type == "string" and test("\\S"))' global.json)"
INSTALLED_SDK_VERSION="$(dotnet --version)"
# AD-11 ASP.NET pins are centrally owned by Hexalith.Builds (no longer redefined
# locally), so resolve evaluated PackageVersion items from the real import graph.
# Checking out only the Builds submodule is a source checkout, not a candidate
# restore/build/publish gate.
git submodule update --init --checkout -- references/Hexalith.Builds
ASPNET_PIN_EVALUATOR="$GATE_ROOT/evaluate-package-versions.proj"
ASPNET_PIN_OUTPUT="$EVIDENCE_ROOT/effective-package-versions.txt"
cat > "$ASPNET_PIN_EVALUATOR" <<EOF
<Project>
  <Import Project="$CHECKOUT/Directory.Packages.props" />
  <Target Name="WriteEffectivePackageVersions">
    <WriteLinesToFile File="$ASPNET_PIN_OUTPUT"
                      Lines="@(PackageVersion->'%(Identity) %(Version)')"
                      Overwrite="true"
                      Encoding="UTF-8" />
  </Target>
</Project>
EOF
chmod a-w "$ASPNET_PIN_EVALUATOR"

evaluate_package_versions() {
  rm -f "$ASPNET_PIN_OUTPUT"
  dotnet msbuild "$ASPNET_PIN_EVALUATOR" -nologo \
    -t:WriteEffectivePackageVersions >/dev/null
  test -s "$ASPNET_PIN_OUTPUT"
}

discover_effective_aspnet_pin_pairs() {
  local evaluated_items="$1"
  awk '$1 ~ /^Microsoft\.AspNetCore\./ { effective[$1] = $2 }
    END { for (id in effective) print id, effective[id] }' "$evaluated_items" |
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

evaluate_package_versions
mapfile -t ASPNET_PIN_PAIRS < <(discover_effective_aspnet_pin_pairs "$ASPNET_PIN_OUTPUT")
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
  local replacement_metadata="$3"
  local approved_login
  local durable_source
  approved_login="$(jq -er '.login' "$replacement_metadata")"
  durable_source="$(jq -er '.html_url' "$replacement_metadata")"
  jq -e \
    --arg checked_at "$checked_at" \
    --arg repository "$REPOSITORY_URL" \
    --arg source_sha "$CANDIDATE_SHA" \
    --arg sdk_version "$REPOSITORY_SDK_VERSION" \
    --arg sdk_roll_forward "$REPOSITORY_SDK_ROLL_FORWARD" \
    --arg installed_sdk "$INSTALLED_SDK_VERSION" \
    --arg aspnet_version "$REPOSITORY_ASPNET_VERSION" \
    --arg installed_aspnet "$INSTALLED_ASPNET_VERSION" \
    --arg runtime_version "$INSTALLED_RUNTIME_VERSION" \
    --arg approved_login "$approved_login" \
    --arg durable_source "$durable_source" '
      def nonblank: type == "string" and test("\\S");
      def version_parts: split(".") | map(tonumber);
      type == "object" and
      .action == "ad11-baseline-replacement" and
      .owner == $approved_login and
      (.approved_at | nonblank) and
      .durable_source == $durable_source and
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
AD11_REPLACEMENT_GITHUB_METADATA="$EVIDENCE_ROOT/ad11-replacement-authority.github.json"
if ad11_exact_baseline; then
  AD11_MODE='exact-baseline'
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
  AD11_MODE='durable-replacement'
  : "${AD11_REPLACEMENT_APPROVAL_API_URL:?exact AD-11 baseline absent; set GitHub approval API URL}"
  test ! -e "$AD11_REPLACEMENT_EVIDENCE"
  test ! -e "$AD11_REPLACEMENT_GITHUB_METADATA"
  fetch_github_role_record "$AD11_REPLACEMENT_APPROVAL_API_URL" architecture_owner \
    "$AD11_REPLACEMENT_EVIDENCE" "$AD11_REPLACEMENT_GITHUB_METADATA"
  chmod a-w "$AD11_REPLACEMENT_EVIDENCE" "$AD11_REPLACEMENT_GITHUB_METADATA"
  validate_ad11_replacement "$AD11_CHECKED_AT" "$AD11_REPLACEMENT_EVIDENCE" \
    "$AD11_REPLACEMENT_GITHUB_METADATA"
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
    --arg authority_github_metadata_file "$(basename "$AD11_REPLACEMENT_GITHUB_METADATA")" \
    --arg authority_github_metadata_sha256 "$(sha256sum "$AD11_REPLACEMENT_GITHUB_METADATA" | awk '{print $1}')" \
    '{mode: "durable-replacement", checked_at: $checked_at, repository: $repository,
      source_sha: $source_sha, sdk_version: $sdk_version,
      sdk_roll_forward: $sdk_roll_forward, installed_sdk_version: $installed_sdk_version,
      aspnet_version: $aspnet_version, installed_aspnet_version: $installed_aspnet_version,
      runtime_version: $runtime_version, authority_file: $authority_file,
      authority_sha256: $authority_sha256,
      authority_github_metadata_file: $authority_github_metadata_file,
      authority_github_metadata_sha256: $authority_github_metadata_sha256}' > "$AD11_PREFLIGHT"
fi
chmod a-w "$AD11_PREFLIGHT"
(
  cd "$EVIDENCE_ROOT"
  sha256sum ad11-preflight.json > ad11-preflight.sha256
)

assert_ad11_current() {
  local checked_at
  local current_aspnet_version
  local -a current_pairs=()
  local -a current_runtime_versions=()
  assert_candidate_identity
  test "$(jq -er '.sdk.version' global.json)" = "$REPOSITORY_SDK_VERSION"
  test "$(jq -er '.sdk.rollForward' global.json)" = "$REPOSITORY_SDK_ROLL_FORWARD"
  test "$(dotnet --version)" = "$INSTALLED_SDK_VERSION"
  evaluate_package_versions
  mapfile -t current_pairs < <(discover_effective_aspnet_pin_pairs "$ASPNET_PIN_OUTPUT")
  current_aspnet_version="$(validate_aspnet_pin_band "${current_pairs[@]}")"
  test "$current_aspnet_version" = "$REPOSITORY_ASPNET_VERSION"
  dotnet --list-runtimes > "$EVIDENCE_ROOT/dotnet-runtimes-current.txt"
  mapfile -t current_runtime_versions < <(
    discover_bound_runtime_versions "$EVIDENCE_ROOT/dotnet-runtimes-current.txt" \
      "$current_aspnet_version"
  )
  test "${#current_runtime_versions[@]}" -eq 2
  test "${current_runtime_versions[0]}" = "$INSTALLED_ASPNET_VERSION"
  test "${current_runtime_versions[1]}" = "$INSTALLED_RUNTIME_VERSION"
  if test "$AD11_MODE" = 'exact-baseline'; then
    ad11_exact_baseline
  else
    checked_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    validate_ad11_replacement "$checked_at" "$AD11_REPLACEMENT_EVIDENCE" \
      "$AD11_REPLACEMENT_GITHUB_METADATA"
  fi
}

mapfile -t ROOT_SUBMODULES < <(
  git config -f .gitmodules --get-regexp '^submodule\..*\.path$' |
    awk '$2 ~ /^references\// { print $2 }' | LC_ALL=C sort
)
test "${#ROOT_SUBMODULES[@]}" -gt 0
git submodule update --init --checkout -- "${ROOT_SUBMODULES[@]}"

assert_repository_clean_allow_generated() {
  local repository="$1"
  local ignored_entry
  local ignored_path
  test -z "$(git -C "$repository" status --porcelain=v1 --untracked-files=all)" || return 1
  while IFS= read -r -d '' ignored_entry; do
    ignored_path="${ignored_entry:3}"
    case "$ignored_path" in
      */bin/|*/bin/*|*/obj/|*/obj/*) ;;
      *) printf 'unexpected ignored source input: %s:%s\n' \
           "$repository" "$ignored_path" >&2; return 1 ;;
    esac
  done < <(git -C "$repository" status --porcelain=v1 --ignored=matching \
    --untracked-files=all -z)
}

assert_candidate_tree_clean() {
  local repository
  assert_candidate_identity
  test -z "$(git status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"
  for repository in . "${ROOT_SUBMODULES[@]}"; do
    assert_repository_clean_allow_generated "$repository"
  done
}

capture_source_state() {
  local output_file="$1"
  local repository
  {
    printf 'candidate=%s\n' "$(git rev-parse HEAD)"
    printf 'root-status:\n'
    git status --porcelain=v1 --untracked-files=all --ignore-submodules=none
    printf 'root-ignored:\n'
    git status --porcelain=v1 --ignored=matching --untracked-files=all \
      --ignore-submodules=none
    printf 'root-submodules:\n'
    git submodule status
    for repository in "${ROOT_SUBMODULES[@]}"; do
      printf 'submodule=%s head=%s\n' "$repository" "$(git -C "$repository" rev-parse HEAD)"
      printf 'submodule-status=%s:\n' "$repository"
      git -C "$repository" status --porcelain=v1 --untracked-files=all
      printf 'submodule-ignored=%s:\n' "$repository"
      git -C "$repository" status --porcelain=v1 --ignored=matching --untracked-files=all
    done
  } > "$output_file"
}

assert_candidate_tree_clean
capture_source_state "$EVIDENCE_ROOT/source-state-before.txt"
{
  printf 'captured_at=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  uname -a
  dotnet --info
  dapr --version
  docker version
} > "$EVIDENCE_ROOT/environment.txt" 2>&1

export DOTNET_CLI_HOME="$GATE_ROOT/dotnet-home"
export NUGET_PACKAGES="$GATE_ROOT/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$GATE_ROOT/nuget-http-cache"
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH"

fresh_release() {
  local project="$1"
  local use_project_references="${2:-false}"
  local evidence_name="${3:-$(basename "${project%.csproj}")}"
  local project_directory="${project%/*}"
  local gate_status
  assert_ad11_current
  assert_candidate_tree_clean
  if rm -rf "$project_directory/bin/Release" "$project_directory/obj/Release" &&
    dotnet restore "$project" \
      --configfile nuget.config \
      --packages "$NUGET_PACKAGES" \
      -p:UseHexalithProjectReferences="$use_project_references" \
      -p:NuGetAudit=false \
      -p:MinVerVersionOverride=1.0.0 \
      2>&1 | tee "$EVIDENCE_ROOT/$evidence_name.restore.log" &&
    dotnet build "$project" \
      --configuration Release \
      --no-restore \
      -m:1 \
      -p:UseHexalithProjectReferences="$use_project_references" \
      -p:NuGetAudit=false \
      -p:MinVerVersionOverride=1.0.0 \
      2>&1 | tee "$EVIDENCE_ROOT/$evidence_name.build.log"; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_tree_clean
  return "$gate_status"
}

fresh_debug_source() {
  local project="$1"
  local evidence_name="$2"
  local project_directory="${project%/*}"
  local gate_status
  assert_ad11_current
  assert_candidate_tree_clean
  if rm -rf "$project_directory/bin/Debug" "$project_directory/obj/Debug" &&
    dotnet restore "$project" \
      --configfile nuget.config \
      --packages "$NUGET_PACKAGES" \
      -p:Configuration=Debug \
      -p:UseHexalithProjectReferences=true \
      -p:NuGetAudit=false \
      -p:MinVerVersionOverride=1.0.0 \
      2>&1 | tee "$EVIDENCE_ROOT/$evidence_name.restore.log" &&
    dotnet build "$project" \
      --configuration Debug \
      --no-restore \
      -m:1 \
      -p:UseHexalithProjectReferences=true \
      -p:NuGetAudit=false \
      -p:MinVerVersionOverride=1.0.0 \
      2>&1 | tee "$EVIDENCE_ROOT/$evidence_name.build.log"; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_tree_clean
  return "$gate_status"
}

validate_xunit_result() {
  local results="$1"
  python3 - "$results" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
total = int(root.attrib.get("total", "0"))
passed = int(root.attrib.get("passed", "0"))
failed = int(root.attrib.get("failed", "0"))
errors = int(root.attrib.get("errors", "0"))
if total <= 0 or passed <= 0 or failed != 0 or errors != 0:
    raise SystemExit(1)
PY
}

run_xunit_class() {
  local assembly="$1"
  local class_name="$2"
  local evidence_name="$3"
  local methods="$EVIDENCE_ROOT/$evidence_name.methods.txt"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  local gate_status
  assert_ad11_current
  assert_candidate_tree_clean
  if test -f "$assembly" &&
    dotnet "$assembly" -noColor -list methods -class "$class_name" | tee "$methods" &&
    grep -Fq "$class_name." "$methods" &&
    dotnet "$assembly" -noColor -class "$class_name" -xml "$results" &&
    validate_xunit_result "$results"; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_tree_clean
  return "$gate_status"
}

run_xunit_method() {
  local assembly="$1"
  local method_name="$2"
  local evidence_name="$3"
  local methods="$EVIDENCE_ROOT/$evidence_name.methods.txt"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  local gate_status
  assert_ad11_current
  assert_candidate_tree_clean
  if test -f "$assembly" &&
    dotnet "$assembly" -noColor -list methods -method "$method_name" | tee "$methods" &&
    grep -Fxq "$method_name" "$methods" &&
    dotnet "$assembly" -noColor -method "$method_name" -xml "$results" &&
    validate_xunit_result "$results"; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_tree_clean
  return "$gate_status"
}

run_xunit_all() {
  local assembly="$1"
  local evidence_name="$2"
  local results="$EVIDENCE_ROOT/$evidence_name.xml"
  local gate_status
  assert_ad11_current
  assert_candidate_tree_clean
  if test -f "$assembly" &&
    dotnet "$assembly" -noColor -xml "$results" &&
    validate_xunit_result "$results"; then
    gate_status=0
  else
    gate_status=$?
  fi
  assert_candidate_tree_clean
  return "$gate_status"
}
```

The xUnit v3 in-process runner returns exit code zero when a filter matches zero tests or
when every matched test is skipped. The `-list methods` checks reject zero-match filters,
and `validate_xunit_result` requires both a positive `total` and at least one passed test
with no failures or runner errors. `fresh_release` and `fresh_debug_source` delete the
configuration-specific `bin`/`obj`, restore through committed inputs into a new cache, and
build before an assembly is invoked. Every build and test revalidates AD-11 plus regular,
untracked, and ignored source inputs. After all gates, the runtime-source proof is:

```bash
assert_ad11_current
assert_candidate_tree_clean
capture_source_state "$EVIDENCE_ROOT/source-state-after.txt"
```

Ignored `bin`/`obj` evidence output is expected only after the pre-gate ignored-input
check; it is never used as a source or configuration input.

## Parity Capability Matrix

All rows remain `still blocked`. Current source and test paths identify the evidence that
must be rerun; they are not accepted as exact-SHA closure evidence in this packet.

### Read-model and projection-checkpoint erasure

- capability-id: `read-model-erasure`
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
- limitation-ids: `erasure-admission-write-fence-races`, `erasure-caller-slot-completeness`,
  `erasure-authenticated-stitched-e2e`, `erasure-global-administrator-boundary`
- limitations requiring owner disposition: admission/write-fence races, caller-owned slot
  completeness, authenticated stitched E2E, and global-administrator boundary
- rollback: retain the consumer erasure and checkpoint-reset implementation

### Coordinated batching

- capability-id: `coordinated-batching`
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
- limitation-ids: `batch-store-transaction-capability`, `batch-etag-behavior`,
  `batch-corrupt-or-abandoned-envelope`, `batch-terminal-receipt-retention`,
  `batch-cross-profile-boundary`
- limitations requiring owner disposition: store transaction capability, ETag behavior,
  corrupt/abandoned envelopes, terminal receipt retention, and cross-profile boundaries
- rollback: retain the consumer's coordinated detail/index write path and retry protocol

### Six-state lifecycle and route-bound provenance

- capability-id: `lifecycle-and-route-provenance`
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

fresh_debug_source \
  tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj \
  lifecycle-provenance-source-topology
run_xunit_class \
  tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests.dll \
  Hexalith.EventStore.IntegrationTests.ContractTests.QueryResponseProvenanceE2ETests \
  lifecycle-provenance-e2e
```

- closure result and persisted read-back: contract, client, query-routing, server,
  generator, and Sample regression projects passed at the candidate SHA; the required
  query-provenance E2E lane was not reached after the live regression failure
- compatibility decision awaiting approval: preserve `Unknown`, all six operational values,
  the legacy metadata ABI, and the rule that ETag never supplies lifecycle or version
- limitation-ids: `lifecycle-unknown-compatibility`, `lifecycle-operational-value-abi`,
  `lifecycle-legacy-metadata-abi`, `lifecycle-etag-evidence-boundary`
- rollback: retain consumer freshness adapters and fail closed on non-projection evidence

### Duplicate and out-of-order production-handler safety

- capability-id: `delivery-order-and-idempotency`
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
- limitation-ids: `delivery-legacy-project-route-boundary`,
  `delivery-mixed-writer-cutover-boundary`
- rollback: preserve the complete pre-cutover fleet and backup boundary; never roll one
  writer back across the global v2 marker

### Full paged-rebuild equivalence

- capability-id: `paged-rebuild-equivalence`
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
- limitation-ids: `rebuild-complete-prefix-resource-bounds`,
  `rebuild-coordinated-promotion-boundary`, `rebuild-cancellation-resume-semantics`,
  `rebuild-deferred-review-items`
- limitations requiring owner disposition: complete-prefix memory/byte bounds, coordinated
  promotion boundary, cancellation/resume semantics, and every deferred Story 1.19 review item
- rollback: retain the last complete live model and consumer rebuild path; never promote
  page-only or incomplete staged output

### Cursor compatibility

- capability-id: `cursor-compatibility`
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
- limitation-ids: `cursor-key-ring-loss-or-replacement`
- limitation: routine retained-key rotation is distinct from key-ring loss/replacement
- rollback: preserve opaque cursor handling and the existing consumer compatibility path

### Asynchronous projection persistence

- capability-id: `asynchronous-projection-persistence`
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
- limitation-ids: `async-hand-written-state-writes`, `async-stable-work-identity`,
  `async-dead-letter-terminal-cleanup`, `async-null-metadata`
- limitations requiring owner disposition: hand-written state writes, stable work identity,
  dead-letter/terminal cleanup, and null metadata behavior
- rollback: retain the consumer's asynchronous persistence handoff

### Multiple projections per domain

- capability-id: `multiple-projections-per-domain`
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
- limitation-ids: `multiprojection-versioned-route-boundary`,
  `multiprojection-legacy-route-boundary`, `multiprojection-exact-route-identity`,
  `multiprojection-bounded-result-contract`
- rollback: retain consumer-owned detail/index handlers and route composition

## Cross-Cutting Compatibility And Release Gate

- capability-id: `cross-cutting-release-compatibility`
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
assert_ad11_current
assert_candidate_tree_clean
if dotnet restore Hexalith.EventStore.slnx \
    --configfile nuget.config \
    --packages "$NUGET_PACKAGES" \
    -p:UseHexalithProjectReferences=false \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0 \
    2>&1 | tee "$EVIDENCE_ROOT/solution-restore.log" &&
  dotnet build Hexalith.EventStore.slnx \
    --configuration Release \
    --no-restore \
    -m:1 \
    -warnaserror \
    -p:UseHexalithProjectReferences=false \
    -p:NuGetAudit=false \
    -p:MinVerVersionOverride=1.0.0 \
    2>&1 | tee "$EVIDENCE_ROOT/solution-build.log"; then
  solution_gate_status=0
else
  solution_gate_status=$?
fi
assert_candidate_tree_clean
test "$solution_gate_status" -eq 0

DISCOVERED_TEST_PROJECTS="$EVIDENCE_ROOT/discovered-test-projects.txt"
MANDATORY_TEST_PROJECTS="$EVIDENCE_ROOT/mandatory-test-projects.txt"
python3 - Hexalith.EventStore.slnx > "$DISCOVERED_TEST_PROJECTS" <<'PY'
import pathlib
import sys
import xml.etree.ElementTree as ET

solution = ET.parse(sys.argv[1]).getroot()
projects = []
for element in solution.iter("Project"):
    path = element.attrib.get("Path", "")
    if not path.endswith(".csproj"):
        continue
    project = ET.parse(path).getroot()
    if any(
        reference.attrib.get("Include") == "xunit.v3"
        for reference in project.iter("PackageReference")
    ):
        projects.append(path)
for path in sorted(projects):
    print(path)
PY
cat > "$MANDATORY_TEST_PROJECTS" <<'TEST_PROJECT_PATHS'
samples/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj
tests/Hexalith.EventStore.Admin.Abstractions.Tests/Hexalith.EventStore.Admin.Abstractions.Tests.csproj
tests/Hexalith.EventStore.Admin.Cli.Tests/Hexalith.EventStore.Admin.Cli.Tests.csproj
tests/Hexalith.EventStore.Admin.Mcp.Tests/Hexalith.EventStore.Admin.Mcp.Tests.csproj
tests/Hexalith.EventStore.Admin.Server.Host.Tests/Hexalith.EventStore.Admin.Server.Host.Tests.csproj
tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj
tests/Hexalith.EventStore.Admin.UI.E2E/Hexalith.EventStore.Admin.UI.E2E.csproj
tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj
tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj
tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj
tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj
tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Hexalith.EventStore.DeferredWorkGovernance.Tests.csproj
tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj
tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj
tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests.csproj
tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj
tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj
tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj
tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj
tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj
tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj
tests/Hexalith.EventStore.Testing.Integration.Tests/Hexalith.EventStore.Testing.Integration.Tests.csproj
tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj
TEST_PROJECT_PATHS
diff -u "$MANDATORY_TEST_PROJECTS" "$DISCOVERED_TEST_PROJECTS"

while IFS='|' read -r project assembly evidence_name; do
  test -n "$project"
  fresh_release "$project" false "$evidence_name"
  run_xunit_all "$assembly" "$evidence_name"
done <<'TEST_PROJECTS'
tests/Hexalith.EventStore.Admin.Abstractions.Tests/Hexalith.EventStore.Admin.Abstractions.Tests.csproj|tests/Hexalith.EventStore.Admin.Abstractions.Tests/bin/Release/net10.0/Hexalith.EventStore.Admin.Abstractions.Tests.dll|crosscut-admin-abstractions
tests/Hexalith.EventStore.Admin.Cli.Tests/Hexalith.EventStore.Admin.Cli.Tests.csproj|tests/Hexalith.EventStore.Admin.Cli.Tests/bin/Release/net10.0/Hexalith.EventStore.Admin.Cli.Tests.dll|crosscut-admin-cli
tests/Hexalith.EventStore.Admin.Mcp.Tests/Hexalith.EventStore.Admin.Mcp.Tests.csproj|tests/Hexalith.EventStore.Admin.Mcp.Tests/bin/Release/net10.0/Hexalith.EventStore.Admin.Mcp.Tests.dll|crosscut-admin-mcp
tests/Hexalith.EventStore.Admin.Server.Host.Tests/Hexalith.EventStore.Admin.Server.Host.Tests.csproj|tests/Hexalith.EventStore.Admin.Server.Host.Tests/bin/Release/net10.0/Hexalith.EventStore.Admin.Server.Host.Tests.dll|crosscut-admin-server-host
tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj|tests/Hexalith.EventStore.Admin.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Admin.Server.Tests.dll|crosscut-admin-server
tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj|tests/Hexalith.EventStore.Admin.UI.Tests/bin/Release/net10.0/Hexalith.EventStore.Admin.UI.Tests.dll|crosscut-admin-ui
tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj|tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll|crosscut-apphost
tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj|tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests.dll|crosscut-contracts
tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj|tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll|crosscut-client
tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/Hexalith.EventStore.DeferredWorkGovernance.Tests.csproj|tests/Hexalith.EventStore.DeferredWorkGovernance.Tests/bin/Release/net10.0/Hexalith.EventStore.DeferredWorkGovernance.Tests.dll|crosscut-deferred-work-governance
tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj|tests/Hexalith.EventStore.DomainService.Tests/bin/Release/net10.0/Hexalith.EventStore.DomainService.Tests.dll|crosscut-domain-service
tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests.csproj|tests/Hexalith.EventStore.OperationalEvidence.Validator.Tests/bin/Release/net10.0/Hexalith.EventStore.OperationalEvidence.Validator.Tests.dll|crosscut-operational-evidence
tests/Hexalith.EventStore.QueryRouting.Tests/Hexalith.EventStore.QueryRouting.Tests.csproj|tests/Hexalith.EventStore.QueryRouting.Tests/bin/Release/net10.0/Hexalith.EventStore.QueryRouting.Tests.dll|crosscut-query-routing
tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj|tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll|crosscut-server
tests/Hexalith.EventStore.RestApi.Generators.Tests/Hexalith.EventStore.RestApi.Generators.Tests.csproj|tests/Hexalith.EventStore.RestApi.Generators.Tests/bin/Release/net10.0/Hexalith.EventStore.RestApi.Generators.Tests.dll|crosscut-rest-generator
tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj|tests/Hexalith.EventStore.Sample.Tests/bin/Release/net10.0/Hexalith.EventStore.Sample.Tests.dll|crosscut-sample
tests/Hexalith.EventStore.SignalR.Tests/Hexalith.EventStore.SignalR.Tests.csproj|tests/Hexalith.EventStore.SignalR.Tests/bin/Release/net10.0/Hexalith.EventStore.SignalR.Tests.dll|crosscut-signalr
tests/Hexalith.EventStore.Testing.Integration.Tests/Hexalith.EventStore.Testing.Integration.Tests.csproj|tests/Hexalith.EventStore.Testing.Integration.Tests/bin/Release/net10.0/Hexalith.EventStore.Testing.Integration.Tests.dll|crosscut-testing-integration
tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj|tests/Hexalith.EventStore.Testing.Tests/bin/Release/net10.0/Hexalith.EventStore.Testing.Tests.dll|crosscut-testing
samples/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj|samples/Hexalith.EventStore.Sample.Tests/bin/Release/net10.0/Hexalith.EventStore.Sample.Tests.dll|crosscut-sample-quickstart
TEST_PROJECTS

fresh_release \
  tests/Hexalith.EventStore.Admin.UI.E2E/Hexalith.EventStore.Admin.UI.E2E.csproj false \
  crosscut-admin-ui-e2e
pwsh tests/Hexalith.EventStore.Admin.UI.E2E/bin/Release/net10.0/playwright.ps1 \
  install --with-deps chromium 2>&1 | tee "$EVIDENCE_ROOT/crosscut-admin-ui-e2e.playwright.log"
run_xunit_all \
  tests/Hexalith.EventStore.Admin.UI.E2E/bin/Release/net10.0/Hexalith.EventStore.Admin.UI.E2E.dll \
  crosscut-admin-ui-e2e

# Reproduce the configured Debug/source-mode AppHost topology guard lane separately.
fresh_debug_source \
  tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj \
  crosscut-apphost-source-topology
run_xunit_class \
  tests/Hexalith.EventStore.AppHost.Tests/bin/Debug/net10.0/Hexalith.EventStore.AppHost.Tests.dll \
  Hexalith.EventStore.AppHost.Tests.Configuration.TenantsApiLaunchSettingsTests \
  crosscut-apphost-source-topology

# Package-mode Release compilation and source-mode Debug execution are distinct evidence.
fresh_release \
  tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj false \
  crosscut-integration-package-build
fresh_debug_source \
  tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj \
  crosscut-source-topology
run_xunit_class \
  tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0/Hexalith.EventStore.IntegrationTests.dll \
  Hexalith.EventStore.IntegrationTests.ContractTests.QueryResponseProvenanceE2ETests \
  crosscut-source-topology-provenance

fresh_release tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj false crosscut-live-sidecar
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
printf '%s\n' "$PACKAGE_VERSION" > "$EVIDENCE_ROOT/package-version.txt"
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

assert_ad11_current
assert_candidate_tree_clean
if rm -rf "$PACKAGE_OUTPUT" &&
  find src -type d \( -path '*/bin/Release' -o -path '*/obj/Release' \) \
    -prune -exec rm -rf {} + &&
  python3 scripts/pack-release-packages.py "$PACKAGE_OUTPUT" "$PACKAGE_VERSION" \
    2>&1 | tee "$EVIDENCE_ROOT/package-build.log" &&
  validate_literal_package_inventory "$PACKAGE_OUTPUT" "$PACKAGE_VERSION" "$EXPECTED_PACKAGE_IDS" \
    2>&1 | tee "$EVIDENCE_ROOT/literal-package-inventory.log" &&
  python3 scripts/validate-nuget-packages.py "$PACKAGE_OUTPUT" \
    2>&1 | tee "$EVIDENCE_ROOT/package-validator.log" &&
  python3 scripts/validate-consumer-package-references.py "$PACKAGE_OUTPUT" \
    2>&1 | tee "$EVIDENCE_ROOT/package-consumer-validation.log" &&
  find "$PACKAGE_OUTPUT" -maxdepth 1 -type f -name '*.nupkg' -printf '%f\n' \
    | LC_ALL=C sort > "$EVIDENCE_ROOT/package-files.txt" &&
  (cd "$PACKAGE_OUTPUT" && sha256sum -- *.nupkg | LC_ALL=C sort -k2) \
    > "$EVIDENCE_ROOT/nuget-sha256.txt" &&
  test "$(wc -l < "$EVIDENCE_ROOT/nuget-sha256.txt")" -eq 14; then
  package_gate_status=0
else
  package_gate_status=$?
fi
assert_candidate_tree_clean
test "$package_gate_status" -eq 0
```

- required exact container publication, immutable inspection, and provenance commands:

```bash
set -euo pipefail
: "${RELEASE_AUTHORITY_APPROVAL_API_URL:?set the release-owner GitHub approval API URL}"

PUBLISH_CONTAINER_REGISTRY='registry.hexalith.com'
PUBLISH_CONTAINER_REPOSITORY='eventstore'
IMAGE_REPOSITORY="$PUBLISH_CONTAINER_REGISTRY/$PUBLISH_CONTAINER_REPOSITORY"
IMAGE_TAG="quarantine-proof-${CANDIDATE_SHA}"
test "$IMAGE_REPOSITORY" = 'registry.hexalith.com/eventstore'

# Freeze the authority bytes before the first publication-capable command.
AUTHORITY_EVIDENCE="$EVIDENCE_ROOT/release-owner-publication-authority.json"
AUTHORITY_GITHUB_METADATA="$EVIDENCE_ROOT/release-owner-publication-authority.github.json"
AUTHORITY_CHECKED_AT_EVIDENCE="$EVIDENCE_ROOT/release-owner-publication-authority.checked-at.txt"
test ! -e "$AUTHORITY_EVIDENCE"
test ! -e "$AUTHORITY_GITHUB_METADATA"
test ! -e "$AUTHORITY_CHECKED_AT_EVIDENCE"
fetch_github_role_record "$RELEASE_AUTHORITY_APPROVAL_API_URL" release_owner \
  "$AUTHORITY_EVIDENCE" "$AUTHORITY_GITHUB_METADATA"
chmod a-w "$AUTHORITY_EVIDENCE" "$AUTHORITY_GITHUB_METADATA"

validate_publication_authority() {
  local checked_at="$1"
  local approved_login
  local durable_source
  approved_login="$(jq -er '.login' "$AUTHORITY_GITHUB_METADATA")"
  durable_source="$(jq -er '.html_url' "$AUTHORITY_GITHUB_METADATA")"
  jq -e \
    --arg checked_at "$checked_at" \
    --arg repository "$IMAGE_REPOSITORY" \
    --arg tag "$IMAGE_TAG" \
    --arg source_sha "$CANDIDATE_SHA" \
    --arg approved_login "$approved_login" \
    --arg durable_source "$durable_source" '
      def nonblank: type == "string" and test("\\S");
      type == "object" and
      .action == "container-publication" and
      .owner == $approved_login and
      (.authorized_at | nonblank) and
      .durable_source == $durable_source and
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
  assert_repository_clean_allow_generated "$1"
}

validate_published_manifest() {
  local manifest_file="$1"
  local image_digest="$2"
  local platforms_file="$3"
  local manifest_sha256
  [[ "$image_digest" =~ ^sha256:[0-9a-f]{64}$ ]] || return 1
  jq -e '.manifests | type == "array" and length == 2' "$manifest_file" \
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
assert_ad11_current
assert_candidate_tree_clean
dotnet restore "$CONTAINER_PROJECT" \
  --configfile nuget.config \
  --packages "$NUGET_PACKAGES" \
  -p:UseHexalithProjectReferences=false \
  -p:NuGetAudit=false \
  -p:MinVerVersionOverride=1.0.0

# Immediately before publication, recompute one final action-time readiness decision. The
# function's last command is the authority predicate; no build, hash, or other mutable action
# occurs between this complete readiness check and dotnet publish.
prepare_publication_action() {
  AUTHORITY_CHECKED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  printf '%s\n' "$AUTHORITY_CHECKED_AT" > "$AUTHORITY_CHECKED_AT_EVIDENCE"
  chmod a-w "$AUTHORITY_CHECKED_AT_EVIDENCE"
  AUTHORITY_SHA256="$(sha256sum "$AUTHORITY_EVIDENCE" | awk '{print $1}')"
  AUTHORITY_GITHUB_METADATA_SHA256="$(
    sha256sum "$AUTHORITY_GITHUB_METADATA" | awk '{print $1}'
  )"
  AUTHORITY_CHECKED_AT_SHA256="$(
    sha256sum "$AUTHORITY_CHECKED_AT_EVIDENCE" | awk '{print $1}'
  )"
  assert_ad11_current
  for repository in . "${ROOT_SUBMODULES[@]}"; do
    assert_publication_source_clean "$repository"
  done
  capture_source_state "$EVIDENCE_ROOT/source-state-before-publication.txt"
  validate_publication_authority "$AUTHORITY_CHECKED_AT"
}

prepare_publication_action
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
test "$(sha256sum "$AUTHORITY_GITHUB_METADATA" | awk '{print $1}')" \
  = "$AUTHORITY_GITHUB_METADATA_SHA256"
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
  --arg authority_github_metadata_file "$(basename "$AUTHORITY_GITHUB_METADATA")" \
  --arg authority_github_metadata_sha256 "$AUTHORITY_GITHUB_METADATA_SHA256" \
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
      github_metadata_file: $authority_github_metadata_file,
      github_metadata_sha256: $authority_github_metadata_sha256,
      checked_at: $authority_checked_at,
      checked_at_file: $authority_checked_at_file,
      checked_at_sha256: $authority_checked_at_sha256
    }}' \
  > "$EVIDENCE_ROOT/container-provenance.json"
assert_ad11_current
assert_candidate_tree_clean
capture_source_state "$EVIDENCE_ROOT/source-state-after-publication.txt"
```

The authority check revalidates the immutable evidence copy at a fresh action timestamp after
candidate/source-cleanliness checks and before the first registry write. The checked-at file,
both hashes, exact authorized repository/tag/source SHA, raw-manifest digest, platform set, and
actual publish properties survive in provenance. Publication always uses a visibly quarantined
tag; consumers can use only the separately verified immutable digest, so a failed post-publication
inspection cannot expose an apparently approved proof tag. This is an evidence-integrity preflight only:
it does not create human
release authority, replace registry authentication/authorization, or substitute for the
later proof-result approval and distinct release-owner disposition.

### Deterministic Raw Evidence Bundle

After every gate succeeds, build the raw bundle locally before upload. The fixed test-result
inventory prevents a missing lane from becoming self-consistent evidence, every result is parsed
again, filtered runs retain their method listing, and the tarball is byte-reproducible:

```bash
EXPECTED_FILTERED_XUNIT_RESULTS=(
  erasure-dapr-store erasure-coordinator erasure-lifecycle-admission
  erasure-lifecycle-transport erasure-live
  batch-DaprReadModelBatchTests batch-DaprReadModelStoreTests
  batch-InMemoryReadModelStoreTests batch-ReadModelBatchStoreTests batch-live
  lifecycle-ProjectionAdapterContractTests lifecycle-SubmitQueryResponseTests
  lifecycle-ReadModelFreshnessTests lifecycle-EventStoreGatewayClientTests
  lifecycle-query-routing lifecycle-QueryRouterTests lifecycle-QueriesControllerTests
  lifecycle-CachingProjectionActorTests lifecycle-QueryResponseProvenancePersistenceTests
  lifecycle-RestApiControllerGenerationTests
  lifecycle-RestApiGeneratedControllerErrorSemanticsTests lifecycle-sample-runtime
  lifecycle-provenance-e2e
  delivery-ProjectionDeliveryIdempotencyCoordinatorTests
  delivery-ProjectionDeliveryReconcilerTests delivery-ProjectionDeliveryCutoverTests
  delivery-NamedProjectionDispatchCoordinatorTests
  delivery-DaprProjectionDeliveryRetrySchedulerTests
  delivery-DaprProjectionActivationOutboxTests
  delivery-NamedProjectionDispatchLiveSidecarTests
  delivery-ProjectionDeliveryCutoverLiveSidecarTests
  rebuild-domain-dispatcher rebuild-ProjectionRebuildProductionPathTests
  rebuild-ProjectionUpdateOrchestratorTests rebuild-ProjectionRebuildCheckpointStoreTests
  rebuild-ProjectionLifecycleActorTests rebuild-live-persisted-equivalence
  cursor-QueryCursorCodecTests cursor-QueryCursorScopeTests cursor-persisted-key-ring
  async-domain-dispatcher async-NamedProjectionDispatchCoordinatorTests
  async-DaprProjectionDeliveryRetrySchedulerTests
  async-DaprProjectionActivationOutboxTests async-live
  multiprojection-domain-dispatcher multiprojection-catalog-publication
  multiprojection-NamedProjectionDispatchCoordinatorTests
  multiprojection-NamedProjectionRouteCatalogTests multiprojection-live
  crosscut-apphost-source-topology crosscut-source-topology-provenance
  crosscut-release-manifest crosscut-domain-compatibility
)
EXPECTED_FULL_XUNIT_RESULTS=(
  crosscut-admin-abstractions crosscut-admin-cli crosscut-admin-mcp
  crosscut-admin-server-host crosscut-admin-server crosscut-admin-ui crosscut-apphost
  crosscut-contracts crosscut-client crosscut-deferred-work-governance
  crosscut-domain-service crosscut-operational-evidence crosscut-query-routing
  crosscut-server crosscut-rest-generator crosscut-sample crosscut-signalr
  crosscut-testing-integration crosscut-testing crosscut-sample-quickstart
  crosscut-admin-ui-e2e crosscut-live-sidecar
)
REQUIRED_RAW_LOGS=(
  solution-restore.log solution-build.log crosscut-admin-ui-e2e.playwright.log
  package-build.log literal-package-inventory.log package-validator.log
  package-consumer-validation.log container-publish.txt
)
RAW_EVIDENCE_BUNDLE_FILE="$GATE_ROOT/story-1-20-raw-evidence.tar.gz"
LATEST_SUCCESSFUL_GATE_COMPLETED_AT_FILE="$EVIDENCE_ROOT/latest-successful-gate-completed-at.txt"

build_raw_evidence_bundle() {
  local bundle_stage
  local evidence_name
  local required_log
  bundle_stage="$(mktemp -d)"
  for evidence_name in "${EXPECTED_FILTERED_XUNIT_RESULTS[@]}"; do
    test -s "$EVIDENCE_ROOT/$evidence_name.xml"
    test -s "$EVIDENCE_ROOT/$evidence_name.methods.txt"
    validate_xunit_result "$EVIDENCE_ROOT/$evidence_name.xml"
  done
  for evidence_name in "${EXPECTED_FULL_XUNIT_RESULTS[@]}"; do
    test -s "$EVIDENCE_ROOT/$evidence_name.xml"
    validate_xunit_result "$EVIDENCE_ROOT/$evidence_name.xml"
  done
  for required_log in "${REQUIRED_RAW_LOGS[@]}"; do
    test -s "$EVIDENCE_ROOT/$required_log"
  done
  find "$EVIDENCE_ROOT" -maxdepth 1 -type f \
    \( -name '*.log' -o -name '*.xml' -o -name '*.methods.txt' \) \
    -exec cp -- {} "$bundle_stage/" \;
  for required_log in "${REQUIRED_RAW_LOGS[@]}"; do
    cp -- "$EVIDENCE_ROOT/$required_log" "$bundle_stage/$required_log"
  done
  (
    cd "$bundle_stage"
    find . -maxdepth 1 -type f -printf '%P\n' | LC_ALL=C sort \
      > raw-evidence-files.txt
    tar --sort=name --mtime='UTC 1970-01-01' --owner=0 --group=0 --numeric-owner \
      -cf - -- * | gzip -n > "$RAW_EVIDENCE_BUNDLE_FILE"
  )
  rm -rf -- "$bundle_stage"
  test -s "$RAW_EVIDENCE_BUNDLE_FILE"
  sha256sum "$RAW_EVIDENCE_BUNDLE_FILE"
}

printf '%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  > "$LATEST_SUCCESSFUL_GATE_COMPLETED_AT_FILE"
build_raw_evidence_bundle
```

Upload that exact file to immutable HTTPS storage. Do not rebuild from a different directory or
compression tool after recording its hash.

### Hybrid Durable Evidence Retention

After uploading the deterministic raw bundle, run
this block before evidence commit A. Critical identity/provenance files are copied into the
repository; the larger raw bundle is retrieved and byte-compared with the locally generated
bundle before it is bound by HTTPS URL and SHA-256 without bloating Git. The block prints the
approval-subject path/hash after all non-approval evidence is frozen, then prompts for the two
GitHub API URLs if they were not exported. Post the exact subject hash and scope in both approval
records before answering those prompts; a non-interactive run without both URLs fails closed:

```bash
set -euo pipefail
: "${RAW_EVIDENCE_BUNDLE_URL:?set immutable HTTPS raw-evidence bundle URL}"
: "${RAW_EVIDENCE_BUNDLE_SHA256:?set raw-evidence bundle SHA-256}"
: "${STORY_1_16_REVIEW_API_URL:?set Story 1.16 reviewer GitHub approval API URL}"
[[ "$RAW_EVIDENCE_BUNDLE_URL" =~ ^https://[^[:space:]]+$ ]]
[[ "$RAW_EVIDENCE_BUNDLE_SHA256" =~ ^[0-9a-f]{64}$ ]]
test -s "$RAW_EVIDENCE_BUNDLE_FILE"
test "$(sha256sum "$RAW_EVIDENCE_BUNDLE_FILE" | awk '{print $1}')" \
  = "$RAW_EVIDENCE_BUNDLE_SHA256"
FETCHED_RAW_EVIDENCE_BUNDLE="$(mktemp)"
curl --fail --silent --show-error --location "$RAW_EVIDENCE_BUNDLE_URL" \
  > "$FETCHED_RAW_EVIDENCE_BUNDLE"
test "$(sha256sum "$FETCHED_RAW_EVIDENCE_BUNDLE" | awk '{print $1}')" \
  = "$RAW_EVIDENCE_BUNDLE_SHA256"
cmp --silent "$RAW_EVIDENCE_BUNDLE_FILE" "$FETCHED_RAW_EVIDENCE_BUNDLE"
RAW_BUNDLE_INSPECTION_DIRECTORY="$(mktemp -d)"
tar -tzf "$FETCHED_RAW_EVIDENCE_BUNDLE" > "$RAW_BUNDLE_INSPECTION_DIRECTORY/archive-files.txt"
test -z "$(awk '/(^\/|(^|\/)\.\.(\/|$))/' "$RAW_BUNDLE_INSPECTION_DIRECTORY/archive-files.txt")"
tar -xzf "$FETCHED_RAW_EVIDENCE_BUNDLE" -C "$RAW_BUNDLE_INSPECTION_DIRECTORY"
diff -u \
  "$RAW_BUNDLE_INSPECTION_DIRECTORY/raw-evidence-files.txt" \
  <(find "$RAW_BUNDLE_INSPECTION_DIRECTORY" -maxdepth 1 -type f \
      ! -name archive-files.txt ! -name raw-evidence-files.txt -printf '%f\n' | LC_ALL=C sort)
for evidence_name in "${EXPECTED_FILTERED_XUNIT_RESULTS[@]}"; do
  validate_xunit_result "$RAW_BUNDLE_INSPECTION_DIRECTORY/$evidence_name.xml"
  test -s "$RAW_BUNDLE_INSPECTION_DIRECTORY/$evidence_name.methods.txt"
done
for evidence_name in "${EXPECTED_FULL_XUNIT_RESULTS[@]}"; do
  validate_xunit_result "$RAW_BUNDLE_INSPECTION_DIRECTORY/$evidence_name.xml"
done
for required_log in "${REQUIRED_RAW_LOGS[@]}"; do
  test -s "$RAW_BUNDLE_INSPECTION_DIRECTORY/$required_log"
done
rm -rf -- "$RAW_BUNDLE_INSPECTION_DIRECTORY"
rm -f -- "$FETCHED_RAW_EVIDENCE_BUNDLE"
assert_ad11_current
assert_candidate_tree_clean
capture_source_state "$EVIDENCE_ROOT/source-state-after.txt"

APPROVED_PACKAGE_VERSION="$(cat "$EVIDENCE_ROOT/package-version.txt")"
APPROVED_PACKAGE_HASH_MANIFEST_SHA256="$(
  sha256sum "$EVIDENCE_ROOT/nuget-sha256.txt" | awk '{print $1}'
)"
APPROVED_CONTAINER_REPOSITORY="$(jq -er '.repository' "$EVIDENCE_ROOT/container-provenance.json")"
APPROVED_CONTAINER_DIGEST="$(jq -er '.digest' "$EVIDENCE_ROOT/container-provenance.json")"
test -n "$APPROVED_PACKAGE_VERSION"
[[ "$APPROVED_PACKAGE_HASH_MANIFEST_SHA256" =~ ^[0-9a-f]{64}$ ]]
test "$APPROVED_CONTAINER_REPOSITORY" = 'registry.hexalith.com/eventstore'
[[ "$APPROVED_CONTAINER_DIGEST" =~ ^sha256:[0-9a-f]{64}$ ]]

STORY_1_16_REVIEW_RECORD="$EVIDENCE_ROOT/story-1-16-followup-review.json"
STORY_1_16_REVIEW_GITHUB_METADATA="$EVIDENCE_ROOT/story-1-16-followup-review.github.json"
fetch_github_role_record "$STORY_1_16_REVIEW_API_URL" story_1_16_reviewer \
  "$STORY_1_16_REVIEW_RECORD" "$STORY_1_16_REVIEW_GITHUB_METADATA"
STORY_1_16_REVIEWER_LOGIN="$(jq -er '.login' "$STORY_1_16_REVIEW_GITHUB_METADATA")"
STORY_1_16_REVIEW_SOURCE="$(jq -er '.html_url' "$STORY_1_16_REVIEW_GITHUB_METADATA")"
jq -e \
  --arg owner "$STORY_1_16_REVIEWER_LOGIN" \
  --arg durable_source "$STORY_1_16_REVIEW_SOURCE" \
  --arg repository "$REPOSITORY_URL" \
  --arg source_sha "$CANDIDATE_SHA" '
    def nonblank: type == "string" and test("\\S");
    type == "object" and
    .action == "story-1-16-followup-review" and
    .owner == $owner and
    .durable_source == $durable_source and
    (.reviewed_at | nonblank) and
    .disposition == "approved" and
    (.scope | type == "object") and
    .scope.repository == $repository and
    .scope.reviewed_runtime_sha == $source_sha and
    (.scope.summary | nonblank) and
    (.scope.verification | nonblank) and
    (.scope.residual_limitations | nonblank)
  ' "$STORY_1_16_REVIEW_RECORD" >/dev/null

DURABLE_EVIDENCE_DIRECTORY="$SOURCE_REPOSITORY/_bmad-output/implementation-artifacts/evidence/story-1-20/$CANDIDATE_SHA"
test ! -e "$DURABLE_EVIDENCE_DIRECTORY"
mkdir -p "$DURABLE_EVIDENCE_DIRECTORY"
CORE_EVIDENCE_FILES=(
  candidate-source-sha.txt
  ad11-preflight.json
  ad11-preflight.sha256
  effective-package-versions.txt
  dotnet-runtimes.txt
  dotnet-runtimes-current.txt
  environment.txt
  source-state-before.txt
  source-state-after.txt
  source-state-before-publication.txt
  source-state-after-publication.txt
  expected-package-ids.txt
  package-version.txt
  package-files.txt
  nuget-sha256.txt
  discovered-test-projects.txt
  mandatory-test-projects.txt
  generated-image-index.json
  generated-image-index.digest.txt
  capture-generated-image-index.targets
  container-inspect.txt
  container-manifest.json
  container-platforms.txt
  container-provenance.json
  release-owner-publication-authority.json
  release-owner-publication-authority.github.json
  release-owner-publication-authority.checked-at.txt
  story-1-16-followup-review.json
  story-1-16-followup-review.github.json
  latest-successful-gate-completed-at.txt
)
if test "$AD11_MODE" = 'durable-replacement'; then
  CORE_EVIDENCE_FILES+=(
    ad11-replacement-authority.json
    ad11-replacement-authority.github.json
  )
fi
for evidence_file in "${CORE_EVIDENCE_FILES[@]}"; do
  test -s "$EVIDENCE_ROOT/$evidence_file"
  cp -- "$EVIDENCE_ROOT/$evidence_file" "$DURABLE_EVIDENCE_DIRECTORY/$evidence_file"
done
jq -n \
  --arg url "$RAW_EVIDENCE_BUNDLE_URL" \
  --arg sha256 "$RAW_EVIDENCE_BUNDLE_SHA256" \
  '{url: $url, sha256: $sha256}' \
  > "$DURABLE_EVIDENCE_DIRECTORY/raw-evidence-bundle.json"
(
  cd "$DURABLE_EVIDENCE_DIRECTORY"
  printf '%s\n' "${CORE_EVIDENCE_FILES[@]}" raw-evidence-bundle.json \
    | LC_ALL=C sort -u > critical-evidence-expected-files.txt
  while IFS= read -r evidence_file; do
    test -f "$evidence_file"
    test ! -L "$evidence_file"
    [[ "$evidence_file" != */* ]]
    sha256sum -- "$evidence_file"
  done < critical-evidence-expected-files.txt
) > "$DURABLE_EVIDENCE_DIRECTORY/critical-evidence-sha256.txt"
CRITICAL_EVIDENCE_MANIFEST_SHA256="$(
  sha256sum "$DURABLE_EVIDENCE_DIRECTORY/critical-evidence-sha256.txt" | awk '{print $1}'
)"
[[ "$CRITICAL_EVIDENCE_MANIFEST_SHA256" =~ ^[0-9a-f]{64}$ ]]

APPROVAL_SUBJECT="$DURABLE_EVIDENCE_DIRECTORY/approval-subject.json"
jq -n \
  --arg repository "$REPOSITORY_URL" \
  --arg tested_runtime_sha "$CANDIDATE_SHA" \
  --arg evidence_manifest_sha256 "$CRITICAL_EVIDENCE_MANIFEST_SHA256" \
  --arg raw_evidence_bundle_sha256 "$RAW_EVIDENCE_BUNDLE_SHA256" \
  --arg package_version "$APPROVED_PACKAGE_VERSION" \
  --arg package_hash_manifest_sha256 "$APPROVED_PACKAGE_HASH_MANIFEST_SHA256" \
  --arg container_repository "$APPROVED_CONTAINER_REPOSITORY" \
  --arg container_digest "$APPROVED_CONTAINER_DIGEST" \
  '{
    schema: "hexalith.eventstore.story-1-20-approval-subject/v1",
    repository: $repository,
    tested_runtime_sha: $tested_runtime_sha,
    evidence_manifest_sha256: $evidence_manifest_sha256,
    raw_evidence_bundle_sha256: $raw_evidence_bundle_sha256,
    package_version: $package_version,
    package_hash_manifest_sha256: $package_hash_manifest_sha256,
    container_repository: $container_repository,
    container_digest: $container_digest,
    capabilities: [
      {id:"read-model-erasure",limitations:["erasure-admission-write-fence-races","erasure-caller-slot-completeness","erasure-authenticated-stitched-e2e","erasure-global-administrator-boundary"]},
      {id:"coordinated-batching",limitations:["batch-store-transaction-capability","batch-etag-behavior","batch-corrupt-or-abandoned-envelope","batch-terminal-receipt-retention","batch-cross-profile-boundary"]},
      {id:"lifecycle-and-route-provenance",limitations:["lifecycle-unknown-compatibility","lifecycle-operational-value-abi","lifecycle-legacy-metadata-abi","lifecycle-etag-evidence-boundary"]},
      {id:"delivery-order-and-idempotency",limitations:["delivery-legacy-project-route-boundary","delivery-mixed-writer-cutover-boundary"]},
      {id:"paged-rebuild-equivalence",limitations:["rebuild-complete-prefix-resource-bounds","rebuild-coordinated-promotion-boundary","rebuild-cancellation-resume-semantics","rebuild-deferred-review-items"]},
      {id:"cursor-compatibility",limitations:["cursor-key-ring-loss-or-replacement"]},
      {id:"asynchronous-projection-persistence",limitations:["async-hand-written-state-writes","async-stable-work-identity","async-dead-letter-terminal-cleanup","async-null-metadata"]},
      {id:"multiple-projections-per-domain",limitations:["multiprojection-versioned-route-boundary","multiprojection-legacy-route-boundary","multiprojection-exact-route-identity","multiprojection-bounded-result-contract"]},
      {id:"cross-cutting-release-compatibility",limitations:["release-package-inventory-and-byte-identity","release-container-registry-and-platform-provenance","release-durable-evidence-retention","release-owner-authority-and-migration"]}
    ] | map(. + {classification:"available"})
  }' > "$APPROVAL_SUBJECT"
APPROVAL_SUBJECT_SHA256="$(sha256sum "$APPROVAL_SUBJECT" | awk '{print $1}')"
[[ "$APPROVAL_SUBJECT_SHA256" =~ ^[0-9a-f]{64}$ ]]
printf '%s\n' \
  "approval_subject_file=$APPROVAL_SUBJECT" \
  "approval_subject_sha256=$APPROVAL_SUBJECT_SHA256" \
  "latest_successful_gate_completed_at=$(cat "$LATEST_SUCCESSFUL_GATE_COMPLETED_AT_FILE")"
if test -z "${EVENTSTORE_OWNER_APPROVAL_API_URL:-}"; then
  printf 'EventStore-owner GitHub approval API URL: ' >&2
  IFS= read -r EVENTSTORE_OWNER_APPROVAL_API_URL
fi
if test -z "${RELEASE_OWNER_DISPOSITION_API_URL:-}"; then
  printf 'Release-owner GitHub disposition API URL: ' >&2
  IFS= read -r RELEASE_OWNER_DISPOSITION_API_URL
fi
validate_github_approval_api_url "$EVENTSTORE_OWNER_APPROVAL_API_URL"
validate_github_approval_api_url "$RELEASE_OWNER_DISPOSITION_API_URL"
FINAL_APPROVAL_CHECKED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
EXPECTED_LIMITATION_IDS="$(mktemp)"
jq -er '.capabilities[].limitations[]' "$APPROVAL_SUBJECT" | LC_ALL=C sort -u \
  > "$EXPECTED_LIMITATION_IDS"

EVENTSTORE_OWNER_APPROVAL_RECORD="$EVIDENCE_ROOT/eventstore-owner-proof-approval.json"
EVENTSTORE_OWNER_APPROVAL_GITHUB_METADATA="$EVIDENCE_ROOT/eventstore-owner-proof-approval.github.json"
RELEASE_OWNER_DISPOSITION_RECORD="$EVIDENCE_ROOT/release-owner-final-disposition.json"
RELEASE_OWNER_DISPOSITION_GITHUB_METADATA="$EVIDENCE_ROOT/release-owner-final-disposition.github.json"
fetch_github_role_record "$EVENTSTORE_OWNER_APPROVAL_API_URL" eventstore_owner \
  "$EVENTSTORE_OWNER_APPROVAL_RECORD" "$EVENTSTORE_OWNER_APPROVAL_GITHUB_METADATA"
fetch_github_role_record "$RELEASE_OWNER_DISPOSITION_API_URL" release_owner \
  "$RELEASE_OWNER_DISPOSITION_RECORD" "$RELEASE_OWNER_DISPOSITION_GITHUB_METADATA"

validate_final_owner_record() {
  local record="$1"
  local metadata="$2"
  local action="$3"
  local decision="$4"
  local approved_login
  local durable_source
  approved_login="$(jq -er '.login' "$metadata")"
  durable_source="$(jq -er '.html_url' "$metadata")"
  jq -e \
    --arg checked_at "$FINAL_APPROVAL_CHECKED_AT" \
    --arg action "$action" \
    --arg decision "$decision" \
    --arg repository "$REPOSITORY_URL" \
    --arg source_sha "$CANDIDATE_SHA" \
    --arg raw_sha256 "$RAW_EVIDENCE_BUNDLE_SHA256" \
    --arg package_version "$APPROVED_PACKAGE_VERSION" \
    --arg package_manifest_sha256 "$APPROVED_PACKAGE_HASH_MANIFEST_SHA256" \
    --arg container_repository "$APPROVED_CONTAINER_REPOSITORY" \
    --arg container_digest "$APPROVED_CONTAINER_DIGEST" \
    --arg approved_login "$approved_login" \
    --arg durable_source "$durable_source" \
    --arg evidence_manifest_sha256 "$CRITICAL_EVIDENCE_MANIFEST_SHA256" \
    --arg approval_subject_sha256 "$APPROVAL_SUBJECT_SHA256" \
    --arg latest_gate_completed_at "$(cat "$LATEST_SUCCESSFUL_GATE_COMPLETED_AT_FILE")" '
      def nonblank: type == "string" and test("\\S");
      type == "object" and
      .action == $action and
      .owner == $approved_login and
      (.approved_at | nonblank) and
      .durable_source == $durable_source and
      (.accepted_scope | nonblank) and
      (.limitations | type == "array" and length > 0 and
        all(.[]; type == "object" and (.id | nonblank) and
          .disposition == "accepted" and (.rationale | nonblank))) and
      .decision == $decision and
      (.scope | type == "object") and
      .scope.repository == $repository and
      .scope.tested_runtime_sha == $source_sha and
      .scope.raw_evidence_bundle_sha256 == $raw_sha256 and
      .scope.package_version == $package_version and
      .scope.package_hash_manifest_sha256 == $package_manifest_sha256 and
      .scope.container_repository == $container_repository and
      .scope.container_digest == $container_digest and
      .scope.evidence_manifest_sha256 == $evidence_manifest_sha256 and
      .scope.approval_subject_sha256 == $approval_subject_sha256 and
      .scope.latest_successful_gate_completed_at == $latest_gate_completed_at and
      ((.approved_at | fromdateiso8601) >= ($latest_gate_completed_at | fromdateiso8601)) and
      ((.approved_at | fromdateiso8601) <= ($checked_at | fromdateiso8601))
    ' "$record"
  diff -u "$EXPECTED_LIMITATION_IDS" \
    <(jq -er '.limitations[].id' "$record" | LC_ALL=C sort -u)
}

validate_final_owner_record \
  "$EVENTSTORE_OWNER_APPROVAL_RECORD" \
  "$EVENTSTORE_OWNER_APPROVAL_GITHUB_METADATA" \
  story-1-20-proof-approval \
  authorize-consumer-migration
validate_final_owner_record \
  "$RELEASE_OWNER_DISPOSITION_RECORD" \
  "$RELEASE_OWNER_DISPOSITION_GITHUB_METADATA" \
  story-1-20-release-disposition \
  approve-release-identities
cp -- "$EVENTSTORE_OWNER_APPROVAL_RECORD" \
  "$DURABLE_EVIDENCE_DIRECTORY/eventstore-owner-proof-approval.json"
cp -- "$EVENTSTORE_OWNER_APPROVAL_GITHUB_METADATA" \
  "$DURABLE_EVIDENCE_DIRECTORY/eventstore-owner-proof-approval.github.json"
cp -- "$RELEASE_OWNER_DISPOSITION_RECORD" \
  "$DURABLE_EVIDENCE_DIRECTORY/release-owner-final-disposition.json"
cp -- "$RELEASE_OWNER_DISPOSITION_GITHUB_METADATA" \
  "$DURABLE_EVIDENCE_DIRECTORY/release-owner-final-disposition.github.json"
chmod a-w \
  "$DURABLE_EVIDENCE_DIRECTORY/eventstore-owner-proof-approval.json" \
  "$DURABLE_EVIDENCE_DIRECTORY/eventstore-owner-proof-approval.github.json" \
  "$DURABLE_EVIDENCE_DIRECTORY/release-owner-final-disposition.json" \
  "$DURABLE_EVIDENCE_DIRECTORY/release-owner-final-disposition.github.json" \
  "$APPROVAL_SUBJECT"
EVENTSTORE_OWNER_APPROVAL_URL="$(
  jq -er '.html_url' \
    "$DURABLE_EVIDENCE_DIRECTORY/eventstore-owner-proof-approval.github.json"
)"
EVENTSTORE_OWNER_APPROVAL_SHA256="$(
  sha256sum "$DURABLE_EVIDENCE_DIRECTORY/eventstore-owner-proof-approval.json" \
    | awk '{print $1}'
)"
RELEASE_OWNER_DISPOSITION_URL="$(
  jq -er '.html_url' \
    "$DURABLE_EVIDENCE_DIRECTORY/release-owner-final-disposition.github.json"
)"
RELEASE_OWNER_DISPOSITION_SHA256="$(
  sha256sum "$DURABLE_EVIDENCE_DIRECTORY/release-owner-final-disposition.json" \
    | awk '{print $1}'
)"
printf '%s\n' \
  "evidence_manifest_sha256=$CRITICAL_EVIDENCE_MANIFEST_SHA256" \
  "approval_subject_sha256=$APPROVAL_SUBJECT_SHA256" \
  "raw_bundle_url=$RAW_EVIDENCE_BUNDLE_URL" \
  "raw_bundle_sha256=$RAW_EVIDENCE_BUNDLE_SHA256" \
  "eventstore_owner_approval_url=$EVENTSTORE_OWNER_APPROVAL_URL" \
  "eventstore_owner_approval_sha256=$EVENTSTORE_OWNER_APPROVAL_SHA256" \
  "release_owner_disposition_url=$RELEASE_OWNER_DISPOSITION_URL" \
  "release_owner_disposition_sha256=$RELEASE_OWNER_DISPOSITION_SHA256"
rm -f -- "$EXPECTED_LIMITATION_IDS"
```

Evidence commit A must copy the eight final printed values into packet front matter. Its
structural verifier below re-hashes the committed manifest, validates the raw-bundle record,
parses and binds both committed final-owner records, and binds package/container identities
before accepting A.

| Gate | Required result | Evidence target | Required disposition owner | Current result |
| --- | --- | --- | --- | --- |
| AD-11 readiness | exact SDK `10.0.302` plus matching ASP.NET and installed `Microsoft.NETCore.App` runtime `10.0.10`, or complete, scoped, unexpired replacement authority binding those versions before candidate gates | `ad11-preflight.json`, runtime inventory, its SHA-256, and replacement-authority copy/hash when used | Architecture owner and EventStore build/release maintainer | PASS in the 2026-07-17 current-HEAD readiness audit at `772cdfef...`; every later selected candidate must repeat the preflight |
| Exact committed source | Same 40-hex SHA before and after gates; clean regular and ignored inputs before and after every gate | `source-state-*.txt`, submodule SHAs, `environment.txt` | EventStore owner | PASS for failed candidate `85877902f8d60a466ab90cd8b68b53838863db1c` |
| Release build and tests | warning-free solution build; at least one passed and no failed/error tests for every configured project/filter | restore/build logs plus XML and method-list files under `$EVIDENCE_ROOT` | EventStore owner | **FAIL / non-authorizing**; at current commit `772cdfef...`, the Release build, broad lanes, former lifecycle method/class, and full live-sidecar passed, but the corrected source-mode query-provenance E2E failed twice with 404 / `query_projection_missing` |
| NuGet inventory | exact 14-ID set, one approved version, 14 SHA-256 values, package-only consumer success | `package-files.txt`, package/validator/consumer logs, `package-version.txt`, `nuget-sha256.txt` | EventStore release owner | NOT RUN |
| Container runtime | freshly revalidated pre-publication release-owner authority; clean candidate source; quarantined publication tag; immutable registry digest equal to the raw-manifest SHA-256; exact `linux/amd64` and `linux/arm64`; digest-to-tested-SHA provenance | immutable authority and checked-at copies/hashes, `container-inspect.txt`, raw manifest/hash, exact platform set, `container-provenance.json` | EventStore release owner | NOT RUN |
| Durable evidence | critical identity/provenance and exact final-approval records committed under `_bmad-output`; immutable external raw bundle bound by HTTPS URL and SHA-256 | `critical-evidence-sha256.txt`, `raw-evidence-bundle.json`, `eventstore-owner-proof-approval.json`, `release-owner-final-disposition.json`, packet front-matter pins | EventStore owner and EventStore release owner | NOT RUN |
| Limitations and migration | every matrix-row limitation accepted or rejected by a named reviewer in a durable source | signed review record or PR URL and date | EventStore owner | NOT RUN |

- limitation-ids: `release-package-inventory-and-byte-identity`,
  `release-container-registry-and-platform-provenance`, `release-durable-evidence-retention`,
  `release-owner-authority-and-migration`
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

### Evidence Commit A, Pointer-Only Commit B, And Authorizing Commit C Verification

Run this repository-root procedure only after the completed proof results and distinct
EventStore-owner and release-owner dispositions exist in durable external sources. It
proves that evidence commit A is a single-parent, `_bmad-output/`-only child of the equal
candidate/tested runtime while recording every evidence/approval identity and remaining fail
closed; its direct child B changes exactly one front-matter field to point back to A; and B's
direct child C performs only the exact verified authorization/status transition:

```bash
set -euo pipefail
PACKET='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
STORY='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-and-runtime-pin.md'
SPRINT_STATUS='_bmad-output/implementation-artifacts/sprint-status.yaml'
FOLLOWUP_SPEC='_bmad-output/implementation-artifacts/spec-1-11-complete-projection-freshness-lifecycle.md'
DEFERRED_WORK='_bmad-output/implementation-artifacts/deferred-work.md'
RELEASE_PACKAGES='tools/release-packages.json'
APPROVAL_ROLE_ALLOWLIST='_bmad-output/implementation-artifacts/1-20-github-approval-role-allowlist.json'
: "${EVIDENCE_COMMIT_A:?set the evidence commit A SHA}"
: "${POINTER_COMMIT_B:?set the pointer-only commit B SHA}"
: "${AUTHORIZATION_COMMIT_C:?set the authorizing commit C SHA}"

EVIDENCE_COMMIT_A="$(git rev-parse --verify --end-of-options "${EVIDENCE_COMMIT_A}^{commit}")"
POINTER_COMMIT_B="$(git rev-parse --verify --end-of-options "${POINTER_COMMIT_B}^{commit}")"
AUTHORIZATION_COMMIT_C="$(git rev-parse --verify --end-of-options "${AUTHORIZATION_COMMIT_C}^{commit}")"
[[ "$EVIDENCE_COMMIT_A" =~ ^[0-9a-f]{40}$ ]]
[[ "$POINTER_COMMIT_B" =~ ^[0-9a-f]{40}$ ]]
[[ "$AUTHORIZATION_COMMIT_C" =~ ^[0-9a-f]{40}$ ]]
test "$(git rev-list --parents -n 1 "$POINTER_COMMIT_B" | awk '{print NF}')" -eq 2
test "$(git rev-parse --verify --end-of-options "${POINTER_COMMIT_B}^")" = "$EVIDENCE_COMMIT_A"
test "$(git rev-list --parents -n 1 "$AUTHORIZATION_COMMIT_C" | awk '{print NF}')" -eq 2
test "$(git rev-parse --verify --end-of-options "${AUTHORIZATION_COMMIT_C}^")" = "$POINTER_COMMIT_B"

A_PACKET="$(mktemp)"
RUNTIME_PACKET="$(mktemp)"
RUNTIME_EXECUTABLE_BLOCKS="$(mktemp)"
A_EXECUTABLE_BLOCKS="$(mktemp)"
A_APPROVAL_ROLE_ALLOWLIST="$(mktemp)"
B_PACKET="$(mktemp)"
EXPECTED_B_PACKET="$(mktemp)"
A_STORY="$(mktemp)"
A_SPRINT_STATUS="$(mktemp)"
A_FOLLOWUP_SPEC="$(mktemp)"
A_DEFERRED_WORK="$(mktemp)"
A_RELEASE_PACKAGES="$(mktemp)"
A_EXPECTED_PACKAGE_IDS="$(mktemp)"
A_EXPECTED_PACKAGE_FILES="$(mktemp)"
A_EVIDENCE_MANIFEST="$(mktemp)"
A_RAW_EVIDENCE_BUNDLE="$(mktemp)"
A_RAW_EVIDENCE_DIRECTORY="$(mktemp -d)"
A_EXPECTED_FILTERED_RESULTS="$(mktemp)"
A_EXPECTED_FULL_RESULTS="$(mktemp)"
A_EXPECTED_RESULT_FILES="$(mktemp)"
A_EXPECTED_REQUIRED_LOGS="$(mktemp)"
A_EXPECTED_LIMITATION_IDS="$(mktemp)"
A_EXPECTED_CAPABILITY_IDS="$(mktemp)"
A_APPROVAL_SUBJECT="$(mktemp)"
A_EXPECTED_SOURCE_STATE="$(mktemp)"
A_PACKAGE_HASHES="$(mktemp)"
A_CONTAINER_PROVENANCE="$(mktemp)"
B_STORY="$(mktemp)"
B_SPRINT_STATUS="$(mktemp)"
C_PACKET="$(mktemp)"
C_STORY="$(mktemp)"
C_SPRINT_STATUS="$(mktemp)"
EXPECTED_C_PACKET="$(mktemp)"
EXPECTED_C_STORY="$(mktemp)"
EXPECTED_C_SPRINT_STATUS="$(mktemp)"
git show "$EVIDENCE_COMMIT_A:$PACKET" > "$A_PACKET"
git show "$POINTER_COMMIT_B:$PACKET" > "$B_PACKET"
git show "$EVIDENCE_COMMIT_A:$STORY" > "$A_STORY"
git show "$EVIDENCE_COMMIT_A:$SPRINT_STATUS" > "$A_SPRINT_STATUS"
git show "$EVIDENCE_COMMIT_A:$FOLLOWUP_SPEC" > "$A_FOLLOWUP_SPEC"
git show "$EVIDENCE_COMMIT_A:$DEFERRED_WORK" > "$A_DEFERRED_WORK"
git show "$POINTER_COMMIT_B:$STORY" > "$B_STORY"
git show "$POINTER_COMMIT_B:$SPRINT_STATUS" > "$B_SPRINT_STATUS"
git show "$AUTHORIZATION_COMMIT_C:$PACKET" > "$C_PACKET"
git show "$AUTHORIZATION_COMMIT_C:$STORY" > "$C_STORY"
git show "$AUTHORIZATION_COMMIT_C:$SPRINT_STATUS" > "$C_SPRINT_STATUS"

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

verify_committed_github_role_record() {
  local record="$1"
  local metadata="$2"
  local expected_role="$3"
  local response
  local api_url
  local login
  local html_url
  local -a curl_headers=(
    -H 'Accept: application/vnd.github+json'
    -H 'X-GitHub-Api-Version: 2022-11-28'
  )
  test -f "$record" && test ! -L "$record" && test -s "$record"
  test -f "$metadata" && test ! -L "$metadata" && test -s "$metadata"
  api_url="$(jq -er --arg role "$expected_role" '
    select(.role == $role) | .api_url
  ' "$metadata")"
  [[ "$api_url" =~ ^https://api\.github\.com/repos/Hexalith/Hexalith\.EventStore/(issues/comments/[0-9]+|pulls/[0-9]+/reviews/[0-9]+)$ ]]
  login="$(jq -er '.login' "$metadata")"
  html_url="$(jq -er '.html_url' "$metadata")"
  jq -e --arg role "$expected_role" --arg login "$login" '
    .roles[$role] | type == "array" and index($login) != null
  ' "$A_APPROVAL_ROLE_ALLOWLIST" >/dev/null
  response="$(mktemp)"
  if test -n "${GITHUB_TOKEN:-}"; then
    curl_headers+=(-H "Authorization: Bearer $GITHUB_TOKEN")
  fi
  curl --fail --silent --show-error --location \
    "${curl_headers[@]}" "$api_url" > "$response"
  test "$(jq -er '.user.login' "$response")" = "$login"
  test "$(jq -er '.html_url' "$response")" = "$html_url"
  test "$(jq -jr '.body' "$response" | sha256sum | awk '{print $1}')" \
    = "$(jq -er '.body_sha256' "$metadata")"
  cmp --silent "$record" <(jq -jr '.body' "$response")
  rm -f -- "$response"
}

A_TESTED_RUNTIME_SHA="$(front_value "$A_PACKET" tested_runtime_sha)"
[[ "$A_TESTED_RUNTIME_SHA" =~ ^[0-9a-f]{40}$ ]]
A_CANDIDATE_SOURCE_SHA="$(front_value "$A_PACKET" candidate_source_sha)"
test "$A_CANDIDATE_SOURCE_SHA" = "$A_TESTED_RUNTIME_SHA"
A_TESTED_RUNTIME_COMMIT="$(git rev-parse --verify --end-of-options \
  "${A_TESTED_RUNTIME_SHA}^{commit}")"
test "$A_TESTED_RUNTIME_COMMIT" = "$A_TESTED_RUNTIME_SHA"
git show "$A_TESTED_RUNTIME_COMMIT:$PACKET" > "$RUNTIME_PACKET"
git show "$A_TESTED_RUNTIME_COMMIT:$APPROVAL_ROLE_ALLOWLIST" \
  > "$A_APPROVAL_ROLE_ALLOWLIST"
jq -e '
  .schema == "hexalith.eventstore.github-approval-role-allowlist/v1" and
  .repository == "Hexalith/Hexalith.EventStore" and
  (.roles | type == "object") and
  (.roles | keys | sort) ==
    ["architecture_owner", "eventstore_owner", "release_owner", "story_1_16_reviewer"] and
  all(.roles[]; type == "array" and length > 0 and length == (unique | length))
' "$A_APPROVAL_ROLE_ALLOWLIST" >/dev/null
extract_executable_blocks() {
  local packet="$1"
  local output="$2"
  awk '
    /^```bash$/ { in_block = 1; print; next }
    in_block { print }
    in_block && /^```$/ { in_block = 0 }
    END { if (in_block) exit 1 }
  ' "$packet" > "$output"
  test -s "$output"
}
extract_executable_blocks "$RUNTIME_PACKET" "$RUNTIME_EXECUTABLE_BLOCKS"
extract_executable_blocks "$A_PACKET" "$A_EXECUTABLE_BLOCKS"
cmp --silent "$RUNTIME_EXECUTABLE_BLOCKS" "$A_EXECUTABLE_BLOCKS"
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
printf '%s\n' "${A_CHANGED_FILES[@]}" | grep -Fxq "$PACKET"
printf '%s\n' "${A_CHANGED_FILES[@]}" | grep -Fxq "$FOLLOWUP_SPEC"
A_EVIDENCE_DIRECTORY="_bmad-output/implementation-artifacts/evidence/story-1-20/$A_TESTED_RUNTIME_SHA"
printf '%s\n' "${A_CHANGED_FILES[@]}" | grep -Fq "$A_EVIDENCE_DIRECTORY/"
A_EVIDENCE_COPY="$(mktemp -d)"
git archive "$EVIDENCE_COMMIT_A" "$A_EVIDENCE_DIRECTORY" | tar -x -C "$A_EVIDENCE_COPY"
A_EVIDENCE_ROOT="$A_EVIDENCE_COPY/$A_EVIDENCE_DIRECTORY"
test -d "$A_EVIDENCE_ROOT"
git show "$A_TESTED_RUNTIME_COMMIT:$RELEASE_PACKAGES" > "$A_RELEASE_PACKAGES"
A_EVIDENCE_MANIFEST_SHA256="$(front_value "$A_PACKET" evidence_manifest_sha256)"
[[ "$A_EVIDENCE_MANIFEST_SHA256" =~ ^[0-9a-f]{64}$ ]]
cat > "$A_EVIDENCE_MANIFEST" <<'CORE_EVIDENCE_FILES'
ad11-preflight.json
ad11-preflight.sha256
candidate-source-sha.txt
capture-generated-image-index.targets
container-inspect.txt
container-manifest.json
container-platforms.txt
container-provenance.json
discovered-test-projects.txt
dotnet-runtimes-current.txt
dotnet-runtimes.txt
effective-package-versions.txt
environment.txt
expected-package-ids.txt
generated-image-index.digest.txt
generated-image-index.json
latest-successful-gate-completed-at.txt
mandatory-test-projects.txt
nuget-sha256.txt
package-files.txt
package-version.txt
raw-evidence-bundle.json
release-owner-publication-authority.checked-at.txt
release-owner-publication-authority.github.json
release-owner-publication-authority.json
source-state-after-publication.txt
source-state-after.txt
source-state-before-publication.txt
source-state-before.txt
story-1-16-followup-review.github.json
story-1-16-followup-review.json
CORE_EVIDENCE_FILES
if test "$(jq -er '.mode' "$A_EVIDENCE_ROOT/ad11-preflight.json")" = 'durable-replacement'; then
  printf '%s\n' ad11-replacement-authority.github.json ad11-replacement-authority.json \
    >> "$A_EVIDENCE_MANIFEST"
fi
LC_ALL=C sort -u -o "$A_EVIDENCE_MANIFEST" "$A_EVIDENCE_MANIFEST"
test "$(wc -l < "$A_EVIDENCE_ROOT/critical-evidence-sha256.txt")" \
  -eq "$(wc -l < "$A_EVIDENCE_MANIFEST")"
diff -u "$A_EVIDENCE_MANIFEST" \
  <(awk '
      NF == 2 && length($1) == 64 && $1 !~ /[^0-9a-f]/ && $2 !~ /\// && $2 !~ /^\.\.?$/ {
        print $2; valid++
      }
      END { if (valid != NR) exit 1 }
    ' "$A_EVIDENCE_ROOT/critical-evidence-sha256.txt" | LC_ALL=C sort -u)
diff -u "$A_EVIDENCE_MANIFEST" \
  "$A_EVIDENCE_ROOT/critical-evidence-expected-files.txt"
while IFS= read -r evidence_file; do
  test -f "$A_EVIDENCE_ROOT/$evidence_file"
  test ! -L "$A_EVIDENCE_ROOT/$evidence_file"
done < "$A_EVIDENCE_MANIFEST"
test "$(sha256sum "$A_EVIDENCE_ROOT/critical-evidence-sha256.txt" | awk '{print $1}')" \
  = "$A_EVIDENCE_MANIFEST_SHA256"
(cd "$A_EVIDENCE_ROOT" && sha256sum --check critical-evidence-sha256.txt)

A_RAW_EVIDENCE_BUNDLE_URL="$(front_value "$A_PACKET" raw_evidence_bundle_url)"
A_RAW_EVIDENCE_BUNDLE_SHA256="$(front_value "$A_PACKET" raw_evidence_bundle_sha256)"
[[ "$A_RAW_EVIDENCE_BUNDLE_URL" =~ ^https://[^[:space:]]+$ ]]
[[ "$A_RAW_EVIDENCE_BUNDLE_SHA256" =~ ^[0-9a-f]{64}$ ]]
jq -e \
  --arg url "$A_RAW_EVIDENCE_BUNDLE_URL" \
  --arg sha256 "$A_RAW_EVIDENCE_BUNDLE_SHA256" \
  '.url == $url and .sha256 == $sha256' \
  "$A_EVIDENCE_ROOT/raw-evidence-bundle.json"
curl --fail --silent --show-error --location "$A_RAW_EVIDENCE_BUNDLE_URL" \
  > "$A_RAW_EVIDENCE_BUNDLE"
test "$(sha256sum "$A_RAW_EVIDENCE_BUNDLE" | awk '{print $1}')" \
  = "$A_RAW_EVIDENCE_BUNDLE_SHA256"
tar -tzf "$A_RAW_EVIDENCE_BUNDLE" > "$A_RAW_EVIDENCE_DIRECTORY/archive-files.txt"
test -z "$(awk '/(^\/|(^|\/)\.\.(\/|$)|\/)/' \
  "$A_RAW_EVIDENCE_DIRECTORY/archive-files.txt")"
test "$(LC_ALL=C sort "$A_RAW_EVIDENCE_DIRECTORY/archive-files.txt" | uniq -d | wc -l)" -eq 0
test -z "$(tar -tvzf "$A_RAW_EVIDENCE_BUNDLE" | awk 'substr($1, 1, 1) != "-"')"
tar -xzf "$A_RAW_EVIDENCE_BUNDLE" -C "$A_RAW_EVIDENCE_DIRECTORY"
test -s "$A_RAW_EVIDENCE_DIRECTORY/raw-evidence-files.txt"
diff -u \
  <((printf '%s\n' raw-evidence-files.txt; \
    cat "$A_RAW_EVIDENCE_DIRECTORY/raw-evidence-files.txt") | LC_ALL=C sort) \
  <(LC_ALL=C sort "$A_RAW_EVIDENCE_DIRECTORY/archive-files.txt")
diff -u \
  "$A_RAW_EVIDENCE_DIRECTORY/raw-evidence-files.txt" \
  <(find "$A_RAW_EVIDENCE_DIRECTORY" -maxdepth 1 -type f \
      ! -name archive-files.txt ! -name raw-evidence-files.txt -printf '%f\n' | LC_ALL=C sort)
cat > "$A_EXPECTED_FILTERED_RESULTS" <<'FILTERED_RESULTS'
async-DaprProjectionActivationOutboxTests
async-DaprProjectionDeliveryRetrySchedulerTests
async-NamedProjectionDispatchCoordinatorTests
async-domain-dispatcher
async-live
batch-DaprReadModelBatchTests
batch-DaprReadModelStoreTests
batch-InMemoryReadModelStoreTests
batch-ReadModelBatchStoreTests
batch-live
crosscut-apphost-source-topology
crosscut-domain-compatibility
crosscut-release-manifest
crosscut-source-topology-provenance
cursor-QueryCursorCodecTests
cursor-QueryCursorScopeTests
cursor-persisted-key-ring
delivery-DaprProjectionActivationOutboxTests
delivery-DaprProjectionDeliveryRetrySchedulerTests
delivery-NamedProjectionDispatchCoordinatorTests
delivery-NamedProjectionDispatchLiveSidecarTests
delivery-ProjectionDeliveryCutoverLiveSidecarTests
delivery-ProjectionDeliveryCutoverTests
delivery-ProjectionDeliveryIdempotencyCoordinatorTests
delivery-ProjectionDeliveryReconcilerTests
erasure-coordinator
erasure-dapr-store
erasure-lifecycle-admission
erasure-lifecycle-transport
erasure-live
lifecycle-CachingProjectionActorTests
lifecycle-EventStoreGatewayClientTests
lifecycle-ProjectionAdapterContractTests
lifecycle-QueriesControllerTests
lifecycle-QueryResponseProvenancePersistenceTests
lifecycle-QueryRouterTests
lifecycle-ReadModelFreshnessTests
lifecycle-RestApiControllerGenerationTests
lifecycle-RestApiGeneratedControllerErrorSemanticsTests
lifecycle-SubmitQueryResponseTests
lifecycle-provenance-e2e
lifecycle-query-routing
lifecycle-sample-runtime
multiprojection-NamedProjectionDispatchCoordinatorTests
multiprojection-NamedProjectionRouteCatalogTests
multiprojection-catalog-publication
multiprojection-domain-dispatcher
multiprojection-live
rebuild-ProjectionLifecycleActorTests
rebuild-ProjectionRebuildCheckpointStoreTests
rebuild-ProjectionRebuildProductionPathTests
rebuild-ProjectionUpdateOrchestratorTests
rebuild-domain-dispatcher
rebuild-live-persisted-equivalence
FILTERED_RESULTS
cat > "$A_EXPECTED_FULL_RESULTS" <<'FULL_RESULTS'
crosscut-admin-abstractions
crosscut-admin-cli
crosscut-admin-mcp
crosscut-admin-server
crosscut-admin-server-host
crosscut-admin-ui
crosscut-admin-ui-e2e
crosscut-apphost
crosscut-client
crosscut-contracts
crosscut-deferred-work-governance
crosscut-domain-service
crosscut-live-sidecar
crosscut-operational-evidence
crosscut-query-routing
crosscut-rest-generator
crosscut-sample
crosscut-sample-quickstart
crosscut-server
crosscut-signalr
crosscut-testing
crosscut-testing-integration
FULL_RESULTS
cat > "$A_EXPECTED_REQUIRED_LOGS" <<'REQUIRED_LOGS'
container-publish.txt
crosscut-admin-ui-e2e.playwright.log
literal-package-inventory.log
package-build.log
package-consumer-validation.log
package-validator.log
solution-build.log
solution-restore.log
REQUIRED_LOGS
(
  awk '{print $0 ".xml"; print $0 ".methods.txt"}' "$A_EXPECTED_FILTERED_RESULTS"
  awk '{print $0 ".xml"}' "$A_EXPECTED_FULL_RESULTS"
) | LC_ALL=C sort > "$A_EXPECTED_RESULT_FILES"
diff -u "$A_EXPECTED_RESULT_FILES" \
  <(awk '/\.(xml|methods\.txt)$/' \
      "$A_RAW_EVIDENCE_DIRECTORY/raw-evidence-files.txt" | LC_ALL=C sort)
while IFS= read -r required_log; do
  grep -Fxq "$required_log" "$A_RAW_EVIDENCE_DIRECTORY/raw-evidence-files.txt"
  test -s "$A_RAW_EVIDENCE_DIRECTORY/$required_log"
done < "$A_EXPECTED_REQUIRED_LOGS"
while IFS= read -r result_name; do
  test -s "$A_RAW_EVIDENCE_DIRECTORY/$result_name.methods.txt"
done < "$A_EXPECTED_FILTERED_RESULTS"
while IFS= read -r result_file; do
  case "$result_file" in
    *.xml)
      python3 - "$A_RAW_EVIDENCE_DIRECTORY/$result_file" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
total = int(root.attrib.get("total", "0"))
passed = int(root.attrib.get("passed", "0"))
failed = int(root.attrib.get("failed", "0"))
errors = int(root.attrib.get("errors", "0"))
if total <= 0 or passed <= 0 or failed != 0 or errors != 0:
    raise SystemExit(1)
PY
      ;;
  esac
done < "$A_RAW_EVIDENCE_DIRECTORY/raw-evidence-files.txt"

A_LATEST_GATE_COMPLETED_AT="$(cat "$A_EVIDENCE_ROOT/latest-successful-gate-completed-at.txt")"
[[ "$A_LATEST_GATE_COMPLETED_AT" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$ ]]
test "$(cat "$A_EVIDENCE_ROOT/candidate-source-sha.txt")" = "$A_TESTED_RUNTIME_SHA"
test "$(cat "$A_EVIDENCE_ROOT/ad11-preflight.sha256")" \
  = "$(sha256sum "$A_EVIDENCE_ROOT/ad11-preflight.json" | awk '{print $1}')  ad11-preflight.json"
A_AD11_MODE="$(jq -er '.mode' "$A_EVIDENCE_ROOT/ad11-preflight.json")"
if test "$A_AD11_MODE" = 'exact-baseline'; then
  jq -e \
    --arg repository 'https://github.com/Hexalith/Hexalith.EventStore.git' \
    --arg source_sha "$A_TESTED_RUNTIME_SHA" \
    --arg latest_gate "$A_LATEST_GATE_COMPLETED_AT" '
      .mode == "exact-baseline" and
      .repository == $repository and
      .source_sha == $source_sha and
      .sdk_version == "10.0.302" and
      .sdk_roll_forward == "latestPatch" and
      .installed_sdk_version == "10.0.302" and
      .aspnet_version == "10.0.10" and
      .installed_aspnet_version == "10.0.10" and
      .runtime_version == "10.0.10" and
      ((.checked_at | fromdateiso8601) <= ($latest_gate | fromdateiso8601))
    ' "$A_EVIDENCE_ROOT/ad11-preflight.json" >/dev/null
elif test "$A_AD11_MODE" = 'durable-replacement'; then
  A_AD11_REPLACEMENT="$A_EVIDENCE_ROOT/ad11-replacement-authority.json"
  A_AD11_REPLACEMENT_METADATA="$A_EVIDENCE_ROOT/ad11-replacement-authority.github.json"
  verify_committed_github_role_record \
    "$A_AD11_REPLACEMENT" "$A_AD11_REPLACEMENT_METADATA" architecture_owner
  jq -e \
    --arg repository 'https://github.com/Hexalith/Hexalith.EventStore.git' \
    --arg source_sha "$A_TESTED_RUNTIME_SHA" \
    --arg authority_file 'ad11-replacement-authority.json' \
    --arg authority_sha256 "$(sha256sum "$A_AD11_REPLACEMENT" | awk '{print $1}')" \
    --arg metadata_file 'ad11-replacement-authority.github.json' \
    --arg metadata_sha256 "$(sha256sum "$A_AD11_REPLACEMENT_METADATA" | awk '{print $1}')" \
    --arg latest_gate "$A_LATEST_GATE_COMPLETED_AT" '
      def parts: split(".") | map(tonumber);
      .mode == "durable-replacement" and
      .repository == $repository and
      .source_sha == $source_sha and
      .sdk_roll_forward == "latestPatch" and
      .sdk_version == .installed_sdk_version and
      .aspnet_version == .installed_aspnet_version and
      .aspnet_version == .runtime_version and
      ((.sdk_version | parts) >= ("10.0.302" | parts)) and
      ((.aspnet_version | parts) >= ("10.0.10" | parts)) and
      (((.sdk_version | parts) > ("10.0.302" | parts)) or
        ((.aspnet_version | parts) > ("10.0.10" | parts))) and
      .authority_file == $authority_file and
      .authority_sha256 == $authority_sha256 and
      .authority_github_metadata_file == $metadata_file and
      .authority_github_metadata_sha256 == $metadata_sha256 and
      ((.checked_at | fromdateiso8601) <= ($latest_gate | fromdateiso8601))
    ' "$A_EVIDENCE_ROOT/ad11-preflight.json" >/dev/null
  A_AD11_APPROVER="$(jq -er '.login' "$A_AD11_REPLACEMENT_METADATA")"
  A_AD11_SOURCE="$(jq -er '.html_url' "$A_AD11_REPLACEMENT_METADATA")"
  jq -e \
    --arg repository 'https://github.com/Hexalith/Hexalith.EventStore.git' \
    --arg source_sha "$A_TESTED_RUNTIME_SHA" \
    --arg owner "$A_AD11_APPROVER" \
    --arg durable_source "$A_AD11_SOURCE" \
    --arg sdk_version "$(jq -er '.sdk_version' "$A_EVIDENCE_ROOT/ad11-preflight.json")" \
    --arg aspnet_version "$(jq -er '.aspnet_version' "$A_EVIDENCE_ROOT/ad11-preflight.json")" \
    --arg checked_at "$(jq -er '.checked_at' "$A_EVIDENCE_ROOT/ad11-preflight.json")" \
    --arg latest_gate "$A_LATEST_GATE_COMPLETED_AT" '
      def nonblank: type == "string" and test("\\S");
      .action == "ad11-baseline-replacement" and
      .owner == $owner and .durable_source == $durable_source and
      (.rationale | nonblank) and (.expires_at | nonblank) and
      .scope.repository == $repository and .scope.source_sha == $source_sha and
      .scope.sdk_version == $sdk_version and .scope.sdk_roll_forward == "latestPatch" and
      .scope.aspnet_version == $aspnet_version and .scope.runtime_version == $aspnet_version and
      ((.approved_at | fromdateiso8601) <= ($checked_at | fromdateiso8601)) and
      ((.expires_at | fromdateiso8601) > ($latest_gate | fromdateiso8601))
    ' "$A_AD11_REPLACEMENT" >/dev/null
else
  printf 'unsupported AD-11 mode: %s\n' "$A_AD11_MODE" >&2
  exit 1
fi

A_RUNTIME_GITMODULES="$(mktemp)"
git show "$A_TESTED_RUNTIME_COMMIT:.gitmodules" > "$A_RUNTIME_GITMODULES"
while read -r _ submodule_path; do
  submodule_sha="$(git ls-tree "$A_TESTED_RUNTIME_COMMIT" -- "$submodule_path" |
    awk '$1 == "160000" {print $3}')"
  [[ "$submodule_sha" =~ ^[0-9a-f]{40}$ ]]
  printf 'submodule=%s head=%s\n' "$submodule_path" "$submodule_sha"
done < <(git config -f "$A_RUNTIME_GITMODULES" --get-regexp '^submodule\..*\.path$' |
  LC_ALL=C sort -k2) > "$A_EXPECTED_SOURCE_STATE"
validate_source_state() {
  local source_state="$1"
  python3 - "$source_state" "$A_EXPECTED_SOURCE_STATE" "$A_TESTED_RUNTIME_SHA" <<'PY'
import re
import sys

source_path, expected_path, runtime = sys.argv[1:]
lines = open(source_path, encoding="utf-8").read().splitlines()
expected = open(expected_path, encoding="utf-8").read().splitlines()
if lines[:3] != [f"candidate={runtime}", "root-status:", "root-ignored:"]:
    raise SystemExit("invalid source-state identity or nonempty root status")
try:
    root_submodules = lines.index("root-submodules:", 3)
except ValueError as error:
    raise SystemExit("missing root-submodules marker") from error

def allowed_ignored(line):
    return bool(re.fullmatch(r"!! (?:.+/)?(?:bin|obj)/(?:.*)?", line))

if not all(allowed_ignored(line) for line in lines[3:root_submodules]):
    raise SystemExit("unexpected ignored root input")
cursor = root_submodules + 1
for record in expected:
    path, sha = re.fullmatch(r"submodule=(\S+) head=([0-9a-f]{40})", record).groups()
    status = re.compile(rf" {sha} {re.escape(path)}(?: \([^)]*\))?")
    if cursor >= len(lines) or not status.fullmatch(lines[cursor]):
        raise SystemExit(f"missing root gitlink status for {path}")
    cursor += 1
for record in expected:
    path, _ = re.fullmatch(r"submodule=(\S+) head=([0-9a-f]{40})", record).groups()
    if cursor >= len(lines) or lines[cursor] != record:
        raise SystemExit(f"missing submodule identity for {path}")
    cursor += 1
    if cursor >= len(lines) or lines[cursor] != f"submodule-status={path}:":
        raise SystemExit(f"missing clean-status marker for {path}")
    cursor += 1
    if cursor >= len(lines) or lines[cursor] != f"submodule-ignored={path}:":
        raise SystemExit(f"nonempty regular status for {path}")
    cursor += 1
    while cursor < len(lines) and lines[cursor].startswith("!! "):
        if not allowed_ignored(lines[cursor]):
            raise SystemExit(f"unexpected ignored submodule input for {path}")
        cursor += 1
if cursor != len(lines):
    raise SystemExit("unexpected source-state suffix")
PY
}
for source_state_name in \
    source-state-before.txt source-state-after.txt \
    source-state-before-publication.txt source-state-after-publication.txt; do
  validate_source_state "$A_EVIDENCE_ROOT/$source_state_name"
done

test "$(front_value "$A_FOLLOWUP_SPEC" followup_review_recommended)" = 'false'
A_STORY_REVIEW_RECORD="$A_EVIDENCE_ROOT/story-1-16-followup-review.json"
A_STORY_REVIEW_METADATA="$A_EVIDENCE_ROOT/story-1-16-followup-review.github.json"
verify_committed_github_role_record \
  "$A_STORY_REVIEW_RECORD" "$A_STORY_REVIEW_METADATA" story_1_16_reviewer
A_STORY_REVIEWER_LOGIN="$(jq -er '.login' "$A_STORY_REVIEW_METADATA")"
A_STORY_REVIEW_API_URL="$(jq -er '.api_url' "$A_STORY_REVIEW_METADATA")"
A_STORY_REVIEW_SOURCE="$(jq -er '.html_url' "$A_STORY_REVIEW_METADATA")"
A_STORY_REVIEWED_AT="$(jq -er '.reviewed_at' "$A_STORY_REVIEW_RECORD")"
A_COMMIT_EPOCH="$(git show -s --format=%ct "$EVIDENCE_COMMIT_A")"
jq -e \
  --argjson commit_epoch "$A_COMMIT_EPOCH" \
  --arg owner "$A_STORY_REVIEWER_LOGIN" \
  --arg durable_source "$A_STORY_REVIEW_SOURCE" \
  --arg repository 'https://github.com/Hexalith/Hexalith.EventStore.git' \
  --arg source_sha "$A_TESTED_RUNTIME_SHA" \
  --arg latest_gate "$A_LATEST_GATE_COMPLETED_AT" '
    def nonblank: type == "string" and test("\\S");
    .action == "story-1-16-followup-review" and
    .owner == $owner and .durable_source == $durable_source and
    .disposition == "approved" and
    .scope.repository == $repository and
    .scope.reviewed_runtime_sha == $source_sha and
    (.scope.summary | nonblank) and (.scope.verification | nonblank) and
    (.scope.residual_limitations | nonblank) and
    ((.reviewed_at | fromdateiso8601) >= ($latest_gate | fromdateiso8601)) and
    ((.reviewed_at | fromdateiso8601) <= $commit_epoch)
  ' "$A_STORY_REVIEW_RECORD" >/dev/null
A_STORY_REVIEW_DATE="${A_STORY_REVIEWED_AT%%T*}"
python3 - "$A_FOLLOWUP_SPEC" "$A_STORY_REVIEW_DATE" <<'PY'
import datetime as dt
import re
import sys

text = open(sys.argv[1], encoding="utf-8").read()
expected = sys.argv[2]
dt.date.fromisoformat(expected)
dates = re.findall(r"^### (\d{4}-\d{2}-\d{2}) — Follow-up review disposition$", text, re.M)
if dates != [expected]:
    raise SystemExit(1)
PY
awk \
  -v tested_runtime_sha="$A_TESTED_RUNTIME_SHA" \
  -v reviewer_login="$A_STORY_REVIEWER_LOGIN" \
  -v durable_source_api="$A_STORY_REVIEW_API_URL" \
  -v durable_source="$A_STORY_REVIEW_SOURCE" '
  function field_value(prefix, value) {
    value = substr($0, length(prefix) + 1)
    gsub(/^`|`$/, "", value)
    return value
  }
  /^### [0-9]{4}-[0-9]{2}-[0-9]{2} — Follow-up review disposition$/ {
    sections++; in_disposition = 1; next
  }
  in_disposition && /^### / { in_disposition = 0 }
  in_disposition && index($0, "- reviewed_runtime_sha: ") == 1 {
    runtime++; runtime_ok += (field_value("- reviewed_runtime_sha: ") == tested_runtime_sha)
  }
  in_disposition && index($0, "- reviewer: ") == 1 {
    value = field_value("- reviewer: "); reviewer++; reviewer_ok += (value != "" && value != "not assigned")
  }
  in_disposition && index($0, "- reviewer_login: ") == 1 {
    value = field_value("- reviewer_login: "); login++; login_ok += (value == reviewer_login)
  }
  in_disposition && index($0, "- durable_source_api: ") == 1 {
    value = field_value("- durable_source_api: "); api++; api_ok += (value == durable_source_api)
  }
  in_disposition && index($0, "- durable_source: ") == 1 {
    value = field_value("- durable_source: "); source++
    source_ok += (value == durable_source)
  }
  in_disposition && index($0, "- scope: ") == 1 {
    scope++; scope_ok += (field_value("- scope: ") != "")
  }
  in_disposition && index($0, "- findings_and_resolutions: ") == 1 {
    findings++; findings_ok += (field_value("- findings_and_resolutions: ") != "")
  }
  in_disposition && index($0, "- verification: ") == 1 {
    verification++; verification_ok += (field_value("- verification: ") != "")
  }
  in_disposition && index($0, "- residual_limitations: ") == 1 {
    residuals++; residuals_ok += (field_value("- residual_limitations: ") != "")
  }
  in_disposition && index($0, "- disposition: ") == 1 {
    disposition++; disposition_ok += (field_value("- disposition: ") == "approved")
  }
  END {
    exit !(sections == 1 && runtime == 1 && runtime_ok == 1 &&
      reviewer == 1 && reviewer_ok == 1 && login == 1 && login_ok == 1 &&
      api == 1 && api_ok == 1 && source == 1 && source_ok == 1 &&
      scope == 1 && scope_ok == 1 && findings == 1 && findings_ok == 1 &&
      verification == 1 && verification_ok == 1 && residuals == 1 &&
      residuals_ok == 1 && disposition == 1 && disposition_ok == 1)
  }
' "$A_FOLLOWUP_SPEC"

awk '
  function finish_section() {
    if (!relevant) return
    sections++
    if (status_count != 1 || status_value != "implementation-complete/evidence-confirmed") {
      invalid++
    }
  }
  /^## Deferred from:/ {
    finish_section()
    relevant = ($0 ~ /Story 1\.20/ || $0 ~ /1-20-owner-approved-parity-closure/)
    status_count = 0
    status_value = ""
    next
  }
  relevant && /^  status: / {
    status_count++
    status_value = substr($0, length("  status: ") + 1)
  }
  END {
    finish_section()
    exit !(sections > 0 && invalid == 0)
  }
' "$A_DEFERRED_WORK"

A_PACKAGE_VERSION="$(front_value "$A_PACKET" approved_package_version)"
test -n "$A_PACKAGE_VERSION"
test "$A_PACKAGE_VERSION" != 'null'
A_PACKAGE_HASH_MANIFEST_SHA256="$(front_value "$A_PACKET" approved_package_hash_manifest_sha256)"
[[ "$A_PACKAGE_HASH_MANIFEST_SHA256" =~ ^[0-9a-f]{64}$ ]]
test "$(sha256sum "$A_EVIDENCE_ROOT/nuget-sha256.txt" | awk '{print $1}')" \
  = "$A_PACKAGE_HASH_MANIFEST_SHA256"
test "$(wc -l < "$A_EVIDENCE_ROOT/nuget-sha256.txt")" -eq 14
awk -v version="$A_PACKAGE_VERSION" '
  NF == 2 && length($1) == 64 && $1 !~ /[^0-9a-f]/ &&
    substr($2, length($2) - length(version) - 6) == "." version ".nupkg" { valid++ }
  END { exit !(NR == 14 && valid == 14) }
' "$A_EVIDENCE_ROOT/nuget-sha256.txt"
jq -e '
  .packages | type == "array" and length == 14 and
  (all(.[]; (.id | type == "string" and test("^[A-Za-z0-9.-]+$")))) and
  (map(.id) | unique | length == 14)
' "$A_RELEASE_PACKAGES" >/dev/null
cat > "$A_EXPECTED_PACKAGE_IDS" <<'PACKAGE_IDS'
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
diff -u \
  "$A_EXPECTED_PACKAGE_IDS" \
  <(jq -er '.packages[].id' "$A_RELEASE_PACKAGES" | LC_ALL=C sort)
awk -v version="$A_PACKAGE_VERSION" '{ print $0 "." version ".nupkg" }' \
  "$A_EXPECTED_PACKAGE_IDS" > "$A_EXPECTED_PACKAGE_FILES"
diff -u \
  "$A_EXPECTED_PACKAGE_FILES" \
  <(awk '{ print $2 }' "$A_EVIDENCE_ROOT/nuget-sha256.txt" | LC_ALL=C sort)

A_CONTAINER_REPOSITORY="$(front_value "$A_PACKET" approved_container_repository)"
A_CONTAINER_DIGEST="$(front_value "$A_PACKET" approved_container_digest)"
test "$A_CONTAINER_REPOSITORY" = 'registry.hexalith.com/eventstore'
[[ "$A_CONTAINER_DIGEST" =~ ^sha256:[0-9a-f]{64}$ ]]
A_PUBLICATION_AUTHORITY="$A_EVIDENCE_ROOT/release-owner-publication-authority.json"
A_PUBLICATION_AUTHORITY_METADATA="$A_EVIDENCE_ROOT/release-owner-publication-authority.github.json"
A_PUBLICATION_CHECKED_AT_FILE="$A_EVIDENCE_ROOT/release-owner-publication-authority.checked-at.txt"
A_CAPTURE_TARGETS="$A_EVIDENCE_ROOT/capture-generated-image-index.targets"
A_GENERATED_IMAGE_INDEX="$A_EVIDENCE_ROOT/generated-image-index.json"
A_GENERATED_IMAGE_INDEX_DIGEST="$A_EVIDENCE_ROOT/generated-image-index.digest.txt"
A_CONTAINER_MANIFEST="$A_EVIDENCE_ROOT/container-manifest.json"
A_CONTAINER_PLATFORMS="$A_EVIDENCE_ROOT/container-platforms.txt"
for required_evidence in \
    "$A_PUBLICATION_AUTHORITY" \
    "$A_PUBLICATION_AUTHORITY_METADATA" \
    "$A_PUBLICATION_CHECKED_AT_FILE" \
    "$A_CAPTURE_TARGETS" \
    "$A_GENERATED_IMAGE_INDEX" \
    "$A_GENERATED_IMAGE_INDEX_DIGEST" \
    "$A_CONTAINER_MANIFEST" \
    "$A_CONTAINER_PLATFORMS"; do
  test -s "$required_evidence"
done
verify_committed_github_role_record \
  "$A_PUBLICATION_AUTHORITY" "$A_PUBLICATION_AUTHORITY_METADATA" release_owner
A_PUBLICATION_CHECKED_AT="$(cat "$A_PUBLICATION_CHECKED_AT_FILE")"
[[ "$A_PUBLICATION_CHECKED_AT" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$ ]]
test "$(cat "$A_GENERATED_IMAGE_INDEX_DIGEST")" = "$A_CONTAINER_DIGEST"
test "sha256:$(sha256sum "$A_GENERATED_IMAGE_INDEX" | awk '{print $1}')" \
  = "$A_CONTAINER_DIGEST"
test "sha256:$(sha256sum "$A_CONTAINER_MANIFEST" | awk '{print $1}')" \
  = "$A_CONTAINER_DIGEST"
jq -e '.manifests | type == "array" and length == 2' \
  "$A_CONTAINER_MANIFEST" >/dev/null
jq -r '.manifests[].platform |
  "\(.os)/\(.architecture)\(if .variant then "/" + .variant else "" end)"' \
  "$A_CONTAINER_MANIFEST" | LC_ALL=C sort -u \
  | cmp --silent <(printf '%s\n' linux/amd64 linux/arm64) -
cmp --silent \
  <(printf '%s\n' linux/amd64 linux/arm64) \
  "$A_CONTAINER_PLATFORMS"
jq -e \
  --arg checked_at "$A_PUBLICATION_CHECKED_AT" \
  --arg repository "$A_CONTAINER_REPOSITORY" \
  --arg tag "quarantine-proof-$A_TESTED_RUNTIME_SHA" \
  --arg source_sha "$A_TESTED_RUNTIME_SHA" \
  --arg owner "$(jq -er '.login' "$A_PUBLICATION_AUTHORITY_METADATA")" \
  --arg durable_source "$(jq -er '.html_url' "$A_PUBLICATION_AUTHORITY_METADATA")" '
    def nonblank: type == "string" and test("\\S");
    .action == "container-publication" and
    .owner == $owner and
    (.authorized_at | nonblank) and
    .durable_source == $durable_source and
    (.rationale | nonblank) and
    (.expires_at | nonblank) and
    .scope.repository == $repository and .scope.tag == $tag and
    .scope.source_sha == $source_sha and
    ((.authorized_at | fromdateiso8601) <= ($checked_at | fromdateiso8601)) and
    ((.expires_at | fromdateiso8601) > ($checked_at | fromdateiso8601))
  ' "$A_PUBLICATION_AUTHORITY"
jq -e \
  --arg repository "$A_CONTAINER_REPOSITORY" \
  --arg digest "$A_CONTAINER_DIGEST" \
  --arg source_sha "$A_TESTED_RUNTIME_SHA" \
  --arg tag "quarantine-proof-$A_TESTED_RUNTIME_SHA" \
  --arg authority_sha256 "$(sha256sum "$A_PUBLICATION_AUTHORITY" | awk '{print $1}')" \
  --arg authority_metadata_sha256 "$(sha256sum "$A_PUBLICATION_AUTHORITY_METADATA" | awk '{print $1}')" \
  --arg authority_checked_at "$A_PUBLICATION_CHECKED_AT" \
  --arg authority_checked_at_sha256 "$(
    sha256sum "$A_PUBLICATION_CHECKED_AT_FILE" | awk '{print $1}'
  )" \
  --arg capture_targets_sha256 "$(sha256sum "$A_CAPTURE_TARGETS" | awk '{print $1}')" '
    .repository == $repository and .digest == $digest and .source_sha == $source_sha and
    .tag == $tag and
    .manifest_sha256 == ($digest | sub("^sha256:"; "")) and
    (.platforms | length == 2 and sort == ["linux/amd64", "linux/arm64"]) and
    .sdk_generated_index.file == "generated-image-index.json" and
    .sdk_generated_index.digest == $digest and
    .sdk_generated_index.capture_targets_file == "capture-generated-image-index.targets" and
    .sdk_generated_index.capture_targets_sha256 == $capture_targets_sha256 and
    .publish_properties.container_registry == "registry.hexalith.com" and
    .publish_properties.container_repository == "eventstore" and
    .publish_properties.container_image_tag == $tag and
    .publish_properties.container_runtime_identifiers == "linux-x64;linux-arm64" and
    .publication_authority.file == "release-owner-publication-authority.json" and
    .publication_authority.sha256 == $authority_sha256 and
    .publication_authority.github_metadata_file ==
      "release-owner-publication-authority.github.json" and
    .publication_authority.github_metadata_sha256 == $authority_metadata_sha256 and
    .publication_authority.checked_at == $authority_checked_at and
    .publication_authority.checked_at_file ==
      "release-owner-publication-authority.checked-at.txt" and
    .publication_authority.checked_at_sha256 == $authority_checked_at_sha256
  ' "$A_EVIDENCE_ROOT/container-provenance.json"

A_REGISTRY_MANIFEST="$(mktemp)"
docker buildx imagetools inspect \
  "$A_CONTAINER_REPOSITORY@$A_CONTAINER_DIGEST" --raw > "$A_REGISTRY_MANIFEST"
test "sha256:$(sha256sum "$A_REGISTRY_MANIFEST" | awk '{print $1}')" \
  = "$A_CONTAINER_DIGEST"
cmp --silent "$A_CONTAINER_MANIFEST" "$A_REGISTRY_MANIFEST"
while IFS= read -r child_digest; do
  [[ "$child_digest" =~ ^sha256:[0-9a-f]{64}$ ]]
  child_manifest="$(mktemp)"
  docker buildx imagetools inspect \
    "$A_CONTAINER_REPOSITORY@$child_digest" --raw > "$child_manifest"
  test "sha256:$(sha256sum "$child_manifest" | awk '{print $1}')" = "$child_digest"
  jq -e 'type == "object" and (.schemaVersion == 2)' "$child_manifest" >/dev/null
done < <(jq -er '.manifests[].digest' "$A_CONTAINER_MANIFEST")

A_COMMIT_EPOCH="$(git show -s --format=%ct "$EVIDENCE_COMMIT_A")"
A_REPOSITORY_URL='https://github.com/Hexalith/Hexalith.EventStore.git'
A_APPROVAL_SUBJECT_SHA256="$(front_value "$A_PACKET" approval_subject_sha256)"
[[ "$A_APPROVAL_SUBJECT_SHA256" =~ ^[0-9a-f]{64}$ ]]
cp -- "$A_EVIDENCE_ROOT/approval-subject.json" "$A_APPROVAL_SUBJECT"
test "$(sha256sum "$A_APPROVAL_SUBJECT" | awk '{print $1}')" \
  = "$A_APPROVAL_SUBJECT_SHA256"
jq -e \
  --arg repository "$A_REPOSITORY_URL" \
  --arg source_sha "$A_TESTED_RUNTIME_SHA" \
  --arg evidence_manifest_sha256 "$A_EVIDENCE_MANIFEST_SHA256" \
  --arg raw_sha256 "$A_RAW_EVIDENCE_BUNDLE_SHA256" \
  --arg package_version "$A_PACKAGE_VERSION" \
  --arg package_manifest_sha256 "$A_PACKAGE_HASH_MANIFEST_SHA256" \
  --arg container_repository "$A_CONTAINER_REPOSITORY" \
  --arg container_digest "$A_CONTAINER_DIGEST" '
    .schema == "hexalith.eventstore.story-1-20-approval-subject/v1" and
    .repository == $repository and .tested_runtime_sha == $source_sha and
    .evidence_manifest_sha256 == $evidence_manifest_sha256 and
    .raw_evidence_bundle_sha256 == $raw_sha256 and
    .package_version == $package_version and
    .package_hash_manifest_sha256 == $package_manifest_sha256 and
    .container_repository == $container_repository and
    .container_digest == $container_digest and
    (.capabilities | type == "array" and length == 9 and
      (map(.id) | unique | length == 9) and
      all(.[]; .classification == "available" and
        (.limitations | type == "array" and length > 0 and
          length == (unique | length))))
  ' "$A_APPROVAL_SUBJECT" >/dev/null
cat > "$A_EXPECTED_CAPABILITY_IDS" <<'CAPABILITY_IDS'
asynchronous-projection-persistence
coordinated-batching
cross-cutting-release-compatibility
cursor-compatibility
delivery-order-and-idempotency
lifecycle-and-route-provenance
multiple-projections-per-domain
paged-rebuild-equivalence
read-model-erasure
CAPABILITY_IDS
cat > "$A_EXPECTED_LIMITATION_IDS" <<'LIMITATION_IDS'
async-dead-letter-terminal-cleanup
async-hand-written-state-writes
async-null-metadata
async-stable-work-identity
batch-corrupt-or-abandoned-envelope
batch-cross-profile-boundary
batch-etag-behavior
batch-store-transaction-capability
batch-terminal-receipt-retention
cursor-key-ring-loss-or-replacement
delivery-legacy-project-route-boundary
delivery-mixed-writer-cutover-boundary
erasure-admission-write-fence-races
erasure-authenticated-stitched-e2e
erasure-caller-slot-completeness
erasure-global-administrator-boundary
lifecycle-etag-evidence-boundary
lifecycle-legacy-metadata-abi
lifecycle-operational-value-abi
lifecycle-unknown-compatibility
multiprojection-bounded-result-contract
multiprojection-exact-route-identity
multiprojection-legacy-route-boundary
multiprojection-versioned-route-boundary
rebuild-cancellation-resume-semantics
rebuild-complete-prefix-resource-bounds
rebuild-coordinated-promotion-boundary
rebuild-deferred-review-items
release-container-registry-and-platform-provenance
release-durable-evidence-retention
release-owner-authority-and-migration
release-package-inventory-and-byte-identity
LIMITATION_IDS
diff -u "$A_EXPECTED_CAPABILITY_IDS" \
  <(jq -er '.capabilities[].id' "$A_APPROVAL_SUBJECT" | LC_ALL=C sort)
diff -u "$A_EXPECTED_LIMITATION_IDS" \
  <(jq -er '.capabilities[].limitations[]' "$A_APPROVAL_SUBJECT" | LC_ALL=C sort)
python3 - "$A_PACKET" "$A_APPROVAL_SUBJECT" "$A_TESTED_RUNTIME_COMMIT" <<'PY'
import json
import re
import subprocess
import sys

packet_path, subject_path, runtime = sys.argv[1:]
packet = open(packet_path, encoding="utf-8").read()
subject = json.load(open(subject_path, encoding="utf-8"))
start = packet.index("## Parity Capability Matrix")
end = packet.index("## Owner Review", start)
matrix = packet[start:end]
if re.search(r"`still blocked`|\bFAILED\b|\bNOT RUN\b|\bowner disposition: pending\b", matrix):
    raise SystemExit("matrix contains stale or non-authorizing state")
matches = list(re.finditer(r"^- capability-id: `([^`]+)`$", matrix, re.M))
if len(matches) != 9:
    raise SystemExit("matrix must contain exactly nine capabilities")
subject_map = {item["id"]: item for item in subject["capabilities"]}
if set(subject_map) != {match.group(1) for match in matches}:
    raise SystemExit("matrix and approval subject capability IDs differ")
for index, match in enumerate(matches):
    capability_id = match.group(1)
    section_end = matches[index + 1].start() if index + 1 < len(matches) else len(matrix)
    section = matrix[match.start():section_end]
    if len(re.findall(r"^- classification: `available`$", section, re.M)) != 1:
        raise SystemExit(f"{capability_id}: classification is not exactly available")
    source_marker = re.search(r"^- (?:production )?source paths:$", section, re.M)
    test_marker = re.search(r"^- (?:focused compatibility and packaging )?test paths:$", section, re.M)
    required_marker = re.search(r"^- required exact .+:$", section, re.M)
    rollback_marker = re.search(r"^- rollback: \S", section, re.M)
    if not all((source_marker, test_marker, required_marker, rollback_marker)):
        raise SystemExit(f"{capability_id}: incomplete source/test/command/rollback fields")
    def paths_after(marker):
        tail = section[marker.end():]
        next_bullet = re.search(r"^-[ ]", tail, re.M)
        body = tail[:next_bullet.start()] if next_bullet else tail
        return re.findall(r"^  - `([^`]+)`$", body, re.M)
    source_paths = paths_after(source_marker)
    test_paths = paths_after(test_marker)
    if not source_paths or not test_paths:
        raise SystemExit(f"{capability_id}: empty source or test path list")
    for path in source_paths + test_paths:
        subprocess.run(["git", "cat-file", "-e", f"{runtime}:{path}"], check=True)
    limitation = re.search(
        r"^- limitation-ids: (.+(?:\n  `[^\n]+)*)$", section, re.M)
    if not limitation:
        raise SystemExit(f"{capability_id}: missing limitation IDs")
    limitation_ids = re.findall(r"`([^`]+)`", limitation.group(1))
    if limitation_ids != subject_map[capability_id]["limitations"]:
        raise SystemExit(f"{capability_id}: limitation IDs differ from approval subject")
    if capability_id == "cross-cutting-release-compatibility":
        if "| PASS |" not in section:
            raise SystemExit("cross-cutting row lacks completed gate results")
    elif not re.search(r"^- closure result and persisted read-back: \S", section, re.M):
        raise SystemExit(f"{capability_id}: missing closure result")
PY
validate_committed_owner_record() {
  local record="$1"
  local metadata="$2"
  local expected_role="$3"
  local action="$4"
  local decision="$5"
  local approval_prefix="$6"
  local approval_url
  local approval_sha256
  local approved_login
  approval_url="$(front_value "$A_PACKET" "${approval_prefix}_url")"
  approval_sha256="$(front_value "$A_PACKET" "${approval_prefix}_sha256")"
  [[ "$approval_sha256" =~ ^[0-9a-f]{64}$ ]]
  test "$(sha256sum "$record" | awk '{print $1}')" = "$approval_sha256"
  verify_committed_github_role_record "$record" "$metadata" "$expected_role"
  test "$approval_url" = "$(jq -er '.html_url' "$metadata")"
  approved_login="$(jq -er '.login' "$metadata")"
  jq -e \
    --argjson commit_epoch "$A_COMMIT_EPOCH" \
    --arg action "$action" \
    --arg decision "$decision" \
    --arg durable_source "$approval_url" \
    --arg repository "$A_REPOSITORY_URL" \
    --arg source_sha "$A_TESTED_RUNTIME_SHA" \
    --arg raw_sha256 "$A_RAW_EVIDENCE_BUNDLE_SHA256" \
    --arg package_version "$A_PACKAGE_VERSION" \
    --arg package_manifest_sha256 "$A_PACKAGE_HASH_MANIFEST_SHA256" \
    --arg container_repository "$A_CONTAINER_REPOSITORY" \
    --arg container_digest "$A_CONTAINER_DIGEST" \
    --arg approved_login "$approved_login" \
    --arg evidence_manifest_sha256 "$A_EVIDENCE_MANIFEST_SHA256" \
    --arg approval_subject_sha256 "$A_APPROVAL_SUBJECT_SHA256" \
    --arg latest_gate_completed_at "$A_LATEST_GATE_COMPLETED_AT" '
      def nonblank: type == "string" and test("\\S");
      type == "object" and .action == $action and
      .owner == $approved_login and (.approved_at | nonblank) and
      .durable_source == $durable_source and
      (.accepted_scope | nonblank) and
      (.limitations | type == "array" and length > 0 and
        length == (map(.id) | unique | length) and
        all(.[]; type == "object" and (.id | nonblank) and
          .disposition == "accepted" and (.rationale | nonblank))) and
      .decision == $decision and
      .scope.repository == $repository and
      .scope.tested_runtime_sha == $source_sha and
      .scope.raw_evidence_bundle_sha256 == $raw_sha256 and
      .scope.package_version == $package_version and
      .scope.package_hash_manifest_sha256 == $package_manifest_sha256 and
      .scope.container_repository == $container_repository and
      .scope.container_digest == $container_digest and
      .scope.evidence_manifest_sha256 == $evidence_manifest_sha256 and
      .scope.approval_subject_sha256 == $approval_subject_sha256 and
      .scope.latest_successful_gate_completed_at == $latest_gate_completed_at and
      ((.approved_at | fromdateiso8601) >= ($latest_gate_completed_at | fromdateiso8601)) and
      ((.approved_at | fromdateiso8601) <= $commit_epoch)
    ' "$record"
  diff -u "$A_EXPECTED_LIMITATION_IDS" \
    <(jq -er '.limitations[].id' "$record" | LC_ALL=C sort)
}

validate_committed_owner_record \
  "$A_EVIDENCE_ROOT/eventstore-owner-proof-approval.json" \
  "$A_EVIDENCE_ROOT/eventstore-owner-proof-approval.github.json" \
  eventstore_owner \
  story-1-20-proof-approval \
  authorize-consumer-migration \
  eventstore_owner_approval
validate_committed_owner_record \
  "$A_EVIDENCE_ROOT/release-owner-final-disposition.json" \
  "$A_EVIDENCE_ROOT/release-owner-final-disposition.github.json" \
  release_owner \
  story-1-20-release-disposition \
  approve-release-identities \
  release_owner_disposition
A_OWNER_REVIEW="$(awk '/^## Owner Review$/{inside=1; next} inside && /^## /{exit} inside{print}' "$A_PACKET")"
A_EVENTSTORE_OWNER_LOGIN="$(jq -er '.login' \
  "$A_EVIDENCE_ROOT/eventstore-owner-proof-approval.github.json")"
A_RELEASE_OWNER_LOGIN="$(jq -er '.login' \
  "$A_EVIDENCE_ROOT/release-owner-final-disposition.github.json")"
grep -Fq "$A_EVENTSTORE_OWNER_LOGIN" <<< "$A_OWNER_REVIEW"
grep -Fq "$A_RELEASE_OWNER_LOGIN" <<< "$A_OWNER_REVIEW"
grep -Fq "$(front_value "$A_PACKET" eventstore_owner_approval_url)" <<< "$A_OWNER_REVIEW"
grep -Fq "$(front_value "$A_PACKET" release_owner_disposition_url)" <<< "$A_OWNER_REVIEW"
grep -Fq 'migration decision: `authorize_consumer_migration: false`' <<< "$A_OWNER_REVIEW"
test -z "$(grep -E 'not assigned|not recorded|accepted scope: none|accepted limitations: none' \
  <<< "$A_OWNER_REVIEW")"
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
test "$(git ls-tree "$EVIDENCE_COMMIT_A" -- "$PACKET" | awk '{print $1}')" \
  = "$(git ls-tree "$POINTER_COMMIT_B" -- "$PACKET" | awk '{print $1}')"
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

mapfile -t C_CHANGED_FILES < <(git diff --name-only "$POINTER_COMMIT_B" "$AUTHORIZATION_COMMIT_C")
diff -u \
  <(printf '%s\n' "$PACKET" "$SPRINT_STATUS" "$STORY" | LC_ALL=C sort) \
  <(printf '%s\n' "${C_CHANGED_FILES[@]}" | LC_ALL=C sort)
for authorization_path in "$PACKET" "$STORY" "$SPRINT_STATUS"; do
  test "$(git ls-tree "$POINTER_COMMIT_B" -- "$authorization_path" | awk '{print $1}')" \
    = "$(git ls-tree "$AUTHORIZATION_COMMIT_C" -- "$authorization_path" | awk '{print $1}')"
done

awk '
  NR == 1 && $0 == "---" { in_front = 1; print; next }
  in_front && $0 == "---" { in_front = 0; print; next }
  in_front && $0 == "final_decision: still blocked" && decision_changed == 0 {
    print "final_decision: available"; decision_changed = 1; next
  }
  in_front && $0 == "authorize_consumer_migration: false" && migration_changed == 0 {
    print "authorize_consumer_migration: true"; migration_changed = 1; next
  }
  !in_front && /^## / { section = $0 }
  section == "## Decision" && $0 == "`still blocked`" && body_decision_changed == 0 {
    print "`available`"; body_decision_changed = 1; next
  }
  section == "## Owner Review" &&
      $0 == "- migration decision: `authorize_consumer_migration: false`" &&
      owner_migration_changed == 0 {
    print "- migration decision: `authorize_consumer_migration: true`"
    owner_migration_changed = 1
    next
  }
  section == "## Final Decision" && $0 == "`still blocked`" && final_decision_changed == 0 {
    print "`available`"; final_decision_changed = 1; next
  }
  { print }
  END {
    if (decision_changed != 1 || migration_changed != 1 || body_decision_changed != 1 ||
        owner_migration_changed != 1 || final_decision_changed != 1) exit 1
  }
' "$B_PACKET" > "$EXPECTED_C_PACKET"
cmp --silent "$EXPECTED_C_PACKET" "$C_PACKET"

awk '
  NR == 1 && $0 == "---" { in_front = 1; print; next }
  in_front && $0 == "---" { in_front = 0; after_front = 1; print; next }
  in_front && $0 == "status: blocked" && front_changed == 0 {
    print "status: done"; front_changed = 1; next
  }
  after_front && $0 == "Status: blocked" && body_changed == 0 {
    print "Status: done"; body_changed = 1; next
  }
  { print }
  END { if (front_changed != 1 || body_changed != 1) exit 1 }
' "$B_STORY" > "$EXPECTED_C_STORY"
cmp --silent "$EXPECTED_C_STORY" "$C_STORY"

B_LAST_UPDATED="$(awk '/^last_updated:/ { print $2; count++ }
  END { if (count != 1) exit 1 }' "$B_SPRINT_STATUS")"
B_LAST_UPDATED_COMMENT="$(awk '/^# last_updated:/ { print $3; count++ }
  END { if (count != 1) exit 1 }' "$B_SPRINT_STATUS")"
C_LAST_UPDATED="$(awk '/^last_updated:/ { print $2; count++ }
  END { if (count != 1) exit 1 }' "$C_SPRINT_STATUS")"
C_LAST_UPDATED_COMMENT="$(awk '/^# last_updated:/ { print $3; count++ }
  END { if (count != 1) exit 1 }' "$C_SPRINT_STATUS")"
[[ "$B_LAST_UPDATED" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]]
[[ "$C_LAST_UPDATED" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]]
test "$B_LAST_UPDATED_COMMENT" = "$B_LAST_UPDATED"
test "$C_LAST_UPDATED_COMMENT" = "$C_LAST_UPDATED"
[[ "$C_LAST_UPDATED" == "$B_LAST_UPDATED" || "$C_LAST_UPDATED" > "$B_LAST_UPDATED" ]]
awk -v last_updated="$C_LAST_UPDATED" '
  $0 == "# Story 1.19 review is complete. Story 1.20 remains in progress while:" {
    print "# Story 1.20 owner-approved parity closure is complete; authorizing commit C"
    print "# verified every pinned artifact, approval, prerequisite, and migration decision."
    blocker_start++; in_blocker = 1; next
  }
  in_blocker && $0 == "# `final_decision: still blocked` cannot transition this story or Epic 1 to done." {
    blocker_end++; in_blocker = 0; next
  }
  in_blocker { next }
  /^# last_updated:/ { print "# last_updated: " last_updated; comment_count++; next }
  /^last_updated:/ { print "last_updated: " last_updated; last_count++; next }
  /^  epic-1:/ { print "  epic-1: done"; epic_count++; next }
  /^  1-20-owner-approved-parity-closure-and-runtime-pin:/ {
    print "  1-20-owner-approved-parity-closure-and-runtime-pin: done"; story_count++; next
  }
  { print }
  END {
    if (in_blocker || blocker_start != 1 || blocker_end != 1 || comment_count != 1 ||
        last_count != 1 || epic_count != 1 || story_count != 1) exit 1
  }
' "$B_SPRINT_STATUS" > "$EXPECTED_C_SPRINT_STATUS"
cmp --silent "$EXPECTED_C_SPRINT_STATUS" "$C_SPRINT_STATUS"

test "$(front_value "$C_PACKET" tested_runtime_sha)" = "$A_TESTED_RUNTIME_SHA"
test "$(front_value "$C_PACKET" documentation_commit_sha)" = "$EVIDENCE_COMMIT_A"
test "$(front_value "$C_PACKET" final_decision)" = 'available'
test "$(front_value "$C_PACKET" authorize_consumer_migration)" = 'true'
test "$(awk '/^## Decision$/{inside=1; next} inside && /^## /{exit} inside && /^`/{print}' \
  "$C_PACKET")" = '`available`'
test "$(awk '/^## Final Decision$/{inside=1; next} inside && /^## /{exit} inside && /^`/{print}' \
  "$C_PACKET")" = '`available`'
grep -Fq -- '- migration decision: `authorize_consumer_migration: true`' "$C_PACKET"
test "$(front_value "$C_STORY" status)" = 'done'
awk '
  /^  epic-1:/ { epic_count++; epic_ok += ($0 == "  epic-1: done") }
  /^  1-20-owner-approved-parity-closure-and-runtime-pin:/ {
    story_count++
    story_ok += ($0 == "  1-20-owner-approved-parity-closure-and-runtime-pin: done")
  }
  END { exit !(epic_count == 1 && epic_ok == 1 && story_count == 1 && story_ok == 1) }
' "$C_SPRINT_STATUS"
```

A missing or non-unique opening field, unequal candidate/runtime identity, unresolved tested
runtime, an incomplete Story 1.16 review, an open Story 1.20 prerequisite, incomplete capability
rows, a nonliteral package inventory, missing/mismatched evidence or approval pins, merge commit
A, any A parent other than that runtime, an A path outside `_bmad-output/`, A not changing the
packet and Story 1.16 disposition, merge commit B/C, wrong parent/pointer, file-mode change,
extra path, stale sprint blocker narrative, regressing tracker date, or any edit beyond the
reconstructed A/B/C contracts exits nonzero. A and B remain non-authorizing. Only verified C
changes the decision, migration, story, and reconciled sprint guards after reusing A's already
validated immutable evidence and approval identities.

## Consumer Handoff Guard

Run the complete A/B/C verifier above in the authorization repository **in the current shell**,
then continue directly with exactly one consumer procedure below. The procedures derive every
EventStore source/package/container pin from the already verified C packet and A evidence tree;
callers supply only the consumer checkout/project or the directory containing the approved
package bytes. Starting a new shell, assigning an artifact pin, or skipping the verifier loses
the verified variables and fails closed.

### Source consumer procedure

```bash
set -euo pipefail
: "${AUTHORIZATION_COMMIT_C:?run the A/B/C verifier in this shell first}"
: "${CONSUMER_REPOSITORY:?set the source consumer repository path}"
test "$(front_value "$C_PACKET" final_decision)" = 'available'
test "$(front_value "$C_PACKET" authorize_consumer_migration)" = 'true'
APPROVED_EVENTSTORE_SHA="$A_TESTED_RUNTIME_SHA"
[[ "$APPROVED_EVENTSTORE_SHA" =~ ^[0-9a-f]{40}$ ]]
EVENTSTORE_SUBMODULE='references/Hexalith.EventStore'
cd "$CONSUMER_REPOSITORY"

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

The caller supplies the directory containing the exact 14 `.nupkg` files and the consumer
project. Version and hash-manifest pins come only from verified A/C. The approved ID inventory
is literal, not merely a count:

```bash
set -euo pipefail
: "${AUTHORIZATION_COMMIT_C:?run the A/B/C verifier in this shell first}"
: "${APPROVED_PACKAGE_DIRECTORY:?set the directory containing approved package bytes}"
: "${CONSUMER_PROJECT:?set the consumer project path}"
test "$(front_value "$C_PACKET" final_decision)" = 'available'
test "$(front_value "$C_PACKET" authorize_consumer_migration)" = 'true'
APPROVED_PACKAGE_VERSION="$A_PACKAGE_VERSION"
APPROVED_PACKAGE_HASHES="$A_EVIDENCE_ROOT/nuget-sha256.txt"
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
SELECTED_EVENTSTORE_IDS="$CONSUMER_GATE/selected-eventstore-package-ids.txt"
jq -er --arg version "$APPROVED_PACKAGE_VERSION" '
  [.libraries | to_entries[] |
    select(.key | startswith("Hexalith.EventStore") or startswith("hexalith.eventstore"))] as $eventstore |
  ($eventstore | length > 0) and
  all($eventstore[];
    .value.type == "package" and
    ((.key | ascii_downcase) | endswith(("/" + $version) | ascii_downcase))) as $valid |
  if $valid then $eventstore[].key | split("/")[0] else error("invalid EventStore assets") end
' "$ASSETS_FILE" | LC_ALL=C sort -u > "$SELECTED_EVENTSTORE_IDS"
test -s "$SELECTED_EVENTSTORE_IDS"
test -z "$(comm -23 "$SELECTED_EVENTSTORE_IDS" "$EXPECTED_IDS")"
while IFS= read -r package_id; do
  lower_id="${package_id,,}"
  lower_version="${APPROVED_PACKAGE_VERSION,,}"
  approved_nupkg="$APPROVED_PACKAGE_DIRECTORY/$package_id.$APPROVED_PACKAGE_VERSION.nupkg"
  restored_nupkg="$CONSUMER_GATE/packages/$lower_id/$lower_version/$lower_id.$lower_version.nupkg"
  test -f "$approved_nupkg"
  test -f "$restored_nupkg"
  cmp --silent "$approved_nupkg" "$restored_nupkg"
done < "$SELECTED_EVENTSTORE_IDS"
dotnet build "$CONSUMER_PROJECT" \
  --configuration Release \
  --no-restore \
  -p:UseHexalithProjectReferences=false
```

The filename/version check proves the full approved inventory is present. The asset-file
check rejects lookalike EventStore IDs, proves every selected EventStore dependency is a
literal approved ID at the exact package version, and byte-compares the restored `.nupkg`
with the already hash-verified approved file. Source ordering or an overlapping NuGet source
mapping therefore cannot substitute different bytes.

### Container consumer procedure

```bash
set -euo pipefail
: "${AUTHORIZATION_COMMIT_C:?run the A/B/C verifier in this shell first}"
test "$(front_value "$C_PACKET" final_decision)" = 'available'
test "$(front_value "$C_PACKET" authorize_consumer_migration)" = 'true'
APPROVED_IMAGE_REPOSITORY="$A_CONTAINER_REPOSITORY"
APPROVED_IMAGE_DIGEST="$A_CONTAINER_DIGEST"
APPROVED_PLATFORM_SET_FILE="$A_EVIDENCE_ROOT/container-platforms.txt"
APPROVED_TESTED_RUNTIME_SHA="$A_TESTED_RUNTIME_SHA"
APPROVED_CONTAINER_PROVENANCE="$A_EVIDENCE_ROOT/container-provenance.json"
[[ "$APPROVED_IMAGE_DIGEST" =~ ^sha256:[0-9a-f]{64}$ ]]
[[ "$APPROVED_TESTED_RUNTIME_SHA" =~ ^[0-9a-f]{40}$ ]]
test -s "$APPROVED_PLATFORM_SET_FILE"
test -s "$APPROVED_CONTAINER_PROVENANCE"
cmp --silent \
  <(printf '%s\n' 'linux/amd64' 'linux/arm64') \
  <(LC_ALL=C sort -u "$APPROVED_PLATFORM_SET_FILE")
jq -e \
  --arg repository "$APPROVED_IMAGE_REPOSITORY" \
  --arg digest "$APPROVED_IMAGE_DIGEST" \
  --arg source_sha "$APPROVED_TESTED_RUNTIME_SHA" '
    .repository == $repository and
    .digest == $digest and
    .source_sha == $source_sha and
    .manifest_sha256 == ($digest | sub("^sha256:"; "")) and
    (.platforms | sort) == ["linux/amd64", "linux/arm64"]
  ' "$APPROVED_CONTAINER_PROVENANCE"

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
The approved platform file is itself constrained to the literal two-platform set, and the
approved provenance must map that exact repository/digest to the approved tested runtime SHA.
Until all three procedures have real approved pins and pass in the consumer repository,
this packet authorizes no consumer repository, package pin, deployment, or rollback change.

### Downstream story routing

These are adoption owners. Registration creates no migration authority and changes no
dependency pointer. Story 2.7 owns the narrowly scoped pre-authorization EventStore
registration/harness correction required to make Story 1.20's live provenance gate valid.
Story 2.12 owns the later Tenants dependency-adoption phase.

| Repository | Adoption owner | Current state | Activation/closure boundary |
| --- | --- | --- | --- |
| FrontComposer | Story 11.24, `Adopt the Owner-Approved EventStore Runtime Identity` | Registered `backlog`; no story file | Remains backlog until this packet is `available` and authorizes migration; then prove exact source/package identities, Pact provider verification, dual-mode builds, and live Aspire command/query/realtime smoke. |
| Memories | Epic 28 / Story 28.1, `Adopt Owner-Approved EventStore Runtime Identity` | Registered `backlog`; no story file | Remains backlog until this packet is `available` and authorizes migration; then prove exact source/package identities while preserving the DAPR ingestion registration chain and live persistence/search/dedup behavior. |
| Tenants prerequisite | EventStore Story 2.7, `Pre-Authorization Registration And Provenance Correction` | `done` | Committed correction `fd8ab24d...` resolves the stale sample registration/source-topology prerequisite without changing dependency identities; Story 1.20 must still rerun its own exact-candidate gate. |
| Tenants adoption | EventStore Story 2.12, `Tenants Runtime Identity Adoption And Package-Mode Validation` | Registered `backlog`; no story file | Remains backlog until this packet is `available` and authorizes migration; then align the source/package graph, including conditional Gateway ownership, and record exact Tenants maintainer evidence. |
| Builds | Existing platform pin ownership | No new story | AD-11 SDK/runtime and central ASP.NET pins are already landed. A later EventStore package-version update is release-owner work only after approved artifacts exist. |

FrontComposer, Memories, and Tenants Story 2.12 implementation story files must not be created
before activation because their trackers interpret a created story file as `ready-for-dev`.
Tenants Story 2.7 is complete as the fail-closed implementation prerequisite. Builds
must not speculate a future EventStore package version.

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
perl -nle 'while (m{(?<![A-Za-z0-9_.-])((?:_bmad-output|src|tests|docs|scripts|tools|references)/[A-Za-z0-9_./-]+\.(?:csproj|cs|md|py|json|props|targets|yml|yaml|config|slnx)|Directory\.Build\.targets|Directory\.Packages\.props|Hexalith\.EventStore\.slnx|global\.json|nuget\.config)}g) { print $1 }' \
  "$PACKET" | sort -u > "$CITED_PATHS"
MISSING_PATHS="$(mktemp)"
while IFS= read -r path; do
  test -e "$path" || printf '%s\n' "$path"
done < "$CITED_PATHS" > "$MISSING_PATHS"
test ! -s "$MISSING_PATHS"
test "$(wc -l < "$CITED_PATHS")" -gt 0
```

Result: PASS; every extracted cited repository source, test, evidence, configuration,
submodule-owned configuration, release-tool, solution, and restore-config path exists.

### xUnit v3 filter and zero-test guard check

The installed xUnit v3 in-process runner was probed directly. A nonexistent class returned
exit code zero and XML `total="0"`, proving that exit status alone is insufficient:

```bash
set -euo pipefail
ASSEMBLY='tests/Hexalith.EventStore.Client.Tests/bin/Release/net10.0/Hexalith.EventStore.Client.Tests.dll'
PACKET='_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md'
ZERO_XML="$(mktemp --suffix=.xml)"
SKIPPED_XML="$(mktemp --suffix=.xml)"
PASSED_XML="$(mktemp --suffix=.xml)"
VALIDATOR_FUNCTION="$(mktemp)"
awk '/^validate_xunit_result\(\) \{$/ { capture = 1 }
  capture { print }
  capture && /^}$/ { exit }' "$PACKET" > "$VALIDATOR_FUNCTION"
bash -n "$VALIDATOR_FUNCTION"
source "$VALIDATOR_FUNCTION"
set +e
dotnet "$ASSEMBLY" -noColor \
  -class Hexalith.EventStore.Client.Tests.DoesNotExist \
  -xml "$ZERO_XML"
ZERO_STATUS=$?
set -e
test "$ZERO_STATUS" -eq 0
grep -Eq 'total="0"' "$ZERO_XML"
printf '%s\n' '<assemblies total="4" passed="0" failed="0" skipped="4" errors="0" />' \
  > "$SKIPPED_XML"
! validate_xunit_result "$SKIPPED_XML"
printf '%s\n' '<assemblies total="4" passed="3" failed="0" skipped="1" errors="0" />' \
  > "$PASSED_XML"
validate_xunit_result "$PASSED_XML"

METHODS="$(mktemp)"
dotnet "$ASSEMBLY" -noColor -list methods \
  -class Hexalith.EventStore.Client.Tests.Queries.QueryCursorScopeTests > "$METHODS"
grep -Fq 'Hexalith.EventStore.Client.Tests.Queries.QueryCursorScopeTests.' "$METHODS"
```

Result: PASS; zero-match returned `0`/`total="0"`, the all-skipped fixture was rejected, the
fixture with three passed tests was accepted, and the positive list filter found six method
rows. This was a runner-behavior probe against an existing assembly, not parity closure
evidence; the mandatory harness still requires a fresh build, at least one passed test, and
no failures/errors for every credited run.

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
- Story 2.7 correction: committed at `fd8ab24d110e605dfd64487cda84a301126d337d`
  and recorded `done`. Base
  configuration no longer registers the stale `orders` and `inventory` sample bindings;
  Development configuration contains exactly the hosted `counter` and `greeting` domains.
- Corrected source-topology E2E: PASS, 1/1. The exact method
  `LiveHandlerRoute_WithCurrentProjectionValidator_NeutralizesProjectionEvidence` returned
  HTTP 200 with `HandlerComputed` provenance and no projection-validator leakage.
- Persisted-state proof: the harness stops EventStore, deletes the prior
  `admin:query-types:tenants` state, and restarts EventStore. After the request it reads the
  newly persisted Redis hash payload and verifies that it contains `list-tenants`; the
  assertion cannot pass on stale state.
- Story 2.7 preserves atomic fail-closed behavior for genuine operational-metadata failures
  and makes no Tenants identity change. Story 2.12 still owns later authorized Tenants
  identity adoption.
- Candidate boundary: the correction is committed, but this readiness audit did not select
  or execute the complete gate at an exact clean Story 1.20 runtime. The complete gate must be rerun at the selected
  unchanged candidate SHA before any parity row becomes authorizing.
- NuGet inventory, package consumer, container publication/inspection, Story 1.16 named
  follow-up disposition, and owner approvals remain incomplete for an authorizing runtime.

## Final Decision

`still blocked`

Story 1.20 and Epic 1 remain `in-progress`. The lifecycle and AD-11 implementation blockers
are cleared at commit `772cdfef...`, and Story 2.7's committed `fd8ab24d...` correction passes
the source-topology implementation prerequisite. It is not yet evidence from the exact clean
committed Story 1.20 candidate. Every parity row remains non-authorizing;
source/package/container identities are unapproved or unresolved, Story 1.16's named
follow-up disposition is open, and owner approval is absent.
