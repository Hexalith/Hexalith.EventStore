# Architecture Update Review — Adversarial Divergence

**Date:** 2026-09-09  
**Subject:** `ARCHITECTURE-SPINE.md` update  
**Lens:** configured adversarial divergence, with security, data-integrity, brownfield, and cross-team seam checks  
**Verdict:** **FAIL** — the update closes most findings from the prior validation, but one remaining subscriber contract can still authorize silent poison-message loss, and four high-impact rules remain ambiguous enough for independently built units to diverge.

## Review boundary

This review compares the updated spine with the current EventStore/domain-service/Operations code, AppHost and deployment assets, release workflow, PRD, epics, and payload-protection specification. An architecture rule may intentionally describe a target not yet implemented; implementation drift is a finding only when the spine fails to identify the gap, scopes a gate incorrectly, or leaves the target contract open to incompatible implementations.

## Critical

### C1 — The durable capture-before-ack rule does not cover every live subscriber terminal disposition

- **Severity:** Critical
- **Location:** `ARCHITECTURE-SPINE.md:105-109` (AD-8) and `:249-253` (AD-31)
- **Evidence:** AD-8 defines duplicate, in-progress, gap, and checkpoint behavior but says nothing about terminal poison acknowledgement. AD-31 assigns dead-letter recovery to `Hexalith.EventStore.Operations`, then applies the no-ack rule only to “capture” outcomes. The current generic domain-event subscriber returns HTTP 200 for `FailedInvalidPayload` specifically so DAPR drops the message, while durable dead-letter attachment is optional (`src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs:47-60`). The current Operations receiver also returns HTTP 200 for `Unretainable` (`src/Hexalith.EventStore.Operations/Endpoints/DeadLetterOperationsEndpointExtensions.cs:110-119`). The epic's canonical contract is stronger: a live message is acknowledged only after the configured tenant/domain dead-letter path durably accepts it, otherwise it remains retryable (`epics.md:4734-4736`).
- **Impact:** A domain-service implementer can conform to AD-8 and reasonably treat invalid payload as terminal, while an Operations implementer can conform only to its own capture flow. The handoff between them remains optional. A malformed or unsupported event can therefore be acknowledged and permanently lost even though the spine claims AD-31 prevents dead-letter loss.
- **Fix:** Amend AD-8 or AD-31 with a platform-wide terminal-disposition invariant: every production subscriber, including the generic domain-event SDK route, may acknowledge poison only after a tenant/domain-scoped durable poison record with stable `MessageId` identity has been accepted by the configured sink; timeout, cancellation, unknown/unsupported outcome, hash conflict, capture failure, or unretainable evidence remains retryable or moves through a separately proven durable quarantine. Bind the DAPR subscription/dead-letter-topic configuration and Operations capture contract to the same rule and fingerprint. Keep `SkippedUnknownEventType` and `SkippedNoHandlers` explicit policy cases rather than implicit successful drops.

## High

### H1 — AD-26 accidentally blocks ordinary package and container releases, contradicting AD-11 and the existing release workflow

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:123-129` (AD-11), `:219-223` (AD-26), and `:375-377`
- **Evidence:** AD-11 authorizes manifest-governed package/container release mechanics. AD-26 then states without qualification that “Release or deployment is prohibited” until a production broker, overlay, shared-backend proof, secrets, ACLs, restore posture, and production gates are all proven. The deferred introduction repeats that release is prohibited. The current protected workflow intentionally publishes 14 packages and the `eventstore` image (`.github/workflows/release.yml:115-136`), while several of the named production-profile gates remain backlog by design.
- **Impact:** Release engineering cannot tell whether patch packages, prerelease packages, immutable candidate images, evidence-only releases, and the existing protected production-environment workflow are prohibited. One team can halt all artifact publication; another can interpret “release” as only workload promotion. Both readings are supported by the text.
- **Fix:** Replace the blanket term with the exact forbidden act: for example, “production workload promotion/deployment and any production-readiness claim are prohibited.” State separately whether immutable candidate/package/image publication remains permitted under AD-11 and whether a production-labeled image publication is itself promotion. Use the same phrase in the Deferred introduction.

### H2 — The idempotency deployment catalog and route catalog overlap without one named authority or atomic join

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:46-50`, `:209-217` (AD-25), and `:261-265` (AD-33)
- **Evidence:** The diagram presents “Route + idempotency catalogs” as separate. AD-25 requires every host to load a deployment catalog keyed by `(Domain, CommandType)` that binds adapter, operation, descriptor, key generations, consumer identity, generation, and digest. AD-33 separately requires one platform route catalog keyed by `(Domain, MessageType)` / `(Domain, ProjectionType)` and binding app ID, method, contract version, and digest. Neither rule names the canonical schema/artifact/owner, whether AD-25 rows are embedded in or reference AD-33 rows, which digest is the deployment authority, or how a multi-host generation cuts over and rolls back atomically. Brownfield code demonstrates the exact fragmentation this decision must close: trusted idempotency adapters are keyed only by `CommandType` (`IdempotencyIntentAdapterRegistry.cs:30-68`), named projection entries are keyed by `AppId + ServiceVersion + Domain` (`NamedProjectionRouteCatalog.cs:24-52`), and query invocation resolves a separate `DomainServiceRegistration` before calling a fixed `query` method (`DaprDomainQueryInvoker.cs:25-38`).
- **Impact:** Gateway, admission, domain invocation, projection dispatch, AppHost, and ACL tooling can each produce locally valid but mutually incompatible catalogs. A route generation can change while the descriptor/key generation does not, or vice versa, producing wrong-service invocation, rejected legitimate retries, or replay under a different canonical intent.
- **Fix:** Name one owning package and deployable artifact/schema. Define a stable row identity and relationship: either one content-addressed catalog envelope containing route and idempotency facets, or two manifests joined by an immutable route-entry ID and one root digest. Specify canonical serialization, generation/activation state, signatures or trusted delivery boundary, prepare/ready/commit/rollback protocol, and the exact startup/readiness comparison each host performs. Until this exists, put the catalog-authority design under Deferred with a production-blocking owner and trigger.

