---
title: 'Aspire Security Resource Naming'
type: 'refactor'
created: '2026-07-29'
baseline_revision: 'a40ab8a63271b1d186b75a0d8181f66893fe91d4'
baseline_commit: 'a40ab8a63271b1d186b75a0d8181f66893fe91d4'
final_revision: '1f59b3f09fe7137c849fd52516e727f7c70a297b'
status: 'done'
review_loop_iteration: 0
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
  - summary: >-
      No CI lane executes the source-mode (`HEXALITH_TENANTS_SOURCE`) AppHost topology, so the Tenants security dependents asserted by the new naming test are never exercised anywhere.
    evidence: |-
      `.github/workflows/ci.yml` runs `tests/Hexalith.EventStore.AppHost.Tests` only in package mode, where `tenants`/`tenants-api` do not exist; the single source-mode job filters to `FullyQualifiedName~TenantsApiLaunchSettingsTests`, which excludes `AspireSecurityResourceNamingTests`. The pre-existing condition is the narrow source-mode filter, not the new test: its `if (builder.Resources.Any(... "tenants" ...))` guard silently shrinks the expected set rather than failing. Removing `tenants.WithJwtBearerSecurity(security)` from `Program.cs` leaves both CI jobs green.
    location: >-
      .github/workflows/ci.yml:99
    severity: medium
  - summary: >-
      Documentation and agent guidance invoke `aspire run --project`, which the pinned Aspire CLI 13.4.6 does not accept.
    evidence: |-
      `aspire run --help` and `aspire publish --help` on 13.4.6 list only `--apphost`. `--project` remains at roughly fifteen sites including `deploy/README.md`, `docs/getting-started/quickstart.md`, `docs/getting-started/first-domain-service.md`, `docs/brownfield/development-guide.md`, `docs/guides/deployment-*.md`, `docs/guides/troubleshooting.md:552`, and `.claude/agents/aspire.md:62`. This is CLI-flag drift across the documentation set, unrelated to the security role identity, and nothing in the repository verifies documented CLI invocations.
    location: >-
      docs/getting-started/quickstart.md:29
    severity: medium
  - summary: >-
      New evidence bearing on the first deferred entry -- the premise that the default non-persistent AppHost picks Keycloak host ports dynamically is contradicted by the implementation.
    evidence: |-
      `KeycloakFastStartPorts.ResolveDynamic` calls `FindAvailablePort(8180, ...)` / `FindAvailablePort(8543, ...)`, and `HexalithEventStoreSecurityExtensions` binds both endpoints proxyless in the non-persistent branch as well; its own source comment states the default "prefers 8180/8543 and moves forward when either port is busy". Quickstart's `localhost:8180` is therefore correct for the default topology in the ordinary case. Recorded as new information only -- the orchestrator owns entry one's status and resolution. Review pass 4 corrected the same wrong premise where this run had introduced it into `docs/guides/troubleshooting.md`.
    location: >-
      src/Hexalith.EventStore.Aspire/KeycloakFastStartPorts.cs:72
    severity: medium
  - summary: >-
      New evidence only, no status change -- the `aspire run --project` drift also exists in production source and at more sites than the earlier entry counts.
    evidence: |-
      `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:150` emits `aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` inside a runtime diagnostic message, so the flag the pinned CLI 13.4.6 rejects reaches users from shipped code, not only from documentation; a documentation-only sweep would miss it. `docs/guides/configuration-reference.md:597,600,610` additionally use `dotnet run --project src/Hexalith.EventStore.AppHost`, and `scripts/generated-api-smoke-preflight.sh` passes `--project` at three sites, so the tracked total exceeds the "roughly fifteen sites" already recorded. Recorded as new information only -- the orchestrator owns the earlier entry's status and resolution.
    location: >-
      src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:150
    severity: medium
  - summary: >-
      Building the real AppHost model inside a unit test mutates a machine-wide temp directory, so the AppHost suite can disturb a concurrently running `aspire run`.
    evidence: |-
      `AspireSecurityResourceNamingTests` builds the actual model through `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`, which executes `src/Hexalith.EventStore.AppHost/Program.cs` top to bottom, including `ResolveIsolatedDaprComponentPath` at `Program.cs:249-266`. That helper deletes every `*.yaml` under `Path.GetTempPath()/hexalith-eventstore-dapr-components/statestore` and re-copies the component; the path is isolated from the repository, not per process, and `AspireEnvironmentMutationCollection` serialises only in-process environment mutation. The end state is byte-identical, so the window is narrow, but a sidecar starting inside it can fail to read its state-store component. Pre-existing AppHost behaviour; the new exposure is that a normal test run now executes it. A durable fix needs a per-instance subdirectory or an env-var override in production AppHost source, outside this story's zero-production-source boundary.
    location: >-
      src/Hexalith.EventStore.AppHost/Program.cs:257
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
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` -- durable guard for the actual AppHost registration (enabled and disabled) and exact stale role-identity forms across root-owned source, fixtures, and operator/agent docs.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireEnvironmentMutationCollection.cs` -- non-parallel xUnit collection isolating process-wide environment mutation during AppHost model construction.
- `tests/Hexalith.EventStore.IntegrationTests/{Fixtures/KeycloakAuthFixture.cs,Security/AspireTopologyFixture.cs}` -- existing `security` endpoint/client lookups and Keycloak-specific token logic; verification-only.
- `tests/Hexalith.EventStore.Admin.UI.Tests/{Layout/MainLayoutTests.cs,Services/AdminApiAccessTokenProviderRoleTests.cs}` -- authority fixtures carrying an obsolete HTTPS role hostname; correct to the `security` role.
- `deploy/README.md`, `docs/assets/regenerate-demo-checklist.md`, `docs/brownfield/integration-architecture.md`, `docs/brownfield/project-parts.json`, `docs/guides/deployment-docker-compose.md`, `docs/guides/troubleshooting.md`, `.claude/agents/aspire.md` -- correct stale resource/service/DNS examples while retaining implementation terminology.
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

