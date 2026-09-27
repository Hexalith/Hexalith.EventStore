<!-- Historical Story 6.5 review material; the AD-13 normative artifact and its approval gate remain authoritative. -->

## Review Triage Log

| ID | Verdict and evidence | Route |
| --- | --- | --- |
| BH-1 | high — The artifact requires both new wire fields at append while its rollout accepts old writers; a new gateway would reject their commands. | bad_spec |
| BH-2 | high — `IEventPayload` has no aggregate-id member, yet the reader requires one for every legacy payload. Valid admitted history could fail replay. | bad_spec |
| BH-3 | high — Effective current-version DTOs and stored version-1 discriminator rules are mixed; the existing projection/subscription DTOs lack metadata version, allowing ambiguous or repeated adaptation. | bad_spec |
| BH-4 | medium — Exact legacy CLR aliases may identify distinct historical payload shapes; mapping every alias to version 1 has no proof or per-alias schema rule. | bad_spec |
| BH-5 | medium — The pipeline checks payload aggregate identity only after the last hop, so an intermediate identity change can reach the next upcaster. | bad_spec |
| BH-6 | high — The rollout gates on a registry fingerprint without defining its canonical bytes or contents; equal claims need not represent equal reader capabilities. | bad_spec |
| BH-7 | high — Subscription poison says retry or quarantine without a selection rule or sink readiness contract, leaving terminal acknowledgement undecided. | bad_spec |
| BH-8 | high — Publication enters the shared reader but the draft does not choose stored or adapted payload bytes; registry changes could alter redelivery under the same MessageId. | bad_spec |
| BH-9 | medium — The 256-event preparation limit names a legacy full-array handler as a limitation without defining its supported compatibility path or typed failure. | bad_spec |
| EC-1 | high — Same v1 writer admission contradiction as BH-1; the old wire shape is reachable during reader-first rollout. | bad_spec |
| EC-2 | high — `ProjectionEventDto` and `EventStoreDomainEventEnvelope` omit `MetadataVersion`; without it receivers cannot distinguish v1 from incomplete v2. | bad_spec |
| EC-3 | high — Same undefined registry fingerprint as BH-6; two services may report agreement despite different upcaster implementations. | bad_spec |
| EC-4 | high — Same missing legacy identity extractor as BH-2; `IEventPayload` is a marker interface. | bad_spec |
| EC-5 | high — Marker acquisition is keyed only by MessageId; completed records do not compare scope or event digest, so a conflicting reused ID can be treated as a duplicate. | bad_spec |
| EC-6 | medium — `StreamsController` and `AdminStreamQueryController` read/unprotect events but are absent from the inventory; Story 6.6 could leave them outside the shared path. | bad_spec |
| VG-1 | low — Epic context says the event-versioning artifact is absent, now stale after this draft exists; update its status after rederivation. | patch |
| VG-2 | false — `epics.md`'s backlog consequence is conditional on a completion request; this build has requested no completion and the tracker truthfully shows work in progress. | reject |
| BH2-1 | high — The downstream DTO declaration names `EventContractType` and `PayloadVersion` twice, so an implementation cannot represent both stored and effective values without arbitrary renaming. | bad_spec |
| BH2-2 | high — The preappend table requires payload aggregate ID even though the registered legacy `NoPayloadIdentity` policy admits a shape without that claim; a valid v1 writer would be rejected. | bad_spec |
| BH2-3 | high — The v2 read rule calls the metadata pair well formed but does not require legacy `EventTypeName` to equal canonical `EventContractType`, allowing contradictory stored identity claims. | bad_spec |
| BH2-4 | high — The digest covers stored protected bytes and protection extensions while subscription receives only readable bytes and restamped publication metadata; it cannot verify a conflicting original event from the delivered evidence. | bad_spec |
| BH2-5 | high — The original digest's "fixed schema order" gives no field list, null encoding, numeric/timestamp form, or extension encoding, so producers can calculate different digests for the same event. | bad_spec |
| BH2-6 | false — Whole-assembly hashing can delay the v2 write switch after an unrelated rebuild, but the stated gate intentionally waits for all serving readers to advertise one approved fingerprint; v1 reads/writes remain available during that wait. | reject |
| BH2-7 | high — `DomainServiceOptions.MaxEventSizeBytes` is configurable above its 1 MiB default, whereas the draft hard-caps all historical reads at 1 MiB; valid persisted history could become unreadable. | bad_spec |
| BH2-8 | high — The snapshot spec's folded envelope version does not identify aggregate state schema compatibility; using an old folded state with a new event Apply path can diverge from full replay. | bad_spec |
| BH2-9 | medium — Duplicate historical alias registration fails safely, but the draft has no preflight inventory or explicit disposition for an alias reused across unknown historical schemas, so rollout readiness could claim coverage without testing the corpus. | bad_spec |
| BH2-10 | high — Requiring idempotent or transactional handler behavior without registration/readiness enforcement still allows a crash after side effects and before `Dispatched` to run side effects twice. | bad_spec |
| EC2-1 | high — The preappend payload-ID requirement contradicts registered `NoPayloadIdentity` for admitted legacy wire events. | bad_spec |
| EC2-2 | high — Stored and effective DTO fields share the same names, making the wire contract impossible to implement literally. | bad_spec |
| EC2-3 | high — The reader only checks output size and identity after each hop; malformed UTF-8, non-object JSON or excessive depth can reach the next upcaster. | bad_spec |
| EC2-4 | high — The precommit cancellation rule says abandon staged changes without first witnessing whether an ambiguous `SaveStateAsync` committed; `AggregateActor` already performs save-failure readback and the new rule could regress it. | bad_spec |
| EC2-5 | high — Subscriber lacks stored protected bytes and original protection metadata used by the marker digest, so it cannot independently recompute that digest. | bad_spec |
| EC2-6 | high — Canonical digest encoding omits complete field order, null/extension and number/time encodings; marker equality would vary by implementation. | bad_spec |
| EC2-7 | medium — `DomainSharedProjectionRebuildDispatcher` consumes projection DTOs and computes history/inventory fingerprints, but the inventory and validation vectors omit its rebuild route. | bad_spec |
| VG2-1 | high — The downstream DTO's duplicate stored/effective member names leave no implementable schema or compatibility mapping. | bad_spec |
| VG2-2 | high — The gate claims to hash the entire document except receipt lines while actually excluding its entire section, permitting its approval rules to change under the same digest. | bad_spec |
| BH3-1 | false — The documented boundary is the standalone receipt marker line; `5f492b...` hashes through the preceding newline. The reviewer hashed to the first inline mention of the marker inside the rule, yielding `dfb143...`. | reject |
| BH3-2 | high — `EventStoreDomainEventEnvelope` lacks `AggregateType` and `DomainServiceVersion`; both are in the delivery digest, so a subscriber cannot recompute it from the proposed DTO. | bad_spec |
| BH3-3 | high — `DaprDomainServiceInvoker.ToDomainResult` maps wire events into three-field `ISerializedEventPayload` wrappers, and `EventPersister` reads those wrappers; the new metadata fields disappear before persistence. | bad_spec |
| BH3-4 | high — `Contracts.Events.EventEnvelope` normalizes null extensions to an empty map, while the digest distinguishes null/empty and absent/null; a digest from that typed object can differ from raw storage evidence. | bad_spec |
| BH3-5 | medium — The draft allows a separately provable discriminator for reused aliases but defines no descriptor/fingerprint/read mechanism, so valid distinguishable history has no selected chain. | bad_spec |
| BH3-6 | medium — The approved folded-snapshot v1 envelope lacks the new Apply witness; unconditional bypass after 6.6 would cause persistent full replay unless an additive reconciliation/refold path is specified. | bad_spec |
| BH3-7 | medium — `AggregateIdentity` has tenant/domain/aggregate ID only; the comparison to aggregate type needs a registered route or persisted aggregate metadata source. | bad_spec |
| BH3-8 | high — Duplicate JSON member names can make an identity extractor and CLR deserializer select different `aggregateId` values. The reader lacks duplicate-property rejection. | bad_spec |
| BH3-9 | high — Stored `SerializationFormat` can differ from the readable format emitted by unprotection; the delivery digest currently names the stored field but the published DTO can carry the readable field. | bad_spec |
| BH3-10 | high — Existing `MaxEventSizeBytes` and `MaxEventsPerResult` are configurable above the draft v2 limits; without active-producer preflight, cutover can reject formerly valid commands. | bad_spec |
| EC3-1 | high — Same missing `AggregateType` and `DomainServiceVersion` delivery fields as BH3-2; digest recomputation cannot succeed. | bad_spec |
| EC3-2 | high — Same stored/readable serialization-format ambiguity as BH3-9; protected deliveries can hash a value different from the published one. | bad_spec |
| EC3-3 | high — Queued v1 publications have no attestation; the new subscriber would classify them as poison despite being valid admitted history. | bad_spec |
| EC3-4 | high — Existing durable marker records have state/time only, so six-field comparison cannot safely deduplicate their completed or dispatched deliveries after upgrade. | bad_spec |
| EC3-5 | medium — Same undefined historical alias discriminator as BH3-5; the proposed exception to migration has no algorithm. | bad_spec |
| EC3-6 | high — `UpcastAsync` returns an allocated byte array before the pipeline can enforce its output size; a registered hop can exhaust memory first. | bad_spec |
| EC3-7 | medium — Non-cancellation upcaster exceptions are not split between deterministic invalid input and transient implementation/dependency failure, so consumers may retry or quarantine differently. | bad_spec |
| EC3-8 | medium — `AdminTraceQueryController.GetEventsAsync` reads raw envelopes and exposes `EventTypeName`, but the inventory and shared reader rollout omit it. | bad_spec |
| EC3-9 | high — Existing publications may have been sent before pin records existed; a new publisher cannot assert same-MessageId byte stability for their retry without drain or durable reconciliation. | bad_spec |
| BH4-1 | medium — The approval text says only receipt fields are excluded while the hash excludes the entire receipt section, including labels and status; that section must be strictly parsed or described as mutable. | bad_spec |
| BH4-2 | high — New v1 wire objects can serialize nullable fields as explicit null, but the artifact admits only absent fields; valid v1 output from an additive binary could be rejected. | bad_spec |
| BH4-3 | high — `DomainServiceWireResult.FromDomainResult` is static and currently emits CLR names with no registry input; the v2 writer mode and descriptor source are not defined at that production seam. | bad_spec |
| BH4-4 | high — `EventStorePayloadSerialization.Options` binds properties case-insensitively; JSON names differing only by case pass ordinal duplicate checks and can split extractor/deserializer identity. | bad_spec |
| BH4-5 | high — Projection and replay DTOs deliberately omit tenant/domain/aggregate scope; new version fields alone cannot prove the gateway validated the same stored event received by the domain service. | bad_spec |
| BH4-6 | high — Fingerprint omits validator, chain executor and attestation verifier implementation/configuration; readers can advertise the same fingerprint while applying different evolution or trust behavior. | bad_spec |
| BH4-7 | medium — A snapshot byte digest with no canonical bytes or writer/readback rule can mismatch after provider serialization even for the same folded state. | bad_spec |
| BH4-8 | high — Existing Completed/Dispatched markers have no effect receipt; requiring that proof for all legacy states can block ordinary duplicate delivery indefinitely. | bad_spec |
| BH4-9 | high — The handler contract only returns Task today, so a vague registration-time proof of idempotency cannot enforce safe post-effect retry or guide existing handlers' migration. | bad_spec |
| BH4-10 | high — Stored digest processes an unbounded extensions dictionary; large provider metadata can exhaust read/publication work without a typed limit. | bad_spec |
| BH4-11 | high — `EventPublisher` currently calls generic `PublishEventAsync` with an object; pinning DTO bytes does not ensure the DAPR serializer sends those exact bytes on retry. | bad_spec |
| BH4-12 | high — Pinned old signatures become unverifiable if an attestation public key is retired before queued delivery, retry and rollback horizons drain. | bad_spec |
| BH4-13 | medium — The additive async processor has no exact signature, registration precedence or router selection rule, so cancellation may still use the old tokenless path. | bad_spec |
| EC4-1 | high — Case-insensitive property collision is reachable under the existing Web serializer; ordinal-only duplicate rejection cannot protect identity extraction. | bad_spec |
| EC4-2 | medium — A syntactically valid upcaster output may violate its declared target schema and reach the next hop; add version-specific schema validation before handoff. | bad_spec |
| EC4-3 | high — `DomainServiceWireResult.FromDomainResult` creates a full byte array before the proposed 1 MiB v2 check, so a large result can exhaust memory first. | bad_spec |
| EC4-4 | high — `IEventUpcaster` descriptor names a serializer, but E fingerprint records omit that serializer ID; different edge serialization policies could advertise one fingerprint. | bad_spec |
| EC4-5 | high — The approved folded-snapshot 4,096-byte overhead applies only to Unprotected and Legacy no-op paths; epic context currently states it without that qualifier and would misclassify protected snapshots. | patch |
| EC4-6 | high — `CoordinatedCommandActor` reads and builds the entire source stream for policy validation but is omitted from the inventory and bounds; long streams can retain unbounded arrays. | bad_spec |
| BH5-1 | false — Stored `EventMetadata.MetadataVersion` is a required `int`, and `EventPersister` writes `1` for v1; wire absence/null is normalized before persistence. The delivery digest hashes the stored value, which the DTO carries. | reject |
| BH5-2 | high — `EventRouteAttestation` names stored digest and target version but does not bind effective payload bytes/format; a changed adapted body could skip upcasting and reach Apply. | bad_spec |
| BH5-3 | medium — A committed event can lack a publication pin after a crash before first send; the draft needs explicit deterministic recovery from durable event/outbox evidence to avoid a stuck publication. | bad_spec |
| BH5-4 | high — Marker identity includes `RegistryFingerprint`, which changes on normal reader upgrade; a matching original event could conflict instead of deduplicate. | bad_spec |
| BH5-5 | high — Marker key includes scope and handler route, so a reused `MessageId` in another scope gets a fresh marker despite the stated cross-scope conflict rule. | bad_spec |
| BH5-6 | high — Pinned CloudEvent topic/id/type/source/content type/headers are not bound to subscriber attestation checks, allowing broker/route metadata to diverge from the signed event. | bad_spec |
| BH5-7 | high — Addressed valid events with no registered handler have no explicit disposition, leaving the current silent completion path plausible. | bad_spec |
| BH5-8 | medium — Current actor reads materialize typed envelopes before a page-level byte check; "bounded raw storage envelope" lacks a provider/API mechanism or transport cap. | bad_spec |
| BH5-9 | high — Unconditional `IEventContract` preappend validation excludes admitted v1 `IEventPayload` aliases that do not implement the current contract. | bad_spec |
| BH5-10 | medium — Story 6.2 is still backlog and current snapshots lack `SnapshotEnvelopeVersion`; the witness rule needs a no-folded-runtime path and integration ownership. | bad_spec |
| BH5-11 | high — A whole-fleet fingerprint cannot agree when services intentionally register different domain assemblies; readiness must compare a domain-scoped manifest or common complete registry. | bad_spec |
| BH5-12 | low — `git diff --check -- <new file>` ignores the untracked normative artifact and can report success without checking it. | patch |
| EC5-1 | high — Same unbound effective payload as BH5-2; an altered adapted replay DTO can be applied under a valid source attestation. | bad_spec |
| EC5-2 | high — `PrimaryWriteAlias` may denote an older source version than the current serialized CLR payload; v1 emission could upcast current bytes as old schema. | bad_spec |
| EC5-3 | medium — `IAggregateReplay.Replay` is synchronous; the token matrix does not define a token-aware replay adapter or cancellation checks within long replay. | bad_spec |
| EC5-4 | high — `DomainServiceRequestRouter.Replay` currently resolves only keyed `IDomainProcessor`; a route registered only as `IAsyncDomainProcessor` would fail as unknown despite being active. | bad_spec |
| EC5-5 | high — A legacy Completed marker has only MessageId/state/time; matching one candidate actor event cannot independently prove that marker belonged to it when the ID was reused. | bad_spec |
| EC5-6 | high — `DaprAggregateStateReconstructor` drops metadata during replay DTO conversion and `TrustedEffectRetentionGate` reads source events before authorizing an effect; both require explicit shared-reader integration. | bad_spec |
| BH6-1 | high — The D fingerprint records the CLR assembly but not the current payload type. Two types in that assembly can deserialize or Apply differently under one fingerprint. | bad_spec |
| BH6-2 | high — A describes an alias-specific validator, but the A fingerprint record lacks that validator's identity and implementation digest; V is version-scoped and cannot distinguish two aliases at one version. | bad_spec |
| BH6-3 | medium — The production v2 writer depends on an endpoint-advertised mode without defining its authoritative owner or response transport field. `DomainServiceRequestRouter` still calls the static v1 result helper, leaving normal and rejection results able to choose different modes. | bad_spec |
| BH6-4 | high — V1 writes retain configurable size limits while new readers cap legacy readable payloads at 64 MiB. A continuing v1 writer can append an event that the new fleet cannot read. | bad_spec |
| BH6-5 | high — A valid historical event already at its current schema but larger than the 1 MiB effective-output cap has no upcast hop to shrink it. The corpus preflight checks the 64 MiB source ceiling but not existence of a compliant effective path. | bad_spec |
| BH6-6 | medium — `ProjectionDeliveryRetryWorker` and `EventStoreProjectionDeliveryHistoryReader` load event envelopes, and `ProjectionUpdateOrchestrator` calls `GetEventsAsync(0)` before projection dispatch. The shared-reader list does not name their admission boundaries, allowing these paths to keep an old conversion route. | bad_spec |
| BH6-7 | medium — `AdminStreamQueryController` repeatedly calls `GetEventsAsync(0)`, which materializes the whole stream. The document's page and legacy-policy array limits do not set a bounded Admin inspection path. | bad_spec |
| BH6-8 | high — The projection failure row allows either fail or quarantine and does not define whether a quarantined event blocks the contiguous checkpoint or how replay resumes. Those alternatives produce different durable progress. | bad_spec |
| BH6-9 | medium — The snapshot witness may be a side record or a future extension. This leaves storage and atomic readback choice open despite the document's closed-decisions claim. | bad_spec |
| BH6-10 | high — The artifact requires signed attestations but does not specify signature algorithm, signed-byte encoding, key ID format or verifier trust configuration. Publisher and subscriber implementations can disagree while each follows the digest rules. | bad_spec |
| BH6-11 | high — A tenant-scoped MessageId reservation cannot detect reuse in another tenant, but the rule promises conflict across scopes. The alternative global or tenant-scoped key leaves uniqueness undecided. | bad_spec |
| EC6-1 | low — The sequence contract requires positive contiguous `long` values, while `EventPersister` uses unchecked `currentSequence + 1 + i` and the draft has no max-sequence preappend guard. At overflow, sequence can wrap instead of failing before staging. | bad_spec |
| EC6-2 | high — Same continuing-v1-writer/read-ceiling conflict as BH6-4; the active writer must be fenced before the new reader fleet takes over. | bad_spec |
| EC6-3 | high — Same alias-validator fingerprint omission as BH6-2; version-level V records cannot bind alias-specific behavior. | bad_spec |
| EC6-4 | high — The receiver authenticates source attestation and effective payload hash, but the text does not require comparison of every adapted DTO target/scope field and the active domain fingerprint to the signed claims before Apply. A valid signature could accompany stale or misrouted DTO metadata. | bad_spec |
| EC6-5 | high — DeliveryDigest covers stored-form fields 01–13, but the additive stored/effective provenance members outside those fields are not required to be derived from or compared with signed evidence before handler routing. Altered provenance can be accepted with an unchanged signed digest. | bad_spec |
| EC6-6 | high — Same global-versus-tenant MessageId reservation contradiction as BH6-11; cross-tenant reuse is invisible to a tenant-scoped index. | bad_spec |
| EC6-7 | high — The new read rule rejects non-NFC metadata, while `EventPersister` persists producer strings such as `UserId` and `DomainServiceVersion` without NFC normalization. Previously admitted v1 history can become unreadable. | bad_spec |
| VG6-1 | low — `epics.md` still says 6.5 is backlog and the artifact absent, and says 6.6 is unauthorized because it is absent. The tracker now says 6.5 is in progress and the draft exists; its missing human approval is the current 6.6 gate. | patch (moot after bad_spec) |
| BH7-1 | high — `ReplayEventEnvelope.MetadataVersion` is already a required constructor member and subscription `GlobalPosition`, `CausationId`, `UserId` already exist; proposing nullable additions with the same names creates an impossible additive API. | bad_spec |
| BH7-2 | high — `DeliveryDigest` hashes publication extensions, but the subscription DTO has no declared readable `Extensions` member; the subscriber cannot recompute it. | bad_spec |
| BH7-3 | high — `MapEventStoreDomainEvents` currently passes only a deserialized envelope to the processor; the draft needs a verified inbound transport context for topic/component/CloudEvent attributes/headers. | bad_spec |
| BH7-4 | high — “All routing-relevant headers” has no exact allowlist, casing, duplicate or broker-added-header treatment; publisher/subscriber can hash different maps for one delivery. | bad_spec |
| BH7-5 | medium — Registry `V` records bind validators but no serializer/options/format for an older canonical V2 payload version; after current version advances, source decoding can diverge under equal fingerprints. | bad_spec |
| BH7-6 | high — Signer/verifier config IDs do not necessarily bind the actual public-key trust map, purpose assignments or revocation state; equal fingerprints can accept different route claims. | bad_spec |
| BH7-7 | medium — Static V1 `FromDomainResult` allocates the whole result before gateway validation, while the draft gives only V2 a numeric event-count cap. Configurable `MaxEventsPerResult` alone does not bound upstream allocation. | bad_spec |
| BH7-8 | medium — The 64 MiB legacy array/page budget counts readable payload only; 100,000 envelopes with bounded but sizeable metadata/extensions and object overhead can exceed the claimed 128 MiB live-scratch budget. | bad_spec |
| BH7-9 | high — MessageIds are generated during append and the global CAS reservation is only required at subscriber admission. A duplicate ID can commit immutable conflicting history before it is detected. | bad_spec |
| BH7-10 | high — The global reservation specifies a key but no one authoritative cross-service store or CAS consistency contract; separate consumers could admit conflicting identities. | bad_spec |
| BH7-11 | high — Marker/effect identity includes a handler route but its stable ID and migration across CLR/registration renames are undefined, allowing a changed route to bypass a prior receipt. | bad_spec |
| BH7-12 | high — Per-event attestation does not bind the requested range/head/count/order; `AggregateReconstructionRequest.UpToSequence` exists but `AggregateReplayer` does not require its final event to reach that target, so a truncated valid signed set can be Applied. | bad_spec |
| EC7-1 | high — `AggregateTerminated` is a persisted `IRejectionEvent` without `IEventContract`; unconditional V2 writer checks would reject a valid framework tombstone rejection. | bad_spec |
| EC7-2 | medium — Missing mode implies V1 but the router's fingerprint verification lacks an explicit both-absent legacy request rule, leaving old callers vulnerable to rejection. | bad_spec |
| EC7-3 | high — An unknown signing key fails subscriber verification, but the draft lacks a verifier-first key rotation readiness gate or retry disposition for a new key's valid deliveries. | bad_spec |
| EC7-4 | medium — `CoordinatedCommandActor` now reads the target with `GetEventsAsync(0).Any` for non-fenced retry; the spec names only its source read, leaving an unbounded target bypass. | bad_spec |
| BH8-1 | medium — The receipt has a digest and typed name but no separately verifiable human approval source; an edited receipt alone cannot establish human authorship of authorization. | bad_spec |
| BH8-2 | high — The manifest codec gives record order but no complete per-record tags, keys or optional-field encoding; two implementations can fingerprint the same descriptors differently. | bad_spec |
| BH8-3 | high — Route/delivery/prefix signature claims list values but omit exact field tags and encodings, so independently deployed signers and verifiers can disagree. | bad_spec |
| BH8-4 | high — Prefix attestation names an ordered digest-list hash but no domain separator, count or concatenation bytes. A shortened/reordered list can be verified inconsistently. | bad_spec |
| BH8-5 | high — A tail projection beginning at checkpoint+1 has no authenticated checkpoint anchor, so the current complete-prefix rule either rejects valid incremental delivery or silently trusts an omitted prefix. | bad_spec |
| BH8-6 | high — Existing protection API returns a complete `byte[]`; checking readable size after unprotect cannot enforce the claimed preallocation ceiling. | bad_spec |
| BH8-7 | medium — An arbitrary registered upcaster can allocate internally outside the supplied output writer, so its whole-process hard scratch promise lacks an enforceable boundary. | bad_spec |
| BH8-8 | high — The chain scratch limit is 64 MiB and includes source plus hop buffers, making a permitted 64 MiB legacy source unable to start any hop or validator. | bad_spec |
| BH8-9 | high — V1 allows 1,000 events at up to 64 MiB each with no total result-byte cap, allowing roughly 64 GiB to materialize in one result. | bad_spec |
| BH8-10 | medium — SnapshotDigest binds protection state/version/format but omits `Scheme`, `KeyAlias`, `ContentHint` and `CompatibilityFlags`; changed protection metadata can pass the witness. | bad_spec |
| BH8-11 | high — `TrustMapDigest` changes the domain fingerprint as verifiers add a key, while rollout demands one current fingerprint across readers/publishers; signing transition needs an explicit write fence/activation state. | bad_spec |
| BH8-12 | high — A predeclared route filter may terminalize valid no-handler delivery, but its scope, stable registration and fingerprint binding are unspecified, leaving an unaudited skip path. | bad_spec |
| EC8-1 | high — Same V1 total-result byte cap omission as BH8-9; 1,000 near-64 MiB events exceed any bounded wire allocation. | bad_spec |
| EC8-2 | high — `AggregateReconstructionRequest.IncludeTimeline` can add a serialized state per event; the draft caps input history but no cumulative timeline output bytes. | bad_spec |
| EC8-3 | high — The subscription overlimit row permits hold or quarantine “by deterministic source classification” but gives no rule selecting one, so peers may disagree on acknowledgement. | bad_spec |
| EC8-4 | high — V/A records name identity policy/extractor but do not hash the extractor implementation bytes, allowing different payload identity behavior under one fingerprint. | bad_spec |
| VG8-1 | low — The approval paragraph says the sprint tracker changes only after human receipt, while the story is correctly marked `in-progress` now. Narrow the sentence to completion/6.6 authorization. | patch (moot after bad_spec) |

