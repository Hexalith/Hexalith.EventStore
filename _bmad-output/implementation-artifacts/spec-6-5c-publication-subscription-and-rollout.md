---
title: 'Story 6.5c: Publication, Subscription, and Rollout Spec'
type: 'feature'
created: '2026-09-27'
status: 'in-progress'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

## Intent

Specify stable publication and effect bytes, subscriber admission, delivery disposition, and mixed-version rollout as an input to the single Story 6.5 AD-13 artifact. This story changes no runtime behavior.

## Boundaries & Constraints

One committed MessageId retains one pinned publication identity through retry. Every addressed logical route has a durable completed or authenticated filtered result before physical acknowledgement. Poison, unavailable broker/provider evidence, or a failed route never becomes silent success. Existing signed fixture literals and public compatibility remain intact; no independent approval of Story 6.6 is granted.

## Tasks & Acceptance

- [x] Inventory outbox, broker, transport, subscription, marker, effect, dead-letter, and operator-evidence paths in this slice.
- [x] Propose exact publication/delivery codecs and pins, membership and route authority, per-route effect receipts, duplicate and poison handling, legacy JSON-to-binary handoff, and bounded capture/readback.
- [x] Propose reader-first and writer-cutover readiness, provider probes, key retention, mixed fleet rollback, package compatibility, and support-safe diagnostics.
- [x] Resolve `BH37-9`'s historical UTC-ticks versus original-offset wording with one normative delivery-digest rule and a byte-level vector; record its explicit disposition.

**Acceptance Criteria:**

- Given initial publication, redelivery, multi-route subscription, or legacy handoff, when bytes and effects are verified, then the same MessageId preserves exact authenticated evidence, each route has at most one effect, and acknowledgement waits for the complete addressed route set.
- Given changed bytes, membership, keys, broker claims, or a malformed/oversized carrier, when rollout or delivery proceeds, then readiness or admission holds with a typed outcome and no silent acknowledgement or payload disclosure.
- Given Story 6.5 integration, when this work is reviewed, then its section candidate and finding disposition are ready for reconciliation with Stories 6.5a and 6.5b; this child story alone authorizes no runtime work.

## Candidate authority and source inventory

This is a **documentation candidate**, grounded at `e29b44a2d185b01ddafe534672153308285b4444`. “MUST” below means proposed Story 6.6 behavior, not an implemented guarantee. The [single AD-13 draft](spec-event-versioning-upcasting.md) remains unchanged with its §12 `UNAPPROVED` receipt; Story 6.6 is unauthorized. [6.5a](spec-6-5a-event-contract-writer-and-migration-evidence.md) A3–A8 and [6.5b](spec-6-5b-verified-read-replay-and-projection.md) B2/B6/B8 replace narrower draft wording as identified here. The [epic context](epic-6-context.md) supplies complete-prefix, bounded-cost and compatibility constraints; Epic 8 remains optional.

| Boundary | Current source and test evidence | Required 6.5c seam |
| --- | --- | --- |
| Actor outbox/publication | [AggregateActor](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs) saves events then invokes [EventPublisher](../../src/Hexalith.EventStore.Server/Events/EventPublisher.cs), which unprotects/restamps and calls generic DAPR object publication; range recovery retains identity but rebuilds bytes. [Unreadable publisher tests](../../tests/Hexalith.EventStore.Server.Tests/Security/UnreadableProtectedDataBehaviorTests.cs) check readability, not exact pins. | 6.5a A5 same-save outbox/result/sidecar readback, one global MessageId binary pin and broker acceptance receipt. |
| Subscriber, marker, effect | [Processor](../../src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs) shares a MessageId marker across CLR handlers; [DAPR marker store](../../src/Hexalith.EventStore.Client/Subscriptions/DaprEventStoreDomainEventMarkerStore.cs) reads on acquisition without persisting a lease. [Marker tests](../../tests/Hexalith.EventStore.Client.Tests/Subscriptions/EventStoreDomainEventMarkerStoreTests.cs) and [processor tests](../../tests/Hexalith.EventStore.Client.Tests/Subscriptions/EventStoreDomainEventProcessorTests.cs) document current skips/completion. | Stable logical HandlerRouteId, durable lease, effect key/receipt per route, complete physical route manifest. |
| Ingress | [Domain event endpoint](../../src/Hexalith.EventStore.DomainService/EventStoreDomainEventsEndpointExtensions.cs) binds a DTO and returns 200 for invalid/no-handler skips, as [endpoint tests](../../tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainEventsEndpointExtensionsTests.cs) expect. | Bounded raw Binary/structured/legacy adapter and verified transport context before DTO/marker/handler. |
| Poison/operator evidence | [Dead-letter parser](../../src/Hexalith.EventStore.Operations/Capture/DeadLetterEnvelopeParser.cs) recognizes legacy JSON; [capture endpoint](../../src/Hexalith.EventStore.Operations/Endpoints/DeadLetterOperationsEndpointExtensions.cs) defaults to 1 MiB and acknowledges oversize/conflict/unretainable; [drain actor](../../src/Hexalith.EventStore.Operations/Actors/DeadLetterDrainActor.cs) saves current bounded item/index. [Capture tests](../../tests/Hexalith.EventStore.Operations.Tests/DeadLetterCaptureBodyTests.cs) assert oversize 200; [actor tests](../../tests/Hexalith.EventStore.Operations.Tests/DeadLetterDrainActorTests.cs) cover current item/index. | AD-31 exact bytes/headers, tenant-scoped encrypted retention and authenticated readback before poison ack. Current tests describe a gap. |
| Public status/deployment | [CommandStatus](../../src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs) assigns terminal `PublishFailed=6`; [status response](../../src/Hexalith.EventStore/Models/CommandStatusResponse.cs) carries optional retry/recovery fields, and [status controller](../../src/Hexalith.EventStore/Controllers/CommandStatusController.cs) omits `Retry-After` for terminal states, as [status tests](../../tests/Hexalith.EventStore.Server.Tests/Commands/CommandStatusControllerTests.cs) verify. | 6.5a A8 immutable first POST pin, latest status head and the terminality proof below. |

## C1. Exact publication and transport contract

Import draft §4's checked big-endian `U`, `B`, `B32`, `N`, `I`, `T`, `Q`, `O(X)` and `M` codecs, strict UTF-8/JSON, no ordinal or case-folded duplicate names, and preallocation bounds. `T` is signed i64 **UTC instant ticks followed by signed i16 original offset minutes** in `[-840,840]`, consistent with DateTimeOffset; `Q` is a UTC instant only. Import draft §7's exact 37-field `HX-EV-DELIVERY-BODY-1\0 || 01 || 0025 || tags 01..25`: tag `09` stored Timestamp is `T`, tag `21` optional CloudEvent time is `O(T)`. The body has no digest, key ID, signature or attestation field. `DeliveryDigest = SHA256("HX-EV-DELIVERY-1\0" || 01 || B(exact body bytes))`. Its fields are: `01` StoredDigest B32, `02` MessageId U, `03..06` tenant/domain/aggregate ID/type U, `07..08` sequence/global position N, `09` T, `0a` correlation U, `0b..0d` causation/user/DomainServiceVersion O(U), `0e` stored EventTypeName U, `0f` stored MetadataVersion I, `10` stored canonical type O(U), `11` stored payload version O(I), `12` stored format U, `13` readable format U, `14` **original readable** payload B, `15` readable extensions O(M), `16` protection state U, `17` protection metadata version I, `18..1a` scheme/key alias/content hint O(U), `1b` flags O(M), `1c..1f` CloudEvent specversion/id/type/source U, `20` subject O(U), `21` time O(T), `22` content type U, `23..24` component/topic U, `25` six routing headers M. Subscriber compares every consumed metadata field, including original offset, to authenticated source and signed claim. Publisher never upcasts; subscriber creates a current purpose-01 view only after source admission. `ReadableExtensions` and protection flags retain absent/null/empty `O(M)` distinctions; first pin copies the stored extension map and replaces only the exact `eventstore.protection` key with canonical provider-returned unprotect metadata. A case-variant-only historical protection key holds for approved mapping and is never silently normalized. Extension limits are 64 entries, 256 UTF-8 bytes/key, 4,096 bytes/value, 256 KiB canonical map and 512 KiB complete encoded event metadata. Preflight retained history; valid old data beyond a new capability cap holds readiness, while authenticated permanent schema/metadata violation is poison only after AD-31 capture.

At actor commit, 6.5a A5 **codec-02** intent/receipt and member root bind exact immutable event, encoding/digest sidecars, outbox and **domain/append result payload hash**, including rejection events. The draft §7 outbox intent `HX-EV-OUTBOX-INTENT-1\0 || 01 || 0012` stages exact source URI, component/topic, routing map/policy, destination configuration digest/bytes or immutable reference, optional subject/time `O(T)` and stored-form core derivation in the same save. Its tag `12` is one `B` containing `00 || inline configuration` or `01 || complete immutable configuration-reference record`, with no nested second `B`; readback resolves and hashes the exact configuration. Mutable publisher options never fill absent intent. An old outbox without authenticated intended-route evidence holds for separately approved migration. Derive the first pin only from authenticated original source/readability and that read-back intent, after A5 complete generation readback and globally Committed batch root. Same-ID append retry compares the **entire** original batch/result/fingerprint; a member proof grants only one already committed member's publication/effect check.

The exact global MessageId CAS pin contains decoded body, purpose-02 `HX-EV-ATTEST-1\0 || 01 || 0003` claim/key ID/64-byte P1363 signature, DeliveryDigest, StoredDigest, scope, old approved RegistryFingerprint, signed claim tag-`0a` issuance UTC, routing intent hash, renderer version, actual outbound bytes per mode and hashes. Key is the **exact MessageId**, never `(MessageId,digest)`; changed body, header, destination, scope or same-mode rendering conflicts. First-pin recovery requires durable no-prior-send proof; ambiguous prior send holds. Pin capacity is reserved before send (`PublicationPinCapacityHold`). Its signed historic obligation remains through queue, broker retry, quarantine, rollback and late-delivery obligations. Only after durable closure may a CAS-authenticated global-MessageId tombstone replace bulky bytes; it retains hashes/lengths, scope/digests, original key/interval and completed route receipts for the enforceable maximum redelivery horizon. Without a finite broker horizon keep full pin bytes. Missing historic key/pin/source holds, never permits a new send or false duplicate.

Transport is exact binary `application/vnd.hexalith.eventstore.v2+octet-stream` body with canonical padded Base64 `ce-hyevattestation`, or sorted structured CloudEvents JSON with canonical padded `data_base64` and `hyevattestation`. Both modes strictly decode to the same body/attestation and signed `specversion=1.0`, id/type/source, optional subject/time with original offset, content type, component/topic and six `hx-*` routing headers. The six are `hx-tenant-id`, `hx-domain`, `hx-aggregate-id`, `hx-aggregate-type`, `hx-event-contract-type`, `hx-payload-version`; structured mode keeps them outside the CloudEvent object. V1 header type/version comes from exact registered domain alias descriptor while stored body pair remains absent/null. Case-fold only legal HTTP header-name case before duplicate detection; reject changed values, case-fold duplicates, unknown `hx-*`, unknown CloudEvent attributes, alternate Base64 and noncanonical mode rendering as `DeliveryPinConflict`. Non-`hx-*` broker headers cannot route. All six signed headers are required on new deliveries and sorted by unsigned UTF-8 name; `hx-payload-version` is canonical unsigned ASCII decimal 1..1024 without sign, space or leading zero. Generic object publication/DTO binding does not satisfy this contract. Same-mode retry uses retained exact outbound bytes; a broker mode conversion is accepted only after strict decode and canonical re-render to the same pin.

Before parsing, Base64 decoding, JSON DOM creation, signature verification, or buffer allocation, admission obtains a streaming length bound on **each** byte domain: decoded delivery body ≤128 MiB, decoded attestation ≤8 KiB, encoded attestation header/JSON value ≤16 KiB, complete Binary transport body ≤128 MiB, and complete structured JSON transport body ≤192 MiB. Canonical padded Base64 of a 128 MiB body is exactly 178,956,972 bytes; a structured carrier is admitted only when its exact canonical JSON length is at least that value plus its actual bounded attributes/attestation and remains ≤192 MiB. Binary body length equals decoded body length. All broker/application headers together are ≤64 KiB of exact name/value bytes including separators, ≤128 entries and ≤8 KiB per name/value pair; the complete body-plus-header/framing carrier is ≤193 MiB. Only `Binary` and `Structured` are legal modes. All domains and the mode/length relationship are independently checked before growth; a tuple of individually small but physically impossible lengths is invalid. The carrier is parsed/decoded incrementally in bounded chunks; a 192 MiB JSON DOM or full decoded-plus-encoded copy is never allocated. Length-prefixed fields, JSON strings, Base64 output, sorting scratch and signatures are charged before growth; overflow, unknown length, truncated stream or unsupported provider streaming limit yields `DeliveryCarrierLimitHold` and non-2xx, with no parsed identity or effect. A production probe must prove these bounds through broker, sidecar, ingress and AD-31 capture without a larger intermediate allocation. An over-limit carrier can be acknowledged only through C4's separately proved exact-full-byte physical quarantine; a truncated hash or captured prefix grants no authority.

### BH37-9 disposition and byte vectors

**Accepted historical wording conflict; superseded here.** [Loop-7](story-6-5-design-notes.md) says CloudEvent time uses UTC ticks only. Draft §4 `T`, draft §7 tag `21` `O(T)` and [Loop-9](story-6-5-design-notes.md) require the original offset. Equal instant with offsets 0 and +60 minutes has distinct `T` suffix `0000` and `003c`; canonical CloudEvent text has seven fractional digits with `Z` or original `+HH:MM`/`-HH:MM`. No UTC-offset normalization occurs in body, claim, digest or pin. Starting with **unchanged** 842-byte draft `V17DeliveryBody`, changing **only** tag `09` offset `0000→003c` changes digest `54a83b017e8357120374f60a175c438f48b0cd80013e065aa93360670a3f22ad` to `fa9dcf5c6ce3b8fe79a1b4bb6fee665f65431c8be5e10ac4af34ced59e6fb02e`. Making tag `21` present with the same UTC ticks yields two 852-byte bodies with digest `36cbbdd4d189651f90a150e0cccdd50a78495809e6decd9adc0296195c2444a4` at offset zero and `6a5b849b4f8399eb61a32becfaa496d970ff4e55a626737895c0bbdd4d70fcc1` at +60. The executable checks below derive all four without altering the historical literal.

## C2. Broker membership, acceptance and recovery

Import draft §7's exact signed `MembershipClaim` (`HX-EV-MEMBERSHIP-1\0 || 01 || 0008`), historical `ConsumerLeaseClaim` (`HX-EV-CONSUMER-LEASE-1\0 || 01 || 000f`), `PublicationFenceClaim` (`HX-EV-PUBLICATION-FENCE-1\0 || 01 || 0009`) and production `ConsumerConfiguration` **codec 02/000f**. Do not alter V23's historical configuration/lease literal bytes. Membership tags `01..08` are U domain/component/topic, N positive revision, B32 current domain RegistryFingerprint, B sorted member set, Q issued/expiry UTC. Each member is `U logical consumerIdentity || B32 exact configurationHash`, sorted unique by unsigned UTF-8; one logical identity is derived from `(physical subscription ID, HandlerRouteId)`. Consumer configuration codec-02 tags `01..0f` are U consumer/domain/component/topic/physical subscription/route/type selector/tenant rule, B32 binary capability hash, U duplicate mode, U effect mode (`AtomicStore` or `ProviderIdempotency`), U provider ID, B32 V3 probe receipt digest, U backend identity, N active revision. Historical lease tags `01..0f` retain U consumer/domain/component/topic, N revision, B32 membership hash/configuration hash/RegistryFingerprint/capability hash, U duplicate mode/provider ID, B32 probe hash, U backend, Q issued/expiry. Resolve lease tag `07` through exact authenticated codec-02 configuration and verify effect mode at its tag `0b`; changed configuration with unchanged lease bytes fails. Fence tags `01..09` retain domain/component/topic, revision, membership hash/fingerprint, operation ID, expiry, 16-byte broker nonce. Every claim is signed with its purpose-specific broker authority under the draft §6 carrier, distinct from event trust; signatures, key purpose, intervals/revocation and exact member order must validate.

Production tag-`09` capability is `SHA256("HX-EV-BINARY-CAP-2\0" || 01 || B32 base carrier capability hash || B32 current HandlerCompatibilityHash || B32 EffectProviderConfigurationHash || B(exact route capability evidence))`, where `EffectProviderConfigurationHash = SHA256("HX-EV-EFFECT-CONFIG-1\0" || 01 || U effect mode || U provider ID || U backend ID || B32 canonical provider-options hash)`. Route evidence contains the exact `52` row, optional `43` row and sorted `58` extras, or the exact signed external manifest; an unchanged lease cannot conceal a changed effect mode, backend or handler. One physical subscription with two routes has two independent logical configurations/leases **and one** pinned send. Internal route capability binds exact primary `52`, optional verified `43`, all sorted extra `58` event-type/format rows, `HandlerCompatibilityHash`, filter and effect provider configuration. External routes require the purpose-`0b` signed external manifest and independently broker-attested transitive loader graph, signed closure and current V3 provider probe. A `*` selector means only the exact attested nonempty multi-type set, never arbitrary event types. Membership carrier is at most 1 MiB/4,096 members/4 KiB each; external route set at most 256 KiB/1,024 rows, full external carrier at most 2 MiB and closure at most 1 MiB. Check lengths/counts before nested allocation.

Production duplicate-safety readiness requires draft `HX-EV-DEDUP-PROBE-3\0 || 03 || 0015`, with **two different** route IDs, one injected crash between A's effect and receipt commit, A retry and A/B duplicates, exactly one final effect and complete `EventEffectReceipt` per route, plus the six-row purpose-`0c` provider-signed transcript. Require one V3 probe for every mode/backend/options/revision in use; V23's one-route probe is historical fixture evidence only. Broker independently queries full provider receipts and validates transcript/key/mode/backend/version/ETag before signing a lease. An effect-capable DI dependency outside the selected atomic write session or exact-key provider blocks readiness; a configured provider name cannot stand in for a probe.