### 2026-07-30 — Review pass 4
- intent_gap: 0
- bad_spec: 0
- patch: 9: (high 0, medium 6, low 3)
- defer: 3: (high 0, medium 3, low 0)
- reject: 8: (high 0, medium 5, low 3)
- addressed_findings:
  - `[medium]` `[patch]` Troubleshooting claimed the default non-persistent mode picks host ports "dynamically" and that only persistent mode uses 8180/8543, contradicting `KeycloakFastStartPorts.ResolveDynamic` (prefers 8180/8543, walks forward only on collision) and deleting previously accurate guidance; restored the correct behaviour in both the Port Conflicts cause and the `aspire run` note.
  - `[medium]` `[patch]` The Compose guide renamed its service key to `security` but left its own Mermaid topology node and OIDC edge as `Keycloak[...:8180]`, so the diagram contradicted the service definition two hunks below; reconciled the node, the edge, and the accessible text description.
  - `[medium]` `[patch]` `.claude/agents/aspire.md` still listed resource `` `keycloak` `` in its "Topology (app model in Program.cs)" table — the only stale role identity left repo-wide, and the file that configures the Aspire agent; renamed to `security` (Keycloak-backed).
  - `[medium]` `[patch]` The audit pathspec `src tests deploy docs` structurally could not reach the file above; widened it to the root-owned agent/CI/sample/script/tool trees plus root markdown, excluding submodules, BMAD artifacts, generated API docs, and the generated `CHANGELOG.md`.
  - `[medium]` `[patch]` `git grep` exit 1 made "no stale identity" indistinguishable from "scanned nothing", so a mis-resolved repository root or dead pathspec would pass vacuously; added a positive control asserted before the negative result.
  - `[medium]` `[patch]` No coverage existed for the `EnableKeycloak=false` branch the reconciled documentation now asserts; added AppHost-model and helper tests proving no `security` resource and no Reference/WaitFor edges, plus a test pinning the public `ResourceName` override.
  - `[low]` `[patch]` The audit shelled out to git with sequential `ReadToEnd` calls before `WaitForExit` (pipe-deadlock ordering) and a null-check that could never fire when git is absent from PATH; switched to concurrent async draining and explicit `Win32Exception` handling.
  - `[low]` `[patch]` The deliberate `"key" + "cloak"` self-evasion was unexplained, so any maintainer inlining the literal would turn the audit permanently red; extracted it behind a documented helper.
  - `[low]` `[patch]` `AddHexalithEventStoreSecurity_WhenDefault_...` forced `EnableKeycloak=true`, so a test named for the default no longer exercised it; added a test that clears the switch entirely, keeping the hermetic tests as they are.