| BH9-1 | high — `AggregateActor` command current-state input and `DomainProcessorStateRehydrator` are active Apply paths but §3 only attests replay/projection; commands can apply unverified history. | bad_spec |
| BH9-2 | high — No folded snapshot runtime exists, and a command stream beyond the 100,000-event/256 MiB legacy array cap has no supported rehydration path. | bad_spec |
| BH9-3 | medium — `DomainProcessorStateRehydrator` loops synchronously without a token; a long command replay can continue after cancellation. | bad_spec |
| BH9-4 | high — Whole-prefix SHA-256 list hash cannot be extended from a bounded tail, so each incremental checkpoint needs a full reread. | bad_spec |
| BH9-5 | high — `StateCheckpointReadbackHash` lacks canonical state/checkpoint bytes and version; independent nodes cannot reproduce it. | bad_spec |
| BH9-6 | high — S includes consumer-specific `HandlerCatalogFingerprint`, so publishers and subscribers with different handlers cannot share the claimed domain fingerprint. | bad_spec |
| BH9-7 | high — E row omits edge serializer assembly-file hash; equal registry fingerprints can encode different hop bytes. | bad_spec |
| BH9-8 | high — D row omits state serializer assembly-file hash; equal registry fingerprints can encode different folded state. | bad_spec |
| BH9-9 | high — Route claim omits consumed timestamp/global position/correlation/user metadata and source lookup is conditional; altered DTO metadata can affect projection while signature verifies. | bad_spec |
| BH9-10 | high — Effect reservation checked on retry cannot distinguish crash before effect from crash after effect before receipt; the allowed alternative permits duplicate effects. | bad_spec |
| BH9-11 | high — `StateSchemaApplyHash` is included in snapshot digest without canonical input records, preventing independent verification. | bad_spec |
| BH9-12 | medium — Existing malformed `eventstore.protection` values become `ProviderOpaque`, but digest rule does not bind raw extension and derived protection record consistently. | bad_spec |
| EC9-1 | high — Same unsigned consumed route metadata as BH9-9; projection fingerprint can diverge under a valid route signature. | bad_spec |
| EC9-2 | medium — `DateTimeOffset` preserves an original offset, while T hashes UTC ticks only; distinct stored timestamps may collide in digest and diverge in serialized rebuild evidence. | bad_spec |
| EC9-3 | high — Same non-extendable whole-prefix hash as BH9-4; bounded checkpoint advancement is impossible from tail alone. | bad_spec |
| EC9-4 | high — Expired signing keys fail verification even for pinned queued deliveries; retaining key bytes alone does not keep old obligations verifiable. | bad_spec |

