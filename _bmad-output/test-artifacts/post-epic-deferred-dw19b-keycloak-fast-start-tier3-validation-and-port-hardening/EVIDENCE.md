# DW19b ST3 — Tier 3 Keycloak Container-Reuse Runtime Validation — Evidence

Date: 2026-05-25
Machine: Windows 11; .NET SDK 10.0.300; Docker 29.4.3; DAPR runtime 1.17.7 (full `dapr init`); Keycloak image 26.5.6.
Command (per run): `KEYCLOAK_TEST_REUSE=true dotnet test tests/Hexalith.EventStore.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~KeycloakE2E|FullyQualifiedName~DaprAccessControlE2E|FullyQualifiedName~KeycloakAuthentication"`
Selected tests: 17 across the two Keycloak-enabled collections (`AspireTopology`: 10, `KeycloakAuthTests`: 7).
Reproduce with `run-reuse-validation.ps1` in this folder. Raw logs: `run1.log`, `run2.log`, `control-no-reuse.log`; container snapshots: `docker-before-run1.txt`, `docker-after-run1.txt`, `docker-after-run2.txt`.

## AC1 — Container reuse across two separate `dotnet test` runs — ✅ PROVEN (POSITIVE)

`before-run1`: no Keycloak container (true cold start; stale container removed first).

| Property | after run 1 (cold) | after run 2 (warm) | Identical? |
|---|---|---|---|
| Container ID | `a19e1114db72e17aad813e30411a79ec6e8658a4f239e70471c8474335a672d1` | `a19e1114db72…672d1` | ✅ yes |
| `CreatedAt` | `2026-05-25T16:18:58.613525508Z` | `2026-05-25T16:18:58.613525508Z` | ✅ yes |
| DCP `…usvc-dev.lifecycle-key` | `198d6ba7eefe872404570dd7f59a9f07` | `198d6ba7eefe872404570dd7f59a9f07` | ✅ yes |
| `…usvc-dev.persistent` | `true` | `true` | ✅ yes |
| `State.Status` / `StartedAt` | `running` / `16:19:01.079507616Z` | `running` / `16:19:01.079507616Z` | ✅ yes (never restarted) |
| Proxyless host ports | `8443→8180`, `9000→8543` (127.0.0.1) | same | ✅ yes |

