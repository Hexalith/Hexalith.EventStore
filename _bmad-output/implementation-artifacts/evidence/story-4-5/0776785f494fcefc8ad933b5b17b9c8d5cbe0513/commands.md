# Re-runnable Commands

Run all commands from the repository root. Ordinary reruns create a fresh timestamped directory under `runs/`; they do not overwrite the canonical reviewed capture. The explicit canonical-replacement token is documented only for a deliberate evidence refresh.

## Initialize a fresh capture

```bash
story_4_5_workspace="$(git rev-parse --show-toplevel)"
story_4_5_baseline="0776785f494fcefc8ad933b5b17b9c8d5cbe0513"
story_4_5_capture_root="$story_4_5_workspace/_bmad-output/implementation-artifacts/evidence/story-4-5"
story_4_5_canonical="$story_4_5_capture_root/$story_4_5_baseline"
story_4_5_run_id="${HEXALITH_STORY_4_5_RUN_ID:-$(date -u +%Y%m%dT%H%M%SZ)-$$}"
story_4_5_evidence="${HEXALITH_STORY_4_5_OUTPUT_DIR:-$story_4_5_capture_root/runs/$story_4_5_run_id}"
mkdir -p "$story_4_5_evidence"
if [[ "$(realpath "$story_4_5_evidence")" == "$(realpath "$story_4_5_canonical")" \
    && "${HEXALITH_STORY_4_5_ALLOW_CANONICAL_REPLACE:-}" != "$story_4_5_baseline" ]]; then
  echo "Refusing to overwrite the canonical reviewed capture" >&2
  exit 2
fi

for story_4_5_static in commands.md commit-capture.md environment.md redaction.md source-state.md validate-evidence.py verification-summary.md; do
  cp "$story_4_5_canonical/$story_4_5_static" "$story_4_5_evidence/$story_4_5_static"
done
story_4_5_runner="$story_4_5_workspace/tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests"
```

To refresh the canonical directory intentionally, set both variables before running that block:

```bash
export HEXALITH_STORY_4_5_OUTPUT_DIR="$PWD/_bmad-output/implementation-artifacts/evidence/story-4-5/0776785f494fcefc8ad933b5b17b9c8d5cbe0513"
export HEXALITH_STORY_4_5_ALLOW_CANONICAL_REPLACE="0776785f494fcefc8ad933b5b17b9c8d5cbe0513"
```

## Exact Release solution build

`pipefail` preserves a build failure through the redaction and `tee` pipeline. The raw build output is captured with workspace paths redacted.

```bash
set -o pipefail
dotnet build Hexalith.EventStore.slnx --configuration Release -p:UseHexalithProjectReferences=false 2>&1 \
  | sed -e "s#${story_4_5_workspace}#<workspace>#g" \
  | tee "$story_4_5_evidence/solution-build.log"
```

## Positive evidence runs

Deterministic classifier and safe-parser branch matrix:

```bash
"$story_4_5_runner" -noColor \
  -class 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.AppendDurabilityRaceClassifierTests' \
  -class 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.DaprStateErrorParserTests' \
  -ctrf "$story_4_5_evidence/classifier-parser-test-results.json"
```

Focused race and generic ETag control:

```bash
HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" "$story_4_5_runner" -noColor \
  -method 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.AppendDurabilityRaceLiveSidecarTests.SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome' \
  -ctrf "$story_4_5_evidence/race-test-results.json"

HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" "$story_4_5_runner" -noColor \
  -method 'Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.ActorConcurrencyConflictTests.MetadataKey_StaleEtagUpdate_IsRejected' \
  -ctrf "$story_4_5_evidence/generic-etag-test-results.json"
```

Full LiveSidecar regression:

```bash
HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" "$story_4_5_runner" -noColor \
  -ctrf "$story_4_5_evidence/live-sidecar-test-results.json"
```

## One-at-a-time invariant mutations

Each command must exit `1` with exactly its focused test failed. The environment variable changes only the selected test assertion; it does not alter production code or persisted inputs. The wrapper rejects an unexpected success or any exit code other than `1`.

```bash
story_4_5_expect_mutation_failure() {
  local mutation="$1"
  local method="$2"
  local receipt="$3"
  set +e
  HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" \
    HEXALITH_STORY_4_5_MUTATION="$mutation" \
    "$story_4_5_runner" -noColor -reporter quiet -method "$method" -ctrf "$story_4_5_evidence/$receipt"
  local exit_code=$?
  set -e
  if [[ $exit_code -ne 1 ]]; then
    echo "Mutation $mutation returned $exit_code; expected 1" >&2
    return 1
  fi
}

story_4_5_race_method='Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.AppendDurabilityRaceLiveSidecarTests.SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome'
story_4_5_generic_method='Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.ActorConcurrencyConflictTests.MetadataKey_StaleEtagUpdate_IsRejected'

story_4_5_expect_mutation_failure gate-timing "$story_4_5_race_method" mutation-gate-timing.json
story_4_5_expect_mutation_failure intermediate-raw-durability "$story_4_5_race_method" mutation-intermediate-raw-durability.json
story_4_5_expect_mutation_failure final-state-consistency "$story_4_5_race_method" mutation-final-state-consistency.json
story_4_5_expect_mutation_failure conflict-retry-classification "$story_4_5_race_method" mutation-conflict-retry-classification.json
story_4_5_expect_mutation_failure key-addressability "$story_4_5_race_method" mutation-key-addressability.json
story_4_5_expect_mutation_failure generic-409-semantics "$story_4_5_generic_method" mutation-generic-409-semantics.json
story_4_5_expect_mutation_failure retained-generic-value "$story_4_5_generic_method" mutation-retained-generic-value.json
```

Restore and rerun the positive focused pair after every mutation:

```bash
unset HEXALITH_STORY_4_5_MUTATION
HEXALITH_STORY_4_5_EVIDENCE_DIR="$story_4_5_evidence" "$story_4_5_runner" -noColor \
  -method "$story_4_5_race_method" -method "$story_4_5_generic_method" \
  -ctrf "$story_4_5_evidence/post-mutation-focused-test-results.json"
```

## Redact, hash, and validate semantics

```bash
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
! rg -n '/home/[^/[:space:]]+/projects/hexalith/eventstore|\"computer\": \"(?!<redacted-machine>)|\"user\": \"(?!<redacted-machine-user>)' \
  "$story_4_5_evidence" --glob '*.json' --glob '*.log' --pcre2
(cd "$story_4_5_evidence" && sha256sum -c evidence-sha256.txt)
```

## Repository gates

```bash
python3 scripts/check-deferred-work.py
git diff --check
git diff --name-only 0776785f494fcefc8ad933b5b17b9c8d5cbe0513 -- src .github/workflows
```
