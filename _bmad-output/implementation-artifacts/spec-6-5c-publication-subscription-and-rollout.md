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

Import draft §4's checked big-endian `U`, `B`, `B32`, `N`, `I`, `T`, `Q`, `O(X)` and `M` codecs, strict UTF-8/JSON, no duplicate names in canonical maps or JSON objects, and preallocation bounds. HTTP header multiplicity follows the explicit retained-image rule below; draft map uniqueness does not discard an admitted non-routing header duplicate. `T` is signed i64 **UTC instant ticks followed by signed i16 original offset minutes** in `[-840,840]`, consistent with DateTimeOffset; `Q` is a UTC instant only. Import draft §7's exact 37-field `HX-EV-DELIVERY-BODY-1\0 || 01 || 0025 || tags 01..25`: tag `09` stored Timestamp is `T`, tag `21` optional CloudEvent time is `O(T)`. The body has no digest, key ID, signature or attestation field. `DeliveryDigest = SHA256("HX-EV-DELIVERY-1\0" || 01 || B(exact body bytes))`. Its fields are: `01` StoredDigest B32, `02` MessageId U, `03..06` tenant/domain/aggregate ID/type U, `07..08` sequence/global position N, `09` T, `0a` correlation U, `0b..0d` causation/user/DomainServiceVersion O(U), `0e` stored EventTypeName U, `0f` stored MetadataVersion I, `10` stored canonical type O(U), `11` stored payload version O(I), `12` stored format U, `13` readable format U, `14` **original readable** payload B, `15` readable extensions O(M), `16` protection state U, `17` protection metadata version I, `18..1a` scheme/key alias/content hint O(U), `1b` flags O(M), `1c..1f` CloudEvent specversion/id/type/source U, `20` subject O(U), `21` time O(T), `22` content type U, `23..24` component/topic U, `25` six routing headers M. Subscriber compares every consumed metadata field, including original offset, to authenticated source and signed claim. Publisher never upcasts; subscriber creates a current purpose-01 view only after source admission. `ReadableExtensions` and protection flags retain absent/null/empty `O(M)` distinctions; first pin copies the stored extension map and replaces only the exact `eventstore.protection` key with canonical provider-returned unprotect metadata. A case-variant-only historical protection key holds for approved mapping and is never silently normalized. Extension limits are 64 entries, 256 UTF-8 bytes/key, 4,096 bytes/value, 256 KiB canonical map and 512 KiB complete encoded event metadata. Preflight retained history; valid old data beyond a new capability cap holds readiness, while authenticated permanent schema/metadata violation is poison only after AD-31 capture.

At actor commit, 6.5a A5 **codec-02** intent/receipt and member root bind exact immutable event, encoding/digest sidecars, outbox and **domain/append result payload hash**, including rejection events. The draft §7 outbox intent `HX-EV-OUTBOX-INTENT-1\0 || 01 || 0012` stages exact source URI, component/topic, routing map/policy, destination configuration digest/bytes or immutable reference, optional subject/time `O(T)` and stored-form core derivation in the same save. Its tag `12` is one `B` containing `00 || inline configuration` or `01 || complete immutable configuration-reference record`, with no nested second `B`; readback resolves and hashes the exact configuration. Mutable publisher options never fill absent intent. An old outbox without authenticated intended-route evidence holds for separately approved migration. Derive the first pin only from authenticated original source/readability and that read-back intent, after A5 complete generation readback and globally Committed batch root. Same-ID append retry compares the **entire** original batch/result/fingerprint; a member proof grants only one already committed member's publication/effect check.

The exact global MessageId CAS pin contains decoded body, purpose-02 `HX-EV-ATTEST-1\0 || 01 || 0003` claim/key ID/64-byte P1363 signature, DeliveryDigest, StoredDigest, scope, old approved RegistryFingerprint, signed claim tag-`0a` issuance UTC, routing intent hash, renderer version, precomputed exact request and predicted accepted outbound bytes, C2 complete ordered request and predicted accepted transport-header images, and their equal six-header signed projection for each authorized mode pair, and exact hashes of the retained body, attestation, mode renderings and complete header images. Key is the **exact MessageId**, never `(MessageId,digest)`; changed body, header, destination, scope or same-mode rendering conflicts. Before any send, the publisher deterministically renders and CAS-pins **both** exact request and predicted accepted body/header images from the immutable destination configuration, accepted membership revision, proposed accepted mode, renderer version and signed routing intent. The broker must attest that its complete accepted image will equal this prediction; no post-acceptance result may choose new bytes for the global pin. An unpredicted broker-added, reordered or normalized header holds before acceptance, while an authorized Binary-to-Structured conversion may change only its predicted mode rendering and mode-specific Content-Type. The two complete images are independently pinned and compared; decoded body, attestation and all six signed routing headers remain equal. A provider unable to inspect and compare the complete predicted image in its atomic accept path cannot advertise publication readiness. First-pin recovery requires durable no-prior-send proof; ambiguous prior send holds. Pin capacity is reserved before send (`PublicationPinCapacityHold`). Its signed historic obligation remains through queue, broker retry, quarantine, rollback and late-delivery obligations. A hash-only tombstone cannot perform C2's required full-byte duplicate comparison. Full pin body, attestation, original/accepted renderings, complete ordered header images, signing evidence and source remain available byte-for-byte throughout every possible late duplicate, retry, route, rollback and incident obligation. Only after authenticated closure proves no future same-MessageId comparison can arise may a CAS tombstone replace these bytes; alternatively a tombstone may name one independently authenticated immutable full-byte comparison source, with exact authority/key/generation/ETag, source bytes and no-expiry obligation read back for the whole enforceable redelivery horizon. On every duplicate, the reader resolves that source and compares all bytes before returning Accepted; missing or expired source holds. No finite broker horizon by itself makes hashes sufficient. Missing historic key/pin/source holds, never permits a new send or false duplicate.

Transport is exact binary `application/vnd.hexalith.eventstore.v2+octet-stream` body with canonical padded Base64 `ce-hyevattestation`, or sorted structured CloudEvents JSON with canonical padded `data_base64` and `hyevattestation`. Both modes strictly decode to the same body/attestation and signed `specversion=1.0`, id/type/source, optional subject/time with original offset, content type, component/topic and six `hx-*` routing headers. The six are `hx-tenant-id`, `hx-domain`, `hx-aggregate-id`, `hx-aggregate-type`, `hx-event-contract-type`, `hx-payload-version`; structured mode keeps them outside the CloudEvent object. V1 header type/version comes from exact registered domain alias descriptor while stored body pair remains absent/null. Case-fold only legal HTTP header-name case to identify **decision fields**: the six routing names, every `hx-*`, CloudEvent `ce-*` field, `ce-hyevattestation`, `Content-Type`, and any configured mode or attestation selector. A duplicate of any such name, including a differently cased spelling, is `DeliveryPinConflict`; unknown `hx-*`/CloudEvent attributes, changed values, alternate Base64 and noncanonical mode rendering also conflict. Exact ordered duplicates of other HTTP names are admitted only when the signed destination configuration names them as non-decision inputs and the C2 full-byte broker/sidecar/ingress probe proves they cannot affect routing, trust, filtering, effects, quarantine identity or mode selection. Otherwise hold admission; the generic duplicate rule never rejects an otherwise proved non-routing image. JSON object member names remain unique under strict JSON parsing. Non-`hx-*` broker headers cannot route. All six signed headers are required on new deliveries and sorted by unsigned UTF-8 name; `hx-payload-version` is canonical unsigned ASCII decimal 1..1024 without sign, space or leading zero. Generic object publication/DTO binding does not satisfy this contract. Same-mode retry uses retained exact outbound bytes; a broker mode conversion is accepted only after strict decode and canonical re-render to the same pin.

Before parsing, Base64 decoding, JSON DOM creation, signature verification, or buffer allocation, admission obtains a streaming length bound on **each** byte domain: decoded delivery body ≤128 MiB, decoded attestation ≤8 KiB, encoded attestation header/JSON value ≤16 KiB, complete Binary transport body ≤128 MiB, and complete structured JSON transport body ≤192 MiB. Canonical padded Base64 of a 128 MiB body is exactly 178,956,972 bytes; a structured carrier is admitted only when its exact canonical JSON length equals `4 * ceil(actual decoded body length / 3)` canonical padded-Base64 bytes plus the actual bounded JSON attribute, attestation and framing bytes and remains ≤192 MiB. The 178,956,972-byte figure is only the 128 MiB decoded-body boundary vector; it is not a lower bound for smaller bodies. Binary body length equals decoded body length. All broker/application headers together are ≤64 KiB of exact name/value bytes including separators, ≤128 entries and ≤8 KiB per name/value pair; the complete body-plus-header/framing carrier is ≤193 MiB. Only `Binary` and `Structured` are legal modes. All domains and the mode/length relationship are independently checked before growth; a tuple of individually small but physically impossible lengths is invalid. The carrier is parsed/decoded incrementally in bounded chunks; a 192 MiB JSON DOM or full decoded-plus-encoded copy is never allocated. Length-prefixed fields, JSON strings, Base64 output, sorting scratch and signatures are charged before growth; overflow, unknown length, truncated stream or unsupported provider streaming limit yields `DeliveryCarrierLimitHold` and non-2xx, with no parsed identity or effect. A production probe must prove these bounds through broker, sidecar, ingress and AD-31 capture without a larger intermediate allocation. An over-limit carrier can be acknowledged only through C4's separately proved exact-full-byte physical quarantine; a truncated hash or captured prefix grants no authority.

### BH37-9 disposition and byte vectors

**Accepted historical wording conflict; superseded here.** [Loop-7](story-6-5-design-notes.md) says CloudEvent time uses UTC ticks only. Draft §4 `T`, draft §7 tag `21` `O(T)` and [Loop-9](story-6-5-design-notes.md) require the original offset. Equal instant with offsets 0 and +60 minutes has distinct `T` suffix `0000` and `003c`; canonical CloudEvent text has seven fractional digits with `Z` or original `+HH:MM`/`-HH:MM`. No UTC-offset normalization occurs in body, claim, digest or pin. Starting with **unchanged** 842-byte draft `V17DeliveryBody`, changing **only** tag `09` offset `0000→003c` changes digest `54a83b017e8357120374f60a175c438f48b0cd80013e065aa93360670a3f22ad` to `fa9dcf5c6ce3b8fe79a1b4bb6fee665f65431c8be5e10ac4af34ced59e6fb02e`. Making tag `21` present with the same UTC ticks yields two 852-byte bodies with digest `36cbbdd4d189651f90a150e0cccdd50a78495809e6decd9adc0296195c2444a4` at offset zero and `6a5b849b4f8399eb61a32becfaa496d970ff4e55a626737895c0bbdd4d70fcc1` at +60. The executable checks below derive all four without altering the historical literal.

## C2. Broker membership, acceptance and recovery

Import draft §7's exact signed `MembershipClaim` (`HX-EV-MEMBERSHIP-1\0 || 01 || 0008`), historical `ConsumerLeaseClaim` (`HX-EV-CONSUMER-LEASE-1\0 || 01 || 000f`), `PublicationFenceClaim` (`HX-EV-PUBLICATION-FENCE-1\0 || 01 || 0009`) and production `ConsumerConfiguration` **codec 02/000f**. Do not alter V23's historical configuration/lease literal bytes. Membership tags `01..08` are U domain/component/topic, N positive revision, B32 current domain RegistryFingerprint, B sorted member set, Q issued/expiry UTC. Each member is `U logical consumerIdentity || B32 exact configurationHash`, sorted unique by unsigned UTF-8; one logical identity is derived from `(physical subscription ID, HandlerRouteId)`. Consumer configuration codec-02 tags `01..0f` are U consumer/domain/component/topic/physical subscription/route/type selector/tenant rule, B32 binary capability hash, U duplicate mode, U effect mode (`AtomicStore` or `ProviderIdempotency`), U provider ID, B32 V3 probe receipt digest, U backend identity, N active revision. Historical lease tags `01..0f` retain U consumer/domain/component/topic, N revision, B32 membership hash/configuration hash/RegistryFingerprint/capability hash, U duplicate mode/provider ID, B32 probe hash, U backend, Q issued/expiry. Resolve lease tag `07` through exact authenticated codec-02 configuration and verify effect mode at its tag `0b`; changed configuration with unchanged lease bytes fails. Fence tags `01..09` retain domain/component/topic, revision, membership hash/fingerprint, operation ID, expiry, 16-byte broker nonce. Every claim is signed with its purpose-specific broker authority under the draft §6 carrier, distinct from event trust; signatures, key purpose, intervals/revocation and exact member order must validate.

Production tag-`09` capability is `SHA256("HX-EV-BINARY-CAP-2\0" || 01 || B32 base carrier capability hash || B32 current HandlerCompatibilityHash || B32 EffectProviderConfigurationHash || B(exact route capability evidence))`, where `EffectProviderConfigurationHash = SHA256("HX-EV-EFFECT-CONFIG-1\0" || 01 || U effect mode || U provider ID || U backend ID || B32 canonical provider-options hash)`. Route evidence contains the exact `52` row, optional `43` row and sorted `58` extras, or the exact signed external manifest; an unchanged lease cannot conceal a changed effect mode, backend or handler. One physical subscription with two routes has two independent logical configurations/leases **and one** pinned send. Internal route capability binds exact primary `52`, optional verified `43`, all sorted extra `58` event-type/format rows, `HandlerCompatibilityHash`, filter and effect provider configuration. External routes require the purpose-`0b` signed external manifest and independently broker-attested transitive loader graph, signed closure and current V3 provider probe. A `*` selector means only the exact attested nonempty multi-type set, never arbitrary event types. Membership carrier is at most 1 MiB/4,096 members/4 KiB each; external route set at most 256 KiB/1,024 rows, full external carrier at most 2 MiB and closure at most 1 MiB. Check lengths/counts before nested allocation.

Production duplicate-safety readiness requires draft `HX-EV-DEDUP-PROBE-3\0 || 03 || 0015`, with **two different** route IDs, one injected crash between A's effect and receipt commit, A retry and A/B duplicates, exactly one final effect and complete `EventEffectReceipt` per route, plus the six-row purpose-`0c` provider-signed transcript. Require one V3 probe for every mode/backend/options/revision in use; V23's one-route probe is historical fixture evidence only. Broker independently queries full provider receipts and validates transcript/key/mode/backend/version/ETag before signing a lease. An effect-capable DI dependency outside the selected atomic write session or exact-key provider blocks readiness; a configured provider name cannot stand in for a probe.

At acceptance, `TryPublishAtRevision(tenant, domain, component, topic, expectedRevision, fenceNonce, operationId, ExactCanonicalPinDigest, requestMode, exactRequestModeTransportBytes, exactCompleteRequestHeaderImage, proposedAcceptedMode, exactPredictedAcceptedTransportBytes, exactPredictedAcceptedHeaderImage)` atomically checks authoritative broker UTC, active member set, every lease/configuration/probe/fingerprint, revision and fence expiry/nonce **with the complete accepting body and header bytes**. `ExactCanonicalPinDigest = SHA256("HX-EV-BROKER-PIN-1\0" || 01 || B(exact decoded body) || B(exact decoded attestation))`. The proposed accepted mode is either the request mode or a specifically authorized canonical conversion; the broker atomically compares the actual accepted mode, exact canonical body rendering and complete accepted header image with the already pinned prediction and returns all three exact actual images in its signed Accepted result; any difference holds **before** acceptance with no new pin. Both modes are exactly `Binary` or `Structured`. Publisher precheck or a later header hash is insufficient. Join, leave, filter/handler/provider/registry change advances revision and invalidates old fence/leases. Missing compare-and-accept is `ConsumerMembershipFenceUnavailable`. The broker enforces **one unique key** `(U tenant, U domain, U component, U topic, U memberSendOperationId)` before digest comparison; its immutable value binds the canonical pin digest, request mode and exact request body, accepted mode and exact accepted rendering, the complete request and accepted `HX-EV-BROKER-HEADERS-2` header images and authenticated parent binding. Both request and accepted body/header images are retained and read back byte-for-byte; conversion may change only the **precommitted** canonical mode rendering and its mode-specific complete header image, including Content-Type; decoded body, attestation and signed six-header projection remain equal. A different digest, either mode, bytes, either complete header image or parent under that same key conflicts; the digest is never part of key selection. Every member send OperationId, including a future retry-nonce ID, requires `HX-EV-SEND-PARENT-1\0 || 01 || 000c`: `01` U authenticated tenant, `02..04` U domain/component/topic, `05` U original parent command OperationId, `06` B32 A5 tenant-scoped ScopeOpHash, `07` N A8 member position, `08` U event MessageId, `09` U member send OperationId, `0a` B32 exact outbox-intent/pin binding hash (`SHA256(B(exact outbox intent) || B(exact global pin))`), `0b` U configured broker send-parent issuer ID and `0c` Q signed issuance UTC. Its complete record/signature carrier is ≤16 KiB, reserved before A4 Prepared; its key is the unique broker operation key above. Authenticate complete same-save outbox/preparation evidence, signed send intent and broker parent-index CAS/readback before acceptance. The broker parent index is append-only under the authenticated tenant and ScopeOpHash parent namespace; an unbound, mismatched or late-invented send ID holds before acceptance. A terminal parent reject fence keyed by the authenticated tenant and original command ScopeOpHash is checked atomically across **every** A8 destination in that tenant **before** the unique-key duplicate path on every accept path, including delayed old and newly presented member IDs; a broker without that cross-destination atomic check cannot certify C5 terminality; a matching historical Accepted receipt remains queryable but cannot create new acceptance. Accepted is idempotent only for the matching unique key and immutable value. After uncertain send, `GetPublicationReceipt` uses the exact attempt key and complete-chain rules below; an operation-level latest result cannot authorize retry or erase a prior Unknown. Matching Accepted records without resend; a proved class-01 Rejected permits renewal of fence/nonce for the same member send OperationId/pin; Unknown or unavailable evidence holds without new nonce/send. Receipt retention covers pin, parent binding, retry, handoff and rollback obligations. Cancellation after send starts follows the same lookup under a bounded recovery token; caller cancellation cannot infer nonacceptance.

`HX-EV-SEND-PARENT-1` is signed under **distinct purpose `1c`**, by the configured broker send-intent authority named in the pre-Prepared destination configuration and accepted revision's broker trust map. Purpose-`1c` keys are bound to that issuer, tenant, destination and A5 parent ScopeOpHash namespace; event, membership, historical-grant, policy and generic broker signatures cannot substitute. Before broker parent-index CAS or any send, verify the exact twelve-tag claim, fixed P-256/P1363 draft §6 carrier, issuer/SPKI/purpose, signed tag-`0c` UTC within the issuer's `[notBefore,notAfter)` interval, current revocation state, complete same-save A5 outbox/preparation root, original command and member-plan position, MessageId, pin/outbox bytes and destination configuration. For a **new acceptance**, broker UTC must also be within the pinned purpose-`1c` authority interval and at or after signed issuance; reverify purpose, issuer, tenant/parent index and current revocation against the accepted revision's independently configured trust map. An old Accepted receipt remains queryable after key expiry only under its retained historic trust obligation; expiry cannot authorize another acceptance, and a fresh send ID after a proved rejection requires a freshly signed parent claim under an active key. A changed key or issuer without authenticated retained-history mapping holds. Retain the exact claim/carrier and original trust interval through all send, receipt, terminal, rollback and delayed-delivery obligations. A claim with a valid generic carrier but wrong purpose, unknown issuer, expired-at-issuance time or revoked key grants no send authority.

The complete retained outbound header image is `HX-EV-BROKER-HEADERS-2\0 || 01 || 0003`: tag `01` B is `u16 headerCount ||` each application or broker header at the accepted outbound publication boundary in **original byte order**, `B(exact name bytes) || B(exact value bytes)`, including duplicates and non-routing names; tag `02` B is the six received routing name/value pairs in their original relative order with the same framing; tag `03` B is the six canonical lowercase-name/exact signed-value pairs sorted by unsigned UTF-8 name. Tags `02` and `03` reconstruct the retained `HX-EV-BROKER-HEADERS-1` projection by prepending its exact domain/version/two-tag framing and enclosing these two payloads as its ordered `B` fields; its earlier hash remains a derived view and cannot replace tag `01`. Header count is 1..128, exact tag-`01` name/value/separator bytes obey C1's 64 KiB total and 8 KiB per pair, and the complete image is ≤192 KiB after the two derived six-header projections. The six routing entries must occur exactly once in tag `01`, with their original order/casing copied byte-for-byte into tag `02`; Structured keeps them separately transported outside its JSON CloudEvent body. Verify legal header names, case-folded uniqueness of every C1 decision field (routing, CloudEvent, attestation and mode-defining), no unknown `hx-*`/CloudEvent field, and exact signed canonical values from the pinned body before constructing tag `03`. Preserve **every** other tag-`01` entry, its casing, order, duplicate position and value only when the signed destination configuration and full-byte probe prove it is a non-decision input; otherwise hold before acceptance. A permitted duplicate non-routing entry is part of exact same-key equality and cannot be rejected merely because its name repeats. The image hash is `SHA256("HX-EV-BROKER-HEADERS-HASH-2\0" || 01 || B(exact complete image))` and is compared with complete image bytes at duplicate readback. The global MessageId pin, broker immutable send value and exact original send attempt retain the complete request image and, on authorized conversion, the separately produced complete accepted image. Each image has its own full-byte readback and hash; the signed six-header projection must agree across both. A changed non-routing header in either image is a same-key conflict even when the six-header projection and body match.

Only fields outside the accepted publication's retained body/header image are transport-managed: protocol connection framing (`Content-Length`, `Transfer-Encoding`, hop-by-hop `Connection` and the headers it explicitly names), sidecar connection IDs and broker delivery-attempt metadata generated **after** immutable acceptance. A versioned, signed destination configuration enumerates their exact names, source boundary and parser behavior; **name alone never excludes a header present at the accepted boundary**. An excluded field cannot be used by any admission, routing, trust, filter, effect, quarantine-identity or status decision. If a sidecar or broker adds, removes, reorders or normalizes a field **before** immutable acceptance, that field belongs in tag `01`; an unknown or decision-affecting excluded field holds activation. The producer signs/retains the original image; broker acceptance and consumer ingress read back that same ordered byte sequence, including duplicate non-routing fields and casing, and prove the excluded fields have no decision input. A provider that exposes only normalized header maps cannot claim exact retry equality.

The broker immutable send hash is exactly `SHA256("HX-EV-BROKER-SEND-VALUE-3\0" || 01 || U tenant || U domain || U component || U topic || U memberSendOperationId || B32 ExactCanonicalPinDigest || U requestMode || B32 SHA256(exact request-mode body bytes) || U acceptedMode || B32 SHA256(exact accepted-mode rendered body bytes) || B32 SHA256("HX-EV-BROKER-HEADERS-HASH-2\0" || 01 || B(exact complete request header image)) || B32 SHA256("HX-EV-BROKER-HEADERS-HASH-2\0" || 01 || B(exact complete accepted header image)) || B32 ParentBindingHash)`, where `ParentBindingHash = SHA256("HX-EV-SEND-PARENT-HASH-1\0" || 01 || B(exact twelve-tag parent claim) || B(exact purpose-1c signature carrier))`. If no conversion occurs, request and accepted modes and their complete renderings/images are byte-identical. An authorized Binary-to-Structured conversion requires both canonical renderings and both complete images precomputed in the global pin **before send** and atomically compared inside `TryPublishAtRevision`, with equal decoded body/attestation, equal signed six-header projection and a pinned destination conversion capability at that revision; otherwise hold before acceptance. `Content-Type` and any other mode-defining field may differ only as the exact configured canonical Binary versus Structured rendering, retained separately in the request and accepted complete images. The verifier compares each image to its own pinned mode and rendering, rejects duplicate or case-fold-colliding decision-field names in either image, and never requires the two complete images to be identical across an authorized conversion. A changed non-routing duplicate or an unconfigured mode-header change remains a same-key conflict. The broker retains and reads back both full bodies, both complete header images, claim, carrier and conversion authority under the tenant-scoped unique operation key. Same-key duplicate equality compares **every** retained request and accepted byte sequence and both modes before returning the original Accepted result; a hash match alone is insufficient. The original Accepted result retains the actual accepted mode/body/header image and the equality proof against the precommitted prediction; duplicate and lost-acknowledgement lookup repeat full-byte comparison with both pinned images and reject any changed broker normalization. Changed body, request mode, accepted mode, conversion rule, parent claim, routing or non-routing header conflicts. The broker, sidecar and raw ingress must pass a production probe sending reordered, recased, duplicate non-routing and changed-value headers through both modes, comparing producer, broker readback and ingress **full bytes** and proving excluded transport fields cannot affect a decision. Normalization, lost ordering/casing, unavailable full-byte readback or an unproven exclusion holds activation; this probe is unexecuted in this candidate.

Every broker submission is an immutable nonce attempt. Before crossing the send boundary, CAS-register `HX-EV-PUBLICATION-ATTEMPT-1\0 || 01 || 000b` (≤16 KiB): tags `01..05` U tenant/domain/component/topic/member send OperationId, `06` N positive contiguous attempt ordinal ≤the signed member maximum, `07` B 16-byte fence nonce, `08` B32 exact broker immutable send hash above, `09` B32 predecessor attempt-link hash (zero for first), `0a` Q broker UTC registration instant and `0b` U broker attempt issuer ID. The attempt key is the unique operation key plus ordinal and exact nonce; the broker signs the registration under purpose 20 and its pinned attempt-registration issuer and rejects nonce reuse, ordinal gap or altered registration. Each immutable result observation is a purpose-21 signed `HX-EV-PUBLICATION-ATTEMPT-RESULT-1\0 || 01 || 0008` (≤16 KiB): `01` B32 exact attempt-record hash, `02` U literal `Accepted`, `Rejected` or `Unknown`, `03` O(U) closed reason code (present only for Rejected), `04` B32 exact immutable send-value hash, `05` Q broker result UTC, `06` U result authority ID, `07` B32 broker acceptance/rejection/fence evidence hash or zero for Unknown and `08` B32 predecessor result-chain hash. `AttemptRecordHash = SHA256("HX-EV-PUBLICATION-ATTEMPT-HASH-1\0" || 01 || B(exact signed attempt claim) || B(exact attempt signature carrier))` and `ResultObservationHash = SHA256("HX-EV-PUBLICATION-RESULT-HASH-1\0" || 01 || B(exact signed result claim) || B(exact result signature carrier))`. For attempt ordinal above one, attempt tag `09` is `SHA256("HX-EV-PUBLICATION-ATTEMPT-LINK-1\0" || 01 || B32 prior AttemptRecordHash || B32 prior **definitive** ResultObservationHash)`; the first has 32 zero bytes. Result tag `01` is the exact current AttemptRecordHash and result tag `08` is the prior ResultObservationHash for this attempt, or 32 zero bytes for its first observation. A result/carrier hash is valid only after its exact signed bytes and referenced broker evidence are authenticated.

A signed Unknown preserves uncertainty; it never proves nonacceptance. Result observations are CAS-created under the exact attempt key plus positive contiguous observation ordinal, with tag `08` binding the predecessor observation hash (zero for first); the signed chain head fixes its count and final hash. A later broker reconciliation may append one definitive Accepted or Rejected observation after Unknown without altering that Unknown; at most two observations exist per attempt, so repeated unresolved queries reuse the same signed Unknown. At most one definitive result is allowed per attempt; conflicting definitive results, a second result after a definitive result, a gap or missing broker proof are incidents. An Accepted result commits the immutable operation acceptance pin. Every exact-attempt lookup reads back the complete observation chain and broker evidence, never only the latest operation status. `GetPublicationReceipt(tenant,domain,component,topic,sendOperationId,attemptOrdinal,fenceNonce,expectedSendValueHash)` returns that attempt's complete authenticated observation history, its definitive result if any, and the complete ordered cross-attempt chain through the broker's signed append-only chain head; absence or ambiguous lookup holds. To register a subsequent attempt or new parent-bound send ID, reconcile **every** earlier ordinal and nonce against its exact signed result and broker acceptance index: all must end in class-01 retryable Rejected, with no **unresolved** Unknown, Accepted, missing observation, conflicting value or in-flight registration anywhere in the member chain. The predecessor hash and signed head must match exact count/order; operation-level Accepted is exposed separately and cannot be substituted for an old Rejected result. Terminal C5 proof verifies the same complete chain and accepts class-02 final Rejected only as that member's terminal outcome. Retain all attempts, results, chain heads and source proofs for the pin and terminal horizon. The broker's signed `HX-EV-PUBLICATION-ATTEMPT-CHAIN-1\0 || 01 || 000c` head has ordered `01..05` U tenant/domain/component/topic/send OperationId, `06` N checked registered attempt count, `07` B32 latest exact attempt-record hash (zero when none), `08` B32 latest result-observation hash (zero before a result), `09` B32 exact operation acceptance-index hash (zero absent), `0a` B32 exact purpose-`1c` parent-claim/carrier hash, `0b` U purpose-`22` issuer ID and `0c` Q signed broker head UTC. The pinned purpose-22 broker attempt-head issuer signs the complete ≤16 KiB head; its CAS generation and receipt bind the exact ordered attempt and observation keys, result count 0..2 per attempt and full byte hashes. Each registration or result observation advances that head atomically with the corresponding record; a torn record/head readback holds. Lookup verifies all ordinals 1..tag-`06`, nonces, predecessor hashes, every observation and the acceptance index under one broker generation. Pre-Prepared reservation charges the signed maximum attempts, up to two 16 KiB observations per attempt, chain heads and source proofs against A8's 64 MiB referenced-evidence limit; if the worst case cannot fit, lower the signed maximum before Prepared or hold. A valid committed member cannot be stranded by an unreserved attempt record.

The tenant is taken from authenticated A5 admission and the signed send-parent/outbox image, never from an untrusted request header. Broker uniqueness, receipt lookup, parent-index CAS and terminal reject-fence checks use the same ordered tenant/domain/component/topic/send key and tenant/ScopeOpHash namespace. A duplicate send ID in another tenant is independent even when all other ID bytes match; a fence in one tenant cannot suppress another tenant's send. A signed parent record with a different tenant or a broker incapable of atomically fencing every destination in that tenant holds before acceptance. The accepted receipt and its immutable value carry the exact tenant and parent binding for readback. A new retry-nonce send OperationId under that parent is permitted only after authenticated readback of the prior member send ID and a class-01 retryable Rejected receipt, with no Accepted or unresolved Unknown attempt anywhere in that member chain; class-02 terminal rejection cannot mint a new ID. Each new ID is signed and indexed under the same tenant/ScopeOpHash, original MessageId and immutable pin before acceptance.

Four **distinct hexadecimal signing-purpose IDs** govern the broker send evidence: `20` for `HX-EV-PUBLICATION-ATTEMPT-1` registration, `21` for `HX-EV-PUBLICATION-ATTEMPT-RESULT-1` observations, `22` for `HX-EV-PUBLICATION-ATTEMPT-CHAIN-1` heads, and `23` for the parent-member send-chain head below. Each claim's existing issuer tag, or the head's issuer tag added below, names the exact configured broker publication issuer in the signed pre-Prepared destination configuration and accepted-revision trust map; a generic broker signature or another purpose grants no authority. Use draft §6's fixed P-256/P1363 carrier with exact claim bytes, purpose, issuer/SPKI and signed broker UTC in the issuer's `[notBefore,notAfter)` interval. Before registration, result append or head CAS, check broker UTC against the active purpose-specific interval, independently configured issuer/tenant/destination/parent namespace and current revocation state; verify the same at signed issuance on historical readback and also reject a later explicit revocation unless an authenticated retained-history exception is approved. Ordinary key expiry does not erase a historical definitive receipt, but it never authorizes a new attempt or successor. Retain all four exact claims, carriers, trust maps and revocation/interval evidence until every publication, route, terminal, retry, rollback and incident obligation closes. A valid purpose-`1c` parent claim is necessary but cannot replace any of these four signatures.

