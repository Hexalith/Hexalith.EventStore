# Architecture Update — Adversarial Divergence Review

**Date:** 2026-10-07
**Subject:** `_bmad-output/planning-artifacts/architecture.md`, SHA-256 `e15b079f475c03603ac895213bfdd8ce0f68d0fdb90985d23e38d7874ac3f6cf` (36 ADs, `status: draft`, AD-26 plus four inline clauses `[ASSUMPTION]`).
**Baseline diff:** the pre-update scratchpad copy `architecture.pre-update-2026-10-07.md` against the subject. The update added AD-34, AD-35, and AD-36 and amended AD-1, 5, 7, 8, 10, 11, 12, 13, 17, 18, 22–28, 30, 31, and 33.
**Level below:** `_bmad-output/planning-artifacts/epics.md` (working tree, including the uncommitted backlog Stories 2.14–2.17, 3.18–3.20, 4.16–4.17, 5.11, 6.7, 7.21, and 9.3–9.5), plus the Story 5.5 spec (working tree, in review) and the current source it names.
**Evidence class:** `tool-persona`. This review is evidence under the PRD Assurance Control. It is not an approval, not a ratification, and not `independent`. It changes nothing in the spine, the epics, the tracker, or the code.

## Verdict

**FAIL: units built to the letter of the ADs still diverge.** The 2026-10-07 amendments add a second credential plane (AD-36) and a third digest plane (AD-34 → AD-26). Neither plane says how it meets the planes that already exist:

- AD-36 says nothing about the delegated-user hops that already run (generated REST hosts, Admin, Tenants API, McpCli).
- AD-36 says nothing about the actor-invocation surface.
- AD-26 and AD-34 do not say which artifact owns the operation matrix and its digest.

Two pairs are critical: one breaks every human-authorized internal hop, and one reopens the NFR7 second-writer class. Ten are high. Each finding below gives a ≤2-sentence wording that would close it. Nothing is closed here, and every gate stays where the spine leaves it.

## Method

For each amended or new AD, I built two units one level down that each satisfy every AD as written, then checked whether they still compose. Where current source already instantiates one of the units, I cite it so the pair is concrete rather than hypothetical. Ratings:

- **critical:** data loss, an authorization bypass, or no conforming implementation of a primary flow.
- **high:** two owners or two authorities for one fact, or a cycle that blocks a gate.
- **medium:** drift that a validator would catch late.
- **low:** wording or a qualification gap.

---

## Critical

### C1 — Delegated-user hops have no conforming credential (AD-4, AD-18 ¶2, AD-29 vs AD-18 ¶4, AD-36, AD-10)

**Unit A (the delegated-user callers):**
- Generated REST hosts use `IEventStoreGatewayClient` (AD-4, `architecture.md:132`). The Sample API chains `InboundBearerForwardingHandler` before `AddEventStoreDaprServiceInvocation("eventstore", …)` (`samples/Hexalith.EventStore.Sample.Api/Program.cs:36-39`).
- AD-18 ¶2 assumes this design: the handler is "registered last … after any inbound bearer or header-forwarding handler" (`:246-248`).
- Admin Server forwards the operator's bearer to `eventstore` over Dapr service invocation (`src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionQueryService.cs:104-112`), consistent with AD-29's "authenticated operator identity from end to end" (`:348`).
- Admin UI → Admin Server works the same way (`src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs:105-119`).
- The Tenants API is drawn as "service invocation only" to EventStore (`:488`).

**Unit B (the receivers):**
- AD-18 ¶4 says: "Internal workload calls never forward an inbound `Authorization` bearer" (`:254-257`).
- AD-36 says: "Every service-to-service call into a protected EventStore or domain-service endpoint presents … exactly one short-lived workload assertion", and the resulting principal "never satisfies a human-authorization policy" (`:394`).
- The Story 5.5 workload scheme denies "an `Authorization` header alongside an assertion", and its outbound handler "strips `Authorization` and any inbound assertion" (spec-5-5, working tree).

