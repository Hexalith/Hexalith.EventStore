#!/usr/bin/env bash
# Shell validation for scripts/generated-api-smoke-preflight.sh (Story 3.8, AC8).
#
# Sources the preflight so its functions are testable without side effects (the preflight only runs
# main when executed directly), then exercises: argument parsing, read-only defaults, support-safe
# redaction, AppHost token-command handoff, control-plane port resolution, and HTTP header
# parsing. Run standalone: `bash scripts/tests/generated-api-smoke-preflight.test.sh`.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PREFLIGHT="${SCRIPT_DIR}/generated-api-smoke-preflight.sh"

PASS=0
FAIL=0

ok()   { PASS=$((PASS + 1)); printf '  ok   - %s\n' "$1"; }
fail() { FAIL=$((FAIL + 1)); printf '  FAIL - %s\n' "$1"; }

assert_contains() {
  local haystack="$1" needle="$2" msg="$3"
  if printf '%s' "${haystack}" | grep -qF -- "${needle}"; then ok "${msg}"; else fail "${msg} (missing: ${needle})"; fi
}
assert_not_contains() {
  local haystack="$1" needle="$2" msg="$3"
  if printf '%s' "${haystack}" | grep -qF -- "${needle}"; then fail "${msg} (leaked: ${needle})"; else ok "${msg}"; fi
}
assert_eq() {
  local actual="$1" expected="$2" msg="$3"
  if [[ "${actual}" == "${expected}" ]]; then ok "${msg}"; else fail "${msg} (got '${actual}', want '${expected}')"; fi
}

printf '# generated-api-smoke-preflight shell validation\n'

# --- Executed-mode behavior (before sourcing) ------------------------------------------------

printf '## executed-mode argument handling\n'

"${PREFLIGHT}" --bogus >/dev/null 2>&1
assert_eq "$?" "1" "unknown argument exits 1 (usage error)"

help_out="$("${PREFLIGHT}" --help 2>&1)"
assert_contains "${help_out}" "READ-ONLY diagnostic gate" "--help prints the read-only intent"
assert_contains "${help_out}" "--sample-api-smoke" "--help documents the smoke flag"
assert_contains "${help_out}" "blocked environment" "--help documents exit categories"

# --- Source for function-level tests ---------------------------------------------------------

# shellcheck disable=SC1090
source "${PREFLIGHT}"

printf '## read-only defaults\n'
assert_eq "${RUN_SMOKE}" "0" "smoke is off by default (read-only)"
assert_eq "${START_CONTROL_PLANE}" "0" "control-plane start is off by default (read-only)"
assert_eq "${JSON_MODE}" "0" "human output by default"

printf '## parse_args\n'
RUN_SMOKE=0; JSON_MODE=0; CHECK_TENANTS=0; SAMPLE_API_URL=""
parse_args --sample-api-smoke --json --tenants --sample-api-url http://localhost:5016
assert_eq "${RUN_SMOKE}" "1" "parse_args sets RUN_SMOKE"
assert_eq "${JSON_MODE}" "1" "parse_args sets JSON_MODE"
assert_eq "${CHECK_TENANTS}" "1" "parse_args sets CHECK_TENANTS"
assert_eq "${SAMPLE_API_URL}" "http://localhost:5016" "parse_args captures --sample-api-url"

printf '## sample API destination ownership\n'
TOPOLOGY_JSON='{"resources":[{"displayName":"sample-api","urls":[{"name":"http","url":"http://localhost:5016"},{"name":"https","url":"https://localhost:7016/"}]}]}'
SAMPLE_API_URL=""
assert_eq "$(resolve_sample_api_smoke_base)" "https://localhost:7016" "described HTTPS endpoint is selected directly"
SAMPLE_API_URL="https://localhost:7016/"
assert_eq "$(resolve_sample_api_smoke_base)" "https://localhost:7016" "matching explicit described endpoint is accepted"
SAMPLE_API_URL="https://localhost:7999"
if resolve_sample_api_smoke_base >/dev/null; then
  fail "unowned loopback explicit endpoint fails closed"
else
  ok "unowned loopback explicit endpoint fails closed"
fi
SAMPLE_API_URL="https://example.test:7016"
if resolve_sample_api_smoke_base >/dev/null; then
  fail "non-loopback explicit endpoint fails closed"
