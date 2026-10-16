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
