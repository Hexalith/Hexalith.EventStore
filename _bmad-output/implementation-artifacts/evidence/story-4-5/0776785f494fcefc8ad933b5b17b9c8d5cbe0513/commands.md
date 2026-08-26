# Re-runnable Commands

Run all commands from the repository root with `bash`. Ordinary reruns create a fresh timestamped
directory under `runs/`; they do not overwrite the canonical reviewed capture. The explicit
canonical-replacement token is documented only for a deliberate evidence refresh.

Every block below is written so that running it in an interactive shell cannot terminate that
shell, cannot leave `errexit`/`pipefail` armed after it returns, and cannot act on an unset
variable. Each block is self-contained: it re-derives nothing implicitly and guards every variable
it consumes.

## Initialize a fresh capture

```bash
story_4_5_init() {
  story_4_5_workspace="$(git rev-parse --show-toplevel)" || return 1
  story_4_5_baseline="0776785f494fcefc8ad933b5b17b9c8d5cbe0513"
  story_4_5_capture_root="$story_4_5_workspace/_bmad-output/implementation-artifacts/evidence/story-4-5"
  story_4_5_canonical="$story_4_5_capture_root/$story_4_5_baseline"
  story_4_5_run_id="${HEXALITH_STORY_4_5_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)-$$}"
  story_4_5_evidence="${HEXALITH_STORY_4_5_OUTPUT_DIR:-$story_4_5_capture_root/runs/$story_4_5_run_id}"
  mkdir -p "$story_4_5_evidence" || return 1
  if [[ "$(realpath "$story_4_5_evidence")" == "$(realpath "$story_4_5_canonical")" \
      && "${HEXALITH_STORY_4_5_ALLOW_CANONICAL_REPLACE:-}" != "$story_4_5_baseline" ]]; then
    echo "Refusing to overwrite the canonical reviewed capture" >&2
    return 2
  fi

  # A deliberate canonical refresh writes into the canonical directory itself, where the static
  # files are already in place; copying a file onto itself would fail.
  if [[ "$(realpath "$story_4_5_evidence")" != "$(realpath "$story_4_5_canonical")" ]]; then
    local story_4_5_static
    for story_4_5_static in commands.md commit-capture.md environment.md redaction.md \
        source-state.md validate-evidence.py verification-summary.md; do
      cp "$story_4_5_canonical/$story_4_5_static" "$story_4_5_evidence/$story_4_5_static" || return 1
    done
  fi
  story_4_5_runner="$story_4_5_workspace/tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests"
  story_4_5_race_method='Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.AppendDurabilityRaceLiveSidecarTests.SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome'
  story_4_5_generic_method='Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.ActorConcurrencyConflictTests.MetadataKey_StaleEtagUpdate_IsRejected'
}

story_4_5_init && echo "capture directory: $story_4_5_evidence"
```

To refresh the canonical directory intentionally, export both variables **before** calling
`story_4_5_init`:

```bash
export HEXALITH_STORY_4_5_OUTPUT_DIR="$PWD/_bmad-output/implementation-artifacts/evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513"
export HEXALITH_STORY_4_5_ALLOW_CANONICAL_REPLACE="0776785f494fcefc8ad933b5b17b9c8d5cbe0513"
```

## Control-plane port layout during the campaign

Where `dapr init` publishes placement/scheduler on `6050`/`6060` rather than `50005`/`50006`, run
the campaign in two modes so both requirements are met at once:

- **Forwarder DOWN** for the build, the deterministic matrix, the focused runs, the perturbation
  campaign and the restored focused pair. The Story 4.5 fixture then falls through to `6050`/`6060`,
  which is what exercises — and records — the dual-probe branch the packet advertises.
  `validate-evidence.py` asserts `controlPlanePorts.placementResolved == 6050`.
- **Forwarder UP** for the two full-suite receipts only. `Oq8PostgresqlFixture` is owned by Story
  4.14, hash-bound by the sealed 4.14/4.15 packets, and hard-codes `50005`/`50006`; forwarding lets
  that collection start without editing a file this story must not touch.