**Divergence:** Every hop that acts for a human crosses a sidecar.
- Under AD-36, such a hop must carry an assertion, and the resulting workload principal cannot satisfy EventStore's or Admin's human, tenant, and role policies (AD-10 `:174`).
- Under AD-18 ¶4, the hop cannot carry the human bearer.
- Carrying both is denied by the 5.5 receiver.

AD-29's fallback, a "validated, bounded delegation", has no contract, issuer, or carrier, and AD-36 forbids hosts to mint or attach credentials outside the platform handlers. So Admin, generated REST, Sample UI, Tenants API, and any in-cluster McpCli (AD-35) have no conforming path. If the platform handler is ever generalized to every sidecar-routed client, it breaks all of them.

**Closing wording (AD-36, new paragraph):** "A sidecar-routed call made for an authenticated human is a delegated-user call: it forwards only that human's AD-10 bearer (or an AD-29 delegation issued by the AD-10 issuer) for the receiver's human audience and carries no workload assertion, while a workload call carries no human credential and exactly one assertion. Every AD-33 route entry declares which kind it admits, and receivers reject the other kind and any request carrying both."

### C2 — The actor state component can be given a second writer by obeying AD-9 parity (AD-9, AD-3, AD-26, Story 5.7 vs AD-5 envelope, AD-1, Story 4.16)

**Unit A (Story 5.7, "Production DAPR Component And ACL Parity"):**
- AD-9 requires local and deployment topology to change together (`:168`).
- 5.7's reconciliation flags as a parity gap that "production state-store templates omit the local `keyPrefix: none` posture" (`epics.md:4431`).
- The local component sets `keyPrefix: none`, scoped to `eventstore`, `eventstore-admin`, **and `tenants`** (`src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml:41-58`). The stated reason is that "eventstore-admin reads the same EventStore state keys".
- AD-3 permits Admin to read platform-owned operational state through the Dapr state API (`:126`).
- AD-26 names exactly one state component, `statestore`, with `actorStateStore: true` (`:326`).
- Production today is scoped to `eventstore` and `eventstore-admin` (`deploy/dapr/statestore-postgresql.yaml`).

**Unit B (Story 4.16, envelope first):**
- AD-5 requires a mechanically enforced no-second-writer envelope through "component scopes and ACLs", the actor-only write path, and single activation (`:140`).
- AD-1 forbids application code to read or write Dapr private actor-state keys (`:114`).

**Divergence:** Dapr component scopes apply per component, not per key, and do not separate read from write. With `keyPrefix: none`, any scoped app's state-API writes land in the actor key space, so `eventstore-admin` and, locally, the domain service `tenants` become second writers to actor rows. 5.7 achieves AD-9 parity by importing exactly the posture that 4.16 must exclude, and neither story depends on the other. Locally, scoping `tenants` to the actor store already contradicts AD-1's "domain modules receive no direct persistence authority".

**Closing wording (AD-5 envelope or AD-26):** "The `actorStateStore: true` component is scoped to the actor-host app ID alone and keeps the default app-ID key prefix in every profile, and state that any other app ID reads lives in a separately scoped component. AD-9 parity is reached by moving the AppHost to the production posture, never by widening production."

---

## High

### H1 — One "JWT contract" cannot validate both humans and workloads (AD-10 vs AD-36, Story 5.11, trusted-effect verifier)

**Pair:** a Story 5.11 conformance host versus a Story 5.5 workload receiver.
- AD-10 makes "issuer, audience, signature, lifetime, roles, and tenant validation … mandatory with 60-second clock skew" for every inventoried host. That inventory now includes "every DomainService SDK host that validates AD-36 workload assertions" (`:176`).
- 5.11's negative suite rejects "role, and tenant violations" on every conforming host (`epics.md:4660-4661`).
- AD-36 requires that assertions be validated "through this contract", yet the rebuilt principal "never carries tenant, role, or administrator claims" and has its own lifetime ceiling (`:394`). The 5.5 scheme enforces `exp - iat <= 300 s`.
- The trusted-effect delegation verifier is a third validator (`src/Hexalith.EventStore/Authentication/JwtTrustedEffectDelegationVerifier.cs`): audience `eventstore-gateway`, a 30-second skew, a 15-minute lifetime, and asymmetric algorithms only.

