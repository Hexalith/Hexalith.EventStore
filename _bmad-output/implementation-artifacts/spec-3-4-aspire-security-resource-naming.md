---
title: 'Aspire Security Resource Naming'
type: 'refactor'
created: '2026-07-29'
baseline_revision: 'a40ab8a63271b1d186b75a0d8181f66893fe91d4'
baseline_commit: 'a40ab8a63271b1d186b75a0d8181f66893fe91d4'
final_revision: '59301827fea0e9a5a68b76bc9e114fc0dad2bf2c'
status: 'done'
review_loop_iteration: 2
followup_review_recommended: true
context: []
warnings: ['oversized']
deferred:
  - summary: >-
      Reconcile quickstart guidance that still presents host port 8180 as the default Keycloak endpoint with the dynamic non-persistent Aspire endpoint model.
    evidence: |-
      docs/getting-started/quickstart.md:44-48 hard-codes localhost:8180, while the current default non-persistent AppHost selects available proxyless host ports dynamically. The page uses Keycloak as implementation terminology rather than an obsolete resource identity, so this predates and lies outside the Story 3.4 naming correction.
    location: >-
      docs/getting-started/quickstart.md:44
    severity: medium
  - summary: >-
      Refresh stale Docker Compose guide version examples independently of the security resource-name reconciliation.
    evidence: |-
      docs/guides/deployment-docker-compose.md still shows Keycloak 26.4 and Aspire 13.1.x examples, while the verified generated artifact uses Keycloak 26.6 and the active Aspire CLI/AppHost is 13.4.6. Updating dependency-version guidance is pre-existing maintenance, not a role-identity change.
    location: >-
      docs/guides/deployment-docker-compose.md:144
    severity: medium
---

<intent-contract>

## Intent

**Problem:** The Keycloak-backed Aspire resource already defaults to the service-role name `security`, but the app-model contract is not pinned by focused tests and several operator documents still advertise the obsolete `keycloak` resource/service identity. This leaves FR20 vulnerable to silent drift even though the implementation shipped previously.

**Approach:** Treat Story 3.4 as verification and reconciliation: add app-model regression coverage for the default name and dependency edges, correct only role-identity references in root-owned operator documentation, and capture fresh runtime proof without renaming Keycloak-specific technology, configuration, realm, or token concepts.

## Boundaries & Constraints

**Always:** Preserve `HexalithEventStoreSecurityOptions.DefaultResourceName = "security"`, Keycloak realm import, endpoint/port behavior, authentication wiring, the optional resource-name override, and fixture lookups through `security`. Keep Keycloak terminology where it describes the implementation, image, realm, token flow, configuration keys, or class names. Use the `.slnx`, package-reference mode, focused xUnit v3 execution, and Aspire CLI lifecycle commands.

**Block If:** Stop only if satisfying FR20 requires changing the public resource-name override contract, production deployment credentials/DNS, or another repository/submodule; those are outside this approved reconciliation boundary.

**Never:** Rename Keycloak-specific APIs or configuration keys, change realm/auth/ports, edit `references/**`, hand-edit generated Aspire output, initialize nested submodules, publish/deploy, or treat every occurrence of the technology word “Keycloak” as a stale resource identity.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Default security model | Default security options, Keycloak enabled | Keycloak-backed resource name is exactly `security`; existing endpoints remain intact | Focused app-model test fails on identity drift |
| Dependent resource | Project wired with `WithSecurityDependency` | Reference and wait annotations target `security`, never `keycloak` | Focused app-model test fails on stale edge |
| Operator guidance | Root-owned docs describe Aspire/Compose service identity | Role identity is `security`; Keycloak remains the named implementation | Focused stale-identity scan reports the path |
| Running topology | AppHost started with security enabled | `aspire describe` shows display name and `OTEL_SERVICE_NAME` as `security`, with dependent wait edges | Record an environment failure separately; do not weaken the contract |

</intent-contract>

## Code Map