else
  ok "non-loopback explicit endpoint fails closed"
fi
SAMPLE_API_URL=""

printf '## resolve_port\n'
assert_eq "$(resolve_port 12345 6050 50005)" "12345" "explicit override wins"
# No candidate reachable -> preferred default (first candidate).
assert_eq "$(resolve_port '' 59997 59998)" "59997" "falls back to first candidate when none reachable"

printf '## redaction (support-safe)\n'
# File-based fixture avoids shell-quoting artifacts; single-quoted id + double-quoted id + dapr token.
fixture="$(mktemp)"
opaque_token="$(openssl rand -hex 24)"
fake_jwt="$(printf 'eyJ%s.%s.%s' "$(openssl rand -hex 8)" "$(openssl rand -hex 12)" "$(openssl rand -hex 18)")"
secret_password="$(openssl rand -hex 12)"
credential_line="$(printf 'Pass%sword=%s' '' "${secret_password}")"
account_key="$(openssl rand -hex 12)"
printf '%s\n' "Bearer ${opaque_token} ${fake_jwt} ${credential_line} AccountKey=${account_key} redis://cache.local:6379 10.1.2.3 issuer=https://identity.internal.example/realms/hexalith tenantId='tenant-prod-001' userId=\"real-user\" email=real-user@example.com dapr-api-token=${opaque_token} http://localhost:8080/api/tenant-a/counter/counter-1" \
  >"${fixture}"
redacted="$(redact <"${fixture}")"
rm -f "${fixture}"

assert_contains "${redacted}" "Bearer [redacted-token]" "authorization value redacted"
assert_contains "${redacted}" "[redacted-jwt]" "compact JWT redacted"
assert_contains "${redacted}" "[redacted-secret]" "password/account key redacted"
assert_contains "${redacted}" "[redacted-connection]" "redis connection string redacted"
assert_contains "${redacted}" "[redacted-private-address]" "private address redacted"
assert_contains "${redacted}" "[redacted-url]" "non-local issuer URL redacted"
assert_contains "${redacted}" "[redacted-email]" "email redacted"
assert_contains "${redacted}" "tenantId=[redacted-id]" "concrete tenant id redacted"
assert_contains "${redacted}" "userId=[redacted-id]" "concrete user id redacted"
assert_contains "${redacted}" "dapr-api-token=[redacted-token]" "dapr api token redacted"
# Localhost URL and dev fixtures survive.
assert_contains "${redacted}" "http://localhost:8080/api/tenant-a/counter/counter-1" "localhost URL + dev fixtures preserved"
# Nothing sensitive leaks.
assert_not_contains "${redacted}" "tenant-prod-001" "no concrete tenant value leaks"
assert_not_contains "${redacted}" "real-user" "no user value leaks"
assert_not_contains "${redacted}" "${opaque_token}" "no generated opaque token leaks"
assert_not_contains "${redacted}" "${fake_jwt}" "no generated compact JWT leaks"
assert_not_contains "${redacted}" "${secret_password}" "no generated password leaks"
assert_not_contains "${redacted}" "${account_key}" "no generated account key leaks"
assert_not_contains "${redacted}" "identity.internal.example" "no internal issuer host leaks"

printf '## AppHost token command handoff\n'
mock_token="$(openssl rand -hex 8).$(openssl rand -hex 12).$(openssl rand -hex 16)"
mock_args="$(mktemp)"
aspire() {
  printf '%s\n' "$*" >"${mock_args}"
  printf '%s' "${mock_token}"
}
jwt="$(request_apphost_smoke_token)"
assert_eq "${jwt}" "${mock_token}" "token-only AppHost command result is captured"
assert_contains "$(cat "${mock_args}")" "resource sample-api issue-smoke-token" "sample-api owns the token command"
assert_contains "$(cat "${mock_args}")" "--apphost ${APPHOST}" "token command targets the configured AppHost"
rm -f "${mock_args}"

aspire() { printf 'diagnostic\n%s' "${mock_token}"; }
if request_apphost_smoke_token >/dev/null; then
  fail "non-token command stdout fails closed"
else
  ok "non-token command stdout fails closed"
