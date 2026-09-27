---
title: 'Story 6.5a: Event Contract, Writer, and Migration Evidence Spec'
type: 'feature'
created: '2026-09-27'
status: 'candidate'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

## Intent

Specify stable event identity, bounded writer admission, actor-owned evidence, and retained-history migration as a focused input to the single Story 6.5 AD-13 artifact. This is specification work, not runtime implementation or approval of Story 6.6.

## Boundaries & Constraints

Preserve immutable stored event bytes, message and sequence identity, current constructors and public package compatibility, actor save/readback authority, and the distinct new-writer and retained-offline V1 evidence branches. Epic 8's production protection engine is not a prerequisite. The existing normative draft remains unapproved until Story 6.5's exact-content human gate passes.

## Tasks & Acceptance

- [x] Inventory the contract, wire, serializer, append, actor, result, migration, and diagnostic paths that this slice owns.
- [x] Propose exact canonical metadata and registry descriptors, fingerprint and chain compatibility rules, numeric write/read ceilings, preallocation checks, and V1/V2 writer negotiation.
- [x] Propose provider-portable actor intent/commit evidence, sidecar/origin and retained-history admission, no-op witness and capsule retirement, and truthful command/publication outcome revisions.
- [x] Give `BH37-1`, `BH37-2`, `BH37-6`, `BH37-7`, and `BH37-8` explicit accepted or rejected dispositions with cross-links to the proposed normative sections and verification vectors.

**Acceptance Criteria:**

- Given new and retained V1 events and V2 writes, when the proposed contracts are checked, then the exact metadata, source-kind, bounded admission, same-save proof, no-op, failure, cancellation, and retry outcomes are deterministic without rewriting history or trusting caller assertions.
- Given a whole wire result or an ambiguous actor save, when size and evidence are checked, then allocation is bounded before materialization and committed truth is established by authenticated provider readback before any retry or public outcome.
- Given Story 6.5 integration, when this work is reviewed, then its section candidate and findings are ready for reconciliation with Stories 6.5b and 6.5c; this child story alone authorizes no runtime work.

## Verification

Review the candidate against the current source inventory and the exact `BH37-*` triage rows. Check positive, corrupt, oversized, stale, ambiguous-save, no-op, cancellation, and mixed-version vectors; keep the AD-13 receipt `UNAPPROVED`.

## Candidate scope and reading convention

This is a **section candidate**, prepared against repository commit `02cf007c9860326aa03b32a78541a00b5717bd4a`. It is not the normative AD-13 artifact or a human approval. `status: candidate` means ready for review and integration, not approved runtime delivery. [The Story 6.5 draft](spec-event-versioning-upcasting.md) and its six-field receipt remain unchanged and `UNAPPROVED`; Story 6.6 remains unauthorized. The five dispositions below apply to this candidate only. [The historical review log](story-6-5-review-triage.md) remains historical evidence, including its open rows.

Sections A1–A10 are proposed replacement/addition text for draft §§1–5, 7–11. A reference to a draft codec imports that exact codec, including separators, tag order, field count, bounds, signature purpose and rejection rules. It does not authorize a local alternative. Explicit candidate replacements below take precedence **inside this candidate only**. Unmentioned draft rules remain inputs to integration. V17, V20, V22 and V23 literal bytes, hashes and signatures in draft §11 are historical interoperability fixtures and must not be regenerated or edited to match a new production codec. In particular, purpose-06 encoding evidence and the V1 origin-branch codec are unchanged.

### A1. Source inventory and authority

Paths below are repository-relative; braces list individual files, not an unspecified implementation area. Current source has none of the proposed purpose-11 certificates, migration manifests or response pins merely because this document names them.