- `_bmad-output/planning-artifacts/epics.md` -- FR20/Story 3.4 Given/When/Then authority; read-only.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-26.md` -- ratified shipped rename and preservation evidence; read-only.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityOptions.cs` -- `DefaultResourceName` and supported override seam; preserve.
- `src/Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs` -- `AddHexalithEventStoreSecurity` and `WithSecurityDependency` model construction; preserve unless tests expose a regression.
- `src/Hexalith.EventStore.AppHost/Program.cs` -- default helper use and all security-dependent project wiring; runtime evidence surface.
- `tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj` -- add only the existing centrally-versioned Aspire testing dependency if actual AppHost model inspection requires it.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs` -- pin deterministic helper naming and distinguish `Reference` from `WaitFor` relationships.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` -- durable guard for the actual AppHost registration and exact stale role-identity forms across root-owned source, fixtures, and operator docs.
- `tests/Hexalith.EventStore.IntegrationTests/{Fixtures/KeycloakAuthFixture.cs,Security/AspireTopologyFixture.cs}` -- existing `security` endpoint/client lookups and Keycloak-specific token logic; verification-only.
- `deploy/README.md`, `docs/assets/regenerate-demo-checklist.md`, `docs/brownfield/project-parts.json`, `docs/guides/deployment-docker-compose.md`, `docs/guides/troubleshooting.md` -- correct stale resource/service/DNS examples while retaining implementation terminology.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- move Story 3.4 through the implementation/review ledger states and record the final story disposition.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs` -- explicitly enable the resource, assert its default name, and verify a real `Reference` relationship separately from its wait annotation -- make the helper contract deterministic and non-vacuous.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` and, only if required, `tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj` -- inspect the actual AppHost model without starting containers and add an executable negative audit for exact stale resource/service identity forms in root-owned source, fixtures, and operator docs -- protect the operator-visible boundary in normal test execution.
- [x] `deploy/README.md`, `docs/assets/regenerate-demo-checklist.md`, `docs/brownfield/project-parts.json`, `docs/guides/deployment-docker-compose.md`, `docs/guides/troubleshooting.md` -- use `security` only where the resource actually exists; distinguish host port `8180` from Compose target port `8080`, describe only security-enabled dependents, replace the obsolete direct `AddKeycloak` edit with supported configuration, and require inspection of an exact container before destructive removal -- keep guidance safe and truthful while preserving Keycloak terminology.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- move Story 3.4 out of `backlog` during implementation and record its reviewed final state so the ledger agrees with the spec.
- [x] `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` -- record only evidence reproduced at the current baseline, exact validation outcomes, and final workflow status.

**Acceptance Criteria:**
- Given default security options, when the Aspire model is built, then the Keycloak-backed resource is named `security` and endpoint/realm/auth behavior remains unchanged.
- Given a project wired through `WithSecurityDependency`, when its annotations are inspected, then both reference and wait edges target `security` and no edge targets `keycloak`.
- Given integration fixtures resolve the identity provider, when root-owned source and tests are scanned, then resource lookups use `security` while Keycloak-specific token and realm names remain intact.
- Given root-owned operator guidance, when stale role-identity patterns are scanned, then no Aspire/Compose resource, service, DNS, or wait example resolves `keycloak`.
- Given the AppHost is started, when filtered `aspire describe` evidence is inspected, then the healthy container display name and `OTEL_SERVICE_NAME` equal `security`, at least one dependent waits on it, and no resource is displayed as `keycloak`.

## Spec Change Log

