set -euo pipefail
: "${S:?exact authorized frozen source SHA required}"
test "$(git rev-parse HEAD)" = "$S"
test "$(gh api repos/Hexalith/Hexalith.EventStore/git/ref/heads/main --jq .object.sha)" = "$S"
gh api --method GET repos/Hexalith/Hexalith.EventStore/actions/workflows/ci.yml/runs \
  -f branch=main -f event=push -f head_sha="$S" -f status=success -f per_page=100 \
  --jq '[.workflow_runs[] | select(.head_sha == "'"$S"'" and .head_branch == "main" and .event == "push" and .status == "completed" and .conclusion == "success")] | length > 0' \
  | python3 -c 'import json,sys; assert json.load(sys.stdin) is True'
gh workflow run release.yml --repo Hexalith/Hexalith.EventStore --ref main \
  -f bypass-validation=false