### 2026-07-30 — Review pass 5
- intent_gap: 0
- bad_spec: 0
- patch: 11: (high 0, medium 5, low 6)
- defer: 3: (high 0, medium 3, low 0)
- reject: 13: (high 0, medium 6, low 7)
- addressed_findings:
  - `[medium]` `[patch]` The Port Conflicts cause claimed persistent mode "fails fast on a collision", but `KeycloakFastStartPorts.Resolve` validates only value range, distinctness and the reserved `8080` and never probes availability -- an occupied pinned port wedges the container in `Created` instead. Corrected the claim and cross-linked the fast-start section.
  - `[medium]` `[patch]` Three further sites in the same file still described the default mode as random/dynamic-port (`*random* host ports`, `(non-persistent, dynamic-port) topology`, `uses dynamic ports and never has this problem`), contradicting the pass-4 correction fifteen lines above; reconciled all three to the real preferred-port-then-walk-forward model.
  - `[medium]` `[patch]` "A conflict on the `security` ports needs no action in the default mode" over-stated the guarantee: the probe is a build-time loopback bind released before the container starts, so a port claimed in between still wedges it. Qualified the advice and restored a remedy.
  - `[medium]` `[patch]` The audit's positive control could not detect a re-narrowed pathspec -- `HexalithEventStoreSecurityOptions` occurs only under `src`/`tests`, so reverting to `src tests deploy docs` would have kept it green while re-opening `.claude`. Added a coverage control requiring every audited tree plus root Markdown to contribute tracked files; proven non-vacuous by mutation.
  - `[medium]` `[patch]` The default-mode preferred-port behaviour the reconciled guidance now asserts was pinned by no test (`ResolveDynamic` had zero coverage; the `WhenDefault` helper test only checks `> 0` and `!= 8080`). Added two walk-forward-from-the-preferred-port facts that reject an ephemeral-port implementation.
  - `[low]` `[patch]` The `git grep` audit scanned tracked build/restore output (`**/.artifacts/**`, `**/bin/**`, `**/obj/**`, `**/*.lscache`) that the superseded `rg` command excluded and that carries the implementation name; restored those excludes for the same reason `CHANGELOG.md` is excluded.
  - `[low]` `[patch]` `AddHexalithEventStoreSecurity_WhenPersistent_...` never pinned `EnableKeycloak`, so an ambient `EnableKeycloak=false` turned its null-forgiving dereference into a `NullReferenceException`; pinned it like its siblings.
  - `[low]` `[patch]` The Compose Mermaid `security` node dropped the host port and showed `:8080`, colliding with the Command API node while the external client edge needs `8180`; relabelled as `host :8180 → :8080`.
  - `[low]` `[patch]` Two pass-3 deferrals never reached `deferred-work.md`; appended the still-valid one (stale Compose dependency-version examples) as a new ledger entry. The other -- quickstart's `localhost:8180` -- was deliberately not propagated because pass 4's own evidence refutes its premise; the orchestrator owns that entry's status in this spec's frontmatter.
  - `[low]` `[patch]` The per-dependent Reference cardinalities were undocumented magic numbers, so an added realm-URL-valued environment variable would read as identity drift; documented the derivation (one `WithReference` plus one per realm-URL `ReferenceExpression`).
  - `[low]` `[patch]` The spec's documented audit shell block lacked the positive control the committed test has and omitted the generated-output excludes, advertising a weaker check than the gate; added the control, the excludes, and a note naming the test as authoritative.

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
- `dotnet tests/Hexalith.EventStore.AppHost.Tests/bin/Release/net10.0/Hexalith.EventStore.AppHost.Tests.dll -class Hexalith.EventStore.AppHost.Tests.Configuration.KeycloakFastStartPortsTests` -- expected: the fast-start port class passes, including the default-mode preferred-port walk-forward behaviour the operator guidance asserts.
- The deterministic stale-role audit is this Git-tracked, text-only command; exit `1` from `git grep` means clean, while matches or a Git error fail the command. The committed `AspireSecurityResourceNamingTests` audit is the authoritative gate: it runs the same patterns *plus* two controls this shell form cannot express -- a pattern control (the scan reached tracked text) and a coverage control (every audited tree still contributes tracked files, so re-narrowing the pathspec fails loudly). Run the positive control alongside the negative scan so a mis-resolved root or dead pathspec cannot pass vacuously:

  ```bash
  # Positive control: must exit 0 with matches before the negative result is meaningful.
  git grep -l --ignore-case -I -e 'HexalithEventStoreSecurityOptions' \
    -- .agents .claude .codex .github .opencode deploy docs perf samples scripts src tests tools \
       ':(glob,top)*.md' || exit 1

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
    -- .agents .claude .codex .github .opencode deploy docs perf samples scripts src tests tools \
       ':(glob,top)*.md' ':(exclude)docs/api/**' ':(exclude,glob,top)CHANGELOG.md' \
       ':(exclude,glob)**/.artifacts/**' ':(exclude,glob)**/bin/**' ':(exclude,glob)**/obj/**' \
       ':(exclude,glob)**/*.lscache'
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
- `HexalithEventStoreSecurityExtensionsTests` passed 8/8; `AspireSecurityResourceNamingTests` passed 3/3; the complete AppHost test assembly passed 61/61 with no failures or skips (5/5, 2/2 and 57/57 before the review-pass-4 coverage patches).
- The Admin UI project built with zero warnings/errors; `MainLayoutTests` passed 9/9 and `AdminApiAccessTokenProviderRoleTests` passed 7/7 after correcting their role-authority fixtures.
- The exact hardened Git-tracked stale-role audit returned no matches, `docs/brownfield/project-parts.json` parsed successfully with `jq empty`, and `git diff --check` was clean.
- Scratch-only Docker Compose publish passed all 7 pipeline steps and the exact checks above. The generated service key, internal authority/DNS references, and `OTEL_SERVICE_NAME` were `security`; exactly the five security-enabled application services depended on it; no `keycloak` service key or DNS identity was generated. The validated scratch files and then-empty directory were removed explicitly after inspection.
- Fresh live proof passed through the specified foreground lifecycle: `aspire wait security --non-interactive` reported healthy in 24.0 seconds; JSON inspection verified display name `security`, state `Running`, health `Healthy`, `OTEL_SERVICE_NAME=security`, wait edges from `eventstore`, `eventstore-admin`, `eventstore-admin-ui`, `sample-api`, and `sample-blazor-ui`, and no `keycloak` display name. The reproducible command above captures that JSON without printing environment secrets, and `aspire stop --non-interactive` stopped the topology cleanly.
- Environment note: `aspire start` on CLI 13.4.6 returned a detached PID but its orphan detector immediately stopped the AppHost when the launcher exited, so `aspire wait` could not discover it. The required foreground `aspire run` lifecycle remained stable and produced the successful evidence above; this CLI-specific limitation did not weaken any acceptance gate.

**Review pass 4 re-verification (2026-07-30), after the nine patches:**

- Package-mode restore and the Release solution build passed with zero warnings and zero errors.
- `HexalithEventStoreSecurityExtensionsTests` 8/8, `AspireSecurityResourceNamingTests` 3/3, full AppHost assembly 61/61 — no failures, no skips. Admin UI `MainLayoutTests` 9/9 and `AdminApiAccessTokenProviderRoleTests` 7/7 after a zero-warning Release build.
- The widened stale-role audit returned no matches (`git grep` exit 1). Its non-vacuity was proven by mutation: appending `` `keycloak` `` to `.claude/settings.json` — a path only reachable through the widened pathspec — failed the test with `[".claude/settings.json:77:`keycloak`"]`, and the file was restored clean.
- `jq empty docs/brownfield/project-parts.json` passed; `git diff --check` reported no whitespace errors.
- The scratch-only Docker Compose proof was re-run at the current tree and passed every check: one `security:` service key, exactly five dependents, `OTEL_SERVICE_NAME: "security"`, `http://security:8080` internal DNS, and no `keycloak` service key or DNS identity. Scratch output was removed by its validated cleanup trap.
- The live-topology gate was not re-executed. `git diff --stat <baseline> -- src samples` is empty for the whole story: no production source changed at any point, so the recorded live `aspire describe` evidence still describes the current model, and the re-run Compose proof independently confirms the runtime-facing identity at HEAD.

