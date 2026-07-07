#!/usr/bin/env bash
# Generated API DAPR/Aspire smoke preflight (Story 3.8).
#
# A LOCAL, READ-ONLY diagnostic gate. It classifies environment readiness, Aspire topology
# state, DAPR sidecar health, and (optionally) the generated Sample REST API surface BEFORE a
# developer records an "Aspire smoke blocked" note or treats a generated-API endpoint failure as
# a product defect. It never replaces the generator tests or the integration tests.
#
# Usage:
#   scripts/generated-api-smoke-preflight.sh                 # read-only preflight (default)
#   scripts/generated-api-smoke-preflight.sh --sample-api-smoke   # + HTTP smoke of the generated Sample API
#   scripts/generated-api-smoke-preflight.sh --json          # machine-readable output for story records
#   scripts/generated-api-smoke-preflight.sh --tenants       # also check the Tenants generated API when present
#   scripts/generated-api-smoke-preflight.sh --start-control-plane  # (explicit) start placement/scheduler if missing
#   scripts/generated-api-smoke-preflight.sh --help
#
# Overrides (used only as a fallback when `aspire describe` output is unavailable):
#   --sample-api-url URL         Base URL of the generated Sample API host (e.g. http://localhost:5016)
#   --eventstore-dapr-port N     DAPR HTTP port of the eventstore sidecar (default 3501)
#   --sample-api-dapr-port N     DAPR HTTP port of the sample-api sidecar
#   --apphost PATH               AppHost project/dir for `aspire describe` (default: src/Hexalith.EventStore.AppHost)
#
# Exit status categories (AC7):
#   0  success                 4  generated-api failure (product defect)
#   1  usage error             5  state-evidence failure (persisted state contradicts the response)
#   2  blocked environment     3  topology not running
#
# The default mode is READ-ONLY: it starts no Docker containers, placement, scheduler, or Aspire.
# Only --start-control-plane starts processes, and it prints the exact command it runs.
#
# Support-safe: every emitted line passes through redact(); the minted dev JWT, DAPR API tokens,
# raw payloads, and raw traces are never printed. The redaction categories mirror the shared C#
# contract Hexalith.EventStore.Testing.Integration.DaprDiagnostics.ToSupportSafeDiagnostic.

set -uo pipefail

# --- Constants -------------------------------------------------------------------------------

readonly DEFAULT_APPHOST="src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj"
readonly DEFAULT_EVENTSTORE_DAPR_PORT="3501"   # fixed in the AppHost so Admin.Server can query metadata
readonly DEV_SIGNING_KEY="DevOnlySigningKey-AtLeast32Chars!"  # dev-only symmetric key (EnableKeycloak=false)
readonly DEV_ISSUER="hexalith-dev"
readonly DEV_AUDIENCE="hexalith-eventstore"
readonly SMOKE_TENANT="tenant-a"
readonly SMOKE_COUNTER="counter-1"
readonly REDIS_PORT="6379"
# Candidate host ports in preference order, mirroring DaprLocalEndpoints: containerized `dapr init`
# (6050/6060) first, then legacy slim-mode native ports (50005/50006).
readonly PLACEMENT_CANDIDATES=(6050 50005)
readonly SCHEDULER_CANDIDATES=(6060 50006)

# Exit codes.
readonly EX_OK=0
readonly EX_USAGE=1
readonly EX_BLOCKED=2
readonly EX_NO_TOPOLOGY=3
readonly EX_API_FAIL=4
readonly EX_STATE_FAIL=5

# --- Options (defaults) ----------------------------------------------------------------------

JSON_MODE=0
RUN_SMOKE=0
CHECK_TENANTS=0
START_CONTROL_PLANE=0
APPHOST="${DEFAULT_APPHOST}"
SAMPLE_API_URL=""
EVENTSTORE_DAPR_PORT="${DEFAULT_EVENTSTORE_DAPR_PORT}"
EVENTSTORE_DAPR_PORT_EXPLICIT=0
SAMPLE_API_DAPR_PORT=""

# Accumulated findings as compact JSON objects (assembled by jq at the end).
FINDINGS_JSON=()
# Highest-precedence terminal state observed so far.
WORST_EXIT="${EX_OK}"
WORST_STATUS="ok"

# --- Small helpers ---------------------------------------------------------------------------

usage() {
  sed -n '2,40p' "$0"
}