### H3 — AD-30 collapses an in-MVP projection-erasure seam and a post-MVP full GDPR/payload-erasure workflow into one adopted rule

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:243-247` (AD-30), `:373`, and the full-erasure Deferred row
- **Evidence:** AD-30 binds FR5 and FR37 in one workflow and mandates subject freeze, projection/checkpoint erasure, payload-key invalidation, an `Erased` delivery, and logical/projection/cryptographic/broker/backup/legal-hold facets. The PRD explicitly separates generic projection read-model/checkpoint erasure as MVP scope from full GDPR aggregate/event tombstoning, broker-history deletion, physical backup erasure, provider key custody, and the optional payload engine as post-MVP/out-of-scope work (`prd.md:382-398`). The payload specification also withholds final erasure while replicas, exports, backups, restore points, caches, and provider state remain unaccounted for.
- **Impact:** Story 1.14 can be incorrectly blocked on Epic 8 and operational GDPR capabilities, or a team can claim the full AD-30 workflow delivered after implementing only generic projection erasure. The same word “erasure” would denote incompatible completion boundaries and evidence certificates.
- **Fix:** Split the rule into two phase-explicit contracts. Keep MVP read-model/checkpoint erasure under AD-7 (typed, idempotent, read-back-proven, with no GDPR/crypto-erasure claim). Retain AD-30 for the post-MVP/full workflow and explicitly state that it is not authorized or delivered by Story 1.14, FR5, a projection-erasure response, or the approved Epic 8 specification alone. Define the stable workflow identity that relates both without merging their completion states.

### H4 — The spine binds NFR3 but omits the shared JWT contract that prevents host-by-host authentication drift

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:117-121` (AD-10), `:155-159` (AD-16), `:231-235` (AD-28), and `:278`
- **Evidence:** The security rules cover authorization, fallback policy, and DAPR app-channel tokens, but no rule selects the platform JWT configuration contract. NFR3 requires the EventStore gateway, Admin Server Host, and Sample API to share exact production/break-glass behavior, an explicit asymmetric algorithm allowlist, HTTPS metadata outside Development, issuer/audience/signature/lifetime validation, and 60-second skew (`prd.md:317-320`). Brownfield code has already centralized those semantics in `Hexalith.EventStore.ServiceDefaults.Authentication.JwtBearerAuthenticationContract`, but the spine neither ratifies that type/package nor prevents a host from copying or replacing it. The current EventStore host separately registers JWT plus a DAPR-internal scheme (`ServiceCollectionExtensions.cs:60-103`), illustrating why the boundary needs a named composition rule.
- **Impact:** New generated API hosts, Admin, or consumer hosts can satisfy the generic AD-10 wording while selecting different algorithms, key posture, discovery requirements, or clock skew. This is a security and interoperability divergence at precisely the separately built host boundary the spine claims to bind.
- **Fix:** Amend AD-10 or add a decision naming ServiceDefaults as owner of the one versioned JWT validation/configuration contract. Ratify NFR3's exact allowlist and environment behavior, require all JWT-binding hosts to consume that contract rather than reimplement it, and make contract/config fingerprint mismatch a readiness failure. Keep DAPR app authentication under AD-28 as a distinct scheme with explicit selection rules.