fi
aspire() { return 1; }
if request_apphost_smoke_token >/dev/null; then
  fail "failed AppHost token command fails closed"
else
  ok "failed AppHost token command fails closed"
fi

printf '## header_value parsing\n'
hf="$(mktemp)"
printf 'HTTP/1.1 202 Accepted\r\nLocation: /api/v1/commands/status/ABC\r\nRetry-After: 1\r\nETag: "W/xyz"\r\n\r\n' >"${hf}"
assert_eq "$(header_value "${hf}" location)" "/api/v1/commands/status/ABC" "case-insensitive Location header parsed"
assert_eq "$(header_value "${hf}" retry-after)" "1" "Retry-After header parsed"
rm -f "${hf}"

printf '## header_value ignores redirect (3xx) headers\n'
# A curl -D dump across an HTTP->HTTPS 307 contains BOTH responses' headers. header_value must read
# only the final (202) block, so the 307's Location does not mask a genuine "202 with no Location".
hf_redirect="$(mktemp)"
printf 'HTTP/1.1 307 Temporary Redirect\r\nLocation: https://localhost:7443/api/x\r\n\r\nHTTP/1.1 202 Accepted\r\nRetry-After: 1\r\n\r\n' >"${hf_redirect}"
assert_eq "$(header_value "${hf_redirect}" location)" "" "a 202 with no Location is not masked by the 307 redirect's Location"
assert_eq "$(header_value "${hf_redirect}" retry-after)" "1" "final-response Retry-After parsed across a redirect"
rm -f "${hf_redirect}"

printf '## parse_args rejects a trailing value flag with no value (no infinite loop)\n'
( parse_args --sample-api-url ) 2>/dev/null; assert_eq "$?" "1" "trailing --sample-api-url with no value is a usage error"
( parse_args --apphost ) 2>/dev/null;        assert_eq "$?" "1" "trailing --apphost with no value is a usage error"

printf '## redaction preserves URL scheme for localhost\n'
assert_eq "$(printf 'see https://localhost:17017/dashboard' | redact)" "see https://localhost:17017/dashboard" "https://localhost keeps its scheme (not downgraded to http)"
assert_eq "$(printf 'api http://localhost:8080/x' | redact)" "api http://localhost:8080/x" "http://localhost is preserved as-is"

# The escalation/classification contract is the tool's core value (env-vs-product). Exercise it
# directly — the isolated-helper tests above would pass even if this routing were broken.
reset_verdict() { WORST_EXIT="${EX_OK}"; WORST_STATUS="ok"; CONTROL_PLANE_BLOCKED=0; FINDINGS_JSON=(); JSON_MODE=1; }

printf '## smoke failure classification (environment/auth vs product)\n'
reset_verdict; classify_smoke_failure command-increment 202 404
assert_eq "${WORST_STATUS}" "generated-api-failure" "404 route-not-served is a product defect"
assert_eq "${WORST_EXIT}" "4" "404 maps to exit 4"
reset_verdict; classify_smoke_failure command-increment 202 403
assert_eq "${WORST_STATUS}" "blocked-environment" "403 is an auth/access-control (environment) condition, not a product defect"
assert_eq "${WORST_EXIT}" "2" "403 maps to exit 2 (blocked environment)"
reset_verdict; classify_smoke_failure command-increment 202 000
assert_eq "${WORST_STATUS}" "blocked-environment" "an unreachable host (000) is an environment condition"
reset_verdict; classify_smoke_failure query-get 200 500
assert_eq "${WORST_STATUS}" "generated-api-failure" "an unexpected served 5xx is a product/gateway defect"

printf '## escalation precedence: an environment blocker is not out-precedenced by a later env-classified smoke failure\n'
reset_verdict; escalate "${EX_BLOCKED}" blocked-environment; classify_smoke_failure command-increment 202 000
assert_eq "${WORST_STATUS}" "blocked-environment" "environment blocker stays the verdict when the smoke also fails for an environment reason"
assert_eq "${WORST_EXIT}" "2" "verdict exit stays 2, not overridden to a product-failure code"
JSON_MODE=0

printf '\n# summary: %d passed, %d failed\n' "${PASS}" "${FAIL}"
[[ "${FAIL}" -eq 0 ]]