have() { command -v "$1" >/dev/null 2>&1; }

# Redact secrets from any externally-sourced text before it is surfaced. Mirrors the C# contract
# DaprDiagnostics.ToSupportSafeDiagnostic so script output and integration-test evidence scrub the
# same categories: compact JWTs, bearer tokens, DAPR API tokens, connection secrets, private
# network addresses, non-local URLs, e-mails, and concrete tenant/user identifiers.
redact() {
  sed -E \
    -e 's/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/[redacted-jwt]/g' \
    -e 's/[Bb][Ee][Aa][Rr][Ee][Rr][[:space:]]+[A-Za-z0-9._~+/=-]{20,}/Bearer [redacted-token]/g' \
    -e 's/([Dd][Aa][Pp][Rr][_-]?[Aa][Pp][Ii][_-]?[Tt][Oo][Kk][Ee][Nn][[:space:]]*[:=][[:space:]]*)[^[:space:]]+/\1[redacted-token]/g' \
    -e 's/([Aa]ccount[Kk]ey|[Ss]hared[Aa]ccess[Kk]ey|[Pp]assword)[[:space:]]*=[[:space:]]*[^;[:space:]]+/[redacted-secret]/g' \
    -e 's/([Rr]edis[Pp]assword[^[:space:]:=]*[:=][[:space:]]*)[^{}[:space:]]+/\1{redacted-secret}/g' \
    -e 's#(redis://|amqp://|Endpoint=sb://)[^[:space:]]+#[redacted-connection]#g' \
    -e 's/\b(10\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}|172\.(1[6-9]|2[0-9]|3[01])\.[0-9]{1,3}\.[0-9]{1,3}|192\.168\.[0-9]{1,3}\.[0-9]{1,3})\b/[redacted-private-address]/g' \
    -e 's#https?://(localhost|127\.0\.0\.1)([:/][^[:space:]]*)?#__LOCALSAFE__\1\2#g' \
    -e 's#https?://[^[:space:]]+#[redacted-url]#g' \
    -e 's#__LOCALSAFE__#http://#g' \
    -e 's/[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/[redacted-email]/g' \
    -e "s/\\b([Tt]enant[Ii]d|[Tt]enant|[Uu]ser[Ii]d|[Uu]ser|sub|subject)([[:space:]]*[:=][[:space:]]*)['\"]?[A-Za-z0-9._@%+-]{3,}['\"]?/\\1=[redacted-id]/g"
}

# base64url without padding — used only for JWT assembly.
b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }

# Probe a TCP endpoint for reachability within a short timeout (bash /dev/tcp).
port_reachable() {
  local host="$1" port="$2"
  timeout 3 bash -c ">/dev/tcp/${host}/${port}" 2>/dev/null
}

# Resolve a DAPR control-plane port: explicit override wins, else first reachable candidate, else
# the preferred default so diagnostics still name the expected port.
resolve_port() {
  local override="$1"; shift
  local candidates=("$@")
  if [[ -n "${override}" ]]; then printf '%s' "${override}"; return; fi
  local c
  for c in "${candidates[@]}"; do
    if port_reachable localhost "${c}"; then printf '%s' "${c}"; return; fi
  done
  printf '%s' "${candidates[0]}"
}

# Record a finding: category, short name, status, detail. Redacts name+detail, streams a human line
# (unless --json), and appends a JSON object for the machine summary.
record() {
  local category="$1" name="$2" status="$3" detail="${4:-}"
  name="$(printf '%s' "${name}" | redact)"
  detail="$(printf '%s' "${detail}" | redact)"
  FINDINGS_JSON+=("$(jq -cn --arg c "${category}" --arg n "${name}" --arg s "${status}" --arg d "${detail}" \
    '{category:$c,name:$n,status:$s,detail:$d}')")
  if [[ "${JSON_MODE}" -eq 0 ]]; then
    printf '  [%-15s] %-26s %s\n' "${status}" "${name}" "${detail}"
  fi
}

section() {
  [[ "${JSON_MODE}" -eq 0 ]] && printf '\n[%s]\n' "$1"
  return 0
}

note() {
  [[ "${JSON_MODE}" -eq 0 ]] && printf '%s\n' "$(printf '%s' "$1" | redact)"
  return 0
}