```bash
# forwarder up (full-suite receipts only)
socat TCP-LISTEN:50005,bind=127.0.0.1,fork,reuseaddr TCP:127.0.0.1:6050 &
socat TCP-LISTEN:50006,bind=127.0.0.1,fork,reuseaddr TCP:127.0.0.1:6060 &
# forwarder down again before the focused/perturbation runs
kill %1 %2
```

On a host where `dapr init` already publishes `50005`/`50006`, no forwarder is needed and the
resolved ports will be the first candidates; that capture would not exercise the second branch, and
`validate-evidence.py` would reject it as the reviewed capture.

## Run order

The order matters and the validator enforces part of it:

1. Release solution build (forwarder down).
2. Deterministic matrix (down).
3. Focused race and generic control (down) — writes captures.
4. **Full LiveSidecar suite (up)** — before the campaign.
5. Perturbation campaign (down).
6. Restored focused pair (down) — rewrites and seals the committed captures.
7. **Post-mutation full LiveSidecar suite (up)** — after the campaign.
8. Redact, hash, validate.

`validate-evidence.py` requires steps 6 and 7 to have started at or after the last perturbation
receipt finished, so a pre-campaign copy cannot satisfy either.

A parallel `aspire run` on the same machine registers the fixed, non-namespaced
`IdempotencyAdmissionActor` type into the same placement ring and will fail the admission tests in
steps 4 and 7. Check `pgrep -af daprd` and `docker logs dapr_placement` before trusting a red
full-suite run.

## Exact Release solution build

`--no-incremental` is mandatory: an up-to-date build skips `CoreCompile` entirely, and MSBuild
emits no warnings for a compile it never ran, which would make the `0 Warning(s)` line vacuous
under `TreatWarningsAsErrors=true`. `pipefail` is scoped to a subshell so it cannot leak into the
operator's shell; it preserves a build failure through the redaction and `tee` pipeline.

```bash
: "${story_4_5_workspace:?run story_4_5_init first}"
: "${story_4_5_evidence:?run story_4_5_init first}"
story_4_5_build_command=(dotnet build "<workspace>/Hexalith.EventStore.slnx"
  --configuration Release --no-incremental -p:UseHexalithProjectReferences=false)
printf '$ %s\n' "${story_4_5_build_command[*]}" > "$story_4_5_evidence/solution-build.log"
(
  set -o pipefail
  dotnet build "$story_4_5_workspace/Hexalith.EventStore.slnx" \
    --configuration Release --no-incremental -p:UseHexalithProjectReferences=false 2>&1 \
    | sed -e "s#${story_4_5_workspace}#<workspace>#g" \
    | tee -a "$story_4_5_evidence/solution-build.log"
)
```

The first line of `solution-build.log` records the exact command that produced the rest of it. That
line is the load-bearing check: MSBuild prints a `Project -> path` line for up-to-date projects
too, so the project count cannot distinguish a real compile from a no-op, and `0 Warning(s)` is
vacuous for a compile that never ran. `validate-evidence.py` requires the header to carry
`--no-incremental` and `--configuration Release`, and applies an elapsed-time floor as a backstop.

`Hexalith.EventStore.Gateway` and `Hexalith.EventStore.TestSubscriber` emit `bin/Debug` paths inside
this Release build because neither project is a member of `Hexalith.EventStore.slnx`, so the
solution configuration does not flow to them. Their `.csproj` files live under `src/`, which AC6
freezes byte-for-byte, so this story records the condition instead of fixing it; the validator
allowlists exactly those two names, re-checks that they really are non-members, and rejects a
`bin/Debug` path from anything else.

## Positive evidence runs

Deterministic classifier and safe-parser branch matrix:

```bash
: "${story_4_5_runner:?run story_4_5_init first}"
"$story_4_5_runner" -noColor \
  -class 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.AppendDurabilityRaceClassifierTests' \
  -class 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.AppendDurabilityFinalShapeClassifierTests' \
  -class 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.DaprStateErrorParserTests' \
  -class 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.StateStoreComponentCanonicalizationTests' \
  -class 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.Story45MutationSwitchTests' \
  -ctrf "$story_4_5_evidence/classifier-parser-test-results.json"
```