| BH10-1 | high — Checkpoint readback digest includes durable checkpoint bytes that may contain its own signed hash and a post-write ETag; construction can be circular or impossible before save. | bad_spec |
| BH10-2 | high — V1 downserializer is allowed without implementation/options fingerprint or semantic-preservation proof; equal fingerprints may emit different V1 bytes. | bad_spec |
| BH10-3 | high — `EventUpcastResult` is named but `IEventUpcaster` public method/input/writer/token/registration contract is absent. | bad_spec |
| BH10-4 | high — V2 writer lacks total event-count and serialized-result bounds; a bounded per-event writer can still materialize an unbounded result. | bad_spec |
| BH10-5 | high — Map codec says tagged-length key/value without fixing tag bytes; independent digest implementations can diverge. | bad_spec |
| BH10-6 | medium — Duplicate JSON property rule names a pinned case-insensitive comparer without specifying `StringComparer.OrdinalIgnoreCase` behavior; admission can diverge. | bad_spec |
| BH10-7 | high — State/Apply descriptors live in each D event row with no same-aggregate equality rule; snapshot compatibility can conflict by event type. | bad_spec |
| BH10-8 | high — Unique historical MessageId alone cannot prove a legacy marker's route, delivery bytes or completed effect; route-specific migration can skip or duplicate work. | bad_spec |
| BH10-9 | high — Cancellation matrix omits before-send, ambiguous-send, acknowledgment and notification cleanup outcomes; retry truth is underdefined. | bad_spec |
| BH10-10 | high — Registered upcasters have no purity/determinism contract; same source and fingerprint can produce different effective bytes. | bad_spec |
| BH10-11 | medium — Snapshot digest includes trust-map-bearing RegistryFingerprint, so key rotation alone invalidates otherwise compatible folded state. | bad_spec |
| EC10-1 | high — New gateway always sends mode/fingerprint and requires echo, but old V1 endpoints cannot echo; reader-first rollout can reject valid V1 commands. | bad_spec |
| EC10-2 | high — DeliveryDigest includes exact raw CloudEvent body while DTO signature/digest fields may be in that body; without an external carrier or exclusion codec construction is circular. | bad_spec |
| EC10-3 | high — Same unsafe legacy marker migration as BH10-8; unique ID inventory does not prove route/effect identity. | bad_spec |
| EC10-4 | medium — First command on empty stream has no defined signed count-zero prefix/head-zero encoding. | bad_spec |

| BH11-1 | high — New storage and wire fields lack exact JSON names/casing at each boundary; independently deployed endpoints can disagree. | bad_spec |
| BH11-2 | high — Replay/projection requests lack declared carriers for route/prefix claims, signatures and key IDs; receivers cannot verify required proof. | bad_spec |
| BH11-3 | high — `IBoundedPayloadWriter` and `IBoundedScratchAllocator` lack exact methods, accounting and overflow behavior; upcaster contract is incomplete. | bad_spec |
| BH11-4 | high — F record gives distinct identity/semantic validator IDs one shared assembly hash and no separate options digests; equal fingerprints can hide changed behavior. | bad_spec |
| BH11-5 | high — Handler catalog fingerprint has no canonical field order/length/null codec; consumers can hash equivalent catalogs differently. | bad_spec |
| BH11-6 | high — Malformed protection fallback uses current metadata version; a parser upgrade changes digest for unchanged stored bytes. | bad_spec |
| BH11-7 | high — Snapshot witness lacks the event transform chain that produced folded state; changed upcaster can make snapshot differ from full replay. | bad_spec |
| BH11-8 | high — Checkpoint anchor lacks evolution/handler semantic compatibility; a new tail may be Applied to incompatible old state. | bad_spec |
| BH11-9 | high — Checkpoint readback verifies copied body bytes but not durable handler/read-model state; it can certify completion without the actual state. | bad_spec |
| BH11-10 | false — The 100,000-event maximum-metadata vector is required by prior design notes to hit `LegacyArrayLimit` before excess allocation; it is not claimed to succeed. | reject |
| BH11-11 | high — Historic-key expiry exception lacks exact durable pin obligation and verification checks; peers can disagree on admissibility. | bad_spec |
| BH11-12 | high — Equal reservation contract assembly hash does not prove writers/subscribers share one backend/namespace; duplicate MessageIds can commit in split stores. | bad_spec |
| EC11-1 | high — Same snapshot transform-chain omission as BH11-7; stale folded state can be accepted after upcaster change. | bad_spec |
| EC11-2 | high — Same checkpoint semantic compatibility omission as BH11-8; incremental state can diverge after handler/upcaster change. | bad_spec |
| EC11-3 | high — Same version-dependent malformed-protection fallback as BH11-6; unchanged event digest can change after deploy. | bad_spec |
| EC11-4 | high — Registry primary-key components and numeric versions are not individually length/type encoded; equal descriptors can produce different manifest bytes. | bad_spec |
| EC11-5 | high — F identity and semantic validators have only one assembly hash; implementation mismatch is invisible under equal fingerprints. | bad_spec |

| BH12-1 | high — StoredDigest is reserved before actor save but hashes provider/raw presence/protection bytes not yet fixed by the draft; reservation may bind a different final event. | bad_spec |
| BH12-2 | high — Singular `EventEvolutionProof.RouteClaim` covers one event while replay/projection requests carry multiple; later events lack matched proof. | bad_spec |
| BH12-3 | high — Handler catalog route record omits checkpoint serializer options/state schema descriptor that checkpoint verification claims it binds. | bad_spec |
| BH12-4 | high — Domain-wide `EventTransformHash` appends one aggregate-scoped StateSchemaApplyHash without selecting a route; multi-aggregate domains are ambiguous. | bad_spec |
| BH12-5 | medium — Domain-wide transform hash includes unrelated aggregates and write-only downserializers, invalidating unaffected state witnesses. | bad_spec |
| BH12-6 | high — Slice 2 says V2 can enable before slice 3 installs all command/projection/subscription consumers and effect proof; rollout step 3 demands the opposite. | bad_spec |
| BH12-7 | high — Shared reader requires stored attestation, but V1 stored events are unsigned; legacy source authentication and fresh route signing are undefined. | bad_spec |
| BH12-8 | high — Delivery attestation carrier is left as side metadata or envelope while DTO also names fields; raw body/content type cannot interoperate. | bad_spec |
| BH12-9 | high — Atomic effect+receipt mode lacks unique-key serializable/CAS exclusion; two concurrent deliveries can both run an effect. | bad_spec |
| BH12-10 | high — Old V1 endpoint may allocate unbounded wire result before gateway fence; old serving binaries need upgrade or upstream hard bound before eligibility. | bad_spec |
| BH12-11 | high — Separate handler state and checkpoint readbacks lack shared operation/version fence; individually valid reads may represent different logical commits. | bad_spec |
| BH12-12 | high — Checkpoint body/readback includes full state bytes without numeric ceiling or bounded hashing path, bypassing memory budgets. | bad_spec |
| EC12-1 | high — Same multi-aggregate transform-hash ambiguity as BH12-4; peers can derive different hashes. | bad_spec |
| EC12-2 | high — EventTransformHash omits protection adapter code/options; changed readable bytes can leave old snapshot/checkpoint apparently compatible. | bad_spec |
| EC12-3 | high — Same handler catalog checkpoint serializer/options/descriptor omission as BH12-3; incompatible state codec can retain fingerprint. | bad_spec |