At acceptance, `TryPublishAtRevision(tenant, domain, component, topic, expectedRevision, fenceNonce, operationId, ExactCanonicalPinDigest, exactModeTransportBytes)` atomically checks authoritative broker UTC, active member set, every lease/configuration/probe/fingerprint, revision and fence expiry/nonce **with accepting bytes**. `ExactCanonicalPinDigest = SHA256("HX-EV-BROKER-PIN-1\0" || 01 || B(exact decoded body) || B(exact decoded attestation))`. Publisher precheck alone is insufficient. Join, leave, filter/handler/provider/registry change advances revision and invalidates old fence/leases. Missing compare-and-accept is `ConsumerMembershipFenceUnavailable`. The broker enforces **one unique key** `(U tenant, U domain, U component, U topic, U memberSendOperationId)` before digest comparison; its immutable value binds the canonical pin digest, mode, exact original request-mode hash, accepted-mode deterministic renderer hash and authenticated parent binding. A different digest, mode, bytes or parent under that same key conflicts; the digest is never part of key selection. Every member send OperationId, including a future retry-nonce ID, requires `HX-EV-SEND-PARENT-1\0 || 01 || 000a`: `01` U authenticated tenant, `02..04` U domain/component/topic, `05` U original parent command OperationId, `06` B32 A5 tenant-scoped ScopeOpHash, `07` N A8 member position, `08` U event MessageId, `09` U member send OperationId and `0a` B32 exact outbox-intent/pin binding hash (`SHA256(B(exact outbox intent) || B(exact global pin))`). Its complete record/signature carrier is ≤16 KiB, reserved before A4 Prepared; its key is the unique broker operation key above. Authenticate complete same-save outbox/preparation evidence, signed send intent and broker parent-index CAS/readback before acceptance. The broker parent index is append-only under the authenticated tenant and ScopeOpHash parent namespace; an unbound, mismatched or late-invented send ID holds before acceptance. A terminal parent reject fence keyed by the authenticated tenant and original command ScopeOpHash is checked atomically across **every** A8 destination in that tenant **before** the unique-key duplicate path on every accept path, including delayed old and newly presented member IDs; a broker without that cross-destination atomic check cannot certify C5 terminality; a matching historical Accepted receipt remains queryable but cannot create new acceptance. Accepted is idempotent only for the matching unique key and immutable value. After uncertain send, authenticated `GetPublicationReceipt` by the unique operation key plus expected digest returns matching Accepted (record without resend), proved Rejected (renew fence/nonce for the same member send OperationId/pin) or Unknown/unavailable (hold without new nonce/send). Receipt retention covers pin, parent binding, retry, handoff and rollback obligations. Cancellation after send starts follows the same lookup under a bounded recovery token; caller cancellation cannot infer nonacceptance.

The tenant is taken from authenticated A5 admission and the signed send-parent/outbox image, never from an untrusted request header. Broker uniqueness, receipt lookup, parent-index CAS and terminal reject-fence checks use the same ordered tenant/domain/component/topic/send key and tenant/ScopeOpHash namespace. A duplicate send ID in another tenant is independent even when all other ID bytes match; a fence in one tenant cannot suppress another tenant's send. A signed parent record with a different tenant or a broker incapable of atomically fencing every destination in that tenant holds before acceptance. The accepted receipt and its immutable value carry the exact tenant and parent binding for readback. A new retry-nonce send OperationId under that parent is permitted only after authenticated readback of the prior member send ID and a class-01 retryable Rejected receipt, with no Accepted or unknown attempt anywhere in that member chain; class-02 terminal rejection cannot mint a new ID. Each new ID is signed and indexed under the same tenant/ScopeOpHash, original MessageId and immutable pin before acceptance.

An Accepted broker receipt freezes each addressed `(physical subscription ID, HandlerRouteId, configuration hash, membership revision)` as a historical execution obligation. The broker and coordinator refuse a join, leave, rename, filter, provider/backend change or lease replacement that would remove an owed route until its terminal result is authenticated, unless they first CAS-install an authenticated old-to-new execution mapping. `HX-EV-HISTORICAL-EXECUTION-GRANT-1\0 || 01 || 000e` is a ≤16 KiB ordered claim: `01..04` U domain/component/topic/physical subscription, `05` U old HandlerRouteId, `06` B32 Accepted-receipt hash, `07` B32 global pin hash, `08` B32 old EventEffectKey hash, `09` B32 old configuration hash, `0a` B32 old handler/effect/backend capability hash, `0b` B32 old membership/lease hash, `0c` U replacement endpoint or old endpoint identity, `0d..0e` Q issued/expiry UTC. It is signed with distinct purpose `13` by the configured broker membership authority, whose stable issuer identity, SPKI, interval and revocation state are pinned in the accepted revision's broker trust map. Verify exact bytes, purpose, issuer, signature, Accepted receipt, old key/configuration and broker UTC within `[issued,expiry)` on every invocation; a later revocation blocks the grant unless an authenticated safe replacement already owns the obligation. The grant authorizes **only** that old route/pin/effect key, no new publication or changed filter decision. The mapping binds this grant and Accepted receipt to the replacement endpoint/handler graph and provider mode/backend V3 probe. A replacement must query the old provider's exact-key receipt and every uncertain effect operation, then CAS-migrate authenticated completed receipts or install a fenced shared exact-key provider before any handler rerun. If the old provider is unreachable, an outcome is uncertain or a cross-provider transaction cannot prove one effect, hold mapping, membership transition and route; never rerun under a new backend. A mapping preserves the old HandlerRouteId and EventEffectKey; it cannot create a second effect or turn an incomplete route into `Filtered`. Grant validity or an authenticated exact-obligation renewal path must cover the full outstanding route horizon before a replacement revision activates; a grant expiry without renewal leaves the old route held under its original obligation and blocks that transition. Broker/coordinator readback proves drain or the exact mapping/grant and reconciliation before activating a revision. Expired/revoked current leases prohibit new acceptance but do not erase an already accepted obligation; unprovable historical authority is `ConsumerMembershipFenceUnavailable` and keeps physical acknowledgement pending.

## C3. Raw ingress, route effects and physical acknowledgement

Replace DTO-only `MapEventStoreDomainEvents` with bounded raw ingress that authenticates Binary or structured data, CloudEvent core, component/topic and `hx-*` application metadata into `VerifiedDeliveryContext` **before** DTO/marker/handler selection. It verifies purpose-02 signature, exact prior pin obligation where old RegistryFingerprint is approved, global Committed MessageId batch root and addressed member proof, fresh authenticated actor source/StoredDigest and current purpose-01 effective route/view. An old purpose-02 pin is valid only under original signed issuance/key interval and exact retained obligation; independently re-evolve the current view under active registry/trust. Missing old key/obligation or current source/view holds, not poison acceptance. DTO `isAdapted`, `verifiedEffectiveEvent` or caller headers never assert authority. The public unsealed six-argument `EventStoreDomainEventContext` and nullable `GlobalPosition` remain source/binary compatible; the additive immutable verified wrapper carries non-null signed global position where required, exact stored/effective provenance, route/digests and private proof copies. Old handlers remain callable only behind a proven effect-safe adapter.

At the authenticated physical membership revision, resolve the **complete** sorted addressed HandlerRouteId set. A valid addressed event with no handler is `HandlerCapabilityMismatch` and retries; a predeclared signed catalog filter may terminalize only its proven out-of-scope route. CAS-stage/read back `HX-EV-BINARY-HANDOFF-1\0 || 01 || 0011`: tags `01..08` U MessageId/tenant/domain/aggregate type/aggregate ID/component/topic/physical subscription ID, `09` N membership revision, `0a..0e` B32 StoredDigest/DeliveryDigest/pinned body hash/pinned attestation hash/route-set hash, `0f` B route set (`u32 count || sorted unique U HandlerRouteId`), `10` U stable delivery OperationId, `11` B32 pinned core/header/transport-rule hash. At most 4,096 routes and 1 MiB complete manifest. Its key hashes component/topic/physical subscription/MessageId; revision, digests and route set are **values** under that one key. Partial staging or changed evidence leaves physical delivery unacknowledged. A committed pointer authorizes the complete set only after all route records and pin read back. Duplicate admission checks global pin/tombstone, actor source, manifest and each route receipt first; another route's receipt never completes this route.

`EventEffectKey` is exactly `(tenant, domain, aggregateType, aggregateId, MessageId, DeliveryDigest, HandlerRouteId)`. Provider's canonical `HX-EV-EFFECT-RECEIPT-1\0 || 01 || 000e` (≤16 KiB) fields `01..07` are that seven-part key; `08` U provider operation ID, `09` B32 committed effect-result digest, `0a` U provider identity, `0b` B exact backend descriptor, `0c` U mode, `0d` N actual commit version, `0e` U actual ETag. `ReceiptDigest = SHA256(exact complete record)`; authenticate full bytes/readback, not DTO or digest alone. `AtomicStore` commits enlisted effects and unique-key receipt in **one** serializable transaction; `ProviderIdempotency` enforces the exact key on the external effect and exposes the same queryable result/receipt across restarts. Before handler invocation query the exact receipt. After crash/uncertain commit query again; a match finalizes that route without invoking handler. Changed key, mode, backend, ETag or result conflicts. A standalone marker or later receipt cannot emulate atomic safety. `HandlerCapabilityHold` occurs before effect/ack if the route lacks this contract and its V3 probe.

All C3 effect, route-decision, filtered, route-quarantine and physical-filter records have a 16 KiB complete encoded cap, including domain/version/tag framing; the C4 physical-quarantine receipt has the same cap. Every U field obeys A8's 1,024-byte encoded-value bound, and the effect receipt's backend descriptor B is at most 1,024 bytes. Before A4 Prepared, preflight the worst-case complete encoded record for each planned member/route using full admitted identifiers, provider IDs, backend descriptor, ETag, signed claim and reference lengths; reserve its record slots, effect-provider transaction capacity, B6 scratch and A8 referenced-evidence quota. Before any handler/effect invocation, redo exact-byte preflight for the accepted revision and actual provider descriptor/ETag bound. A provider that cannot atomically store/read back the full receipt and terminal decision under those caps cannot advertise V3 readiness. If a valid admitted route cannot fit, hold before Prepared or before invocation; never commit an effect that cannot be receipted. The retry policy's maximum attempts must also fit the 64 MiB complete referenced-evidence reservation with the 16 KiB receipt maximum, or admission lowers the signed maximum before Prepared. No identifier is truncated or assigned a new public cap.

A route has exactly **one** stable terminal-decision key, `route-decision:` plus SHA-256 of the C3 handoff key and `U HandlerRouteId`, independent of attempt ID or outcome. `HX-EV-ROUTE-DECISION-1\0 || 02 || 000d` (≤16 KiB) has ordered `01..05` U component/topic/physical subscription/MessageId/HandlerRouteId, `06` B32 exact handoff hash, `07` B32 global pin hash, `08` U state (`EffectReserved`, `Completed`, `Filtered`, `Quarantined`), `09` O(B32) exact referenced effect/filter/quarantine receipt hash, `0a` O(U) reservation owner ID, `0b` O(N) positive monotonically increasing fence generation, `0c` O(Q) lease expiry UTC and `0d` O(B32) exact latest append-only takeover-record hash. `EffectReserved` has absent receipt hash, present owner/fence/expiry, and a takeover-chain hash only after takeover; `Completed` retains the winning owner/fence, has absent expiry, present receipt and a takeover-chain hash exactly when taken over; `Filtered`/`Quarantined` have absent owner/fence/expiry/reconciliation fields and present receipt. No null stands for an absent field. CAS/readback under one ETag permits absent→`EffectReserved`→`Completed` or absent→`Filtered`/`Quarantined`; an expired reservation may only CAS-renew or CAS-transfer to `EffectReserved` with a higher fence. No terminal state changes. The decision key lives in the effect provider's same linearizable transaction namespace; a two-store sequence unable to enforce the exclusive reservation and completion is unsupported.

Reserve for at most 60 seconds using the provider's authoritative UTC; renewal requires the current ETag, owner and fence before expiry. Every effect invocation carries that fence, and the provider must atomically exclude an older fence from new or late commits. A takeover after expiry first CAS-excludes the old owner, then queries the old fence's exact uncertain effect receipt and all pending commits under the same `EventEffectKey`; it binds the authenticated query/absence result in the append-only chain below. If the old effect committed, finalize its receipt without invoking again. Invocation under the new fence is permitted only when the provider proves that the old fence cannot commit later and that no effect/receipt exists; ambiguous or unavailable reconciliation holds. The old owner may never invoke after exclusion, and a second owner may never invoke concurrently. `AtomicStore` commits effect, receipt and `Completed` decision in one serializable transaction guarded by the live fence. `ProviderIdempotency` uses the reserved exact key for its external effect, queries an uncertain result after crash and finalizes `Completed` only from that authenticated receipt; a reservation never authorizes 2xx. Filter/quarantine CAS requires an absent decision and proved no reservation, including an expired one; timeout does not convert an effect into poison or filtered. Receipt records in separate locations have no terminal authority without the matching decision/readback. Every duplicate verifies the decision's exact handoff, pin, owner/fence history, evidence bytes and referenced receipt; a conflicting terminal outcome holds as an incident.

Each transfer appends `HX-EV-EFFECT-TAKEOVER-1\0 || 01 || 000d` under the **same route-decision key namespace** plus `:takeover:` and checked positive new-fence `N`, with ordered `01` B32 exact route-decision key hash, `02` B32 handoff/pin hash, `03` N old fence, `04` U old owner, `05` U old decision ETag, `06` Q old lease expiry, `07` N new fence, `08` U new owner, `09` B32 exact provider old-owner-exclusion receipt hash, `0a` B32 exact old-key uncertain-effect query/receipt hash, `0b` B32 predecessor takeover-record hash (all zero for the first), `0c` Q provider takeover UTC and `0d` U new decision ETag. The signed/provider-authenticated complete record and exact exclusion/query source images are ≤8 KiB and ≤16 KiB respectively, CAS-create once and read back in the provider's decision transaction. The provider must allocate and expose the new decision ETag inside the same linearizable CAS transaction before sealing tag `0d` of the takeover record, then prove that exact ETag in the authenticated postcommit readback; a provider that cannot do so cannot advertise fenced takeover. The decision's tag `0d` is the **latest complete takeover-record hash**, not a replaceable reconciliation assertion. Its first transfer links from the initial reservation's owner/fence/ETag; every later record must name the immediately preceding record, prior new owner/fence and decision ETag. Verify the full ordered chain from fence 1 to tag `0d`, each old-owner exclusion and uncertain-effect result, exact predecessor hashes, provider ETags and final receipt before invoking or acknowledging. Reserve up to 64 transfers and their ≤2 MiB total record/source evidence before first reservation; a 65th transfer or missing/ambiguous prior record holds the route and requires separately authorized recovery, never chain truncation or a reset fence. An accepted historical route/provider mapping retains this chain with the unchanged `EventEffectKey` through the maximum delivery/rollback horizon.

Takeover tag `02` is exactly `SHA256("HX-EV-EFFECT-TAKEOVER-BIND-1\0" || 01 || B(exact committed C3 binary handoff manifest) || B(exact global MessageId pin) || U tenant || U domain || U aggregateType || U aggregateId || U MessageId || B32 DeliveryDigest || U HandlerRouteId || U accepted providerId || U accepted backendId)`. B uses draft §4's checked four-byte length and hashes the complete retained bytes in streaming order; the scoped identifiers are the authenticated accepted EventEffectKey and configuration, each within A8's U bound. The accepted provider/backend identity stays fixed across every takeover even if a later mapping uses another provider. Read back the exact handoff, pin, key and accepted configuration, recompute tag `02` for every chain record, and require identical bytes and hash across the chain. A missing old pin/handoff or changed provider identity holds before another invocation.

Physical acknowledgement for **either Binary or legacy JSON** requires the committed handoff pointer and **every** addressed route's authenticated durable terminal decision and matching `Completed` effect receipt or permitted `Filtered` proof read back. A separately proven permanently invalid source uses a matching `Quarantined` decision and capture proof below; handoff alone is never a route result. One completed and one failed/unavailable route remains unacknowledged; redelivery resumes only incomplete routes. Empty addressed set is success only under C3's exact signed physical-filter receipt or C4's unidentified physical quarantine proof. `InProgress` acquisition needs a persisted fenced lease; current DAPR read-only acquisition is insufficient. Old `Completed`/`Dispatched` markers migrate by CAS only with independent scoped actor plus route-specific receipt/outbox/catalog proof and global-ID inventory; `Dispatched` finalizes from receipt, old `InProgress` retries only with proved idempotency. Ambiguous evidence holds. No-handler, unavailable key/provider, new capability/limit hold, cancellation or pin mismatch never maps to 200. Deterministic authenticated permanent schema/identity poison may be acknowledged **only after** exact-byte AD-31 durable capture/readback or the C4 authenticated broker-owned full-byte reference **and** one durable `Quarantined` decision/receipt for every addressed route. Its distinct canonical receipt is `HX-EV-ROUTE-QUARANTINE-1\0 || 01 || 000a`: `01..05` U component/topic/physical subscription/HandlerRouteId/MessageId, `06` O(B32) StoredDigest, `07` O(B32) DeliveryDigest, `08` B tagged authority reference, `09` B32 exact carrier-byte-and-header hash, `0a` B32 exact authenticated source/retention proof hash. Tag `08` is exactly `00 || U AD-31 capture key || B32 exact AD-31 item/index proof hash` or `01 || U broker immutable full-byte reference ID || B32 exact broker reference proof hash`, enclosed once by `B`; other discriminators, untagged strings and bare hashes are invalid. The `00` verifier decrypts and reads back the complete AD-31 item and index, checking exact bytes, headers, tenant/scope, ETag, proof and retention. The `01` verifier uses the accepted broker authority to resolve and read back the **entire** ≤193 MiB carrier and every header under its immutable ID, then checks signed reference/retention proof, byte-for-byte equality and the horizon. Neither authority's proof is interchangeable with the other. It is ≤16 KiB, keyed by physical delivery plus route ID under the handoff, create-once/read back, and its optional digests cannot be fabricated when parsing failed; the 128 MiB local capture limit never turns a larger structured addressed carrier into an uncloseable poison route. Compare exact source, route set, captured/referenced bytes and retention on duplicate. It records no effect and cannot stand in for `Completed` on a valid event, missing handler or unavailable capability. Only after all decisions, receipts, capture/reference and manifest readback may poison receive 2xx. Capture never permits replay/projection checkpoint advancement. An untrusted carrier is not proven permanent source poison.

The catalog filter predicate is an independently signed, accepted-revision claim, not a mutable catalog lookup. `HX-EV-CATALOG-FILTER-1\0 || 01 || 0011` (claim plus signature carrier ≤4 KiB) uses draft §4 ordered tags: `01` U issuer ID; `02..05` U domain/component/topic/physical subscription; `06` U literal `Route` or `Physical`; `07` O(U) HandlerRouteId (present only for `Route`, absent only for `Physical`); `08` N positive accepted membership revision; `09` B32 accepted membership-claim hash; `0a` B32 exact accepted route or physical-filter configuration hash; `0b` B32 accepted RegistryFingerprint; `0c` B32 accepted transform implementation/manifest hash; `0d` U literal `HX-EV-SELECTOR-1`; `0e` B exact expression (≤2 KiB); `0f` U literal `HX-EV-FILTER-INPUT-1`; `10..11` Q issued/expiry UTC. The draft §6 carrier signs these **exact claim bytes** with distinct purpose `14` and the accepted revision's named catalog-filter authority/issuer, not an event, membership or historical-grant key. Verify signature, purpose, issuer/SPKI, scope, accepted revision/configuration, broker UTC in `[issued,expiry)` at acceptance, and signed revocation state before filtering; later revocation or missing historical trust proof holds an owed route rather than reevaluating under a new claim.

The only admitted expression language is `01 || u16 rowCount ||` 1..64 rows, each `U tenantId || U domain || U canonicalEventType || I minimumVersion || I maximumVersion`, with positive inclusive bounds ≤1024 and minimum ≤maximum. Rows are sorted uniquely by their complete encoded bytes; no wildcard, regex, host callback, locale, clock or implicit type alias exists. Evaluation is the Boolean OR of exact ordinal UTF-8 equality for the first three fields and inclusive integer comparison for version. `Filtered` requires that Boolean to be false. The exact accepted-revision input is `HX-EV-FILTER-INPUT-1\0 || 01 || 0007` with ordered `01..03` U tenant/domain/accepted-effective canonical event type, `04` I accepted-effective version, `05` B32 authenticated original StoredDigest, `06` B32 accepted RegistryFingerprint and `07` B32 exact accepted transform implementation/manifest hash. It is retained as bytes with source/transform proof before any filter decision; the claim's schema ID and transform hash must match it. The input hash is SHA-256 of those **complete bytes**. Only authenticated reconstruction from the original source and the retained, exact accepted-revision transform bytes may replace a missing input, with byte-identical readback; unknown language, schema, transform or claim bytes hold. The route and physical-filter transcripts retain the exact claim, input, evaluation result and source proof; their receipt hashes bind those retained bytes. Check the complete 4 KiB signed-claim carrier and 16 KiB receipt capacities before accepted-revision activation; an oversized valid catalog configuration holds rather than truncating a claim or input. Current evolution cannot change an accepted filter decision.