The **parent-member send-ID chain** has one namespace key `parent-member-sends:` plus SHA-256 of `U authenticated tenant || B32 ScopeOpHash || N one-based A8 member position`. Its immutable `HX-EV-PARENT-MEMBER-SEND-1\0 || 01 || 000d` row (≤16 KiB) contains ordered `01` U tenant, `02` B32 ScopeOpHash, `03` U original command OperationId, `04` N member position, `05` U MessageId, `06` U destinationId, `07` U member send OperationId, `08` N positive contiguous send ordinal, `09` B32 predecessor row hash (zero only at first), `0a` O(B32) predecessor definitive class-01 Rejected result hash, `0b` O(B32) predecessor signed attempt-head hash, `0c` B32 exact purpose-`1c` parent-binding hash and `0d` N count of **all** earlier member nonce attempts. The two optional fields are absent at ordinal one and present together thereafter; null is invalid. `MemberSendRowHash = SHA256("HX-EV-PARENT-MEMBER-ROW-1\0" || 01 || B(exact row))`. Every row is CAS-created once under the exact namespace/ordinal and read back in full. The initial row is installed before the first possible send from the signed A8 plan; no row exists for C5's separately proved zero-attempt class-03 member.

`HX-EV-PARENT-MEMBER-HEAD-1\0 || 01 || 000d` (≤16 KiB) has ordered `01` U tenant, `02` B32 ScopeOpHash, `03` U original OperationId, `04` N member position, `05` U MessageId, `06` U destinationId, `07` B32 immutable global pin hash, `08` N send-row count, `09` B32 latest exact row hash, `0a` B32 previous signed head hash (zero at first), `0b` N broker CAS generation and `0c` U purpose-`23` issuer ID and `0d` Q signed broker head UTC. The broker signs each append-only head generation with purpose `23`; `MemberSendHeadHash = SHA256("HX-EV-PARENT-MEMBER-HEAD-HASH-1\0" || 01 || B(exact head) || B(exact purpose-23 carrier))`; its CAS receipt identifies the exact prior and new head ETags and the created row key/hash. A member-level linearizable CAS appends **row plus signed head together**, checks the tenant/ScopeOpHash parent terminal fence in the same transaction across every destination and rejects a second successor to one prior row. The first head has count one; each successor head increments count by one, names the immediately preceding signed head and row, and preserves the same member, destination, MessageId and pin. Before successor CAS, read back the complete prior send ID's purpose-`22` attempt head, every purpose-`20` registration and purpose-`21` result observation, broker acceptance index and exact parent claim; its final result must be definitive **class-01 retryable Rejected**, with no Accepted, unresolved Unknown, registered/in-flight attempt or class-02 result anywhere in this member's prior chains. The successor row binds that final result and head, the prior cumulative nonce count, and a freshly valid purpose-`1c` claim for the new send ID. Two coordinators proposing different successors from one Rejected ID race on the same head ETag; exactly one wins and the other must read and follow it or hold on different bytes. Neither may create an alternate branch or accept under an unindexed ID.

At every renewal, new ID, terminal decision or public status read, enumerate **all** send ordinals `1..head.count` from the broker's authenticated member namespace, read each full row/CAS receipt and each historical signed head, recompute row/head hashes and predecessor links, compare count, unique send IDs, cumulative prior nonce counts and the complete purpose-`20/21/22` per-ID attempt/observation chains. The per-ID attempt ordinal is local to its send ID; C5's flat terminal attempt ordinal is the contiguous member-wide order obtained by adding row tag-`0d` to that local ordinal. Sum of all IDs' nonce attempts stays ≤the one frozen 1..64 member maximum; send-row count cannot exceed that maximum. Any omitted ID, gap, fork, unindexed accepted operation, wrong issuer/purpose, changed pin or count mismatch holds every status projection; an unresolved signed Unknown holds renewal and terminality and may project only the authenticated A8 nonterminal `unknown` shape after complete chain verification. A latest operation receipt or parent-index hash alone cannot prove completeness. The head and all predecessor generations/rows remain retained through the maximum route, terminal, key-rotation, rollback and incident horizon. Before A4 Prepared, reserve worst-case 64 complete rows/heads plus every permitted attempt/result record under A8's 64 MiB referenced-evidence limit; a provider unable to atomically fence row/head/parent acceptance or retain their full bytes is not V3 ready.

An Accepted broker receipt freezes each addressed `(physical subscription ID, HandlerRouteId, configuration hash, membership revision)` as a historical execution obligation. The broker and coordinator refuse a join, leave, rename, filter, provider/backend change or lease replacement that would remove an owed route until its terminal result is authenticated, unless they first CAS-install an authenticated old-to-new execution mapping. `HX-EV-HISTORICAL-EXECUTION-GRANT-1\0 || 01 || 000e` is a ≤16 KiB ordered claim: `01..04` U domain/component/topic/physical subscription, `05` U old HandlerRouteId, `06` B32 Accepted-receipt hash, `07` B32 global pin hash, `08` B32 old EventEffectKey hash, `09` B32 old configuration hash, `0a` B32 old handler/effect/backend capability hash, `0b` B32 old membership/lease hash, `0c` U replacement endpoint or old endpoint identity, `0d..0e` Q issued/expiry UTC. It is signed with distinct purpose `13` by the configured broker membership authority, whose stable issuer identity, SPKI, interval and revocation state are pinned in the accepted revision's broker trust map. Verify exact bytes, purpose, issuer, signature, Accepted receipt, old key/configuration and broker UTC within `[issued,expiry)` on every invocation; a later revocation blocks the grant unless an authenticated safe replacement already owns the obligation. The grant authorizes **only** that old route/pin/effect key, no new publication or changed filter decision. The mapping binds this grant and Accepted receipt to the replacement endpoint/handler graph and provider mode/backend V3 probe. A replacement must query the old provider's exact-key receipt and every uncertain effect operation, then CAS-migrate authenticated completed receipts or install a fenced shared exact-key provider before any handler rerun. If the old provider is unreachable, an outcome is uncertain or a cross-provider transaction cannot prove one effect, hold mapping, membership transition and route; never rerun under a new backend. A mapping preserves the old HandlerRouteId and EventEffectKey; it cannot create a second effect or turn an incomplete route into `Filtered`. Grant validity or an authenticated exact-obligation renewal path must cover the full outstanding route horizon before a replacement revision activates; a grant expiry without renewal leaves the old route held under its original obligation and blocks that transition. Broker/coordinator readback proves drain or the exact mapping/grant and reconciliation before activating a revision.