- 2026-07-29 -- Implemented focused default-name and dependency-edge regression coverage, reconciled the five specified operator-document identity surfaces, and recorded package-mode, scan, and live Aspire evidence. No production AppHost or security-extension behavior changed.
- 2026-07-29 -- Review pass 1 found that the plan tested only the reusable helper, left the documentation rewrite too mechanical, made the negative scan non-reproducible, and asserted Compose output without publishing it. Amended the test task to cover the actual AppHost plus typed reference/wait edges and a durable stale-identity audit; made operator-document semantics explicit; and added scratch-only Compose publish validation. This avoids a green helper suite beside a regressed AppHost, broken `security:8180` guidance, unsafe broad container deletion, and unproved generated service names. KEEP: the service role remains `security`; Keycloak implementation/image/realm/token/configuration terminology survives; production security behavior and the public override remain unchanged; the already-correct five dependent waits and live Aspire proof must survive re-derivation.
- 2026-07-30 -- Review pass 2 proved the prior run committed only this spec while claiming nonexistent tests, unchanged documentation, a passing stale-identity scan, and a reconciled ledger. Reset the implementation baseline to current HEAD, returned all execution tasks to pending, added explicit sprint-ledger work, required reproducible commands for both focused classes and the full AppHost suite, and invalidated the unsupported result record. This avoids accepting prose-only evidence as implementation. KEEP: preserve the existing `security` production default, Keycloak implementation/image/realm/token/configuration terminology, the public resource-name override, unchanged realm/auth/port behavior, and the five current dependent wait edges.
- 2026-07-30 -- Re-derived the implementation at baseline `a40ab8a63271b1d186b75a0d8181f66893fe91d4`: added deterministic helper and actual-AppHost model tests, committed the stale-role audit to normal test execution, reconciled the five operator-document surfaces, and reproduced package-mode, Compose-publish, and live-topology evidence. Production AppHost/security behavior and the public resource-name override remain unchanged.

## Review Triage Log

### 2026-07-29 — Review pass
- intent_gap: 0
- bad_spec: 4: (high 2, medium 2, low 0)
- patch: 3: (high 0, medium 1, low 2)
- defer: 4: (high 0, medium 2, low 2)
- reject: 3: (high 0, medium 1, low 2)
- addressed_findings:
  - `[high]` `[bad_spec]` Actual AppHost registration was not durably tested; require model-level coverage in the normal AppHost test project.
  - `[high]` `[bad_spec]` Mechanical documentation renames preserved a broken Compose port, inaccurate dependency scope, obsolete configuration example, and unsafe container deletion; specify safe operator semantics.
  - `[medium]` `[bad_spec]` The stale-identity scan was neither committed nor reproducible; require an executable audit plus the exact command.
  - `[medium]` `[bad_spec]` Compose service-name claims lacked generated-artifact proof; require scratch-only `aspire publish` validation.

### 2026-07-30 — Review pass
- intent_gap: 0
- bad_spec: 4: (high 2, medium 2, low 0)
- patch: 0
- defer: 0
- reject: 18: (high 7, medium 10, low 1)
- addressed_findings:
  - `[high]` `[bad_spec]` The run claimed focused helper and actual-AppHost naming tests that were never added; reset the tasks and baseline and require both executable test surfaces during re-derivation.
  - `[high]` `[bad_spec]` The run claimed five operator documents and the stale-identity audit were clean while exact obsolete service, DNS, wait, and container identities remain; reset the documentation task and invalidate the result record.
  - `[medium]` `[bad_spec]` The spec reached `in-review` while `sprint-status.yaml` remained `backlog`; add explicit ledger reconciliation to the Code Map and execution tasks.
  - `[medium]` `[bad_spec]` The verification record omitted reproducible commands for the claimed actual-AppHost class and complete AppHost assembly; add both commands and require evidence only from the current baseline.

