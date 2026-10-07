# Dapr logical claim design candidate

Status: **unapproved, non-authorizing design preparation**. This document changes
no normative schema, assigned purpose, trust map, historical fixture or runtime
registration. Its field choices require an owner decision before implementing a
semantic signer/verifier or admitting these claims to consumers.

The compatible outer `HX-EV-PROOF-1` container is independent of claim contents;
the dormant framing owner can retain its exact entries without authenticating
them. The historical route/prefix **meaning** is not interchangeable with the
current Dapr logical evidence. Historical route tag `01` is `StoredDigest`, whose
specified preimage includes a raw provider renderer, provider encoding identity
and derived protection evidence. Current
[`EventLogicalDigest`](../../../../../src/Hexalith.EventStore.Server/Events/EventLogicalDigest.cs)
length-frames the literal `eventstore.logical-payload.v1`, tenant/domain/aggregate
ID, MessageId/correlation/causation, stored type, application format and optional
canonical type; it appends sequence, metadata/payload versions and the SHA-256 of
exact application payload bytes, returning lowercase hex. It does not bind every
consumed metadata field, aggregate type, physical bytes or a historical generation.
A separate consumed-metadata/source comparison remains necessary. A same-size
SHA-256 value does not make these evidence models
equivalent. Purpose-10/11 receipts, historical committed-generation witness and
retroactive raw-provider capture are withdrawn. None may be reconstructed from
typed Dapr actor readback or hidden database tables.

The proposed path is a separately identified **Dapr actor logical evidence
model**, with distinct claim separators and a required explicit fleet capability.
Keep old fields/claims immutable. Never put an application digest into an existing
`StoredDigest` property and treat it as historical authority. The current public
`VerifiedEffectiveEventView`/command carriers therefore cannot by themselves be
the new verified intake; choose a new additive logical carrier or an explicitly
versioned discriminated carrier before integration.

| Record/field | Historical meaning | Proposed Dapr logical meaning or disposition |
| --- | --- | --- |
| Route separator/codec | `HX-EV-ROUTE-1`, codec 01, 18 fields | Candidate `HX-EV-DAPR-ROUTE-1`, codec 01, 20 fields; new schema identity, not an approved assignment. |
| Route tag `01` | B32 `StoredDigest` from physical/provider evidence | B32 `ApplicationLogicalDigest` matching the actor-owned digest and exact admitted logical value under the selected source binding. Distinct field name and separator. |
| Route tags `02..12` | Scope, sequence/MessageId, stored type/version/format, target, registry fingerprint, effective payload hash/format, consumed metadata hash | Preserve addressed fields and scalar encodings. Recompute consumed metadata with a **distinct candidate** `HX-EV-DAPR-CONSUMED-1` domain separator and the logical digest. No provider interpretation. |
| New route tags `13`, `14` | Absent | B32 `SourceBindingHash`, U `LogicalEvidenceModelId` (candidate literal `dapr-actor-logical-v1`). |
| Prefix separator/codec | `HX-EV-PREFIX-1`, codec 01, 15 fields | Candidate `HX-EV-DAPR-PREFIX-1`, codec 01, 17 fields. |
| Prefix tags `01..09`, `0c` | Addressed scope, start/end/head/target/count and registry fingerprint | Preserve scalar meanings; head/target come from the admitted addressed actor source and are fixed for this operation. |
| Prefix tags `0a`, `0b` | Ordered list and cumulative accumulator over historical `StoredDigest` | Separate candidate `HX-EV-DAPR-PREFIX-LIST-1` / accumulator domains, over ordered `(sequence, ApplicationLogicalDigest)` and the same source/model binding. Exact genesis/step preimages must be selected together with source-head and restart semantics. |
| Prefix tag `0d` | Checkpoint anchor for qualified historical/named projection state | Absent in the first candidate. Non-null logical checkpoint anchors need a distinct qualified checkpoint schema and exact durable state/control-owner readback; outer framing alone cannot admit them. |
| Prefix tags `0e`, `0f` | Witnessed snapshot digest/prefix accumulator | Absent in the first candidate. Select the Story 6.1 logical snapshot witness/serializer binding and verify it through the owning actor before enabling snapshot tails. No historical-generation claim. |
| New prefix tags `10`, `11` | Absent | B32 `SourceBindingHash`, U `LogicalEvidenceModelId`, matching every route. |
| Route/prefix signing purpose and trust map | Assigned purposes 01/03 under existing exact schemas | **Unselected.** Choose explicit reuse with these distinct separators and a model-scoped key/capability, or an approved new purpose assignment. This candidate assigns neither and grants no key authority. |
| Command continuation/state, checkpoint, delivery/effect keys | Bind historical digest/proof/accumulator semantics | Require their own field-level logical schema decisions and compatibility advertisement. Never silently rename their existing digest/hash fields. Remain fenced. |

Tags above are hexadecimal and are candidate fields in **new** records only.
No existing row kind or purpose is added by this document. Individual existing
page/proof/route and combined scratch ceilings still apply; a new evidence model
does not raise them.

The proposed `SourceBindingHash` is one **actor/range binding**, shared by every
route and the prefix. It identifies the actual actor application, actor type/ID,
tenant/domain/aggregate route, fixed head/target, immutable event-key mapping
codec/prefix/configuration, pinned source/profile configuration and digest codec
implementation/options. It does **not** include a varying per-event key. For each
route, derive the exact addressed logical event key from that pinned mapping and
its signed sequence, and compare it with the independently addressed actor read;
never accept an arbitrary caller key. This choice avoids claiming that distinct
event keys have an equal binding hash. The complete canonical binding record/hash
and key-mapping codec are implementation inputs still to be selected. A friendly
`storeId` or caller-supplied DTO cannot establish this binding. The source must
read via Dapr, verify original application bytes and relevant metadata/digest,
admit the complete contiguous prefix and hold a fixed head/target before signing.
A returned current logical value proves that logical readback; it cannot prove
an independently authenticated older committed generation. Legacy events with
no same-save digest require an explicit logical-source admission policy rather
than a fabricated historical witness.

Concrete choices needed for the next integrated implementation are:

1. The new logical model's exact separators/field counts, signing-purpose/trust
   decision, carrier choice, consumed-metadata/list/accumulator preimages and
   legacy-without-digest policy. The owner must decide these **semantics**, not
   merely approve a production deployment.
2. Actual source actor app/type/ID/key mapping and fixed-head/readback protocol;
   the selected serving runtime/shared-framework/native immutable execution-image
   plan and complete executable/catalog closure. Repository file hashes do not
   select executed loader images, native loading or serving-peer membership.
3. Canonical Counter/Greeting identities/current versions and V/A validator,
   deserializer/options/identity policy; state schema/canonical serializer and
   S/readback rows. Source proposals and detached scalar copies do not supply
   canonical state bytes or complete verified route catalogs.
4. For durable continuations/checkpoints/effects, exact actor/control owner and
   atomic participant sets. Candidate first-page event-only replay can exclude
   checkpoint/snapshot tails; multi-page completion still needs ledger/blob/final
   result ownership, generations, pinned retries and fresh ambiguous-save readback.
   Choose one actor save where it fits, or a qualified Dapr ETag transaction and
   recoverable cross-owner sequence. Broker/effect store work cannot be declared
   atomic with an actor merely because physical storage is shared.

Dormant framing/ownership and source-derived declarations are implemented and
testable without these choices. Generic semantic wire codecs can still be
prepared after the schema decision; dispatch/effects must wait for the exact
model/catalog/owner binding. Production ratification, environment publication and
live qualification are separate later gates. All M1–M8 and O01–O20 remain open.