Focused race and generic ETag control:

```bash
HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" "$story_4_5_runner" -noColor \
  -method "$story_4_5_race_method" \
  -ctrf "$story_4_5_evidence/race-test-results.json"

HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" "$story_4_5_runner" -noColor \
  -method "$story_4_5_generic_method" \
  -ctrf "$story_4_5_evidence/generic-etag-test-results.json"
```

Full LiveSidecar regression. `HEXALITH_STORY_4_5_EVIDENCE_DIR` is deliberately **not** set here:
the suite includes the two capturing tests, and letting a regression run rewrite the committed
captures is what previously decoupled a capture from the receipt that is supposed to attest it.

```bash
"$story_4_5_runner" -noColor \
  -ctrf "$story_4_5_evidence/live-sidecar-test-results.json"
```

## One-at-a-time invariant perturbations

A perturbation changes what the harness **does** -- it releases the gate early, skips a probe,
arms the gate for the wrong writer, classifies against a sequence the state does not exhibit,
points a probe at an unroutable endpoint, replays a token that is still current, or reads a key the
run never wrote. It never inverts an assertion, and it never rewrites a committed capture: the
capture writer returns early whenever `HEXALITH_STORY_4_5_MUTATION` is set, so the perturbed run
reaches the receipt only through the test's xUnit output.

An unrecognized perturbation name fails closed with `InvalidOperationException` rather than running
the unperturbed harness and producing a receipt that looks like a real mutation.

Each command must exit `1` with exactly its focused test failed. The wrapper rejects an unexpected
success or any exit code other than `1`, and does not arm `errexit` in the operator's shell.

```bash
story_4_5_expect_mutation_failure() {
  local mutation="$1"
  local method="$2"
  local receipt="$3"
  shift 3
  local exit_code=0
  HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" \
    HEXALITH_STORY_4_5_MUTATION="$mutation" \
    "$story_4_5_runner" -noColor -reporter quiet -method "$method" \
    -ctrf "$story_4_5_evidence/$receipt" || exit_code=$?
  if [[ $exit_code -ne 1 ]]; then
    echo "Mutation $mutation returned $exit_code; expected 1" >&2
    return 1
  fi

  # Exit 1 alone is not evidence: a harness crash, a timeout, or an argument error also exits 1.
  # Require the receipt to attribute the failure to every invariant this perturbation must
  # falsify, which is the same set validate-evidence.py pins.
  local invariant
  for invariant in "$@"; do
    if ! grep -q "\[invariant:${invariant}\]" "$story_4_5_evidence/$receipt"; then
      echo "Mutation $mutation exited 1 without attributing to [invariant:$invariant]" >&2
      return 1
    fi
  done
  echo "Mutation $mutation produced $receipt"
}
```

Run the campaign as one chained command so a failure stops it rather than scrolling past while a
later success masks it:

