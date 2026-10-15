set -euo pipefail
: "${P1R_NEW:?new invocation parent inside the workspace required}"
: "${P1R_PLANNING:?new independently observed planning JSON required}"
: "${OWNER_SELECTION_REFERENCE:?explicit new tuple-selection reference required}"
python3 tools/p1r-published-executor.py observe --out "$P1R_NEW/observed" \
  --planning-observations "$P1R_PLANNING" \
  --selection-reference "$OWNER_SELECTION_REFERENCE"
