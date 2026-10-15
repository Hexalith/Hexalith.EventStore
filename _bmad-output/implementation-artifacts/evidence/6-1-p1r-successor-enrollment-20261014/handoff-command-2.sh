set -euo pipefail
: "${RELEASE_RUN:?observed exact release run ID required}"
: "${RELEASE_ATTEMPT:?observed exact release attempt required}"
: "${NEW_RELEASE_EVIDENCE:?new evidence directory required}"
gh run view "$RELEASE_RUN" --repo Hexalith/Hexalith.EventStore \
  --json headSha,event,status,conclusion,url
gh run download "$RELEASE_RUN" --repo Hexalith/Hexalith.EventStore \
  --name "release-evidence-${RELEASE_RUN}-${RELEASE_ATTEMPT}" \
  --dir "$NEW_RELEASE_EVIDENCE"