| Seam | Current source and behavior | Proposed Story 6.6 contract / candidate section |
| --- | --- | --- |
| Event identity | `src/Hexalith.EventStore.Contracts/Events/IEventContract.cs` supplies static `EventType`, static `Domain` and instance `AggregateId`. `src/Hexalith.EventStore.Client/Events/EventContractResolver.cs` uses `src/Hexalith.EventStore.Client/Conventions/NamingConventionEngine.cs` for kebab validation. No payload schema version exists here. | Explicit registered D/V/A/E/F descriptors; canonical identity independent of CLR name and software version (A2). |
| Public/stored metadata | `src/Hexalith.EventStore.Contracts/Events/EventMetadata.cs` has 15 positional members including required `int MetadataVersion`; `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs` has 17, including payload/extensions. | Add nullable init members without changing either constructor/deconstruction, original offset, identity or protection evidence (A2, A4). |
| Domain response writer | `src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs` serializes every payload with `SerializeToUtf8Bytes`, uses a CLR full name and allocates a complete list. `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs` calls `FromDomainResult` for normal and rejection results. | One bounded selected writer on both routes, preserving the old helper as explicit V1 compatibility only (A2–A3). |
| Wire and gateway ingress | `src/Hexalith.EventStore.Contracts/Commands/DomainServiceRequest.cs` has two positional arguments. `src/Hexalith.EventStore.Contracts/Events/ISerializedEventPayload.cs` has name/bytes/format only. `src/Hexalith.EventStore.Server/DomainServices/DaprDomainServiceInvoker.cs` calls default `SendAsync`, then `ReadFromJsonAsync<DomainServiceWireResult>`, maps wrappers, and only then calls `ValidateResponseLimits`. `DomainServiceOptions.cs` in that directory currently defaults to 1,000 events and 1 MiB/event; those checks are post-allocation. | Bound transport buffering, encoded bytes, tokens, Base64 and collection allocation before materialization; retain triplet presence through admission and lossless mapping (A3). The candidate's measured V1 ceiling is a future capability, not a description of today's defaults. |
| Append preparation | `src/Hexalith.EventStore.Server/Events/EventPersister.cs` serializes/protects complete buffers, allocates positions, generates IDs/time, writes metadata version 1, and stages event plus aggregate metadata. It deliberately never calls `SaveStateAsync`. | Validate before reservation, pin volatile values once in a durable capsule, include all exact save rows in acyclic evidence (A4–A5). |
| Actor commit/reconciliation | `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `ProcessCommandCoreAsync` stages events, possible snapshot, publication index, pipeline checkpoint and `event_batch_commit_witness`. `InspectEventBatchSaveFailureAsync` clears cache and compares pipeline/index/token. This is current recovery logic, not complete raw-byte provider certification. | Actor alone saves; bounded full-set readback plus separately issued post-save receipt, including metadata/index/snapshot/effect mutations actually in that save (A5). |
| No-op/terminal result | That actor's `DomainResult.IsNoOp` branch calls `CompleteTerminalAsync` with preserved `ResultPayload`. `src/Hexalith.EventStore.Server/Actors/{CommandProcessingResult,IdempotencyChecker}.cs` carries/checks current terminal results. | Same-save immutable no-op result and exact checkpoint witness, with no event reservation/outbox; separate immutable append result and public response pin (A7–A8). |
| Submission/idempotency | `src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs`, `Handle`, `CreateReplayResult`, `RequireExecutionIdentity`, unknown-outcome recovery: protected admission selects original `ExecutionMessageId`/`ExecutionCorrelationId`; Replay and reconciled recovery can return before normal actor routing, and currently rebuild results with the retry request correlation. | Every evidence-required Replay/Recoverable/Pending/unknown-outcome exit resolves original execution and A8 pin or truthful hold; no actor-only interception (A4, A8). |
| Public command response | `src/Hexalith.EventStore/Controllers/CommandsController.cs` authenticates, sanitizes extensions, dispatches, then builds 202 body plus absolute Location from result message ID and Retry-After=1. `ParseOptionalResultPayload` bounds/parses the optional payload. | Preserve the public shape and existing policy, but render committed execution replies once under A8; pin original execution message/correlation and original application Location rather than reconstructing from retry host/correlation. |
| Committed rejection response | `SubmitCommandHandler.Handle`/`ThrowDeterministicFailure` and `CreateReplayResult`/`ThrowDeterministicFailure` can exit through `DomainCommandRejectedException`. `src/Hexalith.EventStore/ErrorHandling/{DomainCommandRejectedExceptionHandler,DomainRejectionProblemCatalog}.cs` rebuilds ProblemDetails from the current middleware correlation, request path and catalog. | Apply the original bounded renderer and response pin before either normal or replay rejection exceptions can reconstruct a committed reply; retain the existing public rejection shape (A8, V22). |
| Status and correlation | `src/Hexalith.EventStore/Controllers/CommandStatusController.cs` searches only claimed tenants; direct message-primary records precede bounded correlation resolution, ambiguous matches return 409, then legacy-only fallback. `src/Hexalith.EventStore.Server/Commands/{ICommandStatusStore,DaprCommandStatusStore,ICommandCorrelationIndex}.cs`: current status is an advisory TTL row and a failed read may return null. `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs` defines PublishFailed as permanent, and the controller tells clients to stop polling terminal states. | Add private authenticated tenant/message-to-execution lookup and evidence-required classification before selecting outcome head; preserve authorization, ambiguity and legitimate legacy fallback without interpreting null/advisory success as authority (A8). |
| Raw source/protection | `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` loads typed state; `src/Hexalith.EventStore.Contracts/Events/EventEnvelope.cs` and `src/Hexalith.EventStore.Contracts/Security/EventStorePayloadProtectionMetadataCarrier.cs` normalize/read extension evidence. | Authenticate exact raw bytes and sidecars before normalization. Preserve existing no-op/legacy/protected readability; do not require Epic 8's optional production engine (A4, A6). |
| Publication and diagnostics | `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` currently unprotects and publishes; `src/Hexalith.EventStore/Controllers/{StreamsController,AdminStreamQueryController,AdminTraceQueryController,ReplayController}.cs` and `src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/EventTypeInfo.cs` are public evidence seams. | Publication receipts, not transport errors, determine outcome revisions. Diagnostics expose canonical type, stored/current version, sequence, reason, bound and safe hashes; never payload, CLR assembly identity, source-evidence content, secrets or provider internals (A8–A9). |
| Existing proof seams | `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorInfrastructureFailureTests.cs` has `EventBatchSaveCommitsThenThrows_DoesNotReinvokeDomainOrAppendADuplicateEvent`, replaced-witness, terminal-save and cancellation tests. `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs` checks metadata, contiguous sequence, no-op and `PersistEventsAsync_DoesNotCallSaveStateAsync`. | Future production-path vectors in A10 must inspect durable exact bytes and record sets at those boundaries. This story changes no tests and claims no implementation of those future vectors. |

The architecture basis is [AD-5, AD-6, AD-12 and AD-13](../planning-artifacts/architecture.md): admission precedes actor-owned mutation, stored identity is stable, high-risk evidence inspects durable state, and runtime evolution requires exact-content approval.

### A2. Canonical metadata, registry and compatibility

`EventContractType` is 1–64 ASCII bytes matching `^[a-z0-9]([a-z0-9-]*[a-z0-9])?$`; repeated interior hyphens are legal. `(Domain, EventContractType)` is unique under ordinal comparison. Payload versions are integer 1..1024; envelope `MetadataVersion` is independently 1 or 2. `DomainServiceVersion` remains software identity. V1's exact domain-scoped CLR alias resolves to its registered source payload version, including versions greater than one; it never defaults generically to payload version one. V2's stored discriminator equals its canonical type byte-for-byte.

Retain existing constructors, deconstruction, defaults, accessibility and member types. Add nullable init-only `EventContractType`/`eventContractType` and `PayloadVersion`/`payloadVersion` to both metadata records. Preserve required `EventMetadata.MetadataVersion` and `ReplayEventEnvelope.MetadataVersion` as `int`. Add default-null interface members `MetadataVersion`, `EventContractType`, `PayloadVersion` to `ISerializedEventPayload` so existing implementers still compile/load. Preserve `DomainServiceWireEvent(string EventTypeName, byte[] Payload, string SerializationFormat = "json")`; its three new init members have exact Web JSON names `metadataVersion`, `eventContractType`, `payloadVersion`. Serialized normal and rejection carriers, invoker mapping, persister, raw provider serializer and readback must preserve all three. No discriminator inferred from a CLR name becomes V2 evidence.

Add nullable `WriterMode`/`writerMode` and `RegistryFingerprint`/`registryFingerprint` to request/result without changing positional constructors. The fingerprint's wire form is exactly 64 lowercase hex characters; binary evidence uses its decoded B32. Compare addressed domain's authenticated active capability before invocation and the exact response echo before append. Only authenticated old peers with **both** fields absent use implicit V1/no echo; null, one-sided, stale or mismatched mode-bearing exchanges are `CapabilityMismatch`. Mode-bearing requests go only to endpoints advertising that mode and registry. The selected compatibility writer **omits both negotiation properties** when the admitted mode is implicit V1; additive nullable properties must have explicit null-ignore serialization or equivalent bounded writer logic on both request and result. A null property is not omission. A mode-bearing writer emits both exact non-null values even if a serializer default would otherwise suppress one. Never set a global null-ignore option that erases the event/protection presence distinctions. K06 fixes no-op response bytes: implicit V1 is `{"isRejection":false,"events":[],"resultPayload":null}`; mode-bearing V1 adds `,"writerMode":"V1","registryFingerprint":"` plus the 64 lowercase fingerprint characters plus `"` before the final brace. Request serialization has the same paired omission/emission rule while retaining its existing command/currentState members. Both explicit-null negotiation fields fail admission. The selected production writer handles both normal results and rejection adapters; `AggregateTerminated` needs an explicit per-domain registered adapter, as does any other rejection without `IEventContract`.

| Selected mode | Raw event admission before typed binding |
| --- | --- |
| V1 | Exact registered alias and format; the canonical pair is both absent or both explicit null; metadata version absent/null/1. Partial presence/value, metadata 2, unknown or ambiguous alias fails `MalformedMetadata`/`UnknownEventContract`. Retain historical case-insensitive known-property mapping and exact original spelling for evidence. |
| V2 | All three members present and non-null; metadata 2; canonical type and positive version equal the current D/V write descriptor; `eventTypeName` equals canonical type. Every known raw property, including registered nested metadata, uses declared lower-camel spelling. Wrong-case, partial or contradictory triplet fails `MalformedMetadata`. |

Both modes reject duplicate decoded names ordinally and with .NET `StringComparer.OrdinalIgnoreCase` at every JSON object depth **before** binding, including escaped spellings of a name. Reject invalid Unicode, non-object event roots and unsupported formats. Preserve raw presence and protection absent/null/empty distinctions through that decision. Validate addressed tenant/domain/aggregate ID/type against key, command, envelope and the version-specific payload identity extractor or explicitly registered `NoPayloadIdentity`. Stored sequence must be positive/contiguous; check signed-long overflow before adding count or allocating global positions. Do not impose GUID parsing on message or aggregate IDs.

Use the exact draft §5 descriptors: D/`44` (12 fields), V/`56` (10), A/`41` (15), E/`45` (12), F/`46` (15), S/`53` (31), and G/`47` (3). They bind current schema/Apply, per-version schema/format/identity, exact aliases, adjacent upcast edges, V1 downserialization, shared readers/serializers/signers and sealed managed/native dependency closure. Primary keys, ascending field tags, direct assembly-file hashes and expanded-default options hashes are the draft's exact codecs. No CLR auto-load or ambiguous alias fallback is admitted. Required options must exist; omitted optional and explicit equal defaults hash identically. Startup verifies the complete shared per-domain manifest and locally executed bytes before advertising it.

`RegistryFingerprint` remains the exact draft `HX-EV-REGISTRY-1` hash over sorted rows. `StateSchemaApplyHash` and `EventTransformHash` keep the draft §5 codecs: the latter includes applicable D/V/A/E, S-read, protection adapter and selected G closure, with row count equal to those actual rows; it excludes write-only F and trust-only changes. A change affecting historic F output still changes its admitted A/V/E read behavior when applicable. Changed read/Apply behavior invalidates compatible folded state and requires replay/rebuild; trust-only rotation requires new current attestations but does not itself change read semantics. Carry this rule to 6.5b/6.5c; do not copy a contradictory older wrapper note.

Every retained source version must reach current through one unambiguous adjacent, acyclic E chain of at most 16 hops, with schema/identity checked at every hop and output at most 1 MiB. Advancing current reruns that proof for the entire retained allow-list. V1 output from current bytes is legal only through an exact-schema `PrimaryWriteAlias` or registered bounded F with source-schema, identity and semantic round-trip validation; F output uses the declared alias source version. Missing/lossy F is `DownserializeRejected`, never a silent writer downgrade. Compatibility verification must compile old source and run an already compiled consumer against the additive package surface, including deconstruction and a third-party serialized-payload implementer.

### A3. Bounded producer and whole-response ingress

All numbers are bytes (`KiB=1024`, `MiB=1048576`) and hard maxima; a measured/configured provider may advertise a smaller value. Use checked addition/multiplication, charge actual rented capacity and framing before allocating, and never infer allocation safety from Content-Length or a successful response alone.

| Boundary | Ceiling and overflow outcome |
| --- | --- |
| V2 writer/result | 256 events; 1 MiB readable payload/event; 16 MiB **entire encoded result**, including JSON names, escaping, Base64, result payload, echo and framing. `PayloadLimit` / `ResultLimit`. |
| Upgraded V1 writer/result | 1,000 events; measured readable ceiling at most 64 MiB/event; 128 MiB entire encoded result; raw protected stored envelope at most 128 MiB. The existing 1 MiB configured limit remains effective until explicitly admitted as a larger measured capability. `PayloadLimit` / `ResultLimit` / `RawEnvelopeLimit`. |
| Result parsing | JSON depth 64 with root depth 1; 1,000,000 total object-member/array-element nodes; 64 KiB maximum incremental read window. All raw bytes, including whitespace/unknown V1 fields, count toward the selected whole-result ceiling. `ResultLimit`. |
| Per-event metadata | 512 KiB encoded; extensions at most 64 entries, key at most 256 UTF-8 bytes, value at most 4,096 bytes, canonical extension map at most 256 KiB. `MetadataLimit`. Protection evidence is included, not exempted. |
| Memory and preparation | 128 MiB aggregate live pipeline scratch; immutable raw-source budget separately at most 128 MiB; encrypted prepared capsule at most 512 MiB; combined live capsule/request/response and reserved evidence capacity at most 1 GiB per operation. `ScratchLimit` / `AppendPreparationLimit`. |
| Registry/read compatibility | Row at most 64 KiB; manifest at most 65,536 rows and 64 MiB including options/closure/sort workspace. Page at most 256 events/64 MiB readable, proof at most 64 MiB, command proof at most 2 MiB; legacy array at most 100,000 events, 64 MiB readable and 256 MiB conservative accounting, including 8,192 bytes/event. `RegistryLimit` / draft read-limit outcomes. These independent maxima do not grant an oversized combined allocation. |
| Actor evidence | Intent and provider receipt each at most 2 MiB; each row key at most 1,024 UTF-8 bytes; at most 16,384 mutation rows/save, still subject to the 2 MiB record bound. Member proof at most 16 KiB and 10 siblings for 1,000 members. `ActorCommitEvidenceHold` before save if an admitted batch cannot be represented. |

The producer must preflight event count, declared serializer/protector output bounds and complete encoded result using conservative checked estimates **before** whole-payload serialization or output buffer creation. It then serializes once into a capped streaming sink; each write requests capacity before copying. Escaping and `4 * ceil(payloadBytes / 3)` Base64 expansion are charged, including quotes. An unbounded converter/protector cannot advertise writer readiness. A conservative estimate may refuse an unrepresentable result; it may not authorize a larger allocation. Rejection and no-op payloads obey the same cap. Keep the existing V1 helper callable for compatibility; invoking it before a later limit check is not a production path.

The proposed gateway replaces **both** implicit HTTP response buffering and unrestricted whole-result binding:

1. Choose mode/limits from authenticated request capability before send. Request headers-only completion (`HttpCompletionOption.ResponseHeadersRead` on the current HTTP seam) and a bounded response stream. Prove every HTTP handler/sidecar/proxy layer uses streaming or an enforced preallocation cap; a bounded application stream after an earlier unbounded transport buffer is insufficient.
2. Reject a declared body length above the selected cap before reading, but count every received byte even when length is absent, wrong or chunked. The producer emits identity content encoding. A compatibility adapter may accept compression only with independent compressed and decompressed counters, each capped at the selected result limit, and a bounded decompressor under the scratch budget; otherwise reject content encoding before body materialization. Stop on byte `cap+1`, cancel/dispose the transport and return `ResultLimit`, with no typed result or append.
3. Tokenize incrementally before creating a string, byte array, list entry, `JsonDocument` or DTO. Charge depth, node count, decoded name storage and token growth before extending buffers. Check duplicate names and raw triplet presence/casing at this stage. A token spanning windows is streamed or held in charged bounded chunks; the tokenizer cannot allocate an unbounded token buffer. Skip allowed unknown V1 values under the same byte/depth/node checks; reject unknown V2 members. `events` must be one array, not null; charge each next member before allocating it. A 257th V2 or 1,001st V1 event fails even when its payload is empty.
4. Count and validate payload Base64 incrementally, including canonical alphabet/padding and decoded length; JSON-unescape the scalar under the same counters before checking Base64. Do not call a whole-string Base64 converter first. Charge encoded and decoded capacities while simultaneously live, and enforce per-event metadata and readable caps before payload allocation. Oversized `resultPayload`, a huge unknown legacy string or a long sequence of insignificant whitespace cannot evade the whole-body cap.
5. Validate the echo and every event's raw triplet/alias/identity using private charged data. Web JSON property order is not authority: if echo follows `events`, retain only bounded unauthoritative chunks until the full envelope is admitted. No early event is staged, returned, applied or reserved while a later token can still fail. Only then construct the admitted result and additive wrappers. Transfer buffer ownership instead of silently retaining another full copy; typed strings/objects and private validation copies count toward 128 MiB live scratch.

Use bounded streaming or the draft's tenant-private, per-operation authenticated encrypted non-authoritative spool when a maximum legal result cannot fit with live parse buffers. Reserve disk capacity under the operation quota; retain its key only in charged memory, remove ciphertext on completion/cancellation and delete orphan ciphertext on recovery. No plaintext temporary files. A provider that cannot stream, enforce preallocation bounds or prove safe spool cleanup fails readiness. This protocol bounds the invoker's whole-result allocation independently of the producer; a producer's self-reported size does not suffice. It does not claim to bound arbitrary memory allocated inside user domain code.

### A4. Immutable bytes, preparation and migration branches

Import draft §4's exact `U`, `B`, `B32`, `N`, `I`, `T`, `Q`, `O`, `M`, strict raw JSON renderer, five-byte presence bitmap, derived protection record, options/default expansion and `StoredDigest` formulas. `T` retains original offset minutes; changing only offset changes consumed evidence. Absent/null/empty extension states remain distinct. The original provider bytes are retained verbatim, even when canonical spelling gives an equal digest. Decoder implementation/options/dependency behavior is pinned; a newer decoder cannot redefine an old digest. Provider encoding ID is authenticated original evidence, never the current provider's guessed ID. Caller-created DTO/proof values are inputs to verification and cannot establish authority.

Use draft §7's exact preparation identity/key and `AppendOperationFingerprint`; for a command `OperationId` is its **admitted execution** `CommandEnvelope.MessageId`, not an event ID or a retry submission ID. For an existing compatibility path without protected admission this is the initial submitted MessageId; that naming rule does not relax AD-5/AD-25 production admission or authorize an unfenced mutation. With an IdempotencyKey, `SubmitCommandHandler` must first obtain authenticated admission, compare the existing protected canonical command identity and obtain `ExecutionMessageId`, `ExecutionCorrelationId`, scope and original admitted input. These define the execution request (the same substitution the current handler makes, with IdempotencyKey removed before actor routing). A retry may submit different MessageId/correlation IDs while matching that protected identity; those transport identities neither change ScopeOpHash nor generate another capsule/pin. Original-input hash means the frozen **original admitted execution request**; never compare its full hash with a retry envelope containing changed transport IDs. Changed semantic payload/scope/other protected identity still conflicts under current admission rules. Recheck current caller authentication, tenant/command authorization and trusted-extension policy before returning any retained result; an old pin grants no access. Compare the canonical original execution request, addressed scope, selected mode/fingerprint and input hash on retry. Fence one `Preparing` owner before generating IDs, timestamp/offset, positions, protected bytes, signatures and bounded domain-result bytes. While still `Preparing`, finish A5 steps 1–5, including all control before/after images, absence evidence and derived certificate/origin bytes. Persist every exact capsule chunk and authenticate readback of its bytes/hash/order and complete manifest **before** CAS-publishing `Prepared`; authenticate that CAS before event-ID reservation, `Ready`, or actor save. No mutation needed by save/recovery may first be constructed after this boundary. Upfront estimate/reservation includes all A5 rows and A7/A8 retirement, complete publication-observation evidence and response capacity; A8 preflight must pass before `Prepared` or append. Missing retained bytes hold; they are never regenerated from current actor state, even when the record would hash identically. Lost preparation acknowledgement holds the same operation; no retry reruns a serializer, protector, clock, allocator or signer to manufacture a comparison result.

| Source | Mandatory evidence before evolved read / activation |
| --- | --- |
| New upgraded V1 | Same-save event, purpose-06 encoding sidecar, version-2 ten-field `HX-EV-STORED-EVIDENCE-1`, outbox, result, member-root and A5 certificate. Exact `HX-EV-V1-ORIGIN-1` branch `01` contains certificate hash and A5 pre-save bundle hash; verify separate post-save purpose-11 receipt. No offline manifest is required. |
| Valid retained V1 | Exact original raw provider bytes and uniquely proven historical encoding/decoder; draft §4's 26-field `HX-EV-V1-MIGRATION-1` record (64 KiB), dual independent purpose-0d/0e signed carrier (65 KiB), independently read-back reviewer docket, source evidence and version-2 digest sidecar, then purpose-06 encoding sidecar. Origin branch `02` binds manifest/docket/source/digest-sidecar hashes. The signer and reviewer keys/identities differ. |
| Raw-corrupt retained item | A6 source-specific disposition may close that item's cutover inventory only. It is unreadable and has no invented digest, encoding or origin. |
| New V2 | Same-save raw/encoding/digest/outbox/result/member-root/intent plus post-save provider receipt; no V1 origin. V2 still proves the complete save, although the draft V1 origin codec is inapplicable. |

Retained manifest source kinds remain exactly `versioned-config`, `deployment-log`, `commit-log`, `actor-outbox`, `store-generation`. The independently authenticated interval must enclose the actual event commit or exact commit/generation identity must prove it; JSON appearance proves neither. Retain original source bytes, reviewer receipt, decoder executable/options/closure, signatures, exact trust maps and key intervals/revocation through readable lifetime plus backup, rollback, queue and incident-replay obligations. Missing/changed references or multiple possible encodings yield `LegacyEvidenceConflict` and readiness hold, preserving old bytes. CAS-create/readback only; never rewrite or overwrite conflicting historical evidence. Compare `MigrationAuthorityDigest` separately beside RegistryFingerprint across readers/publishers; do not fold it retroactively into a signed fixture.

### A5. Complete acyclic intent and post-save commit proof

This replaces draft §4's incomplete intended-record list and reconciles it with draft §7's member-root sidecar. New production intent, bundle and receipt use codec byte `02` under their existing separators; codec `01` examples are not evidence of this new complete-set contract. Purpose-06, V1-origin, append fingerprint, result-record, batch-root and member-root codecs stay unchanged. Integration must update every producer/verifier reference together, including 6.5b authenticated raw-source admission.

Define `ScopeOpHash = SHA256(U tenant || U domain || U aggregateType || U aggregateId || U OperationId)`, lowercase hex when used in keys. Keys are actor-scoped and store/compare their full components to reject hash collision. The intent key is `actor-intent:` plus ScopeOpHash; member-root key is `batch-member-root:` plus ScopeOpHash. Events and existing actor keys keep their existing keys; encoding/digest/origin records use the exact event key plus `:encoding-evidence`, `:stored-digest-evidence`, `:v1-origin` respectively. Provider receipts are retrieved from a backend-owned committed-operation index by canonical backend identity, actor key, OperationId and committed generation, outside the save they certify.

For candidate v2, an intended mutation row is `kind:byte || action:byte || U exactKey || O(B32 expectedBeforeHash) || O(B32 afterHash)`. Actions are `01` put, `02` delete. Absent expected-before means proven absence; a value hashes exact prior bytes. Put requires a value after-hash; delete requires absent after-hash. Explicit null is forbidden in either O. A put of identical bytes still appears if staged. All old-state checks occur under the actor save fence/CAS; a record-hash comparison outside that fence is insufficient. The list is `u32 count || rows`, sorted by unsigned UTF-8 exact-key bytes, with each physical key exactly once. Scope each key to this actor. A failed before-image comparison saves nothing only when the provider proves rejection under the fence and no future commit; it enters the terminal stale-preparation protocol below. It never licenses restaging an old domain result against a new actor version.

| Kind | Exact record category / cardinality |
| --- | --- |
| `01`, `02`, `03`, `04` | Raw event, encoding evidence, stored-digest evidence, outbox routing intent: exactly one of each per appended event, matched by sequence/key. |
| `05`, `06` | One immutable append/no-op result; one member-root sidecar for eventful batches and none for no-op. |
| `07`, `08`, `09` | Aggregate metadata; pipeline/idempotency checkpoint; publication recovery index. Include every put/delete actually made, with exact prior/post bytes. A no-op does not advance stream metadata. |
| `0a`, `0b`, `0c` | Snapshot/witness, trusted effect receipt, pending-count/recovery ownership. Include every actual mutation; no optional component may silently stage outside the list. |
| `0d` | Exactly one A7 immutable no-op checkpoint witness for no-op; absent for eventful save. |
| `7e`, `7f` | Derived intent certificate itself; V1 origin records. Excluded only from intended business rows to break the hash cycle, included in complete actual receipt/readback rows. Exactly one certificate; one origin per V1 event, none for V2/no-op. |

Unknown kinds fail writer readiness. Any future actor state category needs an explicitly reviewed codec/kind revision before use. This list includes control-state deletions, not just events. No-op compatibility terminal checkpoint/index/pending-count mutations are listed when they occur. The actor must seal its staged mutation set before save; comparison to the declared set rejects any extra/missing mutation. A hidden provider-internal storage row is not an application mutation, but the provider must attest atomic visibility of every listed application mutation under one generation.

The exact dependency order is below. Steps 1–5 run under the original fenced `Preparing` owner; “stage” there means prepare private proposed-save bytes, not save or authorize reservation. Alongside the existing hash codecs, the capsule retains every exact non-absent before-image and authenticated absence proof, every exact after-image, all derived certificate/origin bytes and their deterministic keys. Hash-only control rows are insufficient recovery evidence. The actor version/fence used to observe those images is pinned; a later mismatch follows AbortedStale, never reconstruction.

1. Prepare raw event, encoding/digest sidecars, outbox bytes and **domain-result payload bytes**; compute the unchanged AppendOperationFingerprint. In all member leaves `domain/append-result hash` means SHA-256 of those pre-rendered payload bytes (result-record tag `05`), never the hash of the later result **record**.
2. Build the draft's exact MemberLeaf/tree and six-field `HX-EV-BATCH-MEMBER-ROOT-1` from those values. For `n=1` root equals its leaf; for odd levels duplicate last child; 1..1,000 members only. The root sidecar contains fingerprint and payload hash, but no result-record, certificate or commit hash.
3. Form `CoreBundleBytes` as `u32 eventCount`, then for each sequence in ascending order `B raw || B encoding || B digest || B outbox`, then `B memberRootSidecar`. `CoreBundleHash = SHA256(CoreBundleBytes)`. This is the exact complete event/outbox/encoding/digest/member-root bundle for batch-root tag `08` and append-result tag `08`; it deliberately excludes the result record and derived certificate/origins. Build the unchanged eight-field append-result record with that hash and exact original result bytes. The no-op alternative builds A7's result then its witness, with no CoreBundle/member-root.
4. Finish every other business mutation under the actor fence, including snapshots/index/checkpoint/ownership rows. Let `ExactRows` be `u32 count || each (mutationRow || O(B exactAfterBytes))` in the same sorted order; after-bytes are present for put, absent for delete, and must match afterHash. `PreSaveBundleHash = SHA256("HX-EV-ACTOR-PRESAVE-BUNDLE-1\0" || 02 || B ExactRows)`. No origin, certificate, provider ETag or receipt enters this hash.
5. Encode `HX-EV-ACTOR-INTENT-1\0 || 02 || 0008` with unchanged field meanings/types: `01` U actor key, `02` U tenant, `03` U domain, `04` U aggregate type, `05` U aggregate ID, `06` U OperationId, `07` N proposed checked logical version, `08` B exact intended mutation list. Stage it. For each V1 event, stage unchanged origin branch `01` referencing SHA-256 of that certificate and PreSaveBundleHash. These derived records contain no self-hash. Their deterministic keys and bytes must verify before save.
6. Seal the complete staged set (business rows plus certificate/origins). Complete A4 encrypted chunk/manifest readback, then publish/read back `Prepared`; only afterward reserve event IDs and establish `Ready`. Before save, read/compare the retained complete set and pinned before-images under the save fence; lost bytes hold without regeneration. Perform one actor-owned atomic save under the checked expected actor version/fence. For eventful batches, the global reservation must already be `Ready` with every exact ordered ID reference read back. Neither a certificate nor a staged/co-stored receipt proves commitment.
7. After save, obtain the separately issued signed provider receipt `HX-EV-ACTOR-COMMIT-1\0 || 02 || 0009`: `01` U actor key, `02` U OperationId, `03` N actual logical version, `04` U backend identity, `05` B exact provider fence, `06` U resulting ETag, `07` N committed generation, `08` B32 certificate hash, `09` B complete actual mutation list in the v2 row codec. All intended rows must match exactly; the only additional rows are the exact derived `7e`/`7f` set. The receipt never lists itself. Sign through draft §6 purpose `11` with backend-bound provider authority after the save, never a domain/caller signer.
8. Authenticated provider readback under that committed generation compares **every put's exact bytes**, every delete's proven absence, before-image/CAS evidence, derived rows, actor version, backend, fence and actual ETag. Compute `ActorBundleReadbackHash = SHA256("HX-EV-ACTOR-BUNDLE-1\0" || 02 || B completeExactRows || U committedETag || N logicalVersion)`, with completeExactRows using ExactRows framing including derived rows. Compare the member root and all leaves once. Only then CAS the whole batch root `Ready→Committed` with this hash and immutable result key/hash, and read back that CAS before publication or a committed public outcome. A no-op follows A7 without an event root.

Retain exact certificate, receipt, complete mutation evidence, original purpose-11 SPKI/key interval/revocation/backend binding and verifier implementation/options for every open read, retry, backup, rollback or delivery obligation. Later actor saves may change live metadata/index keys; historical receipt verification uses the authenticated retained committed-generation images, while a fresh source read proves that the addressed immutable event/sidecars still match. It never requires today's actor head/ETag to equal the historical commit ETag. A provider unable to retain or authenticate historical exact images cannot advertise this contract. Its failure is `ActorCommitEvidenceHold`, not permission to accept a digest-only/current-cache substitute.

Lost acknowledgement, timeout or cancellation after save starts follows the same path with a bounded recovery token: clear/ignore staged cache, query the provider operation index, read back the complete generation and reconcile the global root. Full exact match proves committed; an unchanged prior generation **plus authenticated proof that no in-flight save can later commit** proves no commit. Partial, foreign, stale, missing, conflicting or unavailable evidence remains a hold with capsule/reservation retained. Never restage, rerun domain work, release IDs or return a new result while that uncertainty exists. A successful HTTP/save response without provider readback is also insufficient.

#### Proven-no-commit stale preparation

A changed actor version/head or before-image invalidates the prepared domain result as a candidate for a new save. Preserve its original bytes; never replace its logical version, sequences, generated values or signature to fit a newer head. Under the original operation fence, reconcile A5 first. A complete commit wins even if today's actor is newer. Partial/unknown/in-flight evidence stays `ActorCommitEvidenceHold`.

Only authenticated rejection/no-start evidence naming this operation, preparation hash, attempted fence and checked actor version, **plus a durable fence invalidation proving no delayed save can later commit**, permits `Prepared → AbortedStale`. The command admission owner CAS-publishes create-once `append-abort:` plus ScopeOpHash: `HX-EV-APPEND-ABORT-1\0 || 01 || 000a`, tags `01..05` U tenant/domain/aggregate type/aggregate ID/OperationId, `06` B32 preparation identity, `07` B32 exact original capsule manifest hash, `08` N expected actor logical version, `09` B32 authenticated no-future-commit proof hash, `0a` U reason exactly `stale-source`. Record and proof are each at most 64 KiB, reserved before preparation. Read back exact abort, authority, fence invalidation and absence of committed actor/root/pin before CAS-releasing all reserved ID references/root together. Uncertain release holds; never release one member as if the batch were partly aborted.

Retain the abort tombstone, original identity, no-future-commit proof and release receipts through all retry/backup/rollback obligations; reclaim encrypted chunks only after authenticated absence and retention decision as for A7. A repeat of this operation returns the same typed `AppendPreparationStale` precommit conflict; it does not rerun domain work, allocate again, or mint a committed-response pin. A fresh command requires a **new admitted execution identity** (and a new idempotency key where the old key binds the original execution), current source read/admission and a new domain invocation. This explicitly ends the old operation; no transparent same-operation re-admission is permitted. This terminal conflict is independently authorized by the proven abort and current access policy, not by A8's committed-response pin. If preparation never started, release instead requires affirmative durable admission/fence evidence of that state; missing records alone are not proof.

### A6. Raw-corrupt disposition without moving-head conflicts

Replace the draft's thirteen-field corrupt disposition with candidate `HX-EV-RAW-CORRUPT-1\0 || 02 || 0010` (16 fields). Tags `01..0d` retain draft encodings: U tenant/domain/aggregate type/aggregate ID/event key (`01..05`), N sequence (`06`), B32 exact raw hash (`07`), N observed head (`08`), U observed actor ETag (`09`), B32 exact authenticated observed raw-readback proof hash (`0a`), U AD-31 capture key (`0b`), B32 independently authenticated operator-decision hash (`0c`), Q decision UTC (`0d`). Append `0e` B exact draft §5 canonical backend descriptor, `0f` U immutable provider store-incarnation ID, `10` B32 SourceIdentityHash. At most 64 KiB, checked before field allocation. A store-incarnation is an authenticated stable generation/restore identity, not a mutable actor ETag or a friendly store name.

`SourceIdentityHash = SHA256("HX-EV-CORRUPT-SOURCE-1\0" || 01 || B backendDescriptor || U storeIncarnation || U tenant || U domain || U aggregateType || U aggregateId || U eventKey || N sequence || B32 rawHash)`. The create-once key is `raw-corrupt-disposition:` plus lowercase SHA-256 of the same input **without final rawHash**. Retain and compare all key components; changed bytes at an existing key cause a conflict instead of creating a second disposition. The operator-approved decision must bind SourceIdentityHash, reason, exact capture key/hash and decision time. AD-31 capture must authenticate the exact original bytes/scope/hash; no caller-provided hash can stand in for that capture.

The original head/ETag/proof are immutable **observations at decision time**, retained and verified as historical proof. On every later use, obtain a fresh authenticated source proof for the same backend incarnation, addressed event key, sequence and exact raw bytes; require current head to cover the sequence and a valid current provider fence. Independently verify the original capture/decision and current source identity. A new valid head/ETag/readback signature caused by appending a later event is accepted when SourceIdentityHash and decision are unchanged. Do not compare fresh proof hash or actor ETag for equality with tags `09`/`0a`. Historical proof validity under its retained interval and fresh current proof validity are separate checks.

Changed raw bytes, key/scope, store incarnation or approved decision are `LegacyEvidenceConflict`; unverifiable/stale fresh proof, missing capture/decision or unreadable historical authority is a readiness hold (`RawSourceUnavailable` where appropriate). A restore/backend move needs an independently approved source migration, not automatic acceptance based on equal JSON. Corruption disposition closes inventory for this one item only: replay stops there with `RawEnvelopeCorrupt`, no invented StoredDigest, no Apply/handler/checkpoint advancement and no silent subscription acknowledgement. AD-31 capture-before-ack remains separately necessary. Valid later events may append under their normal writer gate; they do not make the corrupt prefix readable.

### A7. Exact no-op checkpoint and capsule retirement

A no-op preserves its original scoped command OperationId/input and creates no event ID, reservation root, member-root, outbox or publication. Pre-render bounded domain/no-op result bytes once. Keep draft `HX-EV-NOOP-RESULT-1\0 || 01 || 0009`: U OperationId, B32 preparation identity, B32 original-input hash, U mode, O(B32) fingerprint, B result bytes, B32 result-byte hash, U checkpoint key, U actor logical version, in ascending tags `01..09`. Tag `09` is invariant positive decimal with no sign/leading zeros and must decode to the same checked N used by the intent/receipt. The candidate fixes its immutable result key to `noop-result:` plus ScopeOpHash and checkpoint key to `noop-checkpoint:` plus ScopeOpHash. There is no dependence on wall-clock expiry of an old mutable idempotency cache row.

The checkpoint **is** the immutable no-op completion witness. Its exact codec is `HX-EV-NOOP-WITNESS-1\0 || 01 || 0010`:

| Tags | Encodings / values in order |
| --- | --- |
| `01..06` | U actor state key, U tenant, U domain, U aggregate type, U aggregate ID, U exact OperationId. |
| `07..0a` | B32 preparation identity, B32 original-input hash, U selected mode (`V1`/`V2`), O(B32) selected fingerprint (absent only for authenticated implicit V1). |
| `0b..0e` | N actor logical version, N unchanged stream head (>=0), U no-op result key, B32 hash of exact complete no-op result **record**. |
| `0f..10` | I event count, exactly zero; U completion state, exactly `completed`. |

Whole witness is at most 4 KiB. It contains neither its own hash nor certificate/receipt hashes. Result points to witness by **key**, witness hashes already prepared result record, then the intent hashes both, so dependency order is acyclic. The actor stages result/witness with create-once expected absence and all required compatibility idempotency/pipeline/index/pending-count mutations in the **same** terminal save (A5 kinds `05`, `0d`, and applicable control kinds). Existing public command/status shapes and constructors remain unchanged; A8 replaces their private evidence selection for evidence-required executions. The witness attests unchanged head, and no eventful kind may be present. An existing equal result/witness is read and reconciled, never saved again; any input/mode/fingerprint/result/version mismatch is `AppendPreparationConflict` or evidence hold as appropriate.

The A5 intent/provider receipt must include both exact rows plus actual control mutations and prove one generation. Read back result/witness bytes, cross-references, preparation identity and immutable original input; compare receipt version and unchanged stream head at that save. A changed later head cannot invalidate a legitimate earlier no-op witness. On ambiguous terminal save, use complete-set readback; no witness, partial control state or mismatched receipt holds and never reruns the no-op. A currently absent mutable cache row does not erase the retained witness's command-idempotency obligation.

After all capsule-dependent in-flight retry, publication (none for no-op) and rollback obligations close durably, publish/read back the draft's fifteen-field, at-most-4-KiB `HX-EV-NOOP-COMPLETION-1` at `noop-completion:` plus ScopeOpHash. Its tag `0c` is SHA-256 of the **exact A7 witness bytes**, `0d` certificate hash, `0e` exact purpose-11 receipt claim hash; retain the corresponding signed receipt carrier. A7 defines the formerly unspecified checkpoint. Tag `0f` names the authenticated retention decision covering exact capsule chunks and remaining compact-record/result/response-pin obligations. Pre-reserve compact evidence before save. Closure of the bulky capsule obligation never means deletion of still-needed idempotency/evidence records.

Read back compact record and all referenced source/result/witness/intent/receipt and A8 public pin before chunk deletion. CAS-delete encrypted chunks and release quota only after authenticated absence; ambiguous deletion retains charges. Exact late append-layer retry reads the retained no-op result; exact **command** retry uses A8's original response pin. Missing any required witness/first pin holds `AppendPreparationHold`/`CommandOutcomeHold`, without re-rendering, generating IDs or invoking domain work. No-op source evidence and compact result must outlive all promised command retry/backup/rollback obligations; capacity exhaustion holds a new operation before save rather than evicting an existing promise.

### A8. Immutable first response and later publication revisions

The domain/append result, publication outcome revision and public response pin are three different records. Immutable append result bytes never change after save. The command endpoint must not turn `PublishFailed`, a transport exception or caller cancellation into proof that no event committed. This candidate resolves the draft's retry ambiguity by selecting **the first durably pinned public response**, permanently, for identical command retries. Later publication progress is read through authenticated command-status inspection; it does not silently replace the POST retry response.

#### Complete publication observations

Replace draft §7's outcome codec `HX-EV-COMMAND-OUTCOME-1\0 || 01` with **`03`**; codec `02` is unassigned. Retain the draft's twelve tag types: U OperationId, U tenant, U domain, U aggregate ID, B32 append-result-record hash, B32 committed batch-root/no-op-witness hash, U publication state, O(B32) publication **receipt-set hash**, B exact public response body bytes, N revision, B32 body hash, N actor logical version. Extend tag `07` from `pending`/`published`/`failed` to also admit `unknown` and `not-applicable`. Tag `08` is present for every eventful outcome, even pending/unknown, and absent only for no-op `not-applicable`. This replaces the prior single-receipt meaning. No-op's witness proves zero events and cannot be labeled published.

Derive the **entire expected event-member list** from all immutable committed outbox intents, including rejection events. Each event MessageId has **exactly one immutable component/topic destination**, selected by the imported draft §7 outbox/global publication pin. This candidate adds no fan-out and changes none of those imported codecs or per-MessageId keys. Identity is `(event ordinal, exact event MessageId, destination ID)`; destination ID denotes that exact pinned component/topic/configuration, never the current display name or retry configuration. Sort by numeric ordinal; reject duplicate ordinal/MessageId, missing/extra members and zero or multiple destinations for an event. Count equals the committed event count: at most 1,000 V1 members or 256 V2 members under A3. A second physical destination for the same MessageId fails readiness before append. Each U in these codecs is at most 1,024 UTF-8 bytes. Prepare/read back each exact publication pin before the observation; never use a caller-made pin.

`ExpectedEntries = u32 count || entries`, each entry `I ordinal || U MessageId || U destinationId || B32 exactOutboxIntentHash || B32 exactPublicationPinHash`. `ExpectedSetHash = SHA256("HX-EV-PUBLICATION-EXPECTED-1\0" || 01 || B ExpectedEntries)`. A complete observation row repeats one expected entry, then `N admittedAttempt || state:byte || O(B32 exactReceiptHash) || B32 exactAuthenticatedObservationProofHash`. Attempts are positive. States: `00` pending (receipt absent), `01` accepted (value receipt), `02` failed (value receipt), `03` unknown (receipt absent). Null receipt is forbidden. Failed means definitive durable rejection/exhaustion for that member/attempt, never an exception or time elapsed. Pending needs authenticated outbox/attempt readback proving queued/not accepted and no unresolved send for that member. Unknown needs authenticated attempt state showing an unresolved send; unavailable authority is a hold, not a fabricated unknown observation.

`HX-EV-PUBLICATION-SET-1\0 || 01 || 0006`: tags `01` U OperationId, `02` B32 append-result-record hash, `03` B32 committed batch-root hash, `04` B32 ExpectedSetHash, `05` B exact coordinated observation fence, `06` B(`u32 count || ordered observation rows`). The complete set is capped at 2 MiB before allocation; observation fence at 64 KiB. Every referenced receipt/proof is capped at 64 KiB, with at most 64 MiB combined unique evidence per observation, streamed under A3 scratch and charged to the 1 GiB operation quota. Exceeding any cap preserves the previous outcome and holds. Hash the exact complete set record for outcome tag `08`. Retain exact set plus every receipt, pin, observation proof, original verifier keys/options and set-fence proof. Hashes alone prove nothing: independently authenticate each provider/broker receipt under draft §7, compare operation/scope/member/destination/attempt/pin and accepted/failure disposition, and read back the whole vector under the coordinator fence. The coordinator snapshot linearizes observation against publication changes; unavailable common fence or incomparable member proof is `CommandOutcomeHold`. This is private evidence; broker signed claim codecs/fixtures remain unchanged.

Before A4 publishes Prepared, reserves IDs or permits append, the writer must conservatively preflight and durably reserve the **complete** first-outcome capacity: exact expected-entry framing and all record/tag/length overhead; the worst-case accepted observation row for every event (`entry bytes + 8 attempt + 1 state + 33 present-receipt + 32 observation hash`); complete set/fence; renderer/response/outcome/pin/head/preparation records; and authenticated provider/broker receipt, pin, observation, closure and verifier evidence needed by that vector. Use advertised authenticated maximum encoded evidence sizes, not the smaller initial pending rows or average receipts. Count all separately live/retained bytes against A3 scratch, the 512 MiB capsule and the combined 1 GiB operation quota, as applicable. The complete set must fit 2 MiB and its referenced unique evidence 64 MiB even when every member is accepted; conservative deduplication is allowed only for already known identical immutable evidence. A provider without enforceable evidence maxima/reservable capacity cannot admit the batch. Reduce the admitted batch or hold before append when any bound fails; revision zero must never first discover after commit that its complete outcome is unrepresentable. Post-append authority failures still hold and preserve commit truth. Later attempts/revisions reserve additional evidence before admitting that attempt/revision, retain accepted and prior obligated evidence, and cannot spend the first-outcome reservation.

Reduce the verified complete vector deterministically: all accepted → `published`; otherwise any unknown → `unknown`; otherwise any pending → `pending`; otherwise → `failed` (at least one failed, every other row accepted/failed). Thus one accepted plus one pending is pending, not an invalid batch or published. Preserve every accepted row/receipt permanently across attempts/revisions; it cannot become pending/failed/unknown and cannot be sent again. Only unresolved members progress. A failed member may move to a higher-attempt pending row after definitive prior-attempt closure and durably admitted retry under the same immutable pin; an unknown member must reconcile accepted or definitive no-acceptance/no-future-acceptance before retry. No new attempt guesses that proof. A failed→pending transition changes the complete set hash and retains old evidence. No silent retry of accepted members, including when a different event failed.

Revisions begin at zero, increase by one with checked N arithmetic and immutable keys `command-outcome:` plus ScopeOpHash plus `:` plus invariant revision decimal. Published/not-applicable are terminal. Pending/failed/unknown revisions require newly authenticated complete observations; identical evidence reuses its revision, regardless of an HTTP result. Publication state is separate from the saved domain rejection/success decision; published rejection events do not turn a rejected command into a successful one. No-op completion maps from its witness, with no publication assertion. Status body rendering preserves current public contracts: projection of verified pending/unknown to the existing nonterminal shape must not claim Completed/EventsPublished; published alone can claim complete publication. Private `failed` proves failure for the observed member attempts and may later admit a retry; it does not establish permanent public `CommandStatus.PublishFailed`, whose existing contract tells clients to stop polling. Story 6.5/6.5c integration must establish the exact compatibility-preserving terminal mapping, including required terminality evidence, existing public fields/polling semantics and permitted transitions. Until that mapping is established and its evidence satisfied, rendering or exposing a public outcome from private `failed` deterministically yields `CommandOutcomeHold`; no permanent-failure reply or new outcome pin/head is manufactured from per-attempt failure. Preserve append truth, retained observations and every existing response pin; an authorized exact POST retry still returns its existing pin. No new public shape, enum/member, proof codec or interpretation of transient HTTP status as commit authority is implied.

The latest pointer is `HX-EV-COMMAND-OUTCOME-HEAD-1\0 || 01 || 0004`: `01` U immutable outcome key, `02` N revision, `03` B32 exact outcome-record hash, `04` O(B32) previous outcome-record hash, absent only at zero. Key `command-outcome-head:` plus ScopeOpHash is CAS-fenced. Create immutable revision bytes first, then CAS the head from the exact previous version/hash; authenticate readback of both before status inspection exposes them. If immutable revision `r+1` already exists after a crash before the head CAS, authenticate its exact bytes, complete retained observation/receipt set, authoring fence and durable CAS-preparation evidence binding predecessor key/revision/hash `r`. Reconcile that existing revision first: CAS/read back the head from the exact predecessor to those same `r+1` bytes, or verify that the head already contains that transition. Only then record a newer observation at `r+2`. Missing/conflicting revision, observation, predecessor or fence evidence holds; never overwrite the occupied key, skip a revision, or substitute a newer observation at `r+1`. Competing observations follow that same reconciliation rule. This predecessor/CAS evidence is retained under the command fence before revision creation and is bounded/reserved with revision evidence; the existing outcome/head codecs remain unchanged. Revision record plus pointer/evidence count toward the 1 GiB quota; no age-based eviction while obligated. Capacity or overflow holds the next revision while preserving prior committed truth.

Pin the exact endpoint response using `HX-EV-COMMAND-RESPONSE-1\0 || 01 || 0003`: `01` I HTTP status, `02` M application response headers, `03` B exact body. The header map has at most 64 entries/256 KiB encoded, name at most 256 ASCII bytes, non-null value at most 4,096 UTF-8 bytes, charged before allocation. Header names are canonical lowercase ASCII, sorted, with no case-fold duplicates; pin exact content type and command-status location when returned. This preserves the existing endpoint's body/headers/status contract. Transport-managed Date, connection/framing, compression and per-attempt trace headers are outside this application record and cannot change its meaning. Reuse exact uncompressed body bytes without JSON reserialization; middleware must preserve those entity bytes. Whole encoded response **and** outcome record each fit the selected mode's result cap, with shared simultaneous scratch and quota accounting. No-op endpoint body need not equal its domain-service wire body; preserve both separately.

The first-response pin is `HX-EV-COMMAND-RESPONSE-PIN-1\0 || 01 || 000d`, at `command-response-pin:` plus ScopeOpHash, capped at 4 KiB:

| Tags | Exact fields in order |
| --- | --- |
| `01..05` | U OperationId, U tenant, U domain, U aggregate type, U aggregate ID. |
| `06..08` | B32 original-input hash, B32 append/no-op result-record hash, B32 committed batch-root or no-op witness hash. |
| `09..0d` | N first outcome revision (exactly zero), B32 exact revision-zero outcome-record hash, U immutable response-blob key, B32 exact response-record hash, N actor logical version. |

#### Fenced first-response preparation

First-pin requirements apply to **committed execution replies**, including committed rejection/no-op results and replayed committed outcomes. Independently authorized admission, authentication, tenant authorization, validation, limit, proven-abort errors and truthful unresolved holds can return their existing errors without fabricating a committed pin. Such responses must not claim accepted execution/commit/publication from an unproved result. In particular the current protected `Pending` short circuit must return a truthful in-progress/hold response until committed evidence and first pin exist; it cannot reconstruct an unpinned success from an admission session.

Use `command-response-preparation:` plus ScopeOpHash for a durable private fenced state record, created/read back **at original execution admission, before domain invocation, actor save or rendering**. `HX-EV-RESPONSE-PREPARATION-1\0 || 01 || 000a`: tags `01` B32 ScopeOpHash, `02` B32 original admitted input hash, `03` B admission-authority proof, `04` N positive owner-fence generation, `05` byte state, `06` O(B32 response-input-record hash), `07` O(B32 response-record hash), `08` O(B32 revision-zero-record hash), `09` O(B32 first-pin hash), `0a` O(B32 predecessor preparation-record hash). No nulls. Whole record and admission proof each at most 64 KiB, subject to the whole-record cap. States `00 NeverStarted`, `01 Rendering`, `02 Prepared`, `03 Pinned`: 00 has tags 06–09 absent and 0a absent at creation; 01 has only 06 present; 02 has 06–08 present; 03 has 06–09 present. Every transition binds exact predecessor hash; owner transfer is a fenced CAS retaining state/references and invalidating old owner, not resetting to 00. A missing/corrupt record or unavailable admission chain cannot become NeverStarted. The admission authority must prevent every normal/replay/recovery path from bypassing this creation/fence; existing executions need independently approved migration evidence, never inference from missing data.

At original admission, the existing admission-authority proof must also bind the exact canonical public-origin/renderer configuration digest and retained bytes. The canonical configuration is bounded to 64 KiB, hashed with the draft options codec, and contains the trusted absolute public origin plus exact renderer/options identity and original endpoint Location-construction policy; it is authenticated/read back with the admission proof before execution. Retain the configuration bytes and referenced renderer/options implementations through every retry obligation. The proof in preparation tag `03` and its hash in the message-scope lookup bind this original configuration without adding a public field or changing either private record's framing. Recovery must verify and reuse these admitted bytes/digest; a later deployment, proxy origin, retry Host or renderer default cannot replace them. Missing original configuration holds even when response preparation is still NeverStarted.

After A5/A7 committed readback, prepare immutable bounded inputs at `command-response-input:` plus ScopeOpHash: `HX-EV-RESPONSE-INPUT-1\0 || 01 || 000c`, tags `01` B32 ScopeOpHash, `02` B32 admitted original input hash, `03` B32 append/no-op result-record hash, `04` B32 batch-root/no-op witness hash, `05` B32 actor provider-receipt claim hash, `06` N actor logical version, `07` U observed publication state, `08` O(B32 complete publication-set hash), `09` B32 endpoint renderer/options identity hash, `0a` U exact original execution correlation, `0b` U exact absolute status Location, `0c` B32 authenticated A8 message-scope lookup hash. Input record is at most 64 KiB; individual U at most 4 KiB. Retain exact referenced bytes/verifiers plus rendering policy options (at most 64 KiB), under shared 1 GiB quota. All scope/root/result/version/observation/lookup links must verify. The renderer/options identity selects the existing endpoint's bounded public-shape/status/result-payload policy, headers and serializer; its registered hash makes those choices deterministic from the pinned inputs. It may not consult a new clock, random ID, request host, current publication status or retry correlation. Location uses the exact origin/construction policy retained and bound by the original admission proof plus original execution ID; renderer-input tag `09` must equal that admitted renderer/options identity. No untrusted host or newer configuration substitution is permitted.

Order is mandatory:

1. Prove original admission, current authorization, committed complete actor/root or no-op readback and complete publication observation (or no-op witness); validate/authenticate exact message-scope lookup. Verify the A3/A4 pre-append reservation still covers the exact response/outcome/pin/input bytes and evidence before rendering; this is a consumption check, not first capacity admission after commit. Immutable-create/read back the input record.
2. CAS NeverStarted→Rendering binding that exact input hash, then authenticate readback. **No render may begin before positive Rendering readback**. A lost CAS acknowledgement may continue only when the still-live original owner proves it has not started rendering; a recovery owner finding Rendering cannot infer that from absent output and holds if prepared bytes are unavailable.
3. Render the existing public response once into private capped storage; outcome revision zero contains the identical body. Persist the exact response record at `command-response:` plus ScopeOpHash and exact revision zero immutable, then read back both complete bytes/hashes. CAS Rendering→Prepared binding their hashes and read back. If the owner crashes between rendering and durable-byte persistence, the operation holds; it does not regenerate. Recovery from Rendering may progress only when **both** exact immutable output records already exist and verify against the input/fence with durable preparation-write evidence. Missing one or partial persistence holds. Provider authority must prove which owner wrote both; naked blob presence is insufficient.
4. Derive the 13-field first pin from those exact Prepared references; CAS-create/read back `command-response-pin:` plus ScopeOpHash. This single first-pin CAS is the visibility authority when a multi-key transaction is unavailable. Publish/read back initial head from exact revision zero, reconcile durable preparation to Pinned, then permit committed execution response bytes. Initial head may be recovered from a read-back valid pin/Prepared inputs; no later revision can publish until first-pin readback. No competing renderer may render a second candidate; an unequal existing blob/pin is a conflict/hold, never a winner selected by time.
5. After any lost acknowledgement, independently read admission/preparation/inputs/output/pin/head under the current command fence. A matching durable pin returns its exact HTTP status, application headers and body; proven pin absence and no in-flight CAS may finish only from verified Prepared bytes. Pin exists with NeverStarted/Rendering or missing/corrupt output, or Prepared with lost bytes → `CommandOutcomeHold`. A valid NeverStarted plus its intact original admission chain and proof of no superseding owner/transition permits first rendering through steps 1–4; absence of a pin by itself does not.

A later revision cannot rewrite the first response or render a replacement POST reply. Exact retry compares original execution identity through authenticated admission/compact records and uses revision zero permanently, while status inspection follows the latest head. Retained registry proof and current access checks apply; rotation does not allow reexecution. Rendering inputs are fixed once; if an input was prepared while state remained NeverStarted, a recovery owner reuses those exact inputs after authenticating them rather than choosing a newer observation. Stale/invalid required input evidence holds.

#### Existing retry and status integration

The current `SubmitCommandHandler.Handle` paths for `Replay`, `Recoverable`, `UnknownProviderOutcome` reconciliation, and `CreateReplayResult` must resolve the admitted **ExecutionMessageId** and original scope before A8 lookup. Replay and reconciled terminal/retryable results cannot escape via a newly constructed `SubmitCommandResult`; they select the same internal response-pin carrier consumed by `CommandsController`. Recoverable may continue only the original fenced recovery; Pending/unknown evidence may produce an independently authorized hold but no unpinned committed reply. For a protected retry with new submitted message/correlation IDs, return the **original** pinned body/correlation/status Location after current access checks. The per-attempt diagnostic correlation remains transport-only. Public request/response constructors and contracts remain unchanged; this is an internal authority path, not a new caller-supplied proof field. `src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs` remains ahead of handler response selection for API calls: authentication, tenant/RBAC validation or its exact prevalidated authorization context cannot be bypassed by Replay. Existing internal-service admission and domain authorization remain required on their own paths. The ordinary actor duplicate path and no-op path use the same selection. Current result-payload withholding/current access policy still applies: if policy forbids releasing a retained body, return the authorized denial/hold and withhold the pinned body; never silently alter the pinned committed reply.

The committed rejection exception exit obeys the same rule: both normal `Handle`/`ThrowDeterministicFailure` and replay `CreateReplayResult`/`ThrowDeterministicFailure` must select the original internal response pin before `DomainCommandRejectedExceptionHandler` can reconstruct or write a committed reply. For the first committed rejection, use A8's fenced, bounded first-response preparation with the original admission-bound renderer/options and retained `DomainRejectionProblemCatalog` implementation to render the existing ProblemDetails status, content type, fields and extensions once from original execution correlation and admitted endpoint inputs. Retain that catalog behavior in the existing renderer/options identity and implementation closure; use the existing response and pin codecs. After readback, both exception paths return the exact pinned status/headers/body, without rebuilding from current middleware correlation, request path or catalog. Missing pin/preparation authority holds under A8. Independently authorized precommit errors and current access denials remain separate existing errors/holds; they do not fabricate a committed rejection pin or release a denied pinned body.

**Explicit Review-32 replacement:** Within one tenant, an execution MessageId maps to at most one `(domain, aggregate type, aggregate ID)` scope. Reusing that execution MessageId in a different scope is `CommandIdentityConflict` at admission, before domain invocation, preparation or actor mutation; the same MessageId in different tenants remains independent. This replaces only draft §11's Review-32 vector that admits one original command MessageId across different scopes: cross-tenant preparation keys/batch roots remain distinct, while same-tenant cross-scope reuse must reject instead of creating a second preparation/root. Same-scope changed input still conflicts, and generated event MessageIds remain globally unique under the imported event reservation rule. The tenant/message authority aligns with `src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs`, `BuildKey(tenantId, messageId)`, and `SubmitCommandHandler`'s archive lookup by tenant/execution MessageId and existing message/command-type identity guard. Those current seams ground the choice; they do not already implement the proposed full-scope admission check.

Define authenticated lookup at `command-execution-scope:` plus lowercase SHA-256(`U tenant || U executionMessageId`). `HX-EV-COMMAND-SCOPE-1\0 || 01 || 000a`, tags `01` U tenant, `02` U executionMessageId (equals OperationId), `03` U domain, `04` U aggregate type, `05` U aggregate ID, `06` B32 ScopeOpHash, `07` B32 original input hash, `08` B32 admission-authority proof hash, `09` U evidence class exactly `required`, `0a` U original execution correlation. At most 4 KiB; U at most 1,024 bytes; encode/create/read back under admission owner before actor save, bind its hash in response-input. The authoritative admission index retains this classification/lookup through all retry/status obligations; collisions across scopes cannot select one by hash or arrival order. Unavailable/missing evidence-required lookup yields hold. An admitted tenant/execution MessageId cannot be silently rebound to a new scope; conflicting same tenant/message follows the Review-32 replacement above. The lookup plus preparation initialization is a recoverable admission operation: actor save remains fenced until both exact records read back.

`CommandStatusController` keeps authentication, identifier validation and its `eventstore:tenant` restriction. For each authorized tenant, first ask the authenticated admission index for direct message classification and lookup, then (when there is no direct execution) use the existing bounded `ICommandCorrelationIndex` semantics to resolve a unique original execution ID. Correlation mappings for evidence-required executions are admission-owned/authenticated and bounded; an advisory mapping is only a candidate until the authoritative lookup and original correlation verify. Ambiguous correlation or multiple tenant/execution matches remains 409, never pick a latest/first success. Resolve only authorized tenants; no match remains 404 without cross-tenant disclosure. A direct required record does not fall through to correlation or legacy on missing head/response evidence.

An evidence-required lookup selects ScopeOpHash, then verifies A5/A7 commitment, first pin, exact latest outcome/head/complete receipt set, and maps that verified observation into the **existing status response shape**. Exact POST retries still select revision zero, never that latest response. Advisory `ICommandStatusStore`/`DaprCommandStatusStore` rows may cache a view, but their TTL expiry, null-on-failure or stale Completed/EventsPublished cannot certify outcome. Preserve legacy-only status fallback only when authenticated migration/admission inventory explicitly classifies the execution as legacy with no evidence-required obligation and no conflicting required match. Missing classification or authority → truthful unknown/hold, not guessed legacy. Retain that classification/tombstone even after a lookup expires so deletion cannot downgrade an operation. Existing legacy correlation ambiguity rules still apply. The pinned Location names the original execution MessageId and therefore enters this same tenant-scoped route; it is never a raw scope hash or a new public route.

Retain revision zero, response record, first pin, append/witness and their exact trust/evidence for the operation's full promised retry lifetime (at least 24 hours and longer for any open retry, rollback, backup or incident obligation); later status revisions do not shorten it. Exact late retry after capsule retirement still returns revision zero. Removing the first response pin while its obligation is open is an evidence failure, not permission to return a newer response. A no-op pins `not-applicable` with original public shape and never generates a publication receipt/send.

### A9. Failure, cancellation and ordered migration

| Boundary / observation | Deterministic action and durable consequence |
| --- | --- |
| Invalid metadata, identity, alias, version, echo or F output | `MalformedMetadata`, `EventIdentityMismatch`, `UnknownEventContract`, `CapabilityMismatch` or `DownserializeRejected` before reservation/save. Preserve no partial result. Unknown/stale capability holds readiness; it is not proof of poison. |
| Count/encoded size/parser/scratch/sequence overflow | A3 limit or `SequenceExhausted` before the prohibited allocation/staging. Release only uncommitted private buffers/reservations proven safe to release. |
| Caller cancellation before durable preparation/save | Stop work, clear owned scratch/spool and propagate cancellation once no mutation/in-flight commit is proven. If a prepare/save might have begun, reconcile its exact operation instead of asserting no commit. |
| Actor save returns, throws, loses acknowledgement, or caller cancels after start | A5 full authenticated readback. Commit match preserves committed truth; proven no commit permits only fenced same-capsule continuation if its source/version is unchanged, or terminal A5 AbortedStale cleanup if changed; all other states `ActorCommitEvidenceHold` with no duplicate append or ID release. |
| Commit proven but batch-root transition ambiguous | Reconcile one entire root under original fingerprint; no independently committed member, publication or fresh append. Preserve same operation/capsule. |
| Commit proven; publication pending/failed/unknown | Retain append result/outbox. A8 may pin only a proved complete observation with an established compatibility-preserving public mapping; private failed without the integrated terminal mapping/evidence yields `CommandOutcomeHold`, preserving existing pins; an unknown broker send reconciles its receipt before another send. It cannot claim published or undo append. |
| First response or no-op retirement ambiguous | Exact A7/A8 evidence or hold; retain quota until complete readback/absence. No regeneration or reexecution. |
| Permanent corrupt/schema-invalid authenticated historical source | Stop replay/checkpoint; AD-31 capture before subscriber poison acknowledgement. New capability limits, unknown keys, protection unavailability, unexpected exceptions or OOM are hold/retry, never poison acknowledgement. |

Caller cancellation is neither poison nor an instruction to discard committed evidence. Post-start recovery uses a separate bounded token (one 30-second attempt, then durable recovery ownership and hold if unresolved); it never continues unbounded in a disconnected request or uses the cancelled caller token to abandon readback. A lost or cancelled public connection may deliver no response, but cannot change the committed result or first-pin bytes. Cancellation between pin commit and send returns the same first pin on retry. Public diagnostic status is `unknown`/hold when authority is unavailable, not a guessed success/failure.

Reader-first rollout follows draft §10 with these explicit dependencies:

1. Inventory all retained aliases/versions, measured limits, exact historical encoding/decoder evidence, corrupt items, protection readability and original keys/obligations. Fence every V1 appender at gateway **and actor**, drain uncertain old writes, and complete valid retained branch-02 evidence or A6 corrupt dispositions before evolved-reader activation. Dormant reader deployment alone grants no read capability.
2. Slice 1 includes the minimal **bounded evidence-writing V1 producer**, including A3 ingress/writer and A5 same-save/member-root/receipt probes. New V1 is branch-01, not offline migration. No old endpoint may allocate an unbounded response before a gateway's later check. Retain existing readers until this gate is complete.
3. Dry-run V1/V2 writers and verify exact bytes on the configured production provider plus a second independent provider/conforming harness. Integrate 6.5b read/replay and 6.5c publication/rollout using A5 codec-02 complete sets, A6 source identity, A7 checkpoint and A8 immutable first pin. Global reservations, consumer capability, binary carrier, effect receipts and historic-ID collision/backfill gates remain the draft's prerequisites before V2 enablement.
4. After any V2 append, fence V1-only command/replay/projection/rebuild/subscription endpoints in both deployment and rollback before routing work. Returning the **writer** to V1 needs a valid F and cannot downgrade retained V2 read requirements; unavailable capable peers yield `RollbackReaderCapabilityHold`. Rotate verifiers first and retain original keys/evidence through every obligation. Reader/transform changes and writer/trust changes follow A2's distinct compatibility rules.

Nothing here starts these runtime slices. Story 6.5 integration must reconcile all three child candidates, rerun review and obtain its exact-content named human approval; the current receipt remains `UNAPPROVED`.

### A10. Dispositions and verification vectors

All five assigned findings are **accepted**. Acceptance means this candidate supplies proposed testable closure; it does not rewrite their historical status or approve Story 6.6.

| Finding and historical source | Candidate disposition / integration destination | Verification |
| --- | --- | --- |
| `BH37-1`, review log row 546 | Accepted: [A5](#a5-complete-acyclic-intent-and-post-save-commit-proof) includes member-root and every actual actor mutation, explicit acyclic order and separate provider receipt. Replace draft §4 intent/receipt and align §7 bundle/readback language. | V05–V08, V19, K02/K07/K11. |
| `BH37-2`, row 547 | Accepted: [A3](#a3-bounded-producer-and-whole-response-ingress) moves admission ahead of HTTP buffering, token/string/Base64/list/DTO allocation on normal, rejection and no-op paths. Align draft §§2/8/10. | V01–V04, V16, V18, K01/K06. |
| `BH37-6`, row 551 | Accepted: [A6](#a6-raw-corrupt-disposition-without-moving-head-conflicts) makes corrupt source identity immutable while head/ETag/proof remain historical observations plus fresh read checks. Replace draft §4 conflict rule and align raw-source admission. | V09–V10, K03/K08. |
| `BH37-7`, row 552 | Accepted: [A7](#a7-exact-no-op-checkpoint-and-capsule-retirement) defines checkpoint key, 16-field codec, CAS preconditions, same-save complete-set readback and late-retry retirement evidence. Align draft §§4/7/10. | V11–V12, V21/V24, K04/K10. |
| `BH37-8`, row 553 | Accepted: [A8](#a8-immutable-first-response-and-later-publication-revisions) fixes original-command retries to revision zero's first durable response pin; status inspection alone advances with receipt-backed revisions. Replace draft §7's ambiguous retry alternative. | V13–V15, V20–V24, K05/K09/K10/K12. |

These are future implementation acceptance vectors, not claims that runtime tests were added/run. Each requires authenticated durable state and exact bytes, no second append/domain invocation where forbidden, and allocation counters/provider fault injection where applicable. A status code or mocked call count alone cannot pass.

The frozen execution specification's complete I/O matrix maps to these candidate vectors. “Document verified” below means the schemas, branches, bounds and reproducible codec arithmetic were checked here; provider execution remains future Story 6.6 verification.

| Frozen matrix row | Candidate sections | Vectors / document verification |
| --- | --- | --- |
| New V1/V2 writer | A2–A5 | V01–V08, V16–V19; K01/K06 size/negotiation arithmetic and K02/K07 complete-list byte/hash checks. |
| Retained V1 | A2, A4, A6, A9 | V09–V10, V16–V17; two exclusive origin branches and unchanged draft migration codecs checked; K03/K08 source identity and full disposition hashes. |
| Ambiguous actor save | A5, A7–A9 | V07–V08, V11, V14, V19/V21; full/absent/partial readback and stale-abort outcomes enumerated, K02/K04/K07/K10/K11 codecs checked. |
| Corrupt event and later append | A6 | V09–V10; K03/K08 unchanged source identity versus changed raw byte and changed observation, full disposition encoded. |
| No-op and exact retry | A7–A8 | V11–V15, V20–V24; K04 witness/result, K05/K09/K10 complete publication/response evidence, K12 execution/status selection. |

| ID | Input / injected boundary | Required result and byte-level assertion |
| --- | --- | --- |
| V01 | V2 wire event `{"eventTypeName":"counter-created","payload":"e30=","serializationFormat":"json","metadataVersion":2,"eventContractType":"counter-created","payloadVersion":1}` with matching registered `{}` schema/identity policy and exact echo. Mutate one triplet member absent/null/wrong case, alias or echo. | Admitted positive retains bytes `7b7d` through mapping; each mutation rejects before reservation. Escaped `\u006detadataVersion` plus literal `metadataVersion` is duplicate; numeric metadata 1 paired with canonical members is malformed. |
| V02 | Bodies of exactly 16,777,216 and 16,777,217 V2 bytes; exactly 134,217,728 and +1 V1 bytes; absent/lying Content-Length, chunked bodies and compressed compatibility stream. Include huge trailing whitespace or `resultPayload`. | At cap may pass only if every other bound passes; +1 stops at the counted offending byte with `ResultLimit`, no whole-string/result allocation or partial append. Compression must obey both counters. Instrument upstream HTTP buffering as well as decoder allocation. |
| V03 | V2 counts 256/257, V1 1,000/1,001; Base64 decoded payload 1,048,576/+1 (V2), measured V1 ceiling/+1; result containing legal 64 MiB V1 payload plus excessive metadata. | Check before next list entry/decoded-buffer capacity. A legal per-event size does not waive whole-result framing/Base64 cap. Also admit small pending rows but worst-case accepted rows/provider evidence above A8 caps: preflight rejects before Prepared/append, never after commit when creating revision zero. K01 fixes Base64 boundary arithmetic. |
| V04 | Depth 64/65, nodes 1,000,000/+1, huge token across 64 KiB windows, duplicate/case-fold names at nested depth, 128 MiB live scratch/+1, unbounded custom converter/protector. | Deterministic preallocation limit/malformed rejection or readiness hold. Maximum admitted source uses bounded streaming/encrypted spool; no uncharged copies/plaintext spool remain after cancellation. |
| V05 | New V1 and V2 one-/three-event batches; 1,000-member V1 maximum; original timestamp with nonzero offset. | Verify payload hash→fingerprint→member-root→result record→complete intent→V1 origins→post-save receipt in order. All exact staged rows and before/after images, including member-root, metadata/index/deletion and derived certificate/origin rows, are retained/read back before Prepared and match provider bytes/absence; crash after Prepared cannot regenerate any missing row; stored offsets and raw bytes unchanged. Proof depth 0/2/10 respectively. |
| V06 | Omit or mutate **only** member-root from intent, actual receipt, CoreBundleHash or readback; add an unlisted snapshot/effect/index mutation; duplicate a key. | Each fails `ActorCommitEvidenceHold` before root commitment/publication. K02 proves one-member inclusion changes intent row-set bytes/hash; success cannot rely on equal event payload alone. |
| V07 | Save commits then throws; save never starts; partial record readback; old generation/foreign backend; fake caller-signed receipt; issue receipt before save; mismatch delete absence. | Full match alone commits one root and returns original append bytes. Proven no-start plus no future commit permits only fenced continuation of the same capsule when source/version is unchanged, or A5 AbortedStale cleanup when changed. All unresolved evidence holds with no second invocation/append, no released IDs and no committed response, as in A9. |
| V08 | Cancel before prepare, after Prepared, during save, after actor commit/before root CAS, after pin CAS/before HTTP send. Advance actor head after a fully proven original commit. | Cancellation follows A9; retries use same generated IDs/offsets/protected bytes/signatures and first response. Later head reads use current authenticated source plus retained historical generation, never false commit conflict. |
| V09 | Authenticated corrupt raw bytes `7bff7d` at sequence 7/head 9. Record decision/capture. Append valid event at sequence 10; head/ETag/current proof change. | Same backend/incarnation/key/sequence/raw hash/decision keeps SourceIdentityHash and disposition valid for inventory. Replay still stops at 7 with no digest/Apply/checkpoint; changed head is not conflict. K03 fixes identity hash independence. |
| V10 | Change one corrupt raw byte, source namespace/incarnation, event key, decision/capture hash; provide stale/missing current proof. | Changed source/decision conflicts; unavailable proof holds. Neither accepts a fabricated origin, makes corrupt bytes readable nor silently acknowledges a delivery. |
| V11 | No-op with preserved nonempty result payload and head 9. Commit result, A7 witness, actual terminal checkpoint/index mutations and certificate in one save; then lose acknowledgement. | Both cross-references and all rows read back exactly; unchanged head, zero events and no outbox/reservation/member-root. Missing witness, different logical version/input/result or partial control state holds; no second no-op invocation/save. K04 fixes witness/result dependency bytes. |
| V12 | Publish no-op completion after obligations close; crash before/after chunk deletion; saturate 4 KiB witness/compact and 1 GiB operation quotas. Late exact retry after successful reclamation. | No release on ambiguous deletion. Late append retry returns exact original result and command retry exact first response without volatile regeneration. Missing witness/receipt/pin holds; changed input conflicts. |
| V13 | First response revision 0 `pending`, lose HTTP acknowledgement; durable broker acceptance creates revision 1 `published`; retry identical original command. | POST retry returns original revision-0 status/header/body bytes, even after restart/capsule compaction. Status inspection reads revision 1. Append bytes/fingerprint never change; no second send/effect. K05 asserts revision pin separation. Crash after creating revision 1 but before head CAS, then observe newer progress: authenticate/link existing revision 1 first and persist newer evidence at revision 2; missing/conflicting predecessor or set holds without overwrite/skip. |
| V14 | Lose first-pin CAS acknowledgement, prepare two competing responses, remove first pin or revision-0 body while later head survives, or mutate one pinned header/body byte. | Exact complete matching readback or `CommandOutcomeHold`; never pick latest revision or reserialize. Only durable NeverStarted with intact admission/fence history may begin first rendering; Rendering with missing prepared bytes holds. |
| V15 | Zero-event no-op under A8, with domain wire result and existing public command body intentionally different; cancellation after first pin. | Pin `not-applicable`, absent publication receipt; retain both exact bodies in their distinct records. Retry returns pinned public bytes; no fabricated published state or broker call. |
| V16 | Retained V1 alias registered at source version 2; new branch-01 V1 without offline manifest; retained branch-02 with valid manifest/docket; mixed V2 history plus V1-only rollback endpoint. Remove one decoder/key/source obligation or change MigrationAuthorityDigest. | Both legitimate V1 branches admit independently; missing/swapped branch/evidence holds, never guesses version/encoding. V1-only endpoint fenced after V2, F loss rejected, stored original bytes/metadata unchanged. |
| V17 | Registry at row 65,536/65,537, row 64 KiB/+1, manifest 64 MiB/+1; 16/17-hop retained chain; optional-default options omitted/explicit; changed F-only row vs changed read G/Apply dependency. | Boundary overflow/ambiguity holds before readiness; equivalent expanded defaults hash equal. F-only registry change leaves EventTransformHash equal; read dependency changes force replay. Preserve all draft signed fixtures and `UNAPPROVED` receipt byte-for-byte. |
| V18 | Serialize implicit V1 request and no-op/rejection result with additive nullable negotiation members; then emit mode-bearing V1/V2. Inject explicit null, only one property, uppercase hex. | K06 fixes exact paired omission and exact non-null emitted bytes on request and response. Presence is checked before typed binding; invalid pairs fail before append, unrelated `resultPayload:null` stays null. |
| V19 | Prepared capsule for logical version 3; unrelated valid append changes source before this save. Provider proves rejection and durable old-fence invalidation, or withholds either proof. | Complete commit reconciles original bytes if found. Otherwise A5 creates exact AbortedStale tombstone only with both proofs, reads back batch-wide release before chunk reclamation; same operation stays terminal and cannot reuse its domain result or regenerate values. New admitted execution reruns source/domain. Missing proof holds. K11 fixes abort bytes and decision. |
| V20 | Two distinct events, each with exactly one immutable component/topic destination: first accepted, second pending, then failed/unknown/accepted. Omit/add/reorder a receipt row; retry failed member. Also attempt two destinations for one MessageId and 1,001 V1 members. | A8 complete ordered set reduces pending/failed/unknown/published as defined; all accepted is necessary for published. Accepted member/pin/receipt never regresses or resends; failed→pending requires prior-attempt closure and higher attempt, unknown requires reconciliation. No-op remains not-applicable/absent set. Multiple destinations for one event or an over-count batch reject before append. Private failed with no integrated permanent-failure mapping/evidence yields `CommandOutcomeHold` for public outcome selection; preserve append truth and existing pins, and never tell clients to stop polling from per-attempt failure. This public mapping/hold case is future runtime verification. K09 fixes only local hashes, partial/missing/extra/duplicate outcomes, and rejects non-increasing or unproved retries. |
| V21 | Crash before Rendering CAS, after Rendering CAS before render, between render and byte persistence, after one/both blobs, after Prepared CAS, after first-pin CAS. Steal owner fence; lose pin/bytes; clear prep record; inject invalid state/fence or per-state tag presence. Crash after admission but before response-input creation, then change public origin/renderer configuration. | Only intact NeverStarted admission chain permits first render. Rendering with complete authenticated durable outputs may finish; missing outputs hold even if no response was sent. Prepared retries reuse exact outputs. Matching pinned readback returns original; a pin beside NeverStarted/Rendering holds as contradictory evidence. Missing authority cannot recreate NeverStarted, and superseded owners cannot persist/send. Recovery uses the original admission-bound origin/renderer bytes for the same Location; missing original configuration holds, never consults retry Host/current configuration. K10 fixes bytes for four preparation-state fixtures and checks coarse admission/output/pin-presence decisions, including the contradictory pin case. Owner-fence theft, one-of-two blob persistence, preparation-write authority, original configuration and preparation state/fence/tag semantics remain future runtime/provider checks in this vector. |
| V22 | Protected retry changes submitted MessageId/correlation while semantic identity matches; exercise Replay, Recoverable, Pending and unknown-provider reconciliation before actor entry. Change semantic input or revoke access. Reuse an execution MessageId in a different scope within one tenant, then in a different tenant. Retry a committed rejection through the exception path after changing transport correlation and the rejection catalog. | Resolve original ExecutionMessageId, input/scope and same pin, return original body/correlation/Location without invocation or another append. Pending/unknown unproved commit yields truthful hold. Semantic change conflicts; current authorization/withholding denial cannot release or rewrite retained body. A8's Review-32 replacement rejects same-tenant cross-scope reuse at admission and preserves independent cross-tenant execution keys/roots. For the committed rejection, normal and replay exception exits return identical original ProblemDetails status/headers/body from the pin despite those changes; current access denial still withholds it. This rejection-path case is future runtime verification. K12 only checks protected-identity equality and selects the supplied admission fixture's execution ID and constant response; submitted IDs are intentionally unused. Authenticated identity resolution, tenant-wide conflict admission, handler branches, pin selection and current access checks remain future runtime/provider checks. |
| V23 | Poll pinned Location using execution ID or unique/ambiguous correlation, multiple authorized tenants, denied tenant, expired advisory status, required lookup/head missing beside stale legacy Completed row; observe private failed without an integrated permanent-failure mapping/evidence. | Required lookup selects correct scope and latest complete outcome; required missing evidence holds, never falls back. Unique authenticated legacy-only classification retains current fallback. Ambiguity remains 409, absent authorized execution 404, missing tenant claims 403; unauthorized scope never exposed. Private failed without the integrated terminal mapping/evidence returns `CommandOutcomeHold`, never inferred terminal PublishFailed/stop-polling semantics; append truth and existing POST pins remain intact. This mapping/hold case is future runtime verification. K10 fixes lookup fixture bytes; K12 checks only supplied classification/presence/ambiguity decisions. Authenticated tenant/correlation resolution, authorization, head/receipt verification and the actual legacy fallback remain future runtime/provider checks. |
| V24 | Invalid request, denied tenant, preallocation limit, no-future-commit stale abort, unresolved save, committed no-op/failed publication before first pin. | Existing independently authorized errors/holds can return without a committed pin. Only verified committed execution replies require pin; no unpinned accepted success, forged commit/publication claim or blanket inability to return errors. No-op witness still authorizes not-applicable only, with exact body retention. |

#### Review-loop correction traceability

| Finding | Candidate correction | Future / local verification |
| --- | --- | --- |
| BH1-1 | A2 explicitly controls paired negotiation omission/emission. | V18, K06. |
| BH1-2 | A5 terminal AbortedStale forbids changed-version result reuse and same-operation regeneration. | V19, K07/K11. |
| BH1-3 and EC1-1 | A8 codec-03 outcome binds a complete ordered receipt set and all-member reduction. | V20, K09/K10. |
| BH1-4 | A8 limits pins to committed execution replies, preserving authorized errors and holds. | V24, V21–V23. |
| BH1-5 | A8 durable preparation/fenced input and Prepared bytes distinguish never-started from lost state. | K10 locally fixes four state-fixture bytes and coarse presence decisions, including pin/state contradiction. V21 retains future fencing, partial persistence, preparation-write/configuration authority and state/fence/tag semantic checks. |
| BH1-6 | A1/A4/A8 integrate handler short circuits and original admitted execution identity. | K12 locally checks identity equality and selection from a supplied admission fixture; it does not model handler branches or submitted-ID resolution. V22 retains future authenticated identity/admission, Replay/Recoverable/Pending/unknown, pin and access checks. |
| BH1-7 | A1/A8 bridge authorized status lookup to scope and preserve explicit legacy classification. | K10 locally fixes lookup bytes; K12 checks supplied classification/presence/ambiguity decisions. V23 retains future authenticated tenant/correlation lookup, authorization, latest-outcome verification and actual legacy fallback. |
| BH1-8 | Complete intent/origin/receipt/presave/readback/corrupt/outcome/pin bytes and malformed forms are executable. | K06–K12; provider acceptance remains V05–V24. |

BH1-9/10 concern pre-existing epic-context edits outside this candidate. Those bytes remain preserved; neither issue is closed by this document.

#### Local codec known-answer vectors

K01–K12 below are deterministic byte-arithmetic and explicitly local decision-model checks, **not** authenticated provider or admitted-domain fixtures. Their tiny marker after-images stand for exact staged bytes solely to isolate framing/hash dependencies; production V05–V24 must use real source schemas and provider proofs. Run the following with Python 3 from any directory. No repository file is written. The 40 literal length/hash pairs (five retained, 35 added) make codec changes detectable; full record decoding rejects malformed counts/tags/trailing bytes, and set checks reject missing/extra/duplicate rows. The generic parser does not validate preparation state range, positive owner-fence generation or state-specific optional-field presence; those semantic rejection checks remain in V21. K07 includes both derived certificate/origin rows, a deletion, exact after-images and the original +01:00 offset. The small decision models express only the stated local assertions, not every acceptance rule, provider authentication or production-path execution.

```python
import hashlib
import struct

