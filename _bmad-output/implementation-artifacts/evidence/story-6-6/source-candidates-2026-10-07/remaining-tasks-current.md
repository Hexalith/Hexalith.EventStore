# Remaining requirements after local preparations

All M1–M8 tasks and O01–O20 obligations remain open. The completed local slices
are prerequisites, not integrated authenticated consumers. Missing production
authority is a later activation gate; the implementation inputs below are actual
unselected schemas, runtime bindings or owner/participant choices.

| Task | Completed local preparation in this continuation | Next concrete implementation input / work |
| --- | --- | --- |
| M1 | Optional whole-managed-process observation; reference-identity context membership/bindings; real isolated startup, late, reflection, dynamic and in-flight controls. | Select the serving managed/shared-framework/native immutable execution-image and loader plan, with actual runtime image/context/ABI identities and complete D/V/A/E/F/S/handler/filter/receipt/codec closure. Current file/deps hashes do not identify executed native images or catalog completeness. Native observations, approved dynamic paths and 64 MiB/65,536-row scale qualification remain unimplemented/unproven. Compose one admitted resolver after those concrete bindings exist. |
| M2 | Greeting's exact two-byte V1/json writer; Counter's prior six declarations preserved; private outer proof framing and shared scratch controls. | Select the distinct Dapr logical route/prefix evidence model, signing purpose/trust/carrier, consumed-metadata/list/accumulator preimages and legacy-without-digest policy in the [design candidate](dapr-logical-claims.design-candidate.md). Existing `StoredDigest` means provider/raw evidence and cannot receive an application digest. Choose actual actor/range/key-mapping/fixed-head binding and canonical event/schema/validator/identity/deserializer rows before wiring the shared evolved reader. Outer framing does not verify claims or sign anything. |
| M3 | Existing aggregate actor ownership and Dapr-only boundary retained; no SQL/provider extension introduced. | Define actual ledger/blob/final-result, checkpoint/state-root, publication/membership/acceptance and hold participants for each producer. Then choose one dedicated actor save where the complete set fits, or a qualified ETag transaction plus recoverable cross-owner sequence. Exact state owner app/type/ID/key configuration and generation/readback contracts are absent. This is a protocol design input, not solely missing deployment approval. Implement concurrency, crash/lost-ack, takeover and cancellation matrices after that choice. |
| M4 | Optional detached snapshot declarations on both processor bases; admitted graph charge, exact distinct root and original-token boundaries; Counter/Greeting fixed scalar copies. Actual Apply/Handle failure/cancellation and pre-tail caller-accessor mutation controls. | Generic legacy typed references deliberately retain compatibility. No generic graph independence, canonical state serializer, state schema/apply identity or command proof authority is claimed. Choose those declarations and M2's logical command-state/continuation model, then M3's operation owner before durable page transitions/pinned retries/final readback. Fixed-head source/target reconstruction and authenticated query sessions remain to implement. |
| M5 | Existing legacy projection path stays compatible. No verified projection route is enabled. | Choose the logical verified current-event carrier/proof model (existing `VerifiedEffectiveEventView.StoredDigest` is insufficient), actual handler route/capability, state serializer/readback/backend binding and M3 participant owner. Then prepare/integrate prescribed verified projection contracts, complete-prefix validation, named generations, fenced prior sessions and migration guard. Retained legacy key inventory and copy/readback/cutover are separately required. |
| M6 | Existing unavailable versioned publication/subscriber routes remain fenced. | Choose the logical delivery/effect identity model after replacing the withdrawn stored-digest meaning; actual broker, membership/acceptance/no-future-acceptance control owner and route receipt mode (`AtomicStore` or the application's explicit idempotency contract). Then implement exact Binary/Structured codecs and budgets, stable eight-field effect key, receipts/readback and shared subscriber verification. The old physical/provider proof semantics cannot be relabeled logical evidence. |
| M7 | Existing actor recovery/operator behavior retained; no new action or UI authority issued. | Choose logical hold/capture owner, original committed-batch identity, broker disable/reject and retained-capsule/cursor contracts, plus purpose-2d operator/tenant policy. Then implement scoped inventory/resume/redrive and diagnostics against M3/M6's concrete protocol. Committed commands cannot run again. Historical physical custody/generation-dependent operations remain unavailable. |
| M8 | Full affected Client/DomainService/Sample regressions, Release build, local 14-package/3-consumer lanes, 21 isolated guard mutations across four scripts, and an additive workflow for those guards. Governed OQ8 CI bytes preserved. | New integrated paths need their meaningful crash/cancellation/compatibility and live Dapr matrices after the above choices. API/wire/compiled-consumer coverage for every future claim and full acceptance remain incomplete. The additive workflow was locally linted; GitHub execution is not observed. OQ8 resealing remains separately governed. |

The three machine-readable source candidates and the logical design proposal
make these choices concrete for review; their authority flags are false. Dormant
semantic codecs/control implementations can continue once the missing **model
and owner choices** are selected. This inventory does not describe all future
local code as impossible, and does not treat unapproved candidates as a blanket
implementation blocker.

Separate activation/qualification gates: the canonical
`deploy/dapr/production-profile.yaml` is absent (`test -f` exit 1, no output),
AD-26 is unratified, and actual serving-peer/production environment authority and
exact live component/fleet/broker capability evidence are missing. Those facts
prevent readiness/activation, deployment/publication and acceptance claims; they
do not invalidate passing local verification or grant permission to fabricate
the missing semantics. Original frozen intent, baseline and historical approval
digest remain unchanged.