```bash
story_4_5_expect_mutation_failure gate-hold "$story_4_5_race_method" mutation-gate-hold.json gate-hold \
&& story_4_5_expect_mutation_failure gate-targeting "$story_4_5_race_method" mutation-gate-targeting.json gate-hold gate-targeting \
&& story_4_5_expect_mutation_failure intermediate-raw-durability "$story_4_5_race_method" mutation-intermediate-raw-durability.json intermediate-raw-durability \
&& story_4_5_expect_mutation_failure key-addressability "$story_4_5_race_method" mutation-key-addressability.json key-addressability \
&& story_4_5_expect_mutation_failure final-state-sound "$story_4_5_race_method" mutation-final-state-sound.json final-state-sound \
&& story_4_5_expect_mutation_failure conflict-retry-classification "$story_4_5_race_method" mutation-conflict-retry-classification.json conflict-retry-classification \
&& story_4_5_expect_mutation_failure infrastructure-free "$story_4_5_race_method" mutation-infrastructure-free.json infrastructure-free \
&& story_4_5_expect_mutation_failure infrastructure-free-transport "$story_4_5_race_method" mutation-infrastructure-free-transport.json infrastructure-free key-addressability conflict-retry-classification \
&& story_4_5_expect_mutation_failure state-store-component-identity "$story_4_5_race_method" mutation-state-store-component-identity.json state-store-component-identity \
&& story_4_5_expect_mutation_failure stale-token-proven-stale "$story_4_5_generic_method" mutation-stale-token-proven-stale.json stale-token-proven-stale \
&& story_4_5_expect_mutation_failure generic-409-semantics "$story_4_5_generic_method" mutation-generic-409-semantics.json generic-409-semantics \
&& story_4_5_expect_mutation_failure retained-generic-value "$story_4_5_generic_method" mutation-retained-generic-value.json retained-generic-value \
&& echo "mutation campaign complete"
```

Two perturbations falsify more than one invariant, and the argument lists say so rather than hiding
it: holding the wrong writer (`gate-targeting`) also means the intended writer was not held, and
redirecting the writer endpoint (`infrastructure-free-transport`) also destroys the namespace probe
and the race classification. `validate-evidence.py` pins the same exact sets and rejects a receipt
that falsifies any other combination.

## Restored runs after the mutation campaign

The focused pair runs last among the capturing commands, so the committed
`append-durability-race.json` and `generic-etag-control.json` are exactly the captures embedded in
`post-mutation-focused-test-results.json`. `validate-evidence.py` asserts that equality, which is
what binds each committed artifact to the run that produced it.

```bash
unset HEXALITH_STORY_4_5_MUTATION
HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" "$story_4_5_runner" -noColor \
  -method "$story_4_5_race_method" -method "$story_4_5_generic_method" \
  -ctrf "$story_4_5_evidence/post-mutation-focused-test-results.json"
```

A mutation campaign dirties the process environment, so a clean **full** suite must follow it as
well. This run again omits `HEXALITH_STORY_4_5_EVIDENCE_DIR`, so it proves the whole suite is green
after the campaign without rewriting the captures the previous command sealed.

```bash
"$story_4_5_runner" -noColor \
  -ctrf "$story_4_5_evidence/post-mutation-live-sidecar-test-results.json"
```

## Redact, hash, and validate semantics

```bash
: "${story_4_5_workspace:?run story_4_5_init first}"
: "${story_4_5_evidence:?run story_4_5_init first}"
for story_4_5_json in "$story_4_5_evidence"/*.json; do
  sed -i -e "s#${story_4_5_workspace}#<workspace>#g" "$story_4_5_json"
  story_4_5_redacting="${story_4_5_json}.redacting"
  jq 'if ((.results?.extra? | type) == "object") then
        .results.extra.computer = "<redacted-machine>"
        | .results.extra.user = "<redacted-machine-user>"
      else . end' "$story_4_5_json" > "$story_4_5_redacting"
  mv "$story_4_5_redacting" "$story_4_5_json"
done

(cd "$story_4_5_evidence" && \
  find . -maxdepth 1 -type f ! -name evidence-sha256.txt -printf '%f\0' \
    | sort -z | xargs -0 sha256sum > evidence-sha256.txt)

python3 "$story_4_5_evidence/validate-evidence.py" "$story_4_5_evidence"
```

The redaction scan must distinguish "no match" (`rg` exit 1, the expected result) from "the scan
itself failed" (`rg` exit 2 for a bad glob, a missing PCRE2 build, or an unreadable file). A bare
`! rg …` inverts both into success and reports the directory clean having scanned nothing.