| BH13-1 | high — `hx-ev-attestation` contains hyphens, which CloudEvents extension attribute names forbid; conforming transport may reject/strip it. Official core spec confirms ASCII lowercase letters/digits only. | bad_spec |
| BH13-2 | high — Binary extension's unpadded base64url JSON mapping conflicts with CloudEvents canonical Base64 mapping; standard receivers can decode differently. Official JSON format confirms Base64. | bad_spec |
| BH13-3 | high — Binary data lacks JSON `data_base64` mapping/decoding before digest; official JSON format requires that member for Binary payloads. | bad_spec |
| BH13-4 | high — EventTransformHash omits S envelope validator/chain executor/JSON options that can change replay behavior while snapshot remains accepted. | bad_spec |
| BH13-5 | high — Checkpoint claim binds trust-map-bearing RegistryFingerprint but key rotation continuity lacks prior-claim verification/re-sign procedure. | bad_spec |
| BH13-6 | high — Upcaster descriptor has edge identity policy but E manifest record omits it; equal fingerprints can validate differently. | bad_spec |
| BH13-7 | high — HandlerRouteId uniqueness scope `(domain,type)` conflicts with catalog primary key `(domain,routeId)`; one rule permits IDs another rejects. | bad_spec |
| BH13-8 | medium — WriterMode/RegistryFingerprint have no exact .NET types, allowed values or JSON encoding; exact echo may diverge. | bad_spec |
| BH13-9 | high — Snapshot witness hash is not tied to the `DomainServiceCurrentState.SnapshotState` object delivered to Apply; altered object can ride valid prefix proof. | bad_spec |
| BH13-10 | high — Paged command rehydration has no exact page request/response, continuation state or final proof handoff; long-stream route remains unimplementable. | bad_spec |
| BH13-11 | high — Raw subscription ingress has no numeric before-materialization body cap; broker delivery may allocate beyond event limits. | bad_spec |
| BH13-12 | high — Protection output API returns `byte[]` without preallocation cap; oversized protected event can allocate before raw-envelope rejection. | bad_spec |
| EC13-1 | high — Same invalid CloudEvents extension naming as BH13-1, confirmed against official spec. | bad_spec |
| EC13-2 | high — Same missing S semantic read fields in transform hash as BH13-4; replay behavior can change under same witness. | bad_spec |
| EC13-3 | high — Checkpoint handler-state source sequence can lag completed sequence unless equality is required before signing. | bad_spec |

| BH14-1 | high — `Server.Events.EventEnvelope` is the actual persisted carrier and lacks the additive canonical pair; adding only Contracts EventMetadata leaves nowhere to store it. | bad_spec |
| BH14-2 | medium — V1 version-1 wording conflicts with per-alias explicit source version and can misroute an alias for version 2+. | bad_spec |
| BH14-3 | high — Replay/projection DTO keeps Payload as stored bytes but has no named `EffectivePayload` for verified upcast bytes. | bad_spec |
| BH14-4 | high — Fixed 4,096-byte route claim cannot hold previously admitted long UserId/DomainServiceVersion/alias strings without a historical overlimit path. | bad_spec |
| BH14-5 | high — StoredDigest raw-presence bitmap lacks exact member order/packing/states; independent peers can hash one event differently. | bad_spec |
| BH14-6 | medium — A retained version-1 event cannot reach current version >17 with only 16 one-step hops; cutover needs a reachability/migration gate before advancing. | bad_spec |
| BH14-7 | high — Signed continuation token omits exact tags/count/bytes and purpose-05 claim codec; gateway/domain implementations can disagree. | bad_spec |
| BH14-8 | high — Final prefix proof lacks cross-page cumulative binding from prior page accumulator/token; a long command can accept incomplete history. | bad_spec |
| BH14-9 | medium — Whole consumer catalog hash invalidates unrelated checkpoints when an unrelated handler/filter changes. | bad_spec |
| BH14-10 | high — Delivery-shaped historic obligation cannot verify checkpoint signature covering many events after key expiry. | bad_spec |
| BH14-11 | false — The normative doc already states the legacy full-array adapter fills one array from successive authenticated pages and verifies the final prefix; no single 100,000-event proof request is required. | reject |
| BH14-12 | low — `epics.md` backlog clause contradicted in-progress tracker; changed it to permit in-progress draft while keeping completion/6.6 gated. | patch (moot after bad_spec; applied) |
| EC14-1 | high — Same 4,096-byte route claim overlimit for valid V1 metadata/alias as BH14-4. | bad_spec |
| EC14-2 | high — Explicit V1 mode-bearing request can echo a stale RegistryFingerprint because only V2 compares active capability; wrong alias/serializer may emit. | bad_spec |
| EC14-3 | medium — `hx-payload-version` signed routing value lacks exact decimal text grammar; peers can disagree on `01`, `+1` and `1`. | bad_spec |

| BH15-1 | low — Opening approval reference says §14 but gate is §12; update exact section reference. | patch (moot after bad_spec) |
| BH15-2 | high — V1 unknown raw JSON members are retained in digest without canonical field encoding; different records can collide or peers diverge. | bad_spec |
| BH15-3 | high — DeliveryDigest includes canonical raw body but body has no complete binary field schema; publisher/subscriber cannot reproduce bytes. | bad_spec |
| BH15-4 | high — TrustMapDigest omits exact separator/field tags/order/timestamp encoding; fingerprints can diverge. | bad_spec |
| BH15-5 | high — Selected S/protection-adapter bytes in EventTransformHash are not exactly encoded; state compatibility can diverge. | bad_spec |
| BH15-6 | high — Applicable filter-row selection for HandlerCompatibilityHash lacks exact route/topic/tenant predicate rule. | bad_spec |
| BH15-7 | high — Duplicate continuation token rejection lacks spent-token ledger and legitimate lost-response retry rule. | bad_spec |
| BH15-8 | high — Nullable ReadableExtensions DTO conflicts with mandatory M in DeliveryDigest; null and empty may hash inconsistently. | bad_spec |
| BH15-9 | medium — Snapshot compatibility flags may be null but protection M encoding lacks presence/null rule. | bad_spec |
| BH15-10 | medium — Public replay `FailedEventType` and timeline `EventTypeName` still map to CLR names without canonical compatibility rule. | bad_spec |
| BH15-11 | medium — Existing Type Catalog `EventTypeInfo.TypeName` and `SchemaVersion` lack mapping to canonical event type and payload version. | bad_spec |
| EC15-1 | high — Explicit JSON null extension value lacks M value discriminator; StoredDigest cannot represent admitted source consistently. | bad_spec |
| EC15-2 | high — Same unknown V1 member canonicalization gap as BH15-2; provider JSON spelling/order can alter evidence. | bad_spec |
| EC15-3 | high — Received CloudEvent core may conflict with stored MessageId/type/source without an explicit equality rule; routing/dedup may diverge. | bad_spec |
| EC15-4 | medium — Cancellation after Pending reservation but before save lacks a proven-no-commit release rule; ID may remain blocked. | bad_spec |
| EC15-5 | high — Legacy array accounting omits separately allocated EffectivePayload; 256 MiB limit can be exceeded. | bad_spec |
| EC15-6 | high — Authenticated permanent overlimit row says source `may` be AD-31 captured, allowing peers to ack or retry differently. | bad_spec |

| BH16-1 | high — Pin key includes DeliveryDigest, so a changed digest can create a second pin for the same MessageId; enforce a global MessageId CAS invariant. | bad_spec |
| BH16-2 | high — Legacy `CurrentState` accepts free typed/JSON state and can bypass signed source proof before command handling. | bad_spec |
| BH16-3 | medium — `IDomainProcessor.ProcessAsync` has no token; its internal rehydration cannot be interrupted between events as the draft claims. | bad_spec |
| BH16-4 | medium — New abstract members on public `ISerializedEventPayload` would break external implementers; use compatible defaults or a versioned carrier. | bad_spec |
| BH16-5 | high — Durable spent token plus volatile domain scratch can strand replay after a crash; define restart-from-source behavior. | bad_spec |
| BH16-6 | medium — A bounded token ledger has no numeric capacity, retention or full outcome, so implementations diverge and may grow without bound. | bad_spec |
| BH16-7 | high — Route/prefix proofs lack signing time or durable obligation, yet verification asks for the signing-key interval after rotation. | bad_spec |
| BH16-8 | medium — Fingerprint H hashes unspecified canonical option bytes; equal configurations can produce different hashes across peers. | bad_spec |
| BH16-9 | high — Recursive raw renderer lacks depth/node ceilings before typed admission, allowing stack/work exhaustion under a byte cap. | bad_spec |
| BH16-10 | medium — `U scope/store ID` is not a reproducible field tuple in the checkpoint readback hash. | bad_spec |
| BH16-11 | medium — Stored extension presence and readable restamping are not separated, so a publisher cannot pin/compare both representations consistently. | bad_spec |
| BH16-12 | medium — Codec/hash verification scenarios lack fixed input/expected-byte/digest vectors needed for independent implementations. | bad_spec |
| EC16-1 | high — A valid witnessed snapshot at nonempty head with zero tail has no signed completion form; the only empty-prefix form requires head zero. | bad_spec |
| EC16-2 | high — A page can start after snapshot sequence plus one without a snapshot-bound prefix rule, skipping source events before authoritative command state. | bad_spec |
| EC16-3 | medium — Failure while reserving a later batch ID leaves earlier Pending IDs without an explicit no-save cleanup path. | bad_spec |
| EC16-4 | high — Same reachable raw JSON depth/work exhaustion as BH16-9. | bad_spec |
| EC16-5 | high — Same spent-token/volatile-scratch crash gap as BH16-5. | bad_spec |
| EC16-6 | high — Same MessageId pin uniqueness gap as BH16-1. | bad_spec |
| EC16-7 | medium — S raw validator bytes need not include the renderer implementation, so one RegistryFingerprint can admit different StoredDigest renderers. | bad_spec |

| BH17-1 | high — Existing `AggregateReplayer` resolves `ReplayEventEnvelope.EventTypeName` and `Payload`, while the new DTO keeps both as stored; a migrated legacy adapter needs verified effective values or a zero-hop restriction. | bad_spec |
| BH17-2 | high — Invalid UTF-8/duplicate JSON prevents StoredDigest computation, but raw actor source can still be authenticated; specify raw-byte evidence and deterministic poison/hold disposition before parser success. | bad_spec |
| BH17-3 | high — A 128 MiB raw buffer exhausts the 128 MiB scratch limit that also counts validation and hop buffers; separate charged budgets or stream parsing. | bad_spec |
| BH17-4 | high — StoredDigest includes provider encoding ID without an immutable stored source, so later provider migration can change the digest of old bytes. | bad_spec |
| BH17-5 | medium — Protection record allows `Legacy` state although current `PayloadProtectionState` has only Unprotected/Protected/ProviderOpaque; missing extension maps to Unprotected with legacy flag. | bad_spec |
| BH17-6 | high — Required V1 `hx-event-contract-type`/`hx-payload-version` headers have no stored pair to compare; derive them from the registered alias and bind that mapping in signed routing headers. | bad_spec |
| BH17-7 | medium — Route/prefix old-key obligation has no durable lifecycle/limits. Treat those transient proofs as current-key only and reauthenticate/re-sign after rotation; retain exact historic obligations only for delivery/checkpoints. | bad_spec |
| BH17-8 | high — One state-key readback hash cannot witness all `ReadModelBatchOperation` writes for a named projection; define sorted batch-wide fenced readback before checkpoint. | bad_spec |
| BH17-9 | high — Slice 3 can activate a subscriber requiring committed reservations before slice 4 backfills old history; gate production activation on complete backfill. | bad_spec |
| BH17-10 | medium — Transform row selection from “stored types/versions” can vary with each peer's observed corpus; derive it from a fixed admitted-source manifest only. | bad_spec |
| BH17-11 | medium — Framing vectors lack admitted end-to-end route/prefix/checkpoint/delivery proof bytes and signatures, leaving independent consumer interop untested. | bad_spec |
| EC17-1 | high — “V2 requires current write descriptor” can be read as a read rule, rejecting valid older V2 history after current version advances. | bad_spec |
| EC17-2 | high — Exact page retry after committed transition but lost response is rejected, stranding command replay; return pinned page/next token without re-Apply while operation/response remain valid. | bad_spec |