Ordinary expiry after a route was accepted does not revoke its historical filter decision: the accepted broker receipt retains the signed claim, acceptance UTC, key/interval proof and exact predicate/input/transform bytes until the owed route closes. A delayed route decision verifies that the claim was valid **at acceptance**, not that delivery occurs before its expiry. Explicit later key revocation, missing accepted-time proof or changed retained bytes holds the route; current catalog settings never classify that already accepted delivery. Physical filtering for an empty accepted set uses the same historical-time rule.

`Filtered` has one distinct, durable, effect-free receipt: `HX-EV-FILTERED-RECEIPT-1\0 || 01 || 0014`, with ordered tags `01..07` equal to the exact seven `EventEffectKey` fields, `08..0a` U component/topic/physical subscription ID, `0b` N accepted membership revision, `0c` B32 handoff route-set hash, `0d..0e` B32 pinned body/attestation hashes, `0f` B exact purpose-specific signed catalog predicate claim, `10` N predicate/route revision, `11` B32 authenticated **accepted-revision** source/view proof hash, `12` B32 exact decision-transcript hash, `13` U literal `Filtered`, `14` B32 exact predicate-input hash. The complete record is ≤16 KiB; a separately retained ≤64 KiB transcript binds full source/pin, selected event type/version, accepted RegistryFingerprint/transform, exact effective predicate input bytes, catalog route identity, predicate bytes and deterministic out-of-scope decision. The accepted membership revision pins both predicate **and input**. Current evolution cannot silently substitute a later effective view; if the original input is unavailable, historical reconstruction is permitted only from authenticated original source and retained accepted-revision registry/transform implementation bytes plus a matching signed proof, otherwise hold. The signed catalog claim fixes predicate bytes, route revision, scope and validity at acceptance. Its receipt key is the C3 handoff key plus `U HandlerRouteId` and `filtered:`, independent of attempt ID. CAS create-once, authenticate the retained acceptance-time signed claim, broker UTC interval and trust proof at the accepted membership revision, plus current revocation state and exact transcript/source/input, then read back complete bytes and referenced proof before proposing the matching `Filtered` route decision. Changed pin, route, revision, input or decision conflicts. `Filtered` authorizes no handler/effect, marker completion by itself or checkpoint; a failed handler cannot be relabeled filtered.

An empty addressed set requires `HX-EV-PHYSICAL-FILTER-1\0 || 01 || 0010` (≤16 KiB), keyed by `physical-filter:` plus SHA-256 of `U component || U topic || U physical subscription || U stable delivery OperationId || U MessageId`, never broker attempt ID. Ordered tags are `01..05` those five U values, `06` N accepted membership revision, `07..08` B32 exact pinned body/attestation hashes, `09` B32 authenticated source/StoredDigest hash, `0a` B32 empty canonical route-set hash, `0b` B exact signed accepted-revision catalog predicate claim, `0c` N predicate/route revision, `0d` B32 accepted-revision predicate-input hash, `0e` B32 full decision-transcript hash, `0f` U literal `Filtered`, `10` B32 Accepted broker receipt hash. Its retained transcript proves the same accepted-revision source/input and deterministic physical out-of-scope predicate; no current route state substitutes. CAS create-once and authenticate/read back exact claim, input, transcript, pin, source, empty route set and broker receipt before 2xx. A duplicate with changed bytes, revision or proof conflicts. No unauthenticated empty set, status name alone or filter receipt lacking the matching decision authorizes physical acknowledgement.

## C4. Legacy handoff, capture and safe diagnostics