**Divergence:** Read literally, AD-10 makes the 5.5 workload scheme and the delegation verifier "locally reimplemented validators" that fail the host gate. Applying AD-10's mandatory tenant and role checks to AD-36 assertions rejects every valid assertion.

**Closing wording (AD-10):** "The contract defines versioned profiles, each with its own fingerprint, mandatory and forbidden claims, audience rule, skew, and lifetime ceiling: human bearer, AD-36 workload assertion, and resource-bound delegation. A host validates a credential only through a registered profile, and a validator outside those profiles fails the host gate."

### H2 — Two resource-bound credential families with contradictory issuer rules (AD-36 resource binding vs trusted-effect delegation and gateway proof)

**Pair:** projection provenance (AD-36 `:396`) versus trusted-effect submission.
- AD-36 names only "today the projection-change provenance" and states that "only the AD-10 symmetric mode … can bind today".
- `TrustedEffectSubmitRequest.DelegationToken` (Contracts) carries a delegation bound to tenant, effect ID, source and target aggregate, command digest, workload, purpose, and causation. That is a tenant-scoped effect outside a live request, so it falls under AD-36's general rule. The verifier accepts **asymmetric tokens only** and throws when no authority is configured.
- `IReminderDelegationTokenProvider` states that "no production implementation ships".
- `TrustedEffectGatewayProof` signs admissions with an HMAC domain string of its own (`hexalith-trusted-effect-gateway-proof-v1`) keyed from the **AD-25 idempotency digest key ring**.

**Divergence:**
1. Trusted effects need exactly the binding-capable asymmetric issuer that AD-36 says does not exist, and the spine never names this credential.
2. The gateway proof is a host-defined signing protocol, which AD-36 forbids.
3. The gateway proof is an unregistered consumer of digest keys. AD-24 allows a digest-key generation to retire once "an AD-25 catalog proves that no live references remain" (`:293`), and that check cannot see in-flight proofs.

**Closing wording (AD-36 resource binding):** "Every resource-bound credential family (projection provenance and trusted-effect delegation today) is enumerated here with its issuer, mode, audience, and binding claims, and a family whose issuer cannot bind stays non-production. Internal proofs use dedicated keys inventoried in the AD-24 contract and never the AD-25 digest ring."

### H3 — The production freshness path cannot reach multiple replicas, and the ADs disagree on whether PubSub can ever qualify (AD-8, AD-36, Story 5.5, Story 2.13, AD-31)

**Pair:** the Story 2.13 Dapr design versus the Story 5.5 transport decision.
- AD-8 says `Direct` is "the default and only production-eligible transport" (`:160`).
- AD-36 says bound transports "stay non-production until an owner-approved binding-capable issuer exists" (`:396`), which implies PubSub can become eligible.
- The 5.5 owner decision records "multi-replica broadcast relies on the SignalR backplane". That backplane is excluded from every AD-26 profile and is an AD-34 non-conformance (`:93`, `:326`, `:382`).
- Story 2.13's preferred candidate, "Dapr pub/sub local-hub fan-out" (`epics.md:2017`), is itself a tenant-scoped effect outside a live request, so it needs AD-36 binding. Its constraints list omits AD-34 and AD-36 (`:2007`).
- Once PubSub is eligible, AD-8's delivery-failure rule (`:162`) requires every denied or expired signal to be captured by the AD-31 sink before acknowledgment. That sink is non-production, and capturing the signal stores the bearer `Provenance` JWT, which AD-10 says never enters evidence. AD-31 replay would also re-present an expired assertion.

**Divergence:** No production design delivers multi-replica freshness. 2.13 can satisfy its own listed ADs and still build an unbound fan-out. AD-8 and AD-36 disagree on PubSub's future.