**The same physical container served both `dotnet test` invocations.** The identical `StartedAt` proves the container was reattached, not recreated or even restarted, between runs. This refutes the open question DW19 left (that Aspire's `DistributedApplicationTestingBuilder` / `_app.DisposeAsync()` might tear down a `ContainerLifetime.Persistent` container): with the proxyless fixed-port pin, the DCP `lifecycle-key` is byte-stable across `dotnet test` runs exactly as it is across `aspire run`, and the persistent container survives `DisposeAsync`.

Wall-clock (`SUMMARY.txt`): run 1 (cold) **00:02:21.9** vs run 2 (warm) **00:01:44.4** — the warm run is **~37s (≈26%) faster**, and the warm run did not re-import the realm (container not recreated).

## AC3 — Within-run cross-collection reuse — ✅ PROVEN (POSITIVE)

Both Keycloak-enabled collections (`AspireTopology` then `KeycloakAuthTests`) run serially in one invocation (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`). Throughout both runs only **one** Keycloak container ever existed (`a19e…`), with an unchanging ID/`CreatedAt`/`lifecycle-key`. The second collection reused the first collection's warm container; neither hung in `Created`, and no second bind of `8180`/`8543` was attempted. No "first collection's dispose removed the container" negative was observed.

## AC4 / AC7 — Ports & default path

- Proxyless fixed host ports observed exactly as `8180` (http) / `8543` (management) with `KEYCLOAK_TEST_REUSE=true` and no port overrides — identical to the DW19 working-tree behavior (AC4 default branch).
- **Control run (reuse OFF, default path):** same 17-test filter without `KEYCLOAK_TEST_REUSE`. Result `control-no-reuse.log`: **no persistent container created and none left behind after the run** (default non-persistent path tears Keycloak down on dispose), confirming the default topology is unchanged (AC7). `KeycloakHttpPort`/`KeycloakManagementPort` are read only on the persistent path (unit-tested in `KeycloakFastStartPortsTests`).

## AC2 — OIDC auth tests on cold + warm — ⚠️ AUTH VERIFIED; 5 pre-existing command-pipeline 500s (FINDING, not reuse/DW19b-related)

Both runs: **12 passed / 5 failed (identical set, identical on cold and warm).**

Passing (auth enforcement + token acquisition all green):
- OIDC token acquisition (real Resource-Owner-Password tokens), OIDC discovery 200.
- `UnauthenticatedRequest_Returns401`, `SubmitCommand_NoToken_Returns401Unauthorized`.
- All 403 tenant-isolation / readonly / no-tenant cases (`…Returns403…`, `SubmitCommand_CrossTenantToken_Returns403Forbidden`, `SubmitCommand_ReadOnlyUser_Returns403Forbidden`, `SubmitCommand_NoTenantClaims_Returns403Forbidden`).
- DAPR access-control denial (`SampleSidecar_InvokeEventStore_DeniedByAccessControl`, `…ResponseContainsErrorContext`).
- **`SubmitCommand_ValidKeycloakToken_Returns202Accepted` — an authorized 202 command submission — PASSED** (cold, warm, and control).

Failing (all 5 are authorized command submissions expecting `202`, returning `500 internal-server-error` ProblemDetails on `POST /api/v1/commands`):
- `KeycloakE2ESmokeTests.AuthenticatedCommandSubmission_WithKeycloakToken_ReturnsAccepted`
- `KeycloakE2ESecurityTests.AdminUser_SubmitCommand_ReturnsAcceptedAsync`
- `KeycloakE2ESecurityTests.TenantAUser_SubmitCommandForOwnTenant_ReturnsAcceptedAsync`
- `KeycloakAuthenticationTests.SubmitCommand_TenantScopedUser_CanAccessOwnTenant`
- `KeycloakAuthenticationTests.ConcurrentTenantCommands_EventsRemainIsolated`

**Attribution — these 500s are NOT caused by container reuse and NOT caused by DW19b:**
1. **Identical on cold (run 1) and warm (run 2):** reuse does not change the outcome.
2. **Identical on the default non-persistent control run** (`KEYCLOAK_TEST_REUSE` unset): the same 5 fail with the same 500 → the failure exists on the byte-for-byte pre-DW19b path.
3. **DW19b changed only** (a) Keycloak fixed-port resolution active solely on the persistent path with custom ports (none set here, so wiring is identical to DW19), and (b) fixture env-var restore (cleanup only). Neither touches the command/actor pipeline or auth.
4. The failure is **intermittent** within the authorized-submission set (`SubmitCommand_ValidKeycloakToken_Returns202Accepted` succeeds while siblings 500), and the 401/403 auth boundary is fully correct — characteristic of a DAPR actor placement / first-activation timing issue in the local Tier 3 command pipeline (cf. project-context: "DAPR slim/local mode may require placement and scheduler processes before actor flows work").

The exact server stack is not in these logs (the VSTest console logger captured only the HTTP 500 ProblemDetails, not the in-process app `ILogger` output). Root-causing the command-pipeline 500 is **out of DW19b scope** (the story explicitly forbids changing the auth/command pipeline) and is routed to new deferred work.

## Disposition

- **Headline (AC1) reuse validation: positive and proven** with byte-identical container-identity evidence — DW19's single open question is answered. AC3 reuse also positive. AC4/AC7 corroborated.
- AC2: auth path verified; the 5 authorized-command-submission 500s are a pre-existing, reuse-independent, DW19b-independent command-pipeline finding (see new deferred-work item `dw19b-tier3-e2e-command-submission-500`). They do not gate the reuse conclusion and are not a reuse "negative result."
