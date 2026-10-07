---
title: 'Story 5.5: Internal And Domain-Service Trust Boundary'
type: 'feature'
created: '2026-09-22'
status: 'done'
baseline_commit: '253980f9eb63cc5a7d96be7c8676197e78f39d67'
route: 'dispatch'
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A plaintext DAPR caller header creates a global-administrator principal. Domain routes and projection notifications lack credentials, while Tenants trusts wire administrator flags.

**Approach:** Authenticate the application channel and independently establish the caller or publisher identity; authorize each operation and scope before binding or downstream work. Rebuild administrator context from trusted identity at every protected boundary and preserve the three anonymous probes.

**Decision:** Use `APP_API_TOKEN` to authenticate the DAPR-to-app channel and short-lived workload assertions from the existing trusted JWT issuer for caller, audience, and operation proof. Bind signed publisher provenance to projection tenant/topic. Reuse the shared JWT validation contract; do not introduce a custom signing protocol or certificate platform. Human administrator authority requires separately verified, current delegation or authorization.

## Boundaries & Constraints

**Always:** Deny missing, duplicate, conflicting, expired, wrong-audience, wrong-caller, wrong-operation, and unavailable credentials before protected work. Validate notification tenant/topic consistency. Keep only `/health`, `/alive`, and `/ready` explicitly anonymous and support-safe. Retain authorized internal and delegated-human flows and bounded, safe reason/correlation telemetry.