**Closing wording (AD-8):** "A freshness signal that is denied, expired, or unbound is acknowledged and dropped with a bounded metric, is never captured with its credential, and is never replayed. PubSub and any Story 2.13 fan-out become production-eligible only through an AD-26 profile change that names a binding-capable issuer."

### H4 — MessageId grammar version has four carriers and compatibility has two authorities (AD-17, AD-33, AD-11, Stories 2.15 and 3.18)

**Pair:** a generated API host compiled against Contracts package N versus the gateway running catalog generation G.
- AD-17 makes the `Contracts` declaration the owner and says, as an `[ASSUMPTION]`, that the version is carried by the AD-33 route entry and the generator metadata (`:238`).
- The AD-33 instance is authored and signed by the Platform deployment owner at deploy time (`:372`), and its "contract version" is not defined as the grammar version.
- Story 2.15 introduces "a versioned contract manifest" that **assigns** v1 or v2 and also "records an NFR12 compatibility classification" (`epics.md:2092`, `:2105`).
- AD-11 `[ASSUMPTION]` names the Story 3.18 inventory as the **sole** compatibility authority (`:182`). Neither story depends on the other.

**Divergence:** A generated host can emit `Location` for a v1 MessageId while the activated route entry says v2. The gateway then canonicalizes differently, and the `Location` points at nothing, which AD-17 exists to prevent. Whichever of 2.15 or 3.18 lands first defines the classification schema.

**Closing wording (AD-17):** "The `Contracts` declaration is the only source of a command's MessageId version; the AD-33 entry and generator metadata are derived mechanically and carry its digest, and activation fails when a generated host's embedded digest differs. Story 2.15 writes its classifications into the Story 3.18 inventory schema and creates no second manifest."

### H5 — The seal source is unowned, and truthful-FAIL runs cannot be seals (AD-11, AD-12, PRD Assurance Control, Story 9.2 vs Stories 3.19, 3.20, 9.3–9.5)

**Pair:** the Story 3.19 validator (non-blocking, truthful FAIL) versus the Story 9.2 assurance check.
- AD-11 rejects "a validator result not retrieved from a sealed AD-12 CI run" (`:196`).
- AD-12 defines only non-blocking push runs, plus an `[ASSUMPTION]` that the seal comes from "the required, blocking run of the guarded transition" (`:204`).
- The PRD requires "a required, blocking run on the exact head SHA and workflow-file digest" (`prd.md:200`).
- Story 9.2 builds a "blocking, required check" (`epics.md:7381`), while 3.19, 3.20, and 9.3–9.5 run under the non-blocking truthful-FAIL rule.
- The release lane's validators belong to Hexalith.Builds at a pinned SHA (AD-22 `:281`), which makes it a third candidate seal.

**Divergence:** No story builds the transition-triggered blocking run.
- A required check that truthfully fails blocks `main`.
- A non-blocking run is not a seal, so `release-available` and `production-promoted` can never be sealed.
- Alternatively, an implementer might cite a non-blocking PASS as "sealed".

**Closing wording (AD-12):** "A seal is the result of a dedicated, required transition workflow, owned by Story 9.2 and triggered for one guarded transition, that re-runs the gate validator on the exact head SHA and binds its workflow-file digest. Push-time truthful-FAIL runs are evidence only and never seal."

### H6 — AD-26 ratification binds a whole-file digest that the spine itself requires to change afterward (AD-26, AD-36, AD-34, Stories 4.17 and 9.3)

**Pair:** the AD-26 ratification record versus later mandated spine edits.
- Ratification is "bound to the final architecture digest" (`:328`).
- The spine then requires further edits: reconcile AD-36 "when Story 5.5 exits review" (`:396`, `:528`); Story 4.17 binds the OQ8 identity "in … architecture" (`epics.md:4079-4081`); any accepted AD-34 exception rewrites AD-34 "Current state" (`:382`).
- PRD `:670` invalidates rows on any governed-source change.