**Review pass 5 re-verification (2026-07-30), after the eleven patches:**

- Package-mode restore and the Release solution build passed with zero warnings and zero errors.
- `HexalithEventStoreSecurityExtensionsTests` 8/8, `AspireSecurityResourceNamingTests` 3/3, `KeycloakFastStartPortsTests` 20/20 (18 before this pass), full AppHost assembly **63/63** — no failures, no skips. Admin UI `MainLayoutTests` 9/9 and `AdminApiAccessTokenProviderRoleTests` 7/7 after a zero-warning Release build.
- The new pathspec coverage control was proven non-vacuous by mutation: removing `".claude"` from `_auditPathspec` failed the audit with `The stale-identity audit no longer reaches '.claude'; obsolete role identities there would pass unnoticed.` The file was restored and the suite re-run green.
- The documented shell audit ran with its new positive control: the control matched three tracked files and the negative scan returned `git grep` exit 1 over the widened pathspec including the new generated-output excludes.
- `jq empty docs/brownfield/project-parts.json` passed. `python3 scripts/check-deferred-work.py` exited 0 with the three appended story-3.4 entries classified `dw6-unclassified-legacy-advisory`, as the existing section is.
- The scratch-only Docker Compose proof was re-run at the patched tree and passed every check: one `security:` service key, exactly five dependents, `OTEL_SERVICE_NAME: "security"`, `http://security:8080` internal DNS, and no `keycloak` service key or DNS identity. Scratch output was removed by its validated cleanup trap.
- The live-topology gate was again not re-executed, on the same evidence: `git diff --stat a40ab8a6 -- src samples` remains empty at the end of this pass, so no runtime-facing behaviour changed. `ResolveDynamic`'s preferred-port behaviour, previously only asserted in prose, is now pinned by two executable facts.