| BH18-1 | high — EventEvolutionProof lists signed claims but lacks an exact container codec, entry count/order/key-ID/signature bytes for interop and size checking. | bad_spec |
| BH18-2 | high — Optional checkpoint anchor hash lacks a preimage, so a signed tail cannot bind one exact checkpoint. | bad_spec |
| BH18-3 | high — Existing `AggregateReconstructionRequest.Events` is a full array; paged command request/response and endpoint fields are not fixed despite the new async seam. | bad_spec |
| BH18-4 | high — 64 MiB retry ledger cannot retain an admitted 64 MiB readable page plus proof/encoding; define separate bounded pinned-response storage or shrink pages. | bad_spec |
| BH18-5 | high — Historical signed provider-encoding manifest has no format/purpose/trust/verification rule, leaving V1 StoredDigest provenance unauthenticated. | bad_spec |
| BH18-6 | high — Existing subscription JSON DTO binding cannot parse new binary body; queued flat JSON and new attested binary deliveries need explicit dual ingress. | bad_spec |
| BH18-7 | high — V2 publisher gate lacks proof that every subscribed consumer accepts the new binary body, risking delivery loss outside the domain peer set. | bad_spec |
| BH18-8 | high — Batch readback treats absent state as failure, but a successful `ReadModelBatchOperation.Delete` requires an authenticated absence witness. | bad_spec |
| BH18-9 | medium — Snapshot side record has a key and digest but no exact persisted schema/linkage/readback codec across mixed versions. | bad_spec |
| BH18-10 | medium — Snapshot prefix accumulator has no incremental actor-owned update/readback path; recomputing from sequence 1 on each fold defeats bounded snapshot work. | bad_spec |
| BH18-11 | high — Corpus proof of V1 downserializer does not validate each data-dependent produced payload/schema/identity/round-trip before append. | bad_spec |
| EC18-1 | medium — `IBoundedPayloadWriter.Complete` returns memory without immutable ownership/lifetime rule, so a later hop could read a reused buffer. | bad_spec |
| EC18-2 | medium — A historical V1 extension named with a case variant of `eventstore.protection` collides when current `Write` adds the exact key; hold/migrate rather than silently restamp conflicting keys. | bad_spec |

| BH19-1 | high — `DomainServiceRequest` adds only writer fields (§2), while §6 requires the router to reject free `CurrentState`; no signed proof carrier or lookup reference reaches the router. | bad_spec |
| BH19-2 | high — Existing `DomainServiceCurrentState.Events` is `EventEnvelope[]` and `DomainProcessorStateRehydrator` Applies those stored fields; §2 retains them without an effective-event command carrier, so a nonzero-hop command may Apply old payload. | bad_spec |
| BH19-3 | medium — §6 permits zero routes at nonempty head only with a snapshot; a projection already checkpointed at head needs a metadata-only completion with a checkpoint anchor and no snapshot. | bad_spec |
| BH19-4 | high — The paged request carries snapshot state/side record but no defined authenticated lookup or original v1 snapshot-storage bytes/ETag evidence for the receiver's required comparison. | bad_spec |
| BH19-5 | high — Delivery/checkpoint/encoding claims omit signed issuance time, while historic expired-key checks use a separately stored original signing time that a signer could backdate. | bad_spec |
| BH19-6 | high — §6 says reread handler state and checkpoint under a write fence across different stores but gives no shared fence token/transaction protocol, so concurrent state advance can yield a signed mixed pair. | bad_spec |
| BH19-7 | medium — §7/§10 require global historical MessageId backfill but do not select a disposition for valid retained IDs reused across scopes; cutover can be blocked indefinitely without an explicit collision gate/migration rule. | bad_spec |
| BH19-8 | medium — Reserving all batch IDs in order does not reject duplicate IDs inside one batch before staging; a repeated Pending record could be mistaken for the same operation's reservation. | bad_spec |
| BH19-9 | medium — Global committed ID reservations and durable publication pins have no capacity/fail-closed growth rule, so a full store can strand writes or jeopardize uniqueness. | bad_spec |
| BH19-10 | high — §5 hashes direct implementation assemblies but omits transitive loaded dependencies; peers with different validator/Apply dependency bytes can advertise one fingerprint. | bad_spec |
| BH19-11 | medium — `StoredDigest` includes original protection extension value without fixing decoded scalar UTF-8 versus JSON token bytes, contradicting the promised JSON spelling independence. | bad_spec |
| BH19-12 | high — §7 authenticates consumer capability but has no membership revision/fence or validity interval, so a joined/changed subscriber can receive unreadable binary after the check. | bad_spec |
| EC19-1 | high — Same missing command `CurrentState` proof carrier as BH19-1; the router has no exact signed state-proof field to validate before dispatch. | bad_spec |
| EC19-2 | high — Same unsigned issuance-time defect as BH19-5 for purpose-06 sidecars; expired-key provenance can be backdated without a bound time. | bad_spec |

| BH20-1 | high — Projection DTO `Payload`/`EventTypeName` stay stored, while `DomainProjectionDispatcher` forwards `ProjectionRequest` to `IAsyncDomainProjectionHandler` without a mandatory effective-event view; evolved history can reach handler as old bytes. | bad_spec |
| BH20-2 | high — `EventStoreDomainEventProcessor` currently resolves `EventTypeName` and deserializes `Payload`; added nullable effective fields do not require the subscription handler path to use verified effective payload after a hop. | bad_spec |
| BH20-3 | high — `EventTransformHash` row-count includes selected G rows but the concatenation omits their bytes, so dependent implementation drift does not change this semantic witness. | bad_spec |
| BH20-4 | high — §6 says preflight exact retry-response serialization before replay advances scratch, although final response bytes depend on replay output; the required ordering cannot be implemented. | bad_spec |
| BH20-5 | medium — §6 invalidates an operation when scratch is missing even if a completed final response is durably pinned; exact retry after lost final response should return pinned bytes without Apply. | bad_spec |
| BH20-6 | high — Legacy flat-JSON ingress reissues attested binary before effects but gives no durable handoff/ack rule; a crash between reissue and old ack can lose or duplicate work. | bad_spec |
| BH20-7 | high — Rollback retains V1 aliases/keys but does not fence a V1-only command endpoint after V2 events have been persisted, so commands may read unreadable history. | bad_spec |
| BH20-8 | medium — The only admitted byte-level fixture is unprotected zero-hop V1; exact V2 metadata, a real upcast and protected/ProviderOpaque evidence lack compact known-answer vectors. | bad_spec |
| BH20-9 | medium — Embedded verifier hashes supplied registry/transform row bytes without decoding row tags/counts, so internally malformed row bytes could pass if paired supplied hashes were updated. | bad_spec |
| BH20-10 | false — §12 specifies all six exact receipt fields and requires independent authenticated GitHub evidence fetch/actor/date/digest/sentence check; the digest command is explicitly labelled nonapproval. | rejected |
| EC20-1 | high — Legacy handler registration remains callable without a production activation guard proving a durable effect receipt; crash after effect before `Dispatched` cannot safely retry. | bad_spec |

| BH21-1 | high — §6 releases response quota and assumes no ledger transition after CAS/readback failure, but a CAS may commit before its acknowledgement is lost; replaying Apply can diverge from a pinned response. | bad_spec |
| BH21-2 | high — §6 writes handler state before checkpoint CAS and says the old checkpoint/state remain authoritative on failure; an in-place state overwrite can destroy last-good state despite a shared fence. | bad_spec |
| BH21-3 | high — 64 MiB state plus 64 MiB timeline and JSON/Base64 overhead can exceed the 128 MiB pinned response cap; page shrinking cannot reduce accumulated final output. | bad_spec |
| BH21-4 | medium — Exact final response retry promises a 24-hour pin yet also rejects an expired 15-minute continuation token; these require one explicit precedence rule. | bad_spec |
| BH21-5 | high — V1 StoredDigest admission requires encoding sidecars before reader cutover, but slice 1 activates V1 admission while slice 4 backfills sidecars. | bad_spec |
| BH21-6 | high — Legacy handoff CAS key includes source digest, so one broker identity with changed source evidence creates a different key instead of hitting `LegacyHandoffConflict`. | bad_spec |
| BH21-7 | high — New `IVerifiedDomainProjectionHandler` lacks exact DI discovery, route validation/catalog fingerprint and rebuild dispatch integration; a verified-only route could be invisible. | bad_spec |
| BH21-8 | high — Canonical subscription handler selection has no additive registration mapping from current CLR-name registry and effect contract, so an eligible current handler cannot be reliably selected. | bad_spec |
| BH21-9 | medium — `/replay-state/pages` permits a checkpoint-anchored projection zero-tail despite having no projection route or checkpoint state; that proof belongs only to projection completion. | bad_spec |
| BH21-10 | high — Binary capability leases prove decoding but omit duplicate-safe handling; legacy handoff or broker retry can replay one MessageId to external subscribers. | bad_spec |
| EC21-1 | high — A durable delivery pin may carry a prior approved fingerprint, while §2/§7 require its view route to use the active fingerprint; admission needs separate historic-pin verification and current source evolution. | bad_spec |
| EC21-2 | medium — Snapshot/checkpoint zero-tail sets start=k+1; a valid completed sequence k=`long.MaxValue` cannot be represented in signed `N` or paged request. | bad_spec |

| BH22-1 | high — Purpose-07 final proof contains only the final page's route entries; purpose-05 tokens and durable ledger bind stored accumulator but not earlier effective-event chain, so the router cannot verify the claimed full chain. | bad_spec |
| BH22-2 | medium — Raw renderer canonicalizes `-0` and `0` identically, while a V1 unknown-member decoder may observe raw token text; equal StoredDigest could then yield different semantics. | bad_spec |
| BH22-3 | high — G dependency roots enumerate D/V/A/E/F/S, but HandlerCompatibilityHash needs transitive handler/registration/receipt-provider dependencies absent from the manifest. | bad_spec |
| BH22-4 | high — Cross-store fence says both stores reject stale generations without defining a persisted compare-and-write operation; a stale writer can commit versioned state after losing the coordinator fence. | bad_spec |
| BH22-5 | high — Legacy handoff keys depend on a stable broker delivery identity that the spec neither derives nor requires the provider to supply across redelivery. | bad_spec |
| BH22-6 | high — Consumer lease and publication fence have no exact claim bytes, signing authority or revision comparison protocol; readiness can diverge between peers/sends. | bad_spec |
| BH22-7 | medium — V2 exact lower-camel writer names coexist with case-insensitive known-property binding; a single wrong-case property has no declared admission outcome. | bad_spec |
| BH22-8 | medium — `IBoundedScratchAllocator.Rent(int)` charges actual capacity before allocation but a pool may round up after the request; the promised preallocation bound is unenforceable without exact-size or reserved bucket policy. | bad_spec |
| BH22-9 | medium — Unknown option names fail readiness, yet no component option-schema authority/allow-list is defined, so independent peers cannot know what to reject. | bad_spec |
| BH22-10 | low — Additional fully signed V2/upcast/protected routes would mostly repeat the fixed signing codec already verified by the admitted V1 fixture; V20 literal source/hop/protection hashes cover the differing byte shapes. | rejected |
| EC22-1 | high — A final page may use the full 2 MiB command-page proof limit, leaving no bytes for purpose-07 claim/signature and outer framing; its completion cannot be admitted. | bad_spec |