H = lambda b: hashlib.sha256(b).digest()
U = lambda s: B(s.encode("utf-8"))
B = lambda b: struct.pack(">I", len(b)) + b
N = lambda n: struct.pack(">q", n)
I = lambda n: struct.pack(">i", n)
O = lambda b: b"\x00" if b is None else b"\x02" + b

def record(name, version, fields):
    return (name.encode("ascii") + b"\0" + bytes([version])
            + struct.pack(">H", len(fields))
            + b"".join(bytes([i]) + value for i, value in enumerate(fields, 1)))

scope = ("t", "d", "counter", "a", "op")
scope_hash = H(b"".join(U(s) for s in scope)).hex()
actor = "t:d:a"
result_key = "noop-result:" + scope_hash
witness_key = "noop-checkpoint:" + scope_hash
input_hash, preparation = H(b"input"), H(b"preparation")
body = b'{"isRejection":false,"events":[],"resultPayload":null}'

# K01: encoded size includes padded Base64, before string/array allocation.
assert 4 * ((1048576 + 2) // 3) == 1398104
assert 4 * ((67108864 + 2) // 3) == 89478488
assert 4 * ((1048577 + 2) // 3) == 1398104  # decoded limit still rejects +1

# K02: complete intended rows include the member-root after-image.
def put(kind, key, value):
    return bytes([kind, 1]) + U(key) + O(None) + O(H(value))

rows = [("event:1", put(1, "event:1", b"E\n")),
        ("batch-member-root:op", put(6, "batch-member-root:op", b"M\n"))]
rows.sort(key=lambda row: row[0].encode("utf-8"))
full = struct.pack(">I", 2) + b"".join(row for _, row in rows)
missing = struct.pack(">I", 1) + rows[1][1]
assert H(full) != H(missing)

# K03: source identity omits historical/fresh head and ETag observations.
backend = (b"HX-EV-STATE-BACKEND-1\0\x01" + U("fixture") + U("cluster")
           + U("ns") + H(b"endpoint") + U("store"))
prefix = (b"HX-EV-CORRUPT-SOURCE-1\0\x01" + B(backend) + U("incarnation-1")
          + b"".join(U(s) for s in scope[:4]) + U("event:7") + N(7))
source = H(prefix + H(bytes.fromhex("7bff7d")))
old_observation, new_observation = N(9) + U("etag-9"), N(10) + U("etag-10")
assert old_observation != new_observation
assert H(prefix + H(bytes.fromhex("7bff7d"))) == source
assert H(prefix + H(bytes.fromhex("7bfe7d"))) != source

# K04: result refers to witness key; witness hashes result, never itself.
result = record("HX-EV-NOOP-RESULT-1", 1, [
    U("op"), preparation, input_hash, U("V1"), O(None), B(body), H(body),
    U(witness_key), U("3")])
witness = record("HX-EV-NOOP-WITNESS-1", 1, [
    U(actor), U("t"), U("d"), U("counter"), U("a"), U("op"),
    preparation, input_hash, U("V1"), O(None), N(3), N(9), U(result_key),
    H(result), I(0), U("completed")])
assert len(witness) <= 4096 and H(result) in witness

# K05: unchanged first response despite a different later outcome body.
response = record("HX-EV-COMMAND-RESPONSE-1", 1, [
    I(202), struct.pack(">I", 0), B(b'{"status":"pending"}')])
later = record("HX-EV-COMMAND-RESPONSE-1", 1, [
    I(200), struct.pack(">I", 0), B(b'{"status":"published"}')])
append_hash, root_hash = H(b"event-result-record"), H(b"committed-batch-root")
def outcome(state, revision, payload, receipt):
    return record("HX-EV-COMMAND-OUTCOME-1", 3, [
        U("op"), U("t"), U("d"), U("a"), append_hash, root_hash,
        U(state), O(receipt), B(payload), N(revision), H(payload), N(3)])
first = outcome("pending", 0, b'{"status":"pending"}', H(b"complete-pending-observation"))
latest = outcome("published", 1, b'{"status":"published"}', H(b"complete-accepted-observation"))
pin = record("HX-EV-COMMAND-RESPONSE-PIN-1", 1, [
    U("op"), U("t"), U("d"), U("counter"), U("a"), input_hash,
    append_hash, root_hash, N(0), H(first), U("command-response:" + scope_hash),
    H(response), N(3)])
pinned = bytes(response)
assert H(pinned) != H(later) and pinned == response
assert H(first) in pin and H(latest) not in pin and H(response) in pin
expected = {
    "K02-rows": (111, "bd35942cb98c060394897991b7fa2b61c57d02f5e2dd97cc6b3f34125d1af583"),
    "K03-source-preimage": (214, "a4ddbe074b3542cd10561633fd86f5458f233624f0d428998d1ef8932b78fc27"),
    "K04-result": (288, "385b43680a4c69907f3cc32f3f6c5691e11669838fdfcd616587db4a78a71f18"),
    "K04-witness": (297, "5cb3bebaec182ea4441668581b67920bbaeb82464d33e219f09de7eef9081776"),
    "K05-response": (63, "e4964ee7775b165d8920356e453411f6c50c7ab73ad4f78177c452b1baf8b4c6"),
}
for name, value in [("K02-rows", full), ("K03-source-preimage", prefix + H(bytes.fromhex("7bff7d"))),
                    ("K04-result", result), ("K04-witness", witness), ("K05-response", response)]:
    assert (len(value), H(value).hex()) == expected[name], name
    print(name, len(value), H(value).hex())

# K06-K12 extend the K01-K05 definitions above; local codecs, not provider proof.
import json

R = lambda name, fields, version=1: record(name, version, fields)
C = lambda n: struct.pack('>I', n)
T = lambda ticks, offset: N(ticks) + struct.pack('>h', offset)

# K06: paired omission is explicit and preserves unrelated null presence.
def negotiate(value, mode=None, fingerprint=None):
    assert (mode is None) == (fingerprint is None)
    value = dict(value)
    if mode is not None:
        assert mode in ('V1', 'V2') and len(fingerprint) == 64
        assert all(c in '0123456789abcdef' for c in fingerprint)
        value.update(writerMode=mode, registryFingerprint=fingerprint)
    return json.dumps(value, ensure_ascii=False, separators=(',', ':')).encode()

implicit = negotiate({'isRejection': False, 'events': [], 'resultPayload': None})
assert implicit == b'{"isRejection":false,"events":[],"resultPayload":null}'
explicit = negotiate({'isRejection': False, 'events': [], 'resultPayload': None}, 'V1', 'ab'*32)
assert explicit == (b'{"isRejection":false,"events":[],"resultPayload":null,'
                    b'"writerMode":"V1","registryFingerprint":"' + b'ab'*32 + b'"}')
request_implicit = negotiate({'command': {}, 'currentState': None})
request_explicit = negotiate({'command': {}, 'currentState': None}, 'V2', 'ab'*32)
assert request_implicit == b'{"command":{},"currentState":null}'
assert request_explicit == (b'{"command":{},"currentState":null,"writerMode":"V2",'
                            b'"registryFingerprint":"' + b'ab'*32 + b'"}')
def negotiated(v):
    if 'writerMode' not in v and 'registryFingerprint' not in v:
        return 'implicit-v1'
    assert v.get('writerMode') in ('V1', 'V2')
    f = v.get('registryFingerprint')
    assert isinstance(f, str) and len(f) == 64 and all(c in '0123456789abcdef' for c in f)
    return v['writerMode']

def rejects(call):
    try:
        call()
    except (AssertionError, ValueError, UnicodeError, struct.error):
        return
    raise AssertionError('malformed input unexpectedly accepted')

assert negotiated(json.loads(implicit)) == 'implicit-v1'
assert negotiated(json.loads(explicit)) == 'V1'
for bad in [{'writerMode': None, 'registryFingerprint': None}, {'writerMode': 'V1'},
            {'registryFingerprint': 'ab'*32}, {'writerMode': 'V2', 'registryFingerprint': 'AB'*32}]:
    rejects(lambda bad=bad: negotiated(bad))

# Strict bounded parser for the complete records used below. A hash assertion
# fixes bytes; parsing verifies counts/tags/truncation/trailing-byte rejection.
class Cursor:
    def __init__(self, data):
        self.data, self.pos = data, 0
    def take(self, n):
        assert 0 <= n <= len(self.data)-self.pos
        out = self.data[self.pos:self.pos+n]
        self.pos += n
        return out
    def number(self, fmt):
        return struct.unpack(fmt, self.take(struct.calcsize(fmt)))[0]
    def field(self, kind):
        if kind.startswith('o'):
            tag = self.take(1)[0]
            assert tag in (0, 2)  # these candidate optionals forbid null
            return None if tag == 0 else self.field(kind[1:])
        if kind in ('u', 'b'):
            n = self.number('>I')
            value = self.take(n)  # bounded by remaining admitted record bytes
            return value.decode('utf-8') if kind == 'u' else value
        if kind == 'm':
            count = self.number('>I')
            assert count <= 64
            result, previous = {}, None
            for _ in range(count):
                assert self.take(1) == b'\1'
                key = self.field('u')
                assert key.isascii() and key == key.lower() and len(key) <= 256
                assert previous is None or previous < key.encode()
                previous = key.encode()
                assert self.take(2) == b'\2\1'
                value = self.field('u')
                assert len(value.encode()) <= 4096
                result[key] = value
            return result
        if kind == 'h': return self.take(32)
        if kind == 'n': return self.number('>q')
        if kind == 'i': return self.number('>i')
        if kind == 'x': return self.take(1)[0]
        raise ValueError(kind)
    def done(self): assert self.pos == len(self.data)

def parse(data, name, version, schema, cap=2*1048576):
    assert len(data) <= cap
    c = Cursor(data)
    assert c.take(len(name)+1) == name.encode() + b'\0'
    assert c.take(1) == bytes([version])
    assert c.number('>H') == len(schema)
    fields = []
    for tag, kind in enumerate(schema, 1):
        assert c.take(1) == bytes([tag])
        fields.append(c.field(kind))
    c.done()
    return fields

# K07: one complete V1 save, with all business + derived rows and a deletion.
# Opaque exact row contents below intentionally isolate codec dependencies;
# they do not claim a registered runtime envelope or a signed provider receipt.
timestamp = T(639028224000000000, 60)
assert timestamp.hex() == '08de48c8b4f80000003c'
assert T(639028224000000000, 0) != timestamp
raw = b'{"timestamp":"2026-01-01T01:00:00+01:00","payload":"e30="}'
encoding, digest, outbox = b'encoding-evidence\n', b'digest-evidence\n', b'outbox-intent\n'
stored_digest, fingerprint = H(b'stored-digest'), H(b'append-fingerprint')
leaf = H(b'HX-EV-BATCH-MEMBER-1\0\1' + I(0)+I(1)+U('event-message')+U('event:1')
         + H(raw)+stored_digest+H(outbox)+H(encoding)+H(digest)+H(body))
member = R('HX-EV-BATCH-MEMBER-ROOT-1', [U('op'), U(actor), I(1), leaf, H(body), fingerprint])
core = C(1)+B(raw)+B(encoding)+B(digest)+B(outbox)+B(member)
append_result = R('HX-EV-APPEND-RESULT-1', [U('op'),fingerprint,U(actor),N(3),B(body),H(body),I(1),H(core)])

def row(kind, key, after, before=None):
    action = 2 if after is None else 1
    return (kind, action, key, before, after)
def row_bytes(v, exact=False):
    kind, action, key, before, after = v
    value = bytes([kind, action])+U(key)+O(None if before is None else H(before))+O(None if after is None else H(after))
    return value + (O(None if after is None else B(after)) if exact else b'')
def rows_bytes(rows, exact=False):
    rows = sorted(rows, key=lambda r:r[2].encode())
    assert len({r[2] for r in rows}) == len(rows)
    return C(len(rows))+b''.join(row_bytes(r, exact) for r in rows)

business = [row(1,'event:1',raw),row(2,'event:1:encoding-evidence',encoding),
            row(3,'event:1:stored-digest-evidence',digest),row(4,'outbox:1',outbox),
            row(5,'aggregate-operation-result:op',append_result),
            row(6,'batch-member-root:'+scope_hash,member),
            row(7,'aggregate-metadata',b'head=1',b'head=0'),
            row(8,'pipeline:op',b'events-stored',b'processing'),
            row(9,'publication-index',b'event:1',b''),
            row(12,'recovery-owner:old',None,b'owner-2')]
intended = rows_bytes(business)
exact_business = rows_bytes(business,True)
presave = b'HX-EV-ACTOR-PRESAVE-BUNDLE-1\0\2'+B(exact_business)
intent = R('HX-EV-ACTOR-INTENT-1',[U(actor),U('t'),U('d'),U('counter'),U('a'),U('op'),N(3),B(intended)],2)
origin = R('HX-EV-V1-ORIGIN-1',[U('event:1'),U('t'),U('d'),N(1),H(raw),stored_digest,B(b'\1'+H(intent)+H(presave))])
actual = business+[row(126,'actor-intent:'+scope_hash,intent),row(127,'event:1:v1-origin',origin)]
complete_list = rows_bytes(actual)
complete_exact = rows_bytes(actual,True)
receipt = R('HX-EV-ACTOR-COMMIT-1',[U(actor),U('op'),N(3),U('fixture-backend'),B(b'fence-3'),U('etag-3'),N(41),H(intent),B(complete_list)],2)
readback = b'HX-EV-ACTOR-BUNDLE-1\0\2'+B(complete_exact)+U('etag-3')+N(3)

allowed_kinds = set(range(1,14))|{126,127}
def decode_rows(value, exact=False):
    c = Cursor(value)
    count = c.number('>I')
    assert count <= 16384
    result, previous = [], None
    for _ in range(count):
        kind, action = c.take(2)
        key, before, after = c.field('u'), c.field('oh'), c.field('oh')
        assert kind in allowed_kinds and action in (1,2) and len(key.encode()) <= 1024
        assert previous is None or previous < key.encode()
        previous = key.encode()
        assert (action == 1) == (after is not None)
        exact_value = c.field('ob') if exact else None
        if exact:
            assert (action == 1) == (exact_value is not None)
            assert exact_value is None or H(exact_value) == after
        result.append((kind,action,key,before,after,exact_value))
    c.done()
    return result

def verify_save(actual_rows, intended_rows, cert, org):
    aa = decode_rows(actual_rows)
    ii = decode_rows(intended_rows)
    derived = decode_rows(rows_bytes([row(126,'actor-intent:'+scope_hash,cert),row(127,'event:1:v1-origin',org)]))
    assert aa == sorted(ii+derived, key=lambda r:r[2].encode())

verify_save(complete_list,intended,intent,origin)
assert len(decode_rows(complete_exact,True)) == 12
assert any(r[1] == 2 and r[5] is None for r in decode_rows(complete_exact,True))
for altered in [actual[:-1], actual[:-2], actual+[row(10,'snapshot:extra',b'extra')],
                [r for r in actual if r[0] != 6], [r for r in actual if r[1] != 2]]:
    rejects(lambda altered=altered: verify_save(rows_bytes(altered),intended,intent,origin))
rejects(lambda: rows_bytes(actual+[actual[0]]))
for malformed in [C(16385), C(13)+complete_list[4:], C(11)+complete_list[4:], complete_list+b'\0',
                  complete_list[:4]+b'\xff'+complete_list[5:]]:
    rejects(lambda malformed=malformed: decode_rows(malformed))
# Reject an explicit-null optional and changed after-image even with same row key.
small = rows_bytes([row(1,'k',b'v')],True)
rejects(lambda: decode_rows(small[:11]+b'\1'+small[12:],True))
rejects(lambda: decode_rows(small[:-1]+b'w',True))

# K08: complete corrupt disposition retains original observation independently.
corrupt = R('HX-EV-RAW-CORRUPT-1',[U('t'),U('d'),U('counter'),U('a'),U('event:7'),N(7),
             H(bytes.fromhex('7bff7d')),N(9),U('etag-9'),H(b'raw-proof-9'),U('capture:7'),
             H(b'operator-decision'),N(639028224000000000),B(backend),U('incarnation-1'),source],2)
assert parse(corrupt,'HX-EV-RAW-CORRUPT-1',2,['u']*5+['n','h','n','u','h','u','h','n','b','u','h'],65536)[15] == source
assert H(prefix+H(bytes.fromhex('7bff7d'))) == source  # later head is not an input

# K09: two distinct events, one pinned destination each, complete reduction.
publication_append_result = R('HX-EV-APPEND-RESULT-1',[U('op'),H(b'two-event-fingerprint'),U(actor),N(3),
    B(body),H(body),I(2),H(b'two-event-core-bundle')])
# Marker bundle/receipt bytes are local codec inputs, not a production commit.
expected_entries = [(0,'event-message-a','component/topic',H(b'outbox-a'),H(b'pin-a')),
                    (1,'event-message-b','component/topic',H(b'outbox-b'),H(b'pin-b'))]
assert len({e[0] for e in expected_entries}) == len(expected_entries)
assert len({e[1] for e in expected_entries}) == len(expected_entries)
def expected_entry(v):
    ordinal,msg,dest,oh,ph = v
    return I(ordinal)+U(msg)+U(dest)+oh+ph
expected_blob = C(2)+b''.join(expected_entry(e) for e in expected_entries)
expected_set_hash = H(b'HX-EV-PUBLICATION-EXPECTED-1\0\1'+B(expected_blob))

def publication_set(states, attempts=(1,1)):
    assert len(states) == len(expected_entries)
    rows = []
    for index,(entry,state,attempt) in enumerate(zip(expected_entries,states,attempts)):
        assert state in range(4) and attempt > 0
        # Marker receipt/proof hashes are codec inputs, not signed authority.
        rh = H(('receipt-%d-%d-%d'%(index,state,attempt)).encode()) if state in (1,2) else None
        proof = H(('observation-%d-%d-%d'%(index,state,attempt)).encode())
        rows.append(expected_entry(entry)+N(attempt)+bytes([state])+O(rh)+proof)
    return R('HX-EV-PUBLICATION-SET-1',[U('op'),H(publication_append_result),H(b'committed-batch-root'),expected_set_hash,B(b'observation-fence'),B(C(2)+b''.join(rows))])

def evidence_token(prior, index, old_attempt, new_attempt, purpose):
    # Local test token only. Production requires authenticated provider/admission
    # proof of prior-attempt closure/no-future-acceptance and renewed admission.
    return H(b'local-evidence-token\0'+H(prior)+I(index)+N(old_attempt)+N(new_attempt)+U(purpose))

def reduce_set(value, prior=None, closures=None, retry_admissions=None):
    v = parse(value,'HX-EV-PUBLICATION-SET-1',1,['u','h','h','h','b','b'])
    assert v[:4] == ['op',H(publication_append_result),H(b'committed-batch-root'),expected_set_hash]
    c = Cursor(v[5]); count=c.number('>I')
    assert count == len(expected_entries) and count <= 1000
    states=[]; observed=[]; attempts=[]
    for e in expected_entries:
        start=c.pos
        actual_entry=(c.field('i'),c.field('u'),c.field('u'),c.field('h'),c.field('h'))
        assert actual_entry == e
        attempt,state,rh,proof=c.field('n'),c.field('x'),c.field('oh'),c.field('h')
        assert attempt > 0 and state in range(4)
        assert (state in (1,2)) == (rh is not None)
        # Real provider tests must verify receipt/proof signature and fence.
        states.append(state); observed.append(v[5][start:c.pos]); attempts.append(attempt)
    c.done()
    if prior is not None:
        _, prior_states, prior_rows=reduce_set(prior)
        for i,state in enumerate(prior_states):
            before = Cursor(prior_rows[i])
            before.take(len(expected_entry(expected_entries[i])))
            old_attempt = before.field('n')
            new_attempt = attempts[i]
            assert new_attempt >= old_attempt
            if state == 1:
                assert observed[i] == prior_rows[i]
            if state in (2,3) and states[i] == 0:
                assert new_attempt > old_attempt
            if state == 2 and new_attempt == old_attempt:
                assert states[i] == 2  # a definitive failure cannot later accept
            if new_attempt > old_attempt:
                assert state in (2,3)
                purpose = 'failure-closed' if state == 2 else 'no-acceptance-no-future-acceptance'
                assert (closures or {}).get(i) == evidence_token(prior,i,old_attempt,new_attempt,purpose)
                assert (retry_admissions or {}).get(i) == evidence_token(prior,i,old_attempt,new_attempt,'retry-admitted')
    state = ('published' if all(s==1 for s in states) else 'unknown' if 3 in states
             else 'pending' if 0 in states else 'failed')
    return state,states,observed

partial_set=publication_set([1,0]); published_set=publication_set([1,1])
failed_set=publication_set([1,2]); unknown_set=publication_set([1,3])
assert reduce_set(partial_set)[0] == 'pending'
assert reduce_set(published_set,partial_set)[0] == 'published'
assert reduce_set(failed_set,partial_set)[0] == 'failed'
assert reduce_set(unknown_set,partial_set)[0] == 'unknown'
failed_closure={1:evidence_token(failed_set,1,1,2,'failure-closed')}
failed_retry={1:evidence_token(failed_set,1,1,2,'retry-admitted')}
unknown_closure={1:evidence_token(unknown_set,1,1,2,'no-acceptance-no-future-acceptance')}
unknown_retry={1:evidence_token(unknown_set,1,1,2,'retry-admitted')}
renewed=publication_set([1,0],(1,2))
assert reduce_set(renewed,failed_set,failed_closure,failed_retry)[0] == 'pending'
assert reduce_set(renewed,unknown_set,unknown_closure,unknown_retry)[0] == 'pending'
rejects(lambda:reduce_set(partial_set,failed_set,failed_closure,failed_retry))  # same attempt
rejects(lambda:reduce_set(partial_set,unknown_set,unknown_closure,unknown_retry))  # same attempt
rejects(lambda:reduce_set(renewed,failed_set))  # no closure/admission
rejects(lambda:reduce_set(renewed,unknown_set))  # unresolved prior send
rejects(lambda:reduce_set(renewed,unknown_set,unknown_closure))  # no retry admission
rejects(lambda:reduce_set(renewed,unknown_set,failed_closure,unknown_retry))  # foreign closure
rejects(lambda: reduce_set(publication_set([0,1]),partial_set))
# A structurally valid complete record with missing/extra/misordered rows fails.
v=parse(partial_set,'HX-EV-PUBLICATION-SET-1',1,['u','h','h','h','b','b'])
_,_,pub_rows=reduce_set(partial_set)
for badrows in [C(1)+pub_rows[0],C(3)+b''.join(pub_rows)+pub_rows[0],
                C(2)+pub_rows[1]+pub_rows[0],C(2)+pub_rows[0]*2]:
    badset=R('HX-EV-PUBLICATION-SET-1',[U(v[0]),v[1],v[2],v[3],B(v[4]),B(badrows)])
    rejects(lambda badset=badset: reduce_set(badset))

# K10: complete record bytes, four state fixtures and coarse presence decisions.
def M(headers):
    return C(len(headers))+b''.join(b'\1'+U(k)+b'\2\1'+U(v) for k,v in sorted(headers.items()))
location='https://example.test/api/v1/commands/status/op'
public_body=b'{"correlationId":"corr-original","resultPayload":null,"messageId":"op"}'
http_response=R('HX-EV-COMMAND-RESPONSE-1',[I(202),M({'content-type':'application/json','location':location,'retry-after':'1'}),B(public_body)])
root_hash=H(b'committed-batch-root')
def complete_outcome(pubset,revision,body_bytes):
    state=reduce_set(pubset)[0]
    return R('HX-EV-COMMAND-OUTCOME-1',[U('op'),U('t'),U('d'),U('a'),H(publication_append_result),root_hash,
        U(state),O(H(pubset)),B(body_bytes),N(revision),H(body_bytes),N(3)],3)
noop_outcome=R('HX-EV-COMMAND-OUTCOME-1',[U('op'),U('t'),U('d'),U('a'),H(result),H(witness),
    U('not-applicable'),O(None),B(public_body),N(0),H(public_body),N(3)],3)
assert body != public_body  # domain no-op bytes and public bytes remain distinct
outcome0=complete_outcome(partial_set,0,public_body)
outcome1=complete_outcome(published_set,1,b'{"status":"published"}')
full_pin=R('HX-EV-COMMAND-RESPONSE-PIN-1',[U('op'),U('t'),U('d'),U('counter'),U('a'),input_hash,
    H(publication_append_result),root_hash,N(0),H(outcome0),U('command-response:'+scope_hash),H(http_response),N(3)])
head0=R('HX-EV-COMMAND-OUTCOME-HEAD-1',[U('command-outcome:'+scope_hash+':0'),N(0),H(outcome0),O(None)])
head1=R('HX-EV-COMMAND-OUTCOME-HEAD-1',[U('command-outcome:'+scope_hash+':1'),N(1),H(outcome1),O(H(outcome0))])
lookup=R('HX-EV-COMMAND-SCOPE-1',[U('t'),U('op'),U('d'),U('counter'),U('a'),bytes.fromhex(scope_hash),input_hash,H(b'admission-proof'),U('required'),U('corr-original')])
render_input=R('HX-EV-RESPONSE-INPUT-1',[bytes.fromhex(scope_hash),input_hash,H(publication_append_result),root_hash,H(receipt),N(3),U('pending'),O(H(partial_set)),H(b'renderer-options'),U('corr-original'),U(location),H(lookup)])
def prep(state,previous=None):
    return R('HX-EV-RESPONSE-PREPARATION-1',[bytes.fromhex(scope_hash),input_hash,B(b'admission-proof'),N(1),bytes([state]),
        O(H(render_input) if state>=1 else None),O(H(http_response) if state>=2 else None),
        O(H(outcome0) if state>=2 else None),O(H(full_pin) if state>=3 else None),O(None if previous is None else H(previous))])
never=prep(0); rendering=prep(1,never); prepared=prep(2,rendering); pinned_state=prep(3,prepared)
assert H(outcome0) in full_pin and H(outcome1) not in full_pin
assert H(http_response) in full_pin and H(lookup) in render_input
assert parse(lookup,'HX-EV-COMMAND-SCOPE-1',1,['u']*5+['h','h','h','u','u'],4096)[1]=='op'
# This model has one combined output-presence flag, not individual blob states.
# It does not validate fencing, preparation-write proof or original configuration.
def response_recovery(state,admission,bytes_available,pin_present,pin_verified):
    if not admission: return 'hold'
    if pin_present and state < 2: return 'hold'
    if pin_present: return 'return-pinned' if pin_verified and bytes_available else 'hold'
    if state == 0: return 'begin-once'
    if state in (1,2) and bytes_available: return 'verify-prepared-then-cas'
    return 'hold'
assert response_recovery(0,True,False,False,False)=='begin-once'
assert response_recovery(1,True,False,False,False)=='hold'
assert response_recovery(2,True,False,False,False)=='hold'
assert response_recovery(0,False,False,False,False)=='hold'
for contradictory_state in (0,1):
    assert response_recovery(contradictory_state,True,True,True,True)=='hold'
assert response_recovery(3,True,True,True,True)=='return-pinned'

# K11: exact stale-abort bytes and no-future-commit cleanup decision.
abort=R('HX-EV-APPEND-ABORT-1',[U('t'),U('d'),U('counter'),U('a'),U('op'),preparation,
    H(b'capsule-manifest'),N(3),H(b'no-future-commit-proof'),U('stale-source')])
def stale(committed,no_commit,fenced):
    if committed: return 'reconcile-original-commit'
    return 'abort-stale' if no_commit and fenced else 'hold'
assert stale(False,True,True)=='abort-stale'
assert stale(False,True,False)=='hold'
assert stale(True,True,True)=='reconcile-original-commit'

# K12: identity equality/selection from a supplied admission fixture only.
# Submitted IDs are unused; handler branches/authentication/pin lookup are not modeled.
# Status decisions take supplied flags, without resolving tenants/correlations.
def retry(submitted_msg,submitted_corr,protected_identity,admission):
    assert protected_identity == admission['identity']
    return admission['execution'],http_response
admission={'identity':H(b'protected-command'),'execution':'op'}
assert retry('retry-message','retry-correlation',H(b'protected-command'),admission)==('op',http_response)
rejects(lambda:retry('retry-message','retry-correlation',H(b'changed-payload'),admission))
def status(classification,lookup_present,outcome_present,legacy_success,ambiguous=False):
    if ambiguous: return '409'
    if classification=='required': return 'verified' if lookup_present and outcome_present else 'hold'
    if classification=='legacy': return 'legacy' if legacy_success else '404'
    return 'hold'
assert status('required',True,False,True)=='hold'
assert status('required',False,False,True)=='hold'
assert status(None,False,False,True)=='hold'
assert status('legacy',False,False,True)=='legacy'
assert status('required',True,True,True,True)=='409'

# Every changed complete record is parsed and mutated at count/tag/trailing bytes.
records = [
 ('K10-noop-outcome',noop_outcome,'HX-EV-COMMAND-OUTCOME-1',3,['u']*4+['h','h','u','oh','b','n','h','n']),
 ('K07-intent',intent,'HX-EV-ACTOR-INTENT-1',2,['u']*6+['n','b']),
 ('K07-origin',origin,'HX-EV-V1-ORIGIN-1',1,['u','u','u','n','h','h','b']),
 ('K07-receipt',receipt,'HX-EV-ACTOR-COMMIT-1',2,['u','u','n','u','b','u','n','h','b']),
 ('K08-corrupt',corrupt,'HX-EV-RAW-CORRUPT-1',2,['u']*5+['n','h','n','u','h','u','h','n','b','u','h']),
 ('K09-partial-set',partial_set,'HX-EV-PUBLICATION-SET-1',1,['u','h','h','h','b','b']),
 ('K09-published-set',published_set,'HX-EV-PUBLICATION-SET-1',1,['u','h','h','h','b','b']),
 ('K10-outcome-zero',outcome0,'HX-EV-COMMAND-OUTCOME-1',3,['u']*4+['h','h','u','oh','b','n','h','n']),
 ('K10-outcome-one',outcome1,'HX-EV-COMMAND-OUTCOME-1',3,['u']*4+['h','h','u','oh','b','n','h','n']),
 ('K10-response',http_response,'HX-EV-COMMAND-RESPONSE-1',1,['i','m','b']),
 ('K10-pin',full_pin,'HX-EV-COMMAND-RESPONSE-PIN-1',1,['u']*5+['h','h','h','n','h','u','h','n']),
 ('K10-head-zero',head0,'HX-EV-COMMAND-OUTCOME-HEAD-1',1,['u','n','h','oh']),
 ('K10-head-one',head1,'HX-EV-COMMAND-OUTCOME-HEAD-1',1,['u','n','h','oh']),
 ('K10-lookup',lookup,'HX-EV-COMMAND-SCOPE-1',1,['u']*5+['h','h','h','u','u']),
 ('K10-render-input',render_input,'HX-EV-RESPONSE-INPUT-1',1,['h']*5+['n','u','oh','h','u','u','h']),
 ('K10-never',never,'HX-EV-RESPONSE-PREPARATION-1',1,['h','h','b','n','x']+['oh']*5),
 ('K10-rendering',rendering,'HX-EV-RESPONSE-PREPARATION-1',1,['h','h','b','n','x']+['oh']*5),
 ('K10-prepared',prepared,'HX-EV-RESPONSE-PREPARATION-1',1,['h','h','b','n','x']+['oh']*5),
 ('K10-pinned-state',pinned_state,'HX-EV-RESPONSE-PREPARATION-1',1,['h','h','b','n','x']+['oh']*5),
 ('K11-abort',abort,'HX-EV-APPEND-ABORT-1',1,['u']*5+['h','h','n','h','u']),
]
for label,value,name,version,schema in records:
    parse(value,name,version,schema)
    off=len(name)+2
    for bad in [value[:off]+b'\0\0'+value[off+2:],value[:off+2]+b'\xff'+value[off+3:],value+b'\0',value[:-1]]:
        rejects(lambda bad=bad:parse(bad,name,version,schema))

known=[('K06-implicit',implicit),('K06-explicit',explicit),('K06-request-implicit',request_implicit),
       ('K06-request-explicit',request_explicit),('K07-core',core),('K07-intended',intended),
       ('K07-exact-business',exact_business),('K07-presave',presave),('K07-complete-list',complete_list),
       ('K07-complete-exact',complete_exact),('K07-readback',readback),('K07-original-offset',timestamp),
       ('K09-expected',expected_blob),('K09-failed-set',failed_set),('K09-unknown-set',unknown_set),
       ] + [(label,value) for label,value,_,_,_ in records]
expected_extra = {
    'K06-implicit': (54, 'ba6a0ec87292646726a8d01d43b5c659ac7f3b3f218ce070f874fdc065510d15'),
    'K06-explicit': (161, '29259d6d8a02cc225b824f7098b7bca439a33083d82d50c769b5d18a6034ca3f'),
    'K06-request-implicit': (34, 'b03f05906a6db4583519dc300b99c1635c87a15ea8dfec547db573e334cd8376'),
    'K06-request-explicit': (141, '8379b631a2d766c6bc75a02ff9a26c71496735fd7a78628a208716fb5b2baaa7'),
    'K07-core': (280, '7e98ca0a3efae25a51c1fa9d85ea846121838479645338301ddcfd5f76da7f2e'),
    'K07-intended': (745, '21a7724026c207307fa3e8eb6e94997fea3d0182d08e549b3d334c1197f6bec4'),
    'K07-exact-business': (1287, 'b606debc141951b2d34e25a89681c38ab4b9974ddc190f3f466e1afcdf16afbe'),
    'K07-presave': (1321, '09f9d8a89313c28a8f6a782493c84ef31d3d3630d3d9b8a584b4ed44d9ba69f3'),
    'K07-complete-list': (919, '8562980a54f4ce0ba9fbf429b3e70d0486df345c23ae3f1a6f440002afd82215'),
    'K07-complete-exact': (2491, '135eeb1bb8e99668a37f3cf75851a743abedeb88f02b0f245535185c3c356445'),
    'K07-readback': (2535, 'c2ab699898e6885a28f1efd0bfbb4e3b19a54cabf502c14763d9ce277e3c8c2a'),
    'K07-original-offset': (10, '2de6eea083ab6c59fd3b949948f51f2b4a627e2eeded6cd4725ec928a4986539'),
    'K09-expected': (216, 'dc8d8ee50a4ff476169b283c4a8163bbec19e0395c0262681c484236ede2c78f'),
    'K09-failed-set': (524, 'b089ae018877e5d63e164793412df9fdd06715ca6ee034362a9de76f26317855'),
    'K09-unknown-set': (492, 'e0893bf7943bc3c828d7c08535e766bea7631214189f88c79561dceefe6dce13'),
    'K10-noop-outcome': (266, '5564c0404072191d65f39d2ff282f7189264911e8cdd7144069dc07459b1232e'),
    'K07-intent': (830, '986d631318c656e1caf3dc96597232fa2c5b11221bbf7f8e1883989434c5e31e'),
    'K07-origin': (190, '45094b0d18315de296fb7f9706b503d282a92da2fef6b0ffa07aed827daf2435'),
    'K07-receipt': (1059, '32c3381e49b33815d2061c5a47d42279d9a3fd40ce65f3f2dcb4a98761ffaf8c'),
    'K08-corrupt': (364, 'a7ec2aec1eba57546886bb0917d4477f8af75edb895435513b85d5b3fc40b581'),
    'K09-partial-set': (492, '8a9c928aceef1f7d1d9fc34b7bdd2e6be453995f64352116002e996a229e8ca6'),
    'K09-published-set': (524, 'd327210e80ad11fabb025401a2ff0bccc8987adc7fb6dc77e4f2a10ca30e7017'),
    'K10-outcome-zero': (291, 'd682abf3bc4a810f98a23e8fa0928ae3ddb14c56484795985de6df762c5b4fb9'),
    'K10-outcome-one': (244, '14bac5867c2d65fd6eec977249dd01b64ba95c773940c1934281805f161846ab'),
    'K10-response': (241, '73665d48b82c95d2dd709637e1575e2f8facd440958c4307e9de4f09e9e3c7a9'),
    'K10-pin': (338, 'c067d1e99b8960af3090c364598b94283ccac9e45d27f2750939875f28c1fbd1'),
    'K10-head-zero': (163, '5635836a27a4641871618981f71dc03b1152d512f0a621c49f7f4d546bc40369'),
    'K10-head-one': (195, 'c857de8d7e70e9c1518e87ae90713037cd409d2d40308b7d89e5c5fb7fdcf142'),
    'K10-lookup': (192, '3fb02786199a9b960db0a00a5b48a200e03cc1052357d5b748e9761db76b0177'),
    'K10-render-input': (381, '6e118c168e4ff29ca59814ab620a2840c25970a0be8613e109eb2e779be48246'),
    'K10-never': (139, '7433b144becc4696f589f38411a60f9d81cac2287802796cef6376eb58a249bc'),
    'K10-rendering': (203, '14dc2d7e6f50a2fd128bc3b5b6961652e9bbe7e75b0e5d17029830ebdbc7a499'),
    'K10-prepared': (267, '66e52b78c59af09b5be00546f5cc13edb3665fe91404ebd085f623441092508f'),
    'K10-pinned-state': (299, '379ef2de03c6d399bb348da01a44801907d26696fe70df5cc8409da6244503be'),
    'K11-abort': (186, 'c4eb572200c78fbc08cae6c47bfd580e14a6fbfc8dc52243c5736736d1c4607a'),
}
for label,value in known:
    assert (len(value),H(value).hex()) == expected_extra[label],label
    print(label,len(value),H(value).hex())
print('K06-K12 strict-codec and decision-model checks passed')
```

### Integration handoff

6.5b consumes the complete-set codec-02 certificate/receipt and retained committed-generation images, V1 origin distinction, fresh versus historical corrupt proof, immutable offset/protection/digest rules and A3 shared scratch limits. It must not accept a caller-built receipt or require old head equality after a valid append. 6.5c consumes the same member-root/result payload hash meaning, A8 codec-03 complete ordered publication receipt set (one destination/event, at most 1,000 members/2 MiB; 64 MiB referenced evidence, preflighted/reserved before append), no-op `not-applicable`, durable response-preparation states, original execution response pin versus authenticated latest-status lookup, provider authority/retention and writer/rollback gates. Reconcile A8's explicit Review-32 replacement across both children: one execution MessageId has at most one scope per tenant; same-tenant cross-scope reuse conflicts at admission, while cross-tenant reuse remains independent. Story 6.5/6.5c integration must supply A8's exact compatibility-preserving mapping from private failed to permanent public PublishFailed, including terminality evidence, existing public fields/polling semantics and allowed transitions; until then, or without that evidence, public outcome selection holds as specified in V20/V23 while append truth and existing pins remain intact. Shared outcome observations must preserve accepted members and reconcile unknown sends before retry. A5 terminal stale-abort is not permission to rerender or reexecute the same operation. These are concrete candidate contracts to reconcile into one artifact, not optional choices for separate implementations. Unassigned `BH37-3/4/5/9/10` remain with their owning integration work; this candidate neither closes them nor changes their historical rows.

### Verification evidence

Validation is documentation/codec validation only. Required checks are `git diff --check`, the five-disposition/`UNAPPROVED` search, the local codec block above, repository-relative source/link resolution, and byte-for-byte comparison of the normative draft and historical triage against their initial contents. Runtime source and tests must remain unchanged. The repository's markdownlint ignore list excludes `_bmad-output/**`; running a runtime build or tests would not verify this specification's future provider semantics.

Executed on 2026-09-27 after review-loop corrections:

- `git diff --check` — passed, no whitespace errors.
- `rg -n 'BH37-(1|2|6|7|8)|UNAPPROVED' _bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md` — all five explicit accepted disposition rows present; original receipt still unapproved. A Python row-count check confirmed exactly one disposition per assigned finding.
- Extracted and executed the single Python block above with `exec(compile(block, candidate_path, 'exec'))` — K01–K12 passed, including all 40 literal length/SHA-256 pairs and malformed/complete-set/recovery/identity model assertions. Checked 32 named source paths (expanding braces), eight relative file links/section anchors, and contiguous V01–V24 labels — all resolve.
- Compared complete draft and historical triage bytes with `git show HEAD:<path>` — unchanged. Full-file SHA-256: draft `bcf6eee0b2d0795fa8a53b6d4e99ae0fd1ea79896acfca9927f5e448042f3d4a`; triage `cce68e371ed048bebf7f93957f834959fb2ad4612a3e08235099e1adf699449f`. This also preserves all original signed fixture literals and the six-field receipt.
- The implementation agent compared SHA-256 against its pre-edit snapshots for the user epic context (`c51df6e5408c310e0b97e93ef2fb6befd0c3caa3bd4f67ac302afbe5a8842e21`) and execution spec (`866f19ecb618720e7e5b92632b1b29e1635870218b8e3547beeeb15340497028`) — unchanged by that agent. The parent subsequently updated execution status and review evidence; the user epic context remains byte-for-byte unchanged.
- `git diff --name-only -- src tests _bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md _bmad-output/implementation-artifacts/story-6-5-review-triage.md` — empty. Only this work-story file was edited by this implementation; pre-existing context/sprint/execution-spec work was preserved.

Review-pass-3 patch verification on 2026-09-27: corrected the explicit Review-32 replacement, draft outcome codec reference/tag-07 extension, V07 continuation/abort split, and local-model verification claims; added the three missing `status: open` lines in `deferred-work.md`. These patches changed only this candidate and those ledger lines. Re-executed K01–K12: all 40 fixed length/hash pairs and embedded assertions passed, with every literal pair unchanged from HEAD. Both NeverStarted/Rendering pin contradictions hold; removing the new model guard makes the new assertions fail. These assertions do not prove the future provider checks now identified in V21–V23.

The pass-3 structural check found exactly five assigned disposition rows, contiguous V01–V24, all three targeted ledger entries open, 33 resolving named source/test paths and eight resolving relative links/anchors. `git diff --check` passed. Pre-edit hash comparisons preserved the normative draft, historical triage, user epic context, execution spec and sprint tracker; the draft and triage also match HEAD byte-for-byte. The runtime/test/normative/triage diff remained empty.

Resumed-build review verification on 2026-09-27: all three independent review layers completed and the verification-gap reviewer found no gaps. A1/A8/V22 now name and pin the committed rejection exception path. A8/A9/V20/V23 hold public outcomes for private per-attempt failure until the explicit 6.5/6.5c terminal mapping is integrated; existing pins and append truth remain intact. The parent reran K01–K12 with all 40 unchanged fixed length/hash pairs and guard-removal regression sensitivity, resolved 36 source/test paths and eight links/anchors, checked five disposition rows and V01–V24, and passed `git diff --check`. Frozen intent, normative draft, historical triage and original user-context bytes remain unchanged, and runtime/tests have no diff. Existing integration/context deferrals remain open; preparation-write authority and the permanent-failure mapping are recorded in the deferred ledger. Runtime/provider vectors remain future requirements.

Remaining work is Story 6.5 review/integration with 6.5b/6.5c and the human gate, then separately authorized runtime implementation/provider verification. In particular, no current provider is claimed to support complete historical-generation readback or purpose-11 receipts, and the local arithmetic vectors cannot establish those capabilities.