## Auto Run Result

Status: done
Blocking condition: none

Summary: Fifth review pass over the shipped Story 3.4 reconciliation. The Aspire service role remains `security` and zero production source changed at any point in the story. This pass repaired the operator guidance the previous pass had left self-contradictory — the same file described the default port model three different ways and credited persistent mode with a fail-fast it does not have — closed the structural hole in the new stale-identity guard (its positive control could not see a re-narrowed pathspec, the exact regression it was written for), and pinned the default-mode port behaviour the documentation asserts.

Files changed in this pass:

- `docs/guides/troubleshooting.md` — corrected the persistent-mode "fails fast on a collision" claim (validation covers port *values*, never availability), reconciled the three remaining random/dynamic-port statements with the preferred-then-walk-forward model, and qualified the "needs no action" advice with the build-time-probe race.
- `docs/guides/deployment-docker-compose.md` — Mermaid `security` node relabelled `host :8180 → :8080` so the externally reachable port is visible and does not read as a second service on `:8080`.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/AspireSecurityResourceNamingTests.cs` — added a pathspec coverage control (every audited tree plus root Markdown must contribute tracked files), excluded tracked generated build/restore output, documented the derived Reference cardinalities, and factored the git plumbing into a shared `RunGitAsync`.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/KeycloakFastStartPortsTests.cs` — two `ResolveDynamic` facts pinning the preferred-port walk-forward behaviour against an ephemeral-port implementation.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/HexalithEventStoreSecurityExtensionsTests.cs` — the persistent-mode test now pins `EnableKeycloak`, so an ambient `EnableKeycloak=false` cannot turn it into a `NullReferenceException`.
- `_bmad-output/implementation-artifacts/deferred-work.md` — three new append-only entries (the un-propagated pass-3 Compose-version deferral, plus two new-evidence records).
- `_bmad-output/implementation-artifacts/spec-3-4-aspire-security-resource-naming.md` — pass-5 triage log, two new deferrals, the strengthened audit command, and this result.

Review findings: 11 patches applied (high 0, medium 5, low 6); 3 items deferred; 13 rejected; 0 bad_spec; 0 intent_gap. Follow-up review recommendation: `true` — no patched finding was high severity, but the patched score is `3 × 5 + 1 × 6 = 21`.

Verification performed: package-mode restore and zero-warning Release solution build; AppHost assembly 63/63 with focused classes 8/8, 3/3 and 20/20; Admin UI 9/9 and 7/7; coverage control proven non-vacuous by mutation; documented audit clean with its positive control matching; `jq empty` on the brownfield inventory; deferred-work checker exit 0; scratch-only Compose publish re-run proving one `security:` key, five dependents, `OTEL_SERVICE_NAME: "security"` and `http://security:8080`; `git diff --check` clean.

Residual risks: the live `aspire describe` gate remains baseline prose evidence — justified because `git diff a40ab8a6 -- src samples` is empty for the entire story and the Compose proof was re-run at the patched tree, but it has not been re-executed since. The two new `ResolveDynamic` facts occupy a loopback port; they tolerate an already-busy `8180`/`8543` by treating it as the same precondition, but they assume the walk forward stays within 100 ports. Three deferrals were opened: the shared-temp DAPR component mutation that AppHost-model tests now trigger, the `--project` drift reaching production source in `AdminUIServiceExtensions.cs:150`, and the stale Compose dependency-version examples. Deferred entry one (quickstart `localhost:8180`) was deliberately not propagated to the durable ledger because pass 4's evidence refutes its premise; its status remains the orchestrator's to resolve. No acceptance criterion requires an external human or operator action, so no `operator_actions` are owed.