# Raise the worst-so-far terminal state (higher exit code = higher precedence, except OK=0).
escalate() {
  local code="$1" status="$2"
  if [[ "${code}" -gt "${WORST_EXIT}" ]]; then
    WORST_EXIT="${code}"
    WORST_STATUS="${status}"
  fi
}

# --- Argument parsing ------------------------------------------------------------------------

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --sample-api-smoke|--run-smoke) RUN_SMOKE=1; shift ;;
      --json) JSON_MODE=1; shift ;;
      --tenants) CHECK_TENANTS=1; shift ;;
      --start-control-plane) START_CONTROL_PLANE=1; shift ;;
      --sample-api-url) SAMPLE_API_URL="${2:-}"; shift 2 ;;
      --eventstore-dapr-port) EVENTSTORE_DAPR_PORT="${2:-}"; EVENTSTORE_DAPR_PORT_EXPLICIT=1; shift 2 ;;
      --sample-api-dapr-port) SAMPLE_API_DAPR_PORT="${2:-}"; shift 2 ;;
      --apphost) APPHOST="${2:-}"; shift 2 ;;
      -h|--help) usage; exit "${EX_OK}" ;;
      *)
        printf 'ERROR: unknown argument "%s". Use --help for usage.\n' "$1" >&2
        return "${EX_USAGE}"
        ;;
    esac
  done
  return "${EX_OK}"
}

# --- Environment checks (AC2) ----------------------------------------------------------------

# Returns non-zero (blocked) when a required infrastructure prerequisite is missing.
check_environment() {
  section environment
  local blocked=0

  # Docker daemon.
  if have docker && timeout 15 docker version --format '{{.Server.Version}}' >/dev/null 2>&1; then
    local dv; dv="$(timeout 15 docker version --format '{{.Server.Version}}' 2>/dev/null)"
    record environment docker-daemon ok "Docker engine reachable (v${dv})"
  else
    record environment docker-daemon blocked "Docker daemon not reachable. Start Docker, then retry. See https://docs.docker.com/get-docker/"
    blocked=1
  fi

  # Aspire CLI.
  if have aspire; then
    record environment aspire-cli ok "aspire CLI on PATH"
  else
    record environment aspire-cli blocked "aspire CLI missing. Install from https://aspire.dev, then retry."
    blocked=1
  fi

  # DAPR CLI + runtime.
  if have dapr; then
    local dapr_ver; dapr_ver="$(timeout 15 dapr --version 2>/dev/null)"
    if printf '%s' "${dapr_ver}" | grep -qiE 'Runtime version:[[:space:]]*[0-9]'; then
      record environment dapr-runtime ok "DAPR CLI and runtime initialized"
    else
      record environment dapr-runtime blocked "DAPR runtime not initialized. Run 'dapr init' (or 'dapr init --slim'), then retry."
      blocked=1
    fi
  else
    record environment dapr-cli blocked "DAPR CLI missing. Install from https://docs.dapr.io/getting-started/install-dapr-cli/, then 'dapr init'."
    blocked=1
  fi

  # daprd binary (Aspire launches sidecars from this). Look on PATH and in ~/.dapr/bin.
  local daprd_bin=""
  if have daprd; then daprd_bin="$(command -v daprd)"; elif [[ -x "${HOME}/.dapr/bin/daprd" ]]; then daprd_bin="${HOME}/.dapr/bin/daprd"; fi
  if [[ -n "${daprd_bin}" ]]; then
    record environment daprd-binary ok "daprd present (${daprd_bin})"
  else
    record environment daprd-binary blocked "daprd binary not found on PATH or in ~/.dapr/bin. Run 'dapr init', then retry."
    blocked=1
  fi

  # Placement / scheduler reachability. Port-reachability is authoritative (they may run as
  # `dapr init` containers rather than local binaries).
  local placement_port scheduler_port
  placement_port="$(resolve_port "${HEXALITH_EVENTSTORE_TEST_PLACEMENT_PORT:-}" "${PLACEMENT_CANDIDATES[@]}")"
  scheduler_port="$(resolve_port "${HEXALITH_EVENTSTORE_TEST_SCHEDULER_PORT:-}" "${SCHEDULER_CANDIDATES[@]}")"

  if port_reachable localhost "${placement_port}"; then
    record environment placement ok "DAPR placement reachable on localhost:${placement_port}"
  else
    record environment placement blocked "DAPR placement NOT reachable on localhost:${placement_port}. Start it: '\$HOME/.dapr/bin/placement --port 50005 &' (or 'dapr init')."
    blocked=1
  fi

  if port_reachable localhost "${scheduler_port}"; then
    record environment scheduler ok "DAPR scheduler reachable on localhost:${scheduler_port}"
  else
    record environment scheduler blocked "DAPR scheduler NOT reachable on localhost:${scheduler_port}. Start it: '\$HOME/.dapr/bin/scheduler --port 50006 --etcd-data-dir /tmp/dapr-scheduler-data &' (or 'dapr init')."
    blocked=1
  fi

  # Optional, explicitly-gated control-plane bootstrap.
  if [[ "${blocked}" -ne 0 && "${START_CONTROL_PLANE}" -eq 1 ]]; then
    start_control_plane "${placement_port}" "${scheduler_port}"
    # Re-probe after starting.
    port_reachable localhost "${placement_port}" && port_reachable localhost "${scheduler_port}" && blocked=0
  fi

  return "${blocked}"
}