**Divergence:** Each mandated edit invalidates the ratification that Stories 3.19 and 4.16 depend on. Each re-ratification restarts the 24-hour attestation, so the dependency loop does not converge.

**Closing wording (AD-26 ratification):** "Ratification binds the digest of AD-26 and of the clauses it names in the Story 9.4 ledger, not the whole file. An edit outside those clauses re-digests the Story 9.3 baseline without invalidating the ratification."

### H7 — The AD-34 operation/component matrix has two homes, and the profile binds the whole register (AD-34, AD-26, AD-9, Story 3.17 vs Story 3.19)

**Pair:** Story 3.17's `docs/architecture/dapr-infrastructure-inventory.yaml` versus Story 3.19's `deploy/dapr/production-profile.yaml`.
- The 3.17 inventory is written by the maintainer, enforced in the Contracts test lane, and covers every profile, Development and test included (3.17 spec tasks).
- AD-26 says the profile digest "must bind … the AD-34 operation/component matrix and exception-register digest" (`:330`), and the profile is authored by the deployment owner.
- The register at `docs/architecture/dapr-infrastructure-exceptions.yaml` also stores **unresolved** rows, although AD-34 calls it the accepted-exception register.
- Builds owns the version catalog in another repository; its gitlink is bumped automatically and changes the "exact runtime/client/component versions" that every row binds (`:380`).

**Divergence:**
- The two matrices can disagree on component, version, or guarantee.
- A Development-only exception re-digests production and invalidates `production-promoted`.
- A Builds gitlink bump makes rows stale with no signal.

**Closing wording (AD-34):** "Story 3.17's inventory is the only AD-34 matrix, keyed by profile and bound to the Builds gitlink, and the AD-26 profile binds the digest of its own profile-scoped projection of the inventory and register. A gitlink change that alters a bound version fails the 3.17 guard until requalified."

### H8 — Actor invocation reaches disclosure, admission, and freshness with the channel alone (AD-28 vs AD-10, AD-5, AD-8)

**Pair:** any app ID with a Dapr actor client versus EventStore's actors.
- AD-28 admits "actor callbacks" by the channel alone, conferring no authority (`:342`).
- AD-10 requires internal endpoints to authorize tenant and operation "before disclosure or admission-state access" (`:174`).
- `IAggregateActor` exposes `GetEventsAsync` and `ReadEventsRangeAsync`. The admission actors issue fences that `ProcessFencedCommandAsync` accepts. `IETagActor.RegenerateAsync` is the whole `Direct` freshness path (`DaprProjectionChangeNotifier.cs`).
- Dapr documents its ACL policies for service invocation, and AD-34 has not qualified whether they cover actor invocation.

**Divergence:** A domain service can obey every AD and still read event streams, obtain a fence, or regenerate ETags without the AD-3 edge or an AD-36 assertion. That contradicts AD-8's claim that "a forged … callback changes no ETag".

**Closing wording (AD-28):** "Until AD-34 qualifies that the profile restricts actor invocation to the hosting app ID, every actor method that discloses, admits, or mutates validates an EventStore-issued execution context bound to tenant, operation, and fence. The channel alone admits only Dapr runtime callbacks (activation, timers, reminders) and subscription delivery."

---

## Medium and low