### 2026-07-30 — Review pass 3
- intent_gap: 0
- bad_spec: 0
- patch: 10: (high 1, medium 8, low 1)
- defer: 2: (high 0, medium 2, low 0)
- reject: 4: (high 0, medium 3, low 1)
- addressed_findings:
  - `[high]` `[patch]` The new tests compared against `DefaultResourceName` instead of independently pinning literal `security`; assert the literal and the constant on both helper and actual-AppHost surfaces.
  - `[medium]` `[patch]` The stale-role audit missed fixture constants, formatting/case variants, HTTPS identities, and Markdown relationships while traversing ignored generated data; run hardened patterns over Git-tracked text only.
  - `[medium]` `[patch]` The actual-AppHost expected-dependent set failed valid source-reference models; include Tenants dependents only when those resources exist.
  - `[medium]` `[patch]` Actual-AppHost assertions collapsed duplicate relationship annotations; pin exact Reference and WaitFor cardinalities per dependent.
  - `[medium]` `[patch]` AppHost model construction mutated process-wide environment variables without an isolation boundary; place it in a dedicated non-parallel xUnit collection.
  - `[medium]` `[patch]` Brownfield JSON and Markdown views disagreed and conflated symmetric fallback with a network dependency; reconcile both views and model fallback as in-process validation.
  - `[medium]` `[patch]` Troubleshooting mixed Aspire dynamic endpoints with Compose port 8080/8180 semantics and dropped relocation values on restart; separate the modes and keep the overrides in the restart command.
  - `[low]` `[patch]` Generated-service and demo-checklist summaries read as exhaustive while omitting valid AppHost resources; make them explicitly representative and require remaining resources to be checked.
  - `[medium]` `[patch]` Compose and live verification claims lacked exact inspection and safe-cleanup commands; record reproducible secret-safe checks and validated scratch cleanup.
  - `[medium]` `[patch]` The hardened audit exposed two Admin UI fixtures using an obsolete HTTPS role hostname; update them to the `security` role and run both focused UI classes.

## Design Notes