Queued flat JSON is a separately selected ingress mode. Authenticate original broker component/topic/physical subscription/configuration revision, exact body/core/headers and original publication/outbox, then trusted source lookup by scoped MessageId. Derive the **historical complete** logical route set; current membership or broker attempt ID cannot fill a missing route. A one-time JSON→binary migration is permitted only with independent proof of **no prior binary pin/send**. If an authenticated prior binary pin already exists, reuse its exact bytes only after matching original JSON source, historic route set and send intent; an ambiguous prior send or pin conflicts/holds without a second pin. Under global MessageId CAS, install or reuse that one binary pin/shared send intent and `HX-EV-LEGACY-HANDOFF-1\0 || 01 || 0017` manifest plus every route record under one committed batch pointer. Handoff OperationId encodes component/topic/physical subscription/MessageId/original revision; per-route key adds HandlerRouteId. Manifest tags `01..0d` bind component/topic/subscription/revision/MessageId/scope/event key/sequence/StoredDigest/original JSON length; `0e` hashes **exact received JSON bytes**, `0f` binds exact original core/header identity, `10` hashes canonical decoded stored event/readable payload/protection/core identity, `11` complete sorted route set, `12` OperationId, `13..15` shared binary pin/body/attestation hashes, `16` exact immutable send intent, `17` authenticated encrypted original-body side-record hash. Manifest ≤2 MiB, send intent ≤1 MiB, ≤4,096 routes, original raw body ≤128 MiB and complete encrypted side record ≤193 MiB as specified below, checked before allocation. Whitespace may vary only when each exact attempt and all canonical decoded fields independently authenticate. Changed source, JSON content, route set, side record, revision or pin is `LegacyHandoffConflict` with no second effect. The old JSON physical delivery remains **unacknowledged** after handoff staging/readback. Acknowledge it only after the exact shared binary send has durable broker acceptance, side record/manifest/**all** route records and pin/send intent read back, **and every addressed route** has a durable terminal decision with authenticated `Completed`/`Filtered` receipt (or C3's capture-backed/broker-referenced `Quarantined` decision and receipt for proven permanent poison). No original JSON ack is inferred from merely creating a route record. Crash before pointer commit or before the last route result leaves JSON unacknowledged; after all results, redelivery reuses the same pin and receipts and only then acknowledges. A binary duplicate caused by redelivery uses the same per-route receipts and cannot create another effect.

Dead-letter parser accepts verified Binary/`data_base64` and legacy structured `data` with strict mode/size rules. It assigns replay-safe identity only after attestation and pinned transport/body/header verification; malformed identity gets only a support-safe unidentified hash, never an identified replay claim. AD-31 capture streams at most 128 MiB of raw body plus exact bounded headers into authenticated encrypted durable storage under the verified tenant scope for identified events or the broker-signed isolated physical capture scope for unidentified carriers, binding exact bytes, hash, scope/reason, retention horizon and actor index under CAS; read back all exact decrypted bytes/index before HTTP 200. A structured carrier above local capture size can be addressed poison only if the route-quarantine receipt binds a broker-owned immutable **full** carrier/header reference with authenticated byte-for-byte readback and retention beyond retry/rollback/incident obligations. If that primitive is unavailable, readiness must constrain broker admission so a >128 MiB structured carrier cannot become an addressed delivery; any unexpected carrier holds non-2xx. A malformed or over-cap carrier with no authenticated event identity or addressable route set can receive physical quarantine only when the broker proves its exact full bytes/header hash, immutable physical subscription, permanent nonadmissibility and durable exact-byte retention. The bounded `HX-EV-PHYSICAL-QUARANTINE-1\0 || 01 || 0009` receipt has `01..03` U component/topic/physical subscription, `04` B32 exact full transport byte-and-header hash, `05` B tagged authority reference, `06` B32 exact retention/readback proof hash, `07` U stable reason code, `08` U broker-signed stable physical delivery ID and `09` B32 broker nonadmissibility/fence proof hash; ≤16 KiB, create-once/read back before 2xx. Its tag `05` uses C3's exact `00 || U AD-31 key || B32 item/index proof hash` or `01 || U broker full-byte reference ID || B32 broker proof hash` inside one `B`, and the corresponding authority-specific full-byte/header readback verifier. Untagged or mismatched authority proof holds. It carries no MessageId, route completion or replay-safe identity and is unavailable if a valid addressed route might exist. Missing full-byte/retention/nonadmissibility proof, capacity or provider readback returns retryable non-2xx and holds activation; finite retries alone never prove quarantine. AD-31 capture proof is separate from `RawCorruptDisposition`, which closes inventory only and never lets replay pass corrupt history. Retain quarantine bytes/keys/source/pin/receipts through replay, rollback and incident obligations; deletion/quota refund require fenced authenticated absence. Diagnostics disclose access-controlled scope, sequence, size/bound, stored/effective version, readable/protection state, pin/lease/receipt/queue lifecycle and typed reason, never payload, secret, key alias, CLR identity, provider internals or stack.

For a whitespace-varied JSON redelivery, the first `HX-EV-LEGACY-HANDOFF` manifest and its tag-`17` original side record remain immutable; its tag-`0e` is the **first** exact JSON attempt hash, not a demand that all later raw attempts equal it. Each admitted exact attempt is independently authenticated and CAS-recorded as `HX-EV-LEGACY-ATTEMPT-1\0 || 01 || 000b`: `01..04` U component/topic/physical subscription/stable handoff OperationId, `05` N historical membership revision, `06` B32 exact body hash, `07` B32 exact core/header hash, `08` B32 canonical decoded source/readable/protection identity hash, `09` B32 first manifest hash, `0a` B32 route-set hash, `0b` B32 binary pin hash. Its stable key is the handoff key plus hashes of **exact** raw body and core/headers; a hash match requires full-byte encrypted side-record comparison. Create-once/readback binds each attempt's original bytes, core/header evidence, canonical source, route set and one binary pin before any receipt reuse or acknowledgement. At most 64 distinct attempts per handoff are retained; a new variant beyond that bound holds as `LegacyHandoffCapacityHold` without an effect or 2xx. Identical attempts reuse their record. Semantically equal whitespace may vary only when every exact attempt passes source and header authentication and yields the same canonical identity, route set and pin; changed decoded content, source, headers, revision or pin is `LegacyHandoffConflict`. The previous paragraph's changed-side-record conflict applies to changing the **first** side record or to any attempt that fails this binding. A matching authenticated prior binary pin follows the same attempt check and reuses its exact bytes; a conflicting pin is `LegacyHandoffConflict`, and unknown prior send authority holds.

The 128 MiB legacy body cap is not a side-record cap. The durable side record retains **one** protected output over the exact original raw JSON, broker/application headers and canonical core framing; streaming input is not a second retained copy. Its checked reservation is `protected_output_bytes + unencrypted_record_metadata_bytes + nonce/tag/outer_framing_bytes ≤193 MiB`; the encrypted input is `raw_body_bytes + exact_header_bytes + canonical_core_framing_bytes`, with raw body ≤128 MiB, headers ≤64 KiB, core framing ≤1 MiB and provider-proven **worst-case incompressible** protection expansion ≤1 MiB over that complete input. Headers and core framing inside the protected output are charged **there once**, not again as unencrypted metadata. A provider with unknown or greater expansion must reduce its advertised raw-body limit to a value whose measured worst case fits, or hold before admission. Protection output must be ≥ complete encrypted-input length for the incompressible boundary vector; no assumed compression discount is valid. Input and protected-output streams use separately charged ≤1 MiB chunks as simultaneously live B6 scratch; a provider requiring a whole-body or whole-ciphertext buffer must lower its admitted maximum or hold. No buffer never retained is charged a second time as durable side-record bytes. The first side record and each of at most 64 variants consume reserved provider capacity; manifest (≤2 MiB), send intent (≤1 MiB), route records and side records are distinct retained objects under B6 operation/deployment quotas. AD-31 capture uses the same exact protected-output accounting; if it cannot retain/read back a carrier it cannot authorize ack. At a 128 MiB raw body, 64 KiB headers and 1 KiB core framing, 129 MiB protected output plus 1 MiB outer record metadata/framing fits the 193 MiB cap; 193 MiB protected output plus any nonzero outer framing fails. Over-limit or partial readback holds without dropping headers or protection overhead.

For unidentified physical poison, the tag-`08` stable physical delivery ID and tag-`09` nonadmissibility/fence proof in that receipt are mandatory. The stable CAS key is `physical-quarantine:` plus SHA-256 of `U signed capture scope ID || U component || U topic || U physical subscription || U stable physical delivery ID`, never broker attempt ID or a raw-byte hash. A broker must attest that this immutable ID denotes the same complete physical delivery across retries and that no valid addressed route exists under its pinned historical configuration. Before 2xx, read back the full exact body **and all headers**, encrypted AD-31 capture or broker-owned full-byte reference, its retention horizon, the complete receipt and signed broker proof; compare full bytes on duplicate. Same key with changed bytes, headers, subscription, reason or proof conflicts and remains non-2xx. A new attempt ID cannot create a second disposition. If the broker lacks a stable ID, full-byte access, historical route proof or a durable nonadmissibility fence, physical quarantine is unavailable and the carrier retries/holds. The reference and signing key persist through the maximum retry, rollback and incident horizon; a hash alone has no disposition authority.

For unidentified poison, the carrier supplies no trusted tenant. Before capture, the broker authenticates `HX-EV-PHYSICAL-CAPTURE-SCOPE-1\0 || 01 || 0008`: ordered `01` U broker issuer, `02..05` U domain/component/topic/physical subscription, `06` N immutable historical subscription/configuration revision, `07` U isolated capture/access scope ID and `08` B32 exact signed physical configuration hash. Its complete ≤16 KiB record is signed with distinct purpose `1b` by the physical-subscription authority and bound to the immutable broker delivery-log generation. The storage key is the scope ID plus stable physical delivery ID; the scope ID is derived only from signed subscription/configuration bytes, never from carrier tenant, headers or parser output. A single-tenant physical subscription may map to its authenticated tenant partition; a shared subscription uses a separate broker-owned isolation partition whose access policy is bound to the signed physical subscription and disallows tenant-scoped lookup. AD-31 encrypts, indexes and reads back in that exact partition; scope/configuration ambiguity or cross-tenant readback holds non-2xx. The capture-scope claim hash is SHA256(B(exact complete claim) || B(exact purpose-1b signature carrier)) and is bound into the identity source below.

The broker supplies three separately signed **source records** before unidentified quarantine. Their body/header hash is `SHA256("HX-EV-PHYSICAL-CARRIER-1\0" || 01 || B(exact complete transport body) || u32 headerCount ||` each header in original broker order as `B(exact name) || B(exact value))`; preserve duplicate headers and original casing, and require the exact length/count limits or a broker streaming/full-byte quarantine capability. `HX-EV-PHYSICAL-IDENTITY-1\0 || 01 || 0009` has `01..04` U component/topic/subscription/stable physical delivery ID, `05` B32 complete carrier hash, `06` N broker immutable delivery-log generation, `07` U immutable broker full-byte reference ID and `08` Q first-observed broker UTC and `09` B32 exact signed capture-scope claim hash. The distinct purpose-`16` broker delivery authority signs it and proves by immutable delivery-log CAS/ETag readback that the ID and complete bytes cannot change across attempt IDs. `HX-EV-PHYSICAL-NONADMISSIBLE-1\0 || 01 || 0009` has `01` B32 identity-record hash, `02` N exact historical subscription/configuration revision, `03` B32 authenticated historical route-catalog/configuration hash, `04` B32 exact carrier hash, `05` U closed permanent parse/nonadmissibility reason, `06` B32 empty-addressable-route proof hash, `07` B exact broker no-route/reject fence receipt, `08` Q fence UTC and `09` U broker authority ID. Distinct purpose `17` signs it; the authority independently verifies the historical configuration, permanent reason, no possible addressed route and linearizable fence preventing later delivery of those bytes to a route. A parser failure alone cannot establish this. `HX-EV-PHYSICAL-RETENTION-1\0 || 01 || 0009` has `01` B32 identity-record hash, `02` U broker immutable full-byte reference ID, `03` B32 complete carrier hash, `04` N full retained byte length, `05` Q retention-through UTC, `06` B32 immutable storage generation/ETag hash, `07` U retention authority ID, `08` B32 exact full-byte readback receipt hash and `09` B32 exact storage obligation-fence record-and-receipt hash; purpose `18` signs it. Each complete record/signature carrier is ≤16 KiB and uses draft §6's pinned issuer/SPKI, purpose, validity interval and revocation checks. C4 quarantine tag `05` names this exact reference/AD-31 authority, tag `06` hashes the complete signed retention record plus full-byte readback receipt, tag `08` equals identity tag `04`, and tag `09` hashes the complete signed nonadmissibility record plus its fence receipt; the receipt also binds the identity-record hash through both sources. Read back all three exact source records, current historical trust/revocation state, immutable broker log/reference, complete body and each header **byte-for-byte**, retained horizon and the receipt under one stable CAS key before 2xx. Missing signatures, revoked authority, lost reference, shorter horizon, changed ETag or merely matching hashes hold non-2xx. Retain the signed records and bytes for the longest retry, rollback or incident obligation.

The obligation fence is `HX-EV-PHYSICAL-OBLIGATION-FENCE-1\0 || 01 || 000b`: ordered `01` U signed capture scope ID, `02` U immutable full-byte reference ID, `03` B32 physical identity-record hash, `04` B32 complete carrier hash, `05` N checked storage generation, `06` U actual storage ETag, `07` B32 complete open-obligation-set root, `08` U literal `NoExpiryUntilClosure`, `09` Q fence-installed UTC, `0a` U storage authority ID and `0b` B32 exact partition/access-policy hash. Its complete ≤16 KiB record and CAS/ETag receipt are backend-authenticated under the same durable object generation as the full-byte reference; tag `09` of the retention source hashes `B(exact fence record) || B(exact CAS/ETag receipt)`. Readback verifies the exact storage partition, generation/ETag, fence and full bytes together. Any update to the open-obligation set uses a monotonic CAS generation and cannot remove the no-expiry rule before authenticated closure. A backend that expires bytes independently of this fence cannot support unidentified physical quarantine.

A finite retention-through UTC in the signed source is a lower bound, never permission to delete. Before 2xx the storage authority CAS-installs an authenticated obligation fence keyed by signed capture scope and immutable full-byte reference: it blocks expiration, deletion, quota refund and key retirement while **any** retry, delayed-delivery, rollback, backup or incident obligation is open. The signed purpose-18 source's tag `09` binds the exact fence record and CAS receipt, including storage generation/ETag, open-obligation set root and no-expiry-until-closure rule. Every readback verifies both full bytes and the active fence under the same reference. A finite expiry path is permitted only with a signed closed maximum horizon for every obligation, a storage-enforced expiry strictly later than that horizon and a broker proof that no new obligation can arise; otherwise the active fence remains nonexpiring until independently authenticated closure. Renewal intent or a calendar date alone never grants 2xx. Deletion requires a final fenced CAS proving all obligations closed and no future acceptance, then authenticated absence; a previously acknowledged poison blob cannot expire while an obligation is open.

## C5. A8 public outcome and terminal `PublishFailed`

Import 6.5a A8's **codec-03** `HX-EV-COMMAND-OUTCOME-1\0` (not draft codec 01): its twelve tag types, `pending/published/failed/unknown/not-applicable`, tag `08` complete publication-set hash for every eventful outcome and absent only for no-op. Exactly one destination per committed event, including rejection events; the expected set is sorted `(ordinal, MessageId, destinationId, exact outbox-intent hash, exact pin hash)`. `HX-EV-PUBLICATION-SET-1\0 || 01 || 0006` binds every observation row under one coordinator fence; ≤1,000 V1 or 256 V2 members, ≤2 MiB complete set, ≤64 MiB distinct referenced receipt/proof evidence. Reserve worst-case complete-set capacity **before** A4 Prepared/append. Accepted rows remain accepted and are never resent; unknown broker attempts reconcile before retry; failed is a definitive **attempt** outcome and may move to higher admitted attempt. Reduce all accepted→published; otherwise any unknown→unknown; otherwise any pending→pending; otherwise failed. A no-op is `not-applicable` with no publication. A partial accepted/pending/failed set never yields published.

A8's authenticated zero-event no-op is a separate branch **before** retry-policy creation. Its A7 no-op witness and immutable append/result/response evidence prove the empty event set; it has no A8 publication members or set, retry policy, outbox, pin, member send ID, broker acceptance or C5 terminal decision. Public outcome remains `not-applicable` with A8's original no-op response bytes. A retry reads the same no-op witness and immutable public response pin without constructing an eventful policy; any publication or terminal evidence beside that witness is contradictory and holds.

For an eventful batch only, before A4 publishes `Prepared`, the admission owner CAS-creates and authenticates the exact signed `HX-EV-RETRY-POLICY-1\0 || 01 || 000e` at `retry-policy:` plus A5 ScopeOpHash: ordered `01` U configured issuer ID, `02` U original command OperationId, `03` B32 ScopeOpHash, `04` B32 pre-policy member-plan hash, `05` U tenant, `06` U domain, `07` B32 ordered destination-scope hash, `08` B32 exact destination-configuration-set hash, `09` N member count, `0a` B member-policy rows, `0b` B reason rules, `0c..0d` Q issued/expiry UTC and `0e` U policy version. Member rows are `u32 count ||` exactly count rows in A8 member-position order, each `u32 position || B32 SHA256(U full MessageId) || B32 SHA256(U full destinationId) || B32 exact destination-configuration hash || u8 maximumAttempts`; positions are contiguous from 1, count is 1..1,000 (≤256 V2), and every maximum is 1..64. Reason rules are `u16 count ||` 1..64 unique UTF-8-ordinal sorted `U closedReasonCode || u8 class || O(U sourceCodecId)`, with each reason code ≤48 UTF-8 bytes and the complete rule block ≤4 KiB, where classes are exactly `01` retryable, `02` definitive terminal rejection, `03` zero-attempt permanent nonadmissibility. The source codec ID is absent for classes 01/02 and present for class 03; it names one immutable, installed reason-specific decoder and authority, not a mutable registry lookup. No default class or unknown reason is allowed; the signed policy fixes the exact classifier and maximum for each member. Complete claim/signature carrier is ≤128 KiB, and rows, reason rules and expected-set full identifiers are premeasured before allocation. Draft §6's carrier uses distinct purpose `19` and the admitted destination policy authority's pinned issuer/SPKI, signing interval and revocation state. `PrePolicyPlanHash = SHA256("HX-EV-RETRY-PLAN-1\0" || 01 || u32 memberCount ||` each row `I original event ordinal || U full MessageId || U component || U topic || U full destinationId || B32 exact destination-configuration hash)` in event-ordinal order. Generate and freeze this ≤8 MiB exact plan from A4's original command/event allocation and immutable destination configuration **before** constructing any outbox intent or global pin; the policy rows and tag `08` destination-configuration-set hash derive only from it. `DestinationScopeHash = SHA256("HX-EV-RETRY-SCOPES-1\0" || 01 || u32 memberCount ||` each planned row's `I original event ordinal || U component || U topic || U full destinationId)`; it is policy tag `07`. `DestinationConfigurationSetHash = SHA256("HX-EV-RETRY-DESTINATIONS-1\0" || 01 || u32 memberCount ||` each planned row's `I original event ordinal || B32 exact destination-configuration hash)` in that same order; it is policy tag `08`. The plan contains no policy, outbox or pin hash, so signing it cannot depend on bytes that later bind the policy. `RetryPolicyHash = SHA256("HX-EV-RETRY-POLICY-HASH-1\0" || 01 || B(exact complete policy claim) || B(exact purpose-19 signature carrier))`. The A4 complete preparation capsule retains the signed policy and plan bytes; each A5 outbox intent's immutable routing-policy bytes then bind `RetryPolicyHash`. A8's expected set and every later pin must match the plan's full identifiers, event ordinal and destination configuration, and its outbox hash must carry that same `RetryPolicyHash`. Read back all three exact bindings before `Prepared`, every send and every terminal/status verification. A changed policy, missing authority or expired/revoked signing trust holds; current retry settings never amend it. `Rejected` receipts carry the closed reason code and signed broker authority. A failed member may terminalize only with a definitive class-`02` Rejected receipt (possibly before its maximum) or the class-`03` zero-attempt nonadmissibility proof; class-`01` retryable failures remain open even at the maximum. No attempts follow Accepted or a definitive terminal rejection. Exhaustion means the signed maximum was reached **and** the final definitive receipt is class `02`; a count alone, policy ID or hash never grants terminal failure. The policy and all complete receipts are retained and verified through the operation's rollback/incident horizon.

Resolve A8's public terminal mapping as follows. A private `failed` head **alone** cannot produce `CommandStatus.PublishFailed`: a failed member may retry. Permanent public failure requires an immutable provider/broker-authenticated decision `HX-EV-PUBLICATION-TERMINAL-1\0 || 01 || 000b` at `publication-terminal:` plus 6.5a A5 `ScopeOpHash`. Ordered tags are `01` U original OperationId, `02` B32 committed batch-root hash, `03` B32 exact final publication-set hash, `04` B32 current outcome-head hash, `05` N final head revision, `06` B exact coordinator fence, `07` B32 authenticated all-destination queue/outbox drain and no-in-flight-send proof hash, `08` B32 authenticated no-future-acceptance and fenced-operation proof hash, `09` B32 fixed policy/exhaustion decision hash, `0a` Q decision UTC, `0b` U support-safe failure reason code. Complete record ≤64 KiB and each canonical referenced proof ≤64 KiB; reserve the complete framed member proof and source records under A8's same 1 GiB operation quota before terminalization. CAS-create once, authenticate exact readback with the entire proof set and every member receipt; unequal existing bytes hold. At least one required member is nonaccepted, or the complete set is `published`. Every accepted member remains true; terminal `PublishFailed` means at least one required member cannot publish under this permanently closed operation, never that no event stored or another member did not publish. If a broker cannot prove absence of future acceptance after an ambiguous send, terminality is unavailable. Contradictory later acceptance is an incident/evidence conflict and `CommandOutcomeHold`, never a silent status rewrite.

Only with that proof may authorized status inspection project latest private failed to the **existing** `CommandStatusResponse`. Pin every field from authenticated committed source and final head: `CorrelationId`, `MessageId`, `AggregateId`, `EventCount`, `RejectionEventType` and `TimeoutDuration` retain their original immutable record values (including null versus value); `TenantId` is the original authorized tenant scope, never a caller override; `Status="PublishFailed"`, `StatusCode=6`, `Timestamp` is terminal tag-`0a` decision UTC copied into the immutable terminal status record, `FailureReason` is the redacted terminal reason, `Retryable=false`, `RecoveryReasonCode="PublishFailed"`, and `DrainAttemptCount` is the final authenticated count or null when none is proven. For nonterminal revisions, `Timestamp` comes only from the immutable authenticated revision clock below and optional original metadata remains unchanged; a private failed revision is withheld from status inspection. No `Retry-After` header is sent for terminal status, matching existing `IsTerminal()`. Missing/ambiguous decision, key, head, member receipt or no-future-acceptance proof returns `CommandOutcomeHold`, never status 6 or a fabricated nonterminal status. The terminal decision cannot transition to pending/published under the same operation; separately approved recovery needs a new execution identity and preserves original bytes. This adds no public enum, field or route. Pending/unknown use the existing nonterminal shape without `EventsPublished`/`Completed`.

Each nonterminal revision has one `HX-EV-OUTCOME-CLOCK-1\0 || 01 || 0008` (≤4 KiB) at `outcome-clock:` plus ScopeOpHash and checked revision `N`: ordered `01` U original OperationId, `02` N revision, `03` B32 exact outcome-record hash, `04` B32 predecessor-head hash (all zero at revision zero), `05` Q coordinator UTC observation instant, `06` U configured clock issuer, `07` B32 authenticated broker/observation source hash and `08` B32 exact successor-head hash. The accepted coordinator authority signs the exact record with distinct purpose `1a`; verify issuer/SPKI, issuance interval, revocation and signed UTC source, and CAS-create/read back the complete record **before** publishing that outcome/head revision. The outcome and successor-head bytes are computed deterministically without a clock field, so this sidecar changes neither A8's twelve-tag outcome nor four-tag head. The successor-head CAS checks the staged clock hash and the same fence/revision; the immutable key binds one instant to that revision. After a lost acknowledgement, a retry reads the existing clock and head and never samples a new instant. Status inspection verifies the complete signed clock, exact outcome/head hashes and predecessor chain, then renders its `Q` instant as the existing UTC `Timestamp`; missing, conflicting, expired-at-issuance or unreadable clock evidence is `CommandOutcomeHold`, with no fallback to local wall time. Terminal status continues to use the independently authenticated terminal decision time.

The **first committed POST reply** remains 6.5a A8's immutable revision-zero pin and is publication-independent except its fixed `resultPayload` gate: only revision-zero `published` includes the payload. Revision-zero pending/unknown/failed accepted commands pin the same 202 `{correlationId,messageId}` application response; committed rejection pins existing ProblemDetails classified from immutable result bytes, never transient publication status. `HX-EV-RESPONSE-PREPARATION-1\0` proceeds NeverStarted→Rendering→Prepared→Pinned under fence, with one render and exact byte readback before first response. Later status revisions never replace that response; exact retry returns original status/application headers/body after current access checks. Never overwrite A5 domain/append result or first response pin with `PublishFailed`; failure after actor save is a publication observation, not proof of no commit. No-op returns its original no-op public shape without publication. Lost response, cancellation or ambiguous save reads back append, full observation, response preparation/pin and head under bounded recovery; missing authority holds without rerender or restaging.

Tags `07..09` of the terminal record hash three **separately retained authenticated canonical source records**, never operator assertions. Each uses draft §4 `U/B/B32/N/Q`, ordered tags, version byte, ≤64 KiB complete record and a purpose-specific broker/provider signature or backend-authenticated CAS/ETag under the exact OperationId and coordinator fence. `HX-EV-TERMINAL-DRAIN-1\0 || 01 || 0007` binds `01` U OperationId, `02` B32 final complete publication-set hash, `03` B **framed** complete member/last-attempt roster, `04` B32 authenticated outbox/queue empty-state readback, `05` B32 in-flight send ledger closure, `06` B exact coordinator/backend fence (≤8 KiB), `07` Q final observation UTC. Tag `03` is `u32 totalMemberCount || B32 completeRosterHash || u32 segmentCount ||` sorted contiguous descriptors, each `u32 firstOrdinal || u32 count || B32 segmentHash`; count is 1..1,000, segment count 1..1,000, no gaps/overlap, and the descriptor list is ≤48 KiB. Each retained `HX-EV-TERMINAL-MEMBERS-1\0 || 01` segment is ≤64 KiB and contains its exact framed rows: `u32 rowCount` followed by ascending `N ordinal || B32 MessageIdHash || B32 destinationIdHash || B32 pinHash || N actualAttemptCount || O(B32) lastAttemptOperationIdHash || U outcome || O(B32) exact lastReceiptHash || O(B32) exact signed nonadmissibilityProofHash`. For zero attempts the two last-attempt fields are **absent** (`00`), the proof hash is present (`02` plus B32), and outcome is `failed`; with one or more attempts the last-attempt fields are present and the nonadmissibility proof hash is absent. Explicit null (`01`) is invalid in all three optional fields. The proof hash names the exact signed member nonadmissibility record below and is bound by both this roster row and the policy row. `actualAttemptCount` is 0..the signed member maximum; roster outcome is exactly `accepted` or `failed` at terminality and must match the final publication-set row. No `unknown` or `pending` member can terminalize. Deterministically take the longest contiguous prefix satisfying both the kind's row-count cap and the complete 64 KiB encoded-segment cap, then the next; one oversized row holds. Set `segmentHash = SHA256(exact complete segment bytes)` and `completeRosterHash = SHA256("HX-EV-TERMINAL-ROSTER-1\0" || 01 || u32 totalMemberCount || u32 segmentCount || B(each exact ordered segment))`. Compare every row to the final publication set and every exact Accepted/rejected/exhausted receipt. The original A8 complete publication set remains ≤2 MiB; the separately retained segments and receipt images are charged under its ≤64 MiB referenced-evidence bound. The immutable signed retry policy fixes 1..64 attempts per member, with ≤1,000 members (≤256 V2) and ≤64,000 attempted sends. Before A4 Prepared, reserve the policy's **worst-case** receipt, segment, descriptor and roster bytes under A8's 64 MiB referenced-evidence and 1 GiB operation quotas; if the provider's exact receipt bound will not fit, lower and sign the retry maximum before Prepared or hold admission. It cannot grow afterward. Every roster/attempt/policy row is ≤512 bytes and each scoped segment header (OperationId hash, final-set hash, segment ordinal and row count) is ≤1 KiB. Deterministically fill each ≤64 KiB segment with the longest contiguous sorted row prefix satisfying its row-count and complete encoded-byte caps. A row or segment over its cap holds before the attempt or terminal decision that would exceed it. CAS-create segments under the same OperationId/final set/fence, authenticate exact backend ETag/readback and compare complete bytes to the signed root's descriptor hashes. At terminal CAS and each status inspection, verify **all** exact segments and referenced receipts. Missing, partial, ambiguous or over-budget evidence is `CommandOutcomeHold`, never a truncated proof.

`HX-EV-TERMINAL-NO-ACCEPT-1\0 || 01 || 0007` binds `01` U OperationId, `02` B32 final set, `03` B complete attempt-set descriptor, `04` B exact broker durable reject-fence receipt (≤16 KiB), `05` B exact producer/coordinator attempt-disable receipt (≤16 KiB), `06` B32 drain-record hash and `07` Q fence UTC. Tag `03` is `u32 totalAttemptCount || B32 completeAttemptSetHash || u32 segmentCount ||` contiguous descriptors `u32 firstFlatOrdinal || u32 rowCount || B32 segmentHash`; ≤512 segments, ≤24 KiB descriptors and 0..64,000 attempts. Zero attempts have zero segments and the canonical empty root; otherwise every segment is nonempty. Each `HX-EV-TERMINAL-ATTEMPTS-1\0 || 01` segment contains its scoped header and `u32 rowCount` followed by ≤512-byte rows sorted uniquely by `(memberOrdinal, attemptOrdinal)`: `u32 memberOrdinal || u32 attemptOrdinal || B32 brokerOperationIdHash || B exact 16-byte fenceNonce || B32 exactPinHash || U definitive outcome || B32 exactReceiptHash`. Member ordinals match the final set; attempt ordinals are contiguous `1..actualFinalAttemptOrdinal` for each member with attempts, where that ordinal is ≤ its immutable signed maximum of 1..64. A member may stop earlier after Accepted or proven terminal exhaustion; zero-attempt members have no attempt rows. Only authenticated `Accepted` or `Rejected` is definitive; `Unknown` prevents terminality. A member's broker operation identity and pin remain unchanged across renewed nonces, and no attempt follows Accepted. `segmentHash = SHA256(exact complete segment bytes)`; `completeAttemptSetHash = SHA256("HX-EV-TERMINAL-ATTEMPT-SET-1\0" || 01 || u32 totalAttemptCount || u32 segmentCount || B(each exact ordered segment))`. Reconcile every row against the broker's complete operation/nonce ledger and exact receipt. A zero-attempt member needs both the separately signed pre-send reason/source and post-fence complete zero-attempt ledger, bound by its nonadmissibility record. The broker fence still covers this OperationId and all current/future member send IDs on every accept path.

`HX-EV-TERMINAL-POLICY-1\0 || 01 || 0007` binds `01` U OperationId, `02` B32 final set, `03` B32 `RetryPolicyHash`, `04` B complete member-decision descriptor, `05` B32 drain hash, `06` B32 no-accept hash and `07` Q decision UTC. Tag `04` has the same framed descriptor form with `u32 memberCount || B32 completePolicySetHash || u32 segmentCount`; member count exactly matches the final set's sorted unique 1..1,000 (≤256 V2) members, with 1..8 segments and ≤1 KiB descriptors. Each `HX-EV-TERMINAL-POLICY-MEMBERS-1\0 || 01` segment contains its scoped header and ≤512-byte rows sorted uniquely by final-set member ordinal: `u32 memberOrdinal || B32 MessageIdHash || B32 terminalReasonHash || N actualAttemptCount || O(B32) exact final-attempt receipt hash || O(B32) exact signed nonadmissibilityProofHash`. For a zero-attempt member count is zero, final receipt is absent (`00`) and nonadmissibility proof hash is present (`02` plus B32); for any attempted member the final receipt is present and nonadmissibility proof absent. Explicit null is invalid. Counts/reasons match the complete attempt set and fixed signed policy; an Accepted member has exact terminal reason literal `Accepted` and its authenticated Accepted receipt, while every failed reason must occur in the signed rule table with the required class. A member ending below its signed attempt maximum requires an exact class-`02` terminal Rejected receipt or an Accepted receipt; the maximum is a ceiling, not a mandatory number of sends. `completePolicySetHash = SHA256("HX-EV-TERMINAL-POLICY-SET-1\0" || 01 || u32 memberCount || u32 segmentCount || B(each exact ordered segment))`. Retryable failures cannot satisfy exhaustion.

All three segment kinds use the **same exact complete record framing**, with only their ASCII NUL-terminated domain separator differing: `HX-EV-TERMINAL-MEMBERS-1\0`, `HX-EV-TERMINAL-ATTEMPTS-1\0` or `HX-EV-TERMINAL-POLICY-MEMBERS-1\0`. Exact bytes are `domainSeparator || 01 || u16(7) || 01 B32 OperationIdHash || 02 B32 finalPublicationSetHash || 03 B32 coordinatorFenceHash || 04 N zeroBasedSegmentIndex || 05 N firstOrdinal || 06 N rowCount || 07 B rowBlob`, with one-byte tag numbers shown, draft §4 scalar encodings, no extra bytes and complete record ≤65,536 bytes. `coordinatorFenceHash = SHA256(B(exact terminal coordinator fence))`; each segment's fence hash must match the terminal source records. The terminal member ordinal is the one-based **position** in A8's immutable expected set sorted by its original `I event ordinal`; it does not replace or renumber that original event ordinal. `firstOrdinal` is the first terminal member position for roster/policy, or the one-based flat ordinal in `(memberPosition, attemptOrdinal)` order for attempts. It matches its descriptor; segment indexes are contiguous from zero. `rowCount` is 1..1,000 for roster, 1..125 for attempts and 1..125 for policy, and also equals the descriptor count and the first `u32` in `rowBlob`. The remainder of `rowBlob` is exactly `rowCount` repetitions of `u32 rowByteLength || rowBytes`; `rowByteLength` is 1..512, fits within the declared `B` length, and `rowBytes` is **only** the ordered typed row fields given above, with no tags, padding, JSON, alternate integer widths or trailing bytes. Attempt and policy `u32` ordinals/counts use checked big-endian; roster's `N` uses signed big-endian i64 and must be positive. Canonically partition the sorted complete rows into the longest contiguous prefix whose **complete encoded segment** fits 65,536 bytes and whose row count stays within that kind's cap, then repeat; a row that cannot fit in a fresh segment holds. The ≤1 KiB scoped-header and 64 KiB segment reservations, existing per-kind descriptor caps and operation reservations still apply: `125 × (512 + 4) + 1,024 = 65,524 ≤ 65,536`, `64,000 / 125 = 512` attempt segments and `1,000 / 125 = 8` policy segments at the worst permitted row size. The complete encoded header, including all tags and the `B` length, consumes its actual bytes and must be ≤1,024. Each descriptor hash is SHA-256 of those exact complete segment bytes. Every set root's `B(each exact ordered segment)` means the concatenation of one draft §4 `B(segmentBytes)` per descriptor in order, including its length, so no serializer choice changes a hash. Zero attempts mean no attempt segment, count zero, and the root hashes the separator, version and two zero `u32` counts with no `B` values. CAS/readback compares the exact bytes before the final terminal root is trusted.

Terminal rows contain fixed-size identity hashes **only as references**, not as substitutes for complete identifiers. For every `B32 XHash` above, compute `SHA256(U(exact full identifier))` for MessageId, destinationId, member send/broker OperationId and terminal reason, and `SHA256(U(exact original command OperationId))` for the segment header. The terminal verifier independently reads the complete authenticated A8 expected set and outbox/pin images for both 1,024-byte MessageId and destinationId values, the C2 parent-bound broker operation index and exact receipt for each send ID, and the signed retry-policy reason table for each failed reason code (or the fixed literal `Accepted` for an accepted member). It recomputes each hash from those **full bytes**, matches ordinal, parent, destination, pin, outcome, nonce and receipt, and rejects missing, multiple, conflicting or altered references. A digest alone cannot prove a member or attempt. The complete A8 expected set remains under its existing 2 MiB bound; the signed policy, all full-ID images, receipts and compact terminal segments are independently reserved before A4 Prepared under the existing 64 MiB referenced-evidence and 1 GiB operation budgets. Preflight uses the maximum 1,024-byte U fields and provider receipt bounds, so a valid committed member cannot encounter a new 512-byte terminal-row cap after append. If even the compact row or complete referenced evidence cannot be reserved, admission holds before Prepared, never drops a committed member. The full source records and identifiers outlive all open retry, rollback and status obligations.

The signed policy's class-`03` reason table is closed over exact reason-specific source codec IDs, verifier versions and authorities; each admitted reason binds its immutable destination configuration, actor source and the independently signed authoritative condition that made this member ineligible **before its first send**. `HX-EV-MEMBER-PRESEND-1\0 || 01 || 000c` is ordered `01` U broker/policy issuer, `02` U authenticated tenant, `03` U original OperationId, `04` N one-based member position, `05` U MessageId, `06` U destinationId, `07` B32 RetryPolicyHash, `08` U exact class-03 reason, `09` B32 immutable destination-configuration hash, `0a` B complete reason-specific source record and signature carrier (≤8 KiB), `0b` B32 broker pre-send zero-accept/zero-queued ledger proof hash and `0c` Q decision UTC. Purpose `1d` signs the complete ≤16 KiB record before any attempt under the accepted broker/policy authority. The verifier reads the full source record, its named rule decoder, the policy, destination configuration and broker ledger, and checks that the condition preceded all possible sends; unavailable or merely current configuration is insufficient. At terminal closure, after the producer disable and broker reject fence, the broker supplies a signed complete operation/send namespace ledger under the same tenant/ScopeOpHash showing zero accepted, zero queued, zero in-flight and zero attempted entries for this member across all destinations and nonce IDs, with generation/ETag and fence order. Its full bytes and readback, not a Boolean or terminal fence alone, yield tag `0c` of the nonadmissibility record. Any prior attempt, unknown ledger gap or source mismatch prevents a zero-attempt row.

The only base class-03 source codec admitted by this candidate is `HX-EV-PRESEND-DESTINATION-REVOKED-1\0 || 01 || 0009`, named exactly by the policy row. Its ordered tags are `01` U authenticated tenant, `02` U original OperationId, `03` N member position, `04` U destinationId, `05` B32 planned configuration hash, `06` B32 signed permanent destination-revocation decision hash, `07` Q revocation-effective UTC, `08` B32 broker no-admission capability/configuration hash and `09` U authoritative revocation issuer. The complete ≤8 KiB source and purpose-specific signature carrier must be issued by the destination policy authority before the first possible send; the verifier reads back the exact revocation decision/configuration and proves that the permanent revocation was effective at the pre-send decision. Other reason codecs require an approved extension to this candidate's closed table before a policy can name them. A temporary outage, unsupported current configuration or broker timeout has no class-03 source.

The post-fence ledger proof is the signed `HX-EV-MEMBER-NO-ATTEMPT-1\0 || 01 || 000c` record: `01` U authenticated tenant, `02` B32 ScopeOpHash, `03` U original OperationId, `04` N member position, `05` U MessageId, `06` U destinationId, `07` B32 complete broker parent-index head, `08` B32 producer-disable receipt hash, `09` B32 broker reject-fence receipt hash, `0a` B32 complete destination queue/outbox/in-flight empty-state readback root, `0b` N zero accepted/queued/attempted count and `0c` Q final broker observation UTC. The purpose-`1f` broker authority signs its complete ≤16 KiB bytes after the reject fence and final empty observation. The broker verifier reads the full append-only send namespace and attempt/receipt ledger for the tenant/parent/member through the fenced generation, checks the zero count and all destination queues, and recomputes the exact root; a count without that complete authenticated ledger is insufficient. The signed nonadmissibility record tag `0c` is `SHA256(B(exact no-attempt record) || B(exact purpose-1f carrier))`. The pre-send proof at tag `0b` is `SHA256(B(exact presend record) || B(exact purpose-1d carrier) || B(exact signed reason-specific source))`. Verify both exact records and their source bytes against the final A8 set and policy on every public terminal read.

A zero-attempt reason is `HX-EV-MEMBER-NONADMISSIBLE-1\0 || 01 || 000c` with ordered `01` U configured issuer ID, `02` U original OperationId, `03` N final-set member ordinal, `04` U MessageId, `05` U destinationId, `06` B32 exact pin hash, `07` B32 `RetryPolicyHash`, `08` U closed nonadmissibility reason code, `09` Q decision UTC, `0a` B32 broker reject-fence receipt hash, `0b` B32 exact signed pre-send decision/source proof hash and `0c` B32 exact broker no-attempt ledger proof hash. Its ≤16 KiB exact bytes are signed in the draft §6 carrier with distinct purpose `15` by the accepted broker/policy authority; verify issuer, SPKI, signature, purpose, interval/revocation, matching tenant-scoped original operation/member/policy/fence, complete signed pre-send source and complete post-fence zero-attempt ledger readback. The record must prove that this member could not be admitted under the frozen policy before any send; an unavailable broker or unknown prior attempt is not such proof. Retain and read back the complete signed record under the A8 referenced-evidence budget. `SHA256("HX-EV-MEMBER-NONADMISSIBLE-PROOF-1\0" || 01 || B(exact record) || B(exact signature carrier))` is the nonadmissibilityProofHash in **both** segment rows; their descriptor hashes feed the drain/policy source hashes and final terminal root. Missing, mismatched or revoked proof prevents public terminality.

All three records and their exact source images/receipts are verified under one fence/head; source hashes alone confer no authority.

Terminal closure has this required linearization order: (1) CAS-disable the producer/coordinator for the original OperationId under a durable fence, freezing the complete member/send-ID namespace and blocking new attempts; (2) atomically install and read back the broker reject fence for that **operation namespace**, so no future accept can succeed even with a delayed or newly presented ID; (3) query/reconcile every send that may have been accepted before the broker fence, retaining each Accepted receipt and definitive rejection, then drain all destination queues, outboxes and in-flight ledgers; (4) perform the **final** empty-state observation/readback after the broker fence, under the same fence generation, and build the drain/no-accept/policy records; (5) CAS the final failed head with terminal decision and immutable public status projection. An accept between steps 1 and 2 is included in step 3; an accept after step 2 contradicts the broker fence and holds as an incident. Every source timestamp/ETag and broker fence generation is checked for this ordering; a stale empty observation cannot terminalize. A rejected fence installation, unknown send, changing queue, missing key or unproven closed delivery horizon is `CommandOutcomeHold`. The status reader authenticates exact source records, final nonaccepted member, every retained Accepted receipt and the ordered fence/empty evidence on each authorized lookup. Retry, status revision or rollback cannot un-fence the same OperationId or change the immutable first POST pin. Closure retains all required pin/key/source bytes until obligations end.

The live `ReplayController` includes `PublishFailed` in its replayable status set and resubmits the archived command with new correlation/MessageId. At the existing replay route, a terminal `PublishFailed` **must** pass a new authenticated replay-safety gate before mediator submission or ID allocation. Its default result is the existing 409 conflict shape with support-safe `ReplayPublicationConflict` reason: an eventful committed batch, and especially a partial Accepted set, must never be re-executed as a fresh command. A safe fresh-command replay requires independent authenticated proof that the old operation committed **no** domain event/result and no publication acceptance; C5's terminal `PublishFailed` has a committed batch root, so it cannot satisfy that proof. Recovery of a partial or zero-Accepted committed set uses a separately approved publication-resume operation that preserves original MessageIds, pins, accepted receipts and first POST bytes; it never invokes the archived command handler anew. Legacy status-6 records without the complete proof hold the same 409 gate until independently reconciled. Keep the public enum, route and response model; denial adds no success claim. A cancellation or lost reply after the gate cannot skip its proof.

## C6. Capability gates, rollout and package compatibility

The **entire** 6.5b ten-item integration handoff is a simultaneous activation checklist:

| Handoff | 6.5c gate / rollback disposition |
| --- | --- |
| 1. Current versus historical proof | Authenticate fresh purpose-10 source separately from A5 historic codec-02 complete save/provider images; valid retained V1 branch-02 and corrupt disposition remain distinct. Never require old commit ETag to equal today's head. |
| 2. Old writer/endpoint fence | Fence pre-existing V1 appenders at gateway **and actor**, reconcile in-flight old writes; new bounded evidence-writing V1 serves only after same-save/receipt probe. V1-only endpoints never receive V2 or mode-bearing work. |
| 3. Purpose-12 selector/trust | Install purpose-12 selector keys and revised TrustMapDigest verifier-first. Attest one new per-domain RegistryFingerprint across serving peers; restart stale paged operations at page 1. Retain historic fixture/pin/key obligations, including the distinct purpose-13 broker grant authority where an accepted route remains owed. |
| 4. Route bounds/capability | Pin B6 complete phase reservation and R/W/legacy-array/deployment budgets; 64 MiB event + 64 MiB prior state + positive work holds. Select only a proven bounded current-type route, including all multi-type capabilities. |
| 5. Retention/rollback | Keep source, purpose-01/02/06/10/11/12 event keys, purpose-13 historical broker-grant keys/trust intervals, purpose-14 catalog-filter, purpose-15 member-nonadmissibility, purpose-1b physical capture-scope, purpose-1d pre-send and purpose-1f no-attempt ledger keys/trust intervals, accepted-revision catalog predicate/input and transform bytes, old approved fingerprints, migration authority/decoder, backend evidence, pins, route decisions/reservation owner-fence histories/reconciliation proofs/receipts, broker-owned full-byte references and obligation fences, signed capture scopes, terminal source records/segments/pre-send and zero-attempt proofs and response records through event/queue/retry/backup/rollback obligations. No capable endpoint → `RollbackReaderCapabilityHold`. |
| 6. Fold/query catalog | Activate B7 `5a` fold-mode and `5b` query catalog rows with pinned deployment capability. Read/Apply/handler semantic change needs authenticated rebuild; F-only write change refreshes transient proof without semantic invalidation. |
| 7. Cache classification | Atomically activate versioned-cache classification and quarantine old entries; no stale cache claims current query or route state. |
| 8. Storage ceilings | Pin B6 1 GiB **per-operation** quota and one **deployment** replay-storage ceiling across replicas, replay and B9 exports; ambiguous deletion retains charge. Named 64 GiB retained state has its own shared cap. |
| 9. Final visibility | Replay, timeline, query, export and named projection rows are visible only through authenticated final pointer/complete proof; partial generations authorize no subscription/command/publication status. |
| 10. Admin timeline | Disclose B9 behavior: target above 1,000 events/64 MiB cumulative timeline state or absent qualified protection returns `TimelineLimit`/`TimelineProtectionHold`, without partial timeline. |

Reader-first activation also needs A3 bounded V1 writer/result ingress, streaming raw provider readback **before typed materialization**, complete valid retained V1 signed manifest/sidecars/branch-02 or independently approved corrupt disposition, historical global MessageId collision inventory and same-backend cross-instance reservation/CAS proof. A5 new V1 branch-01 and V2 complete actor receipt capability precede their reads; a dormant binary advertises no active read capability. Keep old source/wire/DTO constructors and `EventStoreDomainEventContext` binary compatibility through additive optional members; old callers are V1 only under exact alias/capability gate. Package and consumer manifests pin actual implementation/options/transitive managed/native dependency bytes and backend identity; changed package/handler graph cannot advertise unchanged capability. AOT/trimming is not claimed. A missing provider primitive holds affected domain/route; fixture-only hashes and historical V23 probe do not confer readiness. Epic 8 remains optional.

After reader/consumer, effect provider, capture and broker gates pass, enable binary V2 per domain/component/topic under current signed membership. Migrate queued JSON by C4 and reconcile old markers/outboxes/broker unacked counts before removing old ingress. Once V2 history exists, every serving command, replay, projection, rebuild and subscription endpoint in forward **and rollback** deployment must prove V2 read/evolution capability for all retained versions; returning writer mode to V1 does not erase V2 history. Fence a V1-only endpoint before Apply/effect; no capable endpoint is `RollbackReaderCapabilityHold`. A trust-only rotation refreshes transient proof and preserves compatible state after verification; read/Apply or selected handler semantic change requires authenticated replay/rebuild or approved migration. An unknown external member, lost key, changed route set, expired claim or unsupported rollback holds send/effect without acknowledgement. Every active logical consumer needs its own exact lease/probe, even behind one physical route.

Cancellation before preparation CAS leaves zero mutation. After durable preparation but before actor save, release requires proven no commit; after save begins A5 readback decides truth under one bounded recovery attempt then durable ownership/hold. Before first send, cancellation leaves committed outbox/pin pending; after send begins, broker receipt reconciliation precedes retry. After effect commits but before physical ack, redelivery reads receipt and completes only missing routes; cancellation after ack cannot retract it. Projection checkpoints/notification outboxes advance only after their independent fenced readback, with exact-byte notification retry. No cancellation branch labels a valid addressed event poison or removes a committed pin.

## C7. Outcome and future provider vectors

| Frozen matrix or risk | Model check below | Future production/provider/crash proof (unexecuted) |
| --- | --- | --- |
| Retry and key rotation | C01/C02/C07 | Crash after actor save/pin/send/accepted receipt; compare exact source, body, attestation, same-mode bytes, trust interval and current effective route. Changed bytes under one tenant-scoped broker operation key conflict; equal send IDs in different tenants remain independent, while delayed/new member send IDs in the fenced tenant carry the original parent binding and fail its terminal fence; missing old trust holds. |
| Multiple routes | C03/C03b/C03c/C03d/C06 | Two logical routes behind one physical subscription; crash after reservation and after first effect/receipt; exclude the old owner, query uncertain effect, then inspect one exclusive route-decision CAS and full-size effect receipt per route, full handoff, accepted-revision filter input and no broker ack until second receipt. Delay a filtered route beyond ordinary claim expiry with retained acceptance-time proof, then revoke its key and require hold. Leave/rename/provider replacement must drain or preserve the exact historical grant/key; query uncertain old-provider effects before replacement invocation. Chain two takeovers and inspect tag-02 exact handoff/pin hash, every old owner/fence/ETag and reconciliation source through the final decision. |
| Legacy handoff | C04/C04b | Queued original JSON and old marker; compare exact source/route set/side record, whitespace-equivalent decoded value, each bounded exact attempt record, matching/conflicting/ambiguous prior binary pin and changed payload/header including a same-body changed-core/header retry. Crash before/after pointer and each route receipt; an empty historical set needs its signed physical-filter receipt; old physical JSON ack waits for the complete set. |
| Poison/capture | C01b/C04b/C05/C05b | Separate Binary/structured encoded, decoded, attestation and total header boundaries; physically feasible 128 MiB raw/protected-output side-record boundary, malformed/unidentified carrier, structured addressed poison above local AD-31 capture, changed full bytes at stable physical scope, lost blob/index readback and retry exhaustion. Prove purpose-1b isolated physical capture scope, purpose-16 identity, purpose-17 historical nonadmissibility/fence and purpose-18 active retention/obligation-fence source records against complete byte/header readback; no 200 without exact retention and all addressed quarantine decisions or authenticated physical nonadmissibility/full-byte broker retention. |
| Rollout/time | C01/C06/C09 | Equal instant/different offset Binary and structured paths; old endpoint joins after V2 append, purpose-12/key rotation, multi-replica capacity, same-revision configuration/probe/nonce mutation and lease-expiry acceptance race. |
| A8 failure/recovery | C08/C08a/C08b/C08c/C08d/C10 | Empty eventful set, partial Accepted set, definitive rejection later retried, ordered producer/broker fences and final empty observation, signed pre-Prepared retry-policy and acyclic member plan, full 1,024-byte identifiers through compact terminal rows, immutable revision clock/readback, segmented complete roster/attempt/policy proofs including a pre-send reason source and post-fence complete zero-attempt ledger, explicit zero-event no-op without policy, permanent closure and status field projection; deny fresh ReplayController resubmission of a committed `PublishFailed`, including partial acceptance. Inspect exact first pin/head, source records and broker reject fence. |

Models do **not** establish production provider support. Vectors must inspect actor/provider raw bytes and complete A5 generation images; global pin/tombstone; signed broker claims, receipt and physical ack; route effect/receipt counts; encrypted AD-31 capture/readback; first response pin/head; and last-good checkpoint after each injected crash. Run on the configured production provider and a second independent provider or conforming harness. Do not mark activation ready from a runtime build, old tests or local arithmetic.

### Executable local codec and state-model checks

The block reads the unchanged V17 body. C01 checks both `T` placements byte-for-byte; C01b and C04b check independent transport and side-record arithmetic. C02–C10 and focused C03b/C03c/C05b/C08c/C08d model decisions, framing and crash boundaries; injected auth/readback booleans stand for **unexecuted** provider evidence. These small models do not parse real signed claims, typed terminal rows or provider receipts, emulate provider transactions or prove broker linearizability.

```python
from pathlib import Path
import hashlib
import re

draft = Path('_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').read_text()
body = bytes.fromhex(re.search(r'^V17DeliveryBody=([0-9a-f]+)$', draft, re.M).group(1))
assert len(body) == 842
ticks = bytes.fromhex('08de48c8b4f80000')
old = b'\x09' + ticks + b'\x00\x00'
assert body.count(old) == 1

def digest(value):
    return hashlib.sha256(b'HX-EV-DELIVERY-1\0\x01' + len(value).to_bytes(4, 'big') + value).hexdigest()

def offset(minutes):
    return body.replace(old, b'\x09' + ticks + minutes.to_bytes(2, 'big', signed=True))

assert digest(offset(0)) == '54a83b017e8357120374f60a175c438f48b0cd80013e065aa93360670a3f22ad'
assert digest(offset(60)) == 'fa9dcf5c6ce3b8fe79a1b4bb6fee665f65431c8be5e10ac4af34ced59e6fb02e'
assert len(offset(60)) == 842 and offset(0) != offset(60)

def cloud_time(minutes):
    value = offset(minutes)
    marker = b'\x20\x00\x21\x00\x22'
    assert value.count(marker) == 1
    return value.replace(marker, b'\x20\x00\x21\x02' + ticks + minutes.to_bytes(2, 'big', signed=True) + b'\x22')

assert len(cloud_time(0)) == len(cloud_time(60)) == 852
assert digest(cloud_time(0)) == '36cbbdd4d189651f90a150e0cccdd50a78495809e6decd9adc0296195c2444a4'
assert digest(cloud_time(60)) == '6a5b849b4f8399eb61a32becfaa496d970ff4e55a626737895c0bbdd4d70fcc1'
print('C01 T/O(T) equal-instant offset bytes passed')

class Hold(Exception): pass
class Conflict(Exception): pass

def must_raise(error, action):
    try: action()
    except error: return
    raise AssertionError(f'expected {error.__name__}')

MIB = 1 << 20
def carrier_bound(decoded, attestation_decoded, attestation_encoded, encoded_body,
                  header_pairs, mode, framing=0, structured_overhead=0):
    if mode not in ('Binary', 'Structured'): raise Hold()
    if min(decoded, attestation_decoded, attestation_encoded, encoded_body, framing) < 0: raise Hold()
    if len(header_pairs) > 128 or any(min(name, value) < 0 or name + value + 2 > 8192
                                      for name, value in header_pairs): raise Hold()
    headers = sum(name + value + 2 for name, value in header_pairs)
    padded_attestation = 4 * ((attestation_decoded + 2) // 3)
    if mode == 'Binary':
        expected_body = decoded
        if structured_overhead: raise Hold()
    else:
        if structured_overhead < 1: raise Hold()
        expected_body = 4 * ((decoded + 2) // 3) + padded_attestation + structured_overhead
    if (attestation_encoded != padded_attestation or encoded_body != expected_body
        or decoded > 128 * MIB or attestation_decoded > 8 * 1024
        or attestation_encoded > 16 * 1024 or headers > 64 * 1024
        or encoded_body > (128 if mode == 'Binary' else 192) * MIB
        or encoded_body + headers + framing > 193 * MIB): raise Hold()
    return True

assert 4 * ((128 * MIB + 2) // 3) == 178956972
full_structured = 178956972 + 4 * ((8 * 1024 + 2) // 3) + 1024
assert carrier_bound(128 * MIB, 8 * 1024, 10924, full_structured,
                     ((12, 32), (14, 16)), 'Structured', structured_overhead=1024)
assert carrier_bound(128 * MIB, 0, 0, 128 * MIB, (), 'Binary')
must_raise(Hold, lambda: carrier_bound(128 * MIB, 0, 0, 128 * MIB + 1, (), 'Binary'))
must_raise(Hold, lambda: carrier_bound(1, 1, 4, 5, (), 'Structured', structured_overhead=1))
must_raise(Hold, lambda: carrier_bound(1, 1, 4, 9, (), 'Other', structured_overhead=1))
must_raise(Hold, lambda: carrier_bound(1, 1, 4, 1, ((1, 8190),), 'Binary'))
must_raise(Hold, lambda: carrier_bound(1, 1, 4, 1, ((1, 1),) * 129, 'Binary'))
must_raise(Hold, lambda: carrier_bound(128 * MIB + 1, 0, 0, 128 * MIB + 1, (), 'Binary'))
print('C01b canonical mode, encoded/decoded relation and header-pair bounds passed')

class Pin:
    def __init__(self):
        self.by_id = {}; self.first_operation = {}; self.by_operation = {}
        self.receipts = {}; self.accepted = {}
    def create(self, mid, value, attestation, mode_bytes, identity):
        if len(identity) != 4 or not all(isinstance(part, bytes) and part for part in identity): raise Hold()
        candidate = (value, attestation, mode_bytes, identity)  # scope, destination, routing, signed headers
        if mid in self.by_id and self.by_id[mid] != candidate: raise Conflict()
        self.by_id.setdefault(mid, candidate)
        return self.by_id[mid]
    def bind_retry(self, mid, new_operation, parent, prior_operation, proved_rejected, retryable=True):
        if not proved_rejected or not retryable or not new_operation or new_operation in self.by_operation: raise Hold()
        old = self.by_operation.get(prior_operation)
        if old is None or old != (mid, self.by_id.get(mid), parent): raise Conflict()
        if any(row[0] == mid for op, row in self.by_operation.items() if op in self.accepted): raise Conflict()
        prior = sorted((number, state) for (op, number), (_, state) in self.receipts.items()
                       if op == prior_operation)
        if not prior or prior[-1][1] != 'Rejected': raise Hold()
        self.by_operation[new_operation] = (mid, self.by_id[mid], parent)
    def send(self, mid, operation, attempt, nonce, receipt, parent='command'):
        if mid not in self.by_id or not operation or attempt < 1 or not nonce or not parent: raise Hold()
        pinned = self.by_id[mid]
        candidate = (mid, pinned, parent)
        if operation in self.by_operation:
            if self.by_operation[operation] != candidate: raise Conflict()
        else:
            if mid in self.first_operation and self.first_operation[mid] != operation: raise Conflict()
            self.first_operation[mid] = operation
            self.by_operation[operation] = candidate
        if receipt == 'Unknown' or receipt not in ('Accepted', 'Rejected'): raise Hold()
        key = (operation, attempt)
        if key in self.receipts:
            if self.receipts[key] != (nonce, receipt): raise Conflict()
            return self.accepted.get(operation)
        if operation in self.accepted or any(row[0] == mid for op, row in self.by_operation.items()
                                             if op in self.accepted): raise Conflict()
        previous = sorted((number, prior_nonce, state)
                          for (op, number), (prior_nonce, state) in self.receipts.items()
                          if op == operation)
        if attempt != len(previous) + 1 or (previous and previous[-1][2] != 'Rejected'): raise Hold()
        if any(prior_nonce == nonce for _, prior_nonce, _ in previous): raise Conflict()
        self.receipts[key] = (nonce, receipt)  # Immutable result for this attempt/fence nonce.
        if receipt == 'Accepted': self.accepted[operation] = pinned
        return self.accepted.get(operation)

pin = Pin(); plain = offset(0); attestation = b'fixture-attestation'
identity = (b'scope', b'destination', b'routing', b'signed-headers')
assert pin.create('m', plain, attestation, b'binary', identity) == pin.create('m', plain, attestation, b'binary', identity)
must_raise(Conflict, lambda: pin.create('m', offset(60), attestation, b'binary', identity))
must_raise(Conflict, lambda: pin.create('m', plain, attestation, b'changed-rendering', identity))
for changed_part in range(4):
    altered = list(identity); altered[changed_part] = b'changed'
    must_raise(Conflict, lambda altered=altered: pin.create('m', plain, attestation, b'binary', tuple(altered)))
must_raise(Hold, lambda: pin.send('m', 'op', 1, b'n1', 'Unknown'))
assert pin.send('m', 'op', 1, b'n1', 'Rejected') is None
assert pin.send('m', 'op', 1, b'n1', 'Rejected') is None
must_raise(Conflict, lambda: pin.send('m', 'op', 1, b'n1', 'Accepted'))
must_raise(Conflict, lambda: pin.send('m', 'op', 1, b'changed', 'Rejected'))
must_raise(Conflict, lambda: pin.send('m', 'op', 2, b'n1', 'Accepted'))
must_raise(Hold, lambda: pin.send('m', 'op', 3, b'n3', 'Accepted'))
assert pin.send('m', 'op', 2, b'n2', 'Accepted') == (plain, attestation, b'binary', identity)
assert pin.send('m', 'op', 2, b'n2', 'Accepted') == (plain, attestation, b'binary', identity)
assert pin.send('m', 'op', 1, b'n1', 'Rejected') == (plain, attestation, b'binary', identity)
must_raise(Conflict, lambda: pin.send('m', 'op', 2, b'n2', 'Rejected'))
must_raise(Conflict, lambda: pin.send('m', 'op', 3, b'n3', 'Rejected'))
must_raise(Conflict, lambda: pin.send('m', 'different-op', 1, b'n3', 'Accepted'))
must_raise(Conflict, lambda: pin.send('m', 'op', 1, b'n1', 'Rejected', parent='other-parent'))
retry_pin = Pin()
retry_pin.create('retry-member', plain, attestation, b'binary', identity)
assert retry_pin.send('retry-member', 'send-1', 1, b'nonce-1', 'Rejected') is None
must_raise(Hold, lambda: retry_pin.bind_retry('retry-member', 'send-2', 'command', 'send-1', False))
must_raise(Hold, lambda: retry_pin.bind_retry('retry-member', 'send-2', 'command', 'send-1', True, retryable=False))
must_raise(Conflict, lambda: retry_pin.bind_retry('retry-member', 'send-2', 'other-parent', 'send-1', True))
retry_pin.bind_retry('retry-member', 'send-2', 'command', 'send-1', True)
assert retry_pin.send('retry-member', 'send-2', 1, b'nonce-2', 'Accepted') == (plain, attestation, b'binary', identity)
must_raise(Conflict, lambda: retry_pin.send('retry-member', 'send-2', 1, b'nonce-2', 'Accepted', parent='other-parent'))
must_raise(Conflict, lambda: retry_pin.bind_retry('retry-member', 'send-3', 'command', 'send-2', True))
assert pin.create('other-message', plain, attestation, b'binary', identity)
must_raise(Conflict, lambda: pin.send('other-message', 'op', 1, b'other-nonce', 'Accepted'))
must_raise(Hold, lambda: pin.send('m', 'op', 3, b'n3', 'Forged'))
print('C02 scoped pin, parent-bound new send after proved rejection and immutable receipts passed')

class Routes:
    def __init__(self, names, pinned, physical_filter=None):
        if tuple(names) != tuple(sorted(set(names))): raise Conflict()
        self.names = tuple(names); self.pinned = pinned
        self.effects = {}; self.decisions = {}; self.physical_filter = physical_filter
    def admit(self, names, pinned):
        if tuple(names) != self.names or pinned != self.pinned: raise Conflict()
    def commit(self, route):
        if route not in self.names: raise Hold()
        if route in self.decisions:
            if self.decisions[route][0] != 'Completed': raise Conflict()
            return
        self.decisions[route] = ('EffectReserved', None)
        self.effects[route] = self.effects.get(route, 0) + 1
        self.decisions[route] = ('Completed', (self.pinned, route, 'effect-receipt', True))
    def filter(self, route, proof):
        if route not in self.names or proof != (self.pinned, route, 'accepted-input', 'signed-predicate', True): raise Hold()
        if route in self.decisions and self.decisions[route] != ('Filtered', proof): raise Conflict()
        self.decisions[route] = ('Filtered', proof)
    def quarantine(self, route, proof):
        if route not in self.names or proof != (self.pinned, route, 'full-byte-capture', True): raise Hold()
        if route in self.decisions and self.decisions[route] != ('Quarantined', proof): raise Conflict()
        self.decisions[route] = ('Quarantined', proof)
    def ack(self, poison=False):
        if not self.names and self.physical_filter != (self.pinned, 'signed-physical-filter', True): raise Hold()
        if set(self.decisions) != set(self.names): raise Hold()
        for name, (state, proof) in self.decisions.items():
            expected = {'Completed': (self.pinned, name, 'effect-receipt', True),
                        'Filtered': (self.pinned, name, 'accepted-input', 'signed-predicate', True),
                        'Quarantined': (self.pinned, name, 'full-byte-capture', True)}.get(state)
            if proof != expected or (state == 'Quarantined' and not poison): raise Hold()
        return True

routes = Routes(('a', 'b'), (plain, attestation)); routes.commit('a')
must_raise(Hold, routes.ack)
routes.admit(('a', 'b'), (plain, attestation)); routes.commit('a'); routes.commit('b')
assert routes.ack() and routes.effects == {'a': 1, 'b': 1}
must_raise(Conflict, lambda: routes.filter('a', ((plain, attestation), 'a', 'accepted-input', 'signed-predicate', True)))
must_raise(Conflict, lambda: routes.admit(('a',), (plain, attestation)))
must_raise(Conflict, lambda: routes.admit(('a', 'b'), (offset(60), attestation)))
must_raise(Conflict, lambda: Routes(('a', 'a'), (plain, attestation)))
must_raise(Hold, Routes((), (plain, attestation)).ack)
assert Routes((), (plain, attestation), physical_filter=((plain, attestation), 'signed-physical-filter', True)).ack()
filtered = Routes(('a',), (plain, attestation))
must_raise(Hold, lambda: filtered.filter('a', None))
must_raise(Hold, lambda: filtered.filter('a', ((plain, attestation), 'a', 'later-input', 'signed-predicate', True)))
filtered.filter('a', ((plain, attestation), 'a', 'accepted-input', 'signed-predicate', True))
assert filtered.ack() and filtered.effects == {}
must_raise(Conflict, lambda: filtered.commit('a'))
poison = Routes(('a',), (plain, attestation))
poison.quarantine('a', ((plain, attestation), 'a', 'full-byte-capture', True))
must_raise(Hold, poison.ack)
assert poison.ack(poison=True)
print('C03 unique complete routes, exclusive terminal decisions and authenticated receipts passed')

def receipt_capacity(identifiers, backend_descriptor, actual_etag):
    if len(identifiers) != 9 or any(not isinstance(x, bytes) or not 1 <= len(x) <= 1024
                                      for x in identifiers): raise Hold()
    if not isinstance(backend_descriptor, bytes) or len(backend_descriptor) > 1024: raise Hold()
    if not isinstance(actual_etag, bytes) or not 1 <= len(actual_etag) <= 1024: raise Hold()
    u = lambda x: 4 + len(x)
    # All U values, exact backend B, fixed fields, tags and domain/version/count.
    encoded = sum(map(u, identifiers)) + 4 + len(backend_descriptor) + u(actual_etag) + 256
    if encoded > 16 * 1024: raise Hold()
    return encoded

assert receipt_capacity((b'i' * 1024,) * 9, b'b' * 1024, b'e' * 1024) <= 16 * 1024
must_raise(Hold, lambda: receipt_capacity((b'i' * 1024,) * 9, b'b' * 1025, b'e'))
must_raise(Hold, lambda: receipt_capacity((b'i' * 1024,) * 8, b'b', b'e'))
print('C03d admitted full identifier/provider receipt capacity passed')

class EffectReservation:
    def __init__(self):
        self.owner = None; self.fence = 0; self.expiry = 0; self.effect_count = 0; self.history = []
    def reserve(self, owner, now):
        if self.owner is not None or not isinstance(owner, str) or not owner.strip(): raise Hold()
        self.owner = owner; self.fence = 1; self.expiry = now + 60
    def takeover(self, owner, now, old_owner_excluded, uncertain_receipt):
        if (self.owner is None or now < self.expiry or not old_owner_excluded
            or not isinstance(owner, str) or not owner.strip() or len(self.history) >= 64
            or uncertain_receipt not in ('Absent', 'Committed')): raise Hold()
        previous = self.history[-1] if self.history else None
        self.history.append((self.owner, self.fence, self.expiry, old_owner_excluded,
                             uncertain_receipt, previous))  # Append-only predecessor stand-in.
        self.owner = owner; self.fence += 1; self.expiry = now + 60
        if uncertain_receipt == 'Committed': self.effect_count = 1
        return 'finalize-old-receipt' if uncertain_receipt == 'Committed' else 'invoke-under-new-fence'
    def invoke(self, owner, fence, now, absence_proved):
        if owner != self.owner or fence != self.fence or now >= self.expiry or not absence_proved: raise Hold()
        if self.effect_count: raise Conflict()
        self.effect_count += 1

must_raise(Hold, lambda: EffectReservation().reserve(None, 0))
must_raise(Hold, lambda: EffectReservation().reserve('', 0))
reserved = EffectReservation(); reserved.reserve('old', 0)
must_raise(Hold, lambda: reserved.takeover('new', 59, True, 'Absent'))
must_raise(Hold, lambda: reserved.takeover('new', 61, False, 'Absent'))
must_raise(Hold, lambda: reserved.takeover('new', 61, True, 'Unknown'))
must_raise(Hold, lambda: reserved.takeover('', 61, True, 'Absent'))
assert reserved.takeover('new', 61, True, 'Absent') == 'invoke-under-new-fence'
assert len(reserved.history) == 1 and reserved.history[0][:2] == ('old', 1)
must_raise(Hold, lambda: reserved.invoke('old', 1, 61, True))
must_raise(Hold, lambda: reserved.invoke('new', 2, 61, False))
reserved.invoke('new', 2, 61, True)
must_raise(Conflict, lambda: reserved.invoke('new', 2, 61, True))
committed = EffectReservation(); committed.reserve('old', 0)
assert committed.takeover('new', 61, True, 'Committed') == 'finalize-old-receipt'
must_raise(Conflict, lambda: committed.invoke('new', 2, 61, True))
repeated = EffectReservation(); repeated.reserve('first', 0)
assert repeated.takeover('second', 61, True, 'Absent') == 'invoke-under-new-fence'
assert repeated.takeover('third', 122, True, 'Absent') == 'invoke-under-new-fence'
assert repeated.history[1][-1] == repeated.history[0] and repeated.history[0][:2] == ('first', 1)
print('C03b interrupted reservation, owner exclusion and uncertain-effect recovery passed')

def catalog_filter(claim, accepted_input, accepted_revision, signed, unrevoked, transform_available, accepted_route='route-a'):
    scope, revision, registry, transform, language, rows = claim
    tenant, domain, event_type, version, source, input_registry, input_transform = accepted_input
    if (not signed or not unrevoked or not transform_available or scope != accepted_route or not source
        or revision != accepted_revision or registry != input_registry or transform != input_transform
        or language != 'HX-EV-SELECTOR-1' or not 1 <= len(rows) <= 64
        or tuple(rows) != tuple(sorted(set(rows)))): raise Hold()
    if any(not 1 <= row[3] <= row[4] <= 1024 for row in rows): raise Hold()
    matched = any((tenant, domain, event_type) == row[:3] and row[3] <= version <= row[4]
                  for row in rows)
    return 'Addressed' if matched else 'Filtered'

catalog_claim = ('route-a', 7, b'registry-7', b'transform-7', 'HX-EV-SELECTOR-1',
                 (('tenant', 'domain', 'type-a', 1, 3),))
accepted_input = ('tenant', 'domain', 'type-b', 2, b'source', b'registry-7', b'transform-7')
assert catalog_filter(catalog_claim, accepted_input, 7, True, True, True) == 'Filtered'
assert catalog_filter(catalog_claim, ('tenant', 'domain', 'type-a', 2, b'source',
                                     b'registry-7', b'transform-7'), 7, True, True, True) == 'Addressed'
must_raise(Hold, lambda: catalog_filter(catalog_claim, accepted_input, 7, True, True, True, 'route-b'))
must_raise(Hold, lambda: catalog_filter(catalog_claim, accepted_input, 7, False, True, True))
must_raise(Hold, lambda: catalog_filter(catalog_claim, accepted_input, 7, True, False, True))
must_raise(Hold, lambda: catalog_filter(catalog_claim, accepted_input, 7, True, True, False))
must_raise(Hold, lambda: catalog_filter(catalog_claim, accepted_input[:-1] + (b'later-transform',), 7,
                                        True, True, True))
must_raise(Hold, lambda: catalog_filter(catalog_claim[:4] + ('unknown', catalog_claim[5]),
                                        accepted_input, 7, True, True, True))
print('C03c signed accepted-revision selector and input guards passed')





class Handoff:
    def __init__(self):
        self.original = None; self.pointer = None; self.broker_accepted = False
        self.results = {}; self.attempts = {}; self.pin = None; self.physical_filter = None; self.core_header = None
    def stage(self, source, names, pinned, attempt, canonical, prior_binary=None, complete=True,
              no_prior_pin_send_proof=False, historical_empty=False, core_header=b"exact-core-headers"):
        if not names and not historical_empty: raise Hold()  # Historical empty scope requires signed filtering.
        if tuple(names) != tuple(sorted(set(names))): raise Conflict()
        if prior_binary == 'ambiguous': raise Hold()
        if not core_header: raise Hold()
        if self.core_header is not None and self.core_header != core_header: raise Conflict()
        if self.pin is None and prior_binary is None and not no_prior_pin_send_proof: raise Hold()
        if prior_binary is not None and prior_binary != pinned: raise Conflict()
        candidate = (source, tuple(names), pinned, canonical)
        if self.original is not None and candidate != self.original: raise Conflict()
        if attempt in self.attempts and self.attempts[attempt] != (candidate, core_header): raise Conflict()
        if len(self.attempts) >= 64 and attempt not in self.attempts: raise Hold()
        self.attempts[attempt] = (candidate, core_header)  # Exact attempt side record is create-once/read back.
        self.original = candidate
        self.core_header = core_header
        if self.pin is not None and self.pin != pinned: raise Conflict()
        self.pin = pinned
        if complete: self.pointer = candidate
    def accept_binary(self):
        if self.pointer is None: raise Hold()
        self.broker_accepted = True
    def filter_empty(self, proof):
        if not self.broker_accepted or self.pointer is None or self.pointer[1]: raise Hold()
        expected = (self.pointer, 'signed-historical-physical-filter', True)
        if proof != expected: raise Hold()
        if self.physical_filter is not None and self.physical_filter != proof: raise Conflict()
        self.physical_filter = proof
    def route_result(self, name, result, proof=None, permanent_poison=False):
        if not self.broker_accepted or name not in self.pointer[1]: raise Hold()
        kind = {'Completed': 'authenticated-effect-receipt',
                'Filtered': 'authenticated-filter', 'Quarantined': 'authenticated-capture'}.get(result)
        expected = (self.pointer, name, kind, 'permanent-source-poison', True) if result == 'Quarantined' else (self.pointer, name, kind, True)
        if kind is None or proof != expected or (result == 'Quarantined' and not permanent_poison): raise Hold()
        if name in self.results and self.results[name] != (result, proof): raise Conflict()
        self.results[name] = (result, proof)  # One terminal CAS key across outcomes.
    def ack(self):
        if self.pointer is None or self.pointer != self.original or not self.broker_accepted: raise Hold()
        if not self.pointer[1] and self.physical_filter != (self.pointer, 'signed-historical-physical-filter', True): raise Hold()
        if set(self.results) != set(self.pointer[1]): raise Hold()
        for name, (result, proof) in self.results.items():
            kind = {'Completed': 'authenticated-effect-receipt',
                    'Filtered': 'authenticated-filter', 'Quarantined': 'authenticated-capture'}.get(result)
            expected = (self.pointer, name, kind, 'permanent-source-poison', True) if result == 'Quarantined' else (self.pointer, name, kind, True)
            if kind is None or proof != expected: raise Hold()
        return True

handoff = Handoff(); source = ('pub', 'topic', 'sub', 7, 'm'); canonical = b'canonical-source'
must_raise(Hold, lambda: handoff.stage(source, ('a', 'b'), (plain, attestation), b'exact-json-1', canonical))
handoff.stage(source, ('a', 'b'), (plain, attestation), b'exact-json-1', canonical,
              complete=False, no_prior_pin_send_proof=True)
must_raise(Hold, handoff.ack)
handoff.stage(source, ('a', 'b'), (plain, attestation), b'exact-json-1', canonical)
must_raise(Hold, handoff.ack)
handoff.accept_binary()
must_raise(Hold, lambda: handoff.route_result('a', 'Completed'))
handoff.route_result('a', 'Completed', (handoff.pointer, 'a', 'authenticated-effect-receipt', True))
must_raise(Hold, handoff.ack)
handoff.stage(source, ('a', 'b'), (plain, attestation), b'{ "same":true }', canonical,
              prior_binary=(plain, attestation))
must_raise(Hold, lambda: handoff.route_result('b', 'Filtered'))
handoff.route_result('b', 'Filtered', (handoff.pointer, 'b', 'authenticated-filter', True))
assert handoff.ack()
handoff.route_result('a', 'Completed', (handoff.pointer, 'a', 'authenticated-effect-receipt', True))
must_raise(Conflict, lambda: handoff.route_result('a', 'Filtered', (handoff.pointer, 'a', 'authenticated-filter', True)))
must_raise(Conflict, lambda: handoff.stage(source, ('a',), (plain, attestation), b'other', canonical))
must_raise(Conflict, lambda: handoff.stage(source, ('a', 'b'), (plain, attestation), b'other', b'changed'))
must_raise(Conflict, lambda: handoff.stage(source, ('a', 'b'), (plain, attestation),
                                           b'other-header', canonical, core_header=b'changed-header'))
must_raise(Conflict, lambda: handoff.stage(source, ('a', 'b'), (plain, attestation),
                                           b'exact-json-1', canonical, core_header=b'changed-header'))
must_raise(Conflict, lambda: handoff.stage(source, ('a', 'b'), (plain, attestation), b'other',
                                           canonical, prior_binary=(offset(60), attestation)))
must_raise(Hold, lambda: Handoff().stage(source, ('a', 'b'), (plain, attestation),
                                        b'exact-json-1', canonical, prior_binary='ambiguous'))
must_raise(Hold, lambda: Handoff().stage(source, (), (plain, attestation), b'exact-json-1', canonical))
empty_handoff = Handoff()
empty_handoff.stage(source, (), (plain, attestation), b'exact-json-1', canonical,
                    no_prior_pin_send_proof=True, historical_empty=True)
empty_handoff.accept_binary()
must_raise(Hold, empty_handoff.ack)
must_raise(Hold, lambda: empty_handoff.filter_empty((empty_handoff.pointer, 'unsigned-filter', True)))
empty_handoff.filter_empty((empty_handoff.pointer, 'signed-historical-physical-filter', True))
assert empty_handoff.ack()
poison_handoff = Handoff()
poison_handoff.stage(source, ('a',), (plain, attestation), b'poison-json', canonical,
                     no_prior_pin_send_proof=True)
poison_handoff.accept_binary()
poison_proof = (poison_handoff.pointer, 'a', 'authenticated-capture', 'permanent-source-poison', True)
must_raise(Hold, lambda: poison_handoff.route_result('a', 'Quarantined', poison_proof))
must_raise(Hold, lambda: poison_handoff.route_result('a', 'Quarantined',
                                                  (poison_handoff.pointer, 'a', 'authenticated-capture', True),
                                                  permanent_poison=True))
poison_handoff.route_result('a', 'Quarantined', poison_proof, permanent_poison=True)
assert poison_handoff.ack()
print('C04 exact core/header variants, first pin, poison guard and full handoff ack passed')

def side_record_bound(raw_body, headers, core, protected_output, outer_framing, expansion_bound):
    if (expansion_bound is None or expansion_bound > MIB
        or min(raw_body, headers, core, protected_output, outer_framing) < 0
        or raw_body > 128 * MIB or headers > 64 * 1024 or core > MIB): raise Hold()
    encrypted_input = raw_body + headers + core
    if (protected_output < encrypted_input or protected_output > encrypted_input + expansion_bound
        or protected_output + outer_framing > 193 * MIB): raise Hold()
    return True

assert side_record_bound(128 * MIB, 64 * 1024, 1024, 129 * MIB, MIB, MIB)
assert side_record_bound(128 * MIB, 64 * 1024, 1024, 128 * MIB + 64 * 1024 + 1024, MIB, MIB)
must_raise(Hold, lambda: side_record_bound(128 * MIB, 64 * 1024, 1024, 128 * MIB, MIB, MIB))
must_raise(Hold, lambda: side_record_bound(128 * MIB, 64 * 1024, 1024, 129 * MIB, MIB, None))
must_raise(Hold, lambda: side_record_bound(128 * MIB, 64 * 1024, 1024, 129 * MIB, 64 * MIB + 1, MIB))
print('C04b feasible retained-output and side-record budget passed')

def capture_ack(raw, maximum, authenticated, retained, readback, addressed, quarantined,
                broker_full_reference=False, physical_filter=None):
    if not authenticated or not retained or not readback: raise Hold()
    if len(raw) > maximum and not broker_full_reference: raise Hold()
    if not addressed and physical_filter != ('signed-physical-filter', True): raise Hold()
    if set(quarantined) != set(addressed) or any(quarantined[name] is not True for name in addressed): raise Hold()
    return True

class PhysicalQuarantine:
    def __init__(self): self.by_delivery = {}
    def acknowledge(self, component, topic, subscription, broker_delivery_id, raw, headers,
                    retained, readback, signed_nonadmissible, stable_id_proved,
                    scope, signed_scope, active_retention_fence):
        if not all((component, topic, subscription, broker_delivery_id, retained, readback,
                    signed_nonadmissible, stable_id_proved, scope, signed_scope,
                    active_retention_fence)): raise Hold()
        key = (scope, component, topic, subscription, broker_delivery_id)
        exact = (raw, headers, signed_nonadmissible, signed_scope, active_retention_fence)
        if key in self.by_delivery and self.by_delivery[key] != exact: raise Conflict()
        self.by_delivery[key] = exact
        return True

must_raise(Hold, lambda: capture_ack(b'x' * 17, 16, True, True, True, ('a',), {'a': True}))
must_raise(Hold, lambda: capture_ack(b'x', 16, False, True, True, ('a',), {'a': True}))
must_raise(Hold, lambda: capture_ack(b'x', 16, True, True, False, ('a',), {'a': True}))
must_raise(Hold, lambda: capture_ack(b'x', 16, True, True, True, ('a', 'b'), {'a': True}))
must_raise(Hold, lambda: capture_ack(b'x', 16, True, True, True, (), {}))
assert capture_ack(b'x' * 17, 16, True, True, True, ('a',), {'a': True}, broker_full_reference=True)
assert capture_ack(b'x', 16, True, True, True, (), {}, physical_filter=('signed-physical-filter', True))
quarantine = PhysicalQuarantine()
q = lambda raw=b'bad', topic='t', scope='broker-isolation', signed=True, fence=True: quarantine.acknowledge(
    'c', topic, 'sub', 'stable', raw, b'h', True, True, True, True, scope, signed, fence)
must_raise(Hold, lambda: quarantine.acknowledge('c', 't', 'sub', 'stable', b'bad', b'h',
                                                True, False, True, True, 'broker-isolation', True, True))
must_raise(Hold, lambda: q(signed=False))
must_raise(Hold, lambda: q(fence=False))
assert q()
assert q()
assert q(raw=b'other', topic='other')
assert q(scope='another-signed-isolation')
must_raise(Conflict, lambda: q(raw=b'changed'))
print('C05 physical scope, active retention fence and exact full-byte readback passed')

def quarantine_reference(authority, key, proof_hash, full_byte_readback, ad31_verified, broker_verified):
    if (authority not in (0, 1) or not key or len(proof_hash) != 32 or not full_byte_readback): raise Hold()
    if authority == 0 and not ad31_verified: raise Hold()
    if authority == 1 and not broker_verified: raise Hold()
    inner = bytes((authority,)) + len(key).to_bytes(4, 'big') + key + proof_hash
    return len(inner).to_bytes(4, 'big') + inner  # Exact outer B for C3 tag 08 / C4 tag 05.

ad31_ref = quarantine_reference(0, b'capture-key', b'p' * 32, True, True, False)
broker_ref = quarantine_reference(1, b'broker-reference', b'p' * 32, True, False, True)
assert ad31_ref[4] == 0 and broker_ref[4] == 1 and ad31_ref != broker_ref
must_raise(Hold, lambda: quarantine_reference(1, b'broker-reference', b'p' * 32, True, True, False))
must_raise(Hold, lambda: quarantine_reference(0, b'capture-key', b'p' * 32, True, False, True))
must_raise(Hold, lambda: quarantine_reference(2, b'untagged', b'p' * 32, True, True, True))
must_raise(Hold, lambda: quarantine_reference(1, b'broker-reference', b'p' * 32, False, False, True))
print('C05b tagged quarantine authority and full-byte readback guards passed')



class Broker:
    def __init__(self):
        self.revision = 7; self.config = b'config-7'; self.probe = b'probe-7'
        self.nonce = b'nonce-7'; self.lease_expiries = (101, 101)
        self.fence_expiry = 100; self.owed = set(); self.accepted = {}
        self.parents = {}; self.fenced_parents = set(); self.domain = 'domain'
    def bind(self, operation, parent, scope_hash, pin_hash, tenant='tenant-a',
             destination=('component', 'topic'), authenticated=True):
        if not authenticated or not all((operation, parent, scope_hash, pin_hash, tenant, *destination)): raise Hold()
        candidate = (parent, scope_hash, pin_hash, destination)
        key = (tenant, operation)
        if key in self.parents and self.parents[key] != candidate: raise Conflict()
        self.parents[key] = candidate
    def fence_parent(self, tenant, scope_hash): self.fenced_parents.add((tenant, scope_hash))
    def accept(self, operation, revision, config, probe, nonce, mode_bytes, pin_hash, now,
               tenant='tenant-a', destination=('component', 'topic')):
        binding = self.parents.get((tenant, operation))
        if binding is None or binding[3] != destination: raise Hold()
        if (tenant, binding[1]) in self.fenced_parents: raise Hold()  # Before duplicate-key lookup.
        if (revision != self.revision or config != self.config or probe != self.probe
            or nonce != self.nonce or now >= self.fence_expiry
            or not all(now < expiry for expiry in self.lease_expiries)): raise Hold()
        key = (tenant, self.domain, *destination, operation)  # Digest is immutable value.
        candidate = (mode_bytes, pin_hash, binding)
        if key in self.accepted:
            if self.accepted[key] != candidate: raise Conflict()
            return True  # Closed route must not become owed again.
        if binding[2] != pin_hash: raise Hold()
        self.accepted[key] = candidate
        self.owed.add((tenant, operation, destination, 'old-route'))
        return True
    def change(self, revision, config, mapped=False):
        if self.owed and not mapped: raise Hold()
        self.revision = revision; self.config = config; self.nonce = b'new-nonce'
    def complete(self, operation, tenant='tenant-a', destination=('component', 'topic')):
        self.owed.remove((tenant, operation, destination, 'old-route'))

broker = Broker()
must_raise(Hold, lambda: broker.accept('op', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 99))
broker.bind('op', 'command', b'scope-op-hash', b'pin')
broker.bind('other', 'other-command', b'other-scope-hash', b'pin')
must_raise(Conflict, lambda: broker.bind('op', 'different-parent', b'scope-op-hash', b'pin'))
assert broker.accept('op', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 99)
must_raise(Conflict, lambda: broker.accept('op', 7, b'config-7', b'probe-7', b'nonce-7',
                                           b'changed-bytes', b'pin', 99))
must_raise(Conflict, lambda: broker.accept('op', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'changed-pin', 99))
must_raise(Hold, lambda: broker.accept('other', 7, b'changed', b'probe-7', b'nonce-7', b'bytes', b'pin', 99))
must_raise(Hold, lambda: broker.accept('other', 7, b'config-7', b'changed', b'nonce-7', b'bytes', b'pin', 99))
must_raise(Hold, lambda: broker.accept('other', 7, b'config-7', b'probe-7', b'changed', b'bytes', b'pin', 99))
must_raise(Hold, lambda: broker.accept('other', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 100))
broker.bind('op', 'tenant-b-command', b'tenant-b-scope', b'pin', tenant='tenant-b')
assert broker.accept('op', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 99, tenant='tenant-b')
must_raise(Hold, lambda: broker.change(8, b'config-8'))  # Leave cannot strand either tenant.
broker.complete('op', tenant='tenant-b')
broker.complete('op')
assert broker.accept('op', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 99)
assert not broker.owed  # Duplicate Accepted must not reopen completed route.
broker.fence_parent('tenant-a', b'scope-op-hash')
must_raise(Hold, lambda: broker.accept('op', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 99))
broker.bind('future-send', 'command', b'scope-op-hash', b'pin')
must_raise(Hold, lambda: broker.accept('future-send', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 99))
broker.bind('other-destination-send', 'command', b'scope-op-hash', b'pin',
            destination=('other-component', 'other-topic'))
must_raise(Hold, lambda: broker.accept('other-destination-send', 7, b'config-7', b'probe-7',
                                       b'nonce-7', b'bytes', b'pin', 99,
                                       destination=('other-component', 'other-topic')))
assert broker.accept('op', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 99, tenant='tenant-b')
broker.change(8, b'config-8')
must_raise(Hold, lambda: broker.accept('other', 7, b'config-7', b'probe-7', b'nonce-7', b'bytes', b'pin', 99))
print('C06 tenant-scoped unique key and two-destination parent fence passed')

def rotated_delivery(old_pin, source, current_view, old_key=True):
    if not old_key or old_pin is None or source is None or current_view is None: raise Hold()
    if old_pin[0] != source['body'] or source['digest'] != current_view['source_digest']: raise Conflict()
    return True

source = {'body': plain, 'digest': 'stored'}
assert rotated_delivery((plain, attestation), source, {'source_digest': 'stored'})
must_raise(Hold, lambda: rotated_delivery((plain, attestation), source, None))
must_raise(Hold, lambda: rotated_delivery((plain, attestation), source, {'source_digest': 'stored'}, old_key=False))
print('C07 old pin/current route and key hold passed')

TERMINAL_ORDER = ('producer-disable', 'broker-reject', 'reconcile', 'drain', 'final-empty', 'terminal-cas')
def outcome(states, terminal=False, proofs=(), fence_active=False, no_op=False, sequence=()):
    if no_op:
        if states or terminal: raise Hold()
        return 'not-applicable'
    if not states or any(state not in ('accepted', 'unknown', 'pending', 'failed') for state in states): raise Hold()
    if all(state == 'accepted' for state in states):
        if terminal: raise Conflict()  # Terminal proof contradicts complete publication.
        return 'published'
    private = 'unknown' if 'unknown' in states else 'pending' if 'pending' in states else 'failed'
    assert private in ('pending', 'failed', 'unknown')  # Closed five-state outcome codec with above branches.
    if terminal:
        if private != 'failed': raise Conflict()
        if (proofs != ('authenticated-drain', 'broker-reject-fence', 'signed-policy')
            or not fence_active or sequence != TERMINAL_ORDER):
            raise Hold()
        return 'PublishFailed'
    if private == 'failed': raise Hold()  # Status inspection; private revision remains pinned.
    return private

def prepared_branch(event_count, no_op_witness, signed_policy, publication_members):
    if event_count == 0:
        if not no_op_witness or signed_policy or publication_members: raise Hold()
        return 'not-applicable'
    if event_count < 0 or no_op_witness or not signed_policy or publication_members != event_count: raise Hold()
    return 'eventful'

assert prepared_branch(0, True, False, 0) == 'not-applicable'
must_raise(Hold, lambda: prepared_branch(0, True, True, 0))
must_raise(Hold, lambda: prepared_branch(0, True, False, 1))
must_raise(Hold, lambda: prepared_branch(1, True, True, 1))
assert prepared_branch(1, False, True, 1) == 'eventful'
print('C08a no-op branch precedes eventful retry policy passed')

def fresh_replay(status, committed_batch, accepted_members, safe_no_commit_proof):
    if status == 'PublishFailed' and (committed_batch or accepted_members or not safe_no_commit_proof): raise Hold()
    return True

must_raise(Hold, lambda: outcome(()))
assert outcome((), no_op=True) == 'not-applicable'
assert outcome(('accepted', 'accepted')) == 'published'
assert outcome(('accepted', 'pending')) == 'pending'
must_raise(Hold, lambda: outcome(('accepted', 'invalid')))
must_raise(Hold, lambda: outcome(('accepted', 'failed')))
must_raise(Hold, lambda: outcome(('accepted', 'failed'), terminal=True))
must_raise(Hold, lambda: outcome(('accepted', 'failed'), terminal=True,
                                proofs=('authenticated-drain', 'broker-reject-fence', 'signed-policy')))
assert outcome(('accepted', 'failed'), terminal=True,
               proofs=('authenticated-drain', 'broker-reject-fence', 'signed-policy'),
               fence_active=True, sequence=TERMINAL_ORDER) == 'PublishFailed'
must_raise(Hold, lambda: outcome(('accepted', 'failed'), terminal=True,
                                proofs=('authenticated-drain', 'broker-reject-fence', 'signed-policy'),
                                fence_active=True, sequence=('final-empty', 'broker-reject')))
must_raise(Conflict, lambda: outcome(('accepted',), terminal=True,
                                    proofs=('authenticated-drain', 'broker-reject-fence', 'signed-policy'),
                                    fence_active=True))
must_raise(Hold, lambda: fresh_replay('PublishFailed', True, 1, True))
must_raise(Hold, lambda: fresh_replay('PublishFailed', True, 0, True))
assert fresh_replay('PublishFailed', False, 0, True)  # Independent no-commit proof, not C5 terminal.
print('C08 closed outcomes, ordered terminal proof/conflict and committed replay gate passed')

def terminal_segments(rows, maximum, per_member, segment_limit, allow_empty=False):
    if len(rows) > maximum or (not rows and not allow_empty): raise Hold()
    if tuple(rows) != tuple(sorted(set(rows))): raise Hold()
    if len({(member, ordinal) for member, ordinal, _ in rows}) != len(rows): raise Hold()
    by_member = {}
    for member, ordinal, size in rows:
        if member < 1 or member > 1000 or size < 1 or size > 512: raise Hold()
        by_member.setdefault(member, []).append(ordinal)
    if any(ordinals != list(range(1, len(ordinals) + 1)) or len(ordinals) > per_member
           for ordinals in by_member.values()): raise Hold()
    segment_sizes = []; segment_counts = []
    for _, _, size in rows:
        framed = size + 4  # Checked u32 row length.
        if not segment_sizes or segment_sizes[-1] + framed > 64 * 1024 or segment_counts[-1] == 125:
            segment_sizes.append(1024)  # Worst permitted scoped segment header.
            segment_counts.append(0)
        segment_sizes[-1] += framed
        segment_counts[-1] += 1
    descriptor_bytes = 40 + 40 * len(segment_sizes)
    if (len(segment_sizes) > segment_limit
        or descriptor_bytes > (24 * 1024 if segment_limit == 512 else 1024)): raise Hold()
    return len(segment_sizes)

attempt_rows = tuple((i // 64 + 1, i % 64 + 1, 512) for i in range(130))
assert terminal_segments(attempt_rows, 64000, 64, 512) == 2
assert terminal_segments((), 64000, 64, 512, allow_empty=True) == 0
assert (64000 + 124) // 125 == 512  # 512-byte row + u32 framing + 1 KiB header.
must_raise(Hold, lambda: terminal_segments(((1, 1, 512), (1, 1, 512)), 64000, 64, 512))
must_raise(Hold, lambda: terminal_segments(((1, 1, 511), (1, 1, 512)), 64000, 64, 512))
must_raise(Hold, lambda: terminal_segments(tuple((1, i, 512) for i in range(1, 66)), 64000, 64, 512))
must_raise(Hold, lambda: terminal_segments(((1001, 1, 512),), 64000, 64, 512))
must_raise(Hold, lambda: terminal_segments(((1, 1, 513),), 64000, 64, 512))
policy_rows = tuple((i + 1, 1, 512) for i in range(1000))
assert terminal_segments(policy_rows, 1000, 1, 8) == 8
must_raise(Hold, lambda: terminal_segments(policy_rows + ((1001, 1, 512),), 1000, 1, 8))
print('C08b segmented attempt/policy source-record boundaries passed')

def member_proof_hash(record, carrier):
    B = lambda value: len(value).to_bytes(4, 'big') + value
    return hashlib.sha256(b'HX-EV-MEMBER-NONADMISSIBLE-PROOF-1\0\x01'
                          + B(record) + B(carrier)).digest()

def terminal_member(attempts, maximum, roster, policy, signed_proof):
    if maximum < 1 or maximum > 64 or len(attempts) > maximum: raise Hold()
    if tuple(number for number, _ in attempts) != tuple(range(1, len(attempts) + 1)): raise Hold()
    if not attempts:
        if signed_proof is None or len(signed_proof) != 5 or signed_proof[2:] != (True, True, True): raise Hold()
        proof_hash = member_proof_hash(signed_proof[0], signed_proof[1])
        if roster != (0, None, None, proof_hash) or policy != (0, None, proof_hash): raise Hold()
        return 'zero-attempt-nonadmissible'
    if signed_proof is not None or roster[0] != len(attempts) or policy[0] != len(attempts): raise Hold()
    if roster[1] is None or roster[2] != attempts[-1][1] or roster[3] is not None: raise Hold()
    if policy[1] != attempts[-1][1] or policy[2] is not None: raise Hold()
    return 'attempted'

nonadmissible = (b'exact-member-proof-record', b'exact-signature-carrier', True, True, True)
proof_hash = member_proof_hash(nonadmissible[0], nonadmissible[1])
zero_roster = (0, None, None, proof_hash)
zero_policy = (0, None, proof_hash)
assert terminal_member((), 64, zero_roster, zero_policy, nonadmissible) == 'zero-attempt-nonadmissible'
must_raise(Hold, lambda: terminal_member((), 64, (0, b'invented-attempt', None, proof_hash), zero_policy, nonadmissible))
must_raise(Hold, lambda: terminal_member((), 64, zero_roster, (0, None, b'changed'), nonadmissible))
must_raise(Hold, lambda: terminal_member((), 64, zero_roster, zero_policy, (nonadmissible[0], nonadmissible[1], False, True, True)))
must_raise(Hold, lambda: terminal_member((), 64, zero_roster, zero_policy, None))
must_raise(Hold, lambda: terminal_member((), 64, zero_roster, zero_policy,
                                        (nonadmissible[0], nonadmissible[1], True, False, True)))
must_raise(Hold, lambda: terminal_member((), 64, zero_roster, zero_policy,
                                        (nonadmissible[0], nonadmissible[1], True, True, False)))
must_raise(Hold, lambda: terminal_member((), 64, zero_roster, zero_policy,
                                        (nonadmissible[0], b'changed-carrier', True, True, True)))
receipt = b'exact-receipt-hash'
assert terminal_member(((1, receipt),), 64, (1, b'broker-op', receipt, None),
                       (1, receipt, None), None) == 'attempted'
must_raise(Hold, lambda: terminal_member(((2, receipt),), 64, (1, b'broker-op', receipt, None),
                                        (1, receipt, None), None))

def terminal_segment_bytes(kind, operation, final_set_hash, fence_hash, index, first, rows):
    domains = {'members': b'HX-EV-TERMINAL-MEMBERS-1\0',
               'attempts': b'HX-EV-TERMINAL-ATTEMPTS-1\0',
               'policy': b'HX-EV-TERMINAL-POLICY-MEMBERS-1\0'}
    if kind not in domains or not rows or any(not row or len(row) > 512 for row in rows): raise Hold()
    if not isinstance(operation, bytes) or not 1 <= len(operation) <= 1024: raise Hold()
    if len(final_set_hash) != 32 or len(fence_hash) != 32 or index < 0 or first < 1: raise Hold()
    cap = 1000 if kind == 'members' else 125
    if len(rows) > cap: raise Hold()
    u32 = lambda n: n.to_bytes(4, 'big')
    n64 = lambda n: n.to_bytes(8, 'big', signed=True)
    U = lambda b: u32(len(b)) + b
    B = lambda b: u32(len(b)) + b
    blob = u32(len(rows)) + b''.join(u32(len(row)) + row for row in rows)
    image = (domains[kind] + b'\x01\x00\x07' + b'\x01' + hashlib.sha256(U(operation)).digest()
             + b'\x02' + final_set_hash + b'\x03' + fence_hash
             + b'\x04' + n64(index) + b'\x05' + n64(first)
             + b'\x06' + n64(len(rows)) + b'\x07' + B(blob))
    if len(image) > 65536 or len(image) - len(blob) > 1024: raise Hold()
    return image

segment = terminal_segment_bytes('members', b'operation', b's' * 32, b'f' * 32, 0, 1, (b'row',))
assert segment == terminal_segment_bytes('members', b'operation', b's' * 32, b'f' * 32, 0, 1, (b'row',))
assert hashlib.sha256(segment).digest() != hashlib.sha256(terminal_segment_bytes(
    'policy', b'operation', b's' * 32, b'f' * 32, 0, 1, (b'row',))).digest()
assert hashlib.sha256(segment).digest() != hashlib.sha256(terminal_segment_bytes(
    'members', b'operation', b's' * 32, b'f' * 32, 0, 2, (b'row',))).digest()
assert terminal_segment_bytes('members', b'm' * 1024, b's' * 32, b'f' * 32, 0, 1, (b'row',))
assert terminal_segment_bytes('members', b'm' * 1024, b's' * 32, b'f' * 32, 0, 1, (b'row',)) != terminal_segment_bytes(
    'members', b'n' * 1024, b's' * 32, b'f' * 32, 0, 1, (b'row',))
must_raise(Hold, lambda: terminal_segment_bytes('members', b'', b's' * 32,
                                               b'f' * 32, 0, 1, (b'row',)))
must_raise(Hold, lambda: terminal_segment_bytes('members', b'm' * 1025, b's' * 32,
                                               b'f' * 32, 0, 1, (b'row',)))
must_raise(Hold, lambda: terminal_segment_bytes('members', b'operation', b's' * 32,
                                               b'f' * 32, 0, 1, (b'x' * 513,)))
must_raise(Hold, lambda: terminal_segment_bytes('attempts', b'operation', b's' * 32,
                                               b'f' * 32, 0, 1, (b'x',) * 126))
print('C08c presend/zero-ledger stand-ins and exact segment framing passed')

def u_hash(value):
    if not isinstance(value, bytes) or not 1 <= len(value) <= 1024: raise Hold()
    return hashlib.sha256(len(value).to_bytes(4, 'big') + value).digest()

def compact_policy_member(full_set, broker_id, reason, row, policy, receipt_class,
                          signed_policy, signed_receipt, parent_bound):
    message_id, destination_id, pin_hash, configuration_hash = full_set  # Exact A8 set and outbox.
    position, msg_hash, dest_hash, send_hash, reason_hash, row_pin = row
    policy_position, policy_msg, policy_dest, policy_config, maximum = policy
    if (not signed_policy or not signed_receipt or not parent_bound or not 1 <= maximum <= 64
        or position < 1 or policy_position < 1 or position != policy_position or msg_hash != u_hash(message_id)
        or dest_hash != u_hash(destination_id) or send_hash != u_hash(broker_id)
        or reason_hash != u_hash(reason) or row_pin != pin_hash
        or (policy_msg, policy_dest, policy_config) != (msg_hash, dest_hash, configuration_hash)):
        raise Hold()
    if receipt_class not in ('Accepted', 'TerminalRejected'): raise Hold()
    encoded = position.to_bytes(4, 'big') + msg_hash + dest_hash + send_hash + reason_hash + row_pin
    if len(encoded) > 512: raise Hold()
    return encoded

full_member = (b'm' * 1024, b'd' * 1024, b'p' * 32, b'c' * 32)
compact_row = (1, u_hash(full_member[0]), u_hash(full_member[1]), u_hash(b'send'),
               u_hash(b'closed-reason'), full_member[2])
signed_member_policy = (1, compact_row[1], compact_row[2], full_member[3], 2)
assert len(compact_policy_member(full_member, b'send', b'closed-reason', compact_row,
                                 signed_member_policy, 'TerminalRejected', True, True, True)) < 512
must_raise(Hold, lambda: compact_policy_member((b'n' * 1024, full_member[1], full_member[2], full_member[3]),
                                           b'send', b'closed-reason', compact_row,
                                           signed_member_policy, 'TerminalRejected', True, True, True))
must_raise(Hold, lambda: compact_policy_member(full_member, b'send', b'closed-reason',
                                           (0, *compact_row[1:]), (0, *signed_member_policy[1:]),
                                           'TerminalRejected', True, True, True))
must_raise(Hold, lambda: compact_policy_member(full_member, b'invented-send', b'closed-reason',
                                           compact_row, signed_member_policy,
                                           'TerminalRejected', True, True, True))
must_raise(Hold, lambda: compact_policy_member(full_member, b'send', b'closed-reason',
                                           compact_row, signed_member_policy,
                                           'RetryableRejected', True, True, True))
must_raise(Hold, lambda: compact_policy_member(full_member, b'send', b'closed-reason',
                                           compact_row, signed_member_policy,
                                           'TerminalRejected', False, True, True))
must_raise(Hold, lambda: compact_policy_member(full_member, b'send', b'closed-reason',
                                           compact_row, signed_member_policy,
                                           'TerminalRejected', True, True, False))

class RevisionClock:
    def __init__(self): self.records = {}
    def pin(self, revision, outcome_hash, head_hash, utc, signed, readback):
        if not signed or not readback or revision < 0 or not outcome_hash or not head_hash: raise Hold()
        row = (outcome_hash, head_hash, utc)
        if revision in self.records and self.records[revision] != row: raise Conflict()
        self.records.setdefault(revision, row)
        return self.records[revision][2]

clock = RevisionClock()
assert clock.pin(1, b'outcome', b'head', 123, True, True) == 123
assert clock.pin(1, b'outcome', b'head', 123, True, True) == 123
must_raise(Conflict, lambda: clock.pin(1, b'outcome', b'head', 124, True, True))
must_raise(Hold, lambda: clock.pin(2, b'outcome-2', b'head-2', 125, True, False))
print('C08d full 1,024-byte identities, signed policy and immutable revision clock passed')



def rollout(v2_history, endpoint_v2, source_ok, scratch_ok):
    if v2_history and not endpoint_v2: raise Hold()
    if not source_ok or not scratch_ok: raise Hold()
    return True

assert rollout(True, True, True, True)
must_raise(Hold, lambda: rollout(True, False, True, True))
must_raise(Hold, lambda: rollout(False, True, False, True))
must_raise(Hold, lambda: rollout(False, True, True, False))
print('C09 rollback/source/budget admission passed')

def cancellation(preparation_started, save_started, commit_proven, receipt_proven,
                 no_commit_readback=False, preparation_readback=False):
    if receipt_proven and not commit_proven: raise Hold()
    if not preparation_started and (save_started or commit_proven or receipt_proven): raise Hold()
    if not preparation_started and not save_started: return 'cancelled-no-mutation'
    if save_started and not preparation_started: raise Hold()
    if preparation_started and not preparation_readback: raise Hold()
    if not save_started:
        if commit_proven or not no_commit_readback: raise Hold()
        return 'prepared-released-after-readback'
    if not commit_proven: raise Hold()
    return 'committed-pending' if not receipt_proven else 'committed-published'

assert cancellation(False, False, False, False) == 'cancelled-no-mutation'
must_raise(Hold, lambda: cancellation(False, False, True, False))
must_raise(Hold, lambda: cancellation(False, False, False, True))
must_raise(Hold, lambda: cancellation(False, False, True, True))
must_raise(Hold, lambda: cancellation(True, False, False, False))
must_raise(Hold, lambda: cancellation(True, False, False, False, no_commit_readback=True))
must_raise(Hold, lambda: cancellation(True, False, False, False, preparation_readback=True))
assert cancellation(True, False, False, False, no_commit_readback=True,
                    preparation_readback=True) == 'prepared-released-after-readback'
must_raise(Hold, lambda: cancellation(True, True, False, False, preparation_readback=True))
assert cancellation(True, True, True, False, preparation_readback=True) == 'committed-pending'
assert cancellation(True, True, True, True, preparation_readback=True) == 'committed-published'
print('C10 durable preparation/readback and cancellation commit truth passed')
```

## Integration handoff and review disposition

Integrate C1–C6 with 6.5a A3–A8 and 6.5b B2/B6/B8 as **one** change to the AD-13 draft. Replace draft codec-01 public outcome with A8 codec-03 complete observations and immutable first POST pin; add C5 terminal decision/evidence before public `PublishFailed`. Replace V1/new V2 save assumptions with A5 codec-02 complete-set certificate/readback, and retain B2's V2 five-flag raw-proof rule. Preserve historic V17/V20/V22/V23 literals, old lease byte shape, old public constructors and §12 six-field `UNAPPROVED` receipt until separately approved integration. **BH37-9: accepted; historical Loop-7 UTC-only wording is superseded by C1's `T`/`O(T)` original-offset rule and four fixed digest vectors.** The historical triage row remains intact. No activation or Story 6.6 authorization follows from this child candidate.

Independent review should challenge: A8 no-future-acceptance proof under a real broker and partial accepted set; same-transaction effect/receipt and multi-route pointer linearizability; raw ingress/capture preallocation and retention under actual DAPR intermediaries; offset preservation in Binary and structured carriers; B6 accounting across replicas; and all ten B handoff gates. An unresolved provider fact blocks readiness and requires approved integration amendment, not local 6.6 improvisation. C01–C10 prove only local byte/state models; C7 provider/crash vectors are unexecuted.

## Verification

Run the fenced Python block with `python3` from repository root and mutation-check C01 digest and C03/C05/C08 acknowledgement guards. Run `git diff --check`, resolve relative file/section links, compare protected normative/triage/6.5a/6.5b/runtime/test hashes and changed paths, and confirm AD-13 receipt stays `UNAPPROVED`. Current runtime tests establish only the inventory's present behavior; they do not validate this future contract. Record exact local results and review findings in the separate 6.5c execution record.