The replacement mapping is `HX-EV-HISTORICAL-EXECUTION-MAPPING-1\0 || 01 || 0010` (≤16 KiB), with ordered `01` U authenticated tenant, `02..04` U domain/component/topic, `05` U physical subscription, `06` U **old** HandlerRouteId, `07` N **old** accepted membership revision, `08` B32 exact old Accepted-receipt hash, `09` B32 old EventEffectKey hash, `0a` B32 old global-pin hash, `0b` B32 purpose-13 grant claim-and-carrier hash, `0c` U replacement endpoint identity, `0d` U replacement provider/backend identity, `0e` B32 replacement V3 probe/configuration/**handler-graph** hash, `0f` B32 exact old-provider reconciliation and exclusion-source hash and `10` N exclusive mapping generation. Its key is `historical-execution-mapping:` plus `SHA256("HX-EV-HISTORICAL-MAPPING-KEY-1\0" || 01 || U tenant || U domain || U component || U topic || U old physical subscription || U old HandlerRouteId || N old revision || B32 old Accepted-receipt hash)`; replacement bytes are values, never key material. The broker membership authority owns one linearizable create-once CAS at this key. Its authenticated CAS receipt includes the exact pre-CAS mapping-record hash, absent prior value, resulting generation/ETag, issuer and commit UTC, and is retained/read back with full mapping bytes. A lost acknowledgement queries this same key and receipt; an identical mapping follows it, while a competing endpoint/provider/backend/probe/grant conflicts and may not create another mapping. Before the owner can invoke, it reads every exact-key old provider receipt, in-flight operation and uncertain-commit ledger under the old `EventEffectKey`, atomically installs a cross-provider exclusion fence against any old owner/handler commit, then CAS-owns the mapping generation with the exact signed reconciliation result. The old and replacement providers must jointly enforce that fence on every effect path and verify one shared key/receipt; missing old readback, uncertain effect, unsupported atomic fence, changed owner generation or competing mapping holds without invoking. Completion is CAS-recorded under the same mapping owner and old route-decision key; the replacement cannot reset that decision, issue a new effect key or bypass the old takeover chain. `ReplacementCapabilityHash = SHA256("HX-EV-HISTORICAL-REPLACEMENT-CAP-1\0" || 01 || B(exact replacement V3 probe claim/carrier) || B(exact replacement configuration) || B(exact handler graph and loader closure))` is tag `0e`; tag `08` uses C2's authenticated purpose-21 `ResultObservationHash` for the definitive Accepted result together with the exact acceptance-index readback named by that result. `OldEffectKeyHash = SHA256("HX-EV-OLD-EFFECT-KEY-1\0" || 01 || B(exact seven-field old EventEffectKey))`; `HistoricalMappingHash = SHA256("HX-EV-HISTORICAL-MAPPING-HASH-1\0" || 01 || B(exact mapping record) || B(exact authenticated mapping CAS receipt))`; tag `0f` is `SHA256("HX-EV-HISTORICAL-RECONCILIATION-1\0" || 01 || B(exact old-provider receipt and uncertain-operation ledger) || B(exact pre-mapping cross-provider exclusion-fence receipt))`; the ownership-CAS receipt is issued **after** sealing the mapping record and is authenticated in `HistoricalMappingHash`, never included in tag `0f`. The mapping receipt's installed-record tag hashes **only** the exact pre-CAS mapping record under `SHA256("HX-EV-HISTORICAL-MAPPING-RECORD-1\0" || 01 || B(exact record))`; its generation/ETag then contributes to `HistoricalMappingHash`. Purpose-13 grant validity is checked separately at accepted revision and invocation; a mapping record or its digest alone grants no historical authority. Expired/revoked current leases prohibit new acceptance but do not erase an already accepted obligation; unprovable historical authority is `ConsumerMembershipFenceUnavailable` and keeps physical acknowledgement pending.

## C3. Raw ingress, route effects and physical acknowledgement

Replace DTO-only `MapEventStoreDomainEvents` with bounded raw ingress that authenticates Binary or structured data, CloudEvent core, component/topic and `hx-*` application metadata into `VerifiedDeliveryContext` **before** DTO/marker/handler selection. It verifies purpose-02 signature, exact prior pin obligation where old RegistryFingerprint is approved, global Committed MessageId batch root and addressed member proof, fresh authenticated actor source/StoredDigest and current purpose-01 effective route/view. An old purpose-02 pin is valid only under original signed issuance/key interval and exact retained obligation; independently re-evolve the current view under active registry/trust. Missing old key/obligation or current source/view holds, not poison acceptance. DTO `isAdapted`, `verifiedEffectiveEvent` or caller headers never assert authority. The public unsealed six-argument `EventStoreDomainEventContext` and nullable `GlobalPosition` remain source/binary compatible; the additive immutable verified wrapper carries non-null signed global position where required, exact stored/effective provenance, route/digests and private proof copies. Old handlers remain callable only behind a proven effect-safe adapter.

At the authenticated physical membership revision, resolve the **complete** sorted addressed HandlerRouteId set. A valid addressed event with no handler is `HandlerCapabilityMismatch` and retries; a predeclared signed catalog filter may terminalize only its proven out-of-scope route. CAS-stage/read back `HX-EV-BINARY-HANDOFF-1\0 || 01 || 0011`: tags `01..08` U MessageId/tenant/domain/aggregate type/aggregate ID/component/topic/physical subscription ID, `09` N membership revision, `0a..0e` B32 StoredDigest/DeliveryDigest/pinned body hash/pinned attestation hash/route-set hash, `0f` B route set (`u32 count || sorted unique U HandlerRouteId`), `10` U stable delivery OperationId, `11` B32 pinned core/header/transport-rule hash. At most 4,096 routes and 1 MiB complete manifest. Its key hashes component/topic/physical subscription/MessageId; revision, digests and route set are **values** under that one key. Partial staging or changed evidence leaves physical delivery unacknowledged. A committed pointer authorizes the complete set only after all route records and pin read back. Duplicate admission checks global pin/tombstone, actor source, manifest and each route receipt first; another route's receipt never completes this route.

`EventEffectKey` is exactly `(tenant, domain, aggregateType, aggregateId, MessageId, DeliveryDigest, HandlerRouteId)`. Provider's canonical `HX-EV-EFFECT-RECEIPT-1\0 || 01 || 000e` (≤16 KiB) fields `01..07` are that seven-part key; `08` U provider operation ID, `09` B32 committed effect-result digest, `0a` U provider identity, `0b` B exact backend descriptor, `0c` U mode, `0d` N actual commit version, `0e` U actual ETag. `ReceiptDigest = SHA256(exact complete record)` remains the historical receipt-content digest; C3 terminal-decision references use the separately domain-separated `EffectReceiptHash` below, which also binds the provider commit/readback receipt. Authenticate full bytes/readback, not DTO or digest alone. `AtomicStore` commits enlisted effects and unique-key receipt in **one** serializable transaction; `ProviderIdempotency` enforces the exact key on the external effect and exposes the same queryable result/receipt across restarts. Before handler invocation query the exact receipt. After crash/uncertain commit query again; a match finalizes that route without invoking handler. Changed key, mode, backend, ETag or result conflicts. A standalone marker or later receipt cannot emulate atomic safety. `HandlerCapabilityHold` occurs before effect/ack if the route lacks this contract and its V3 probe.

All C3 effect, route-decision, filtered, route-quarantine and physical-filter records have a 16 KiB complete encoded cap, including domain/version/tag framing; the C4 physical-quarantine receipt has the same cap. Every U field obeys A8's 1,024-byte encoded-value bound, and the effect receipt's backend descriptor B is at most 1,024 bytes. Before A4 Prepared, preflight the worst-case complete encoded record for each planned member/route using full admitted identifiers, provider IDs, backend descriptor, ETag, signed claim and reference lengths; reserve its record slots, effect-provider transaction capacity, B6 scratch and A8 referenced-evidence quota. Before any handler/effect invocation, redo exact-byte preflight for the accepted revision and actual provider descriptor/ETag bound. A provider that cannot atomically store/read back the full receipt and terminal decision under those caps cannot advertise V3 readiness. If a valid admitted route cannot fit, hold before Prepared or before invocation; never commit an effect that cannot be receipted. The retry policy's maximum attempts must also fit the 64 MiB complete referenced-evidence reservation with up to two 16 KiB result observations per attempt plus chain heads, or admission lowers the signed maximum before Prepared. No identifier is truncated or assigned a new public cap.

A route has exactly **one** stable terminal-decision key, `route-decision:` plus lowercase-hex `SHA256("HX-EV-ROUTE-DECISION-KEY-1\0" || 01 || U authenticated tenant || U authenticated C3 handoff key text || U HandlerRouteId)`, independent of attempt ID or outcome. The committed handoff supplies the exact UTF-8 key text (including its namespace), not a digest or provider-local representation; `U` uses draft §4 length framing. Every provider uses those identical bytes to select the same CAS key, and changed scope or key text conflicts. `HX-EV-ROUTE-DECISION-1\0 || 02 || 000d` (≤16 KiB) has ordered `01..05` U component/topic/physical subscription/MessageId/HandlerRouteId, `06` B32 exact handoff hash, `07` B32 global pin hash, `08` U state (`EffectReserved`, `Completed`, `Filtered`, `Quarantined`), `09` O(B32) exact referenced effect/filter/quarantine receipt hash, `0a` O(U) reservation owner ID, `0b` O(N) positive monotonically increasing fence generation, `0c` O(Q) lease expiry UTC and `0d` O(B32) exact latest append-only takeover-record hash. `EffectReserved` has absent receipt hash, present owner/fence/expiry, and a takeover-chain hash only after takeover; `Completed` retains the winning owner/fence, has absent expiry, present receipt and a takeover-chain hash exactly when taken over; `Filtered`/`Quarantined` have absent owner/fence/expiry/reconciliation fields and present receipt. No null stands for an absent field. CAS/readback under one ETag permits absent→`EffectReserved`→`Completed` or absent→`Filtered`/`Quarantined`; an expired reservation may only CAS-renew or CAS-transfer to `EffectReserved` with a higher fence. No terminal state changes. The decision key lives in the effect provider's same linearizable transaction namespace; a two-store sequence unable to enforce the exclusive reservation and completion is unsupported.

Reserve for at most 60 seconds using the provider's authoritative UTC; renewal requires the current ETag, owner and fence before expiry. Every effect invocation carries that fence, and the provider must atomically exclude an older fence from new or late commits. A takeover after expiry first CAS-excludes the old owner, then queries the old fence's exact uncertain effect receipt and all pending commits under the same `EventEffectKey`; it binds the authenticated query/absence result in the append-only chain below. If the old effect committed, finalize its receipt without invoking again. Invocation under the new fence is permitted only when the provider proves that the old fence cannot commit later and that no effect/receipt exists; ambiguous or unavailable reconciliation holds. The old owner may never invoke after exclusion, and a second owner may never invoke concurrently. `AtomicStore` commits effect, receipt and `Completed` decision in one serializable transaction guarded by the live fence. `ProviderIdempotency` uses the reserved exact key for its external effect, queries an uncertain result after crash and finalizes `Completed` only from that authenticated receipt; a reservation never authorizes 2xx. Filter/quarantine CAS requires an absent decision and proved no reservation, including an expired one; timeout does not convert an effect into poison or filtered. Receipt records in separate locations have no terminal authority without the matching decision/readback. Every duplicate verifies the decision's exact handoff, pin, owner/fence history, evidence bytes and referenced receipt; a conflicting terminal outcome holds as an incident.

Each transfer appends `HX-EV-EFFECT-TAKEOVER-1\0 || 01 || 000d` under the **same route-decision key namespace** plus `:takeover:` and checked positive new-fence `N`, with ordered `01` B32 exact route-decision key hash, `02` B32 handoff/pin hash, `03` N old fence, `04` U old owner, `05` U old decision ETag, `06` Q old lease expiry, `07` N new fence, `08` U new owner, `09` B32 exact provider old-owner-exclusion receipt hash, `0a` B32 exact old-key uncertain-effect query/receipt hash, `0b` B32 predecessor takeover-record hash (all zero for the first), `0c` Q provider takeover UTC and `0d` U new decision ETag. The signed/provider-authenticated complete record and exact exclusion/query source images are ≤8 KiB and ≤16 KiB respectively, CAS-create once and read back in the provider's decision transaction. The provider must allocate and expose the new decision ETag inside the same linearizable CAS transaction before sealing tag `0d` of the takeover record, then prove that exact ETag in the authenticated postcommit readback; a provider that cannot do so cannot advertise fenced takeover. The decision's tag `0d` is the **latest complete takeover-record hash**, not a replaceable reconciliation assertion. Its first transfer links from the initial reservation's owner/fence/ETag; every later record must name the immediately preceding record, prior new owner/fence and decision ETag. Verify the full ordered chain from fence 1 to tag `0d`, each old-owner exclusion and uncertain-effect result, exact predecessor hashes, provider ETags and final receipt before invoking or acknowledging. Reserve up to 64 transfers and their ≤2 MiB total record/source evidence before first reservation; a 65th transfer or missing/ambiguous prior record holds the route and requires separately authorized recovery, never chain truncation or a reset fence. An accepted historical route/provider mapping retains this chain with the unchanged `EventEffectKey` through the maximum delivery/rollback horizon.

Takeover tag `02` is exactly `SHA256("HX-EV-EFFECT-TAKEOVER-BIND-1\0" || 01 || B(exact committed C3 binary handoff manifest) || B(exact global MessageId pin) || U tenant || U domain || U aggregateType || U aggregateId || U MessageId || B32 DeliveryDigest || U HandlerRouteId || U accepted providerId || U accepted backendId)`. B uses draft §4's checked four-byte length and hashes the complete retained bytes in streaming order; the scoped identifiers are the authenticated accepted EventEffectKey and configuration, each within A8's U bound. The accepted provider/backend identity stays fixed across every takeover even if a later mapping uses another provider. Read back the exact handoff, pin, key and accepted configuration, recompute tag `02` for every chain record, and require identical bytes and hash across the chain. A missing old pin/handoff or changed provider identity holds before another invocation.

Physical acknowledgement for an **identified addressed Binary or legacy JSON** delivery requires the committed handoff pointer and **every** addressed route's authenticated durable terminal decision and matching `Completed` effect receipt or permitted `Filtered` proof read back. A separately proven permanently invalid source uses a matching `Quarantined` decision and capture proof below; handoff alone is never a route result. One completed and one failed/unavailable route remains unacknowledged; redelivery resumes only incomplete routes. An authenticated identified empty addressed set is success only under C3's exact signed physical-filter receipt. C4's unidentified physical quarantine follows its separate no-MessageId rule and cannot be presented as a handoff or route result. `InProgress` acquisition needs a persisted fenced lease; current DAPR read-only acquisition is insufficient. Old `Completed`/`Dispatched` markers migrate by CAS only with independent scoped actor plus route-specific receipt/outbox/catalog proof and global-ID inventory; `Dispatched` finalizes from receipt, old `InProgress` retries only with proved idempotency. Ambiguous evidence holds. No-handler, unavailable key/provider, new capability/limit hold, cancellation or pin mismatch never maps to 200. Deterministic authenticated permanent schema/identity poison may be acknowledged **only after** exact-byte AD-31 durable capture/readback or the C4 authenticated broker-owned full-byte reference **and** one durable `Quarantined` decision/receipt for every addressed route. Its distinct canonical receipt is `HX-EV-ROUTE-QUARANTINE-1\0 || 01 || 000a`: `01..05` U component/topic/physical subscription/HandlerRouteId/MessageId, `06` O(B32) StoredDigest, `07` O(B32) DeliveryDigest, `08` B tagged authority reference, `09` B32 exact carrier-byte-and-header hash, `0a` B32 exact authenticated source/retention proof hash. Tag `08` is exactly `00 || U AD-31 capture key || B32 exact AD-31 item/index proof hash` or `01 || U broker immutable full-byte reference ID || B32 exact broker reference proof hash`, enclosed once by `B`; other discriminators, untagged strings and bare hashes are invalid. The `00` verifier decrypts and reads back the complete AD-31 item and index, checking exact bytes, headers, tenant/scope, ETag, proof and retention. The `01` verifier uses the accepted broker authority to resolve and read back the **entire** ≤193 MiB carrier and every header under its immutable ID, then checks signed reference/retention proof, byte-for-byte equality and the horizon. Neither authority's proof is interchangeable with the other. It is ≤16 KiB, keyed by physical delivery plus route ID under the handoff, create-once/read back, and its optional digests cannot be fabricated when parsing failed; the 128 MiB local capture limit never turns a larger structured addressed carrier into an uncloseable poison route. Compare exact source, route set, captured/referenced bytes and retention on duplicate. It records no effect and cannot stand in for `Completed` on a valid event, missing handler or unavailable capability. Only after all decisions, receipts, capture/reference and manifest readback may poison receive 2xx. Capture never permits replay/projection checkpoint advancement. An untrusted carrier is not proven permanent source poison.

An **addressed** `Quarantined` route needs one active **physical retained-object-scoped** complete obligation set before any route decision or 2xx; C4's unidentified physical **disposition receipt** has a separate key, while its storage obligation uses this same canonical object key and head. The canonical physical object ID is the immutable AD-31 capture object key (tag `00`) or broker immutable full-byte object ID (tag `01`), inside its authenticated storage partition and named storage authority. The tagged reference proof hash and generation/ETag are authenticated **values** at that object, never identity or key components. The stable key is `retained-object-obligation:` plus lowercase-hex `SHA256("HX-EV-RETAINED-OBJECT-KEY-1\0" || 01 || U authenticated storage partition || U storage authority ID || u8 authority tag || U canonical physical object ID)`; no handoff key or retry ID enters it. One storage object has exactly one linearizable head and one no-expiry fence even if several committed Binary/legacy handoffs cite it. Different bytes, partition or tagged authority at the same key conflict; changed reference proof follows the object-wide version/CAS chain with authenticated source and full-byte readback, or conflicts. An unproved exclusive-reference claim does not excuse use of the object-wide key. Capacity for up to 128 open entries, all anticipated handoff links and route binds is reserved before the first quarantine decision; a 129th obligation holds before attachment rather than splitting the same object across fences. An AD-31 item/index key or broker reference resolves to exactly one canonical object ID through an immutable signed authority mapping, verified with complete bytes and the storage CAS readback. Distinct IDs may carry identical bytes only with independent fences; two aliases for the **same** object require a signed canonical-ID mapping to this one key. A renewed proof/generation for the same object must traverse this one version/CAS head with full-byte readback or conflict; it cannot create another addressed or unidentified fence. Capture links, purpose-18 sources and every handoff obligation resolve the same canonical object/head.

The storage authority CAS-installs `HX-EV-ADDRESSED-OBLIGATION-SET-1\0 || 02 || 000b` (≤16 KiB) at that object key: `01` U authenticated storage partition, `02` B exact tagged reference, `03` B32 complete carrier/body/ordered-header hash, `04` B32 signed immutable reference/source hash, `05` O(N) expected prior object generation, `06` O(U) expected prior ETag (both absent only at creation), `07` B complete canonical open-obligation entries, `08` U `NoExpiryUntilClosure`, `09` U storage authority, `0a` Q install UTC and `0b` B32 partition/access-policy hash. Resulting generation/ETag occur only in the atomic post-CAS receipt. This initial record CAS-creates the **one** shared retained-object head; an existing unidentified physical initialization at the same key prevents a second initial record and requires a signed conversion of all obligations under the one head or holds. The first handoff's `AddressedRoutes` entry opens in this initial list; a later handoff must CAS-append its own entry to the same head **before** its capture link or any quarantine decision. The immutable committed handoff contains only its own historical route set and pin; it never claims to bind a future object set. Every handoff independently proves all of its routes against its own post-handoff link, while object readback proves the union of every handoff's open obligations and closures.

The canonical open-list is `HX-EV-CAPTURE-OBLIGATIONS-1\0 || 01 || u16 count ||` 0..128 `B(exact entry)` fields sorted uniquely by `(kind, obligationId)` encoded bytes. Each `HX-EV-CAPTURE-OBLIGATION-ENTRY-1\0 || 01 || 0005` entry is `01` U kind from `AddressedRoutes`, `Retry`, `DelayedDelivery`, `Rollback`, `Backup`, `Incident`; `02` U stable obligation ID; `03` B32 exact authenticated opening-source hash; `04` Q opened UTC; `05` U owning authority. An initial list must be nonempty; a zero-entry later version is legal only with authenticated closure of every prior entry and broker no-future-duty proof. `CaptureEntryHash = SHA256("HX-EV-CAPTURE-ENTRY-HASH-1\0" || 01 || B(exact canonical entry))`; each opening-source hash is `SHA256("HX-EV-CAPTURE-OPEN-SOURCE-1\0" || 01 || B(exact authority record and carrier) || B(exact source readback receipt))`, whose authority and bytes are verified for that entry kind. Each `AddressedRoutes` entry uses one committed handoff key as its unique ID, with the exact handoff/route-set hash and authenticated opening receipt as source, and covers **every** route in that handoff's complete signed set. A shared object retains one distinct entry per handoff; closure of one requires all of that handoff's matching terminal route decisions/receipts and broker no-future-route proof, and cannot remove any other handoff entry. Every other open duty has a separate exact entry. The complete list is ≤12 KiB inside each ≤16 KiB set/version; source records, exact entries and their authority signatures are retained and read back, so a root alone grants no removal. An addition supplies an authenticated opening source and appears in the next full list before its duty becomes active. A removal requires `HX-EV-CAPTURE-OBLIGATION-CLOSE-1\0 || 01 || 0006`: `01` B32 exact entry hash, `02` B32 initial obligation-anchor hash (addressed initial set or physical initial fence), `03` B32 authenticated completion-source hash, `04` B32 broker/storage no-future-duty proof hash, `05` Q closure UTC, `06` U closure issuer; its draft §6 P-256/P1363 carrier is signed under distinct capture-closure purpose `27` by the configured AD-31 or broker storage authority. Verify exact claim bytes, issuer/SPKI, purpose, signed UTC within the pinned interval, revocation, complete completion/no-future-duty source and byte-identical readback before removing that one entry. The carrier, full source and readback are retained for the whole chain. Closure of one route cannot remove the collective `AddressedRoutes` entry. Unknown kinds, duplicate IDs, missing source, unsupported count/byte capacity or an unproved closure hold before quarantine.

The common authenticated storage CAS receipt is `HX-EV-CAPTURE-CAS-RECEIPT-1\0 || 01 || 000a` (≤16 KiB): `01` U stable object key, `02` U unique CAS operation ID, `03` B32 `CaptureInstalledRecordHash = SHA256("HX-EV-CAPTURE-INSTALLED-1\0" || 01 || B(exact **pre-CAS** installed record))`, `04` O(N) expected prior generation, `05` O(U) expected prior ETag, `06` N resulting generation, `07` U resulting ETag, `08` B32 full retained-object bytes hash (including ordered headers for a carrier object), `09` U storage authority ID and `0a` Q commit UTC. `CaptureCasReceiptHash = SHA256("HX-EV-CAPTURE-CAS-RECEIPT-HASH-1\0" || 01 || B(exact authenticated receipt))`; all new CAS-receipt references use this hash after full receipt verification. The provider must atomically commit the record/object fence and persist this receipt under the operation ID, allocating the resulting generation/ETag inside that linearizable transaction. Lost acknowledgement queries the immutable receipt by operation ID; a changed replay conflicts. The selected provider capability is the atomic post-CAS receipt: it cannot place an unknown postcommit ETag inside a pre-CAS signed record or synthesize a receipt later from mutable object state. Set/version readback verifies exact installed bytes, receipt signature or backend authority, prior compare values, resulting generation/ETag, full retained bytes and current object head together; missing atomic receipt or unsupported provider holds. `InitialSetHash = SHA256("HX-EV-ADDRESSED-SET-HASH-1\0" || 01 || B(exact set record) || B(exact storage CAS receipt))`; `AddressedVersionHash = SHA256("HX-EV-ADDRESSED-VERSION-HASH-1\0" || 01 || B(exact version record) || B(exact storage CAS receipt))`. All references to initial set or predecessor version hash below use these formulas, after authenticating both full inputs.

The order is record bytes → `CaptureInstalledRecordHash` → atomic CAS receipt → `InitialSetHash` or `AddressedVersionHash` → next version. No record contains its own post-CAS receipt/hash, and no CAS receipt names a hash that includes that receipt. Apply this identical acyclic order to physical fence/version records and legacy side/attempt records.

Each closure-list value is `u16 count ||` 0..128 `B(item)` fields ordered by closed `CaptureEntryHash`, where `item = B(exact purpose-27 claim) || B(exact purpose-27 carrier) || B(exact completion-source readback receipt)`. Duplicate closure entries, an unclosed removal or a closure unrelated to the preceding open list conflict.

All C3 cross-record B32 fields use full authenticated bytes and the following exact formulas: `RouteDecisionHash = SHA256("HX-EV-ROUTE-DECISION-HASH-1\0" || 01 || B(exact decision record) || B(exact provider CAS receipt))`; `EffectReceiptHash = SHA256("HX-EV-EFFECT-RECEIPT-HASH-2\0" || 01 || B(exact complete immutable effect record) || B(exact authenticated provider effect-commit/CAS receipt) || B(exact provider readback receipt))`; `FilteredReceiptHash = SHA256("HX-EV-FILTERED-RECEIPT-HASH-1\0" || 01 || B(exact filtered receipt) || B(exact predicate claim and carrier) || B(exact decision transcript) || B(exact CAS receipt))`; `RouteQuarantineHash = SHA256("HX-EV-ROUTE-QUARANTINE-HASH-1\0" || 01 || B(exact quarantine receipt) || B(exact tagged-authority full-byte readback receipt) || B(exact CAS receipt))`; `PhysicalFilterHash = SHA256("HX-EV-PHYSICAL-FILTER-HASH-1\0" || 01 || B(exact physical-filter receipt) || B(exact purpose-28 identity claim and carrier) || B(exact filter transcript) || B(exact CAS receipt))`; `CaptureRouteBindHash = SHA256("HX-EV-CAPTURE-ROUTE-BIND-HASH-1\0" || 01 || B(exact route-bind record) || B(exact backend CAS receipt))`. Their keys remain the stable keys named above; every referenced record/carrier/CAS receipt is read back and authority-verified before computing a hash. To avoid a self-reference, the atomic `AtomicStore` transaction writes the complete effect record and `Completed` decision tag `09` as the historical `ReceiptDigest = SHA256(exact complete effect record)`; it does not embed the later `EffectReceiptHash`. The provider atomically persists an immutable commit/CAS receipt keyed by the effect operation and decision key, naming exact installed record hashes, resulting version/ETag and transaction identity, then supplies a separate authenticated readback receipt for those same bytes and keys. Only **after** commit/readback does the verifier derive `EffectReceiptHash` above and authorize the decision or route bind; a missing, changed or unqueryable provider receipt holds even if the precommit decision exists. `ProviderIdempotency` follows the same ordered readback rule. The decision verifier requires both its tag-`09` precommit digest and the postcommit `EffectReceiptHash`/provider receipts; neither may be inferred from the other. The C3 route-bind tag `04` uses `RouteDecisionHash`, tag `05` uses the matching one of `EffectReceiptHash`, `FilteredReceiptHash` or `RouteQuarantineHash`; C3 route-decision tag `09` uses the matching `FilteredReceiptHash` or `RouteQuarantineHash`; for `Completed` it uses the precommit `ReceiptDigest` and must additionally verify the postcommit `EffectReceiptHash` and both provider receipts before the state has terminal authority. A bare SHA-256 of a DTO or a hash-only proof grants no route decision. The embedded cross-record fields are also fixed: filtered receipt tag `12` is `FilteredDecisionTranscriptHash = SHA256("HX-EV-FILTER-TRANSCRIPT-HASH-1\0" || 01 || B(exact accepted-revision route transcript))`, while physical-filter tag `0e` is `PhysicalFilterDecisionTranscriptHash = SHA256("HX-EV-PHYSICAL-FILTER-TRANSCRIPT-HASH-1\0" || 01 || B(exact accepted-revision physical transcript))`. Route-quarantine tag `09` is `AddressedCarrierHash = SHA256("HX-EV-ADDRESSED-CARRIER-1\0" || 01 || B(exact complete transport body) || u32 orderedHeaderCount ||` each `B(exact original header name) || B(exact original header value))`; its tag `0a` uses the `HX-EV-ADDRESSED-ROUTE-PROOF-1` formula below. The tagged AD-31 reference proof is `SHA256("HX-EV-AD31-REFERENCE-PROOF-1\0" || 01 || B(exact signed item/index record and carrier) || B(exact storage CAS receipt) || B(exact full-byte readback receipt))`; the tagged broker reference proof uses the distinct `HX-EV-BROKER-REFERENCE-PROOF-1\0` domain with the same framed three source classes under broker authority. Compare each full authenticated source, signer, generation/ETag and byte stream before its digest. No alternate bare-hash encoding is accepted.

After the initial set and CAS/full-byte readback, CAS-create `HX-EV-ADDRESSED-CAPTURE-LINK-1\0 || 01 || 000b` (≤16 KiB) at `addressed-capture-link:` plus SHA-256 of `U handoff key || B32 exact handoff hash`: `01` U handoff key, `02` B32 handoff hash, `03` B32 full handoff route-set root, `04` B tagged reference, `05` B32 exact initial object-set anchor hash, `06` B32 active object-head CAS-receipt hash **at this handoff attachment**, `07` N resulting attachment generation, `08` U resulting attachment ETag, `09` B32 complete carrier/header hash, `0a` U storage authority ID and `0b` Q link UTC. The link is signed/backend-authenticated and read back with the committed handoff, initial object set and complete chain through the attachment generation; that generation must contain this handoff's exact `AddressedRoutes` entry before link creation. Its immutable create-once `HX-EV-ADDRESSED-CAPTURE-LINK-CAS-1\0 || 01 || 0007` receipt (≤16 KiB) has `01` U link key, `02` B32 exact link hash, `03` U literal `Absent`, `04` N resulting link generation, `05` U resulting link ETag, `06` U storage authority ID and `07` Q link commit UTC; the link and receipt commit atomically and read back together. The link key excludes the tagged reference, so another reference or changed link for this same handoff conflicts rather than creating a second authoritative link. `CaptureLinkHash = SHA256("HX-EV-ADDRESSED-CAPTURE-LINK-HASH-1\0" || 01 || B(exact link) || B(exact authenticated link CAS receipt))`; route binds and readback use this hash after both inputs verify. Each route-quarantine tag `0a` is `SHA256("HX-EV-ADDRESSED-ROUTE-PROOF-1\0" || 01 || B(exact permanent-poison proof) || B(exact link and link CAS receipt) || B(exact initial set and storage CAS receipt) || B(each complete object-version record and CAS receipt through this handoff attachment) || B(exact full-byte readback receipt) || U HandlerRouteId)`. It proves that the handoff entry exists in the object-wide union before its first quarantine decision; every later 2xx separately verifies the chain through the current head. The route decision references that exact quarantine receipt. For every route in the complete handoff set, CAS-create `HX-EV-ADDRESSED-CAPTURE-ROUTE-BIND-1\0 || 01 || 0007` (≤16 KiB) at the capture-link key plus `U HandlerRouteId`: `01` B32 exact capture-link hash, `02` B32 initial set hash, `03` U HandlerRouteId, `04` B32 exact terminal route-decision hash, `05` B32 exact matching completed/filtered/quarantined receipt hash, `06` B32 committed handoff hash and `07` U receipt authority ID. It is backend-authenticated and read back with the exact decision and receipt. A completed or filtered decision made before capture is linked **forward** only by this post-handoff record; its immutable old bytes are never claimed to bind a future set. A quarantined decision additionally binds the link through its tag-`0a` receipt proof. Before 2xx, enumerate the handoff route set and require exactly one matching route-bind record per route, all naming this handoff's **same** link/object-set anchor/route root/tagged reference, plus the active storage chain. A missing link/bind, a route absent from the handoff, a second conflicting reference or a changed earlier decision holds; the immutable handoff itself is never rewritten. Preflight one link, one create-once link receipt and at most 4,096 complete route-bind records within provider and B6/A8 evidence quotas **before** the first quarantine decision; an unsupported slot/record size holds before any route effect or physical 2xx.

To change duties, CAS-append `HX-EV-ADDRESSED-OBLIGATION-VERSION-1\0 || 01 || 0009` (≤16 KiB) under the same storage key plus contiguous version: `01` B32 initial set hash, `02` N version, `03` B32 prior version hash (initial set hash at version one), `04` N expected prior generation, `05` U expected prior ETag, `06` B complete **full** current open-list, `07` B exact list of `B(signed closure record and source receipt)` for entries removed at this version, sorted by the closed entry hash (empty if none), `08` U `NoExpiryUntilClosure`, `09` Q version UTC. The storage CAS atomically advances the object/head and persists a common post-CAS receipt whose tag `03` names only the exact pre-CAS version-record hash and whose later tags contain resulting generation/ETag; version `n+1` names version `n` and consumes its resulting compare values. Every prior open entry is present in the new full list unless its individually authenticated closure is in tag `07`; every addition has its opening source. The initial object identity and each immutable handoff capture link never change; later handoff entries enlarge the object-wide union before their links are created. Earlier receipts retain the immutable initial link/set and verify **every** complete version, closure, receipt and full list through the active storage head rather than demand their initial ETag equal the current one. Every original or duplicate poison 2xx reads back the exact retained body and all ordered headers, active no-expiry fence, all open entries and closure proofs, handoff/link, and every addressed route decision/receipt under the current generation/ETag. Missing bytes, partial set, a gap, stale or expiring fence holds non-2xx. Storage atomically blocks expiry, deletion, key retirement and quota refund until every obligation closes with independent proof and no new delivery can arise; a calendar horizon or promised renewal is insufficient.

The catalog filter predicate is an independently signed, accepted-revision claim, not a mutable catalog lookup. `HX-EV-CATALOG-FILTER-1\0 || 01 || 0011` (claim plus signature carrier ≤4 KiB) uses draft §4 ordered tags: `01` U issuer ID; `02..05` U domain/component/topic/physical subscription; `06` U literal `Route` or `Physical`; `07` O(U) HandlerRouteId (present only for `Route`, absent only for `Physical`); `08` N positive accepted membership revision; `09` B32 accepted membership-claim hash; `0a` B32 exact accepted route or physical-filter configuration hash; `0b` B32 accepted RegistryFingerprint; `0c` B32 accepted transform implementation/manifest hash; `0d` U literal `HX-EV-SELECTOR-1`; `0e` B exact expression (≤2 KiB); `0f` U literal `HX-EV-FILTER-INPUT-1`; `10..11` Q issued/expiry UTC. The draft §6 carrier signs these **exact claim bytes** with distinct purpose `14` and the accepted revision's named catalog-filter authority/issuer, not an event, membership or historical-grant key. Verify signature, purpose, issuer/SPKI, scope, accepted revision/configuration, broker UTC in `[issued,expiry)` at acceptance, and signed revocation state before filtering; later revocation or missing historical trust proof holds an owed route rather than reevaluating under a new claim.

The only admitted expression language is `01 || u16 rowCount ||` 1..64 rows, each `U tenantId || U domain || U canonicalEventType || I minimumVersion || I maximumVersion`, with positive inclusive bounds ≤1024 and minimum ≤maximum. Rows are sorted uniquely by their complete encoded bytes; no wildcard, regex, host callback, locale, clock or implicit type alias exists. Evaluation is the Boolean OR of exact ordinal UTF-8 equality for the first three fields and inclusive integer comparison for version. `Filtered` requires that Boolean to be false. The exact accepted-revision input is `HX-EV-FILTER-INPUT-1\0 || 01 || 0007` with ordered `01..03` U tenant/domain/accepted-effective canonical event type, `04` I accepted-effective version, `05` B32 authenticated original StoredDigest, `06` B32 accepted RegistryFingerprint and `07` B32 exact accepted transform implementation/manifest hash. It is retained as bytes with source/transform proof before any filter decision; the claim's schema ID and transform hash must match it. The input hash is SHA-256 of those **complete bytes**. Only authenticated reconstruction from the original source and the retained, exact accepted-revision transform bytes may replace a missing input, with byte-identical readback; unknown language, schema, transform or claim bytes hold. The route and physical-filter transcripts retain the exact claim, input, evaluation result and source proof; their receipt hashes bind those retained bytes. Check the complete 4 KiB signed-claim carrier and 16 KiB receipt capacities before accepted-revision activation; an oversized valid catalog configuration holds rather than truncating a claim or input. Current evolution cannot change an accepted filter decision.

Ordinary expiry after a route was accepted does not revoke its historical filter decision: the accepted broker receipt retains the signed claim, acceptance UTC, key/interval proof and exact predicate/input/transform bytes until the owed route closes. A delayed route decision verifies that the claim was valid **at acceptance**, not that delivery occurs before its expiry. Explicit later key revocation, missing accepted-time proof or changed retained bytes holds the route; current catalog settings never classify that already accepted delivery. Physical filtering for an empty accepted set uses the same historical-time rule.

`Filtered` has one distinct, durable, effect-free receipt: `HX-EV-FILTERED-RECEIPT-1\0 || 01 || 0014`, with ordered tags `01..07` equal to the exact seven `EventEffectKey` fields, `08..0a` U component/topic/physical subscription ID, `0b` N accepted membership revision, `0c` B32 handoff route-set hash, `0d..0e` B32 pinned body/attestation hashes, `0f` B exact purpose-specific signed catalog predicate claim, `10` N predicate/route revision, `11` B32 authenticated **accepted-revision** source/view proof hash, `12` B32 exact decision-transcript hash, `13` U literal `Filtered`, `14` B32 exact predicate-input hash. The complete record is ≤16 KiB; a separately retained ≤64 KiB transcript binds full source/pin, selected event type/version, accepted RegistryFingerprint/transform, exact effective predicate input bytes, catalog route identity, predicate bytes and deterministic out-of-scope decision. The accepted membership revision pins both predicate **and input**. Current evolution cannot silently substitute a later effective view; if the original input is unavailable, historical reconstruction is permitted only from authenticated original source and retained accepted-revision registry/transform implementation bytes plus a matching signed proof, otherwise hold. The signed catalog claim fixes predicate bytes, route revision, scope and validity at acceptance. Its receipt key is the C3 handoff key plus `U HandlerRouteId` and `filtered:`, independent of attempt ID. CAS create-once, authenticate the retained acceptance-time signed claim, broker UTC interval and trust proof at the accepted membership revision, plus current revocation state and exact transcript/source/input, then read back complete bytes and referenced proof before proposing the matching `Filtered` route decision. Changed pin, route, revision, input or decision conflicts. `Filtered` authorizes no handler/effect, marker completion by itself or checkpoint; a failed handler cannot be relabeled filtered.

An empty addressed set requires `HX-EV-PHYSICAL-FILTER-1\0 || 01 || 0010` (≤16 KiB), keyed by `physical-filter:` plus SHA-256 of `U component || U topic || U physical subscription || U stable delivery OperationId || U MessageId`, never broker attempt ID. Ordered tags are `01..05` those five U values, `06` N accepted membership revision, `07..08` B32 exact pinned body/attestation hashes, `09` B32 authenticated source/StoredDigest hash, `0a` B32 empty canonical route-set hash, `0b` B exact signed accepted-revision catalog predicate claim, `0c` N predicate/route revision, `0d` B32 accepted-revision predicate-input hash, `0e` B32 full decision-transcript hash, `0f` U literal `Filtered`, `10` B32 `FilterAcceptedSourceHash` from the first definitive C2 Accepted result and immutable broker delivery-log entry below. Its retained transcript proves the same accepted-revision source/input and deterministic physical out-of-scope predicate; no current route state substitutes. CAS create-once and authenticate/read back exact claim, input, transcript, pin, source, empty route set and broker receipt before 2xx. A duplicate with changed bytes, revision or proof conflicts. No unauthenticated empty set, status name alone or filter receipt lacking the matching decision authorizes physical acknowledgement.

The stable delivery OperationId in the physical-filter key and tag 04 is derived from C2's first definitive Accepted HX-EV-PUBLICATION-ATTEMPT-RESULT-1 claim and purpose-21 carrier, its purpose-20 registered attempt, the purpose-22 signed chain head and the broker's immutable physical delivery-log entry. The broker CAS-creates HX-EV-ACCEPTED-DELIVERY-LOG-1\0 || 01 || 000c: tags 01..06 are U authenticated tenant/domain/component/topic/physical subscription/broker-log partition; 07 is U immutable physical delivery-log ID; 08..09 are N accepted membership revision/log generation; 0a is B32 C2 ResultObservationHash; 0b is B32 AcceptedImageHash; and 0c is B32 exact broker acceptance-index hash. AcceptedImageHash = SHA256("HX-EV-ACCEPTED-IMAGE-1\0" || 01 || B(exact accepted rendered body) || B(exact complete accepted header image)). The accepted-revision broker delivery authority signs the complete log claim under purpose 28; an atomic log CAS receipt names only the pre-CAS log claim/carrier hash, expected generation/ETag, resulting generation/ETag, authority and commit UTC. No signed log claim contains an unknown resulting ETag. The create-once log key is `accepted-delivery-log:` plus lowercase-hex `SHA256("HX-EV-ACCEPTED-DELIVERY-LOG-KEY-1\0" || 01 || U tenant || U domain || U component || U topic || U physical subscription || U broker-log partition || U immutable physical delivery-log ID)`; broker attempt ID is never an input. It maps every redelivery to the same log generation, first Accepted result and partition. A changed log entry, result, accepted revision or image at that key conflicts; missing readback holds.

FilterAcceptedSourceHash = SHA256("HX-EV-PHYSICAL-FILTER-ACCEPTED-1\0" || 01 || B(exact first Accepted result claim) || B(exact purpose-21 carrier) || B(exact signed log claim) || B(exact purpose-28 log carrier) || B(exact atomic log CAS receipt)). StableFilterOperationId is canonical lowercase hex of SHA256("HX-EV-PHYSICAL-FILTER-OP-2\0" || 01 || B32 FilterAcceptedSourceHash || U(exact committed C3 handoff key text)); C3 physical-filter receipt tag 10 and purpose-28 identity tag 05 equal FilterAcceptedSourceHash. The broker signs HX-EV-PHYSICAL-FILTER-IDENTITY-1\0 || 01 || 0008 under distinct physical-delivery purpose 28: tags 01..03 are U component/topic/subscription, 04 is U derived OperationId, 05 is B32 FilterAcceptedSourceHash, 06 is B32 handoff hash, 07 is N signed accepted membership revision and 08 is Q Accepted UTC. Its ≤16 KiB claim/carrier, complete first Accepted result/attempt/chain, immutable signed delivery-log claim/carrier/CAS receipt and handoff are retained/read back before the physical-filter create-once CAS. The verifier checks tenant, domain, component/topic, subscription, partition, accepted revision, complete body/header pin, C2 attempt/index, log generation/ETag and signed Accepted UTC against the accepted revision's pinned broker physical-delivery issuer/SPKI/purpose/interval and current revocation. Redelivery or a changed broker attempt ID resolves the same log entry, source hash and filter key; changed source, pin, revision, route-set or predicate bytes at that key conflict. A missing original Accepted result/log identity, handoff or signed revision holds empty-route acknowledgement.

## C4. Legacy handoff, capture and safe diagnostics

Queued flat JSON is a separately selected ingress mode. Authenticate original broker component/topic/physical subscription/configuration revision, exact body/core/headers and original publication/outbox, then trusted source lookup by scoped MessageId. Derive the **historical complete** logical route set; current membership or broker attempt ID cannot fill a missing route. A one-time JSON→binary migration is permitted only with independent proof of **no prior binary pin/send**. If an authenticated prior binary pin already exists, reuse its exact bytes only after matching original JSON source, historic route set and send intent; an ambiguous prior send or pin conflicts/holds without a second pin. Under global MessageId CAS, install or reuse that one binary pin/shared send intent and `HX-EV-LEGACY-HANDOFF-1\0 || 01 || 0017` manifest plus every route record under one committed batch pointer. Handoff OperationId encodes component/topic/physical subscription/MessageId/original revision; per-route key adds HandlerRouteId. Manifest tags `01..0d` bind component/topic/subscription/revision/MessageId/scope/event key/sequence/StoredDigest/original JSON length; `0e` hashes **exact received JSON bytes**, `0f` binds exact original core/header identity, `10` hashes canonical decoded stored event/readable payload/protection/core identity, `11` complete sorted route set, `12` OperationId, `13..15` shared binary pin/body/attestation hashes, `16` exact immutable send intent, `17` authenticated encrypted original-body side-record hash. Manifest ≤2 MiB, send intent ≤1 MiB, ≤4,096 routes, original raw body ≤128 MiB and complete encrypted side record ≤193 MiB as specified below, checked before allocation. Whitespace may vary only when each exact attempt and all canonical decoded fields independently authenticate. Changed source, JSON content, route set, side record, revision or pin is `LegacyHandoffConflict` with no second effect. The old JSON physical delivery remains **unacknowledged** after handoff staging/readback. Acknowledge it only after the exact shared binary send has durable broker acceptance, side record/manifest/**all** route records and pin/send intent read back, **and every addressed route** has a durable terminal decision with authenticated `Completed`/`Filtered` receipt (or C3's capture-backed/broker-referenced `Quarantined` decision and receipt for proven permanent poison). No original JSON ack is inferred from merely creating a route record. Crash before pointer commit or before the last route result leaves JSON unacknowledged; after all results, redelivery reuses the same pin and receipts and only then acknowledges. A binary duplicate caused by redelivery uses the same per-route receipts and cannot create another effect.

Dead-letter parser accepts verified Binary/`data_base64` and legacy structured `data` with strict mode/size rules. It assigns replay-safe identity only after attestation and pinned transport/body/header verification; malformed identity gets only a support-safe unidentified hash, never an identified replay claim. AD-31 capture streams at most 128 MiB of raw body plus exact bounded headers into authenticated encrypted durable storage under the verified tenant scope for identified events or the broker-signed isolated physical capture scope for unidentified carriers, binding exact bytes, hash, scope/reason, retention horizon and actor index under CAS; read back all exact decrypted bytes/index before HTTP 200. A structured carrier above local capture size can be addressed poison only if the route-quarantine receipt binds a broker-owned immutable **full** carrier/header reference with authenticated byte-for-byte readback and retention beyond retry/rollback/incident obligations. If that primitive is unavailable, readiness must constrain broker admission so a >128 MiB structured carrier cannot become an addressed delivery; any unexpected carrier holds non-2xx. A malformed or over-cap carrier with no authenticated event identity or addressable route set follows a **separate physical-ack path with no MessageId or C3 handoff**. It can receive physical quarantine only when the broker proves its exact full bytes/header hash, immutable physical subscription, permanent nonadmissibility and durable exact-byte retention. The bounded `HX-EV-PHYSICAL-QUARANTINE-1\0 || 01 || 0009` receipt has `01..03` U component/topic/physical subscription, `04` B32 exact full transport byte-and-header hash, `05` B tagged authority reference, `06` B32 exact retention/readback proof hash, `07` U stable reason code, `08` U broker-signed stable physical delivery ID and `09` B32 broker nonadmissibility/fence proof hash; ≤16 KiB, create-once/read back before 2xx. Its tag `05` uses C3's exact `00 || U AD-31 key || B32 item/index proof hash` or `01 || U broker full-byte reference ID || B32 broker proof hash` inside one `B`, and the corresponding authority-specific full-byte/header readback verifier. Untagged or mismatched authority proof holds. It carries no MessageId, route completion or replay-safe identity and is unavailable if a valid addressed route might exist. Its 2xx rule is exact signed historical physical-scope/no-route proof, installed broker no-future-route fence, complete retained body and headers under the active C4 no-expiry storage obligation, then create-once physical receipt and full readback; a failed proof remains non-2xx. No handoff pointer or fabricated route decision is required or permitted on this path. Missing full-byte/retention/nonadmissibility proof, capacity or provider readback returns retryable non-2xx and holds activation; finite retries alone never prove quarantine. AD-31 capture proof is separate from `RawCorruptDisposition`, which closes inventory only and never lets replay pass corrupt history. Retain quarantine bytes/keys/source/pin/receipts through replay, rollback and incident obligations; deletion/quota refund require fenced authenticated absence. Diagnostics disclose access-controlled scope, sequence, size/bound, stored/effective version, readable/protection state, pin/lease/receipt/queue lifecycle and typed reason, never payload, secret, key alias, CLR identity, provider internals or stack.

For a whitespace-varied JSON redelivery, the first `HX-EV-LEGACY-HANDOFF` manifest and its tag-`17` original side record remain immutable; its tag-`0e` is the **first** exact JSON attempt hash, not a demand that all later raw attempts equal it. Each admitted exact attempt is independently authenticated and CAS-recorded as `HX-EV-LEGACY-ATTEMPT-1\0 || 01 || 000b`: `01..04` U component/topic/physical subscription/stable handoff OperationId, `05` N historical membership revision, `06` B32 exact body hash, `07` B32 exact core/header hash, `08` B32 canonical decoded source/readable/protection identity hash, `09` B32 first manifest hash, `0a` B32 route-set hash, `0b` B32 binary pin hash. Its stable key is the handoff key plus hashes of **exact** raw body and core/headers; a hash match requires full-byte encrypted side-record comparison. Create-once/readback binds each attempt's original bytes, core/header evidence, canonical source, route set and one binary pin before any receipt reuse or acknowledgement. At most 64 distinct attempts per handoff are retained; a new variant beyond that bound holds as `LegacyHandoffCapacityHold` without an effect or 2xx. Identical attempts reuse their record. Semantically equal whitespace may vary only when every exact attempt passes source and header authentication and yields the same canonical identity, route set and pin; changed decoded content, source, headers, revision or pin is `LegacyHandoffConflict`. The previous paragraph's changed-side-record conflict applies to changing the **first** side record or to any attempt that fails this binding. A matching authenticated prior binary pin follows the same attempt check and reuses its exact bytes; a conflicting pin is `LegacyHandoffConflict`, and unknown prior send authority holds.

Each first or whitespace-varied exact attempt has a deterministic encrypted full-byte variant side record at `legacy-variant-side:` plus SHA-256 of `U stable handoff key || B(exact attempt key)`, where the attempt key is the existing handoff key plus its exact body/core/header hashes. `HX-EV-LEGACY-VARIANT-SIDE-1\0 || 01 || 000a` (≤16 KiB metadata) has `01` U handoff OperationId, `02` B32 exact attempt-key hash, `03` N complete raw body length, `04` B32 exact raw body hash, `05` B32 exact canonical core/header hash, `06` B32 exact ordered original header-image hash, `07` B32 complete protected side-record output hash, `08` U isolated storage partition, `09` B32 exact protection configuration hash and `0a` U storage authority ID. Its protected output contains the **complete** original body, core and ordered header bytes under C4's 193 MiB side-record cap, including whitespace and duplicate non-routing headers; no normalized representation substitutes. The storage authority CAS-creates the immutable metadata and protected bytes, then issues the common post-CAS `HX-EV-CAPTURE-CAS-RECEIPT-1` with resulting generation/ETag and full encrypted-object hash. Readback decrypts the entire object, compares body/core/every original header byte to broker-authenticated attempt bytes, verifies metadata, generation/ETag, protection key and CAS receipt, and preserves the first manifest/tag-`17` side record unchanged. A duplicate locates **that variant** by handoff plus exact attempt key and performs complete byte comparison even if hashes match; same key with changed bytes conflicts, and missing/unverifiable bytes hold.

The attempt record is CAS-created at `legacy-attempt:` plus `SHA256("HX-EV-LEGACY-ATTEMPT-KEY-1\0" || 01 || U stable handoff key || B32 exact raw-body hash || B32 exact canonical core/header hash)` by the legacy storage authority. This stable key is independent of CAS retry ID, and its full byte source is authenticated before lookup. Its atomic `HX-EV-LEGACY-ATTEMPT-CAS-1\0 || 01 || 0009` receipt (≤16 KiB) is keyed by that same attempt key plus `SHA256("HX-EV-LEGACY-ATTEMPT-CAS-OP-1\0" || 01 || U attempt key || B32 LegacyAttemptRecordHash)` rendered as lowercase hex, and has ordered `01` U attempt key, `02` U CAS operation ID, `03` B32 `LegacyAttemptRecordHash = SHA256("HX-EV-LEGACY-ATTEMPT-RECORD-1\0" || 01 || B(exact **pre-CAS** attempt record))`, `04` O(N) prior generation, `05` O(U) prior ETag (both absent on create), `06` N resulting generation, `07` U resulting ETag, `08` U storage issuer and `09` Q commit UTC. The storage authority atomically commits the record and receipt; full-byte readback under that generation/ETag proves exact attempt bytes and authority. `LegacyAttemptCasReceiptHash = SHA256("HX-EV-LEGACY-ATTEMPT-CAS-HASH-1\0" || 01 || B(exact authenticated nine-tag attempt CAS receipt))`; `LegacyAttemptHash = SHA256("HX-EV-LEGACY-ATTEMPT-HASH-1\0" || 01 || B(exact attempt record) || B(exact attempt CAS receipt))`. The receipt hashes only pre-CAS record bytes in tag `03`, so these two post-CAS hashes are acyclic. A lost acknowledgement queries the receipt by operation ID and then the record by stable attempt key; identical bytes reuse it, changed bytes conflict, and absent or ambiguous readback holds.

Because `HX-EV-LEGACY-ATTEMPT-1` has no side-record field, CAS-create `HX-EV-LEGACY-ATTEMPT-SIDE-LINK-1\0 || 01 || 0006` (≤16 KiB) at `legacy-attempt-side-link:` plus SHA-256 of `U handoff key || B(exact attempt key)`: ordered `01` B32 `LegacyAttemptHash`, `02` B32 `LegacyVariantSideHash = SHA256("HX-EV-LEGACY-VARIANT-SIDE-HASH-1\0" || 01 || B(exact side metadata) || B(exact protected output))`, `03` B32 `CaptureCasReceiptHash` of the **variant side-record** CAS receipt, `04` B32 authenticated complete decrypted readback-receipt hash, `05` B32 exact original broker body/core/header source hash and `06` U storage authority ID. The link is backend-authenticated, create-once and read back with the complete attempt record, attempt CAS receipt, variant side metadata/protected bytes, side CAS receipt and full decrypted bytes before any old route receipt reuse or Binary/legacy physical 2xx. A crash between attempt, side and link creation resumes by querying these stable keys without another effect. The first attempt's link tag `02` must equal immutable manifest tag `17`; later attempts retain independent links and side records. A conflicting link or changed first manifest/side record holds. All variant records, links, generations/ETags, receipts and full bytes remain retained while retry, route, rollback or incident obligations remain open.

The 128 MiB legacy body cap is not a side-record cap. The durable side record retains **one** protected output over the exact original raw JSON, broker/application headers and canonical core framing; streaming input is not a second retained copy. Its checked reservation is `protected_output_bytes + unencrypted_record_metadata_bytes + nonce/tag/outer_framing_bytes ≤193 MiB`; the encrypted input is `raw_body_bytes + exact_header_bytes + canonical_core_framing_bytes`, with raw body ≤128 MiB, headers ≤64 KiB, core framing ≤1 MiB and provider-proven **worst-case incompressible** protection expansion ≤1 MiB over that complete input. Headers and core framing inside the protected output are charged **there once**, not again as unencrypted metadata. A provider with unknown or greater expansion must reduce its advertised raw-body limit to a value whose measured worst case fits, or hold before admission. Protection output must be ≥ complete encrypted-input length for the incompressible boundary vector; no assumed compression discount is valid. Input and protected-output streams use separately charged ≤1 MiB chunks as simultaneously live B6 scratch; a provider requiring a whole-body or whole-ciphertext buffer must lower its admitted maximum or hold. No buffer never retained is charged a second time as durable side-record bytes. The first side record and each of at most 64 variants consume reserved provider capacity; manifest (≤2 MiB), send intent (≤1 MiB), route records and side records are distinct retained objects under B6 operation/deployment quotas. AD-31 capture uses the same exact protected-output accounting; if it cannot retain/read back a carrier it cannot authorize ack. At a 128 MiB raw body, 64 KiB headers and 1 KiB core framing, 129 MiB protected output plus 1 MiB outer record metadata/framing fits the 193 MiB cap; 193 MiB protected output plus any nonzero outer framing fails. Over-limit or partial readback holds without dropping headers or protection overhead.

For unidentified physical poison, the tag-`08` stable physical delivery ID and tag-`09` nonadmissibility/fence proof in that receipt are mandatory. The stable CAS key is `physical-quarantine:` plus SHA-256 of `U signed capture scope ID || U component || U topic || U physical subscription || U stable physical delivery ID`, never broker attempt ID or a raw-byte hash. A broker must attest that this immutable ID denotes the same complete physical delivery across retries and that no valid addressed route exists under its pinned historical configuration. Before 2xx, read back the full exact body **and all headers**, encrypted AD-31 capture or broker-owned full-byte reference, its retention horizon, the complete receipt and signed broker proof; compare full bytes on duplicate. Same key with changed bytes, headers, subscription, reason or proof conflicts and remains non-2xx. A new attempt ID cannot create a second disposition. If the broker lacks a stable ID, full-byte access, historical route proof or a durable nonadmissibility fence, physical quarantine is unavailable and the carrier retries/holds. The reference and signing key persist through the maximum retry, rollback and incident horizon; a hash alone has no disposition authority.

For unidentified poison, the carrier supplies no trusted tenant. Before capture, the broker authenticates `HX-EV-PHYSICAL-CAPTURE-SCOPE-1\0 || 01 || 0008`: ordered `01` U broker issuer, `02..05` U domain/component/topic/physical subscription, `06` N immutable historical subscription/configuration revision, `07` U isolated capture/access scope ID and `08` B32 exact signed physical configuration hash. Its complete ≤16 KiB record is signed with distinct purpose `1b` by the physical-subscription authority and bound to the immutable broker delivery-log generation. The storage key is the scope ID plus stable physical delivery ID; the scope ID is derived only from signed subscription/configuration bytes, never from carrier tenant, headers or parser output. A single-tenant physical subscription may map to its authenticated tenant partition; a shared subscription uses a separate broker-owned isolation partition whose access policy is bound to the signed physical subscription and disallows tenant-scoped lookup. AD-31 encrypts, indexes and reads back in that exact partition; scope/configuration ambiguity or cross-tenant readback holds non-2xx. The capture-scope claim hash is SHA256(B(exact complete claim) || B(exact purpose-1b signature carrier)) and is bound into the identity source below.

The broker coordinates three separately signed **source records** before unidentified quarantine: its purpose-`16`/`17` authorities sign identity/nonadmissibility, while the selected AD-31 or broker storage authority signs purpose-`18` retention. Their body/header hash is `SHA256("HX-EV-PHYSICAL-CARRIER-1\0" || 01 || B(exact complete transport body) || u32 headerCount ||` each header in original broker order as `B(exact name) || B(exact value))`; preserve duplicate headers and original casing, and require the exact length/count limits or a broker streaming/full-byte quarantine capability. `HX-EV-PHYSICAL-IDENTITY-1\0 || 01 || 0009` has `01..04` U component/topic/subscription/stable physical delivery ID, `05` B32 complete carrier hash, `06` N broker immutable delivery-log generation, `07` B exact tagged authority reference from receipt tag `05` (`00` AD-31 capture key/proof or `01` broker immutable full-byte reference/proof) and `08` Q first-observed broker UTC and `09` B32 exact signed capture-scope claim hash. The distinct purpose-`16` broker delivery authority signs it only after it verifies the selected authority's exact reference/item-index proof and immutable delivery-log CAS/ETag readback; that readback proves the ID and complete bytes cannot change across attempt IDs. An AD-31 proof need not be broker-owned, but its exact scoped bytes and item/index generation must be authenticated to the broker signer before purpose-`16` issuance. `HX-EV-PHYSICAL-NONADMISSIBLE-1\0 || 01 || 0009` has `01` B32 identity-record hash, `02` N exact historical subscription/configuration revision, `03` B32 authenticated historical route-catalog/configuration hash, `04` B32 exact carrier hash, `05` U closed permanent parse/nonadmissibility reason, `06` B32 empty-addressable-route proof hash, `07` B exact broker no-route/reject fence receipt, `08` Q fence UTC and `09` U broker authority ID. Distinct purpose `17` signs it; the authority independently verifies the historical configuration, permanent reason, no possible addressed route and linearizable fence preventing later delivery of those bytes to a route. A parser failure alone cannot establish this. `HX-EV-PHYSICAL-RETENTION-1\0 || 01 || 0009` has `01` B32 identity-record hash, `02` B exact same tagged authority reference as identity tag `07` and receipt tag `05`, `03` B32 complete carrier hash, `04` N full retained byte length, `05` Q retention-through UTC, `06` B32 initial post-CAS storage generation/ETag receipt hash, `07` U retention authority ID, `08` B32 exact full-byte readback receipt hash and `09` B32 exact storage obligation-fence record-and-receipt hash; purpose `18` signs it. Each complete record/signature carrier is ≤16 KiB and uses draft §6's pinned issuer/SPKI, purpose, validity interval and revocation checks. C4 quarantine tag `05` names this exact reference/AD-31 authority, tag `06` hashes the complete signed retention record plus full-byte readback receipt, tag `08` equals identity tag `04`, and tag `09` is the domain-separated `PhysicalNonadmissibilityHash` above, computed after the complete signed nonadmissibility record and fence receipt verify; the receipt also binds the identity-record hash through both sources. Read back all three exact source records, current historical trust/revocation state, immutable broker log/reference, complete body and each header **byte-for-byte**, retained horizon and the receipt under one stable CAS key before 2xx. Missing signatures, revoked authority, lost reference, shorter horizon, an ETag change without its authenticated purpose-`18`-anchored version/CAS receipt, or merely matching hashes hold non-2xx. Retain the signed records and bytes for the longest retry, rollback or incident obligation.

For physical source and receipt references, `PhysicalIdentityHash = SHA256("HX-EV-PHYSICAL-IDENTITY-HASH-1\0" || 01 || B(exact purpose-16 claim) || B(exact purpose-16 carrier) || B(exact immutable broker-log readback receipt))`; `PhysicalNonadmissibilityHash = SHA256("HX-EV-PHYSICAL-NONADMISSIBLE-HASH-1\0" || 01 || B(exact purpose-17 claim) || B(exact purpose-17 carrier) || B(exact no-route/reject-fence receipt))`; `PhysicalQuarantineReceiptHash = SHA256("HX-EV-PHYSICAL-QUARANTINE-HASH-1\0" || 01 || B(exact nine-tag quarantine receipt) || B(exact authenticated receipt CAS readback))`. Receipt tag `06` is `SHA256("HX-EV-PHYSICAL-RETENTION-READBACK-1\0" || 01 || B(exact purpose-18 claim) || B(exact purpose-18 carrier) || B(exact **initial** full-byte readback receipt) || B(exact **initial** physical-fence CAS receipt))`, and tag `09` is `PhysicalNonadmissibilityHash`; later physical-version heads are verified separately on each 2xx and never rewrite immutable receipt tag `06`. Purpose-17 tag `01` and purpose-18 tag `01` are `PhysicalIdentityHash`. These inputs are authenticated in that order, with the exact complete carrier/header bytes and broker/storage readbacks verified before digest comparison. The physical quarantine receipt uses a create-once CAS under the stable physical key; its CAS receipt uses the common pre-CAS installed-record hash rule. No bare source digest, unsigned carrier or unverified ETag can authorize 2xx.

For an AD-31-only physical capture, tag `05` of the quarantine receipt, identity tag `07`, retention tag `02` and obligation-fence tag `02` all carry the **same** `00` tagged AD-31 key/item-index proof; no broker full-byte reference ID is required. The broker still signs the immutable physical delivery ID, complete byte/header hash, historical no-route proof and no-future-route fence from its own delivery log. Purpose-`18` is signed by the configured AD-31 storage authority, with the initial post-CAS generation/ETag receipt hash in tag `06`, exact AD-31 item/index readback-receipt hash in tag `08`, and the initial AD-31 no-expiry obligation record/receipt in tag `09`. Before 2xx, resolve the key only inside the signed purpose-`1b` isolated scope, decrypt and compare the **complete** retained transport bytes and ordered headers to broker source bytes, verify item/index proof, generation, ETag and fence in one authority-specific readback; a hash match without full bytes holds. This branch admits only a carrier fitting the 128 MiB AD-31 raw cap and exact protected-output/header budgets. For a larger carrier, the `01` broker-reference branch remains mandatory, with purpose-`18` and the obligation fence signed/read back by the broker storage authority. A mixed discriminator, missing authority-specific trust map, cross-scope reference, shorter retention or unsupported atomic storage fence holds. Both branches use the same stable physical CAS key, signed nonadmissibility record and receipt bound; neither grants an addressed route result.

The initial physical fence is `HX-EV-PHYSICAL-OBLIGATION-FENCE-1\0 || 01 || 000b` (≤16 KiB): `01` U signed capture scope ID, `02` B exact tagged authority reference from identity tag `07`, `03` B32 physical identity-record hash, `04` B32 complete carrier hash, `05` O(N) expected prior storage generation, `06` O(U) expected prior ETag (both absent only on create), `07` B exact complete canonical open-obligation list using C3's `HX-EV-CAPTURE-OBLIGATIONS-1` and entry/closure codecs, `08` U `NoExpiryUntilClosure`, `09` Q fence-install UTC, `0a` U storage authority ID and `0b` B32 partition/access-policy hash. The stable key is `retained-object-obligation:` plus lowercase-hex `SHA256("HX-EV-RETAINED-OBJECT-KEY-1\0" || 01 || U authenticated storage partition || U storage authority ID || u8 authority tag || U canonical physical object ID)` using C3's exact canonical-ID authority mapping; the tagged reference proof, signed capture scope and stable physical delivery ID are verified **values** at that one object key, never key components; the identity hash is an immutable value at that key, so changing identity bytes cannot create an alternate fence branch. The initial list includes separate exact retry, delayed-delivery, rollback, backup and incident entries with their signed opening sources; a broker no-future-delivery proof is necessary for their eventual closure. A second delivery or refreshed AD-31 item/index proof for this same physical object joins this head and extends its union before physical quarantine; a changed proof without a verified next CAS version conflicts. Purpose-18 tag `09`, the physical identity and quarantine receipt resolve this same canonical ID/head. A signed canonical-ID mapping is mandatory before any alias can name an already fenced object; it supplies the same canonical partition, authority ID, tag and object ID for key derivation regardless of the alias issuer. Independently fenced distinct IDs may carry equal bytes. The storage authority atomically installs the fence on the tagged AD-31 item/index or broker full-byte object and persists C3's common authenticated post-CAS receipt under the exact stable key and operation ID. This is the same key and CAS head used for addressed capture. If an addressed initial set already owns that head, a second physical initial fence conflicts; a signed, atomic cross-kind extension preserving the complete existing obligation union is required or physical quarantine holds. The inverse transition follows the same rule. A proof refresh can advance only this shared head and cannot create a second addressed or unidentified initial fence. The receipt, **not a pre-CAS record field**, contains resulting generation/ETag. Purpose-`18` retention tag `09` is `PhysicalFenceHash = SHA256("HX-EV-PHYSICAL-FENCE-HASH-1\0" || 01 || B(exact initial fence record) || B(exact initial post-CAS receipt))`; the signed purpose-`18` source and immutable full-byte reference anchor all later versions. If the provider cannot persist/query the post-CAS receipt atomically with the object CAS, physical quarantine holds before 2xx.

Every update appends `HX-EV-PHYSICAL-OBLIGATION-VERSION-1\0 || 01 || 000d` (≤16 KiB) under the stable physical key plus one-based contiguous version: `01` U signed capture scope, `02` B exact tagged reference, `03` B32 physical identity-record hash, `04` B32 exact initial signed purpose-`18` retention-source hash, `05` N version, `06` B32 immediate predecessor hash (initial fence hash at version one), `07` N expected prior generation, `08` U expected prior ETag, `09` B complete current canonical open-list, `0a` B exact signed closure records/source receipts for removed entries, sorted by the closed entry hash, `0b` B32 complete carrier hash, `0c` U `NoExpiryUntilClosure` and `0d` Q update UTC. The same storage CAS advances the current physical-fence head, installs this version, keeps full retained bytes and persists the common post-CAS receipt with resulting generation/ETag; the next version consumes those exact compare values. Define `PhysicalVersionHash = SHA256("HX-EV-PHYSICAL-VERSION-HASH-1\0" || 01 || B(exact version record) || B(exact version post-CAS receipt))` and `PhysicalRetentionSourceHash = SHA256("HX-EV-PHYSICAL-RETENTION-HASH-1\0" || 01 || B(exact signed purpose-18 claim) || B(exact purpose-18 carrier))`; version tag `04` uses the latter, and tag `06` uses the preceding `PhysicalVersionHash` (or `PhysicalFenceHash` at version one). The version verifies the purpose-`18` source's initial fence/receipt and every predecessor, opening source, individual closure and generation/ETag receipt; no update can replace the initial signed source, drop an unclosed entry or change scope, reference, carrier bytes or partition. A closure record may remove one open entry only after independent signed completion and broker no-future-duty proof; a root alone never proves removal. The storage authority readback returns the complete initial source/fence and **all** versions through the current head, current no-expiry state and all body/header bytes in one object generation. Every original and duplicate physical-quarantine 2xx repeats this full verification and the purpose-`16/17/18` source checks. A changed generation without its version/CAS receipt, missing bytes, stale or expiring fence, chain gap, changed scope or unproved removal holds non-2xx; the original purpose-`18` proof remains anchored and verifiable after a legitimate update. The backend blocks expiration, deletion, key retirement and quota refund until independently authenticated closure of every open duty and no future acceptance.

A finite retention-through UTC in the signed source is a lower bound, never permission to delete. Before 2xx the storage authority CAS-installs an authenticated obligation fence keyed by signed capture scope and exact tagged authority reference: it blocks expiration, deletion, quota refund and key retirement while **any** retry, delayed-delivery, rollback, backup or incident obligation is open. The signed purpose-18 source's tag `09` binds the exact initial fence record and post-CAS receipt, including its resulting generation/ETag, full initial open entries and no-expiry rule. Every readback traverses the purpose-`18`-anchored version chain and verifies both full bytes and the current active fence under the same tagged reference. A finite expiry path is permitted only with a signed closed maximum horizon for every obligation, a storage-enforced expiry strictly later than that horizon and a broker proof that no new obligation can arise; otherwise the active fence remains nonexpiring until independently authenticated closure. Renewal intent or a calendar date alone never grants 2xx. Deletion requires a final fenced CAS proving all obligations closed and no future acceptance, then authenticated absence; a previously acknowledged poison blob cannot expire while an obligation is open.

## C5. A8 public outcome and terminal `PublishFailed`

Import 6.5a A8's **codec-03** `HX-EV-COMMAND-OUTCOME-1\0` (not draft codec 01): its twelve tag types, `pending/published/failed/unknown/not-applicable`, tag `08` complete publication-set hash for every eventful outcome and absent only for no-op. Exactly one destination per committed event, including rejection events; the expected set is sorted `(ordinal, MessageId, destinationId, exact outbox-intent hash, exact pin hash)`. `HX-EV-PUBLICATION-SET-1\0 || 01 || 0006` binds every observation row under one coordinator fence; ≤1,000 V1 or 256 V2 members, ≤2 MiB complete set, ≤64 MiB distinct referenced receipt/proof evidence. Reserve worst-case complete-set capacity **before** A4 Prepared/append. Accepted rows remain accepted and are never resent; unknown broker attempts reconcile before retry; failed is a definitive **attempt** outcome and may move to higher admitted attempt. Reduce all accepted→published; otherwise any unknown→unknown; otherwise any pending→pending; otherwise failed. A no-op is `not-applicable` with no publication. A partial accepted/pending/failed set never yields published.

A8's authenticated zero-event no-op is a separate branch **before** retry-policy creation. Its A7 no-op witness and immutable append/result/response evidence prove the empty event set; it has no A8 publication members or set, retry policy, outbox, pin, member send ID, broker acceptance or C5 terminal decision. Public outcome remains `not-applicable` with A8's original no-op response bytes. A retry reads the same no-op witness and immutable public response pin without constructing an eventful policy; any publication or terminal evidence beside that witness is contradictory and holds.

For an eventful batch only, before A4 publishes `Prepared`, the admission owner CAS-creates and authenticates the exact signed `HX-EV-RETRY-POLICY-1\0 || 01 || 000e` at `retry-policy:` plus A5 ScopeOpHash: ordered `01` U configured issuer ID, `02` U original command OperationId, `03` B32 ScopeOpHash, `04` B32 pre-policy member-plan hash, `05` U tenant, `06` U domain, `07` B32 ordered destination-scope hash, `08` B32 exact destination-configuration-set hash, `09` N member count, `0a` B member-policy rows, `0b` B reason rules, `0c..0d` Q issued/expiry UTC and `0e` U policy version. Member rows are `u32 count ||` exactly count rows in A8 member-position order, each `u32 position || B32 SHA256(U full MessageId) || B32 SHA256(U full destinationId) || B32 exact destination-configuration hash || u8 maximumAttempts`; positions are contiguous from 1, count is 1..1,000 (≤256 V2), and every maximum is 1..64. Reason rules are `u16 count ||` 1..64 unique UTF-8-ordinal sorted `U closedReasonCode || u8 class || O(U sourceCodecId)`, with each reason code ≤48 UTF-8 bytes and the complete rule block ≤4 KiB, where classes are exactly `01` retryable, `02` definitive terminal rejection, `03` zero-attempt permanent nonadmissibility. The source codec ID is absent for classes 01/02 and present for class 03; it names one immutable, installed reason-specific decoder and authority, not a mutable registry lookup. No default class or unknown reason is allowed; the signed policy fixes the exact classifier and maximum for each member. Complete claim/signature carrier is ≤128 KiB, and rows, reason rules and expected-set full identifiers are premeasured before allocation. Draft §6's carrier uses distinct purpose `19` and the admitted destination policy authority's pinned issuer/SPKI, signing interval and revocation state. `PrePolicyPlanHash = SHA256("HX-EV-RETRY-PLAN-1\0" || 01 || u32 memberCount ||` each row `I original event ordinal || U full MessageId || U component || U topic || U full destinationId || B32 exact destination-configuration hash)` in event-ordinal order. Generate and freeze this ≤8 MiB exact plan from A4's original command/event allocation and immutable destination configuration **before** constructing any outbox intent or global pin; the policy rows and tag `08` destination-configuration-set hash derive only from it. `DestinationScopeHash = SHA256("HX-EV-RETRY-SCOPES-1\0" || 01 || u32 memberCount ||` each planned row's `I original event ordinal || U component || U topic || U full destinationId)`; it is policy tag `07`. `DestinationConfigurationSetHash = SHA256("HX-EV-RETRY-DESTINATIONS-1\0" || 01 || u32 memberCount ||` each planned row's `I original event ordinal || B32 exact destination-configuration hash)` in that same order; it is policy tag `08`. The plan contains no policy, outbox or pin hash, so signing it cannot depend on bytes that later bind the policy. `RetryPolicyHash = SHA256("HX-EV-RETRY-POLICY-HASH-1\0" || 01 || B(exact complete policy claim) || B(exact purpose-19 signature carrier))`. The A4 complete preparation capsule retains the signed policy and plan bytes; each A5 outbox intent's immutable routing-policy bytes then bind `RetryPolicyHash`. A8's expected set and every later pin must match the plan's full identifiers, event ordinal and destination configuration, and its outbox hash must carry that same `RetryPolicyHash`. Read back all three exact bindings before `Prepared`, every send and every terminal/status verification. A changed policy, missing authority or expired/revoked signing trust holds; current retry settings never amend it. `Rejected` receipts carry the closed reason code and signed broker authority. A failed member may terminalize only with a definitive class-`02` Rejected receipt (possibly before its maximum) or the class-`03` zero-attempt nonadmissibility proof; class-`01` retryable failures remain open even at the maximum. No attempts follow Accepted or a definitive terminal rejection. Exhaustion means the signed maximum was reached **and** the final definitive receipt is class `02`; a count alone, policy ID or hash never grants terminal failure. The policy and all complete receipts are retained and verified through the operation's rollback/incident horizon.

Resolve A8's public terminal mapping as follows. A private `failed` head **alone** cannot produce `CommandStatus.PublishFailed`: a failed member may retry. Permanent public failure requires the immutable provider/broker-authenticated `HX-EV-PUBLICATION-TERMINAL-1\0 || 01 || 000b` **proposal**: ordered `01` U original OperationId, `02` B32 committed batch-root hash, `03` B32 exact final publication-set hash, `04` B32 exact A8 final failed outcome-head hash, `05` N final head revision, `06` B exact coordinator fence, `07` B32 authenticated all-destination queue/outbox drain and no-in-flight-send proof hash, `08` B32 authenticated no-future-acceptance and fenced-operation proof hash, `09` B32 fixed policy/exhaustion decision hash, `0a` Q decision UTC, `0b` U support-safe failure reason code. The coordinator first CAS-reserves one unsigned eleven-tag proposal, including its decision UTC, at `publication-terminal-reservation:` plus lowercase-hex `SHA256("HX-EV-TERMINAL-BRANCH-1\0" || 01 || U authenticated tenant || B32 ScopeOpHash || B32 exact final failed A8 head hash)`. The reservation is backend-authenticated, bound to the exact head hash/revision/ETag and final proof hashes, and read back before signing. `TerminalProposalUnsignedHash = SHA256("HX-EV-TERMINAL-PROPOSAL-UNSIGNED-1\0" || 01 || B(exact reserved unsigned eleven-tag proposal))`. The create-once signed proposal key is `publication-terminal-proposal:` plus lowercase-hex `SHA256("HX-EV-TERMINAL-PROPOSAL-KEY-1\0" || 01 || U authenticated tenant || B32 ScopeOpHash || B32 exact final failed head hash || B32 TerminalProposalUnsignedHash)`; no signature bytes enter key selection. A retry first reads the branch reservation and then the proposal key; it reuses the first reserved UTC and complete signed carrier. Different unsigned bytes at the same branch reservation, or another carrier at the same proposal key, conflict; a different final head remains an inert separate branch. Complete proposal ≤64 KiB and each canonical referenced proof ≤64 KiB; reserve the complete framed member proof and source records under A8's same 1 GiB operation quota before terminalization. At least one required member must be **nonaccepted** with definitive class-02 or proved class-03 terminal evidence; an all-Accepted set is `published` and any terminal-failure proposal for it is an evidence conflict/`CommandOutcomeHold`. Every accepted member remains true; terminal `PublishFailed` means at least one required member cannot publish under this permanently closed operation, never that no event stored or another member did not publish. If a broker cannot prove absence of future acceptance after an ambiguous send, terminality is unavailable. Contradictory later acceptance is an incident/evidence conflict and `CommandOutcomeHold`, never a silent status rewrite.

The terminal proposal is signed under distinct purpose `29` by the configured command-outcome closure authority named in the pre-Prepared coordinator configuration, never by the broker's generic, event, membership or policy key. Its draft §6 P-256/P1363 signature carrier contains the exact proposal bytes; the issuer ID/SPKI, purpose, tenant/ScopeOpHash, key interval, signed proposal tag-`0a` UTC, authoritative broker UTC at decision and current revocation are verified against the pinned accepted command trust map. A proposal issued outside `[notBefore,notAfter)`, after revocation, with a missing carrier or a mismatched final failed head is inert. `TerminalProposalCarrierHash = SHA256("HX-EV-TERMINAL-PROPOSAL-CARRIER-1\0" || 01 || B(exact eleven-tag proposal) || B(exact purpose-29 signature carrier))`; pointer tag `07` is **exactly** this hash after full claim, carrier and trust verification. The carrier hash is used **only as a verified pointer value**, never as proposal key material. The first complete proposal/carrier and a provider-authenticated CAS receipt commit atomically under the unsigned branch key; the receipt names the pre-CAS proposal/carrier bytes hash and resulting ETag. A lost acknowledgement reads the reserved unsigned proposal, first carrier and its CAS receipt, reusing their UTC and exact bytes without re-signing. Any second carrier, even a valid signature over the same unsigned proposal, conflicts at that key. The immutable reservation, proposal, carrier and CAS receipt are retained with the pointer. A valid proposal remains nonpublic until the atomic failed-head pointer CAS.

The **authoritative** key is `publication-terminal-current:` plus B32 A5 ScopeOpHash. Its immutable `HX-EV-PUBLICATION-TERMINAL-POINTER-1\0 || 01 || 000a` (≤16 KiB) is ordered `01` U authenticated tenant, `02` U original OperationId, `03` B32 ScopeOpHash, `04` B32 exact final failed A8 head hash, `05` N final head revision, `06` U exact final head ETag, `07` B32 exact first retained proposal-and-signature-carrier hash, `08` B32 exact broker reject-fence receipt hash, `09` B32 `TerminalProofRoot` computed by the exact formula below and `0a` Q proposal tag-`0a` decision UTC. Compute the proposal hash from complete signed bytes; the pointer carries no independently sampled timestamp. After C5's ordered broker fence and final empty observation, authenticate/read back the complete proposal and all member/segment/source proofs, then **atomically** CAS-create/read back this pointer with a terminal-closure index under the same A8 head authority, conditional on the exact final failed head hash/ETag/revision, unchanged broker fence generation and absent closure. Every A8 successor-head writer must check that closure index in its own head CAS; once present, no further transition of this operation is allowed. A failed CAS leaves only an inert proposal; a competing head may create a different proposal and close only from its own final authenticated head. Lost pointer/CAS acknowledgement reads the pointer, closure index, head and CAS receipt and succeeds only when all exact bytes, fence and proof root match. Different existing pointer bytes, a stale proposal, missing atomic multi-key provider capability or uncertain head/fence holds; public status never observes a proposal alone. Retain proposal, pointer, CAS receipt, final head/clock and all referenced proof bytes and keys through status, retry, rollback and incident obligations.

Only with that proof may authorized status inspection project latest private failed to the **existing** `CommandStatusResponse`. Pin every field from authenticated committed source and final head: `CorrelationId`, `MessageId`, `AggregateId`, `EventCount`, `RejectionEventType` and `TimeoutDuration` retain their original immutable record values (including null versus value); `TenantId` is the original authorized tenant scope, never a caller override; `Status="PublishFailed"`, `StatusCode=6`, `Timestamp` is proposal tag-`0a` decision UTC authenticated directly by the terminal pointer tag `0a` and the exact final A8 head, `FailureReason` is the redacted terminal reason, `Retryable=false`, `RecoveryReasonCode="PublishFailed"`, and `DrainAttemptCount` is the final authenticated count or null when none is proven. For nonterminal revisions, `Timestamp` comes only from the immutable authenticated revision clock below and optional original metadata remains unchanged; a private failed revision is withheld from status inspection except for the separately authenticated `PublicationRetryExhaustedHold` projection specified below. No `Retry-After` header is sent for terminal status, matching existing `IsTerminal()`. Missing/ambiguous terminal pointer/proposal, key, final head, member receipt or no-future-acceptance proof returns `CommandOutcomeHold`, never status 6 or a fabricated nonterminal status. The terminal decision cannot transition to pending/published under the same operation; separately approved recovery needs a new execution identity and preserves original bytes. This adds no public enum, field or route. Pending/unknown use the existing nonterminal shape without `EventsPublished`/`Completed`.

Each A8 successor outcome revision, including a private `failed` observation, uses one `HX-EV-OUTCOME-CLOCK-1\0 || 01 || 0008` (≤4 KiB) with ordered `01` U original OperationId, `02` N revision, `03` B32 exact outcome-record hash, `04` B32 predecessor-head hash (zero at revision zero), `05` Q coordinator UTC observation instant, `06` U configured clock issuer, `07` B32 authenticated broker/observation source hash and `08` B32 exact successor-head hash. The accepted coordinator authority signs it under distinct purpose `1a`; verify issuer/SPKI, issuance interval, revocation and signed UTC source. Its reservation key is `outcome-clock-reservation:` plus ScopeOpHash, checked revision, predecessor-head hash and exact outcome-record hash, all framed with draft §4 `B32`/`N`; a reservation is branch-specific and is never public by itself. A same-branch retry uses the first signed reservation and UTC, even if its discarded clock sample differs; changed outcome, predecessor, successor head, issuer or source proof at the same key conflicts. A competing branch may hold a distinct inert reservation but cannot claim A8's occupied immutable outcome key.

For predecessor head revision `r`, execute this exact order under the A8 command fence. First read/authenticate the head and the **immutable** A8 outcome key for `r+1`. If that key already exists, authenticate its complete twelve-tag bytes, observation/receipt set and retained A8 predecessor/CAS-preparation evidence, then reconcile **that same** `r+1` outcome to the head from `r` (or verify its existing matching head transition) before considering any different observation. Its original signed clock reservation must match, and a finalization receipt must match if head CAS already occurred; if the record was created but the head CAS was lost, retry the same branch and same UTC. A different observation can be written only at `r+2` after this reconciliation. An occupied revision with changed bytes, absent preparation/clock, mismatched predecessor, invalid fence or unprovable CAS outcome is `CommandOutcomeHold`, never overwritten or skipped. If `r+1` is absent, retain A8's exact predecessor/CAS-preparation evidence under the command fence, derive the intended outcome bytes/hash and successor-head bytes, CAS-create/read back the branch-specific signed clock **reservation**, then CAS-create/read back the immutable A8 outcome at its fixed `r+1` key. A crash after reservation but before outcome leaves only an inert orphan; another valid branch may create `r+1` after checking the key. A crash after outcome creation obliges every branch to reconcile that existing outcome; an orphan reservation from another branch cannot block it.

Only after exact outcome and reservation readback does the successor-head CAS finalize the revision. It atomically checks the expected predecessor head ETag/fence/revision and issues `HX-EV-OUTCOME-CLOCK-FINAL-1\0 || 01 || 0007` with `01` B32 ScopeOpHash, `02` N revision, `03` B32 predecessor-head hash, `04` B32 outcome-record hash, `05` B32 exact signed clock-reservation record/carrier hash, `06` B32 exact successor-head hash and `07` U resulting head ETag. Retain the signed/backend-authenticated CAS receipt under the head generation. A failed CAS never finalizes an orphan and cannot publish its timestamp; after a lost acknowledgement, query the exact head and CAS receipt and return the pinned UTC only for the same outcome, predecessor and reservation. If the head is still `r`, retry finalization of the already-created `r+1` outcome under its valid fence. If the head already contains that transition, verify/read back and return it. If it moved differently or evidence is unavailable, hold; do not reserve or write a competing `r+1`. A later `r+2` needs its own predecessor evidence and signed clock. A provider unable to atomically issue/read back head CAS and clock binding cannot project that revision. A sweeper may reclaim an orphan reservation only after authenticated proof of no matching immutable outcome, head/CAS receipt or retry/rollback obligation. Status inspection verifies the committed A8 outcome/head, finalization receipt, signed reservation and predecessor chain before using tag `05` as its existing UTC `Timestamp`; terminal public status uses only the authenticated proposal/pointer decision time bound to the final head. No local wall-clock fallback exists.

The **first committed POST reply** remains 6.5a A8's immutable revision-zero pin and is publication-independent except its fixed `resultPayload` gate: only revision-zero `published` includes the payload. Revision-zero pending/unknown/failed accepted commands pin the same 202 `{correlationId,messageId}` application response; committed rejection pins existing ProblemDetails classified from immutable result bytes, never transient publication status. `HX-EV-RESPONSE-PREPARATION-1\0` proceeds NeverStarted→Rendering→Prepared→Pinned under fence, with one render and exact byte readback before first response. Later status revisions never replace that response; exact retry returns original status/application headers/body after current access checks. Never overwrite A5 domain/append result or first response pin with `PublishFailed`; failure after actor save is a publication observation, not proof of no commit. No-op returns its original no-op public shape without publication. Lost response, cancellation or ambiguous save reads back append, full observation, response preparation/pin and head under bounded recovery; missing authority holds without rerender or restaging.

Tags `07..09` of the terminal record hash three **separately retained authenticated canonical source records**, never operator assertions. Each uses draft §4 `U/B/B32/N/Q`, ordered tags, version byte, ≤64 KiB complete record and a purpose-specific broker/provider signature or backend-authenticated CAS/ETag under the exact OperationId and coordinator fence. `HX-EV-TERMINAL-DRAIN-1\0 || 01 || 0007` binds `01` U OperationId, `02` B32 final complete publication-set hash, `03` B **framed** complete member/last-attempt roster, `04` B32 combined authenticated outbox/queue empty-state and coordinator drain-ledger readback, `05` B32 in-flight send ledger closure, `06` B exact coordinator/backend fence (≤8 KiB), `07` Q final observation UTC. Tag `03` is `u32 totalMemberCount || B32 completeRosterHash || u32 segmentCount ||` sorted contiguous descriptors, each `u32 firstOrdinal || u32 count || B32 segmentHash`; count is 1..1,000, segment count 1..1,000, no gaps/overlap, and the descriptor list is ≤48 KiB. Each retained `HX-EV-TERMINAL-MEMBERS-1\0 || 01` segment is ≤64 KiB and contains its exact framed rows: `u32 rowCount` followed by ascending `N ordinal || B32 MessageIdHash || B32 destinationIdHash || B32 pinHash || N actualAttemptCount || O(B32) lastAttemptOperationIdHash || U outcome || O(B32) exact lastReceiptHash || O(B32) exact signed nonadmissibilityProofHash`. For zero attempts the two last-attempt fields are **absent** (`00`), the proof hash is present (`02` plus B32), and outcome is `failed`; with one or more attempts the last-attempt fields are present and the nonadmissibility proof hash is absent. Explicit null (`01`) is invalid in all three optional fields. The proof hash names the exact signed member nonadmissibility record below and is bound by both this roster row and the policy row. `actualAttemptCount` is 0..the signed member maximum; roster outcome is exactly `accepted` or `failed` at terminality and must match the final publication-set row. No `unknown` or `pending` member can terminalize. Deterministically take the longest contiguous prefix satisfying both the kind's row-count cap and the complete 64 KiB encoded-segment cap, then the next; one oversized row holds. Set `segmentHash = SHA256(exact complete segment bytes)` and `completeRosterHash = SHA256("HX-EV-TERMINAL-ROSTER-1\0" || 01 || u32 totalMemberCount || u32 segmentCount || B(each exact ordered segment))`. Compare every row to the final publication set and every exact Accepted/rejected/exhausted definitive C2 result observation; the lastReceiptHash is C2 `ResultObservationHash` of that exact definitive observation/carrier, and the complete immutable observation chain and broker head must verify. The original A8 complete publication set remains ≤2 MiB; the separately retained segments and receipt images are charged under its ≤64 MiB referenced-evidence bound. The immutable signed retry policy fixes 1..64 attempts per member, with ≤1,000 members (≤256 V2) and ≤64,000 attempted sends. Before A4 Prepared, reserve the policy's **worst-case** two observations per attempt, chain head, segment, descriptor and roster bytes under A8's 64 MiB referenced-evidence and 1 GiB operation quotas; if the provider's exact receipt bound will not fit, lower and sign the retry maximum before Prepared or hold admission. It cannot grow afterward. Every roster/attempt/policy row is ≤512 bytes and each scoped segment header (OperationId hash, final-set hash, segment ordinal and row count) is ≤1 KiB. Deterministically fill each ≤64 KiB segment with the longest contiguous sorted row prefix satisfying its row-count and complete encoded-byte caps. A row or segment over its cap holds before the attempt or terminal decision that would exceed it. CAS-create segments under the same OperationId/final set/fence, authenticate exact backend ETag/readback and compare complete bytes to the signed root's descriptor hashes. At terminal CAS and each status inspection, verify **all** exact segments and referenced receipts. Missing, partial, ambiguous or over-budget evidence is `CommandOutcomeHold`, never a truncated proof.

`DrainAttemptCount` counts **coordinator drain invocations**, never broker publication sends or C2 nonce attempts. Before each actual drain invocation, the coordinator CAS-appends `HX-EV-COORDINATOR-DRAIN-ATTEMPT-1\0 || 01 || 0008` (≤16 KiB): `01` U authenticated tenant, `02` B32 ScopeOpHash, `03` U original OperationId, `04` N contiguous one-based drain ordinal, `05` U immutable drain invocation ID, `06` B32 predecessor drain-row hash (zero for first), `07` Q coordinator start UTC and `08` B32 exact coordinator fence/source hash. `DrainRowHash = SHA256("HX-EV-COORDINATOR-DRAIN-ROW-1\0" || 01 || B(exact backend-authenticated row))`. The row and indexed head generation commit atomically; invocation without durable registration is unsupported. A lost acknowledgement queries that exact invocation ID and cannot increment twice. The signed `HX-EV-COORDINATOR-DRAIN-HEAD-1\0 || 01 || 000a` (≤16 KiB) contains `01` U tenant, `02` B32 ScopeOpHash, `03` U OperationId, `04` O(N) count, `05` O(B32) latest row hash, `06` N coordinator index generation, `07` U resulting ETag, `08` B32 predecessor signed head hash, `09` Q coordinator UTC and `0a` U pinned issuer. Purpose `25` signs it under the accepted coordinator trust map, issuer/SPKI, interval and revocation rules. Before signing the head, the provider must preallocate and durably reserve its **resulting** ETag under the expected predecessor generation/ETag, then atomically CAS-install exactly that signed head and an immutable backend receipt naming its pre-CAS claim/carrier hash, expected compare values and same allocated result ETag. An unused allocation is inert; a mismatched allocated/actual ETag or provider without this atomic preallocation-and-sign capability holds. No signed head may guess or claim an unknown postcommit ETag. `DrainHeadHash = SHA256("HX-EV-COORDINATOR-DRAIN-HEAD-HASH-1\0" || 01 || B(exact signed purpose-25 head claim) || B(exact purpose-25 carrier) || B(exact atomic backend CAS receipt))`; head tag `08` is exactly the preceding generation's `DrainHeadHash` or 32 zero bytes for the first head. Read back and verify every predecessor claim, carrier and CAS receipt before using a count. Present count zero has present `O(N)` value `0` and absent latest row; present positive count matches all rows `1..count`. An **absent** `O(N)` and absent row hash mean explicit `null`, allowed only with a signed legacy/no-drain absence declaration and complete coordinator-index generation/ETag readback; it does not assert zero historical invocations. Explicit null and present zero have different exact bytes and status values. No invented count or head can be derived from broker attempt totals.

Explicit `null` additionally requires `HX-EV-COORDINATOR-DRAIN-ABSENCE-1\0 || 01 || 0008` (≤16 KiB): ordered `01` U tenant, `02` B32 ScopeOpHash, `03` U OperationId, `04` N complete coordinator-index generation, `05` U exact ETag, `06` Q observation UTC, `07` U literal `LegacyUninstrumented` or `NoDrainStarted`, and `08` U pinned coordinator issuer. Distinct purpose `26` signs the exact record under the accepted issuer/SPKI/interval/revocation rules; its backend CAS/readback proves the same index generation and the absence of registered drain rows. The purpose-`25` nullable head's absent count/row fields must identify the same tenant, operation, generation and ETag. `LegacyUninstrumented` represents an unknown historical count as `null`, never zero; `NoDrainStarted` is permitted only before any possible drain invocation and still projects `null` to preserve the existing no-drain shape. A present zero requires a different signed head with count zero, not an absence declaration. Missing trust, unreadable historical index or a row under an absence declaration holds.

The complete referenced drain evidence is one discriminated byte string: `00 || u32 rowCount ||` each ordinal-ordered `B(exact drain-attempt record) || B(exact backend CAS receipt)` for a present count, or `01 || B(exact purpose-26 absence record) || B(exact purpose-26 carrier) || B(exact coordinator-index absence receipt)` for explicit `null`. Present row count must equal the purpose-`25` head count, including the canonical zero-row `00 || u32(0)`; the absent variant requires absent count/row hash in that signed head. The terminal drain source's tag `04` is `SHA256("HX-EV-TERMINAL-DRAIN-READBACK-2\0" || 01 || B(exact outbox/queue empty-state receipt) || B(exact purpose-25 signed drain-head) || B(exact purpose-25 carrier) || B(exact discriminated drain evidence))`. Its final coordinator fence, index generation/ETag and UTC must agree with the tag-`05` in-flight closure and final status record; read back every drain row, head predecessor and backend receipt to prove the complete contiguous count. The terminal status record binds this exact head/evidence image and count. Status inspection projects `DrainAttemptCount` as the present count (including zero) or `null` from the authenticated absent variant, preserving its existing nullable integer shape. A count beyond `Int32.MaxValue`, missing row, changed head, absent declaration with a row or unavailable coordinator authority is `CommandOutcomeHold`. Admission reserves the configuration's maximum drain rows and receipts within A8's referenced-evidence/operation quotas before Prepared; after the reserved limit, another drain attempt holds rather than truncating history or inventing a count. Retain the complete source through status, retry, rollback and incident obligations.

`HX-EV-TERMINAL-NO-ACCEPT-1\0 || 01 || 0007` binds `01` U OperationId, `02` B32 final set, `03` B complete attempt-set descriptor, `04` B exact broker durable reject-fence receipt (≤16 KiB), `05` B exact producer/coordinator attempt-disable receipt (≤16 KiB), `06` B32 drain-record hash and `07` Q fence UTC. Tag `03` is `u32 totalAttemptCount || B32 completeAttemptSetHash || u32 segmentCount ||` contiguous descriptors `u32 firstFlatOrdinal || u32 rowCount || B32 segmentHash`; ≤512 segments, ≤24 KiB descriptors and 0..64,000 attempts. Zero attempts have zero segments and the canonical empty root; otherwise every segment is nonempty. Each `HX-EV-TERMINAL-ATTEMPTS-1\0 || 01` segment contains its scoped header and `u32 rowCount` followed by ≤512-byte rows sorted uniquely by `(memberOrdinal, attemptOrdinal)`: `u32 memberOrdinal || u32 attemptOrdinal || B32 brokerOperationIdHash || B exact 16-byte fenceNonce || B32 exactPinHash || U definitive outcome || B32 exactReceiptHash`. Member ordinals match the final set; attempt ordinals are contiguous `1..actualFinalAttemptOrdinal` for each member with attempts, where that ordinal is ≤ its immutable signed maximum of 1..64. A member may stop earlier after Accepted or proven terminal exhaustion; zero-attempt members have no attempt rows. Only authenticated `Accepted` or `Rejected` is definitive; `Unknown` prevents terminality. Within one send OperationId the broker operation identity and global pin remain unchanged across renewed nonces; C5 flat `attemptOrdinal` equals its C2 parent-member row tag-`0d` cumulative prior-attempt count plus the local per-send-ID ordinal, and every member-wide ordinal is contiguous across all signed send rows; a new send ID is allowed only by C2's signed same-parent retry chain after complete class-01 Rejected proof, and its original MessageId/pin cannot change. No attempt follows Accepted. `segmentHash = SHA256(exact complete segment bytes)`; `completeAttemptSetHash = SHA256("HX-EV-TERMINAL-ATTEMPT-SET-1\0" || 01 || u32 totalAttemptCount || u32 segmentCount || B(each exact ordered segment))`. Reconcile every row against **every** full parent-member send row, its purpose-23 signed head/predecessor generations, purpose-20/21/22 attempt/result/head chain and the exact definitive result observation for each indexed send ID. `exactReceiptHash` is C2 `ResultObservationHash` of that signed definitive result/carrier; a prior signed Unknown remains in the verified chain and cannot be silently discarded. An unresolved Unknown holds terminality. A zero-attempt member needs both the separately signed pre-send reason/source and post-fence complete zero-attempt ledger, bound by its nonadmissibility record. The broker fence still covers this OperationId and all current/future member send IDs on every accept path.

`HX-EV-TERMINAL-POLICY-1\0 || 01 || 0007` binds `01` U OperationId, `02` B32 final set, `03` B32 `RetryPolicyHash`, `04` B complete member-decision descriptor, `05` B32 drain hash, `06` B32 no-accept hash and `07` Q decision UTC. Tag `04` has the same framed descriptor form with `u32 memberCount || B32 completePolicySetHash || u32 segmentCount`; member count exactly matches the final set's sorted unique 1..1,000 (≤256 V2) members, with 1..8 segments and ≤1 KiB descriptors. Each `HX-EV-TERMINAL-POLICY-MEMBERS-1\0 || 01` segment contains its scoped header and ≤512-byte rows sorted uniquely by final-set member ordinal: `u32 memberOrdinal || B32 MessageIdHash || B32 terminalReasonHash || N actualAttemptCount || O(B32) exact final-attempt receipt hash || O(B32) exact signed nonadmissibilityProofHash`. For a zero-attempt member count is zero, final receipt is absent (`00`) and nonadmissibility proof hash is present (`02` plus B32); for any attempted member the final receipt is present and nonadmissibility proof absent. Explicit null is invalid. Counts/reasons match the complete attempt set and fixed signed policy; an Accepted member has exact terminal reason literal `Accepted` and its authenticated Accepted receipt, while every failed reason must occur in the signed rule table with the required class. A member ending below its signed attempt maximum requires an exact class-`02` terminal Rejected receipt or an Accepted receipt; the maximum is a ceiling, not a mandatory number of sends. `completePolicySetHash = SHA256("HX-EV-TERMINAL-POLICY-SET-1\0" || 01 || u32 memberCount || u32 segmentCount || B(each exact ordered segment))`. Retryable failures cannot satisfy exhaustion.

All three segment kinds use the **same exact complete record framing**, with only their ASCII NUL-terminated domain separator differing: `HX-EV-TERMINAL-MEMBERS-1\0`, `HX-EV-TERMINAL-ATTEMPTS-1\0` or `HX-EV-TERMINAL-POLICY-MEMBERS-1\0`. Exact bytes are `domainSeparator || 01 || u16(7) || 01 B32 OperationIdHash || 02 B32 finalPublicationSetHash || 03 B32 coordinatorFenceHash || 04 N zeroBasedSegmentIndex || 05 N firstOrdinal || 06 N rowCount || 07 B rowBlob`, with one-byte tag numbers shown, draft §4 scalar encodings, no extra bytes and complete record ≤65,536 bytes. `coordinatorFenceHash = SHA256(B(exact terminal coordinator fence))`; each segment's fence hash must match the terminal source records. The terminal member ordinal is the one-based **position** in A8's immutable expected set sorted by its original `I event ordinal`; it does not replace or renumber that original event ordinal. `firstOrdinal` is the first terminal member position for roster/policy, or the one-based flat ordinal in `(memberPosition, attemptOrdinal)` order for attempts. It matches its descriptor; segment indexes are contiguous from zero. `rowCount` is 1..1,000 for roster, 1..125 for attempts and 1..125 for policy, and also equals the descriptor count and the first `u32` in `rowBlob`. The remainder of `rowBlob` is exactly `rowCount` repetitions of `u32 rowByteLength || rowBytes`; `rowByteLength` is 1..512, fits within the declared `B` length, and `rowBytes` is **only** the ordered typed row fields given above, with no tags, padding, JSON, alternate integer widths or trailing bytes. Attempt and policy `u32` ordinals/counts use checked big-endian; roster's `N` uses signed big-endian i64 and must be positive. Canonically partition the sorted complete rows into the longest contiguous prefix whose **complete encoded segment** fits 65,536 bytes and whose row count stays within that kind's cap, then repeat; a row that cannot fit in a fresh segment holds. The ≤1 KiB scoped-header and 64 KiB segment reservations, existing per-kind descriptor caps and operation reservations still apply: `125 × (512 + 4) + 1,024 = 65,524 ≤ 65,536`, `64,000 / 125 = 512` attempt segments and `1,000 / 125 = 8` policy segments at the worst permitted row size. The complete encoded header, including all tags and the `B` length, consumes its actual bytes and must be ≤1,024. Each descriptor hash is SHA-256 of those exact complete segment bytes. Every set root's `B(each exact ordered segment)` means the concatenation of one draft §4 `B(segmentBytes)` per descriptor in order, including its length, so no serializer choice changes a hash. Zero attempts mean no attempt segment, count zero, and the root hashes the separator, version and two zero `u32` counts with no `B` values. CAS/readback compares the exact bytes before the final terminal root is trusted.

Terminal rows contain fixed-size identity hashes **only as references**, not as substitutes for complete identifiers. For every `B32 XHash` above, compute `SHA256(U(exact full identifier))` for MessageId, destinationId, member send/broker OperationId and terminal reason, and `SHA256(U(exact original command OperationId))` for the segment header. The terminal verifier independently reads the complete authenticated A8 expected set and outbox/pin images for both 1,024-byte MessageId and destinationId values, the C2 complete parent-member send-ID rows, CAS receipts, purpose-23 signed head generations and per-ID purpose-20/21/22 attempt/observation chains and definitive result observation for each send ID, and the signed retry-policy reason table for each failed reason code (or the fixed literal `Accepted` for an accepted member). It recomputes each hash from those **full bytes**, matches ordinal, parent, destination, pin, outcome, nonce and receipt, and rejects missing, multiple, conflicting or altered references. A digest alone cannot prove a member or attempt. The complete A8 expected set remains under its existing 2 MiB bound; the signed policy, all full-ID images, receipts and compact terminal segments are independently reserved before A4 Prepared under the existing 64 MiB referenced-evidence and 1 GiB operation budgets. Preflight uses the maximum 1,024-byte U fields and provider receipt bounds, so a valid committed member cannot encounter a new 512-byte terminal-row cap after append. If even the compact row or complete referenced evidence cannot be reserved, admission holds before Prepared, never drops a committed member. The full source records and identifiers outlive all open retry, rollback and status obligations.

The signed policy's class-`03` reason table is closed over exact reason-specific source codec IDs, verifier versions and authorities; each admitted reason binds its immutable destination configuration, actor source and the independently signed authoritative condition that made this member ineligible **before its first send**. `HX-EV-MEMBER-PRESEND-1\0 || 01 || 000c` is ordered `01` U broker/policy issuer, `02` U authenticated tenant, `03` U original OperationId, `04` N one-based member position, `05` U MessageId, `06` U destinationId, `07` B32 RetryPolicyHash, `08` U exact class-03 reason, `09` B32 immutable destination-configuration hash, `0a` B complete reason-specific source record and signature carrier (≤8 KiB), `0b` B32 broker pre-send zero-accept/zero-queued ledger proof hash and `0c` Q decision UTC. Purpose `1d` signs the complete ≤16 KiB record before any attempt under the accepted broker/policy authority. The verifier reads the full source record, its named rule decoder, the policy, destination configuration and broker ledger, and checks that the condition preceded all possible sends; unavailable or merely current configuration is insufficient. At terminal closure, after the producer disable and broker reject fence, the broker supplies a signed complete operation/send namespace ledger under the same tenant/ScopeOpHash showing zero accepted, zero queued, zero in-flight and zero attempted entries for this member across all destinations and nonce IDs, with generation/ETag and fence order. Its full bytes and readback, not a Boolean or terminal fence alone, yield tag `0c` of the nonadmissibility record. Any prior attempt, unknown ledger gap or source mismatch prevents a zero-attempt row.

The only base class-03 source codec admitted by this candidate is `HX-EV-PRESEND-DESTINATION-REVOKED-1\0 || 01 || 0009`, named exactly by the policy row. Its ordered tags are `01` U authenticated tenant, `02` U original OperationId, `03` N member position, `04` U destinationId, `05` B32 planned configuration hash, `06` B32 signed permanent destination-revocation decision hash, `07` Q revocation-effective UTC, `08` B32 broker no-admission capability/configuration hash and `09` U authoritative revocation issuer. The complete ≤8 KiB source and purpose-specific signature carrier must be issued by the destination policy authority before the first possible send; the verifier reads back the exact revocation decision/configuration and proves that the permanent revocation was effective at the pre-send decision. Other reason codecs require an approved extension to this candidate's closed table before a policy can name them. A temporary outage, unsupported current configuration or broker timeout has no class-03 source.

Class-`03` requires a **pre-send broker admission transaction**, not a later revocation inference.

A member whose final definitive result is class-`01` Rejected at its signed maximum has typed state `PublicationRetryExhaustedHold`, keyed by authenticated tenant/ScopeOpHash/member and the complete signed parent-member/attempt heads. It remains a private, nonterminal A8 `failed` observation under the original immutable first-response pin. For authorized status lookup, project the existing `EventsStored`/code-2 public shape from the authenticated committed batch and its last nonterminal UTC revision, with `Retryable=false`, `RecoveryReasonCode="publication_retry_exhausted_hold"`, null terminal-only `FailureReason` and the original `RejectionEventType`, `TimeoutDuration`, `TenantId` and every other nullable public metadata value (including null versus value); `false` here means no automatic publication attempt is armed, **not** a terminal command result. This explicit hold exception never projects status 6 (`PublishFailed`) or `published`. Status and support-safe diagnostics expose the hold code, member ordinal, maximum, exact final class-01 receipt hash and broker generation without payload or secret. Missing committed batch or last authenticated nonterminal UTC source is `CommandOutcomeHold` rather than an invented code-2 timestamp. Every status read reauthenticates the complete chain and hold source; missing proof is `CommandOutcomeHold`, not invisible exhaustion. No extra nonce/send ID, changed MessageId, inferred class-02 result or terminal closure follows merely from reaching the ceiling. The operator recovery gate under the **same original command identity** requires a separately approved signed policy/identity action, authenticated fencing of every old send/route obligation and proof that no old acceptance/effect can emerge before any explicitly authorized continuation. Until that action and readback exist, publication remains held indefinitely and replay of the committed command remains blocked. The current `CommandStatusController` emits `Retry-After: 1` for **every nonterminal** `EventsStored` result, including this hold with `Retryable=false`; the header is a one-second **status polling interval**, never authority to reissue publication, send a new ID or retry the command. The status body keeps the committed result's original rejection event type even for a rejection event. A future 6.6 controller/response change that suppresses or changes this header requires a compatibility gate for existing clients and tests proving the same hold remains pollable and cannot trigger publication; this candidate changes no runtime response behavior.

Before the first possible parent-member registration, enqueue or broker accept, after freezing the A8 member plan/policy and authenticating the reason-specific permanent source, the broker atomically installs a permanent rule keyed by `(U tenant, B32 ScopeOpHash, N member position)` in the same linearizable namespace used by every destination's parent-member CAS, enqueue and acceptance paths. This rule rejects all current/future send IDs, aliases, nonce attempts and mode conversions for that member. Its install CAS is conditional on complete zero Accepted, queued, in-flight, registered and parent-send-row entries **since the namespace's initial generation**, not merely the latest attempt. The producer cannot open a send path before this rule or a normal first send-row path wins the same CAS; if either send evidence exists, zero-attempt terminality is permanently unavailable. The exact signed `HX-EV-MEMBER-PRESEND-ADMISSION-1\0 || 01 || 000e` (≤16 KiB) contains ordered `01` U broker admission issuer, `02` U tenant, `03` B32 ScopeOpHash, `04` U original OperationId, `05` N member position, `06` U MessageId, `07` U destinationId, `08` B32 RetryPolicyHash, `09` B32 immutable destination/source configuration hash, `0a` N broker installation generation, `0b` U resulting broker ETag, `0c` B32 exact permanent rejection rule and reason-source hash, and `0d` B32 complete pre-send zero-ledger readback root and `0e` Q broker installation UTC. Tag `0c` is `SHA256("HX-EV-PRESEND-RULE-SOURCE-1\0" || 01 || B(exact permanent rejection rule) || B(exact signed reason-specific source))`; tag `0d` is `SHA256("HX-EV-PRESEND-ZERO-ROOT-1\0" || 01 || B(exact zero snapshot) || B(each ordered partition proof))`. The broker signs the admission record with distinct purpose `24` and pins its issuer/SPKI/trust domain to the accepted pre-Prepared destination configuration. Verify signed tag-`0e` broker installation UTC against the exact CAS receipt UTC and the issuer's `[notBefore,notAfter)` interval, broker UTC at install, current revocation, purpose, tenant/parent/member/destination and reason-specific source. Expiry after install requires retained historical trust and current nonrevocation; an unpinned or substituted broker signer is invalid.

The installed rule's exact bytes are `HX-EV-MEMBER-PRESEND-RULE-1\0 || 01 || 000a`: ordered `01` U tenant, `02` B32 ScopeOpHash, `03` U original OperationId, `04` N member position, `05` U MessageId, `06` U destinationId, `07` B32 RetryPolicyHash, `08` B32 immutable configuration hash, `09` B32 exact signed permanent reason-source hash and `0a` U literal `RejectAllFutureSends`; ≤16 KiB. Its member-namespace CAS receipt binds the complete rule bytes, install generation/ETag and the prior absent rule/send-row state; it is read back before purpose-`24` issuance. The complete partition manifest is `HX-EV-MEMBER-PRESEND-PARTITIONS-1\0 || 01 || u32 partitionCount ||` each sorted unique `U destinationId || U aliasId || N generation || U ETag || B32 acceptedEmptyRoot || B32 queuedEmptyRoot || B32 inFlightEmptyRoot || B32 registeredEmptyRoot`. The signed frozen destination configuration supplies exactly the partition/alias set, so a missing or extra row conflicts; `partitionCount` is positive and preflighted against the 64 MiB referenced-evidence budget. Each empty root is the broker's backend-authenticated complete index root at the rule-install generation, with zero-count readback and no omitted shard; byte-identical full manifest/readback, not only its digest, is required. The manifest hash in zero-snapshot tag `06` is `SHA256("HX-EV-MEMBER-PRESEND-PARTITIONS-HASH-1\0" || 01 || B(exact manifest))`.

The zero-ledger root hashes the exact broker-authenticated snapshot `HX-EV-MEMBER-PRESEND-ZERO-1\0 || 01 || u16(8)` with ordered `01` B32 same parent-member namespace hash, `02` N initial generation, `03` N installation generation, `04` U installed-rule ID, `05` U broker configuration ID, `06` B32 complete indexed destination/alias partition-root manifest, `07` B sixteen bytes containing four checked big-endian `u32(0)` counts in Accepted, queued, in-flight, registered order, and `08` U snapshot ETag. The manifest enumerates all destination partitions/aliases from the signed policy/configuration, with a signed completeness root; each partition has an exact empty-index CAS/ETag readback at the installation generation. Broker installation, zero snapshot and rule share one atomic generation/ETag, and admission/registration/enqueue consult the same installed rule before any duplicate or new-key path. Read back the **complete** manifest and all partition index proofs, exact rule bytes, reason-specific source, broker generation/ETag and signed record; a hash or zero counter without the complete authoritative index is insufficient. The purpose-`1d` pre-send decision tag `0b` is exactly `SHA256("HX-EV-PRESEND-ADMISSION-PROOF-1\0" || 01 || B(exact purpose-24 admission record) || B(exact purpose-24 carrier) || B(exact zero snapshot) || B(each ordered partition proof))`; its separate reason-source tag remains unchanged. All records are retained through terminal/status and rollback. A late revocation, missing partition, capability hash or terminal reject fence cannot retroactively establish this pre-send condition.

The post-fence ledger proof is the signed `HX-EV-MEMBER-NO-ATTEMPT-1\0 || 01 || 000c` record: `01` U authenticated tenant, `02` B32 ScopeOpHash, `03` U original OperationId, `04` N member position, `05` U MessageId, `06` U destinationId, `07` B32 complete broker parent-index head, `08` B32 producer-disable receipt hash, `09` B32 broker reject-fence receipt hash, `0a` B32 complete destination queue/outbox/in-flight empty-state readback root, `0b` N zero accepted/queued/attempted count and `0c` Q final broker observation UTC. The purpose-`1f` broker authority signs its complete ≤16 KiB bytes after the reject fence and final empty observation. The broker verifier reads the full append-only send namespace and attempt/receipt ledger for the tenant/parent/member through the fenced generation, checks the zero count and all destination queues, and recomputes the exact root; a count without that complete authenticated ledger is insufficient. For this zero-attempt branch tag `07` is exactly 32 zero bytes, accompanied by a complete authenticated absence readback of the parent-member chain namespace from its initial generation through the installed pre-send rule and final fence; a nonempty send row, merely missing head or index gap holds. The signed nonadmissibility record tag `0c` is `SHA256(B(exact no-attempt record) || B(exact purpose-1f carrier))`. The pre-send proof at tag `0b` is `SHA256(B(exact presend record) || B(exact purpose-1d carrier) || B(exact signed reason-specific source) || B(exact purpose-24 admission record and carrier) || B(exact broker zero snapshot and complete partition proofs))`. Verify both exact records and their source bytes against the final A8 set and policy on every public terminal read.

A zero-attempt reason is `HX-EV-MEMBER-NONADMISSIBLE-1\0 || 01 || 000c` with ordered `01` U configured issuer ID, `02` U original OperationId, `03` N final-set member ordinal, `04` U MessageId, `05` U destinationId, `06` B32 exact pin hash, `07` B32 `RetryPolicyHash`, `08` U closed nonadmissibility reason code, `09` Q decision UTC, `0a` B32 broker reject-fence receipt hash, `0b` B32 exact signed pre-send decision/source proof hash and `0c` B32 exact broker no-attempt ledger proof hash. Its ≤16 KiB exact bytes are signed in the draft §6 carrier with distinct purpose `15` by the accepted broker/policy authority; verify issuer, SPKI, signature, purpose, interval/revocation, matching tenant-scoped original operation/member/policy/fence, complete signed pre-send source and complete post-fence zero-attempt ledger readback. The record must prove that this member could not be admitted under the frozen policy before any send; an unavailable broker or unknown prior attempt is not such proof. Retain and read back the complete signed record under the A8 referenced-evidence budget. `SHA256("HX-EV-MEMBER-NONADMISSIBLE-PROOF-1\0" || 01 || B(exact record) || B(exact signature carrier))` is the nonadmissibilityProofHash in **both** segment rows; their descriptor hashes feed the drain/policy source hashes and final terminal root. Missing, mismatched or revoked proof prevents public terminality.

The pre-send admission, post-fence no-attempt and nonadmissibility records and their exact source images/receipts are verified under one fence/head; source hashes alone confer no authority.

The three source hashes and final root have one acyclic byte order. For each terminal source, `Auth(x)` is exactly `01 || B(exact purpose-specific signed carrier)` when the named issuer signs the complete source claim, or `02 || B(exact backend-authenticated post-CAS receipt)` when the provider is the named authority; the source record declares and pins that one variant before creation. Every backend CAS receipt names only `SHA256("HX-EV-TERMINAL-INSTALLED-1\0" || 01 || B(exact pre-CAS source claim))`, expected generation/ETag, resulting generation/ETag, authority and commit UTC; it never names its own source hash. `SourceCas(x)` is the exact authenticated source CAS receipt when `Auth(x)` selects a signed carrier and the empty byte string when `Auth(x)` is the provider CAS receipt; its enclosing `B` always appears once. `Evidence(x)` is `u32 count ||` one `B(exact authenticated item)` per item in the fixed order below; zero count has no items. The drain order is its exact roster descriptor, then each complete roster segment in descriptor order, then each segment CAS/readback receipt, then the final queue/outbox/in-flight readback, then exact purpose-25 drain-head claim/carrier/CAS receipt and every predecessor head/receipt in ascending generation, then each coordinator drain row/CAS receipt in ascending ordinal (or the purpose-26 absence claim/carrier/index receipt), then the exact final coordinator-fence receipt. The no-accept order is its exact attempt descriptor, then each complete attempt segment and CAS/readback receipt in descriptor order, then the exact broker reject-fence receipt, producer-disable receipt, complete signed send/attempt ledger and final empty-state readback. The policy order is its exact member-decision descriptor, then each complete policy segment and CAS/readback receipt in descriptor order, then exact signed retry-policy claim/carrier, each full member identifier/outbox/pin image and every definitive C2 result observation/carrier in member and attempt order, each zero-attempt pre-send source/carrier and post-fence ledger/carrier where applicable. Counts, order, complete bytes, signatures, ETags and full identifiers are mandatory; absent evidence holds.

`TerminalDrainSourceHash = SHA256("HX-EV-TERMINAL-DRAIN-SOURCE-1\0" || 01 || B(exact seven-tag drain claim) || B(Auth(drain)) || B(SourceCas(drain)) || B(Evidence(drain)))`. Drain claim tag `04` and its descriptor are verified before this hash. No-accept claim tag `06` is exactly `TerminalDrainSourceHash`; `TerminalNoAcceptSourceHash = SHA256("HX-EV-TERMINAL-NO-ACCEPT-SOURCE-1\0" || 01 || B(exact seven-tag no-accept claim) || B(Auth(no-accept)) || B(SourceCas(no-accept)) || B(Evidence(no-accept)))`. Policy claim tags `05..06` are exactly these drain/no-accept hashes; `TerminalPolicySourceHash = SHA256("HX-EV-TERMINAL-POLICY-SOURCE-1\0" || 01 || B(exact seven-tag policy claim) || B(Auth(policy)) || B(SourceCas(policy)) || B(Evidence(policy)))`. Proposal tags `07..09` are respectively these three full-source hashes. `TerminalProofRoot = SHA256("HX-EV-TERMINAL-PROOF-ROOT-1\0" || 01 || U authenticated tenant || B32 ScopeOpHash || B32 exact final failed A8 head hash || B32 exact final publication-set hash || B32 TerminalDrainSourceHash || B32 TerminalNoAcceptSourceHash || B32 TerminalPolicySourceHash || B(exact seven-tag drain claim) || B(Auth(drain)) || B(exact seven-tag no-accept claim) || B(Auth(no-accept)) || B(exact seven-tag policy claim) || B(Auth(policy)))`. The root consumes complete authenticated source records directly as well as their ordered segment evidence through the three hashes. Pointer tag `09` equals this root; pointer tag `07` equals the retained first proposal/carrier hash. Every proposal/pointer/status reader recomputes the full sequence from retained bytes, provider receipts and readbacks, with no writer-selected subset or postcommit ETag inside a pre-CAS claim.


Terminal closure has this required linearization order: (1) CAS-disable the producer/coordinator for the original OperationId under a durable fence, freezing the complete member/send-ID namespace and blocking new attempts; (2) atomically install and read back the broker reject fence for that **operation namespace**, so no future accept can succeed even with a delayed or newly presented ID; (3) query/reconcile every send that may have been accepted before the broker fence, retaining each Accepted receipt and definitive rejection, then drain all destination queues, outboxes and in-flight ledgers; (4) perform the **final** empty-state observation/readback after the broker fence, under the same fence generation, and build the drain/no-accept/policy records; (5) authenticate the final failed A8 head and its immutable clock/finalization receipt, CAS-create the branch-scoped terminal proposal, then atomically CAS the authoritative terminal pointer/closure index conditional on that exact head ETag and broker fence; public projection follows only pointer/head/proof readback. An accept between steps 1 and 2 is included in step 3; an accept after step 2 contradicts the broker fence and holds as an incident. Every source timestamp/ETag and broker fence generation is checked for this ordering; a stale empty observation cannot terminalize. A rejected fence installation, unknown send, changing queue, missing key or unproven closed delivery horizon is `CommandOutcomeHold`. The status reader authenticates the final head, terminal pointer and proposal, exact source records, at least one final nonaccepted member, every retained Accepted receipt and the ordered fence/empty evidence on each authorized lookup. Retry, status revision or rollback cannot un-fence the same OperationId or change the immutable first POST pin. Closure retains all required pin/key/source bytes until obligations end.

The live `ReplayController` includes `PublishFailed` in its replayable status set and resubmits the archived command with new correlation/MessageId. At the existing replay route, a terminal `PublishFailed` **must** pass a new authenticated replay-safety gate before mediator submission or ID allocation. Its default result is the existing 409 conflict shape with support-safe `ReplayPublicationConflict` reason: an eventful committed batch, and especially a partial Accepted set, must never be re-executed as a fresh command. A safe fresh-command replay requires independent authenticated proof that the old operation committed **no** domain event/result and no publication acceptance; C5's terminal `PublishFailed` has a committed batch root, so it cannot satisfy that proof. Recovery of a partial or zero-Accepted committed set uses a separately approved publication-resume operation that preserves original MessageIds, pins, accepted receipts and first POST bytes; it never invokes the archived command handler anew. Legacy status-6 records without the complete proof hold the same 409 gate until independently reconciled. Keep the public enum, route and response model; denial adds no success claim. A cancellation or lost reply after the gate cannot skip its proof.

## C6. Capability gates, rollout and package compatibility

The **entire** 6.5b ten-item integration handoff is a simultaneous activation checklist:

| Handoff | 6.5c gate / rollback disposition |
| --- | --- |
| 1. Current versus historical proof | Authenticate fresh purpose-10 source separately from A5 historic codec-02 complete save/provider images; valid retained V1 branch-02 and corrupt disposition remain distinct. Never require old commit ETag to equal today's head. |
| 2. Old writer/endpoint fence | Fence pre-existing V1 appenders at gateway **and actor**, reconcile in-flight old writes; new bounded evidence-writing V1 serves only after same-save/receipt probe. V1-only endpoints never receive V2 or mode-bearing work. |
| 3. Purpose-12 selector/trust | Install purpose-12 selector keys and revised TrustMapDigest verifier-first. Attest one new per-domain RegistryFingerprint across serving peers; restart stale paged operations at page 1. Retain historic fixture/pin/key obligations, including the distinct purpose-13 broker grant authority where an accepted route remains owed. |
| 4. Route bounds/capability | Pin B6 complete phase reservation and R/W/legacy-array/deployment budgets; 64 MiB event + 64 MiB prior state + positive work holds. Select only a proven bounded current-type route, including all multi-type capabilities. |
| 5. Retention/rollback | Keep source, purpose-01/02/06/10/11/12 event keys, purpose-13 historical broker-grant keys/trust intervals, purpose-14 catalog-filter, purpose-15 member-nonadmissibility, purpose-1b physical capture-scope, purpose-1c send-parent keys, purpose-20/21/22/23 attempt/result/attempt-head/member-head authorities, purpose-24 pre-send admission, purpose-25/26 drain head/absence, purpose-27 capture-closure, purpose-28 physical-filter delivery identity and purpose-29 terminal-proposal closure authorities, purpose-1d pre-send and purpose-1f no-attempt ledger keys/trust intervals, accepted-revision catalog predicate/input and transform bytes, old approved fingerprints, migration authority/decoder, backend evidence, pins, route decisions/reservation owner-fence histories/reconciliation proofs/receipts, broker-owned full-byte references or AD-31-only tagged references and both complete addressed-object and unidentified physical obligation fences with every version link, signed capture scopes, terminal proposals/pointers/closure CAS receipts, source records/segments/pre-send and zero-attempt proofs and response records through event/queue/retry/backup/rollback obligations. No capable endpoint → `RollbackReaderCapabilityHold`. |
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
| Retry and key rotation | C01/C02/C02b/C02c/C02d/C07/C11a/C11d | Crash after actor save/pin/send/accepted receipt; compare exact source, body, attestation, same-mode bytes, complete original ordered application/broker header image and six-header projection, purpose-1c and 20/21/22/23 issuer/trust intervals and current effective route. Query each nonce attempt and its complete signed observation chain plus every parent-member send-ID row/head; race two successors from one Rejected ID and reject omission, wrong purpose and stale head; an old Rejected remains Rejected after later acceptance, while unresolved Unknown holds. Exercise same-key duplicate non-routing headers with original order/casing preserved, a changed duplicate value, and case-fold-colliding `Content-Type`/CloudEvent/attestation headers; authorized Binary/Structured conversion pins each mode-specific `Content-Type` in its own complete image. Changed bytes under one tenant-scoped broker operation key conflict; equal send IDs in different tenants remain independent, while delayed/new member send IDs in the fenced tenant carry the original parent binding and fail its terminal fence; missing old trust holds. |
| Multiple routes | C03/C03b/C03c/C03d/C03e/C06/C11c | Two logical routes behind one physical subscription; crash after reservation and after first effect/receipt; exclude the old owner, query uncertain effect, then inspect one exclusive route-decision CAS and full-size effect receipt per route, full handoff, accepted-revision filter input and no broker ack until second receipt. Delay a filtered route beyond ordinary claim expiry with retained acceptance-time proof, then revoke its key and require hold. Leave/rename/provider replacement must drain or preserve the exact historical grant/key; race two replacements on the **old-obligation-scoped** mapping CAS, then query every uncertain old-provider effect and prove cross-provider exclusion before replacement invocation. Changed provider/endpoint bytes on the same stable key must conflict. Chain two takeovers and inspect tag-02 exact handoff/pin hash, every old owner/fence/ETag and reconciliation source through the final decision. |
| Legacy handoff | C04/C04b | Queued original JSON and old marker; compare exact source/route set/side record, whitespace-equivalent decoded value, each bounded exact attempt record and deterministic per-variant side key/link; crash between attempt, encrypted variant CAS and link, then require full body/core/header byte readback before receipt reuse, matching/conflicting/ambiguous prior binary pin and changed payload/header including a same-body changed-core/header retry. Crash before/after pointer and each route receipt; an empty historical set needs its purpose-28 signed Accepted-delivery-derived physical-filter OperationId and receipt; changed broker attempt ID must reuse the same create-once key; old physical JSON ack waits for the complete set; an addressed poison duplicate must recheck the shared capture-set fence. |
| Poison/capture | C01b/C04b/C05/C05b/C05c/C05d/C11b | Separate Binary/structured encoded, decoded, attestation and total header boundaries; physically feasible 128 MiB raw/protected-output side-record boundary, malformed/unidentified carrier, structured addressed poison above local AD-31 capture, changed full bytes at stable physical scope, lost blob/index readback and retry exhaustion. Prove purpose-1b isolated physical capture scope, purpose-16 identity, purpose-17 historical nonadmissibility/fence and purpose-18 active retention/obligation-fence source records against complete byte/header readback for both AD-31-only and broker-reference authority; no 200 without the post-handoff capture link, complete framed addressed open entries and independently authenticated closures, atomic post-CAS generation/ETag receipts and all addressed quarantine decisions, or the separate unidentified signed no-route physical quarantine receipt and its purpose-18-anchored complete physical version chain. Test two **handoffs** sharing one tagged retained object, object-wide union CAS, each handoff's complete route binds, added duty, closure of only one handoff, lost CAS acknowledgement, changed ETag without a version, and original/duplicate 2xx after version advance; all require exact full-byte retention. |
| Rollout/time | C01/C06/C09 | Equal instant/different offset Binary and structured paths; old endpoint joins after V2 append, purpose-12/key rotation, multi-replica capacity, same-revision configuration/probe/nonce mutation and lease-expiry acceptance race. |
| A8 failure/recovery | C08/C08a/C08b/C08c/C08d/C08e/C08f/C08g/C08h/C10/C11e/C11f/C11g/C11h | Empty eventful set, partial Accepted set, definitive rejection later retried, ordered producer/broker fences and final empty observation, signed pre-Prepared retry-policy and acyclic member plan, full 1,024-byte identifiers through compact terminal rows, A8 immutable r+1 outcome reconciliation, orphan-safe revision-clock reservation/head-CAS finalization/readback and rejection of a skipped A8 outcome revision before creation, segmented complete roster/attempt/policy proofs including the atomic signed pre-send rejection rule/zero-admission ledger and separate post-fence complete zero-attempt ledger, explicit zero-event no-op without policy, permanent closure and coordinator drain-ledger count/explicit-absence status projection; deny fresh ReplayController resubmission of a committed `PublishFailed`, including partial acceptance. Inspect exact first pin/head, purpose-29 signed branch-scoped terminal proposals and pointer tag-07 carrier hash, class-01 Rejected-at-maximum `PublicationRetryExhaustedHold` with stable nonterminal public status/operator gate, authoritative pointer/closure CAS readback, authenticated **failed** final A8 head, source records and broker reject fence; a published head with the same bytes/ETag must not close a terminal pointer. |

Models do **not** establish production provider support. Vectors must inspect actor/provider raw bytes and complete A5 generation images; global pin or authenticated full-byte tombstone comparison source; signed broker claims, complete request/accepted mode and header readback, receipt and physical ack; route effect/receipt counts; encrypted AD-31 capture/readback; first response pin/head; and last-good checkpoint after each injected crash. Run on the configured production provider and a second independent provider or conforming harness. Do not mark activation ready from a runtime build, old tests or local arithmetic.

### Executable local codec and state-model checks

The block reads the unchanged V17 body. C01 checks both `T` placements byte-for-byte; C01b and C04b check independent transport and side-record arithmetic. C02–C10 and focused C02b/C02c/C02d/C03b/C03c/C03d/C03e/C05b/C05c/C05d/C08a/C08b/C08c/C08d/C08e/C08f/C08g/C08h/C11a–C11h model decisions, framing and crash boundaries; injected auth/readback booleans stand for **unexecuted** provider evidence. These small models do not parse real signed claims, typed terminal rows or provider receipts, emulate provider transactions or prove broker linearizability.

```python
from pathlib import Path
import hashlib
import re
import json
import base64
import binascii

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
assert carrier_bound(1, 1, 4, 4 + 4 + 17, ((2, 1),), 'Structured', structured_overhead=17)  # Small valid body.
must_raise(Hold, lambda: carrier_bound(128 * MIB, 0, 0, 128 * MIB + 1, (), 'Binary'))
must_raise(Hold, lambda: carrier_bound(1, 1, 4, 5, (), 'Structured', structured_overhead=1))
must_raise(Hold, lambda: carrier_bound(1, 1, 4, 9, (), 'Other', structured_overhead=1))
must_raise(Hold, lambda: carrier_bound(1, 1, 4, 1, ((1, 8190),), 'Binary'))
must_raise(Hold, lambda: carrier_bound(1, 1, 4, 1, ((1, 1),) * 129, 'Binary'))
must_raise(Hold, lambda: carrier_bound(128 * MIB + 1, 0, 0, 128 * MIB + 1, (), 'Binary'))
print('C01b canonical mode, encoded/decoded relation and header-pair bounds passed')

ROUTING_HEADERS = (b'hx-tenant-id', b'hx-domain', b'hx-aggregate-id', b'hx-aggregate-type',
                   b'hx-event-contract-type', b'hx-payload-version')
MODE_FIELDS = {b'content-type', b'ce-hyevattestation'}
PROVEN_NONDECISION_HEADERS = {b'x-trace'}  # Stand-in for signed config + full-byte probe.
BINARY_MEDIA = b'application/vnd.hexalith.eventstore.v2+octet-stream'
STRUCTURED_MEDIA = b'application/cloudevents+json'
def broker_header_image(received, signed):
    if not 1 <= len(received) <= 128 or set(signed) != set(ROUTING_HEADERS): raise Hold()
    routing = []; canonical = {}; seen_decision = set(); seen_other = set()
    total = 0
    for name, value in received:
        if not isinstance(name, bytes) or not isinstance(value, bytes): raise Hold()
        if not name or any(c not in b"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-" for c in name): raise Hold()
        if len(name) + len(value) + 8 > 8192: raise Hold()
        total += len(name) + len(value) + 8
        lowered = name.lower()
        if lowered.startswith(b'hx-') and lowered not in ROUTING_HEADERS: raise Conflict()
        if lowered.startswith(b'ce-') and lowered not in {b'ce-specversion', b'ce-id', b'ce-type',
            b'ce-source', b'ce-subject', b'ce-time', b'ce-datacontenttype', b'ce-hyevattestation'}:
            raise Conflict()
        if lowered in ROUTING_HEADERS or lowered in MODE_FIELDS or lowered.startswith(b'ce-'):
            if lowered in seen_decision: raise Conflict()
            seen_decision.add(lowered)
        elif lowered not in PROVEN_NONDECISION_HEADERS: raise Hold()  # Unique names also need proof.
        else: seen_other.add(lowered)
        if lowered == b'content-type' and value not in (BINARY_MEDIA, STRUCTURED_MEDIA): raise Conflict()
        if lowered in ROUTING_HEADERS:
            if lowered in canonical or value != signed[lowered]: raise Conflict()
            canonical[lowered] = value
            routing.append((name, value))
    if total > 65536 or len(routing) != 6 or set(canonical) != set(ROUTING_HEADERS): raise Hold()
    def row(pairs):
        return len(pairs).to_bytes(2, 'big') + b''.join(len(name).to_bytes(4, 'big') + name
            + len(value).to_bytes(4, 'big') + value for name, value in pairs)
    full = row(received); original = row(routing); normalized = row(sorted(canonical.items()))
    image = (b'HX-EV-BROKER-HEADERS-2\0\x01\x00\x03\x01' + len(full).to_bytes(4, 'big') + full
             + b'\x02' + len(original).to_bytes(4, 'big') + original
             + b'\x03' + len(normalized).to_bytes(4, 'big') + normalized)
    if len(image) > 192 * 1024: raise Hold()
    return image

signed_headers = {name: b'value-' + bytes((i,)) for i, name in enumerate(ROUTING_HEADERS, 1)}
original_headers = tuple((name, signed_headers[name]) for name in ROUTING_HEADERS)
header_image = broker_header_image(original_headers, signed_headers)
assert header_image == broker_header_image(original_headers, signed_headers)
assert header_image != broker_header_image(tuple(reversed(original_headers)), signed_headers)
assert header_image != broker_header_image(((b'HX-Tenant-Id', original_headers[0][1]),
                                          *original_headers[1:]), signed_headers)
changed_structured = list(original_headers); changed_structured[0] = (original_headers[0][0], b'changed')
must_raise(Conflict, lambda: broker_header_image(tuple(changed_structured), signed_headers))
must_raise(Conflict, lambda: broker_header_image((original_headers[0], original_headers[0],
                                                *original_headers[2:]), signed_headers))
must_raise(Hold, lambda: broker_header_image(original_headers[:-1], signed_headers))
with_extra = (*original_headers, (b'X-Trace', b'one'), (b'x-trace', b'two'))
extra_image = broker_header_image(with_extra, signed_headers)
assert extra_image != header_image
assert broker_header_image(with_extra, signed_headers) != broker_header_image(
    (*original_headers, (b'X-Trace', b'two'), (b'x-trace', b'two')), signed_headers)
assert broker_header_image(with_extra, signed_headers) != broker_header_image(
    (*original_headers, (b'x-trace', b'one'), (b'X-Trace', b'two')), signed_headers)
assert broker_header_image(with_extra, signed_headers) != broker_header_image(
    (*original_headers, (b'x-trace', b'two'), (b'X-Trace', b'one')), signed_headers)
must_raise(Conflict, lambda: broker_header_image((*original_headers,
    (b'Content-Type', b'binary'), (b'content-type', b'binary')), signed_headers))
must_raise(Conflict, lambda: broker_header_image((*original_headers,
    (b'ce-id', b'one'), (b'CE-Id', b'one')), signed_headers))
must_raise(Conflict, lambda: broker_header_image((*original_headers,
    (b'ce-hyevattestation', b'a'), (b'CE-HyEvAtTeStAtIoN', b'a')), signed_headers))
must_raise(Hold, lambda: broker_header_image((*original_headers, (b'X-Unproved', b'one')), signed_headers))
must_raise(Hold, lambda: broker_header_image((*original_headers,
    (b'X-Unproved', b'one'), (b'x-unproved', b'two')), signed_headers))
assert broker_header_image((*original_headers, (b'Content-Type', BINARY_MEDIA)), signed_headers)
assert broker_header_image((*original_headers, (b'Content-Type', STRUCTURED_MEDIA)), signed_headers)
for invalid_media in (b'binary', b'structured', b'application/json'):
    must_raise(Conflict, lambda invalid_media=invalid_media: broker_header_image(
        (*original_headers, (b'Content-Type', invalid_media)), signed_headers))
must_raise(Hold, lambda: broker_header_image((*original_headers, (b'X-Trace', b'x' * 8192)), signed_headers))
must_raise(Hold, lambda: broker_header_image((*original_headers, *((b'X-Trace', b'x') for _ in range(123))), signed_headers))

print('C02 exact header image, proved duplicate and decision-field collision guards passed')

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
            return self.receipts[key][1]
        if operation in self.accepted or any(row[0] == mid for op, row in self.by_operation.items()
                                             if op in self.accepted): raise Conflict()
        previous = sorted((number, prior_nonce, state)
                          for (op, number), (prior_nonce, state) in self.receipts.items()
                          if op == operation)
        if attempt != len(previous) + 1 or (previous and previous[-1][2] != 'Rejected'): raise Hold()
        if any(prior_nonce == nonce for _, prior_nonce, _ in previous): raise Conflict()
        self.receipts[key] = (nonce, receipt)  # Immutable result for this attempt/fence nonce.
        if receipt == 'Accepted': self.accepted[operation] = pinned
        return receipt
    def operation_acceptance(self, operation):
        return self.accepted.get(operation)

pin = Pin(); plain = offset(0); attestation = b'fixture-attestation'
identity = (b'scope', b'destination', b'routing', header_image)
assert pin.create('m', plain, attestation, b'binary', identity) == pin.create('m', plain, attestation, b'binary', identity)
must_raise(Conflict, lambda: pin.create('m', offset(60), attestation, b'binary', identity))
must_raise(Conflict, lambda: pin.create('m', plain, attestation, b'changed-rendering', identity))
must_raise(Conflict, lambda: pin.create('m', plain, attestation, b'binary',
                                       (identity[0], identity[1], identity[2], extra_image)))

for changed_part in range(4):
    altered = list(identity); altered[changed_part] = b'changed'
    must_raise(Conflict, lambda altered=altered: pin.create('m', plain, attestation, b'binary', tuple(altered)))
must_raise(Hold, lambda: pin.send('m', 'op', 1, b'n1', 'Unknown'))
assert pin.send('m', 'op', 1, b'n1', 'Rejected') == 'Rejected'
assert pin.send('m', 'op', 1, b'n1', 'Rejected') == 'Rejected'
must_raise(Conflict, lambda: pin.send('m', 'op', 1, b'n1', 'Accepted'))
must_raise(Conflict, lambda: pin.send('m', 'op', 1, b'changed', 'Rejected'))
must_raise(Conflict, lambda: pin.send('m', 'op', 2, b'n1', 'Accepted'))
must_raise(Hold, lambda: pin.send('m', 'op', 3, b'n3', 'Accepted'))
assert pin.send('m', 'op', 2, b'n2', 'Accepted') == 'Accepted'
assert pin.send('m', 'op', 2, b'n2', 'Accepted') == 'Accepted'
assert pin.send('m', 'op', 1, b'n1', 'Rejected') == 'Rejected'
assert pin.operation_acceptance('op') == (plain, attestation, b'binary', identity)
must_raise(Conflict, lambda: pin.send('m', 'op', 2, b'n2', 'Rejected'))
must_raise(Conflict, lambda: pin.send('m', 'op', 3, b'n3', 'Rejected'))
must_raise(Conflict, lambda: pin.send('m', 'different-op', 1, b'n3', 'Accepted'))
must_raise(Conflict, lambda: pin.send('m', 'op', 1, b'n1', 'Rejected', parent='other-parent'))
retry_pin = Pin()
retry_pin.create('retry-member', plain, attestation, b'binary', identity)
assert retry_pin.send('retry-member', 'send-1', 1, b'nonce-1', 'Rejected') == 'Rejected'
must_raise(Hold, lambda: retry_pin.bind_retry('retry-member', 'send-2', 'command', 'send-1', False))
must_raise(Hold, lambda: retry_pin.bind_retry('retry-member', 'send-2', 'command', 'send-1', True, retryable=False))
must_raise(Conflict, lambda: retry_pin.bind_retry('retry-member', 'send-2', 'other-parent', 'send-1', True))
retry_pin.bind_retry('retry-member', 'send-2', 'command', 'send-1', True)
assert retry_pin.send('retry-member', 'send-2', 1, b'nonce-2', 'Accepted') == 'Accepted'
assert retry_pin.operation_acceptance('send-2') == (plain, attestation, b'binary', identity)
must_raise(Conflict, lambda: retry_pin.send('retry-member', 'send-2', 1, b'nonce-2', 'Accepted', parent='other-parent'))
must_raise(Conflict, lambda: retry_pin.bind_retry('retry-member', 'send-3', 'command', 'send-2', True))
assert pin.create('other-message', plain, attestation, b'binary', identity)
must_raise(Conflict, lambda: pin.send('other-message', 'op', 1, b'other-nonce', 'Accepted'))
must_raise(Hold, lambda: pin.send('m', 'op', 3, b'n3', 'Forged'))
print('C02 scoped pin, immutable per-attempt result and separate operation acceptance passed')

class ParentMemberChain:  # C02b: signed/CAS evidence is modeled by pinned purpose and issuer inputs.
    def __init__(self, tenant, scope, member, message, destination, pin_hash, maximum=64):
        self.identity = (tenant, scope, member, message, destination, pin_hash)
        self.maximum = maximum
        self.rows = []
        self.head = b'\0' * 32
        self.terminal = False
        self.results = {}
    def append(self, send_id, expected_generation, parent, purpose, issuer, prior_result=None):
        if (expected_generation != len(self.rows) or self.terminal): raise Conflict()
        if (parent != self.identity or purpose != 23 or issuer != b'pinned-broker'
            or not send_id or any(row[0] == send_id for row in self.rows)): raise Hold()
        if self.rows:
            prior_id = self.rows[-1][0]
            attempts = self.results.get(prior_id)
            if (not attempts or prior_result != attempts[-1] or any(result != 'Rejected01' for result in attempts)
                or sum(len(v) for v in self.results.values()) >= self.maximum): raise Hold()
        elif prior_result is not None: raise Hold()
        previous = self.rows[-1][1] if self.rows else b'\0' * 32
        row_hash = hashlib.sha256(b'parent-member-row:' + previous + send_id
                                  + len(self.rows).to_bytes(4, 'big')).digest()
        self.rows.append((send_id, row_hash, previous))
        self.head = hashlib.sha256(b'purpose-23-head:' + self.head + row_hash).digest()
        return self.head
    def observe(self, send_id, result, purposes=(20, 21, 22), issuer=b'pinned-broker'):
        if purposes != (20, 21, 22) or issuer != b'pinned-broker' or send_id != self.rows[-1][0]: raise Hold()
        if result not in ('Rejected01', 'Rejected02', 'Accepted', 'Unknown'): raise Hold()
        attempts = self.results.setdefault(send_id, [])
        if attempts and attempts[-1] != 'Rejected01': raise Hold()
        if sum(len(v) for v in self.results.values()) >= self.maximum: raise Hold()
        attempts.append(result)
    def verify(self, listed_ids, purpose=23, issuer=b'pinned-broker', terminal=False):
        if purpose != 23 or issuer != b'pinned-broker' or len(listed_ids) != len(self.rows): raise Hold()
        if tuple(listed_ids) != tuple(row[0] for row in self.rows): raise Hold()
        previous = b'\0' * 32
        head = b'\0' * 32
        for ordinal, (send_id, row_hash, predecessor) in enumerate(self.rows):
            if predecessor != previous or row_hash != hashlib.sha256(
                b'parent-member-row:' + previous + send_id + ordinal.to_bytes(4, 'big')).digest(): raise Hold()
            if send_id not in self.results or not self.results[send_id]:
                if terminal or ordinal != len(self.rows) - 1: raise Hold()  # Pending latest installed row.
            elif terminal and ordinal == len(self.rows) - 1 and self.results[send_id][-1] in ('Rejected01', 'Unknown'): raise Hold()
            previous = row_hash
            head = hashlib.sha256(b'purpose-23-head:' + head + row_hash).digest()
        if head != self.head: raise Hold()
        return True

parent_identity = (b'tenant', b'scope', 1, b'message', b'destination', b'pin')
member_chain = ParentMemberChain(*parent_identity)
member_chain.append(b'send-1', 0, parent_identity, 23, b'pinned-broker')
assert member_chain.verify((b'send-1',))  # Installed initial row has no attempt yet.
must_raise(Hold, lambda: member_chain.verify((b'send-1',), terminal=True))
member_chain.observe(b'send-1', 'Rejected01')
must_raise(Conflict, lambda: member_chain.append(b'race-loser', 0, parent_identity, 23, b'pinned-broker', 'Rejected01'))
member_chain.append(b'send-2', 1, parent_identity, 23, b'pinned-broker', 'Rejected01')
member_chain.observe(b'send-2', 'Accepted')
assert member_chain.verify((b'send-1', b'send-2'), terminal=True)
must_raise(Hold, lambda: member_chain.verify((b'send-2',)))
must_raise(Hold, lambda: member_chain.verify((b'send-1', b'send-2'), purpose=21))
must_raise(Hold, lambda: member_chain.verify((b'send-1', b'send-2'), issuer=b'generic-broker'))
must_raise(Hold, lambda: member_chain.append(b'send-3', 2, parent_identity, 23, b'pinned-broker', 'Accepted'))
wrong_scope = (b'tenant', b'other-scope', 1, b'message', b'destination', b'pin')
must_raise(Hold, lambda: member_chain.append(b'send-3', 2, wrong_scope, 23, b'pinned-broker', 'Accepted'))
uncertain_chain = ParentMemberChain(*parent_identity)
uncertain_chain.append(b'uncertain', 0, parent_identity, 23, b'pinned-broker')
uncertain_chain.observe(b'uncertain', 'Unknown')
must_raise(Hold, lambda: uncertain_chain.append(b'successor', 1, parent_identity, 23, b'pinned-broker', 'Unknown'))
print('C02b pending initial row, terminal completeness, successor race and purpose guards passed')

class RetainedSend:  # C02c: canonical renderings and exact signed header projection.
    @staticmethod
    def render(body, attestation):
        obj = {'specversion': '1.0', 'id': 'fixture-event', 'type': 'fixture.contract',
               'source': 'urn:hexalith:fixture', 'datacontenttype': BINARY_MEDIA.decode('ascii'),
               'data_base64': base64.b64encode(body).decode('ascii'),
               'hyevattestation': base64.b64encode(attestation).decode('ascii')}
        return json.dumps(obj, sort_keys=True, separators=(',', ':')).encode('utf-8')
    @staticmethod
    def decode(rendering, headers, mode):
        if mode not in ('Binary', 'Structured'): raise Hold()
        header_map = {}
        for name, value in headers:
            lowered = name.lower()
            if lowered in (b'content-type', b'ce-hyevattestation'):
                if lowered in header_map: raise Conflict()
                header_map[lowered] = value
        expected_media = BINARY_MEDIA if mode == 'Binary' else STRUCTURED_MEDIA
        if header_map.get(b'content-type') != expected_media: raise Conflict()
        if mode == 'Binary':
            encoded = header_map.get(b'ce-hyevattestation')
            if encoded is None: raise Hold()
            try: attestation = base64.b64decode(encoded, validate=True)
            except (ValueError, binascii.Error): raise Conflict()
            if base64.b64encode(attestation) != encoded: raise Conflict()
            return rendering, attestation
        if b'ce-hyevattestation' in header_map: raise Conflict()
        try:
            obj = json.loads(rendering)
            if set(obj) != {'specversion', 'id', 'type', 'source', 'datacontenttype',
                            'data_base64', 'hyevattestation'}: raise Conflict()
            body = base64.b64decode(obj['data_base64'], validate=True)
            attestation = base64.b64decode(obj['hyevattestation'], validate=True)
        except (ValueError, KeyError, TypeError, binascii.Error): raise Conflict()
        if RetainedSend.render(body, attestation) != rendering: raise Conflict()
        return body, attestation
    def __init__(self, body, headers, request_mode, accepted_mode, accepted_body,
                 accepted_headers, signed, conversion_authorized=False):
        request_projection = broker_header_image(headers, signed)
        accepted_projection = broker_header_image(accepted_headers, signed)
        request_decoded = self.decode(body, headers, request_mode)
        accepted_decoded = self.decode(accepted_body, accepted_headers, accepted_mode)
        if request_decoded != accepted_decoded: raise Conflict()
        if request_mode != accepted_mode and not conversion_authorized: raise Hold()
        if request_mode == accepted_mode and (body, headers) != (accepted_body, accepted_headers):
            raise Conflict()
        # Full images may differ on an authorized conversion; signed routing values may not.
        if request_projection is None or accepted_projection is None: raise Hold()
        self.value = (body, tuple(headers), request_mode, accepted_mode,
                      accepted_body, tuple(accepted_headers), tuple(sorted(signed.items())))
        self.comparison_source = self.value
        self.source_active = True
    def tombstone(self, authenticated_source):
        if not authenticated_source: raise Hold()
        self.comparison_source = self.value
    def duplicate(self, body, headers, request_mode, accepted_mode, accepted_body, accepted_headers):
        if not self.source_active or self.comparison_source is None: raise Hold()
        candidate = (body, tuple(headers), request_mode, accepted_mode,
                     accepted_body, tuple(accepted_headers), self.value[-1])
        if candidate != self.comparison_source: raise Conflict()
        self.decode(body, headers, request_mode)
        self.decode(accepted_body, accepted_headers, accepted_mode)
        return 'original-Accepted'

binary_headers = (*original_headers, (b'Content-Type', BINARY_MEDIA),
                  (b'ce-hyevattestation', base64.b64encode(b'fixture-attestation')))
structured_headers = (*original_headers, (b'Content-Type', STRUCTURED_MEDIA))
structured_body = RetainedSend.render(b'body', b'fixture-attestation')
send = RetainedSend(b'body', binary_headers, 'Binary', 'Structured', structured_body,
                    structured_headers, signed_headers, conversion_authorized=True)
send.tombstone(authenticated_source=True)
assert send.duplicate(b'body', binary_headers, 'Binary', 'Structured',
                      structured_body, structured_headers) == 'original-Accepted'
must_raise(Conflict, lambda: RetainedSend(b'body', binary_headers, 'Binary', 'Structured',
    RetainedSend.render(b'other-body', b'fixture-attestation'), structured_headers,
    signed_headers, conversion_authorized=True))
must_raise(Conflict, lambda: RetainedSend(b'body', binary_headers, 'Binary', 'Structured',
    RetainedSend.render(b'body', b'other-attestation'), structured_headers,
    signed_headers, conversion_authorized=True))
must_raise(Conflict, lambda: RetainedSend(b'body', binary_headers, 'Binary', 'Structured',
    structured_body, (*structured_headers[:-1], (b'Content-Type', BINARY_MEDIA)),
    signed_headers, conversion_authorized=True))
must_raise(Conflict, lambda: RetainedSend(b'body', binary_headers, 'Binary', 'Structured',
    structured_body, (*structured_headers, (b'Content-Type', STRUCTURED_MEDIA)),
    signed_headers, conversion_authorized=True))
must_raise(Hold, lambda: RetainedSend(b'body', binary_headers, 'Binary', 'Structured',
    structured_body, structured_headers, signed_headers))
must_raise(Conflict, lambda: send.duplicate(b'body', (*binary_headers, (b'X-Trace', b'two')),
    'Binary', 'Structured', structured_body, structured_headers))
send.source_active = False
must_raise(Hold, lambda: send.duplicate(b'body', binary_headers, 'Binary', 'Structured',
    structured_body, structured_headers))
print('C02c canonical conversion, decoded equality, mode headers and full-byte duplicate passed')

class AttemptObservations:
    def __init__(self): self.rows = {}; self.accepted = False
    def register(self, ordinal, nonce, result):
        if ordinal < 1 or not nonce or result not in ('Unknown', 'Rejected01', 'Rejected02', 'Accepted'): raise Hold()
        if ordinal in self.rows:
            if self.rows[ordinal] != (nonce, (result,)): raise Conflict()
            return result
        if ordinal != len(self.rows) + 1 or self.accepted: raise Hold()
        if any(previous_nonce == nonce for previous_nonce, _ in self.rows.values()): raise Conflict()
        if ordinal > 1 and self.rows[ordinal - 1][1][-1] != 'Rejected01': raise Hold()
        self.rows[ordinal] = (nonce, (result,))
        if result == 'Accepted': self.accepted = True
        return result
    def reconcile(self, ordinal, nonce, definitive):
        if definitive not in ('Rejected01', 'Rejected02', 'Accepted'): raise Hold()
        if ordinal not in self.rows or self.rows[ordinal] != (nonce, ('Unknown',)): raise Hold()
        if self.accepted: raise Conflict()
        self.rows[ordinal] = (nonce, ('Unknown', definitive))
        if definitive == 'Accepted': self.accepted = True
    def lookup(self, ordinal, nonce):
        if ordinal not in self.rows or self.rows[ordinal][0] != nonce: raise Conflict()
        return self.rows[ordinal][1], self.accepted

attempts = AttemptObservations()
assert attempts.register(1, b'n1', 'Unknown') == 'Unknown'
must_raise(Hold, lambda: attempts.register(2, b'n2', 'Rejected01'))
assert attempts.lookup(1, b'n1') == (('Unknown',), False)
attempts.reconcile(1, b'n1', 'Rejected01')
must_raise(Conflict, lambda: attempts.register(2, b'n1', 'Rejected01'))
assert attempts.lookup(1, b'n1') == (('Unknown', 'Rejected01'), False)
assert attempts.register(2, b'n2', 'Accepted') == 'Accepted'
assert attempts.lookup(1, b'n1') == (('Unknown', 'Rejected01'), True)
assert attempts.lookup(2, b'n2') == (('Accepted',), True)
retry_nonces = AttemptObservations()
assert retry_nonces.register(1, b'first', 'Rejected01') == 'Rejected01'
assert retry_nonces.register(2, b'second', 'Rejected01') == 'Rejected01'
must_raise(Conflict, lambda: retry_nonces.register(3, b'first', 'Rejected01'))
assert retry_nonces.register(3, b'third', 'Accepted') == 'Accepted'
terminal_rejection = AttemptObservations()
assert terminal_rejection.register(1, b'terminal', 'Rejected02') == 'Rejected02'
must_raise(Hold, lambda: terminal_rejection.register(2, b'another', 'Accepted'))
reconciled_terminal = AttemptObservations()
reconciled_terminal.register(1, b'unknown', 'Unknown')
reconciled_terminal.reconcile(1, b'unknown', 'Rejected02')
must_raise(Hold, lambda: reconciled_terminal.register(2, b'new', 'Accepted'))
must_raise(Conflict, lambda: attempts.lookup(1, b'changed'))
must_raise(Hold, lambda: attempts.register(3, b'n3', 'Rejected01'))
must_raise(Hold, lambda: attempts.reconcile(1, b'n1', 'Accepted'))
print('C02 complete attempt observations, unique nonces, class-02 closure and Unknown hold passed')

class HistoricalMapping:  # C02d: one old-obligation key, not one key per replacement.
    def __init__(self): self.by_old = {}
    def claim(self, old_obligation, replacement, old_query, exclusion, ownership_cas,
              grant=True, expected_generation=0):
        if not old_obligation or not replacement or not grant or not old_query or not exclusion or not ownership_cas:
            raise Hold()
        current = self.by_old.get(old_obligation)
        if current:
            if current[0] != replacement: raise Conflict()
            return current
        if expected_generation != 0: raise Conflict()
        self.by_old[old_obligation] = (replacement, 1, old_query, exclusion)
        return self.by_old[old_obligation]

mapping = HistoricalMapping()
old_obligation = (b'accepted-receipt', b'old-route', b'old-effect-key')
must_raise(Hold, lambda: mapping.claim(old_obligation, b'new-provider-a', True, False, True))
first_mapping = mapping.claim(old_obligation, b'new-provider-a', True, True, True)
assert mapping.claim(old_obligation, b'new-provider-a', True, True, True) == first_mapping
must_raise(Conflict, lambda: mapping.claim(old_obligation, b'new-provider-b', True, True, True))
must_raise(Conflict, lambda: mapping.claim(old_obligation, b'new-provider-a/changed-backend', True, True, True))
assert mapping.claim((b'other-accepted', b'old-route', b'old-effect-key'),
                     b'new-provider-b', True, True, True)
print('C02d old-obligation mapping CAS, exclusion and competing replacement passed')

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





def physical_filter_operation(accepted_claim, accepted_carrier, handoff_key, attempt_id):
    if not accepted_claim or not accepted_carrier or not handoff_key: raise Hold()
    # A broker retry token is deliberately excluded from the stable key.
    return hashlib.sha256(b'HX-EV-PHYSICAL-FILTER-OP-1\0' + b'\x01'
        + len(accepted_claim + accepted_carrier).to_bytes(4, 'big') + accepted_claim + accepted_carrier
        + len(handoff_key).to_bytes(4, 'big') + handoff_key).hexdigest().encode('ascii')

filter_op = physical_filter_operation(b'signed-accepted', b'carrier', b'handoff', b'attempt-1')
assert filter_op == physical_filter_operation(b'signed-accepted', b'carrier', b'handoff', b'attempt-2')
assert filter_op != physical_filter_operation(b'changed-accepted', b'carrier', b'handoff', b'attempt-1')
assert filter_op != physical_filter_operation(b'signed-accepted', b'carrier', b'other-handoff', b'attempt-1')
must_raise(Hold, lambda: physical_filter_operation(b'', b'carrier', b'handoff', b'attempt'))
print('C03e signed Accepted delivery derives stable physical-filter operation passed')

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
        expected = (self.pointer, name, kind, 'permanent-source-poison', 'active-addressed-fence', True) if result == 'Quarantined' else (self.pointer, name, kind, True)
        if kind is None or proof != expected or (result == 'Quarantined' and not permanent_poison): raise Hold()
        if name in self.results and self.results[name] != (result, proof): raise Conflict()
        self.results[name] = (result, proof)  # One terminal CAS key across outcomes.
    def ack(self, attempt, core_header, addressed_fence_check=None):
        if self.pointer is None or self.pointer != self.original or not self.broker_accepted: raise Hold()
        if not attempt or not core_header or self.attempts.get(attempt) != (self.pointer, core_header): raise Hold()
        if not self.pointer[1] and self.physical_filter != (self.pointer, 'signed-historical-physical-filter', True): raise Hold()
        if set(self.results) != set(self.pointer[1]): raise Hold()
        for name, (result, proof) in self.results.items():
            kind = {'Completed': 'authenticated-effect-receipt',
                    'Filtered': 'authenticated-filter', 'Quarantined': 'authenticated-capture'}.get(result)
            expected = (self.pointer, name, kind, 'permanent-source-poison', 'active-addressed-fence', True) if result == 'Quarantined' else (self.pointer, name, kind, True)
            if kind is None or proof != expected: raise Hold()
            if result == 'Quarantined' and (addressed_fence_check is None or not addressed_fence_check(name)):
                raise Hold()  # Active complete object-set readback on every current attempt.
        return True

handoff = Handoff(); source = ('pub', 'topic', 'sub', 7, 'm'); canonical = b'canonical-source'
must_raise(Hold, lambda: handoff.stage(source, ('a', 'b'), (plain, attestation), b'exact-json-1', canonical))
handoff.stage(source, ('a', 'b'), (plain, attestation), b'exact-json-1', canonical,
              complete=False, no_prior_pin_send_proof=True)
must_raise(Hold, lambda: handoff.ack(b'exact-json-1', b'exact-core-headers'))
handoff.stage(source, ('a', 'b'), (plain, attestation), b'exact-json-1', canonical)
must_raise(Hold, lambda: handoff.ack(b'exact-json-1', b'exact-core-headers'))
handoff.accept_binary()
must_raise(Hold, lambda: handoff.route_result('a', 'Completed'))
handoff.route_result('a', 'Completed', (handoff.pointer, 'a', 'authenticated-effect-receipt', True))
must_raise(Hold, lambda: handoff.ack(b'exact-json-1', b'exact-core-headers'))
handoff.stage(source, ('a', 'b'), (plain, attestation), b'{ "same":true }', canonical,
              prior_binary=(plain, attestation))
must_raise(Hold, lambda: handoff.route_result('b', 'Filtered'))
handoff.route_result('b', 'Filtered', (handoff.pointer, 'b', 'authenticated-filter', True))
assert handoff.ack(b'{ "same":true }', b'exact-core-headers')
must_raise(Hold, lambda: handoff.ack(b'unseen-redelivery', b'exact-core-headers'))
must_raise(Hold, lambda: handoff.ack(b'exact-json-1', b'changed-header'))
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
must_raise(Hold, lambda: empty_handoff.ack(b'exact-json-1', b'exact-core-headers'))
must_raise(Hold, lambda: empty_handoff.filter_empty((empty_handoff.pointer, 'unsigned-filter', True)))
empty_handoff.filter_empty((empty_handoff.pointer, 'signed-historical-physical-filter', True))
assert empty_handoff.ack(b'exact-json-1', b'exact-core-headers')
print('C04 exact current attempt/core/header and complete nonpoison handoff ack passed')

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
    def __init__(self): self.by_delivery = {}; self.scope_by_delivery = {}
    def acknowledge(self, component, topic, subscription, broker_delivery_id, raw, headers,
                    retained, readback, signed_nonadmissible, stable_id_proved,
                    scope, signed_scope, active_retention_fence, broker_scope):
        if not all((component, topic, subscription, broker_delivery_id, retained, readback,
                    signed_nonadmissible, stable_id_proved, scope, signed_scope,
                    active_retention_fence, broker_scope)): raise Hold()
        if scope != broker_scope: raise Conflict()
        physical_key = (component, topic, subscription, broker_delivery_id)
        if physical_key in self.scope_by_delivery and self.scope_by_delivery[physical_key] != scope: raise Conflict()
        key = (scope, *physical_key)
        exact = (raw, headers, signed_nonadmissible, signed_scope, active_retention_fence)
        if key in self.by_delivery and self.by_delivery[key] != exact: raise Conflict()
        self.by_delivery[key] = exact
        self.scope_by_delivery[physical_key] = scope
        return True

must_raise(Hold, lambda: capture_ack(b'x' * 17, 16, True, True, True, ('a',), {'a': True}))
must_raise(Hold, lambda: capture_ack(b'x', 16, False, True, True, ('a',), {'a': True}))
must_raise(Hold, lambda: capture_ack(b'x', 16, True, True, False, ('a',), {'a': True}))
must_raise(Hold, lambda: capture_ack(b'x', 16, True, True, True, ('a', 'b'), {'a': True}))
must_raise(Hold, lambda: capture_ack(b'x', 16, True, True, True, (), {}))
assert capture_ack(b'x' * 17, 16, True, True, True, ('a',), {'a': True}, broker_full_reference=True)
assert capture_ack(b'x', 16, True, True, True, (), {}, physical_filter=('signed-physical-filter', True))
quarantine = PhysicalQuarantine()
must_raise(Conflict, lambda: PhysicalQuarantine().acknowledge(
    'c', 't', 'sub', 'fresh', b'bad', b'h', True, True, True, True,
    'another-signed-isolation', True, True, 'broker-isolation'))
q = lambda raw=b'bad', topic='t', scope='broker-isolation', signed=True, fence=True, broker_scope='broker-isolation': quarantine.acknowledge(
    'c', topic, 'sub', 'stable', raw, b'h', True, True, True, True, scope, signed, fence, broker_scope)
must_raise(Hold, lambda: quarantine.acknowledge('c', 't', 'sub', 'stable', b'bad', b'h',
                                                True, False, True, True, 'broker-isolation', True, True, 'broker-isolation'))
must_raise(Hold, lambda: q(signed=False))
must_raise(Hold, lambda: q(fence=False))
assert q()
assert q()
assert q(raw=b'other', topic='other')
must_raise(Conflict, lambda: q(scope='another-signed-isolation'))
must_raise(Conflict, lambda: q(scope='another-signed-isolation', broker_scope='another-signed-isolation'))
must_raise(Conflict, lambda: q(raw=b'changed'))
print('C05 physical scope, active retention fence and exact full-byte readback passed')

def quarantine_reference(authority, key, proof_hash, full_byte_readback, ad31_verified, broker_verified, receipt_base=0):
    if (authority not in (0, 1) or not isinstance(key, bytes) or not 1 <= len(key) <= 1024
        or len(proof_hash) != 32 or not full_byte_readback or receipt_base < 0): raise Hold()
    if authority == 0 and not ad31_verified: raise Hold()
    if authority == 1 and not broker_verified: raise Hold()
    inner = bytes((authority,)) + len(key).to_bytes(4, 'big') + key + proof_hash
    result = len(inner).to_bytes(4, 'big') + inner  # Exact outer B for C3 tag 08 / C4 tag 05.
    if receipt_base + len(result) > 16 * 1024: raise Hold()
    return result

ad31_ref = quarantine_reference(0, b'capture-key', b'p' * 32, True, True, False)
broker_ref = quarantine_reference(1, b'broker-reference', b'p' * 32, True, False, True)
assert ad31_ref[4] == 0 and broker_ref[4] == 1 and ad31_ref != broker_ref
must_raise(Hold, lambda: quarantine_reference(1, b'broker-reference', b'p' * 32, True, True, False))
must_raise(Hold, lambda: quarantine_reference(0, b'capture-key', b'p' * 32, True, False, True))
must_raise(Hold, lambda: quarantine_reference(2, b'untagged', b'p' * 32, True, True, True))
must_raise(Hold, lambda: quarantine_reference(1, b'broker-reference', b'p' * 32, False, False, True))
must_raise(Hold, lambda: quarantine_reference(0, b'', b'p' * 32, True, True, False))
must_raise(Hold, lambda: quarantine_reference(0, b'x' * 1025, b'p' * 32, True, True, False))
assert quarantine_reference(0, b'x' * 1024, b'p' * 32, True, True, False)
must_raise(Hold, lambda: quarantine_reference(1, b'broker-reference', b'p' * 32, True, False, True, receipt_base=16 * 1024))
print('C05b tagged quarantine authority and full-byte readback guards passed')

class AddressedRetention:  # C05c: one CAS head per partition plus tagged retained object.
    def __init__(self): self.objects = {}
    def install(self, routes, reference, raw, headers, generation, etag, source,
                signed=True, storage_cas=True, no_expiry=True, partition=b'partition', handoff=b'handoff-a',
                object_id=b'canonical-ad31-object'):
        routes = tuple(routes)
        if (not routes or routes != tuple(sorted(set(routes))) or not partition or not reference or not raw
            or not signed or not storage_cas or not no_expiry or not generation or not etag or not source
            or not object_id): raise Hold()
        key = (partition, object_id)
        initial = (raw, tuple(headers), source, reference)
        row = self.objects.get(key)
        if row and (row['initial'] != initial or (generation, etag) !=
                    (row['initial_generation'], row['initial_etag']) or
                    row['handoffs'].get(handoff) != routes): raise Conflict()
        if row is None:
            self.objects[key] = {'initial': initial, 'initial_generation': generation,
                                 'initial_etag': etag, 'generation': generation, 'etag': etag,
                                 'active': True, 'versions': (), 'handoffs': {handoff: routes},
                                 'open': {handoff}, 'closed': set(), 'binds': {handoff: {}},
                                 'all_entries': {handoff}}
        return key
    def extend(self, key, generation, etag, previous_generation, previous_etag,
               signed=True, monotonic=True, open_list=None, closures=()):
        row = self.objects[key]
        if (not signed or not monotonic or not row['active'] or generation <= row['generation']
            or (previous_generation, previous_etag) != (row['generation'], row['etag'])): raise Hold()
        proposed = set(row['open']) if open_list is None else set(open_list)
        if not proposed.issubset(row['all_entries']): raise Hold()  # New entries require attach/opening proof.
        removed = row['open'] - proposed
        if removed != set(closures) or not set(closures).issubset(row['closed']): raise Hold()
        row['versions'] += ((generation, etag, frozenset(proposed), frozenset(closures)),)
        row['generation'], row['etag'], row['open'] = generation, etag, proposed
    def attach(self, key, handoff, routes, generation, etag, previous_generation, previous_etag,
               signed_handoff=True, opening_source=True, link_readback=True):
        row = self.objects[key]; routes = tuple(routes)
        if (handoff in row['handoffs'] or not signed_handoff or not opening_source or not link_readback
            or not routes or routes != tuple(sorted(set(routes))) or len(row['open']) >= 128): raise Hold()
        if (previous_generation, previous_etag) != (row['generation'], row['etag']) or generation <= row['generation']:
            raise Hold()
        row['handoffs'][handoff] = routes
        row['binds'][handoff] = {}
        row['all_entries'].add(handoff)
        self.extend(key, generation, etag, previous_generation, previous_etag,
                    open_list=row['open'] | {handoff})
    def bind(self, key, handoff, route, terminal_decision, receipt, post_handoff_link=True):
        row = self.objects[key]
        if (handoff not in row['open'] or route not in row['handoffs'][handoff]
            or not post_handoff_link or not terminal_decision or not receipt): raise Hold()
        candidate = (terminal_decision, receipt)
        old = row['binds'][handoff].get(route)
        if old is not None and old != candidate: raise Conflict()
        row['binds'][handoff][route] = candidate
    def close(self, key, handoff, all_routes_complete, broker_no_future, signed_closure):
        row = self.objects[key]
        if (handoff not in row['open'] or not all_routes_complete or not broker_no_future
            or not signed_closure or set(row['binds'][handoff]) != set(row['handoffs'][handoff])): raise Hold()
        row['closed'].add(handoff)  # Removal still requires a later exact CAS version.
    def acknowledge(self, key, route, raw, headers, generation, etag, route_decision,
                    readback=True, open_obligations=True, complete_set=True, handoff=b'handoff-a'):
        row = self.objects.get(key)
        if (row is None or not readback or not route_decision or not open_obligations
            or not complete_set or not row['active'] or handoff not in row['open']
            or route not in row['handoffs'][handoff]
            or set(row['binds'][handoff]) != set(row['handoffs'][handoff])
            or (raw, tuple(headers)) != row['initial'][:2]
            or (generation, etag) != (row['generation'], row['etag'])): raise Hold()
        if any(entry not in row['all_entries'] for entry in row['open']): raise Hold()
        return True
    def expire(self, key): self.objects[key]['active'] = False

addressed = AddressedRetention()
shared_key = addressed.install((b'route-a', b'route-b'), b'\x00ad31', b'exact-body',
                               ((b'X-Trace', b'one'),), 7, b'etag', b'permanent-source')
addressed.bind(shared_key, b'handoff-a', b'route-a', b'decision-a', b'receipt-a')
must_raise(Hold, lambda: addressed.close(shared_key, b'handoff-a', True, True, True))
must_raise(Hold, lambda: addressed.acknowledge(shared_key, b'route-a', b'exact-body',
    ((b'X-Trace', b'one'),), 7, b'etag', True))
addressed.bind(shared_key, b'handoff-a', b'route-b', b'decision-b', b'receipt-b')
assert addressed.acknowledge(shared_key, b'route-a', b'exact-body',
                             ((b'X-Trace', b'one'),), 7, b'etag', True)
assert addressed.acknowledge(shared_key, b'route-b', b'exact-body',
                             ((b'X-Trace', b'one'),), 7, b'etag', True)
assert addressed.install((b'route-a', b'route-b'), b'\x00ad31', b'exact-body',
                         ((b'X-Trace', b'one'),), 7, b'etag', b'permanent-source') == shared_key
must_raise(Conflict, lambda: addressed.install((b'route-a', b'route-b'), b'\x00ad31-changed-proof',
    b'exact-body', ((b'X-Trace', b'one'),), 7, b'etag', b'permanent-source'))
assert shared_key != AddressedRetention().install((b'route-a',), b'\x00ad31', b'exact-body',
    ((b'X-Trace', b'one'),), 7, b'etag', b'permanent-source', object_id=b'other-object')
must_raise(Conflict, lambda: addressed.install((b'route-a', b'route-b'), b'\x00ad31',
    b'exact-body', ((b'X-Trace', b'one'),), 8, b'changed-etag', b'permanent-source'))
must_raise(Conflict, lambda: addressed.install((b'route-a',), b'\x00ad31', b'exact-body',
    ((b'X-Trace', b'one'),), 7, b'etag', b'permanent-source'))
must_raise(Hold, lambda: addressed.acknowledge(shared_key, b'route-a', b'exact-body',
    ((b'X-Trace', b'one'),), 7, b'etag', True, complete_set=False))
must_raise(Hold, lambda: addressed.acknowledge(shared_key, b'route-a', b'exact-body',
    ((b'x-trace', b'one'),), 7, b'etag', True))
must_raise(Hold, lambda: addressed.extend(shared_key, 8, b'etag-8', 6, b'etag'))
addressed.extend(shared_key, 8, b'etag-8', 7, b'etag')
must_raise(Hold, lambda: addressed.extend(shared_key, 9, b'etag-9', 8, b'etag-8', open_list=set()))
assert addressed.acknowledge(shared_key, b'route-a', b'exact-body',
    ((b'X-Trace', b'one'),), 8, b'etag-8', True)
addressed.attach(shared_key, b'handoff-b', (b'route-c',), 9, b'etag-9', 8, b'etag-8')
assert addressed.objects[shared_key]['open'] == {b'handoff-a', b'handoff-b'}
must_raise(Hold, lambda: addressed.acknowledge(shared_key, b'route-c', b'exact-body',
    ((b'X-Trace', b'one'),), 9, b'etag-9', True, handoff=b'handoff-b'))
addressed.bind(shared_key, b'handoff-b', b'route-c', b'decision-c', b'receipt-c')
assert addressed.acknowledge(shared_key, b'route-c', b'exact-body',
    ((b'X-Trace', b'one'),), 9, b'etag-9', True, handoff=b'handoff-b')
addressed.close(shared_key, b'handoff-a', True, True, True)
must_raise(Hold, lambda: addressed.extend(shared_key, 10, b'etag-10', 9, b'etag-9',
    open_list=set(), closures=(b'handoff-a',)))
addressed.extend(shared_key, 10, b'etag-10', 9, b'etag-9',
                 open_list={b'handoff-b'}, closures=(b'handoff-a',))
assert addressed.objects[shared_key]['open'] == {b'handoff-b'}
assert addressed.acknowledge(shared_key, b'route-c', b'exact-body',
    ((b'X-Trace', b'one'),), 10, b'etag-10', True, handoff=b'handoff-b')
must_raise(Hold, lambda: addressed.acknowledge(shared_key, b'route-a', b'exact-body',
    ((b'X-Trace', b'one'),), 10, b'etag-10', True))
must_raise(Hold, lambda: addressed.install((b'route-c',), b'\x01broker', b'exact-body', (),
    7, b'etag', b'permanent-source', no_expiry=False, object_id=b'canonical-broker-object'))
broker_key = addressed.install((b'route-c',), b'\x01broker', b'exact-body', (),
                               7, b'etag', b'permanent-source', object_id=b'canonical-broker-object')
addressed.bind(broker_key, b'handoff-a', b'route-c', b'decision', b'receipt')
assert addressed.acknowledge(broker_key, b'route-c', b'exact-body', (), 7, b'etag', True)
addressed.expire(broker_key)
must_raise(Hold, lambda: addressed.acknowledge(broker_key, b'route-c', b'exact-body', (), 7, b'etag', True))

poison_handoff = Handoff()
poison_handoff.stage(source, ('a',), (plain, attestation), b'poison-json', canonical,
                     no_prior_pin_send_proof=True)
poison_handoff.accept_binary()
poison_proof = (poison_handoff.pointer, 'a', 'authenticated-capture', 'permanent-source-poison', 'active-addressed-fence', True)
must_raise(Hold, lambda: poison_handoff.route_result('a', 'Quarantined', poison_proof))
poison_handoff.route_result('a', 'Quarantined', poison_proof, permanent_poison=True)
must_raise(Hold, lambda: poison_handoff.ack(b'poison-json', b'exact-core-headers'))
poison_key = addressed.install((b'a',), b'\x00poison', b'poison-json',
                               ((b'X-Trace', b'one'),), 10, b'poison-etag', b'permanent-source', object_id=b'canonical-poison-object')
addressed.bind(poison_key, b'handoff-a', b'a', b'quarantined', b'capture-receipt')
def poison_fence(route):
    return addressed.acknowledge(poison_key, route.encode(), b'poison-json',
        ((b'X-Trace', b'one'),), 10, b'poison-etag', True)
assert poison_handoff.ack(b'poison-json', b'exact-core-headers', poison_fence)
poison_handoff.stage(source, ('a',), (plain, attestation), b'poison-json-duplicate', canonical,
                     prior_binary=(plain, attestation))
assert poison_handoff.ack(b'poison-json-duplicate', b'exact-core-headers', poison_fence)
addressed.expire(poison_key)
must_raise(Hold, lambda: poison_handoff.ack(b'poison-json', b'exact-core-headers', poison_fence))
must_raise(Hold, lambda: poison_handoff.ack(b'poison-json-duplicate', b'exact-core-headers', poison_fence))
print('C04/C05c complete shared-set and active-fence poison ack passed')


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
               tenant='tenant-a', destination=('component', 'topic'), header_image=b'complete-headers'):
        binding = self.parents.get((tenant, operation))
        if binding is None or binding[3] != destination: raise Hold()
        if (tenant, binding[1]) in self.fenced_parents: raise Hold()  # Before duplicate-key lookup.
        if (revision != self.revision or config != self.config or probe != self.probe
            or nonce != self.nonce or now >= self.fence_expiry
            or not all(now < expiry for expiry in self.lease_expiries)): raise Hold()
        key = (tenant, self.domain, *destination, operation)  # Digest is immutable value.
        candidate = (mode_bytes, pin_hash, binding, header_image)
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
                                           b'bytes', b'pin', 99, header_image=b'changed-X-Trace'))
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
print('C06 complete header image, tenant-scoped key and two-destination fence passed')

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
        if states or terminal or proofs or fence_active or sequence: raise Hold()
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
must_raise(Hold, lambda: outcome((), no_op=True, proofs=('unexpected',)))
must_raise(Hold, lambda: outcome((), no_op=True, fence_active=True))
must_raise(Hold, lambda: outcome((), no_op=True, sequence=TERMINAL_ORDER))
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

def terminal_member(attempts, maximum, roster, policy, signed_proof, final_class=None):
    if maximum < 1 or maximum > 64 or len(attempts) > maximum: raise Hold()
    if tuple(number for number, _ in attempts) != tuple(range(1, len(attempts) + 1)): raise Hold()
    if not attempts:
        if signed_proof is None or len(signed_proof) != 5 or signed_proof[2:] != (True, True, True): raise Hold()
        proof_hash = member_proof_hash(signed_proof[0], signed_proof[1])
        if roster != (0, None, None, proof_hash) or policy != (0, None, proof_hash): raise Hold()
        return 'zero-attempt-nonadmissible'
    if final_class not in ('Accepted', 'Rejected02'): raise Hold()
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
                       (1, receipt, None), None, 'Rejected02') == 'attempted'
must_raise(Hold, lambda: terminal_member(((1, receipt),), 64, (1, b'broker-op', receipt, None),
                                        (1, receipt, None), None, 'Rejected01'))
must_raise(Hold, lambda: terminal_member(((1, receipt),), 64, (1, b'broker-op', receipt, None),
                                        (1, receipt, None), None, 'Unknown'))
assert terminal_member(((1, receipt),), 64, (1, b'broker-op', receipt, None),
                       (1, receipt, None), None, 'Accepted') == 'attempted'
must_raise(Hold, lambda: terminal_member(((2, receipt),), 64, (1, b'broker-op', receipt, None),
                                        (1, receipt, None), None, 'Rejected02'))

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

class PreSendAdmission:  # C08e: member-scoped atomic broker rule and zero-index source stand-ins.
    def __init__(self, partitions):
        self.partitions = tuple(partitions)
        self.registered = set(); self.queued = set(); self.accepted = set(); self.inflight = set()
        self.rules = {}; self.generation = 0
    def has_prior(self, member):
        return any(entry[0] == member for ledger in
                   (self.registered, self.queued, self.accepted, self.inflight) for entry in ledger)
    def install(self, member, policy, configuration, source, manifest, purpose=24,
                issuer=b'pinned-admission', signed=True):
        if (purpose != 24 or issuer != b'pinned-admission' or not signed or not source
            or not policy or not configuration or tuple(manifest) != self.partitions
            or self.has_prior(member) or member in self.rules): raise Hold()
        self.generation += 1
        self.rules[member] = (member, policy, configuration, source, self.generation, tuple(manifest))
        return self.rules[member]
    def send(self, member, send_id):
        if member in self.rules: raise Hold()
        self.registered.add((member, send_id))
    def verify(self, member, proof, manifest, signed=True):
        if (not signed or self.rules.get(member) != proof or proof[0] != member
            or tuple(manifest) != self.partitions or self.has_prior(member)): raise Hold()
        return True

presend = PreSendAdmission((b'destination-a', b'alias-a'))
presend.send((b'tenant', b'parent', b'member-a'), b'first-send')
member_b = (b'tenant', b'parent', b'member-b')
rule = presend.install(member_b, b'policy', b'config', b'permanent-revocation',
                       (b'destination-a', b'alias-a'))
assert presend.verify(member_b, rule, (b'destination-a', b'alias-a'))
must_raise(Hold, lambda: presend.install((b'tenant', b'parent', b'member-a'),
    b'policy', b'config', b'late-revocation', (b'destination-a', b'alias-a')))
must_raise(Hold, lambda: presend.send(member_b, b'late-send'))
must_raise(Hold, lambda: presend.verify(member_b, rule, (b'destination-a',)))
must_raise(Hold, lambda: PreSendAdmission((b'destination-a',)).install(
    member_b, b'policy', b'config', b'late-revocation', (b'destination-a',), purpose=21))
print('C08e member-scoped pre-send rule and unrelated-member guard passed')

class DrainLedger:  # C08f: coordinator invocations differ from broker sends.
    def __init__(self): self.rows = []; self.broker_sends = 0; self.absence = False
    def register(self, invocation):
        if self.absence or not invocation or invocation in self.rows: raise Hold()
        self.rows.append(invocation)
    def status(self, reported, signed=True, complete=True, source=None):
        if not signed or not complete: raise Hold()
        if source == 'absent':
            if self.rows or reported is not None: raise Hold()
            self.absence = True
            return None
        if self.absence or source != 'head' or reported is None or reported < 0 or reported > 2**31 - 1: raise Hold()
        if reported != len(self.rows): raise Hold()
        return reported

drain = DrainLedger()
drain.broker_sends = 3
assert drain.status(0, source='head') == 0
drain.register(b'invocation-1'); drain.register(b'invocation-2')
assert drain.status(2, source='head') == 2
must_raise(Hold, lambda: drain.status(3, source='head'))
must_raise(Hold, lambda: drain.status(2, source='head', complete=False))
must_raise(Hold, lambda: drain.status(None, source='absent'))
absent_drain = DrainLedger()
assert absent_drain.status(None, source='absent') is None
must_raise(Hold, lambda: absent_drain.status(0, source='absent'))
must_raise(Hold, lambda: absent_drain.register(b'late-invocation'))
print('C08f authenticated drain count, present zero and explicit null passed')

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
    if receipt_class == 'Accepted' and reason != b'Accepted': raise Hold()
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
accepted_row = (compact_row[0], compact_row[1], compact_row[2], compact_row[3],
                u_hash(b'Accepted'), compact_row[5])
assert compact_policy_member(full_member, b'send', b'Accepted', accepted_row,
                             signed_member_policy, 'Accepted', True, True, True)
must_raise(Hold, lambda: compact_policy_member(full_member, b'send', b'closed-reason', compact_row,
                                               signed_member_policy, 'Accepted', True, True, True))
must_raise(Hold, lambda: compact_policy_member(full_member, b'send', b'closed-reason',
                                           compact_row, signed_member_policy,
                                           'TerminalRejected', False, True, True))
must_raise(Hold, lambda: compact_policy_member(full_member, b'send', b'closed-reason',
                                           compact_row, signed_member_policy,
                                           'TerminalRejected', True, True, False))

class RevisionClock:  # C08d: immutable A8 outcome key precedes final head CAS.
    def __init__(self, initial=b'prior'):
        self.reservations = {}
        self.outcomes = {}
        self.head = initial
        self.head_revision = 0
        self.final = {}
    def reserve(self, revision, predecessor, outcome_hash, head_hash, utc, signed, readback):
        if not signed or not readback or revision < 1 or not outcome_hash or not head_hash: raise Hold()
        key = (revision, predecessor, outcome_hash)
        row = (head_hash, utc)
        if key in self.reservations and self.reservations[key][0] != head_hash: raise Conflict()
        self.reservations.setdefault(key, row)
        return key
    def create_outcome(self, key, prepared, readback):
        if key not in self.reservations or not prepared or not readback: raise Hold()
        revision = key[0]
        if revision in self.outcomes and self.outcomes[revision] != key: raise Conflict()
        if revision not in self.outcomes and (key[1] != self.head or revision != self.head_revision + 1):
            raise Conflict()  # Before occupying only the next A8 revision.
        self.outcomes.setdefault(revision, key)
        return key
    def finalize(self, key, expected_head, signed_cas, readback):
        if key not in self.reservations or not signed_cas or not readback: raise Hold()
        revision, predecessor, _ = key
        if revision not in self.outcomes: raise Hold()
        if self.outcomes[revision] != key: raise Conflict()
        if predecessor != expected_head: raise Conflict()  # Reject before any finalization.
        if revision in self.final:
            if self.final[revision] != key: raise Conflict()
            return self.reservations[key][1]  # Lost acknowledgement, same branch.
        if self.head != expected_head: raise Conflict()
        self.head = self.reservations[key][0]
        self.head_revision = revision
        self.final[revision] = key
        return self.reservations[key][1]
    def status_time(self, revision):
        if revision not in self.final or self.outcomes.get(revision) != self.final[revision]: raise Hold()
        return self.reservations[self.final[revision]][1]

skipped_clock = RevisionClock()
skipped = skipped_clock.reserve(100, b'prior', b'skipped', b'skipped-head', 120, True, True)
must_raise(Conflict, lambda: skipped_clock.create_outcome(skipped, True, True))
assert 100 not in skipped_clock.outcomes
predecessor_clock = RevisionClock()
predecessor_key = predecessor_clock.reserve(1, b'other-predecessor', b'candidate', b'candidate-head',
                                             121, True, True)
must_raise(Conflict, lambda: predecessor_clock.create_outcome(predecessor_key, True, True))
assert 1 not in predecessor_clock.outcomes
must_raise(Hold, lambda: predecessor_clock.status_time(1))
clock = RevisionClock()
orphan = clock.reserve(1, b'prior', b'loser', b'loser-head', 122, True, True)
must_raise(Hold, lambda: clock.status_time(1))
winning = clock.reserve(1, b'prior', b'outcome', b'head', 123, True, True)
assert winning != orphan
clock.create_outcome(winning, True, True)
must_raise(Conflict, lambda: clock.create_outcome(orphan, True, True))
must_raise(Conflict, lambda: clock.finalize(winning, b'wrong-predecessor', True, True))
must_raise(Hold, lambda: clock.status_time(1))
assert clock.finalize(winning, b'prior', True, True) == 123
assert clock.finalize(winning, b'prior', True, True) == 123
assert clock.status_time(1) == 123
assert clock.reserve(1, b'prior', b'outcome', b'head', 124, True, True) == winning
assert clock.status_time(1) == 123
must_raise(Conflict, lambda: clock.reserve(1, b'prior', b'outcome', b'changed-head', 124, True, True))
must_raise(Conflict, lambda: clock.finalize(orphan, b'prior', True, True))
must_raise(Conflict, lambda: clock.create_outcome(
    clock.reserve(1, b'prior', b'different-observation', b'different-head', 126, True, True), True, True))
next_key = clock.reserve(2, b'head', b'later-observation', b'head-2', 127, True, True)
clock.create_outcome(next_key, True, True)
assert clock.finalize(next_key, b'head', True, True) == 127
assert clock.status_time(2) == 127
lost_ack = RevisionClock()
pending = lost_ack.reserve(1, b'prior', b'occupied', b'occupied-head', 130, True, True)
lost_ack.create_outcome(pending, True, True)
competing = lost_ack.reserve(1, b'prior', b'new-observation', b'new-head', 131, True, True)
must_raise(Conflict, lambda: lost_ack.create_outcome(competing, True, True))
assert lost_ack.finalize(pending, b'prior', True, True) == 130
later = lost_ack.reserve(2, b'occupied-head', b'new-observation', b'new-head-2', 132, True, True)
lost_ack.create_outcome(later, True, True)
assert lost_ack.finalize(later, b'occupied-head', True, True) == 132
must_raise(Hold, lambda: clock.reserve(3, b'head-2', b'x', b'y', 128, True, False))
print('C08d contiguous A8 revision, occupied outcome, predecessor and clock recovery passed')

class TerminalPointer:  # C08g: proposals cannot become public without final-head CAS.
    def __init__(self):
        self.head = b'failed-head-1'; self.etag = b'etag-1'; self.head_state = 'failed'; self.proposals = set()
        self.pointer = None; self.closed = False
    def propose(self, head, members, proof_root, broker_fence):
        if not proof_root or not broker_fence or not members: raise Hold()
        if all(member == 'Accepted' for member in members): raise Conflict()
        if any(member not in ('Accepted', 'Rejected02', 'Nonadmissible03') for member in members): raise Hold()
        proposal = (head, tuple(members), proof_root, broker_fence)
        self.proposals.add(proposal)
        return proposal
    def advance(self, head, etag, state):
        if self.closed or state not in ('failed', 'published', 'pending'): raise Conflict()
        self.head, self.etag, self.head_state = head, etag, state
    def close(self, proposal, expected_head, expected_etag, authenticated=True,
              signed_final_head=True, final_head_state='failed'):
        if not authenticated or not signed_final_head or proposal not in self.proposals: raise Hold()
        if final_head_state != self.head_state or final_head_state != 'failed': raise Conflict()
        if proposal[0] != expected_head or (self.head, self.etag) != (expected_head, expected_etag):
            raise Conflict()
        pointer = (proposal, expected_head, expected_etag)
        if self.pointer is not None:
            if self.pointer != pointer: raise Conflict()
            return self.pointer  # Lost acknowledgement readback.
        self.pointer = pointer; self.closed = True  # One atomic pointer/closure CAS.
        return pointer
    def status(self):
        if self.pointer is None or not self.closed or self.pointer[1:] != (self.head, self.etag):
            raise Hold()
        return 'PublishFailed'

terminal_pointer = TerminalPointer()
must_raise(Conflict, lambda: terminal_pointer.propose(b'failed-head-1', ('Accepted',), b'proof', b'fence'))
old_proposal = terminal_pointer.propose(b'failed-head-1', ('Accepted', 'Rejected02'), b'proof-1', b'fence')
must_raise(Hold, lambda: terminal_pointer.status())  # Proposal alone has no public status.
terminal_pointer.advance(b'failed-head-2', b'etag-2', 'failed')
must_raise(Conflict, lambda: terminal_pointer.close(old_proposal, b'failed-head-1', b'etag-1'))
new_proposal = terminal_pointer.propose(b'failed-head-2', ('Accepted', 'Rejected02'), b'proof-2', b'fence')
pointer = terminal_pointer.close(new_proposal, b'failed-head-2', b'etag-2')
assert terminal_pointer.close(new_proposal, b'failed-head-2', b'etag-2') == pointer
assert terminal_pointer.status() == 'PublishFailed'
must_raise(Conflict, lambda: terminal_pointer.advance(b'published-head', b'etag-3', 'published'))
published_pointer = TerminalPointer()
published_proposal = published_pointer.propose(b'published-head', ('Accepted', 'Rejected02'),
                                                b'proof', b'fence')
published_pointer.advance(b'published-head', b'published-etag', 'published')
must_raise(Conflict, lambda: published_pointer.close(published_proposal,
    b'published-head', b'published-etag'))
must_raise(Conflict, lambda: published_pointer.close(published_proposal,
    b'published-head', b'published-etag', final_head_state='failed'))
must_raise(Hold, lambda: published_pointer.status())
failed_pointer = TerminalPointer()
failed_proposal = failed_pointer.propose(b'failed-head-1', ('Accepted', 'Rejected02'),
                                         b'proof', b'fence')
must_raise(Hold, lambda: failed_pointer.close(failed_proposal, b'failed-head-1',
    b'etag-1', signed_final_head=False))
assert failed_pointer.close(failed_proposal, b'failed-head-1', b'etag-1')
print('C08g authenticated failed A8 head, stale proposal and pointer passed')


def capture_version_hash(record, prior_receipt, generation, etag):  # C05d: acyclic CAS order.
    if not record or not prior_receipt or generation < 1 or not etag: raise Hold()
    installed = hashlib.sha256(b'HX-EV-CAPTURE-INSTALLED-1\0' + b'\x01'
                               + len(record).to_bytes(4, 'big') + record).digest()
    receipt = b'CAS:' + installed + generation.to_bytes(4, 'big') + etag
    version = hashlib.sha256(b'HX-EV-ADDRESSED-VERSION-HASH-1\0' + b'\x01'
                             + len(record).to_bytes(4, 'big') + record
                             + len(receipt).to_bytes(4, 'big') + receipt).digest()
    return installed, receipt, version

installed, cas_receipt, version_hash = capture_version_hash(b'pre-CAS-version', b'prior', 9, b'etag-9')
assert installed in cas_receipt and version_hash not in cas_receipt
assert version_hash != capture_version_hash(b'changed-version', b'prior', 9, b'etag-9')[2]
assert version_hash != capture_version_hash(b'pre-CAS-version', b'prior', 10, b'etag-10')[2]
print('C05d pre-CAS record, post-CAS receipt and acyclic version hash passed')

class RetryExhausted:  # C08h: retryable max is visible, nonterminal, and gated.
    def __init__(self, first_response, maximum):
        self.first_response = first_response; self.maximum = maximum
    def status(self, attempts, final_class, proof, recovery_action=False, old_fence=False):
        if not proof or attempts != self.maximum or final_class != 'Rejected01': raise Hold()
        if recovery_action and not old_fence: raise Hold()
        return ('EventsStored', 2, False, 'publication_retry_exhausted_hold', self.first_response)

exhausted = RetryExhausted(b'first-response', 2)
assert exhausted.status(2, 'Rejected01', True) == ('EventsStored', 2, False,
                                                  'publication_retry_exhausted_hold', b'first-response')
must_raise(Hold, lambda: exhausted.status(2, 'Rejected01', False))
must_raise(Hold, lambda: exhausted.status(2, 'Rejected01', True, recovery_action=True))
assert exhausted.status(2, 'Rejected01', True, recovery_action=True, old_fence=True)[1] == 2

def proposal_carrier_hash(proposal, carrier, purpose, issuer, signed_utc_in_interval, unrevoked):
    if (not proposal or not carrier or purpose != 29 or issuer != b'pinned-closure'
        or not signed_utc_in_interval or not unrevoked): raise Hold()
    return hashlib.sha256(b'HX-EV-TERMINAL-PROPOSAL-CARRIER-1\0' + b'\x01'
        + len(proposal).to_bytes(4, 'big') + proposal
        + len(carrier).to_bytes(4, 'big') + carrier).digest()

proposal_hash = proposal_carrier_hash(b'failed-head-proposal', b'purpose-29-carrier', 29,
                                     b'pinned-closure', True, True)
assert proposal_hash != proposal_carrier_hash(b'failed-head-proposal', b'changed-carrier', 29,
                                              b'pinned-closure', True, True)
must_raise(Hold, lambda: proposal_carrier_hash(b'failed-head-proposal', b'carrier', 21,
                                              b'pinned-closure', True, True))
must_raise(Hold, lambda: proposal_carrier_hash(b'failed-head-proposal', b'carrier', 29,
                                              b'pinned-closure', False, True))
print('C08h signed terminal proposal carrier and exhausted retryable hold passed')

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

# C11a: accepted body and full headers are predicted from immutable configuration before send.
class PrecommittedAcceptance:
    def __init__(self, request_body, request_headers, accepted_body, accepted_headers, decoded, attestation, signed_six):
        self.request = (request_body, tuple(request_headers))
        self.accepted = (accepted_body, tuple(accepted_headers))
        self.common = (decoded, attestation, tuple(signed_six))
    def accept(self, actual_body, actual_headers, decoded, attestation, signed_six):
        if (actual_body, tuple(actual_headers)) != self.accepted or (decoded, attestation, tuple(signed_six)) != self.common:
            raise Conflict()
        return self.request, self.accepted

precommitted = PrecommittedAcceptance(b'binary', ((b'Content-Type', BINARY_MEDIA),),
    b'structured', ((b'Content-Type', STRUCTURED_MEDIA),), b'body', b'attest', signed_headers)
assert precommitted.accept(b'structured', ((b'Content-Type', STRUCTURED_MEDIA),),
    b'body', b'attest', signed_headers)[1][0] == b'structured'
must_raise(Conflict, lambda: precommitted.accept(b'structured',
    ((b'Content-Type', STRUCTURED_MEDIA), (b'X-Broker', b'added')), b'body', b'attest', signed_headers))
must_raise(Conflict, lambda: precommitted.accept(b'structured',
    ((b'Content-Type', STRUCTURED_MEDIA),), b'changed', b'attest', signed_headers))
print('C11a precommitted accepted rendering and complete header image passed')

# C11b: authority proof generations are values at one canonical physical-object key.
def framed(value): return len(value).to_bytes(4, 'big') + value
def object_key(partition, authority, tag, object_id):
    if not partition or not authority or tag not in (0, 1) or not object_id: raise Hold()
    return hashlib.sha256(b'HX-EV-RETAINED-OBJECT-KEY-1\0' + b'\x01' +
        framed(partition) + framed(authority) + bytes((tag,)) + framed(object_id)).digest()

key_a = object_key(b'partition', b'ad31', 0, b'object-1')
assert key_a == object_key(b'partition', b'ad31', 0, b'object-1')
assert key_a != object_key(b'partition', b'ad31', 0, b'object-2')
class CanonicalFence:
    def __init__(self): self.heads = {}
    def install(self, key, proof, carrier, kind):
        old = self.heads.get(key)
        if old is not None and old != (proof, carrier, kind): raise Conflict()
        self.heads[key] = (proof, carrier, kind)
    def renew(self, key, old_proof, new_proof, carrier, cas_receipt):
        old = self.heads.get(key)
        if not cas_receipt or old is None or old[0] != old_proof or old[1] != carrier: raise Hold()
        self.heads[key] = (new_proof, carrier, old[2])

fence = CanonicalFence(); fence.install(key_a, b'proof-1', b'bytes', 'Addressed')
must_raise(Conflict, lambda: fence.install(key_a, b'proof-2', b'bytes', 'Addressed'))
must_raise(Conflict, lambda: fence.install(key_a, b'proof-1', b'bytes', 'Unidentified'))
must_raise(Hold, lambda: fence.renew(key_a, b'proof-1', b'proof-2', b'bytes', False))
fence.renew(key_a, b'proof-1', b'proof-2', b'bytes', True)
assert len(fence.heads) == 1 and fence.heads[key_a][0] == b'proof-2'
print('C11b canonical physical object ID and shared proof-version head passed')

# C11c: the postcommit effect hash authenticates immutable record and provider proof.
def effect_hash(record, provider_commit, provider_readback):
    if not record or not provider_commit or not provider_readback: raise Hold()
    return hashlib.sha256(b'HX-EV-EFFECT-RECEIPT-HASH-2\0' + b'\x01' +
        framed(record) + framed(provider_commit) + framed(provider_readback)).digest()
effect_h = effect_hash(b'exact-effect', b'commit-etag-1', b'readback-etag-1')
assert effect_h != effect_hash(b'changed-effect', b'commit-etag-1', b'readback-etag-1')
assert effect_h != effect_hash(b'exact-effect', b'commit-etag-2', b'readback-etag-1')
assert effect_h != effect_hash(b'exact-effect', b'commit-etag-1', b'readback-etag-2')
must_raise(Hold, lambda: effect_hash(b'exact-effect', b'', b'readback-etag-1'))
def route_key(tenant, handoff_key_text, route):
    return hashlib.sha256(b'HX-EV-ROUTE-DECISION-KEY-1\0' + b'\x01' +
        framed(tenant) + framed(handoff_key_text) + framed(route)).digest()
assert route_key(b'tenant', b'handoff:abc', b'route') == route_key(b'tenant', b'handoff:abc', b'route')
assert route_key(b'tenant', b'handoff:abc', b'route') != route_key(b'tenant', b'abc', b'route')
print('C11c effect record/provider proof and framed route key passed')

# C11d: physical filter identity consumes the C2 Accepted result and immutable log entry.
def filter_operation(accepted_claim, carrier, signed_log, log_carrier, log_cas, handoff_key):
    if not all((accepted_claim, carrier, signed_log, log_carrier, log_cas, handoff_key)): raise Hold()
    source = hashlib.sha256(b'HX-EV-PHYSICAL-FILTER-ACCEPTED-1\0' + b'\x01' +
        framed(accepted_claim) + framed(carrier) + framed(signed_log) +
        framed(log_carrier) + framed(log_cas)).digest()
    return hashlib.sha256(b'HX-EV-PHYSICAL-FILTER-OP-2\0' + b'\x01' +
        source + framed(handoff_key)).hexdigest()
filter_id = filter_operation(b'C2-Accepted', b'purpose-21', b'log-id-7',
    b'purpose-28', b'CAS-generation-1', b'handoff:7')
assert filter_id == filter_operation(b'C2-Accepted', b'purpose-21', b'log-id-7',
    b'purpose-28', b'CAS-generation-1', b'handoff:7')  # New attempt ID is not an input.
assert filter_id != filter_operation(b'C2-Accepted', b'purpose-21', b'log-id-8',
    b'purpose-28', b'CAS-generation-1', b'handoff:7')
assert filter_id != filter_operation(b'C2-Accepted', b'purpose-21', b'log-id-7',
    b'purpose-28', b'CAS-generation-2', b'handoff:7')
must_raise(Hold, lambda: filter_operation(b'', b'purpose-21', b'log-id-7',
    b'purpose-28', b'CAS-generation-1', b'handoff:7'))
print('C11d C2 Accepted/log physical-filter identity passed')

# C11e: the first unsigned proposal and carrier are one branch-scoped CAS outcome.
class TerminalProposalStore:
    def __init__(self): self.reservations = {}; self.proposals = {}
    def reserve(self, tenant, scope, head, unsigned):
        branch = hashlib.sha256(b'HX-EV-TERMINAL-BRANCH-1\0' + b'\x01' +
            framed(tenant) + scope + head).digest()
        if branch in self.reservations and self.reservations[branch] != unsigned: raise Conflict()
        self.reservations[branch] = unsigned
        return branch
    def create(self, branch, scope, head, carrier):
        unsigned = self.reservations[branch]
        uh = hashlib.sha256(b'HX-EV-TERMINAL-PROPOSAL-UNSIGNED-1\0' + b'\x01' + framed(unsigned)).digest()
        key = hashlib.sha256(b'HX-EV-TERMINAL-PROPOSAL-KEY-1\0' + b'\x01' +
            framed(b'tenant') + scope + head + uh).digest()
        if key in self.proposals and self.proposals[key] != (unsigned, carrier): raise Conflict()
        self.proposals[key] = (unsigned, carrier)
        return key, self.proposals[key]
proposal_store = TerminalProposalStore()
branch = proposal_store.reserve(b'tenant', b'scope', b'failed-head', b'proposal@UTC-1')
proposal_key, retained = proposal_store.create(branch, b'scope', b'failed-head', b'carrier-1')
assert proposal_store.create(branch, b'scope', b'failed-head', b'carrier-1') == (proposal_key, retained)
must_raise(Conflict, lambda: proposal_store.create(branch, b'scope', b'failed-head', b'carrier-2'))
must_raise(Conflict, lambda: proposal_store.reserve(b'tenant', b'scope', b'failed-head', b'proposal@UTC-2'))
print('C11e unsigned terminal proposal key, first carrier and UTC retry passed')

# C11f: signed drain predecessor includes carrier and atomic CAS receipt; result ETag is allocated first.
def drain_head_hash(claim, carrier, cas_receipt, allocated_etag, actual_etag):
    if not all((claim, carrier, cas_receipt, allocated_etag, actual_etag)) or allocated_etag != actual_etag:
        raise Hold()
    return hashlib.sha256(b'HX-EV-COORDINATOR-DRAIN-HEAD-HASH-1\0' + b'\x01' +
        framed(claim) + framed(carrier) + framed(cas_receipt)).digest()
drain_head = drain_head_hash(b'head', b'purpose-25', b'CAS:etag-1', b'etag-1', b'etag-1')
assert drain_head != drain_head_hash(b'head', b'changed-carrier', b'CAS:etag-1', b'etag-1', b'etag-1')
assert drain_head != drain_head_hash(b'head', b'purpose-25', b'CAS:etag-2', b'etag-2', b'etag-2')
must_raise(Hold, lambda: drain_head_hash(b'head', b'purpose-25', b'CAS:etag-2', b'etag-1', b'etag-2'))
print('C11f drain predecessor and preallocated ETag/CAS order passed')

# C11g: terminal source hashes and root bind all three ordered authenticated sources.
def terminal_hash(domain, record, carrier, receipt, evidence):
    if not all((record, carrier, receipt, evidence)): raise Hold()
    return hashlib.sha256(domain + b'\x01' + framed(record) + framed(carrier) +
        framed(receipt) + framed(evidence)).digest()
def terminal_root(drain_source, no_accept_source, policy_source):
    return hashlib.sha256(b'HX-EV-TERMINAL-PROOF-ROOT-1\0' + b'\x01' +
        framed(b'tenant') + b'scope' + b'failed-head' + b'final-set' +
        drain_source + no_accept_source + policy_source).digest()
drain_source = terminal_hash(b'HX-EV-TERMINAL-DRAIN-SOURCE-1\0', b'drain', b'signer', b'CAS', b'ordered-roster')
no_accept_source = terminal_hash(b'HX-EV-TERMINAL-NO-ACCEPT-SOURCE-1\0', b'noaccept'+drain_source, b'signer', b'CAS', b'ordered-attempts')
policy_source = terminal_hash(b'HX-EV-TERMINAL-POLICY-SOURCE-1\0', b'policy'+no_accept_source, b'signer', b'CAS', b'ordered-policy')
root = terminal_root(drain_source, no_accept_source, policy_source)
changed_policy = terminal_hash(b'HX-EV-TERMINAL-POLICY-SOURCE-1\0', b'policy'+no_accept_source, b'signer', b'CAS', b'changed-policy')
assert root != terminal_root(drain_source, no_accept_source, changed_policy)
assert root != terminal_root(no_accept_source, drain_source, policy_source)
print('C11g ordered terminal source records, segment evidence and root passed')

# C11h: exhausted rejection retains committed nullable metadata and polling interval.
def exhausted_public_status(original, member_receipt, utc_source):
    if not member_receipt or not utc_source: raise Hold()
    retained = ('CorrelationId', 'MessageId', 'AggregateId', 'EventCount',
                'RejectionEventType', 'TimeoutDuration', 'TenantId', 'DrainAttemptCount')
    result = {field: original[field] for field in retained}
    result.update(Status='EventsStored', StatusCode=2, Retryable=False,
                  RecoveryReasonCode='publication_retry_exhausted_hold',
                  FailureReason=None, Timestamp=utc_source, **{'Retry-After': '1'})
    return result
source_status = {'CorrelationId': 'corr', 'MessageId': 'message', 'AggregateId': None,
    'EventCount': 1, 'RejectionEventType': 'order-rejected', 'TimeoutDuration': None,
    'TenantId': 'tenant', 'DrainAttemptCount': 3}
status = exhausted_public_status(source_status, b'final-Rejected01', b'original-UTC')
for field in ('CorrelationId', 'MessageId', 'AggregateId', 'EventCount', 'RejectionEventType',
              'TimeoutDuration', 'TenantId', 'DrainAttemptCount'):
    assert status[field] == source_status[field]
assert status['RejectionEventType'] == 'order-rejected' and status['Retryable'] is False
assert status['Retry-After'] == '1' and status['Timestamp'] == b'original-UTC'
assert exhausted_public_status(dict(source_status, RejectionEventType=None), b'receipt', b'UTC')['RejectionEventType'] is None
must_raise(Hold, lambda: exhausted_public_status(source_status, b'', b'UTC'))
print('C11h exhausted rejection metadata and status-polling semantics passed')
```

## Integration handoff and review disposition

Integrate C1–C6 with 6.5a A3–A8 and 6.5b B2/B6/B8 as **one** change to the AD-13 draft. Replace draft codec-01 public outcome with A8 codec-03 complete observations and immutable first POST pin; add C5 purpose-29 signed branch-scoped terminal proposal, atomic final-head pointer, nonterminal `PublicationRetryExhaustedHold` and complete evidence before public `PublishFailed`. Reader-first rollout must update status consumers to interpret `Retryable=false` with `EventsStored` and `RecoveryReasonCode="publication_retry_exhausted_hold"` as an operator-held nonterminal state; clients that infer terminality from `Retryable=false` alone are fenced until compatible. C2 adds one old-obligation-keyed historical replacement owner/CAS; C3/C4 require one object-wide addressed capture fence across all handoffs, purpose-28 stable physical-filter identity and acyclic pre-CAS-record/post-CAS-receipt hashes for every retained object. These additions replace any older per-handoff capture-fence or hash-only cross-record interpretation. **C3 supersedes draft §7's 4 KiB complete `EventEffectReceipt` cap with 16 KiB** for both writer/provider preflight and all readers (including old marker adapters and rollback consumers); activation requires a versioned receipt-capability/probe and reader-first compatibility across mixed fleets. An old 4 KiB-only reader may read historical ≤4 KiB records but must be fenced from any route that can produce a larger C3 receipt. No record may be truncated to fit the old cap. Replace V1/new V2 save assumptions with A5 codec-02 complete-set certificate/readback, and retain B2's V2 five-flag raw-proof rule. Preserve historic V17/V20/V22/V23 literals, old lease byte shape, old public constructors and §12 six-field `UNAPPROVED` receipt until separately approved integration. **BH37-9: accepted; historical Loop-7 UTC-only wording is superseded by C1's `T`/`O(T)` original-offset rule and four fixed digest vectors.** The historical triage row remains intact. No activation or Story 6.6 authorization follows from this child candidate.

Independent review should challenge: A8 no-future-acceptance proof under a real broker and partial accepted set; same-transaction effect/receipt and multi-route pointer linearizability; raw ingress/capture preallocation and retention under actual DAPR intermediaries; offset preservation in Binary and structured carriers; B6 accounting across replicas; and all ten B handoff gates. An unresolved provider fact blocks readiness and requires approved integration amendment, not local 6.6 improvisation. C01–C11 and focused model families prove only local byte/state models; C7 provider/crash vectors are unexecuted.

## Verification

Run the fenced Python block with `python3` from repository root and mutation-check C01 digest and C03/C05/C08 acknowledgement guards. Run `git diff --check`, resolve relative file/section links, compare protected normative/triage/6.5a/6.5b/runtime/test hashes and changed paths, and confirm AD-13 receipt stays `UNAPPROVED`. Current runtime tests establish only the inventory's present behavior; they do not validate this future contract. Record exact local results and review findings in the separate 6.5c execution record.
