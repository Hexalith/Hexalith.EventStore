#!/usr/bin/env bash

set -euo pipefail

usage() {
  printf 'usage: %s <download|describe> --object-url URL --object-version VERSION --output PATH\n' \
    "${0##*/}" >&2
  exit 2
}

test "$#" -ge 1 || usage
COMMAND="$1"
shift

OBJECT_URL=''
OBJECT_VERSION=''
OUTPUT=''
while test "$#" -gt 0; do
  case "$1" in
    --object-url)
      test "$#" -ge 2 || usage
      OBJECT_URL="$2"
      shift 2
      ;;
    --object-version)
      test "$#" -ge 2 || usage
      OBJECT_VERSION="$2"
      shift 2
      ;;
    --output)
      test "$#" -ge 2 || usage
      OUTPUT="$2"
      shift 2
      ;;
    *) usage ;;
  esac
done

case "$COMMAND" in
  download|describe) ;;
  *) usage ;;
esac
test -n "$OBJECT_URL" && test -n "$OBJECT_VERSION" && test -n "$OUTPUT" || usage
test ! -e "$OUTPUT"
command -v az >/dev/null
command -v python3 >/dev/null

mapfile -t OBJECT_PARTS < <(python3 - "$OBJECT_URL" <<'PY'
import re
import sys
import urllib.parse

parsed = urllib.parse.urlsplit(sys.argv[1])
if parsed.scheme != "https" or parsed.username or parsed.password or parsed.port:
    raise SystemExit("object URL must be credential-free Azure Blob HTTPS")
match = re.fullmatch(r"([a-z0-9]{3,24})\.blob\.core\.windows\.net", parsed.hostname or "")
if not match or parsed.query or parsed.fragment:
    raise SystemExit("object URL must target one canonical Azure Blob object")
segments = parsed.path.removeprefix("/").split("/", 1)
if len(segments) != 2 or not segments[0] or not segments[1]:
    raise SystemExit("object URL must include container and blob name")
container = urllib.parse.unquote(segments[0])
blob = urllib.parse.unquote(segments[1])
if container != segments[0] or any(character in container for character in "/\\"):
    raise SystemExit("container name must use its canonical unescaped form")
if blob in {".", ".."} or any(part in {".", ".."} for part in blob.split("/")):
    raise SystemExit("blob name contains a traversal segment")
print(match.group(1))
print(container)
print(blob)
PY
)
test "${#OBJECT_PARTS[@]}" -eq 3
ACCOUNT_NAME="${OBJECT_PARTS[0]}"
CONTAINER_NAME="${OBJECT_PARTS[1]}"
BLOB_NAME="${OBJECT_PARTS[2]}"

if test "$COMMAND" = 'download'; then
  az storage blob download \
    --account-name "$ACCOUNT_NAME" \
    --container-name "$CONTAINER_NAME" \
    --name "$BLOB_NAME" \
    --version-id "$OBJECT_VERSION" \
    --auth-mode login \
    --file "$OUTPUT" \
    --overwrite true \
    --no-progress \
    --only-show-errors \
    --output none
  test -s "$OUTPUT"
  exit 0
fi

BLOB_METADATA="$(mktemp)"
CONTAINER_POLICY="$(mktemp)"
cleanup() {
  rm -f -- "$BLOB_METADATA" "$CONTAINER_POLICY"
}
trap cleanup EXIT

az storage blob show \
  --account-name "$ACCOUNT_NAME" \
  --container-name "$CONTAINER_NAME" \
  --name "$BLOB_NAME" \
  --version-id "$OBJECT_VERSION" \
  --auth-mode login \
  --only-show-errors \
  --output json > "$BLOB_METADATA"
az storage container immutability-policy show \
  --account-name "$ACCOUNT_NAME" \
  --container-name "$CONTAINER_NAME" \
  --only-show-errors \
  --output json > "$CONTAINER_POLICY"

ADAPTER_PATH="$(readlink -f -- "${BASH_SOURCE[0]}")"
ADAPTER_ID="${ADAPTER_PATH##*/}"
ADAPTER_ID="${ADAPTER_ID%.sh}"
ADAPTER_SHA256="$(sha256sum "$ADAPTER_PATH" | awk '{print $1}')"

python3 - \
  "$BLOB_METADATA" "$CONTAINER_POLICY" "$OUTPUT" \
  "$ADAPTER_ID" "$ADAPTER_SHA256" "$OBJECT_URL" "$OBJECT_VERSION" <<'PY'
import datetime as dt
import json
import pathlib
import re
import sys

blob_path, policy_path, output_path, adapter_id, adapter_sha256, object_url, object_version = sys.argv[1:]
blob = json.loads(pathlib.Path(blob_path).read_text(encoding="utf-8"))
policy = json.loads(pathlib.Path(policy_path).read_text(encoding="utf-8"))

actual_version = blob.get("versionId") or blob.get("version_id")
sha256 = (blob.get("metadata") or {}).get("sha256")
immutability = blob.get("immutabilityPolicy") or blob.get("immutability_policy") or {}
expiry = immutability.get("expiryTime") or immutability.get("expiry_time")
blob_mode = immutability.get("policyMode") or immutability.get("policy_mode")
policy_state = policy.get("state")

if actual_version != object_version:
    raise SystemExit("provider returned a different object version")
if not isinstance(sha256, str) or not re.fullmatch(r"[0-9a-f]{64}", sha256):
    raise SystemExit("blob metadata lacks the canonical sha256 value")
if policy_state != "Locked" or blob_mode != "Locked":
    raise SystemExit("container and object immutability policies must both be locked")
if not isinstance(expiry, str):
    raise SystemExit("blob metadata lacks an immutability expiry")
expiry_time = dt.datetime.fromisoformat(expiry.replace("Z", "+00:00"))
if expiry_time.tzinfo is None:
    raise SystemExit("immutability expiry must carry a timezone")
retention_until = expiry_time.astimezone(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")

proof = {
    "schema": "hexalith.eventstore.provider-worm-object-proof/v2",
    "provider": {
        "adapter_id": adapter_id,
        "adapter_sha256": adapter_sha256,
        "authenticated_api": True,
    },
    "object": {
        "url": object_url,
        "version": object_version,
        "sha256": sha256,
    },
    "policy": {
        "mode": "WORM",
        "locked": True,
        "retention_until": retention_until,
    },
}
pathlib.Path(output_path).write_text(
    json.dumps(proof, indent=2, sort_keys=True) + "\n",
    encoding="utf-8",
)
PY