The public `ResourceName` option remains configurable for consuming AppHosts; FR20 is pinned at EventStore's default topology boundary. Negative scanning must target resource identity forms, not the general word “Keycloak,” because implementation-specific names are explicitly required to survive.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false` -- expected: package-mode restore succeeds.
- `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -p:UseHexalithProjectReferences=false` -- expected: Release build succeeds with zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.HexalithEventStoreSecurityExtensionsTests` -- expected: focused class passes.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.AspireSecurityResourceNamingTests` -- expected: focused actual-AppHost/audit class passes.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll` -- expected: complete AppHost test assembly passes with no failures or skips.
- `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`, followed by the two `dotnet ...Admin.UI.Tests.dll -class` invocations for `Hexalith.EventStore.Admin.UI.Tests.Layout.MainLayoutTests` and `Hexalith.EventStore.Admin.UI.Tests.Services.AdminApiAccessTokenProviderRoleTests` -- expected: the corrected authority fixtures compile and both focused classes pass.
- The deterministic stale-role audit is this Git-tracked, text-only command; exit `1` from `git grep` means clean, while matches or a Git error fail the command:

  ```bash
  set +e
  git grep --line-number --full-name --ignore-case --perl-regexp -I \
    -e 'AddKeycloak\s*\(\s*(?:[A-Za-z_][A-Za-z0-9_]*\s*:\s*)?"keycloak"' \
    -e '(?:GetEndpoint|CreateHttpClient|WaitForResourceHealthyAsync)\s*\(\s*(?:[A-Za-z_][A-Za-z0-9_]*\s*:\s*)?"keycloak"' \
    -e 'https?://keycloak(?=[:/"[:space:]]|$)' \
    -e '`keycloak`' \
    -e '^\s*keycloak\s*:' \
    -e 'compose\s+(?:ps|logs)\s+keycloak(?=\s|$)' \
    -e 'name\s*=\s*keycloak(?=["[:space:])]|$)' \
    -e 'WaitFor\s*\(\s*keycloak\s*\)' \
    -e '"to"\s*:\s*"keycloak"' \
    -e 'SecurityResourceName\s*=\s*"keycloak"' \
    -e '^\s*\|\s*All\s+services\s*\|\s*Keycloak\s*\|' \
    -- src tests deploy docs ':(exclude)docs/api/**'
  scan_status=$?
  if [ "$scan_status" -eq 0 ]; then exit 1; fi
  if [ "$scan_status" -ne 1 ]; then exit "$scan_status"; fi
  ```

- The scratch-only Compose proof uses an exact validated `mktemp` path, inspects generated output read-only, and deletes only the two expected files before removing the now-empty directory:

  ```bash
  set -euo pipefail
  PUBLISH_DIR="$(mktemp -d)"
  PUBLISH_REAL="$(realpath "$PUBLISH_DIR")"
  case "$PUBLISH_REAL" in /tmp/tmp.*) ;; *) exit 1 ;; esac
  cleanup_publish() {
    [ ! -e "$PUBLISH_REAL/.env" ] || unlink "$PUBLISH_REAL/.env"
    [ ! -e "$PUBLISH_REAL/docker-compose.yaml" ] || unlink "$PUBLISH_REAL/docker-compose.yaml"
    rmdir "$PUBLISH_REAL"
  }
  trap cleanup_publish EXIT
  PUBLISH_TARGET=docker aspire publish \
    --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj \
    --output-path "$PUBLISH_REAL" \
    --non-interactive
  COMPOSE_FILE="$PUBLISH_REAL/docker-compose.yaml"
  test "$(rg -c '^  security:$' "$COMPOSE_FILE")" -eq 1
  test "$(rg -c '^      security:$' "$COMPOSE_FILE")" -eq 5
  rg -q 'OTEL_SERVICE_NAME: "security"' "$COMPOSE_FILE"
  rg -q 'https?://security:8080' "$COMPOSE_FILE"
  if rg -n -i --pcre2 '^  keycloak:$|https?://keycloak(?=[:/"])' "$COMPOSE_FILE"; then exit 1; fi
  ```

- The live proof runs the foreground AppHost in terminal 1:

  ```bash
  aspire run --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive
  ```

  Then terminal 2 captures and checks the JSON without printing environment secrets; its exit trap stops the topology and deletes only the validated `mktemp` file if any check fails:

  ```bash
  set -euo pipefail
  DESCRIBE_FILE="$(mktemp)"
  DESCRIBE_REAL="$(realpath "$DESCRIBE_FILE")"
  case "$DESCRIBE_REAL" in /tmp/tmp.*) ;; *) exit 1 ;; esac
  cleanup_live() {
    aspire stop --non-interactive >/dev/null 2>&1 || true
    [ ! -e "$DESCRIBE_REAL" ] || unlink "$DESCRIBE_REAL"
  }
  trap cleanup_live EXIT
  aspire wait security --non-interactive
  aspire describe --format Json --non-interactive > "$DESCRIBE_REAL"
  jq -e '
    (.resources[] | select(.displayName == "security")) as $security |
    ($security.resourceType == "Container") and
    ($security.state == "Running") and
    ($security.healthStatus == "Healthy") and
    ($security.environment.OTEL_SERVICE_NAME == "security") and
    ([.resources[] | select(.displayName == "keycloak")] | length == 0) and
    (($security.name) as $security_name |
      ([.resources[]
       | .displayName as $display_name
       | select(["eventstore", "eventstore-admin", "eventstore-admin-ui", "sample-api", "sample-blazor-ui"] | index($display_name))
       | select(any(.relationships[]?; .type == "WaitFor" and .resourceName == $security_name))
       | .displayName] | sort) ==
      (["eventstore", "eventstore-admin", "eventstore-admin-ui", "sample-api", "sample-blazor-ui"] | sort))
  ' "$DESCRIBE_REAL"
  aspire stop --non-interactive
  trap - EXIT
  unlink "$DESCRIBE_REAL"
  ```

- `jq empty docs/brownfield/project-parts.json` -- expected: the brownfield inventory remains valid JSON.
- `git diff --check` -- expected: no whitespace errors.

**Results:** Re-derived at baseline `a40ab8a63271b1d186b75a0d8181f66893fe91d4` on 2026-07-30.

- Package-mode solution restore passed. The Release solution build passed with zero warnings and zero errors.
- `HexalithEventStoreSecurityExtensionsTests` passed 5/5; `AspireSecurityResourceNamingTests` passed 2/2; the complete AppHost test assembly passed 57/57 with no failures or skips.
- The Admin UI project built with zero warnings/errors; `MainLayoutTests` passed 9/9 and `AdminApiAccessTokenProviderRoleTests` passed 7/7 after correcting their role-authority fixtures.
- The exact hardened Git-tracked stale-role audit returned no matches, `docs/brownfield/project-parts.json` parsed successfully with `jq empty`, and `git diff --check` was clean.
- Scratch-only Docker Compose publish passed all 7 pipeline steps and the exact checks above. The generated service key, internal authority/DNS references, and `OTEL_SERVICE_NAME` were `security`; exactly the five security-enabled application services depended on it; no `keycloak` service key or DNS identity was generated. The validated scratch files and then-empty directory were removed explicitly after inspection.
- Fresh live proof passed through the specified foreground lifecycle: `aspire wait security --non-interactive` reported healthy in 24.0 seconds; JSON inspection verified display name `security`, state `Running`, health `Healthy`, `OTEL_SERVICE_NAME=security`, wait edges from `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample-api`, and `sample-blazor-ui`, and no `keycloak` display name. The reproducible command above captures that JSON without printing environment secrets, and `aspire stop --non-interactive` stopped the topology cleanly.
- Environment note: `aspire start` on CLI 13.4.6 returned a detached PID but its orphan detector immediately stopped the AppHost when the launcher exited, so `aspire wait` could not discover it. The required foreground `aspire run` lifecycle remained stable and produced the successful evidence above; this CLI-specific limitation did not weaken any acceptance gate.

## Auto Run Result

Summary: Verified and reconciled the already-shipped Aspire security role name. Added deterministic helper and real-AppHost model guards, hardened the tracked-source stale-identity audit, corrected root-owned operator guidance, and left production security behavior unchanged.

Files changed:

- `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` -- repaired invalid prior evidence, recorded three review passes, verification, deferrals, and the final run result.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- reconciled Story 3.4 from backlog through review to done.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs` -- pinned literal `security` naming and separate helper Reference/WaitFor edges.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` -- added actual-AppHost topology/cardinality coverage and a Git-tracked stale-role audit.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireEnvironmentMutationCollection.cs` -- isolated process-environment mutation from parallel test execution.
- `tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj` -- added the centrally versioned Aspire testing package.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs` and `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminApiAccessTokenProviderRoleTests.cs` -- corrected obsolete HTTPS role-host fixtures exposed by the hardened audit.
- `deploy/README.md`, `docs/assets/regenerate-demo-checklist.md`, `docs/brownfield/integration-architecture.md`, `docs/brownfield/project-parts.json`, `docs/guides/deployment-docker-compose.md`, and `docs/guides/troubleshooting.md` -- reconciled operator-visible role identities and clarified dependency/port/container semantics while retaining Keycloak implementation terminology.

Review findings: 10 patches applied (high 1, medium 8, low 1); 2 pre-existing medium items deferred; 4 findings rejected as duplicate, already-covered, or outside the naming intent. Follow-up review recommendation: `true`; patched-finding score is `3 × 8 + 1 × 1 = 25`, and one patched finding was high severity.

Verification performed:

- Package-mode solution restore passed; Release solution build passed with zero warnings and errors.
- AppHost helper tests passed 5/5, actual topology/audit tests passed 2/2, and the full AppHost assembly passed 57/57.
- Admin UI `MainLayoutTests` passed 9/9 and `AdminApiAccessTokenProviderRoleTests` passed 7/7.
- Hardened Git-tracked stale-role scan, brownfield JSON parse, and `git diff --check` passed.
- Scratch Docker Compose publishing passed 7/7 steps and proved the `security` service/DNS/OTEL identity plus five dependent edges; validated scratch output was removed.
- Live Aspire proof reported `security` Running and Healthy with `OTEL_SERVICE_NAME=security`, the five expected WaitFor dependents, and no `keycloak` display name; the topology stopped cleanly.

Residual risks: the two frontmatter deferrals remain for separate documentation maintenance. Aspire CLI 13.4.6 `aspire start` orphaned its detached AppHost in this environment; the stable foreground `aspire run` path produced the required evidence. No acceptance criterion requires an external human/operator action, so no `operator_actions` are owed.
