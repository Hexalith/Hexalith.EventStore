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
command -v curl >/dev/null
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
if any(ord(character) < 32 or ord(character) == 127 for character in container + blob):
    raise SystemExit("object URL contains a control character")
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

VERSIONED_OBJECT_URL="$(python3 - "$OBJECT_URL" "$OBJECT_VERSION" <<'PY'
import sys
import urllib.parse

print(f"{sys.argv[1]}?{urllib.parse.urlencode({'versionid': sys.argv[2]})}")
PY
)"
ACCESS_TOKEN="$(az account get-access-token \
  --resource 'https://storage.azure.com/' \
  --query accessToken \
  --output tsv \
  --only-show-errors)"
test -n "$ACCESS_TOKEN"
CURL_AUTHORIZATION_HEADER="Authorization: Bearer $ACCESS_TOKEN"

if test "$COMMAND" = 'download'; then
  curl --fail --silent --show-error \
    --proto '=https' --tlsv1.2 \
    --header "$CURL_AUTHORIZATION_HEADER" \
    --header 'x-ms-version: 2023-11-03' \
    --output "$OUTPUT" \
    "$VERSIONED_OBJECT_URL"
  test -s "$OUTPUT"
  exit 0
fi

BLOB_HEADERS="$(mktemp)"
cleanup() {
  rm -f -- "$BLOB_HEADERS"
}
trap cleanup EXIT

curl --fail --silent --show-error \
  --proto '=https' --tlsv1.2 \
  --head \
  --header "$CURL_AUTHORIZATION_HEADER" \
  --header 'x-ms-version: 2023-11-03' \
  --dump-header "$BLOB_HEADERS" \
  --output /dev/null \
  "$VERSIONED_OBJECT_URL"

ADAPTER_PATH="$(readlink -f -- "${BASH_SOURCE[0]}")"
ADAPTER_ID="${ADAPTER_PATH##*/}"
ADAPTER_ID="${ADAPTER_ID%.sh}"
ADAPTER_SHA256="$(sha256sum "$ADAPTER_PATH" | awk '{print $1}')"

python3 - \
  "$BLOB_HEADERS" "$OUTPUT" \
  "$ADAPTER_ID" "$ADAPTER_SHA256" "$OBJECT_URL" "$OBJECT_VERSION" <<'PY'
import datetime as dt
import email.utils
import json
import pathlib
import re
import sys

headers_path, output_path, adapter_id, adapter_sha256, object_url, object_version = sys.argv[1:]
headers = {}
for line in pathlib.Path(headers_path).read_text(encoding="iso-8859-1").splitlines():
    if ":" not in line:
        continue
    name, value = line.split(":", 1)
    normalized = name.strip().casefold()
    if normalized in headers:
        raise SystemExit(f"provider returned duplicate {normalized} headers")
    headers[normalized] = value.strip()

actual_version = headers.get("x-ms-version-id")
sha256 = headers.get("x-ms-meta-sha256")
expiry = headers.get("x-ms-immutability-policy-until-date")
blob_mode = headers.get("x-ms-immutability-policy-mode")

if actual_version != object_version:
    raise SystemExit("provider returned a different object version")
if not isinstance(sha256, str) or not re.fullmatch(r"[0-9a-f]{64}", sha256):
    raise SystemExit("blob metadata lacks the canonical sha256 value")
if blob_mode != "Locked":
    raise SystemExit("object immutability policy must be locked")
if not isinstance(expiry, str):
    raise SystemExit("blob metadata lacks an immutability expiry")
expiry_time = email.utils.parsedate_to_datetime(expiry)
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
