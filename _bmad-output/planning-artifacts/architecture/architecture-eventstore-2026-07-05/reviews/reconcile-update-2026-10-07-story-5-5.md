---
review: input-reconciliation
run: architecture spine update 2026-10-07
input: Story 5.5 - Internal And Domain-Service Trust Boundary
spine: _bmad-output/planning-artifacts/architecture.md (updated 2026-10-05, AD-1..AD-33)
reviewer-mode: read-only (only this report written)
created: 2026-10-07
---

# Input Reconciliation - Story 5.5 Trust Boundary vs Architecture Spine

## Scope and sources

| Source | State read | Authority |
| --- | --- | --- |
| `_bmad-output/implementation-artifacts/spec-5-5-internal-and-domain-service-trust-boundary.md` | Frozen block (Intent, Boundaries, I/O matrix) committed in `eeeae00b`. Uncommitted: owner decision of 2026-10-07, review-loop-1 bad_spec loopback amendments (new tasks/ACs, KEEP list, 49-row triage log). Spec `status: in-progress`, `review_loop_iteration: 1`. | Frozen block and the owner-logged change are owner intent. The loopback tasks/ACs are review-loop derivations. |
| `_bmad-output/planning-artifacts/epics.md` Story 5.5 (lines 4302-4362) | Unchanged by today's diff | Requirements: FR28 primary, plus the NFR1/NFR2 internal slice. Architecture constraints: AD-3, AD-10, AD-16, AD-18. |
| `_bmad-output/planning-artifacts/prd.md` | FR28 (line 301), NFR1 (360), NFR2 (361), NFR3 (362), FR16 (259), NFR12 (371) | FR16: "A DAPR notification path is required only where a subscriber cannot hold a SignalR connection". This is consistent with the Direct transport. |
| Working tree (uncommitted, concurrent build loop) | `src/Hexalith.EventStore.ServiceDefaults/Authentication/*` (new), `src/Hexalith.EventStore/Authentication/*`, `ProjectionChangeNotifierOptions.cs`, `DaprProjectionChangeNotifier.cs`, `ProjectionNotificationController.cs`, `TrustedEffectsController.cs`, `HttpTrustedEffectSubmitter.cs`, `src/Hexalith.EventStore.DomainService/*`, AppHost `Program.cs`, `hexalith-realm.json`, docs guides | In-flight implementation only. It is evidence of intent, not of delivery. |
| `epics.md` Story 2.13, `2-13-dapr-notification-distribution-qualification.md` | Backlog | Cross-story impact (see C3, OQ-1) |

Code was read only. No build or test was run, and no git mutation was made.

## (a) Architecture-level decisions and spine status

A decision counts as architecture-level when independently built hosts (EventStore gateway, domain-service hosts, Tenants, Operations, future generated or external hosts) could otherwise pick incompatible choices.