| BH23-1 | high — Reader activation requires sidecars for all V1 events but lets old V1 producers append afterward without sidecars, immediately creating valid history the new fleet cannot read. | bad_spec |
| BH23-2 | high — Signed consumer lease/configuration claims omit the domain RegistryFingerprint required by the cutover gate, so an incompatible registry change can leave a superficially valid lease. | bad_spec |
| BH23-3 | high — Publisher checks lease expiry before `TryPublishAtRevision`, but broker acceptance only fences membership revision; a lease may expire before bytes are accepted. | bad_spec |
| BH23-4 | medium — Broker nonce is one-use but ambiguous send has no exact receipt lookup/idempotent repeat result by operation ID and pinned bytes, leaving acceptance uncertain. | bad_spec |
| BH23-5 | high — External duplicate-safety lease signs configuration/provider ID without a durable cross-retry receipt probe; a declared but nonworking provider can pass cutover. | bad_spec |
| BH23-6 | medium — Both CloudEvents transport modes decode to the same signed delivery, yet historic pin comparison demands one exact received transport byte sequence; legitimate broker mode conversion can falsely conflict. | bad_spec |
| BH23-7 | medium — Option schema defaults may be filled before or after `OptionsHash`, and the normative preimage does not fix which object is hashed for absent optional fields. | bad_spec |
| BH23-8 | medium — Pooled scratch may carry plaintext or cross-tenant bytes but has no clear-before-release/reuse rule; a later renter can observe prior payload. | bad_spec |
| BH23-9 | high — Ephemeral spool may contain readable/protected plaintext without an access/encryption/crash-cleanup lifecycle, creating an unmanaged copy of event data. | bad_spec |
| BH23-10 | high — Durable replay response blobs hold final state/timeline/proof for at least 24 hours but specify only size/retention, not tenant access, at-rest protection or obligation-based deletion. | bad_spec |
| BH23-11 | medium — New membership/lease/fence codecs have no literal known-answer bytes/signatures, leaving broker/publisher/consumer interop unverified. | bad_spec |
| EC23-1 | high — Same active legacy appender sidecar gap as BH23-1; inventory alone does not fence subsequent V1 writes. | bad_spec |
| EC23-2 | high — Same lease expiry race as BH23-3; the broker needs to check lease/fence validity at acceptance. | bad_spec |

| BH24-1 | high — Verified projection registration declares one canonical type/version although a page may contain several types; valid mixed streams have no attested route type set. | bad_spec |
| BH24-2 | high — Membership configuration encodes one route/type selector per consumer identity and forbids duplicate identities, so one broker subscription with multiple handlers cannot be represented. | bad_spec |
| BH24-3 | high — External duplicate probe uses only `(MessageId, DeliveryDigest)` while live effect identity also has scope and HandlerRouteId; the probe can pass a provider that duplicates route-specific effects. | bad_spec |
| BH24-4 | high — Queued legacy JSON proves a prior send, but first-pin recovery forbids deriving a pin after any prior send; no authenticated migration creates the required attested binary pin. | bad_spec |
| BH24-5 | high — Recovery derives a pin from a committed event but outbox evidence does not retain topic/component/source URI/routing configuration at commit, so later config changes can redirect that event. | bad_spec |
| BH24-6 | medium — Replay's complete request fingerprint has no byte codec/field list; altered proof, snapshot hint, timeline flag or request ID may collide with a pinned retry across peers. | bad_spec |
| BH24-7 | high — Additive `PagedContext` has no exact type/fields/ownership rule even though async replay must use it for verified page and continuation state. | bad_spec |
| BH24-8 | medium — Optional signed timeline chunks are promised as an oversize escape path but have no endpoint/ordering/proof/retry contract; remove the promise or define it. | bad_spec |
| BH24-9 | false — §7 explicitly requires a conforming broker `TryPublishAtRevision`/receipt contract and holds binary activation if unavailable; naming a current DAPR backend is not necessary to prevent unsafe cutover in Story 6.5. | rejected |
| BH24-10 | high — Offline V1 sidecar inventory says raw readback establishes original provider encoding ID, but raw bytes alone cannot prove which historical serializer/encoding was used. | bad_spec |
| BH24-11 | medium — Digest-covered frontmatter `status: unapproved-draft` would remain after valid receipt approval or invalidate that approval if edited. | bad_spec |
| EC24-1 | high — Same missing mixed-type projection route set as BH24-1; one page may contain several canonical event types. | bad_spec |
| EC24-2 | medium — A repeated append with an existing Committed MessageId and identical evidence has no explicit idempotent outcome/readback rule. | bad_spec |

| BH25-1 | high — One physical legacy JSON delivery may address several logical routes, but §7 permits old-message acknowledgement after one route's handoff rather than all required route records. | bad_spec |
| BH25-2 | high — Existing `IEventStoreDomainEventHandler<TEvent>` gives no effect transaction/receipt boundary; a provider ID alone cannot make arbitrary handler side effects atomic with the receipt. | bad_spec |
| BH25-3 | high — DurableDedup probe exercises successful duplicates/restart but omits crash after effect and before receipt, the failure path that could rerun an effect. | bad_spec |
| BH25-4 | high — Probe runs one route only; a provider deduplicating by MessageId alone can pass and suppress a second legitimate HandlerRouteId. | bad_spec |
| BH25-5 | medium — Single-type lease capability hash omits `HandlerCompatibilityHash`; route filter/state/handler changes can leave a valid old lease. | bad_spec |
| BH25-6 | high — Multi-type capability references internal 52/43/58 rows, while external logical members declare only route ID; their route set cannot be attested under the required hash. | bad_spec |
| BH25-7 | high — Paged replay keeps private nonserializable scratch in a domain instance but has no owner/affinity routing; load-balanced later pages can repeatedly lose it. | bad_spec |
| BH25-8 | medium — Exact retry fingerprint includes signature/proof bytes but ledger retains only proof hash; after crash a newly signed equivalent proof may fail to recover the original pinned final response. | bad_spec |
| BH25-9 | low — P1363 permits alternate high-S signatures for a claim, but this protocol intentionally identifies and pins **exact signed bytes**; retaining those bytes for retry resolves the practical mismatch without changing the accepted signature codec. | rejected |
| BH25-10 | high — Original actor result is stored only when reservation becomes Committed; a crash after actor save but before that transition leaves no exact result for Pending reconciliation/idempotent retry. | bad_spec |
| BH25-11 | medium — V17 option schema lists seven defaults without required/optional flags; verifier treats only converters as optional, so independent admission could disagree. | bad_spec |
| BH25-12 | medium — V1 probe is said insufficient for production, yet following wording still calls it the readiness probe; specify that only V2 receipt governs readiness. | bad_spec |