start_control_plane() {
  local placement_port="$1" scheduler_port="$2"
  local bin_dir="${HOME}/.dapr/bin"
  if ! port_reachable localhost "${placement_port}" && [[ -x "${bin_dir}/placement" ]]; then
    note "Starting placement: ${bin_dir}/placement --port ${placement_port} &"
    nohup "${bin_dir}/placement" --port "${placement_port}" >/tmp/hexalith-placement.log 2>&1 &
    disown || true
  fi
  if ! port_reachable localhost "${scheduler_port}" && [[ -x "${bin_dir}/scheduler" ]]; then
    note "Starting scheduler: ${bin_dir}/scheduler --port ${scheduler_port} --etcd-data-dir /tmp/dapr-scheduler-data &"
    nohup "${bin_dir}/scheduler" --port "${scheduler_port}" --etcd-data-dir /tmp/dapr-scheduler-data >/tmp/hexalith-scheduler.log 2>&1 &
    disown || true
  fi
  sleep 3
}

# --- Aspire topology discovery (AC3) ---------------------------------------------------------

TOPOLOGY_JSON=""   # raw `aspire describe` JSON (may be empty)

# Runs `aspire describe --format Json` against the AppHost. Returns 0 when a running topology was
# described, non-zero otherwise.
discover_topology() {
  section aspire
  local out
  out="$(timeout 45 aspire describe --format Json --non-interactive --nologo --apphost "${APPHOST}" 2>/dev/null)"
  if [[ -z "${out}" ]] || ! printf '%s' "${out}" | jq -e . >/dev/null 2>&1; then
    record aspire topology not-running "No running AppHost described. Start it: 'EnableKeycloak=false aspire run --project ${DEFAULT_APPHOST}'"
    return 1
  fi
  TOPOLOGY_JSON="${out}"

  local names
  names="$(topology_resource_names)"
  if [[ -z "${names}" ]]; then
    record aspire topology not-running "aspire describe returned no resources. Start the topology and retry."
    return 1
  fi

  # Discover sidecar DAPR HTTP ports from the topology (flags win; else describe; else defaults).
  if [[ "${EVENTSTORE_DAPR_PORT_EXPLICIT}" -eq 0 ]]; then
    local es_port; es_port="$(topology_dapr_http_port eventstore)"
    [[ -n "${es_port}" ]] && EVENTSTORE_DAPR_PORT="${es_port}"
  fi
  if [[ -z "${SAMPLE_API_DAPR_PORT}" ]]; then
    SAMPLE_API_DAPR_PORT="$(topology_dapr_http_port sample-api)"
  fi

  # eventstore + sample + sample-api are required for the generated-API proof; a missing one is a
  # real topology gap (exit 3), not a warning — otherwise a partial topology reads as "clean".
  local r
  for r in eventstore sample sample-api; do
    if printf '%s\n' "${names}" | grep -qx "${r}"; then
      local url; url="$(topology_http_url "${r}")"
      if [[ -n "${url}" ]]; then
        record aspire "resource:${r}" ok "running, http ${url}"
      elif [[ "${r}" == "sample-api" ]]; then
        record aspire "resource:${r}" fail "running but no published HTTP endpoint (generated API cannot be reached)"
        escalate "${EX_NO_TOPOLOGY}" topology-not-running
      else
        record aspire "resource:${r}" ok "running (no HTTP endpoint published)"
      fi
    else
      record aspire "resource:${r}" fail "expected resource not found in the running topology"
      escalate "${EX_NO_TOPOLOGY}" topology-not-running
    fi
  done

  # Tenants generated API is optional; absent => not-applicable (never a failure).
  if [[ "${CHECK_TENANTS}" -eq 1 ]]; then
    if printf '%s\n' "${names}" | grep -qx "tenants-api"; then
      local turl; turl="$(topology_http_url tenants-api)"
      record aspire "resource:tenants-api" ok "running, http ${turl:-<no-http-endpoint>}"
    else
      record aspire "resource:tenants-api" not-applicable "Tenants generated API host not present"
    fi
  fi
  return 0
}

