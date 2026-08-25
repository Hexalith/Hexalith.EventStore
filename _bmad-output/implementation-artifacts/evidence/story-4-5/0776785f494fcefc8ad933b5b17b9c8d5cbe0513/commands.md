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

## Exact Release solution build

`--no-incremental` is mandatory: an up-to-date build skips `CoreCompile` entirely, and MSBuild
emits no warnings for a compile it never ran, which would make the `0 Warning(s)` line vacuous
under `TreatWarningsAsErrors=true`. `pipefail` is scoped to a subshell so it cannot leak into the
operator's shell; it preserves a build failure through the redaction and `tee` pipeline.

```bash
: "${story_4_5_workspace:?run story_4_5_init first}"
: "${story_4_5_evidence:?run story_4_5_init first}"
(
  set -o pipefail
  dotnet build "$story_4_5_workspace/Hexalith.EventStore.slnx" \
    --configuration Release --no-incremental -p:UseHexalithProjectReferences=false 2>&1 \
    | sed -e "s#${story_4_5_workspace}#<workspace>#g" \
    | tee "$story_4_5_evidence/solution-build.log"
)
```

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
  -class 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.DaprStateErrorParserTests' \
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
  local exit_code=0
  HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" \
    HEXALITH_STORY_4_5_MUTATION="$mutation" \
    "$story_4_5_runner" -noColor -reporter quiet -method "$method" \
    -ctrf "$story_4_5_evidence/$receipt" || exit_code=$?
  if [[ $exit_code -ne 1 ]]; then
    echo "Mutation $mutation returned $exit_code; expected 1" >&2
    return 1
  fi
  echo "Mutation $mutation produced $receipt"
}

story_4_5_expect_mutation_failure gate-hold "$story_4_5_race_method" mutation-gate-hold.json
story_4_5_expect_mutation_failure gate-targeting "$story_4_5_race_method" mutation-gate-targeting.json
story_4_5_expect_mutation_failure intermediate-raw-durability "$story_4_5_race_method" mutation-intermediate-raw-durability.json
story_4_5_expect_mutation_failure key-addressability "$story_4_5_race_method" mutation-key-addressability.json
story_4_5_expect_mutation_failure final-state-classified "$story_4_5_race_method" mutation-final-state-classified.json
story_4_5_expect_mutation_failure conflict-retry-classification "$story_4_5_race_method" mutation-conflict-retry-classification.json
story_4_5_expect_mutation_failure infrastructure-free "$story_4_5_race_method" mutation-infrastructure-free.json
story_4_5_expect_mutation_failure generic-409-semantics "$story_4_5_generic_method" mutation-generic-409-semantics.json
story_4_5_expect_mutation_failure retained-generic-value "$story_4_5_generic_method" mutation-retained-generic-value.json
```

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
git diff --name-only 0776785f494fcefc8ad933b5b17b9c8d5cbe0513 -- src .github
```

The last command must print nothing: AC6 requires `src/` and `.github/` to remain byte-identical to
the baseline.