| ID | Rating | Units that diverge | Divergence | Closing wording (≤2 sentences) |
| --- | --- | --- | --- | --- |
| M1 | medium | AD-27 platform-operation namespace (`Contracts`, cataloged in AD-33, external JWT operation claim) vs AD-36 vocabulary (`ServiceDefaults.EventStoreWorkloadOperations`: `domain-service:*`, `projection:notify`, `eventstore:trusted-effect`) vs the AD-25/33 idempotency facet "operation" | Three vocabularies with two owning packages. `ServiceDefaults` does not reference `Contracts`, so one registry cannot be shared. Humans and workloads share one issuer and the `eventstore:operation` claim type, with no rule that keeps them disjoint. | "`Contracts` owns one operation registry with disjoint `platform:` (human JWT) and `workload:` (AD-36) namespaces that ServiceDefaults and the AD-33 catalog reference by ID. A credential carrying an operation from the other namespace is denied." |
| M2 | medium | AD-10 host/fingerprint inventory (shipped inside the ServiceDefaults package) vs Story 5.7 workload inventory vs AD-33 app IDs vs AD-24 secret-contract consumers vs AD-26 profile vs AD-22 manifest | Six inventories of hosts and apps. Hosts in other repositories (Tenants, Parties, McpCli, generated hosts) cannot enter a package-owned inventory without an EventStore release. | "The activated AD-33 catalog plus the AD-26 profile is the only app and host identity set, and every other inventory is validated as equal to it. The AD-10 fingerprint registry is a deploy-time artifact, not package content." |
| M3 | medium | EventStore host (fallback = human JWT) vs DomainService host (fallback = any workload, `EventStoreDomainServiceSecurityExtensions.cs:80`) vs AD-28 "routes admitted by the channel alone … remain under AD-16's authenticated fallback" (`:342`) | Sidecar callbacks cannot satisfy either fallback unless the channel scheme satisfies the fallback. If it does, any route without an attribute becomes reachable by the channel alone. | "Channel-admitted Dapr routes carry an explicit sidecar-channel policy that the channel scheme alone satisfies. The AD-16 fallback is never satisfiable by the channel scheme." |
| M4 | medium | `ProjectionChangeNotifierOptions` vs `SignalROptions` (both define configurable `MaxDetailMetadataEntries` and `MaxDetailMetadataBytes`) vs AD-8's fixed 16 entries / 2,048 bytes (`:158`) vs Story 2.16 (tests defaults only) | Two option owners for one bound. Either can be configured above the AD-8 limit, and the two can differ. | "The AD-8 limits are `Contracts` constants that act as ceilings, read by both notifier and hub. Options may only lower them." |
| M5 | medium | AD-35 retirement of the released `Admin.Cli` package vs AD-11 "inventory remains 14 packages until Story 8.8 … from 14 to 16" (`:186`) | Two ADs own package-count transitions, so AD-35 retirement cannot pass AD-11. | "The package count is derived from `tools/release-packages.json`, and every addition or retirement, including Story 8.8's and AD-35's, passes the same atomic inventory gate and Story 3.18 SemVer classification." |
| M6 | medium | AD-35 ("EventStore keeps … the AD-3 policy edge"; McpCli "through … the EventStore gateway") vs AD-21 and AD-29 (admin semantics live in Admin Server's typed APIs) | Two admin edges. No transport rule exists for gateway-ready McpCli calls, which leads back to C1. | "McpCli reaches the EventStore gateway and Admin Server only over their public AD-3 and AD-21 HTTP edges with the end user's AD-10 bearer or an AD-29 delegation, and admin operations terminate at Admin Server." |
| M7 | medium | AD-33's "AD-36 audience … is the target app ID this catalog resolves" (`:372`; the catalog has no owning story, `:539`) vs receiver audience derived from the host itself (`DAPR_APP_ID`, then a fallback to `ApplicationName`, per the 5.5 spec) vs issuer audience taken from configuration `DomainServiceRegistration.AppId` | Three audience derivations. In production they agree only by naming convention. | "Each receiver's workload audience is its activated AD-33 app ID, and the fallback to `ApplicationName` is Development-only. The issuer takes the audience from the same activated entry." |
| M8 | medium | AD-36 contract (ServiceDefaults owns the "header") vs the `Provenance` body field on Contracts' `ProjectionChangedNotification`, published through `DaprClient.PublishEventAsync` (the AD-18 handler is not on this path), with audience defaulting to `eventstore` for every subscriber | Two owners of the assertion's carrier. The pub/sub audience is undefined, and broker retention persists the token. | "AD-36 defines a body carrier for pub/sub, owned by ServiceDefaults, whose audience is the subscribing app ID. Its token is excluded from broker-retained evidence by expiry-bounded retention or by being stripped at capture." |
| M9 | medium | AD-25 / Story 4.17: "re-verified to the bound SHA-256" for all three permitted forms (`:315-317`, `epics.md:4075-4076`) | A normative projection or an attestation cannot hash to the full-file SHA-256, so an implementer either rejects two of the three forms or verifies a different thing. | "A copy verifies by file SHA-256, a normative projection binds the source SHA-256 plus its own normative digest, and an attestation is a signature over the source SHA-256. All three bind repository, path, and commit." |
| M10 | medium | Story constraint lists vs the governing ADs: 2.13 omits AD-34 and AD-36; 2.14 omits AD-33 and AD-36, while AD-27's namespace needs the AD-33 catalog, which has no owner; 3.17 omits AD-34; epics 5.5 omits AD-28 and AD-36, and its AC (`epics.md:4321`) lets "a DAPR app API token … or approved equivalent" prove the caller alone, while its "remains backlog" line is stale; 5.11 omits AD-36; 4.16 has no dependency on 3.19 or 5.7, yet binds its envelope into 3.19's profile and edits 5.7's ACLs, and 3.19's issuance depends on 4.16 | A story can satisfy its declared ADs and still violate the governing one. | "Each story's constraints name every AD whose rule names its artifact, and the Story 9.3 drift guard fails on an omission. Story 4.16 depends on 3.19's profile schema and owns envelope rows that 5.7 may not alter." |
| M11 | medium | AD-11's 24-hour attestation window (`:196`) vs Story 9.3's manifest, which binds `sprint-status.yaml` and every story record, files that concurrent loops edit continuously | The window measured from "last authored change" may never open for `READY`. | "Approval subjects bind a frozen snapshot digest; a later change creates a new subject instead of resetting the window of the frozen one." |
| M12 | low | Topic `{tenantId}.{projectionType}.projection-changed` with subscription `*.*.projection-changed` vs AD-36 exact topic binding and AD-34 component qualification | A projection type containing dots makes the binding ambiguous, and support for wildcard subscriptions varies by broker and has not been qualified. | "Projection types in topics follow a dot-free grammar declared in `Contracts`, and wildcard subscription is an AD-34-qualified operation per broker." |
| M13 | low | AD-5 / Story 4.16 "removing any one envelope mechanism makes the test fail" (needs a deliberate raw second writer) vs AD-1 (no private actor-state access) and AD-34 (no blanket test exemption) | The falsification fixture has no sanctioned category. | "Envelope falsification writers are AD-34 fault-injection rows confined to their exact test paths and never packable." |

## Requested interaction lenses → findings

| Lens | Findings |
| --- | --- |
| AD-36 vs AD-10 vs AD-28 vs AD-18 (mint, validate, audience; trusted effects; Operations) | C1, H1, H2, H8, M3, M7, M8. Operations (AD-31) inherits C1 for its human-authorized replay. |
| AD-8 Direct/PubSub vs AD-31 vs Story 2.13 options | H3, M4, M8 |
| AD-17 versions vs AD-33 vs generator vs AD-11 inventory | H4 |
| AD-34 register vs AD-26 digest vs AD-9 vs Story 3.17 | H7, C2, M13 |
| AD-5 envelope vs AD-25 fence vs AD-26 vs Story 4.16 | C2, H8 (fence acquisition by the channel alone), M10, M13 |
| AD-11 assurance vs AD-12 truthful FAIL vs seal | H5, H6, M11 |
| AD-27 vs AD-33 vs AD-36 vocabularies | M1, M7 |
| AD-35 vs AD-21/29/3 | C1, M5, M6 |

## Disposition

- No file other than this report was written.
- The proposed wordings are inputs to the owner's next spine update. None is adopted here.
- Under the AD-11 Assurance Control, this report is `tool-persona` evidence only.
- AD-26 stays `[ASSUMPTION]`, the spine stays `draft`, and every gate in the Implementation Status table keeps its current posture.