| # | Decision (Story 5.5) | Why it is cross-host | Spine status | Spine evidence (quotes) |
| --- | --- | --- | --- | --- |
| D1 | **Workload-assertion scheme for service-to-service calls.** Every internal call carries the receiver's app-channel token **and** exactly one short-lived JWT workload assertion from the existing trusted issuer. The assertion names the caller, the receiver audience, and the operation. | Callers and receivers must agree on the credential, its claims, header, audience, operation names, lifetime ceiling, and denial semantics. | **MISSING** (no workload-assertion AD). AD-28 and AD-10 each cover only one half. | AD-28: "Every non-Development DAPR app endpoint validates `dapr-api-token` ... The token alone does not authenticate a claimed caller app ID". AD-10: "AD-28 remains a distinct authentication scheme." Neither names a caller/audience/operation credential. |
| D1a | **Sidecar caller attribution is deny-only.** `dapr-caller-app-id` never establishes identity. If present, it must equal the asserted caller. If absent, the call is not denied. | Receivers must agree on whether attribution is required or optional. | **PARTIAL / wording conflict** (C2) | AD-28: "Operation authorization requires the authenticated channel plus sidecar-established caller attribution and catalog/ACL authorization." The working tree treats the header as optional and deny-only (`WorkloadAssertionEvaluator.cs:79`). The frozen block says: "Never: Derive grants from `dapr-caller-app-id`". |
| D1b | **Minimal rebuilt principal.** A workload principal holds only `workload:<caller>` plus its granted operations. It never carries tenant, role, or admin claims, and it never satisfies a human `[Authorize]` (the gateway default policy is JwtBearer-only). | Every receiver must rebuild the principal the same way, or a workload token would satisfy human endpoints on some hosts. | **PARTIAL** | AD-10: "Caller app ID, mTLS, and ACLs provide attribution and transport constraints, not human or tenant authorization." AD-27: "a `system:*` subject never synthesizes tenant or global-administrator grants." AD-28: "mTLS, ACLs, and caller app ID never create global administrator or tenant claims." No AD states that a workload principal cannot satisfy human policies. |
| D1c | **Reuse of the AD-10 JWT contract** for assertion validation, with the receiver's own audience. No custom signing protocol, issuer, or certificate platform. | Hosts must not each build their own validator. | **PARTIAL** | AD-10: "Every named or future externally reachable or JWT-binding application host consumes this contract". This covers domain-service hosts only by generality. The named inventory lists the "Tenants domain-service host" but not the Sample domain service, which now binds JWT (AppHost `sample.WithEventStoreJwtAuthentication`). |
| D2 | **Resource-bound provenance for projection-change notifications.** The provenance must carry tenant, projection-type, and topic bindings that exactly match the notification. Unbound or partial provenance is denied (`binding-missing`). An issuer that cannot bind refuses bound requests (`IWorkloadAssertionIssuer.CanBindResources`). | Publisher and receiver must agree on the binding claims, what is mandatory, and refusal semantics. Any future publisher, such as a Story 2.13 fan-out, inherits this. | **MISSING** (only AD-10's generic rule applies) | AD-10: "Public and internal endpoints authenticate and authorize tenant and operation before disclosure or admission-state access." AD-8 covers only the payload: "SignalR carries metadata-only freshness notifications." Nothing binds publisher identity to tenant or topic. |
| D3 | **Transport selection for projection-change notifications.** `Direct` (in-process Dapr actor invocation of the ETag actor plus local SignalR broadcast) is the default. `PubSub` is refused at startup without a binding-capable issuer, so it is non-production because only AD-10 symmetric mode can bind. | Operators and hosts must agree on which transport production may use, and the deployment subscription, ACLs, and Story 2.13 depend on it. | **MISSING**, and **CONTRADICTED** in AD-8 wording (C1) | AD-8: "Pub/sub and notifications are at-least-once and unordered." Under `Direct`, regeneration and broadcast are single-attempt and fail-open (`EventReplayProjectionActor.cs:62-80`; `DaprProjectionChangeNotifier.cs:75,166`). AD-26: "Retained direct Redis is not an accepted exception." |
| D4 | **Trusted-effect submitter credentials.** A domain service that submits to `api/v1/trusted-effects` is an internal caller with **its own** workload assertion (caller = its app ID, audience = EventStore, operation = trusted-effect) and is allow-listed at the gateway. It never relies on an app-ID grant. | Every domain module that schedules typed reminders or effects must present the same credential shape. | **MISSING** (the spine does not mention trusted effects; only AD-5 admission covers effects) | AD-5: "Every production aggregate mutation or side effect requires a non-empty opaque idempotency key, a successful AD-25 admission". No inbound identity rule for effect submitters. |
| D5 | **Domain-service inbound authentication.** The SDK route catalog maps each route to an operation-scoped policy. Weak or anonymous host overrides fail inventory or startup. Only the three probes are anonymous. Sidecar-originated routes (subscribe, pub/sub, actors) are admitted by the channel token alone. | All domain modules get the boundary from the SDK (AD-2) and must not diverge. | Probe and fallback rules **LANDED** (AD-16, AD-2). The operation-scoped workload policy is **MISSING**. Channel-only routes are **PARTIAL** (OQ-5). | AD-16: "Every HTTP host configures an authenticated fallback authorization policy. Only `/health`, `/alive`, and `/ready` carry explicit `AllowAnonymous` metadata." AD-2: "A conforming host calls `AddEventStoreDomainService()` and `UseEventStoreDomainService()`." |
| D6 | **Outbound assertion header ownership.** Platform handlers attach the assertion for the exact audience and operation, remove any inbound assertion and `Authorization`, and the query invoker no longer forwards the bearer. | Same class of risk as AD-18: inbound headers steering internal identity. | **PARTIAL** | AD-18: "One platform handler replaces, never appends, outbound `dapr-app-id` and `dapr-api-token` ... Caller-provided and forwarded control-plane headers are discarded." It does not cover the assertion header or bearer forwarding. |
| D7 | **Wire administrator flags are untrusted hints.** The domain-service SDK clears `actor:globalAdmin` / `IsGlobalAdmin` unless the domain verifies the user's current authority. Verifier failure gives a bounded denial. | Every domain and the SDK must treat the same envelope fields the same way. | **PARTIAL** | AD-10 Prevents: "trusting ... caller-supplied roles as application authorization". AD-10: "Every later mutation disposition re-evaluates current authorization." No AD names the envelope admin hints. |
| D8 | **Human global-administrator bootstrap uses a delegated human credential and never an app-ID grant.** | Tenants and EventStore must agree on how the first admin is authorized. | **LANDED** for the prohibition; mechanism and production credential are open (OQ-8) | AD-28: "caller app ID never create global administrator or tenant claims". AD-29 binds human attribution for Admin mutations only. |
| D9 | **Secrets for internal credentials** (`APP_API_TOKEN` per receiver, plus the workload-issuer client secret for EventStore and for each trusted-effect submitter). | Deployment owners must inventory them identically. | **PARTIAL** | AD-24: "The DAPR app-channel token is startup-loaded, so rotation requires a controlled sidecar/workload rollout." Workload-issuer client credentials are not in the contract inventory. |
| D10 | **Audience = the receiver's Dapr app ID.** | Must match AD-33 routing, or a routed call would carry the wrong audience. | **PARTIAL** (implicit) | AD-33: "Commands/queries map by `(Domain, MessageType)` ... to exactly one app ID, method, and contract version." No link from the catalog to an assertion audience or operation (OQ-10). |

The remaining focus ADs (AD-27, AD-29, AD-31) are consistent but need one cross-reference each:
- **AD-27:** "Internal platform-operation scope uses a distinct cataloged namespace plus AD-28 authentication/authorization". AD-28 alone no longer authenticates an internal caller, so the reference should name AD-34.
- **AD-29:** consistent. Story 5.5 does not change Admin mutation attribution. Admin Server still forwards the human JWT, which the gateway selects as JwtBearer.
- **AD-31:** "non-production until ... AD-28 authentication". Operations' invoked recovery operations will need the AD-34 assertion as well as the AD-28 channel.

## (b) Stable owner intent vs in-flight implementation

Story 5.5 is in a **bad_spec loopback** (review loop 1, 2026-10-07). The spine should bind only the left column.

| Stable: owner-decided, spine may bind | Source |
| --- | --- |
| `APP_API_TOKEN` authenticates only the Dapr-to-app channel. | Frozen Decision |
| Short-lived workload assertions from the **existing trusted JWT issuer** prove caller, audience, and operation. The shared JWT validation contract is reused. No custom signing protocol or certificate platform. | Frozen Decision |
| Signed publisher provenance is bound to projection tenant/topic, and notification tenant/topic consistency is validated. | Frozen Decision + Always |
| Deny missing, duplicate, conflicting, expired, wrong-audience, wrong-caller, wrong-operation, and unavailable credentials before protected work. | Frozen Always |
| Never derive grants from `dapr-caller-app-id`, loopback, ACLs, mTLS alone, or wire admin flags. | Frozen Never |
| A minimal principal executes only the permitted operation. A route override gets the same policy, and a weak override fails inventory or startup. A forged callback leaves freshness unchanged. | Frozen I/O matrix |
| Human administrator authority requires separately verified, current delegation or authorization. | Frozen Decision |
| Only `/health`, `/alive`, `/ready` are anonymous. | Frozen Always (already AD-16) |
| **Mandatory binding:** the receiver denies unbound or partially bound provenance, an issuer that cannot bind refuses bound requests, the default transport is `Direct`, and `PubSub` is refused at startup without a binding-capable issuer. The realm `eventstore` client grants neither `projection:notify` nor the `eventstore` audience. **Accepted cost:** no pub/sub redelivery for freshness in authority mode, and multi-replica broadcast relies on the SignalR backplane. | Owner decision 2026-10-07 (Spec Change Log) |
| Every internal caller is a workload with its own assertion, including domain services submitting trusted effects. No multi-audience or all-operation token is shared across receivers. | Direct consequence of the frozen Decision and Never (the old trusted-effect path minted identity from `dapr_caller_app_id`). Recorded in Design Notes and the loopback "known-bad states". Bind the principle only. |

| In-flight: do NOT bind in the spine | Why |
| --- | --- |
| Literal header `X-Hexalith-Workload-Assertion`, caller claim `azp`, `eventstore:operation`, `eventstore:bound-*` claim names, the operation vocabulary strings, reason-code list, EventId 5501, and 401/403/503 mapping | Code under re-derivation. Bind the **owner** (ServiceDefaults contract) the way AD-10 binds `JwtBearerAuthenticationContract`, not the literals. |
| Lifetime ceiling of 300 s (default) / 900 s (configurable) | BH-8 shows external IdPs may not meet it, and the docs patch P-16 is still pending. See OQ-4. |
| Authority-mode issuance: one token per (audience, operation) via Keycloak optional client scopes, per-pair caching, and a returned-`aud` check | BS-C fix in progress. Keep only the principle "never broadened". |
| `AllowedPublishers` default `eventstore`, `ProvenanceAudience`, and the `Provenance` body field on `ProjectionChangedNotification` | Contract shape still moving. NFR12 inventory applies (OQ-12). |
| `IDomainServiceAdministratorVerifier` (every verifier must confirm) and Tenants read-model verifier | Mechanism. The eventual-consistency semantics are unsettled (OQ-7). |
| Tenants bootstrap mechanism (ROPC of the local admin in Keycloak mode; locally signed token in symmetric mode) | BS-B fix in progress. The production credential is undocumented (OQ-8). |
| Per-domain-service workload clients for trusted-effect submitters, `WithEventStoreTrustedEffectSubmitter`, `Authentication:DaprInternal:AllowedCallers` | BS-A fix in progress. The working-tree realm has no submitter client yet. |
| `IHostedLifecycleService` route inventory, any-workload fallback, stripping of the framework anonymous metadata | Implementation of AD-16/AD-2 conformance. |
| Defers D-1..D-4 (gateway Dapr routes anonymous; channel-only routes indistinguishable from peer invocation; Tenants tests not in CI; admin read-model lag) | Pending loop exit. D-1 and D-2 have spine impact (OQ-5, OQ-6). |

## (c) Proposed amendments

All wording below states boundaries and does **not** claim delivery. Each block ends, or is covered by, the spine's standard "required boundary, not evidence" convention.

### New AD-34 - Internal Callers Prove Workload Identity With Scoped Assertions [ADOPTED]

Proposed placement: the "Runtime, security, and identity" theme row, extended to "AD-26 through AD-29, AD-31 through AD-34".

- **Binds:** FR28, FR16, NFR1-NFR4, NFR12, NFR17
- **Prevents:** a caller header, network position, app-channel token, or broadly shared token standing in for an internal caller's identity, audience, operation, or resource scope.
- **Rule:** Every service-to-service call into a protected EventStore or domain-service endpoint presents the receiver's AD-28 channel token **and** exactly one short-lived workload assertion. This covers EventStore invoking a domain service, a domain service submitting a trusted effect, and any future internal caller. The assertion comes from the trusted issuer of the AD-10 JWT contract and is validated through that contract with the receiver's own audience. The assertion names one caller workload, the receiver's AD-33 app ID as audience, and the operation the call performs. A token is never broadened across audiences or operations. Receivers deny the following before binding or downstream work, and fail closed when verification is unavailable:
  - missing, duplicate, or credential-conflicting assertions
  - expired, over-lifetime, wrong-issuer, or wrong-audience assertions
  - callers that are not allow-listed, or whose sidecar attribution conflicts
  - assertions that do not grant the requested operation

  The rebuilt principal holds only the workload identity and its granted operations. It never satisfies a human-authorization policy and never carries tenant, role, or administrator claims. Wire administrator flags remain untrusted hints that only a domain's verification of current human authority may honor. `Hexalith.EventStore.ServiceDefaults` owns the versioned assertion contract (header, claim types, operation vocabulary, lifetime ceiling, bounded reason codes). Hosts consume it and never define their own signing protocol, issuer, or certificate platform.

**Resource binding.** A credential that authorizes a tenant- or topic-scoped effect apart from a live request is today the projection-changed provenance. It must carry tenant, projection-type, and topic bindings that exactly match the AD-27-canonical notification. Unbound or partially bound credentials are denied. An issuer that cannot embed per-request bindings refuses the bound request rather than issuing an unbound assertion. Only the AD-10 symmetric mode, which is never admitted in Production, can currently bind. Bound transports therefore stay non-production until an owner-approved binding-capable issuer exists. This is a required boundary, not evidence that any host has adopted it.

Sentence counts: Rule 4, binding paragraph 4 (plus the closing convention sentence). If one AD is too dense, the alternative is to split the binding paragraph into **AD-35 - Projection-Change Provenance Is Resource-Bound** with the same text, Binds FR16 and FR28, and Prevents "a replayable publisher credential regenerating freshness for any tenant or topic".

### AD-8 amendment (fixes C1 and C3)

Replace the first sentence of the AD-8 Rule ("Pub/sub and notifications are at-least-once and unordered.") with:

> Domain-event pub/sub delivery is at-least-once and unordered. Projection-change freshness signals (ETag-validator regeneration and SignalR notifications) are best-effort hints whose redelivery depends on transport: `Direct`, the default and only production-eligible transport, is one Dapr actor invocation plus local broadcast with no redelivery; `PubSub` is at-least-once and permitted only with AD-34 bound provenance. Consumers tolerate lost, duplicated, and reordered freshness signals; neither a signal nor its absence is completion or freshness evidence, and a forged, unbound, or mismatched callback changes no ETag, freshness, or broadcast state. Multi-replica distribution remains unresolved under Story 2.13, and the retained Redis backplane is not an accepted exception.

The rest of AD-8 is unchanged: deduplication by `MessageId`, scoped sequence guards, the 16-entry/2,048-byte metadata bound, read-model evidence for success, and the Delivery-failure paragraph.

**Is `Direct` consistent with AD-8 after this wording?** Yes. AD-8 exists to stop transport acknowledgement being presented as projection-confirmed success, and `Direct` never asserts success. "User-visible success requires read-model evidence" still governs. The only conflict was the blanket "notifications are at-least-once" clause, which was already imprecise because SignalR delivery to clients was never at-least-once. One residual risk is not covered by the wording and must stay an open question rather than be papered over: **OQ-2**, stale `304` after a lost regeneration.

### AD-28 amendment (fixes C2)

Replace "Operation authorization requires the authenticated channel plus sidecar-established caller attribution and catalog/ACL authorization." with:

> The channel token proves only that a request crossed the receiver's own sidecar; service-invocation operations additionally require an AD-34 workload assertion, and sidecar caller attribution, when present, must match the asserted caller and otherwise serves only to deny. Routes admitted by the channel alone (subscription discovery, pub/sub delivery, actor callbacks) confer no workload identity, operation, tenant, or administrator authority, and remain under AD-16's authenticated fallback including framework-mapped Dapr routes.

### AD-18 amendment (D6)

Append:

> The same ownership applies to the AD-34 assertion header: the platform outbound handler attaches the assertion for the exact receiver audience and operation, replacing never appending, and discards caller-supplied or forwarded assertions. Internal workload calls never forward an inbound `Authorization` bearer, and hosts never mint or attach assertions outside the platform handlers.

### AD-10 amendment (D1b, D1c)

Append to the JWT-contract paragraph, before "AD-28 remains a distinct authentication scheme.":

> AD-34 workload assertions are validated through this contract with the receiver's own audience, so every domain-service host that validates them, including the Sample domain service, belongs to the host/config fingerprint inventory. A workload principal never satisfies a human-authorization policy.

Then keep "AD-28 remains a distinct authentication scheme." and add "and AD-34".

### AD-24 amendment (D9)

Append to "Secret contract and rotation":

> The contract also inventories each receiver's `APP_API_TOKEN` and each AD-34 workload-issuer client credential (EventStore and every trusted-effect submitter); the symmetric signing key is Development or break-glass material and is never a production secret.

### AD-27 cross-reference

"plus AD-28 authentication/authorization" becomes "plus AD-28 channel and AD-34 workload authentication/authorization". Optionally add: "Resource bindings carry the canonical tenant."

### AD-31 cross-reference

In "AD-28 authentication", add "and AD-34 workload authentication for invoked operations".

### AD-33 (optional, one sentence)

> The AD-34 audience of a routed invocation is the target app ID this catalog resolves.

Whether catalog entries should also carry the required operation is left as OQ-10.

### Diagram and gate table (optional)

- **Spine diagram:** label `Aggregate -->|Dapr service invocation| Domain` as "Dapr service invocation; AD-28 channel + AD-34 assertion", and add `Domain -.->|trusted effect; AD-34| Edge`.
- **Implementation Status table:** add one row for "Internal workload assertions (AD-34) and bound projection provenance".
  - Safe posture: projection-change `PubSub` is non-production; no FR28/NFR1 internal-slice readiness claim.
  - Owner: Security plus Story 5.5, before FR28 closure.
  - Also extend the existing "Shared JWT host conformance" row with the domain-service hosts.

## Contradictions with existing ADs

- **C1: AD-8.** "Pub/sub and notifications are at-least-once and unordered" is false for the owner-chosen default `Direct`. That transport is single-attempt and fail-open after the read model persists (`EventReplayProjectionActor.cs:62-80`). Resolved by the AD-8 amendment.
- **C2: AD-28.** AD-28 lists "sidecar-established caller attribution" as a requirement for operation authorization. Story 5.5 makes the assertion the identity and the attribution optional and deny-only (`WorkloadAssertionEvaluator.cs:79`). This is a wording conflict, not a difference of intent. Resolved by the AD-28 amendment.
- **C3: AD-26, AD-1, and Story 2.13 vs the owner's "accepted cost".** The owner wrote that multi-replica SignalR "relies on the SignalR backplane". AD-26 says "Retained direct Redis is not an accepted exception", and Story 2.13 allows a Redis exception only by an architecture-owner decision with evidence. The owner note must not be read as accepting that exception. It records a dependency that existed in both transports, because the projection-changed subscriber is a competing consumer and also delivers to one replica only. Resolved by the AD-8 amendment's last sentence. No accepted exception is implied.
- **C4: AD-27.** AD-27's reference to "AD-28 authentication/authorization" for internal platform scope no longer suffices. This is a cross-reference fix.
- **No conflict with AD-1.** `Direct` still uses the Dapr actor API (`IActorProxyFactory`), so refusing pub/sub creates no non-Dapr path and needs no AD-1 exception.

## Open questions (unsettled, so not decisions)

- **OQ-1: Production pub/sub notification path.** With an OIDC authority, no binding-capable issuer exists. The frozen Decision forbids a custom signing protocol, and the owner rejected Keycloak dynamic scopes. Bound pub/sub is therefore non-production indefinitely.
  - Story 2.13's "Dapr pub/sub fan-out to every locally connected hub" candidate inherits this AD-34 binding requirement, which narrows 2.13 to the Azure SignalR output binding or an owner-approved Redis exception.
  - Owner decision needed: permanent Direct-only, token exchange or resource-indicator issuance, or a new 2.13 design.
- **OQ-2: ETag-validator staleness under `Direct`.** If the in-process `RegenerateAsync` fails after the read model persists, the failure is logged and lost (fail-open catch). `QueriesController.cs:137-174` compares `If-None-Match` against the ETag-actor value after running the query. Without redelivery, a lost regeneration can return `304 Not Modified` on changed data until the next change.
  - Decide among: retry or durable repair, deriving the validator from the persisted checkpoint, or explicit acceptance plus documentation.
  - This touches AD-8's "honest freshness" and AD-15. Confirm the risk with a test before deciding.
- **OQ-3: Production multi-replica freshness distribution** stays blocked on Story 2.13. Under `Direct`, only the replica that runs the projection actor broadcasts locally.
- **OQ-4: Assertion lifetime ceiling.** The working tree uses a 300 s default, configurable up to 900 s, and checks `exp - iat`. BH-8 says some IdPs (Entra-style) cannot issue tokens that short. The spine should name a contract-owned ceiling, not a number, until this is settled.
- **OQ-5: Channel-only routes vs peer service invocation (D-2 / BH-7).** The receiving sidecar adds `APP_API_TOKEN` to every inbound call, so the subscribe, pub/sub, and actor routes cannot tell sidecar-originated delivery from a peer's service invocation. This needs AD-9/Story 5.7 ACL deny rules for those paths, message-level provenance, or an explicit risk acceptance.
- **OQ-6: Gateway Dapr framework routes are anonymous (D-1 / BH-6, high).** `src/Hexalith.EventStore/Program.cs:47-48` maps `MapSubscribeHandler()`/`MapActorsHandlers()` without authorization. This predates the story and is out of its diff. It does not conform to AD-16 and AD-28, so it needs an owner and a gate row, not only a story defer.
- **OQ-7: Administrator authority from an eventually consistent read model (BH-11 / EC-19 / D-4).** This sits against AD-10's "re-evaluates current authorization". Either define a staleness bound or explicitly accept eventual consistency.
- **OQ-8: Production global-administrator bootstrap credential.** It is undocumented (BS-B, BH-9). Keycloak-mode ROPC of a local admin password is a Development mechanism.
- **OQ-9: Trusted-effect submitter credentials in production.** Who provisions a per-domain-service IdP client, how it rotates, and where it sits in the AD-24 inventory are all open. The working-tree realm declares no submitter client, and omitting the submitter handler fails closed (401) with only a log signal (EventId 5542).
- **OQ-10: Operation in the AD-33 catalog.** Should catalog entries carry the required AD-34 operation, so that catalog, ACL, and assertion agree? Custom `DomainServiceRegistration.MethodName` routes currently get no assertion (VG-O2, rejected for this story).
- **OQ-11: Receiver-side single audience.** The issuer verifies the returned `aud`. The receiver checks only audience membership, mitigated by the caller-conflict check. Decide whether the contract should require exactly one audience.
- **OQ-12: NFR12 and AD-9 compatibility.**
  - Three changes are breaking under NFR12 and need Story 3.18 inventory and deprecation evidence:
    - the `Transport` default flip from `PubSub` to `Direct`
    - the credential requirement on all domain-service endpoints
    - the effective denial of external domain-service publishers on `*.*.projection-changed`
  - `deploy/dapr/subscription-projection-changed.yaml` still says it "Receives projection change notifications from external domain services", but it now serves a non-production transport. Stories 5.6-5.8 must reconcile it under AD-9.

## Recommendation for the spine Update run

1. Add **AD-34** with the Rule and Resource-binding text above, marked `[ADOPTED]`. It records accepted intent, not delivery, which matches the spine's own definition of `[ADOPTED]`.
2. Apply the **AD-8** and **AD-28** amendments. They remove C1 and C2.
3. Apply the AD-10, AD-18, and AD-24 amendments and the AD-27 and AD-31 cross-references.
4. Record OQ-1, OQ-2, and OQ-6 in the Implementation Status table or as open questions, and do not decide them in the spine.
5. Do **not** bind header or claim literals, the 300 s value, Keycloak scope mechanics, verifier mechanics, or bootstrap mechanics while Story 5.5 is in bad_spec loopback.
6. Re-run this reconciliation once Story 5.5 exits review. Then confirm that the literal contract (header, claims, operation vocabulary) is stable before naming it in AD-34.