# Prints one resource logical name per line. `aspire describe --format Json` returns
# { "resources": [ { "displayName": "sample-api", "name": "sample-api-<suffix>", "urls": [...],
# "environment": {...} }, ... ] } — the logical name is `displayName` (`.name` carries a random
# instance suffix), verified against a captured live schema.
topology_resource_names() {
  printf '%s' "${TOPOLOGY_JSON}" | jq -r '.resources[]?.displayName // empty' 2>/dev/null | sort -u
}

# Prints the endpoint URL for a resource, preferring the `http` URL over `https` (AC3: prefer HTTP
# for local VM smoke calls), or empty.
topology_http_url() {
  local name="$1"
  printf '%s' "${TOPOLOGY_JSON}" | jq -r --arg n "${name}" '
    .resources[]? | select((.displayName // "") == $n)
    | ( [ .urls[]? | select(.name=="http")  | .url ]
      + [ .urls[]? | select(.name=="https") | .url ] )
    | .[0] // empty' 2>/dev/null | head -n1
}

# Prints a resource's DAPR sidecar HTTP port from its Aspire environment, or empty.
topology_dapr_http_port() {
  local name="$1"
  printf '%s' "${TOPOLOGY_JSON}" | jq -r --arg n "${name}" '
    .resources[]? | select((.displayName // "") == $n)
    | .environment.DAPR_HTTP_PORT // empty' 2>/dev/null | head -n1
}

# --- DAPR sidecar diagnostics (AC4) ----------------------------------------------------------

check_sidecars() {
  section dapr
  sidecar_metadata eventstore "${EVENTSTORE_DAPR_PORT}" 1
  local sample_dapr_port="${SAMPLE_API_DAPR_PORT}"
  if [[ -n "${sample_dapr_port}" ]]; then
    sidecar_metadata sample-api "${sample_dapr_port}" 0
  else
    record dapr "sidecar:sample-api" not-applicable "sample-api DAPR HTTP port not provided; pass --sample-api-dapr-port to probe it"
  fi
}

# Query a DAPR sidecar's metadata endpoint and classify actor-runtime readiness. Never prints the
# DAPR API token or raw payloads — only bounded, categorized fields.
# args: appId daprHttpPort expectActorHost(1|0)
sidecar_metadata() {
  local app_id="$1" port="$2" expect_actor="$3"
  local meta
  if ! port_reachable localhost "${port}"; then
    record dapr "sidecar:${app_id}" fail "DAPR sidecar not reachable on localhost:${port} (sidecar missing or not started)"
    return
  fi
  meta="$(curl -sS -m 10 -H 'Accept: application/json' "http://localhost:${port}/v1.0/metadata" 2>/dev/null)"
  if [[ -z "${meta}" ]] || ! printf '%s' "${meta}" | jq -e . >/dev/null 2>&1; then
    record dapr "sidecar:${app_id}" fail "sidecar HTTP reachable on :${port} but /v1.0/metadata unavailable or unhealthy"
    return
  fi

  local reported_id host_ready placement
  reported_id="$(printf '%s' "${meta}" | jq -r '.id // "unknown"')"
  host_ready="$(printf '%s' "${meta}" | jq -r '.actorRuntime.hostReady // .actorRuntime.placement // "unknown"')"
  placement="$(printf '%s' "${meta}" | jq -r '.actorRuntime.placement // "unknown"')"

  if [[ "${expect_actor}" -eq 1 ]]; then
    if [[ "${host_ready}" == "true" ]] && printf '%s' "${placement}" | grep -qi 'connected'; then
      record dapr "sidecar:${app_id}" ok "app id ${reported_id}, metadata ok, actor hostReady=true, placement connected"
    elif printf '%s' "${placement}" | grep -qi 'disconnected'; then
      record dapr "sidecar:${app_id}" fail "actor host not ready: placement disconnected (actor calls will hang ~60s). See docs/guides/troubleshooting-dapr-actor-placement.md"
      escalate "${EX_BLOCKED}" blocked-environment
    else
      record dapr "sidecar:${app_id}" warn "app id ${reported_id}, metadata ok, actor hostReady=${host_ready}, placement=${placement}"
    fi
  else
    record dapr "sidecar:${app_id}" ok "app id ${reported_id}, metadata ok (service-invocation sidecar; no actor host)"
  fi
}

# --- Optional generated Sample API smoke (AC5, AC6) ------------------------------------------

# Mint a dev JWT (HS256). tenants/permissions are single string claims (JSON-array-encoded), as the
# EventStore claims transformation reads only the first claim of each type. The token is returned on
# stdout for capture into a variable and is NEVER logged.
mint_dev_jwt() {
  local now exp header payload h p sig
  now="$(date +%s)"
  exp="$((now + 3600))"
  header='{"alg":"HS256","typ":"JWT"}'
  payload="$(jq -cn --argjson now "${now}" --argjson exp "${exp}" \
    --arg iss "${DEV_ISSUER}" --arg aud "${DEV_AUDIENCE}" \
    --arg tenants "[\"${SMOKE_TENANT}\"]" \
    --arg perms '["commands:*","queries:*"]' \
    '{sub:"smoke-test", iss:$iss, aud:$aud, tenants:$tenants, permissions:$perms, iat:$now, nbf:$now, exp:$exp}')"
  h="$(printf '%s' "${header}" | b64url)"
  p="$(printf '%s' "${payload}" | b64url)"
  sig="$(printf '%s' "${h}.${p}" | openssl dgst -sha256 -hmac "${DEV_SIGNING_KEY}" -binary | b64url)"
  printf '%s.%s.%s' "${h}" "${p}" "${sig}"
}

run_sample_smoke() {
  section generated-api
  local base="${SAMPLE_API_URL}"
  if [[ -z "${base}" ]]; then
    base="$(topology_http_url sample-api)"
  fi
  if [[ -z "${base}" ]]; then
    record generated-api endpoint fail "Sample API base URL unknown. Provide --sample-api-url or ensure aspire describe publishes an HTTP endpoint."
    escalate "${EX_NO_TOPOLOGY}" topology-not-running
    return
  fi
  base="${base%/}"

  local jwt; jwt="$(mint_dev_jwt)"
  local cmd_url="${base}/api/${SMOKE_TENANT}/counter/${SMOKE_COUNTER}/increment"
  local qry_url="${base}/api/${SMOKE_TENANT}/counter/${SMOKE_COUNTER}"

  # The generated API host enforces UseHttpsRedirection, so an HTTP call returns 307 -> HTTPS on a
  # different port. -L follows it; --location-trusted re-sends the bearer across the port change
  # (curl otherwise drops Authorization on a cross-origin redirect); -k accepts the dev cert.
  local -a curlopts=(-sS -k -L --location-trusted -m 20)

  # --- Command: POST increment -> expect 202 + Location + Retry-After ---
  local hdr_file body_code status location retry_after
  hdr_file="$(mktemp)"
  status="$(curl "${curlopts[@]}" -o /dev/null -D "${hdr_file}" -w '%{http_code}' \
    -X POST -H "Authorization: Bearer ${jwt}" -H 'Content-Type: application/json' \
    --data "{\"counterId\":\"${SMOKE_COUNTER}\"}" "${cmd_url}" 2>/dev/null)"
  location="$(header_value "${hdr_file}" location)"
  retry_after="$(header_value "${hdr_file}" retry-after)"
  rm -f "${hdr_file}"

  if [[ "${status}" == "202" && -n "${location}" && -n "${retry_after}" ]]; then
    record generated-api command-increment ok "POST .../increment -> 202, Location present, Retry-After=${retry_after}"
  elif [[ "${status}" == "202" ]]; then
    record generated-api command-increment warn "POST .../increment -> 202 but missing Location/Retry-After header"
  elif [[ "${status}" == "404" ]]; then
    record generated-api command-increment fail "POST .../increment -> 404 (generated command route not served — product defect, not an environment blocker)"
    escalate "${EX_API_FAIL}" generated-api-failure
  else
    record generated-api command-increment fail "POST .../increment -> ${status:-<no-response>} (expected 202)"
    escalate "${EX_API_FAIL}" generated-api-failure
  fi

  # --- Query: GET -> expect 200 + ETag (poll briefly; the projection settles asynchronously after
  #     the accepted command) ---
  local q_hdr q_status etag attempt
  q_hdr="$(mktemp)"
  q_status=""
  for attempt in 1 2 3 4 5 6; do
    q_status="$(curl "${curlopts[@]}" -o /dev/null -D "${q_hdr}" -w '%{http_code}' \
      -H "Authorization: Bearer ${jwt}" "${qry_url}" 2>/dev/null)"
    [[ "${q_status}" == "200" ]] && break
    sleep 1
  done
  etag="$(header_value "${q_hdr}" etag)"
  rm -f "${q_hdr}"

  if [[ "${q_status}" == "200" ]]; then
    if [[ -n "${etag}" ]]; then
      record generated-api query-get ok "GET . -> 200 with ETag"
    else
      record generated-api query-get warn "GET . -> 200 but no ETag (projection returned none; 304 revalidation not exercisable)"
    fi
  else
    record generated-api query-get fail "GET . -> ${q_status:-<no-response>} (expected 200)"
    escalate "${EX_API_FAIL}" generated-api-failure
  fi

  # --- Query revalidation: If-None-Match -> expect 304 ---
  if [[ "${q_status}" == "200" && -n "${etag}" ]]; then
    local nm_status
    nm_status="$(curl "${curlopts[@]}" -o /dev/null -w '%{http_code}' \
      -H "Authorization: Bearer ${jwt}" -H "If-None-Match: ${etag}" "${qry_url}" 2>/dev/null)"
    if [[ "${nm_status}" == "304" ]]; then
      record generated-api query-revalidate ok "GET . (If-None-Match) -> 304"
    else
      record generated-api query-revalidate warn "GET . (If-None-Match) -> ${nm_status:-<no-response>} (expected 304)"
    fi
  fi

  check_state_evidence "${q_status}"
}

# Case-insensitive HTTP header value lookup from a curl -D dump; trims CR/whitespace.
header_value() {
  local file="$1" name="$2"
  grep -iE "^${name}:" "${file}" 2>/dev/null | tail -n1 | sed -E "s/^[^:]+:[[:space:]]*//" | tr -d '\r'
}

# --- Persisted / read-model evidence (AC6) ---------------------------------------------------

# Count state-store keys matching a pattern, without printing any values. Prefers a host redis-cli,
# else falls back to `docker exec` into a running `dapr init` redis container. Echoes a count, or the
# literal "NA" when no redis-cli is reachable.
redis_scan_count() {
  local pattern="$1"
  if have redis-cli; then
    timeout 10 redis-cli -h localhost -p "${REDIS_PORT}" --scan --pattern "${pattern}" 2>/dev/null | wc -l | tr -d ' '
    return
  fi
  if have docker; then
    local rc; rc="$(docker ps --format '{{.Names}}' 2>/dev/null | grep -i redis | head -n1)"
    if [[ -n "${rc}" ]]; then
      timeout 10 docker exec "${rc}" redis-cli --scan --pattern "${pattern}" 2>/dev/null | wc -l | tr -d ' '
      return
    fi
  fi
  printf 'NA'
}

# A successful smoke must not rely only on 202/200/304. Confirm bounded persisted evidence when the
# state store is discoverable, without dumping raw payloads.
check_state_evidence() {
  local q_status="$1"
  section state-evidence
  if ! port_reachable localhost "${REDIS_PORT}"; then
    record state-evidence redis not-applicable "Redis not reachable on localhost:${REDIS_PORT}; state-evidence-unavailable"
    return
  fi

  # Bounded: count state-store keys referencing the smoke counter. No values are read/printed.
  local key_count; key_count="$(redis_scan_count "*${SMOKE_COUNTER}*")"
  if [[ "${key_count}" == "NA" ]]; then
    record state-evidence counter-state unavailable "no redis-cli on host or in a running redis container; state-evidence-unavailable (smoke not full evidence)"
    return
  fi
  key_count="${key_count:-0}"

  if [[ "${q_status}" == "200" && "${key_count}" -ge 1 ]]; then
    record state-evidence counter-state ok "state store holds ${key_count} key(s) for the smoke counter, consistent with the accepted command"
  elif [[ "${q_status}" == "200" && "${key_count}" -eq 0 ]]; then
    record state-evidence counter-state fail "query returned 200 but state store holds 0 keys for the smoke counter (persisted state contradicts the response)"
    escalate "${EX_STATE_FAIL}" state-evidence-failure
  else
    record state-evidence counter-state unavailable "state store reachable (${key_count} matching key(s)) but the query did not return 200; not full evidence"
  fi
}

# --- Finalize --------------------------------------------------------------------------------

finalize() {
  local next_action="$1"
  next_action="$(printf '%s' "${next_action}" | redact)"

  if [[ "${JSON_MODE}" -eq 1 ]]; then
    local joined="[]"
    if [[ "${#FINDINGS_JSON[@]}" -gt 0 ]]; then
      joined="$(printf '%s\n' "${FINDINGS_JSON[@]}" | jq -s '.')"
    fi
    printf '%s' "${joined}" | jq \
      --arg status "${WORST_STATUS}" --argjson exit "${WORST_EXIT}" --arg next "${next_action}" '{
        status: $status,
        exitCode: $exit,
        environment:   [ .[] | select(.category=="environment") ],
        aspire:        [ .[] | select(.category=="aspire") ],
        dapr:          [ .[] | select(.category=="dapr") ],
        generatedApi:  [ .[] | select(.category=="generated-api") ],
        stateEvidence: [ .[] | select(.category=="state-evidence") ],
        nextAction: $next
      }'
  else
    printf '\n[next-action] %s\n' "${next_action}"
    printf 'result: %s (exit %s)\n' "${WORST_STATUS}" "${WORST_EXIT}"
  fi
  exit "${WORST_EXIT}"
}

# --- Main ------------------------------------------------------------------------------------

main() {
  if ! parse_args "$@"; then
    exit "${EX_USAGE}"
  fi

  # Minimal tool prerequisites for the preflight itself.
  local t
  for t in jq curl openssl; do
    if ! have "${t}"; then
      printf 'ERROR: required tool "%s" is not installed.\n' "${t}" >&2
      exit "${EX_USAGE}"
    fi
  done

  [[ "${JSON_MODE}" -eq 0 ]] && printf '== Generated API DAPR/Aspire Smoke Preflight ==\n'

  # 1) Environment — a required infrastructure blocker stops the run before product checks (AC2).
  if ! check_environment; then
    escalate "${EX_BLOCKED}" blocked-environment
    finalize "Resolve the blocked environment prerequisite(s) above (Docker / Aspire / DAPR / placement / scheduler), then re-run. See docs/brownfield/development-guide.md."
  fi

  # 2) Aspire topology — read-only discovery.
  if ! discover_topology; then
    escalate "${EX_NO_TOPOLOGY}" topology-not-running
    finalize "Start the local topology: 'EnableKeycloak=false aspire run --project ${DEFAULT_APPHOST}', wait for resources to be healthy, then re-run."
  fi

  # 3) DAPR sidecar diagnostics.
  check_sidecars

  # 4) Optional generated Sample API smoke + persisted evidence.
  if [[ "${RUN_SMOKE}" -eq 1 ]]; then
    run_sample_smoke
  else
    section generated-api
    record generated-api smoke not-applicable "smoke not requested; pass --sample-api-smoke to exercise the generated Sample API"
  fi

  # 5) Verdict + next action.
  case "${WORST_STATUS}" in
    ok)                     finalize "Preflight clean. Safe to treat any remaining generated-API failure as a genuine product defect." ;;
    generated-api-failure)  finalize "Generated API returned a product failure above — investigate the generated controllers/gateway, not the environment." ;;
    state-evidence-failure) finalize "Smoke responses were accepted but persisted state contradicts them — investigate the domain-service write path." ;;
    blocked-environment)    finalize "A control-plane condition was detected during diagnostics (see docs/guides/troubleshooting-dapr-actor-placement.md); restore it and re-run." ;;
    *)                      finalize "Review the findings above." ;;
  esac
}

# Only run when executed directly; sourcing (e.g. from the shell test) exposes functions without
# side effects.
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  main "$@"
fi