```bash
story_4_5_redaction_scan() {
  : "${story_4_5_evidence:?run story_4_5_init first}"
  local exit_code=0
  rg -n '/home/[^/[:space:]]+/projects/hexalith/eventstore|"computer": "(?!<redacted-machine>)|"user": "(?!<redacted-machine-user>)' \
    "$story_4_5_evidence" --glob '*.json' --glob '*.log' --pcre2 || exit_code=$?
  case $exit_code in
    0) echo "Redaction scan FAILED: unredacted identifiers remain" >&2; return 1 ;;
    1) echo "Redaction scan clean" ;;
    *) echo "Redaction scan could not run (rg exit $exit_code)" >&2; return 1 ;;
  esac
}

story_4_5_redaction_scan
(cd "$story_4_5_evidence" && sha256sum -c evidence-sha256.txt)
```

## Repository gates

```bash
python3 scripts/check-deferred-work.py
git diff --check
```

### AC6 — no `src/` or `.github/` behavior change

AC6 requires that **Story 4.5 changes nothing** under `src/` or `.github/`. The literal
`git diff --name-only 0776785f… -- src .github` was empty while the story branch sat at the
baseline, but `main` has since advanced through Stories 3.13-3.15 and 4.8-4.15, so that command now
reports those unrelated stories' changes and is no longer a Story 4.5 gate.

The candidate commits are **derived**, not hand-listed: every Story 4.5 commit touches the story's
spec, its report, or its evidence directory, so `git log` enumerates them and a future commit cannot
escape the gate by being forgotten. Some of those commits are **shared** — a single commit can carry
another story's `src/` or `.github/` change alongside a Story 4.5 artifact edit, and per-commit
attribution is then genuinely ambiguous. Each such commit must therefore be *declared* below with
the story that owns its production change; an undeclared one fails the gate loudly.

```bash
: "${story_4_5_workspace:?run story_4_5_init first}"

# sha -> owning story of the src/.github change carried in that shared commit.
declare -A story_4_5_shared_commits=(
  # "recover committed events whose publication was never scheduled": the src/ paths are
  # Story 4.4's implementation, committed together with Story 4.5's first evidence drop.
  [86308550]="Story 4.4 committed-event publication recovery"
  # "update sprint status, deployment guide, and CI documentation": rotates the Hexalith.Builds
  # release SHA in .github/workflows/release.yml for Story 3.14; it touched the Story 4.5 spec
  # only because that is where the loop-4 review findings were written.
  [ba0c367e]="Story 3.14 corrective OCI provenance release"
)

story_4_5_ac6() {
  local sha short touched status=0
  while read -r sha; do
    touched="$(git -C "$story_4_5_workspace" show --name-only --format= "$sha" -- src .github)"
    [[ -z "$touched" ]] && continue
    short="${sha:0:8}"
    if [[ -n "${story_4_5_shared_commits[$short]:-}" ]]; then
      echo "declared shared commit $short -> ${story_4_5_shared_commits[$short]}"
    else
      echo "AC6 violation: undeclared src/.github change in $sha:"; echo "$touched"; status=1
    fi
  done < <(git -C "$story_4_5_workspace" log --format='%H' \
      0776785f494fcefc8ad933b5b17b9c8d5cbe0513..HEAD -- \
      '_bmad-output/implementation-artifacts/spec-4-5-append-durability-race-evidence.md' \
      '_bmad-output/implementation-artifacts/4-5-append-durability-race-evidence.md' \
      '_bmad-output/implementation-artifacts/evidence/story-4-5')

  # Tracked working-tree changes AND untracked additions. `git diff` alone never sees a new file.
  touched="$(git -C "$story_4_5_workspace" diff --name-only HEAD -- src .github)"
  [[ -n "$touched" ]] && { echo "AC6 violation, tracked working tree:"; echo "$touched"; status=1; }
  touched="$(git -C "$story_4_5_workspace" status --porcelain --untracked-files=all -- src .github)"
  [[ -n "$touched" ]] && { echo "AC6 violation, untracked or staged:"; echo "$touched"; status=1; }

  [[ $status -eq 0 ]] && echo "AC6 holds: no Story 4.5 change under src/ or .github/"
  return $status
}

story_4_5_ac6
```

Expected output: the two declared shared commits, then
`AC6 holds: no Story 4.5 change under src/ or .github/`.
