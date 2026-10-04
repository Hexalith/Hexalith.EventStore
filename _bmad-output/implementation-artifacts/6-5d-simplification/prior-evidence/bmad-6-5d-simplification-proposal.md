# Story 6.5d simplification proposal

This is an alternative to the pass-14 iteration-12 repair proposal. It records a smaller architecture and a more focused delivery process; it does not change the current candidate or approve the production gates.

## Why eleven iterations happened

The task bundled replay activation, status precedence, legacy admission, quota accounting, fair capacity waits, membership holds, publication resume, held-delivery capture/redrive and operational inventory into one normative contract. The first review alone identified 32 root causes; subsequent loops found both genuine recovery defects and verification weaknesses.

Repairs accumulated separate origins, reconstruction images, progress heads, repair prerequisites, inventory interests and deletion receipts. Those records introduced new consistency boundaries that required further repairs. Preservation rules retained old internal structures and mutation probes as well as useful safety behavior, making each redesign harder.

The current candidate is 10,011 lines and 957,247 bytes. Its D12 verification section alone is 8,810 lines and 687,094 bytes (71.8% of the candidate). Passing that local executable model proves the exercised model properties; it does not establish actual Dapr/PostgreSQL/broker behavior.

The latest formal input was a 7.5 MB baseline diff with 160 file sections, only two owned by this story. The review instructions require the blind reviewer to report at least ten findings and prevent an empty report. Parent triage can reject findings, but that quota encourages further refinements. Re-derivation after a spec-level finding also makes convergence more expensive. These are process problems, not evidence that eleven iterations were inherently necessary.

## Proposed architecture

| Area | Smaller design | Required boundary |
| --- | --- | --- |
| Publication resume | One bounded execution-owned state record holds revision, request identity, current phase, intended successor, unresolved-member references, active reservation reference and bounded retry outcomes. Existing immutable committed events and publication evidence remain referenced at their established addresses. | Persist intent before an external fence/send. After a crash, read the intended external result and advance that same owner record. A local state transaction cannot make the broker atomic. |
| Held delivery | One bounded held-delivery record holds carrier locator/hash, current signed request/attempt, count, retry deadline and repair state. Repair is a state of that delivery, rather than a second independently managed hold workflow. | Capture acknowledgement requires retained bytes and discoverable durable state. Sending requires exact retained bytes, valid authorization and current count. Unknown evidence remains held. |
| Inventory | One discoverable work registry per existing owner partition, with the current reason derived from the authoritative record. Admin projections and counts are rebuildable. Use the existing actor discovery contract where it meets the required guarantee. | Owner discovery must exist before work can be acknowledged or stranded; an eventually updated dashboard must never be the only restart authority. Verify entry/registry transaction support or define one bounded recovery transition. |
| Metrics and paging | Start with one active Operations reconciliation/aggregation owner. Use a generation-bound cursor that asks the caller to restart paging if its generation changes. | Metrics describe operations; they do not authorize lifecycle transitions. Keep tenant authorization and stable ordering. Add distributed aggregation only when measured scale requires it. |
| Legacy recovery | Keep the existing bounded capsule and one execution-owned recovery phase. Missing historical evidence remains an operator incident. | Reuse the original committed range and MessageIds, preserve rejection classification and never re-execute a command to recover publication. |
| Quota and waits | Retain a dedicated ledger/queue for shared capacity, but use an explicit fixed participant set and supported provider transaction contract instead of adding a general proof/repair framework to every record. | Never infer an atomic transaction from actor serialization alone. The approved backend must demonstrate the reservation/count/refund boundary, including crashes and lost acknowledgements. |

The consolidation concerns mutable coordination state. It does not delete externally required signed claims, immutable publication evidence, audit history, retained carriers or bounded idempotency outcomes. Some state remains separate because it has a different owner, retention horizon or irreversible external effect.

Use the repository's declared Dapr/PostgreSQL production profile as the design target. Other stores must demonstrate the same required transaction and readback contract before support is enabled. This avoids designing a hypothetical universal backend. Architecture AD-26 still requires separate provider evidence; the current profile is not thereby approved.

## What to preserve

Preserve same committed events and MessageIds; no command re-execution; accepted-member exclusion; authorization and tenant isolation; immutable source verification; bounded storage and retry outcomes; inventory-visible incidents; no silent acknowledgement/loss; deterministic crash recovery; exact charge/refund and erasure rules; and the four approved behavioral scenarios.

Preserve verification of these outcomes. For internal record layouts removed by consolidation, map each old test obligation to the new owner's behavior rather than requiring every obsolete record and source-text mutation to survive. Keep canonical encoding answers only where bytes cross an actual signed, durable interoperability or public boundary.

## Delivery and review changes

1. Settle the ownership/transaction diagram before expanding codecs or lifecycle records. List each durable record, owner, bound, external effect and restart source once.
2. Divide the existing obligations into bounded slices: publication resume/legacy recovery, held delivery/capture, quota/capacity waits, then inventory/telemetry integration. This divides delivery and validation, without silently dropping required behavior.
3. Keep the normative contract short and separate from executable verification. Moving verification to supporting files requires an explicit adjustment to the current two-file/testing boundary; do not do so implicitly.
4. Review the owned delta plus relevant dependency context, recording external findings separately. Replace the forced finding count with evidence-based review that permits zero defects. This is a proposed customization, not the current rendered workflow.
5. Gate a slice on required behavior and demonstrated failures: safety, authorization, crash recovery, boundedness and actual provider transactions. Ordinary API/telemetry refinements should not automatically trigger full re-derivation.
6. Implement and run provider-level acceptance for each slice before expanding the next one. Keep AD-13 and other production approvals distinct from local model success.

## Latest findings under the simpler design

Preparation/fence progress and absent-attempt repair evidence remain real safety questions. Consolidated owner phases should answer them directly. Entry/index coherence needs one proven transaction or a discoverable recovery phase, not an expanding hierarchy of repair records. Metric ownership can use one aggregator. Paging can reject a stale generation. Count monotonicity belongs in the owner transition. The lack of a new generic production codec is only a blocker if the selected provider's concrete durable source contract is actually undefined; a fixture-only disclaimer is not proof of a defect by itself.

The recommended next action is a bounded simplification of the contract and ownership model before further implementation. Do not execute the old seven-root additive repair proposal unchanged.