**Never:** Derive grants from `dapr-caller-app-id`, loopback, ACLs, mTLS alone, or wire admin flags. Commit secrets; weaken public Admin policy or DTOs; claim Stories 5.6–5.8 topology parity; or change Story 5.10 reserved-`system` provisioning.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Internal invocation | Valid workload, audience, operation, tenant | Minimal principal; permitted operation executes | Wrong/stale proof denies |
| Header forgery | App ID or admin flag without proof | No grant or downstream work | Bounded denial |
| Route override | SDK or pre-mapped `/project` | Same policy on effective endpoint | Weak override fails inventory/startup |
| Projection callback | Publisher and tenant/topic match | ETag and bounded broadcast | Forgery leaves freshness unchanged |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/Authentication/DaprInternalAuthenticationHandler.cs`, `DaprInternalAuthenticationOptions.cs`, `Extensions/ServiceCollectionExtensions.cs` — header-only admin mint. Reuse fixed-time channel-token verification from `Operations/Security/DaprAppChannelTokenMiddleware.cs`.
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`, `EventStoreDomainEventsEndpointExtensions.cs` — unsecured routes and overrides. `ServiceDefaults/Extensions.cs` marks probes anonymous.
- `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs`, `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` — body-driven ETag/broadcast and topic-producing publisher; bind publisher/topic/tenant first.
- `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs`, `src/Hexalith.EventStore/Queries/DaprDomainQueryInvoker.cs` — outbound proof; query invoker forwards bearer.
- `src/Hexalith.EventStore.Server/Commands/SubmitCommandExtensions.cs`, `src/Hexalith.EventStore.Contracts/Queries/QueryEnvelope.cs`, gateway controllers, `references/Hexalith.Tenants/src/Hexalith.Tenants.Server/` — wire flags reach active Tenants decisions; replace their authority with verified context in the owning repositories.
- `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Authentication/DaprInternalAuthenticationHandlerTests.cs`, `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs` — route, credential, and callback suites currently accept unsecured positives.
- `src/Hexalith.EventStore.Client/Effects/HttpTrustedEffectSubmitter.cs`, `src/Hexalith.EventStore.DomainService/ReminderCoordinator.cs`, `src/Hexalith.EventStore/Controllers/TrustedEffectsController.cs`, `docs/guides/typed-reminders.md` — domain services submit trusted effects to `api/v1/trusted-effects` through Dapr. They are internal callers too and need their own workload assertion.
- `references/Hexalith.Tenants/src/Hexalith.Tenants/Bootstrap/TenantBootstrapHostedService.cs`, `src/Hexalith.EventStore.AppHost/Program.cs` (`ConfigureLocalTokenIssuer`) — the global-administrator bootstrap relied on the `tenants` app-id allow-list. Keycloak mode has a delegated admin grant; symmetric mode, which the Tier-3 `AspireContractTestFixture` uses, has none.
- `src/Hexalith.EventStore/Program.cs`, `tests/Hexalith.EventStore.IntegrationTests/Helpers/DaprInvocationReadinessProbe.cs` — gateway default authorization; the Tier-3 readiness probe invokes `admin/operational-index-metadata`.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.ServiceDefaults/`, `src/Hexalith.EventStore/Authentication/`, `src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs` — add shared credential validation, startup checks, and operation policy; remove header-only admin mint.
- [x] `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` and `EventStoreDomainEventsEndpointExtensions.cs` — protect every SDK operational, subscription, and effective host-override route; retain only the three anonymous probes.
- [x] `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs` and `src/Hexalith.EventStore.Server/Projections/DaprProjectionChangeNotifier.cs` — bind authenticated publisher, topic, and tenant before freshness or SignalR effects.
- [x] `src/Hexalith.EventStore.Server/Commands/SubmitCommandExtensions.cs`, `src/Hexalith.EventStore.Contracts/Queries/QueryEnvelope.cs`, `references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Aggregates/TenantAggregate.cs`, `references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/TenantQueryHandlerBase.cs` — replace wire-admin authority with verified delegation; retain authorized behavior.
- [x] `src/Hexalith.EventStore.AppHost/Program.cs` and the three test files in Code Map — wire runtime secrets and prove real-pipeline zero-effect denials, valid flows, and route inventory; extend Tenants tests at their owning paths.
- [x] `src/Hexalith.EventStore.DomainService/`, `src/Hexalith.EventStore.Client/Effects/HttpTrustedEffectSubmitter.cs`, AppHost and realm — a domain service's trusted-effect submission carries its own assertion (caller = its Dapr app id, audience `eventstore`, operation `eventstore:trusted-effect`) from the shared issuer. Provision a workload credential for each submitting domain service, allow-list it in EventStore `Authentication:DaprInternal:AllowedCallers`, and update `typed-reminders.md`.
- [x] `src/Hexalith.EventStore.ServiceDefaults/Authentication/` issuer and realm — in authority mode, request and cache one token per (audience, operation) through authority scopes (Keycloak optional client scopes). Check the returned `aud` and `eventstore:operation` before attaching it, and grant no audience or operation by default.
- [x] `references/Hexalith.Tenants/src/Hexalith.Tenants/Bootstrap/TenantBootstrapHostedService.cs` and AppHost — bootstrap the global administrator with a delegated human credential in both AppHost modes (symmetric: a locally signed, short-lived token for `Tenants:BootstrapGlobalAdminUserId`), and document the production bootstrap credential.
- [x] Hardening, each verified in the real pipeline:
  - The gateway default policy authenticates only `JwtBearer`.
  - The inventory requires the sidecar-channel policy on `dapr/subscribe`, subscription and actor routes, and counts a fallback only if it denies anonymous.
  - An administrator-verifier failure returns a bounded 503 with reason and correlation, with no raw exception text.
  - EventStore fails startup in authority mode without `Authentication:WorkloadIssuer` client credentials.
  - A discovered token endpoint is cached only after validation.
  - `DaprInvocationReadinessProbe` targets an anonymous probe route or presents a valid assertion.
- [x] Tests:
  - The principal is rebuilt minimal for assertions carrying `sub`, `global_admin`, roles and tenant claims.
  - A real publisher→receiver round trip runs for both notifier overloads.
  - Pub/sub notifier tests pass an issuer.
  - `DaprAppChannelToken.Verify` returns `Unconfigured` outside Development.
  - Publish-mode workload-client wiring is asserted (secret parameter), and so is the `sample` JWT contract.
  - Two verifiers, one allowing and one denying, remove the flag.
  - `ProjectionChangedNotification.ToString` redacts the provenance.
  - A Keycloak-shaped token works: multi-value `aud`, operations as an array.
- [x] Docs — external IdP constraints: token lifetime within the receiver's `MaximumLifetimeSeconds`, service-account-only workload clients, one audience scope per domain service. Symmetric mode lets every key holder mint any assertion and is Development/break-glass only. Administrator authority comes from the global-administrators read model and so is eventually consistent. Domain modules keep the Dapr app-health path on an anonymous probe.

**Acceptance Criteria:**
- Given forged headers or admin flags, when a protected route runs, then no grants or downstream effects occur.
- Given valid scoped workload or human delegation, when an allowed route runs, then only its authorized operation/tenant executes with attribution.
- Given all domain-service routes and overrides, when metadata is enumerated, then non-probes require credentials and only three probes are anonymous.
- Given absent, wrong, stale, duplicate, or topic-mismatched callback proof, when delivered, then ETag and SignalR do not run.
- Given verifier failure, when a protected request arrives, then bounded denial and safe reason/correlation telemetry result.
- Given real-host and integration suites and Release build, when run, then required gates pass without weakening Admin or probes.
- Given a domain service submits a trusted effect with its own valid assertion, when it is received, then only that workload is admitted for `eventstore:trusted-effect`; without the assertion, 401 and no admission.
- Given authority mode, when EventStore invokes domain service X for operation O, then the attached assertion names audience X and grants O, and no broader token is sent.
- Given either AppHost mode, when Tenants bootstraps the configured global administrator, then a delegated human credential succeeds and that administrator's authority verifies from the read model.

## Implementation Notes

Review loop 1 re-derivation (2026-10-07). The loop-0 derivation was restored from its out-of-tree backup as the KEEP baseline, then amended for BS-A/BS-B/BS-C and P-1…P-18.

- **BS-C (authority issuer):** `JwtWorkloadAssertionIssuer` requests one client-credentials token per (audience, operation) with scopes `eventstore-audience.<audience>` and `eventstore-operation.<operation with ':' → '.'>` (`WorkloadAssertionIssuerOptions.AudienceScopePrefix`/`OperationScopePrefix`), caches per pair, and attaches/caches a token only when `aud` is exactly the requested audience and `eventstore:operation` exactly the requested operation (`GetTokenScopeFailure`). The realm `eventstore` client has no mappers, `defaultClientScopes: []`, and the seven audience/operation scopes as optional client scopes; the realm sets `attributes.CreateDefaultClientScopes=true` because declaring `clientScopes` otherwise suppresses Keycloak's built-in scopes (Keycloak 26.6 `RealmManager.importRealm` creates them before clients are imported).
- **BS-A (trusted-effect submitters):** `DomainServiceTrustedEffectAssertionHandler` + `IHttpClientBuilder.AddEventStoreTrustedEffectWorkloadAssertion()` (DomainService) attach the domain service's own assertion (`azp` = `Authentication:WorkloadIssuer:Workload`, defaulting to `EventStore:DomainService:AppId`; audience `eventstore`; operation `eventstore:trusted-effect`) to `HttpTrustedEffectSubmitter.Route` only. `AddEventStoreDomainServiceSecurity` registers the issuer. Provisioning is the reusable Aspire API `WithEventStoreTrustedEffectSubmitter(appId)` (EventStore `AllowedCallers`) and `WithEventStoreWorkloadClientCredentials(...)`; the local AppHost topology has no trusted-effect submitter, so it allow-lists nobody.
- **BS-B (bootstrap):** `TenantBootstrapCredentialProvider` (Tenants) uses the administrator's own ROPC token in authority mode (sent only when `sub` equals `Tenants:BootstrapGlobalAdminUserId`) or, in Development only, a 120 s HS256 token for that user signed with the shared `Authentication:JwtBearer` key. Without a credential the command is not sent (event 2006). Production credential documented in `security-model.md#global-administrator-bootstrap-credential` and Tenants `production-auth-readiness.md`.
- **Hardening:** gateway default policy = JwtBearer only (P-1); readiness probe → `POST /v1.0/invoke/sample/method/alive` (P-2); inventory requires `EventStoreSidecarChannel` on `dapr/subscribe`, `ITopicMetadata` routes, `/dapr/config`, `/healthz`, `/actors/*`, and counts a fallback only with `DenyAnonymousAuthorizationRequirement` (P-3/P-7); verifier failure → empty 503 + reason `administrator-verifier-unavailable` + correlation (P-4); `RequireEventStoreWorkloadIssuerClientCredentials` (ValidateOnStart) on EventStore (P-5); discovered token endpoint cached only after validation (P-6).
- **Verification evidence (2026-10-07):** Release solution build 0 warnings/0 errors. DomainService.Tests 492/492; AppHost.Tests 145/145; QueryRouting.Tests 14/14; Contracts.Tests 2264 (2 skipped, 0 failed); Server.Tests 3906 with 1 failure = the pre-existing `SecretsProtectionTests` hit on `evidence/6-1-p1r-remediation/source-candidate.diff:2590` (re-run with the new untracked files intent-added in a temporary `GIT_INDEX_FILE`: no new hit). IntegrationTests `DaprInvocationReadinessProbeTests` 12/12. Tenants (`-p:UseHexalithProjectReferences=true`, Debug): Server.Tests 825/825; IntegrationTests `CommandApiRuntimeIntegrationTests` + `DomainServiceEndpointsTests` pass except the pre-existing `Commands_endpoint_rejects_client_supplied_globalAdmin_extension_metadata`.
- **Coordinator review fixes (2026-10-07):** OQ8 fixture gives every node and its own daprd a per-node `APP_API_TOKEN` and the sample node the symmetric JwtBearer contract plus `EventStore__DomainService__AppId`; the shipped harness `/process` rebuilds administrator context (`InternalsVisibleTo` Testing.Integration, 503 on verifier failure) and `TenantsDaprTestFixture` registers `TenantsGlobalAdministratorVerifier` over a read model seeded with `test-user`; the issuer accepts numeric-string `expires_in` and has refresh/exp-clamp tests on `FakeTimeProvider`; `security-model.md` corrected (403 `operation-not-granted`, gateway `/dapr/subscribe` and actor routes not yet channel-protected, symmetric key also forges human tokens). Workload-assertion issuance, caching, and evaluation use the dedicated `WorkloadSecurityClock` (default `TimeProvider.System`, never the host's `TimeProvider`), so a host business clock such as OQ8's frozen file clock no longer expires every assertion.
- **Not run / residual risk:** no Tier-3 (Aspire + Dapr) run and no live Keycloak token exchange, so the realm scope design, the Keycloak-mode bootstrap, and the `/alive` readiness probe are proven by model/unit/TestServer tests only. Tenants `AspireTopologyTests`/`TenantsUiRouteSmokeTests` fail in this environment because the Tenants AppHost gives `eventstore`/`eventstore-admin` no `Authentication:JwtBearer` contract (Story 5.3 guard), which this story did not touch.

## Spec Change Log

- **2026-10-07 — owner decision (step-03 matrix audit, projection-callback row):** client-credentials tokens from an OIDC authority cannot carry per-notification tenant/topic bindings, and the first implementation admitted unbound provenance (`CrossProcessPath_UnboundAuthorizedPublisher_RegeneratesOnlyTheNotifiedScope`), contradicting the frozen binding decision. The single Keycloak workload token also reached every domain service, so any domain service could replay it for any tenant. Owner chose **mandatory binding + Direct transport**: the receiver denies unbound or partially bound provenance; the authority-mode issuer refuses bound requests; `Transport` defaults to `Direct` and `PubSub` is refused at startup without a binding issuer. Frozen intent unchanged. Known cost: no pub/sub redelivery for freshness in authority mode; multi-replica broadcast relies on the SignalR backplane.

- **2026-10-07 — review loop 1 → bad_spec loopback (code reverted to baseline; loop-0 derivation backed up out of tree).**
  - *Triggering findings:*
    - **BS-A (high)** — the documented `HttpTrustedEffectSubmitter` typed-reminder path sends no workload assertion, so every trusted-effect submission now gets 401.
    - **BS-B (high)** — the global-administrator bootstrap lost its authorized path once the `tenants` app-id allow-list went. Symmetric mode, which the Tier-3 contract fixture uses, gets 401, and the production delegated credential is undocumented.
    - **BS-C (medium)** — the authority-mode issuer returned one cached token carrying every audience and every operation, ignoring the requested audience and operation.
  - *Amended:* Code Map (trusted-effect submitter, Tenants bootstrap, gateway default policy, Tier-3 probe), new Execution tasks and ACs, Design Notes, and Verification.
  - *Known-bad states avoided:*
    - A domain service that cannot prove itself to the gateway.
    - A Tenants bootstrap that only works through an app-id grant.
    - One multi-audience, all-operation authority token shared across domain services.
    - Accepting unbound projection provenance (see the 2026-10-07 owner decision above).
  - *Patch-level findings folded into the new tasks:* P-1 to P-18 in the Review Triage Log.
  - *Defers pending loop exit:* D-1 to D-4.
  - **KEEP**, which worked and must survive re-derivation:
    1. **Shared ServiceDefaults/Authentication layer.**
       - `DaprAppChannelToken` does a constant-time check with statuses Valid, NotRequired (Development only), Unconfigured, Missing, Duplicate and Invalid.
       - The workload scheme is a named JwtBearer per receiver, configured through `JwtBearerAuthenticationContract` with the receiver's own audience.
       - JwtBearer events deny before validation for: a bad channel token, a duplicate or oversize assertion header, an `Authorization` header alongside an assertion, a caller outside the allow-list, and a `dapr-caller-app-id` that conflicts (used only to deny).
       - An assertion must satisfy `exp - iat <= 300 s`.
       - The principal is rebuilt minimal: `workload:<caller>`, `eventstore:workload`, operations and bindings, nothing else.
       - Denials use bounded `WorkloadAuthenticationReasons` codes and EventId 5501 with a correlation ID; verifier-unavailable maps to 503.
    2. **DomainService.**
       - Route catalog `EventStoreDomainServiceRoutes` maps each route to an operation and a policy.
       - Uses `RequireEventStoreDomainServicePolicy`, an any-workload fallback, and the sidecar-channel policy on subscribe, subscription and actor routes (stripping the framework's anonymous metadata).
       - The anonymous `/` root is removed; only `/health`, `/alive` and `/ready` stay anonymous.
       - An `IHostedLifecycleService` startup inventory fails on weak overrides or extra anonymous endpoints, and outside Development on a missing channel token, JWT contract or audience.
       - `DomainServiceAdministratorAssertions` keeps the wire admin flag only when every registered `IDomainServiceAdministratorVerifier` confirms. In Tenants, `TenantsGlobalAdministratorVerifier` checks the global-administrators read model, and the query handlers ignore the wire flag.
    3. **Outbound.**
       - An `IHttpMessageHandlerBuilderFilter` adds `DomainServiceWorkloadAssertionHandler` to every factory client; all EventStore domain-service invokers use `IHttpClientFactory`.
       - The handler strips `Authorization` and any inbound assertion.
       - `DaprDomainQueryInvoker` no longer forwards the bearer.
    4. **Gateway.**
       - `DaprInternalAuthenticationHandler` is deleted, and `DaprInternal` becomes the workload scheme (audience `eventstore`).
       - The policy scheme selects it only when the assertion header is present.
       - Trusted effects require `eventstore:trusted-effect`, and the workload comes from the rebuilt claim.
    5. **Projection provenance (owner decision).**
       - All three bindings are mandatory (`binding-missing` / `binding-mismatch`).
       - `IWorkloadAssertionIssuer.CanBindResources`; the authority-mode issuer refuses bound requests.
       - `Transport` defaults to `Direct`, and `PubSub` is refused at startup without a binding issuer.
       - `Provenance` is redacted in `PrintMembers`.
       - The realm client grants neither `projection:notify` nor the `eventstore` audience.
    6. **AppHost.**
       - A per-run `APP_API_TOKEN` is shared between each app and its own sidecar via `HexalithEventStoreAppChannelExtensions`. This is verified: CommunityToolkit copies the sidecar's `EnvironmentCallbackAnnotation`s onto the `daprd` executable.
       - The `tenants` allow-list is removed.
       - The Keycloak `eventstore` client is confidential, with a secret placeholder and a lifetime of 300 s or less.
       - Domain services get the shared JWT contract.
    7. **Tests that proved valuable.**
       - TestServer real-pipeline suites: `DomainServiceTrustBoundaryTests`, `DaprInternalAuthenticationHandlerTests` (via `WebApplicationFactory`), the ETag callback denial theory including unbound and partially bound provenance, and the route-inventory tests.
       - AppHost model tests and issuer tests.
       - Avoid literal secrets in tests; the secret scan flags them.
    8. **Verification approach.**
       - Run built test assemblies directly.
       - Build Tenants tests with `-p:UseHexalithProjectReferences=true`.
       - Pre-existing, unrelated failures: the `SecretsProtectionTests` hit on `evidence/6-1-p1r-remediation/source-candidate.diff:2590`, and Tenants `Commands_endpoint_rejects_client_supplied_globalAdmin_extension_metadata` (source `CommandsController` strips the key since `b51978dd`).

## Review Triage Log

Review loop 1 (2026-10-07). Layers: Blind Hunter (BH), Verification Gap (VG), Edge Case Hunter (EC). Routes: bad_spec → loopback; patch and defer entries are moot this loop. Patches are folded into the new tasks, and defers are appended to `deferred-work.md` when the loop exits.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-1 | medium | BS-C bad_spec | `JwtWorkloadAssertionIssuer.AcquireClientCredentialsAsync` caches one token and ignores `request.Audience` and `request.Operation`. `AuthorityContract_UsesCachedClientCredentialsToken` proves the same token goes to `sample` and `tenants`. Cross-service replay is blocked only by the `dapr-caller-app-id` conflict check. |
| BH-2 | low | P-17 patch (docs) | The AppHost hands the same HS256 key to EventStore and the domain services; any holder can mint any assertion. Development/break-glass only, but undocumented. |
| BH-3 | low | rejected | Expired or denied provenance is retried by Dapr. Pub/sub is now refused in production, so this affects non-production symmetric mode only, and the fix (DROP semantics) is more than a direct correction. |
| BH-4 | low | rejected | `GroupScope`/`Metadata` are unbound and there is no `jti` replay cache. Replay stays within the same tenant and projection for at most 300 s, in non-production pub/sub only, and the fix adds complexity. The topic binding duplicates the tenant and projection bindings, which is harmless. |
| BH-5 | low | P-1 patch | A workload principal passes plain `[Authorize]` on the gateway. Every current controller still checks `sub`, tenant or admin claims (Commands, Queries, Streams, CommandStatus, Replay, Admin filters), so this is defense in depth. Fix: `JwtBearer`-only default policy. |
| BH-6 | high | D-1 defer | `src/Hexalith.EventStore/Program.cs:47-48` maps `MapSubscribeHandler`/`MapActorsHandlers` without authorization. This predates the story and is not in its diff. The readiness sub-claim is low. |
| BH-7 | medium | D-2 defer | The sidecar-channel policy cannot tell pub/sub delivery from peer service invocation, because the receiving sidecar adds the token to every call. Domain-event and subscription routes were fully anonymous before this story, so the exposure is pre-existing and only partly mitigated. |
| BH-8 | medium | P-16 patch (docs) | `WorkloadAssertionEvaluator` rejects `exp - iat > MaximumLifetimeSeconds` (at most 900), which Entra-style IdPs cannot issue. The service-account-only and per-domain-service audience requirements are undocumented. |
| BH-9 | medium | BS-B bad_spec | The AppHost now gives `tenants` the local administrator password for the ROPC bootstrap, and no production bootstrap credential is documented. Same root cause as EC-18. |
| BH-10 | low | P-8 patch | `DaprProjectionChangeNotifierSignalRTests...PubSubTransport_DoesNotCallBroadcaster` builds the notifier without an issuer, so it takes the early return and passes vacuously. |
| BH-11 | low | P-18 patch (docs) | `TenantsGlobalAdministratorVerifier` reads an eventually consistent read model while the docs say "current". |
| BH-12 | low | rejected | An identity-provider outage appears as `assertion-missing` 401. `DaprDomainServiceInvoker` turns it into an `HttpRequestException`, which follows the same retry path as other infrastructure failures. |
| BH-13 | low | rejected | No cross-check between `ProvenanceAudience` and the `DaprInternal` audience, and a blank-only publisher list yields an empty set. Both fail closed, affect non-production pub/sub only, and the fix is a guard. |
| BH-14 | false | rejected | CommunityToolkit `DaprDistributedApplicationLifecycleHook.cs:223` copies the sidecar's `EnvironmentCallbackAnnotation`s onto the `daprd` executable, so `APP_API_TOKEN` reaches `daprd`. |
| BH-15 | low | P-15 patch | Array-valued operations are tested (the `Assertion`/`RsaAssertion` helpers). A multi-value `aud` (Keycloak-shaped) token is not. |
| VG-1 | gap | P-9 patch | No test feeds an assertion carrying `sub`, `global_admin`, roles or tenant claims and checks that the rebuilt principal has none of them. |
| VG-2 | high | BS-A bad_spec | `HttpTrustedEffectSubmitter` (the documented `typed-reminders.md:248-251` setup) sends no `X-Hexalith-Workload-Assertion`. The gateway routes it to JwtBearer, so every submission gets 401. |
| VG-3 | gap | P-10 patch | No test publishes through the real `DaprProjectionChangeNotifier` (both overloads) and delivers the result to `/projections/changed`. |
| VG-4 | low | P-8 patch | Duplicate of BH-10. |
| VG-5 | gap | P-11 patch | Deny-on-unconfigured outside Development (`DaprAppChannelToken.Verify` returning `Unconfigured`) lost its only request-level test. |
| VG-6 | gap | P-12 patch | Publish-mode `external-eventstore-workload-client-*` wiring, the parameter's secret flag, and the `sample` JWT contract are not asserted. |
| VG-7 | gap | P-13 patch | The "every verifier must confirm" rule is untested; all tests register one verifier or none. |
| VG-8 | medium | D-3 defer | The Tenants source-mode tests run in no CI lane. The fix edits `.github/workflows/ci.yml`, an OQ8 worktree-hashed gate input, so it needs coordination. |
| VG-9 | gap | P-14 patch | The `ProjectionChangedNotification.PrintMembers` redaction is untested. |
| VG-O1 | high | P-2 patch | `DaprInvocationReadinessProbe` posts to `/v1.0/invoke/sample/method/admin/operational-index-metadata` without an assertion, gets 401 indefinitely, and blocks every Tier-3 fixture. |
| VG-O2 | low | rejected | Custom `DomainServiceRegistration.MethodName` routes outside the catalog get no assertion. No configuration in the repo uses one, and the fix adds mapping complexity. |
| EC-1 | medium | D-2 defer | Duplicate of BH-7. |
| EC-2 | medium | P-3 patch | A host that pre-maps `MapSubscribeHandler` or actor handlers without the sidecar-channel policy falls through to the any-workload fallback: subscription discovery gets 401 and the inventory stays silent. Tenants did exactly this before the diff. |
| EC-3 | low | rejected | Duplicate of BH-3. |
| EC-4 | low | rejected | The audience falls back to `DAPR_APP_ID`, then `ApplicationName` (`EventStoreDomainServiceExtensions.cs:510-511`). That pre-existing identity fallback works when the app id is configured, and the fix is a guard. |
| EC-5 | low | rejected | Namespaced app ids and path-prefixed Dapr endpoints. No configuration uses them, and the fix adds parsing. |
| EC-6 | medium | BS-C bad_spec | Duplicate root cause of BH-1: a receiver missing from the authority token's audiences gets 401 with no startup signal. |
| EC-7 | low | rejected | Metadata-discovery exceptions outside the catch filter would propagate as invocation failures, which are retried. Even if real, this is low. |
| EC-8 | low | rejected | A missing `expires_in` with a token lifetime under 60 s. Keycloak always returns `expires_in`. |
| EC-9 | low | P-6 patch | `_discoveredTokenEndpoint` is assigned before the scheme check, so an invalid endpoint stays cached until restart. Fix: move the assignment after validation. |
| EC-10 | low | P-1 patch | Duplicate of BH-5. |
| EC-11 | low | rejected | Duplicate of BH-13. |
| EC-12 | medium | P-5 patch | In authority mode without `WorkloadIssuer` client credentials, EventStore starts healthy, and every domain-service call then gets 401 at runtime, signalled only by log 5511. |
| EC-13 | low | P-7 patch | `hasFallbackPolicy` is true for any fallback, including one that does not deny anonymous. |
| EC-14 | low | rejected | An `iat` above the maximum date throws, but reaching it needs a trusted signature. |
| EC-15 | false | rejected | Domain modules wire the Dapr app-health check to `/alive` (anonymous; `HexalithEventStoreDomainModuleExtensions.cs:14`). Current Dapr's actor runtime does not probe `/healthz`; its `healthEndpoint` field is unused. |
| EC-16 | low | rejected | A duplicate parameter fails loudly when the model is built; nothing calls the extension twice. |
| EC-17 | low | P-8 patch | Duplicate of BH-10. |
| EC-18 | high | BS-B bad_spec | In symmetric mode `TenantBootstrapHostedService` acquires no token, so the request falls to JwtBearer and gets 401. The admin read model stays empty, and the Tier-3 `AspireContractTestFixture` pins `EnableKeycloak=false`. |
| EC-19 | maybe-false (medium if true) | D-4 defer | Removing the wire-claim path could bring back "No visible tenants" if the global-administrators projection is empty or lagging. To settle: confirm in a Tier-3 run that the projection is populated after `BootstrapGlobalAdmin`. |
| EC-20 | high | BS-B bad_spec | Same root cause as EC-18: verified human administrators lose authority when bootstrap cannot populate the read model. |
| EC-21 | medium | BS-C bad_spec | Duplicate of BH-1. |
| EC-22 | medium | P-4 patch | An administrator-verifier exception escapes as an unhandled 500 with no bounded reason or correlation telemetry (AC: "verifier failure → bounded denial"). |
| EC-23 | low | rejected | Same root cause as BH-4 (no replay cache). AC "duplicate proof" is met for duplicated credentials. |

Review loop 2 (2026-10-07, on the loop-1 re-derivation). This round's diff excludes the spec and other sessions' files. There are no bad_spec or intent_gap entries, so patches and defers are processed normally.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R2-BH-1 | high | carried D-1 defer, plus P2-5 doc patch | Carried from BH-6: `src/Hexalith.EventStore/Program.cs:47-48` is still unprotected and predates this story. New, low: `security-model.md` says the gateway checks the channel token on actor callbacks and `/dapr/subscribe`, but only `/projections/changed` is protected. |
| R2-BH-2 | medium | carried D-2 defer | Same claim as BH-7: the sidecar-channel policy cannot tell peer service invocation from a sidecar delivery. |
| R2-BH-3 | low | carried rejected | Same claim as BH-4: `GroupScope`/`Metadata` are unbound and there is no `jti` replay cache. |
| R2-BH-4 | low | carried rejected | Same claim as BH-3: denials are retried by Dapr, and a stale dead-letter replay is denied. Pub/sub is non-production only. |
| R2-BH-5 | low | carried rejected | Same claim as BH-13: `ProvenanceAudience` is not cross-checked. |
| R2-BH-6 | low | rejected | Receivers accept a token naming several audiences or operations if it includes theirs. The authority grants no audience or operation by default, the issuer attaches only exactly-scoped tokens, and a compromised client could request each scope separately anyway. A strict single-audience check would risk rejecting valid IdP-shaped tokens. |
| R2-BH-7 | low | rejected | One `_gate` serializes cache misses across (audience, operation) pairs. Misses happen about once per pair per ~270 s, and the fix (per-pair gates and failure backoff) adds complexity for a rare slow-IdP case. |
| R2-BH-8 | low | rejected | The inventory treats explicit authorization metadata on non-catalog routes as compliant, by design. Tenants hosting the gateway controllers (`Program.cs:152` `AddApplicationPart`) predates this story, and the unregistered trusted-effect policy there was already an unregistered-scheme error before it. |
| R2-BH-9 | medium | D-5 defer | `TenantBootstrapCredentialProvider` keeps the original bootstrap's hard-coded Keycloak path (`/protocol/openid-connect/token`), ROPC, and lack of an HTTPS check. That logic predates this story; it was moved here from `TryAcquireAccessTokenAsync`. Non-Keycloak authorities cannot bootstrap. |
| R2-BH-10 | low | P2-5 patch (docs) | The symmetric key also signs human JwtBearer tokens, so a holder can impersonate human global administrators. The docs only say "any assertion". |
| R2-BH-11 | false | rejected | Keycloak reads the `CreateDefaultClientScopes` realm attribute on import (`RealmManager.isCreateDefaultClientScopes`), confirmed by the VG layer from Keycloak source. |
| R2-BH-12 | low | rejected | The `/alive` readiness probe does not exercise the authenticated path, and the gateway's channel readiness depends on `AllowedCallers`. Tier-3 tests cover the authenticated path, and the only gateway sidecar-channel route is unused under the default `Direct` transport. |
| R2-BH-13 | low | rejected | Cosmetic: test file name and brace style, a gateway-principal assertion that duplicates the domain-side test on the same evaluator, and paragraph length. |
| R2-VG-1 | medium | P2-2 patch | `DaprDomainServiceTestFixtureBase.MapProcessEndpoint` (`src/Hexalith.EventStore.Testing.Integration/DaprDomainServiceTestFixtureBase.cs:355-361`) calls `DomainServiceRequestRouter.ProcessAsync` directly. The shipped harness therefore honors forged `actor:globalAdmin` flags that the SDK host strips. |
| R2-VG-2 | gap | P2-3 patch | No issuer test advances time past `RefreshAfter` or covers the `exp`-versus-`expires_in` clamp; everything uses `TimeProvider.System`. |
| R2-VG-3 | low | D-6 defer | A configured `AllowedPublishers` list is never exercised. No in-repo publisher other than EventStore exists. |
| R2-VG-O1 | high | P2-1 patch | `Oq8PostgresqlFixture.cs:841-871` starts the sample in `Testing` with no `APP_API_TOKEN` and no JWT contract. Its startup validator then throws, so `integration.yml`'s OQ8 production-matrix test fails. |
| R2-VG-O2 | low | carried rejected | Same claim as VG-O2: non-catalog method names. |
| R2-EC-1 | low | P2-4 patch | `GetLifetime` calls `TryGetInt32` on a string-valued `expires_in` (some IdPs, such as Entra v1, send strings). It throws `InvalidOperationException`, which is caught, and every token is discarded. |
| R2-EC-2 | low | carried rejected | Same claim as EC-7: discovery exceptions outside the catch filter. |
| R2-EC-3 | low | rejected | Duplicate of R2-BH-7. |
| R2-EC-4 | low | carried rejected | Same as VG-O2. |
| R2-EC-5 | low | carried rejected | Same as EC-5: namespaced app ids. |
| R2-EC-6 | low | carried rejected | Same as BH-4. |
| R2-EC-7 | low | carried rejected | Same as BH-3. |
| R2-EC-8 | low | carried rejected | Same as BH-13. |
| R2-EC-9 | low | rejected | Dapr input-binding and job callback routes are not classified as sidecar-originated. No in-repo host maps them, and the fix needs a configurable route list. |
| R2-EC-10 | low | rejected | A host that registers `AddEventStoreDomainService` without `UseEventStoreDomainService` skips the inventory. Unsupported composition. |
| R2-EC-11 | low | rejected | Another anonymous handler on a probe path is exempt from the inventory. No host maps one, and the fix adds endpoint classification. |
| R2-EC-12 | low | P2-5 patch (docs) | `security-model.md` lists a missing operation among the 401 cases, but `WorkloadJwtBearerEvents.Forbidden` returns 403 `operation-not-granted`. |
| R2-EC-13 | low | rejected | Duplicate of R2-BH-12 (readiness). |
| R2-EC-14 | low | rejected | `AddEventStoreTrustedEffectWorkloadAssertion` without `AddEventStoreDomainService` leaves `Workload` unset. Undocumented composition; the documented setup pairs them. |
| R2-EC-15 | medium | D-5 defer | Duplicate of R2-BH-9 (hard-coded Keycloak token path). |
| R2-EC-16 | low | carried rejected | Same claim as EC-23: no replay cache. |
| R2-EC-17 | low | carried rejected | Same claim as BH-4: the topic binding is recomputed from the body. |
| R2-V-1 | high | P2-6 patch | Found by post-patch verification, not by a review layer. The OQ8 live test (`integration.yml` gate) fails: the sample denies every EventStore assertion as `assertion-expired`. The OQ8 hosting startup injects a frozen file-backed `TimeProvider`. `JwtWorkloadAssertionIssuer` stamps `iat`/`exp` (and decides cache refresh) from the host `TimeProvider`, and the receiver's evaluator checks `iat` against it too, while JwtBearer lifetime validation uses wall time. Security-token timing is coupled to a business clock. |

## Design Notes

Intent gap resolved: use the existing trusted JWT issuer. Irreversibles: none. Footprint: shared auth API, hosts, transports, tests, and Tenants. `APP_API_TOKEN` proves only the sidecar channel.

Every internal caller is a workload with its own assertion. That includes EventStore calling domain services, and domain services submitting trusted effects to EventStore.
- **Symmetric mode:** the shared Development key signs each assertion with the exact audience, operation and bindings.
- **Authority mode:** each (audience, operation) gets its own client-credentials token, selected by scopes. Tokens are never broadened.
- **Projection provenance:** follows the 2026-10-07 owner decision.
- **Human global-administrator bootstrap:** a delegated human credential in every mode; never an app-id grant.

## Verification

**Commands:**
`dotnet test` hangs in this repository. Build first, then run each test assembly directly: `dotnet tests/<Project>/bin/Release/net10.0/<Project>.dll [-class <FullName>]`.
- `dotnet test tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Release` — expected: route/pipeline gates pass.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` — expected: credential/callback/wire tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` — expected: succeeds without warnings.
- AppHost.Tests (`AppHostTrustBoundaryModelTests`, `AppHostAuthenticationModelTests`, `AspireSecurityResourceNamingTests`) and QueryRouting.Tests — expected: pass.
- Tenants, from `references/Hexalith.Tenants`: `dotnet build tests/<Project>/<Project>.csproj -c Debug -p:UseHexalithProjectReferences=true`, then run Tenants.Server.Tests and the IntegrationTests classes `CommandApiRuntimeIntegrationTests` and `DomainServiceEndpointsTests`. Expected: pass, apart from the pre-existing reserved-extension test listed in the Spec Change Log.
