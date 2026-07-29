---
title: 'Aspire Security Resource Naming'
type: 'refactor'
created: '2026-07-29'
baseline_revision: 'b8b0eafb6d153009857957d100d67ed62e9b77d6'
baseline_commit: 'b8b0eafb6d153009857957d100d67ed62e9b77d6'
status: 'in-review'
review_loop_iteration: 1
followup_review_recommended: false
context: []
warnings: ['oversized']
deferred: []
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

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs` -- explicitly enable the resource, assert its default name, and verify a real `Reference` relationship separately from its wait annotation -- make the helper contract deterministic and non-vacuous.
- [x] `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` and, only if required, `tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj` -- inspect the actual AppHost model without starting containers and add an executable negative audit for exact stale resource/service identity forms in root-owned source, fixtures, and operator docs -- protect the operator-visible boundary in normal test execution.
- [x] `deploy/README.md`, `docs/assets/regenerate-demo-checklist.md`, `docs/brownfield/project-parts.json`, `docs/guides/deployment-docker-compose.md`, `docs/guides/troubleshooting.md` -- use `security` only where the resource actually exists; distinguish host port `8180` from Compose target port `8080`, describe only security-enabled dependents, replace the obsolete direct `AddKeycloak` edit with supported configuration, and require inspection of an exact container before destructive removal -- keep guidance safe and truthful while preserving Keycloak terminology.
- [x] `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` -- record implementation evidence, exact validation outcomes, and final workflow status.

**Acceptance Criteria:**
- Given default security options, when the Aspire model is built, then the Keycloak-backed resource is named `security` and endpoint/realm/auth behavior remains unchanged.
- Given a project wired through `WithSecurityDependency`, when its annotations are inspected, then both reference and wait edges target `security` and no edge targets `keycloak`.
- Given integration fixtures resolve the identity provider, when root-owned source and tests are scanned, then resource lookups use `security` while Keycloak-specific token and realm names remain intact.
- Given root-owned operator guidance, when stale role-identity patterns are scanned, then no Aspire/Compose resource, service, DNS, or wait example resolves `keycloak`.
- Given the AppHost is started, when filtered `aspire describe` evidence is inspected, then the healthy container display name and `OTEL_SERVICE_NAME` equal `security`, at least one dependent waits on it, and no resource is displayed as `keycloak`.

## Spec Change Log

- 2026-07-29 -- Implemented focused default-name and dependency-edge regression coverage, reconciled the five specified operator-document identity surfaces, and recorded package-mode, scan, and live Aspire evidence. No production AppHost or security-extension behavior changed.
- 2026-07-29 -- Review pass 1 found that the plan tested only the reusable helper, left the documentation rewrite too mechanical, made the negative scan non-reproducible, and asserted Compose output without publishing it. Amended the test task to cover the actual AppHost plus typed reference/wait edges and a durable stale-identity audit; made operator-document semantics explicit; and added scratch-only Compose publish validation. This avoids a green helper suite beside a regressed AppHost, broken `security:8180` guidance, unsafe broad container deletion, and unproved generated service names. KEEP: the service role remains `security`; Keycloak implementation/image/realm/token/configuration terminology survives; production security behavior and the public override remain unchanged; the already-correct five dependent waits and live Aspire proof must survive re-derivation.

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

## Design Notes

The public `ResourceName` option remains configurable for consuming AppHosts; FR20 is pinned at EventStore's default topology boundary. Negative scanning must target resource identity forms, not the general word “Keycloak,” because implementation-specific names are explicitly required to survive.

## Verification

**Commands:**
- `dotnet restore Hexalith.EventStore.slnx -p:UseHexalithProjectReferences=false` -- expected: package-mode restore succeeds.
- `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore -p:UseHexalithProjectReferences=false` -- expected: Release build succeeds with zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.HexalithEventStoreSecurityExtensionsTests` -- expected: focused class passes.
- `if rg -n -e 'AddKeycloak\("keycloak"' -e 'GetEndpoint\("keycloak"' -e 'CreateHttpClient\("keycloak"' -e 'WaitForResourceHealthyAsync\("keycloak"' -e 'http://keycloak' -e '`keycloak`' -e '^[[:space:]]*keycloak:' -e 'compose (ps|logs) keycloak' -e 'name=keycloak' -e 'WaitFor\(keycloak\)' -e '"to": "keycloak"' src tests deploy docs --glob '!docs/api/**' --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/*.lscache'; then exit 1; fi` -- expected: no obsolete role identity form is found.
- `PUBLISH_DIR="$(mktemp -d)" && PUBLISH_TARGET=docker aspire publish --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --output-path "$PUBLISH_DIR" --non-interactive` followed by read-only inspection of the generated Compose service and authority entries -- expected: the Keycloak-backed service key/DNS identity is `security`; remove the temporary directory only after validating it is the `mktemp` path created by this command.
- `aspire run --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive`, `aspire wait security --non-interactive`, filtered `aspire describe --format Json`, then `aspire stop --non-interactive` -- expected: healthy `security` resource, `OTEL_SERVICE_NAME=security`, dependent wait edges, no `keycloak` display name, and clean shutdown.
- `git diff --check` -- expected: no whitespace errors.

**Results (2026-07-29):**

- PASS -- package-mode restore completed with all projects up to date.
- PASS -- the package-mode Release solution build completed with 0 warnings and 0 errors.
- PASS -- the focused `HexalithEventStoreSecurityExtensionsTests` class ran 6 tests with 0 failures, including exact default naming plus separately typed reference and wait assertions.
- PASS -- the focused `AspireSecurityResourceNamingTests` class ran 2 tests with 0 failures. It built the actual AppHost model without starting resources, verified the exact five package-mode reference/wait dependents, and executed the committed stale role-identity audit.
- PASS -- the complete `Hexalith.EventStore.AppHost.Tests` assembly ran 58 tests with 0 failures or skips.
- PASS -- the exact stale-identity scan across `src`, `tests`, `deploy`, and `docs` returned no obsolete resource lookup, Compose service/DNS/command, container-name, or `WaitFor(keycloak)` match. Keycloak technology, image, realm, token, and configuration terminology remains intact.
- PASS -- scratch-only Docker publishing completed all 7 pipeline steps. Generated Compose output used service key `security`, image `quay.io/keycloak/keycloak:26.6`, target port `8080`, `OTEL_SERVICE_NAME=security`, `http://security:8080` authority/service-discovery values, and no `keycloak` service or DNS identity. The validated temporary directory was deleted after inspection.
- PASS -- live `aspire wait security` completed healthy. Filtered `aspire describe --format Json` reported display name `security`, `OTEL_SERVICE_NAME=security`, image `quay.io/keycloak/keycloak:26.6`, unchanged HTTP/management endpoints, dependent waits from `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample-api`, and `sample-blazor-ui`, and no `keycloak` display name.
- PASS -- `docs/brownfield/project-parts.json` parses with `jq`, `git diff --check` reports no errors, `aspire stop --non-interactive` completed successfully, and the final Aspire process listing is empty.
- Workflow status -- implementation and acceptance verification complete; ready for adversarial review.