### H5 — Authoritative deployment documentation remains an unsafe alternate architecture source

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:111-115` (AD-9), AD-6, AD-24, AD-26, and Deferred
- **Evidence:** AD-9 requires AppHost and DAPR YAML to change together but omits operator/deployment documentation from the consistency unit. `deploy/README.md` still tells consumers that `cloudevent.id` is `{correlationId}:{sequenceNumber}` (`:46`), contradicting AD-6 and the current publisher, which sets it to `EventEnvelope.MessageId` (`EventPublisher.cs:198-200`). The same guide recommends Kubernetes Secrets or Azure Key Vault and instructs operators to create Kubernetes Secrets for connection strings (`deploy/README.md:127-137,148-162`), contradicting AD-24's OpenBao-only application/operational secret posture. The epics already classify this documentation reconciliation as backlog rather than delivered (`epics.md:4143-4160`).
- **Impact:** Operators following the tracked deployment guide can deploy an architecture explicitly forbidden by the spine, and consumer teams can implement the wrong deduplication identity. Because the document is under `deploy/`, treating it as merely stale prose is unsafe.
- **Fix:** Extend AD-9's atomic topology unit to include deploy/operator docs and machine-checkable examples. Add the known documentation conflict as a named production gate with owner and trigger until reconciled. State which artifact wins on conflict and ensure CI validates CloudEvent identity and secret-store guidance against the catalog/components.

## Medium

### M1 — The spine's altitude understates its actual platform-wide authority

- **Severity:** Medium
- **Location:** `ARCHITECTURE-SPINE.md:2-7`
- **Evidence:** Frontmatter labels the document `altitude: feature`, while it binds all FR1-FR37 and NFR1-NFR19, selects platform runtime, security, release, routing, persistence, operations, UI, and consumer-removal rules, and describes itself as a platform.
- **Impact:** Downstream tooling or reviewers can apply a feature-level rigor/ownership model and miss system operational dimensions or inherited-platform conflicts.
- **Fix:** Set altitude to the workflow's platform/system value, or narrow the stated scope and binds to a true feature boundary.

### M2 — Decision acceptance and delivery status are still easy to conflate

- **Severity:** Medium
- **Location:** `ARCHITECTURE-SPINE.md:61-265`, especially AD-28 and AD-31
- **Evidence:** Some headings have `[ADOPTED]`, some have no status, and no legend defines the distinction between accepted architecture and implemented/proven capability. AD-28 is marked adopted while current EventStore authentication creates `global_admin` solely from allowlisted `dapr-caller-app-id` (`DaprInternalAuthenticationHandler.cs:12-45`) and has no shared app-channel-token middleware. AD-31 is marked adopted while its rule correctly says Operations is not production-wired.
- **Impact:** Planning, release, or consumer-parity evidence can cite “ADOPTED” as delivery even where the code directly violates the target rule.
- **Fix:** Normalize every AD to one decision status vocabulary and add a short legend: decision status is not implementation status. For rules with a known brownfield gap, add a compact `Current gap / gate` sentence or a separate implementation-status matrix linked to evidence; never overload `[ADOPTED]` as proof.

### M3 — The reserved `system` tenant rule needs a public-provisioning prohibition

- **Severity:** Medium
- **Location:** `ARCHITECTURE-SPINE.md:225-229` (AD-27)
- **Evidence:** AD-27 says `system` is reserved for cataloged platform operations, but does not explicitly carry NFR2's requirement that tenant provisioning reject the reserved name (`prd.md:318`). Current internal authentication manufactures `system:{appId}` subjects with global-admin authority, so “system” already appears in a separate identity namespace.
- **Impact:** Tenant provisioning and internal platform-operation implementations can disagree over whether `system` is a valid managed tenant, a principal prefix, or an operation scope, risking cross-tenant keys and authorization collisions.
- **Fix:** State that `system` is never a managed/provisionable tenant and is rejected at every public tenant boundary. Define its internal namespace separately from tenant IDs, require cataloged operation scope plus AD-28 authentication/authorization, and forbid using a `system:*` subject to synthesize tenant or global-admin grants.

## Positive evidence

The update materially resolves the prior validation's biggest omissions: AD-26 now names a fail-closed PostgreSQL production evidence profile; AD-27 establishes canonical tenant normalization; AD-28 rejects caller-app ID as authorization; AD-29 establishes dual attribution and resumable audit semantics; AD-32 separates diagnostic correlation from status identity; AD-19 names the actual `ProjectionDispatchResponse` / `ProjectionDispatchOutcome` contract; AD-5 explicitly rejects null/stale fences; and the structural/topology sections distinguish the current AppHost from gated production targets.

## Gate recommendation

Do not finalize until C1 and H1-H5 are patched or explicitly deferred with production-blocking owners. M1-M3 are safe autofixes in the same update. After changes, rerun architecture lint and a focused adversarial pass over AD-8/AD-31, AD-11/AD-26, AD-25/AD-33, AD-7/AD-30, and AD-10/AD-28.