| BH26-1 | high — AppendOperationFingerprint hashes prepared raw bytes/result that can differ on a retry after timestamp/ID/position/protection generation; no stable precommit request lookup exists before recomputation. | bad_spec |
| BH26-2 | high — StoredDigest hashes a protection record derived by the current parser, so a decoder upgrade can change the digest of immutable history and break old pin comparison. | bad_spec |
| BH26-3 | high — `AddVerifiedDomainEventHandler` registers the old handler interface while production requires transactional/provider-idempotent callable interfaces; registration cannot guarantee an invocable safe route. | bad_spec |
| BH26-4 | medium — New effect handler signatures omit verified `EventStoreDomainEventContext` metadata used by existing handlers; migration can lose sequence/timestamp/correlation/causation/user/global position. | bad_spec |
| BH26-5 | high — Projection request may hold stored events, 64 MiB proof and repeated route/effective view bytes simultaneously with no combined 128 MiB scratch admission rule. | bad_spec |
| BH26-6 | high — Batch MessageId reservations transition one by one from Pending to Committed; a crash can leave sibling statuses mixed while one retry returns the full batch result. | bad_spec |
| BH26-7 | high — Named-projection batch readback checks each key but lacks a batch-wide version/pointer commit, so a failed checkpoint can expose partial new read models. | bad_spec |
| BH26-8 | high — Public replay records expose or require internal-only PagedContext/PagedProgress types across external domain assemblies; the declared additive API is not callable/compilable as specified. | bad_spec |
| BH26-9 | high — A handler can use another injected effect-capable service outside the transaction yet pass a narrow probe; route activation lacks a dependency audit/enforcement rule. | bad_spec |
| BH26-10 | medium — Checkpoint hashes state-store ID/codec but not backend identity/configuration; two deployments can advertise compatible witnesses while reading different stores. | bad_spec |
| BH26-11 | medium — Earlier wrapper Design Notes still say 64 MiB total upcaster scratch, contradicting normative 128 MiB charged scratch budget. | bad_spec |
| BH26-12 | medium — Earlier wrapper Design Notes still offer a separately provable alias discriminator, while later notes prohibit that unimplemented exception; remove stale alternative. | bad_spec |
| BH27-1 | false — §10 slice 1 already requires all retained V1 sidecars verified and every live V1 appender fenced or upgraded to sidecar-writing before reader activation; slice 2 is the later full bounded V1/V2 writer and transport dry run. | reject |
| BH27-2 | high — §4 requires a signed offline migration manifest and independent review but does not freeze its fields, canonical bytes, authority, or review evidence; two migrations could certify different historical encoding evidence. | bad_spec |
| BH27-3 | high — The immutable StoredDigest sidecar records decoder ID/version but no original decoder implementation digest; a later implementation with the same ID/version has no durable per-event hash to compare. The retained table/readiness hold needs a bound original hash. | bad_spec |
| BH27-4 | medium — `DaprBackupCommandService.ExportStreamAsync` reads and exports event pages including legacy type/payload yet is absent from the path inventory and safe identity/reader rules; its authorized export behavior needs an explicit validated migration path. | bad_spec |
| BH27-5 | high — §6 requires named-projection query readers to follow a checkpoint row-set pointer, but existing `DaprReadModelStore.GetAsync/GetManyAsync` accept only store/key and currently resolve a different batch marker; no route-context or pointer lookup seam is defined. | bad_spec |
| BH27-6 | high — VerifiedEffectiveEventView and command/replay counterparts expose mutable `byte[]`; verification before handing them to a handler does not freeze the applied bytes if a caller or shared owner mutates the array afterward. | bad_spec |
| BH27-7 | high — ExternalRouteCapabilityManifest binds the direct handler implementation digest but not the transitive loaded validator/effect/provider dependency graph required internally; an external dependency change can retain a valid capability hash. | bad_spec |
| BH27-8 | medium — V3 two-route probe has one effect mode/backend/configuration identity but does not define whether B is a provider-local test route or a live route; a different live provider cannot be represented, and probe scope could be misapplied. | bad_spec |
| BH27-9 | low — §7 freezes the full V3 receipt/transcript codec, requires authenticated provider signatures, independent broker queries and production probes; an additional literal V3 signature fixture would improve convenience but does not close a currently undefined readiness rule. | reject |
| BH27-10 | high — Projection pages permit 256 route claims of 1 MiB each under a 64 MiB proof cap, yet only command pages explicitly shrink/re-sign; a valid large-claim projection history may retry `ProofLimit` forever. | bad_spec |
| BH28-1 | high — `EventStoreDomainEventContext` is already a public unsealed record with a six-argument constructor and nullable `GlobalPosition`; §7's new sealed record/non-null field would break consumers. | bad_spec |
| BH28-2 | high — `DomainServiceWireEvent` currently has only type/payload/format, while §2 threads the canonical pair through it without an exact additive V2 `MetadataVersion` property/JSON mapping. | bad_spec |
| BH28-3 | high — Projection pointers/fences are per aggregate, but named/shared projections can write one logical read-model key from multiple aggregates; separate pointers cannot serialize or select one shared visible value. | bad_spec |
| BH28-4 | high — Route-aware query guards cover `GetAsync/GetManyAsync` but legacy `SaveAsync/TrySaveAsync` can still mutate a versioned named-projection logical key outside shadow/pointer commit. | bad_spec |
| BH28-5 | high — New physical shadow rows obscure the old visible logical `ExpectedETag`/idempotent-absence policy; comparing against a fresh row permits a stale logical write. | bad_spec |
| BH28-6 | high — Existing `ReadModelBatchOperation.TimeToLive` begins when the final write becomes visible; a shadow row can expire before/while a committed pointer still references it unless logical TTL and physical retention are separated. | bad_spec |
| BH28-7 | high — `BatchStateReadbackHash` covers changed rows, but a checkpoint claims a complete row set without a canonical full-membership/version root; an unchanged row can be omitted or substituted. | bad_spec |
| BH28-8 | high — Route-aware single-key query verifies a complete member list without a bound or indexed proof, so a large projection can force unbounded read/memory/fence time for one key. | bad_spec |
| BH28-9 | medium — Mandatory checkpoint-body state bytes/serializer remain single-state-shaped for named row-set batches; the exact row-set representation and pointer binding are missing. | bad_spec |
| BH28-10 | high — Batch root is created before per-ID reservations, but no durable Ready phase proves every ID index exists before actor save; partial reservation recovery is ambiguous. | bad_spec |
| BH28-11 | high — Historic reads require offline migration source/review recheck without retention for source evidence, reviewed manifest and signatures through the event's readable lifetime. | bad_spec |
| BH28-12 | high — GitHub issue/review comment IDs and URLs are not immutable body evidence; edits can change the claimed approval after the digest gate passes. | bad_spec |
| EC28-1 | high — A witnessed v1 snapshot can store protected State bytes; §6 extracts folded bytes directly from storage and omits typed unprotection/readability handling before witness validation/deserialization. | bad_spec |
| EC28-2 | high — `GetManyForRouteAsync` and complete row-set proof have no numeric key/byte/time admission bound, permitting excessive memory and shared-fence occupancy. | bad_spec |
| BH29-1 | high — Source progress includes the newly signed checkpoint hash, while the checkpoint signs state bytes containing ProgressHash; neither can be constructed first. | bad_spec |
| BH29-2 | high — Sorting shared writes by per-stream sequence/aggregate ID after arrival cannot make an earlier committed higher-order result deterministic when a lower-order event arrives later. | bad_spec |
| BH29-3 | high — §6 says `IdempotentAbsent` succeeds only on logical absence, but the existing delete policy also succeeds on a present key by deleting it; valid batches would be rejected. | bad_spec |
| BH29-4 | high — Production logical-operation witness omits `ReadModelBatchOperation.ValueTypeName`, which the existing batch fingerprint binds; equal JSON bytes for different value types could be accepted as one retry. | bad_spec |
| BH29-5 | high — Key-space ownership/mode changes affect root interpretation but are not bound into HandlerCompatibilityHash or a migration rule, allowing an unchanged checkpoint hash across incompatible catalog changes. | bad_spec |
| BH29-6 | high — TTL expiry requires a new root/checkpoint without a source event, yet no same-sequence maintenance transition, operation identity, signing or retry rule exists. | bad_spec |
| BH29-7 | high — `SharedProjectionEpochCoordinator` still calls legacy `IReadModelStore` on physical generation keys; the new guard would block it or leave a bypass unless a migration seam is specified. | bad_spec |
| BH29-8 | high — Backup export says it charges total content to an export cap but gives no numeric cap/preallocation rule; the existing exporter materializes one list and content string after only an event-count limit. | bad_spec |
| BH29-9 | high — Committed outbox routing intent omits optional CloudEvent subject/time and the exact configuration bytes needed to derive them; a first pin after crash can differ after configuration drift. | bad_spec |
| BH29-10 | high — `IBoundedPayloadWriter.Complete(): ReadOnlyMemory<byte>` exposes a potentially mutable backing array to the upcaster while claiming exclusive pipeline ownership; a retained alias can change validated bytes. | bad_spec |
| BH29-11 | medium — The literal zero-row `EventTransformHash` primitive vector conflicts with the required S-read/protection-adapter rows and fixture rejection rule; label it inadmissible or replace it. | bad_spec |
| EC29-1 | high — A lower-sorted shared-key source write may arrive after a higher-sorted commit; the specified order needs a global ordinal or hold/rebuild policy, independently corroborating BH29-2. | bad_spec |
| BH30-1 | medium — Terminal **zero-tail** vector says `count=1` or a route at `long.MaxValue` fails, which reads as rejecting a valid ordinary one-event page ending at that sequence. | bad_spec |
| BH30-2 | high — Named source checkpoint is signed before root CAS, but its state readback hash includes the root's resulting provider ETag, unavailable until that CAS unless explicitly reserved. | bad_spec |
| BH30-3 | high — External `IAsyncAggregateReplay` receives an opaque scratch token without a public bounded way to read prior private state or write successor state; multi-page Apply cannot be implemented. | bad_spec |
| BH30-4 | high — `IVerifiedDomainProjectionHandler` still receives `ProjectionRequest.Events` stored payload; prose saying Apply uses verified views cannot prevent an external handler from applying stored bytes. | bad_spec |
| BH30-5 | high — Snapshot witness fallback says replay from page 1 although the admitted request has only the post-snapshot page; the gateway must issue a fresh authenticated page-1 request before any Apply. | bad_spec |
| BH30-6 | high — Append preparation capsule stores all prepared bytes/outbox/result with no aggregate encoded capsule/batch cap or streaming ownership rule, despite preallocation budgets. | bad_spec |
| BH30-7 | high — Million-entry named row/progress indexes lack total encoded construction/storage and per-entry/key limits; a root can exceed 128 MiB scratch before query proof limits apply. | bad_spec |
| BH30-8 | high — Proved-delete tombstones are retained as complete row-set members without a safe prune/compaction rule, so repeated create/delete can exhaust the row-count cap. | bad_spec |
| BH30-9 | high — TTL maintenance uses an aggregate-scoped source checkpoint although a shared key can have many contributing aggregates; no source owns that same-sequence transition. | bad_spec |
| BH30-10 | high — TTL expiry requires a maintenance root but no scheduler/liveness rule or fail-closed query outcome while pending; expired logical values may remain visible indefinitely. | bad_spec |
| EC30-1 | high — Multi-source shared-key TTL expiry has no designated source for the required progress intent; independently corroborates BH30-9. | bad_spec |
| EC30-2 | high — A query could return an expired value before the maintenance root commits; independently corroborates BH30-10 and requires a typed hold rather than stale data. | bad_spec |
| BH31-1 | high — Existing `CommandEnvelope` has stable MessageId but no OperationId; without an explicit mapping, a new retry ID bypasses the durable preparation capsule. | bad_spec |
| BH31-2 | high — Current `AggregateActor` completes no-op results without event persistence; per-event reservation/bundle rules do not define durable exact-result retry for zero events. | bad_spec |
| BH31-3 | medium — A custom upcaster's `ReadOnlyMemory<byte>` input could expose a writable pointer through `Pin` or other alias if the private manager does not reject it; recheck input bytes after the call. | bad_spec |
| BH31-4 | high — Streaming the full million-entry row/progress index per candidate still costs O(total entries) for one event; incremental immutable index updates and a per-event work bound are missing. | bad_spec |
| BH31-5 | high — Query rechecks root pointer but not trusted UTC before return, permitting an unchanged root's value to expire between the initial TTL check and return. | bad_spec |
| BH31-6 | high — Physical-retention renewal may change provider ETag/bytes already hashed into a committed Merkle leaf; separate retention metadata or ETag-preserving renewal is required. | bad_spec |
| BH31-7 | high — Postcommit certificate may be missing after root CAS/crash; readers need a previous-certified-root selection and an exact recovery path without a second visibility decision. | bad_spec |
| BH31-8 | high — Pin-byte reclamation retains only global ID and event digest, insufficient to compare exact signed body/transport bytes on late broker redelivery. | bad_spec |
| BH31-9 | high — Registry/handler manifests have unbounded row counts and aggregate encoded bytes while startup sorts and traverses them; readiness needs numeric parsing limits. | bad_spec |
| BH31-10 | low — The request for a literal signed V3 carrier repeats BH27-9; §7 already fixes the V3 codec, authenticated transcript/signature, independent broker provider queries and production crash/two-route probe, so a literal signature is optional convenience rather than an undefined readiness contract. | reject |
| BH31-11 | false — `ReadModelBatchOperation.Delete` rejects an empty `ExpectedETag` in the existing public API; §6 preserves that API and explicitly marks `IdempotentAbsent` delete-only, while create-only empty ETag applies to admitted writes. | reject |
| BH32-1 | high — `ReadOnlyMemory<byte>` over a custom manager still exposes its writable `MemoryManager` through `MemoryMarshal.TryGetMemoryManager`; Pin rejection and post-call hash cannot prevent temporary mutation restored before the check. | bad_spec |
| BH32-2 | high — Preparation CAS is keyed by command MessageId alone, but `CommandEnvelope` validates only nonblankness and different scopes can reuse it; two valid commands could collide before actor admission. | bad_spec |
| BH32-3 | high — The logical-operation witness requires OperationId for each `ReadModelBatchOperation`, which has no such member; the stable enclosing `ReadModelBatchScope.BatchId` needs an explicit mapping. | bad_spec |
| BH32-4 | high — TTL job identity contains old root hash, so an intervening root can stale a scheduled job; no supersession/rebase procedure ensures expiry progresses. | bad_spec |
| BH32-5 | high — Proof-page reduction is explicit for projection and command pages, but stream/Admin/trace/backup readers using the same bounded proof pipeline can encounter valid large-claim pages without a shrink/re-sign rule. | bad_spec |
| BH32-6 | medium — Older wrapper marker note permits Completed/Dispatched grandfathering with actor evidence alone, weaker than normative route receipt/outbox/catalog proof. | bad_spec |
| BH32-7 | medium — Older wrapper upcaster note still says `Complete(): ReadOnlyMemory<byte>` despite the normative sealed-output `Complete(): void`. | bad_spec |
| BH32-8 | medium — Older wrapper delivery note uses `hx-ev-attestation` base64url while normative transport uses `hyevattestation` padded Base64. | bad_spec |
| BH32-9 | medium — Older wrapper state note includes write-only F in EventTransformHash while normative read hash excludes F. | bad_spec |
| BH32-10 | medium — Older wrapper TTL note calls for purpose-04 maintenance checkpoint while normative route-scoped maintenance uses purpose `0f` and leaves source checkpoints unchanged. | bad_spec |
| BH32-11 | medium — Older wrapper compaction note uses adjacent-member absence proof while normative sparse tree requires authenticated empty path. | bad_spec |
| BH32-12 | medium — Older wrapper no-handler note permits AD-31 terminal quarantine, while normative valid addressed no-handler traffic returns HandlerCapabilityMismatch without acknowledgement. | bad_spec |
| EC32-1 | high — `MemoryMarshal.TryGetMemoryManager` exposes writable input even with Pin forbidden, independently corroborating BH32-1. | bad_spec |
| EC32-2 | medium — V23 Node verifier parses delivery/effect digest fields but does not compare them to literal `V23DeliveryDigest` and `V23EffectReceiptDigest`; mismatched fixture evidence can pass. | bad_spec |
| BH33-1 | false — Epic 6 isolates projection **cost optimization** from upcasting; the named-projection visibility/query protocol is a future 6.6 correctness condition for effective-event checkpoints, with no runtime change in 6.5 and no new 6.4 dependency. | reject |
| BH33-2 | false — §7 explicitly holds binary publishing with `ConsumerMembershipFenceUnavailable` if no broker adapter provides atomic `TryPublishAtRevision`; naming an unproven deployable backend would weaken that gate. | reject |
| BH33-3 | false — §6 explicitly requires a shared serializable provider fence/certified visibility time and returns `CheckpointFenceUnavailable` when unsupported; no provider is asserted compatible without a capability probe. | reject |
| BH33-4 | high — Existing `EventStreamReader` loads typed envelopes; raw provider bytes/encoding evidence are universal prerequisites but the additive bounded raw-source API and provider probe are not frozen. | bad_spec |
| BH33-5 | high — Optional F downserializer has no callable bounded-writer/output contract parallel to the upcaster, so a V1 write may allocate unbounded bytes before validation. | bad_spec |
| BH33-6 | high — Intermediate paged replay returns `PagedProgress` through `AggregateReconstructionResult` without exact Status/StateJson/ErrorCategory/Timeline rules; an old consumer could treat partial state as final success. | bad_spec |
| BH33-7 | low — The shared domain RegistryFingerprint intentionally includes all G closure for fleet capability parity, while route-specific HandlerCompatibilityHash and EventTransformHash select only affected rows; unrelated handler changes trigger renegotiation but not state replay. | reject |
| BH33-8 | high — Public `NamedProjectionReadScope` can be constructed by callers and passed to the additive store API; the store lacks an authenticated capability/authorization check independent of claimed fields. | bad_spec |
| BH33-9 | high — New versioned key-space prefix guards can match pre-existing unversioned read-model keys; cutover requires an inventory and explicit migration before enabling the guard. | bad_spec |
| BH33-10 | high — Backup export promises no partial artifact, but a direct bounded response stream can release earlier pages before later validation fails; stage then atomically release. | bad_spec |
| EC33-1 | high — Existing `DeadLetterEnvelopeParser` requires structured JSON `data` and cannot recover replay-safe identity from new binary/data_base64 delivery bodies. | bad_spec |
| EC33-2 | high — Existing dead-letter endpoint returns HTTP 200 for bodies above its default 1 MiB capture limit without durable capture; a valid bounded binary delivery can be lost. | bad_spec |
| BH34-1 | high — §7 names one `HandlerRouteId` at binary subscriber admission, while §7 also allows several logical members behind one physical subscription; one binary delivery needs a durable all-addressed-route result before physical acknowledgement. | bad_spec |
| BH34-2 | false — §3 already requires every live, retry, history, reconciliation and shared-rebuild entry to validate the complete proof and use the same verified-route selection; the gateway converts legacy transport into `VerifiedProjectionRequest`. Requiring separately named public counterparts for each internal method adds no distinct safety condition. | reject |
| BH34-3 | high — §3 specifies `Task<ProjectionResponse?>` for the full-replay dispatcher and `DomainProjectionHandlerResult` for its selected handler, but `ProjectionResponse` carries no status; a retryable/failed handler result must not become successful state or null/404. | bad_spec |
| BH34-4 | high — §6 guards old read-model writes and calls for an authorized route-aware writer, but the existing `ReadModelBatchScope` lacks route/key-space/fence and the draft freezes no callable writer seam to carry those values. | bad_spec |
| BH34-5 | high — §7 pins “original actor result bytes” before save, while `AggregateActor` can produce `PublishFailed` after the save; the draft must distinguish pinned domain/append output from the final command/publication outcome and exact retries of both. | bad_spec |
| BH34-6 | high — §2/§4 require a signed offline migration manifest to read retained V1 history, while new V1 writes atomically create encoding sidecars without such a manifest. The reader needs an authenticated, nonambiguous source-kind branch for each. | bad_spec |
| BH34-7 | high — Offline signer/reviewer trust maps determine V1 admission but are absent from advertised `TrustMapDigest`; peers with one domain fingerprint could disagree on valid retained history. Bind exact authority bytes and rotation to readiness. | bad_spec |
| BH34-8 | medium — §6 literally registers every finite leaf on each root; a one-key update can duplicate work for up to 1,000,000 leaves. Carry unchanged jobs forward incrementally and bound durable scheduler state. | bad_spec |
| BH34-9 | high — §6's shared ordinal log is required for replay but has no active-size, archival, retention or compaction proof; unbounded growth or premature deletion can break replay. | bad_spec |
| BH34-10 | high — §7's signed membership member-set and external route-set `B` lengths have no numeric pre-verification cap even though the closure itself is bounded; a malformed claim can allocate excessive input before signature verification. | bad_spec |
| BH34-11 | medium — §6 issues one durable query-capability record per query and expires it within five minutes, but gives no issuance quota, expiry cleanup or ambiguous deletion recovery; the index can fill under sustained reads. | bad_spec |
| BH34-12 | medium — §7 requires publication, subscriber and retry to recompute a hash over the entire actor bundle for each batch member; at the 512 MiB batch cap, duplicates can force repeated full-batch reads. Retain commit-time full readback but bind a bounded member inclusion proof to the committed root. | bad_spec |
| BH35-1 | high — §10 Slice 1 activates new V1 writers/readers while its described prerequisite covers encoding sidecars but not the stored-digest sidecar and origin record that §§4/7 require for every newly written V1 event. | bad_spec |
| BH35-2 | high — `AuthenticatedRawEvent` exposes raw and encoding bytes but not the stored-digest sidecar or V1 origin record now mandatory for V1 source authentication under the same head. | bad_spec |
| BH35-3 | high — `ReadbackProof:byte[]` binds listed values in prose but has no frozen encoding, backend trust anchor or verification call; an independent reader cannot verify the provider claim deterministically. | bad_spec |
| BH35-4 | high — V1 origin branch `01` cites an actor-save-certificate hash, but no durable certificate schema or provider verification path proves one save covered event, sidecars and result. | bad_spec |
| BH35-5 | high — Upcaster-facing `IMemoryOwner<byte>` scratch can retain `Memory` after invocation and disposal, potentially aliasing a reused cross-tenant pool owner despite the exclusive-return rule. | bad_spec |
| BH35-6 | high — A capability may grant 256 exact logical keys of up to 512 UTF-8 bytes, while its entire durable record is limited to 1 KiB; the only stated fallback, a prefix grant, can over-authorize. | bad_spec |
| BH35-7 | medium — Page 1 has null continuation, but the owner lease key requires the exact token digest with no absent-token encoding; absent and present-empty tokens must remain distinct. | bad_spec |
| BH35-8 | high — Outbox tag `12` is declared `B` while its discriminator is said to precede “that B value”; the two possible length-prefix placements change committed hashes and pins. | bad_spec |
| BH35-9 | high — §7 requires lowercase HTTP header rendering at ingress, although legal HTTP intermediaries can vary header-name case; compare canonical names and signed values while rejecting case-fold duplicates. | bad_spec |
| BH35-10 | medium — Paged request fingerprint includes stored DTO members but not additive effective/adaptation members; reject those input members explicitly before hashing so changed caller-supplied bytes cannot retrieve a pinned response. | bad_spec |
| BH35-11 | medium — Durable preparation capsules may be 512 MiB each and have no completed-operation reclamation/evidence substitution rule; ordinary traffic can exhaust append capacity indefinitely. | bad_spec |
| BH35-12 | medium — The consumer lease codec has no effect-mode field, yet the following prose requires every lease to repeat it; bind and verify the mode through the exact hashed configuration row without changing historical V23 carrier bytes. | bad_spec |
| BH36-1 | high — Slice 1 says the purpose-11 receipt is staged in the save it certifies, but resulting ETag/generation and all staged-record hashes are available only after that save; including the receipt also makes its record list circular. | bad_spec |
| BH36-2 | high — The named-root certificate is built before final pointer CAS yet binds an “actual resulting provider ETag” that could mean the still-unknown final pointer ETag; separate prepared-record and final-pointer evidence. | bad_spec |
| BH36-3 | high — The sole raw-source result contains four event records but omits the intent certificate and purpose-11 provider receipt that branch-01 V1 verification requires under the same generation. | bad_spec |
| BH36-4 | high — §7 requires exact-byte legacy JSON handoff manifest comparison after crash but gives that manifest no canonical field list/codec. | bad_spec |
| BH36-5 | high — The V3 broker probe hashes route-specific effect receipts, while `EventEffectReceipt` has only descriptive fields; different provider/verifier byte encodings could certify different effects. | bad_spec |
| BH36-6 | medium — Capsule retirement demands event batch/member roots, but a successful no-op deliberately has neither, leaving no-op preparation chunks without a release path. | bad_spec |
| BH36-7 | medium — Exact-key query grants place up to 256 KiB outside the 1 KiB capability record, while the tenant quota charges only index bytes; sustained grants can exhaust uncounted blob storage. | bad_spec |
| BH36-8 | high — The receiver pins exact signed paged requests, but a gateway restart may re-sign the same page and be rejected as changed bytes; gateway-side durable original request retrieval or an explicit restart result is missing. | bad_spec |
| BH36-9 | high — JSON-to-binary migration proves source/route identity but not byte-level equality of decoded queued JSON and the newly pinned readable event; a changed body could be handed off under the same ID. | bad_spec |
| BH36-10 | false — §6 intentionally caps hot plus archived shared ordinals at 64 GiB, retains every replay obligation and holds further writes before capacity is exceeded. Infinite retained history is not promised; a safe capacity hold is the stated deterministic outcome. | reject |
| BH36-11 | high — Slice 1 requires evidence-writing V1 producers before activation but Slice 2 names the bounded V1 writer; the minimal Slice-1 producer and its preallocation bounds must be explicit. | bad_spec |
| BH36-12 | false — §3 caps **pipeline-owned** scratch at 128 MiB, expressly says plugin-local allocation is outside that interface guarantee, and requires registration measurement plus a process memory budget. §8's hard preallocation rule applies to charged pipeline buffers, not arbitrary plugin allocations. | reject |
| EC36-1 | high — The edge review independently confirms BH36-3: raw admission needs authenticated intent-certificate and purpose-11 receipt bytes, not only event/encoding/digest/origin. | bad_spec |
| EC36-2 | high — Purpose-11 provider verification keys/intervals/backend bindings can rotate while branch-01 V1 history remains readable; no explicit retention obligation preserves their verification. | bad_spec |
| EC36-3 | medium — §§4/10 require digest sidecars/origins for every retained V1 event, but §4 also recognizes raw bytes too corrupt to compute StoredDigest; cutover needs an authenticated corrupt-source disposition without fabricated digest or Apply. | bad_spec |
| BH37-1 | open high — §4's pre-save intent certificate omits the member-root sidecar that §7 stages in the same actor save, so its intended-record list cannot prove the complete save. User requested no further iteration. | open |
| BH37-2 | open high — `DaprDomainServiceInvoker` deserializes a whole `DomainServiceWireResult` before the spec's response size admission can bound that allocation. User requested no further iteration. | open |
| BH37-3 | open medium — Paged replay accepts `includeTimeline=true` but pins only state/proof progress and exposes no authenticated accumulated timeline on incomplete pages. User requested no further iteration. | open |
| BH37-4 | open high — One allowed 64 MiB legacy event and 64 MiB prior state exhaust 128 MiB live scratch before proof/successor buffers, with no compositional admission or streaming path. User requested no further iteration. | open |
| BH37-5 | open high — `WriteSuccessorAsync(ReadOnlyMemory<byte>)` may retain caller-mutable array memory across its asynchronous save, so the committed successor could differ from validated bytes. User requested no further iteration. | open |
| BH37-6 | open medium — `RawCorruptDisposition` pins a stream head/ETag/proof that later valid appends change; a stable corrupt event can become a false conflict. User requested no further iteration. | open |
| BH37-7 | open medium — No-op capsule compaction cites a no-op checkpoint witness without exact canonical fields, key or same-save CAS/readback codec. User requested no further iteration. | open |
| BH37-8 | open medium — Command outcome `pending`/`failed`/`published` revisions lack a rule for whether an exact retry returns its original pinned response revision or current latest revision. User requested no further iteration. | open |
| BH37-9 | open medium — The older Loop-7 wrapper note says CloudEvent time uses UTC ticks, while the normative codec preserves its original offset, risking divergent delivery digests. User requested no further iteration. | open |
| BH37-10 | open medium — The older Loop-11 wrapper note ambiguously says a write-only F change both does not and does force replay; the normative transform hash excludes F. User requested no further iteration. | open |

