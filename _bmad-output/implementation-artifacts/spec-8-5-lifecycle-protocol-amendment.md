---
title: 'Story 8.5 Lifecycle Protocol Amendment'
type: security-architecture-amendment-draft
created: '2026-10-23'
status: approved-authorized
implementation_authorized: true
approval_date: '2026-10-23'
approval_source: 'Interactive user: approve and continue'
scope_work_clarification_approval: 'Interactive user: Approve scope-wide clarification (Recommended)'
previous_normative_sha256: a3a6a3bd83e728bea19b9ff50303ff8407b2362225e3e1b0935be8f551ba8c3a
previous_effective_authority_sha256: f82cdf06f748de70326be063e0e849627e42542d69fd4e22ef9eed372f6de22b
base_authority: spec-shared-payload-protection-engine.md
base_normative_sha256: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e
base_full_file_sha256: 542f0b6e4ebe24c02a403ed7af511a03d1a4b6ef5c83b789254fbb055a563c82
candidate_normative_sha256: aed1eb3f6cd438ff0691db5d449dab4d9e5186e3a0dc4d2d7c0eecc56b9fbe7a
effective_authority_sha256: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6
source_sha: 6a0b6441f6d78a3007678a1d0ec336e9cb8ff961
proposed_sections: ['10.2', '10.3', '10.4', '10.5', '10.8', '13.1']
---

# Story 8.5 Lifecycle Protocol Amendment

## Decision and approval subject

The user selected the narrow-amendment route on 2026-10-23 with “apply recommended”, then approved this exact document and implementation with “approve and continue”. This records the current session authorization, without attributing any earlier approval or independent role decision to these bytes.

This detached document leaves the approved base file and fixtures unchanged. Its normative range is the UTF-8/LF/no-BOM content between the unique marker lines below, excluding the markers. On approval, the range replaces only the conflicting details of the listed base sections. Other base requirements remain applicable.

Let `A` be SHA-256 of that range and `B` the base normative SHA-256. The effective authority digest is SHA-256 of ASCII `HXPA`, byte `01`, decoded 32-byte `B`, then decoded 32-byte `A`. All fence/deployment/capability spec-digest fields for this implementation must bind that effective digest. Existing approvals retain their original subjects; the candidate needs its own named-role decisions under the existing assurance rules. Approval does not authorize Azure provisioning, release, Server integration or G5.

<!-- HX-PP-LIFECYCLE-AMENDMENT-BEGIN -->

## 1. Scope and fixed bounds

Implement missing discovery, draining, recovery and record-encoding details using the existing Dapr key-state component, CAS transactions and one bounded hosted worker. Preserve HXP2, AAD, path manifests, key/nonce generation, frozen vectors, public Contracts and package boundaries. The unrelated draft Dapr cryptography amendment remains unapproved; backend operations stay opaque and provider-neutral.

Trusted host enrollment supplies an explicit immutable set of at most 4,096 unique tenant/domain roots through an internal `PayloadProtectionScopeRegistry`. No new configuration option or public registration signature is introduced here. Every operation must belong to an enrolled root; the worker visits only those roots. Missing/inaccessible enrollment fails readiness. A root cannot be removed from a successor deployment while it has unfinished work or members not proven cleared. Host integration is owned by Story 8.7.

Each catalog below has at most 256 pages of 256 entries, with at most 65,536 registrations or pending operations per root. Admission fails on capacity exhaustion; no unbounded overflow map, key scan or implicit pruning is permitted. Each new JSON value is at most 65,536 UTF-8 bytes. Apply the base closed-schema, canonical ULID/digest/integer/timestamp rules. Page numbers are zero-based, rendered as ten decimal digits in keys. Decimal key epochs are canonical unpadded unsigned integers. All counter increments are checked.

## 2. Two discovery catalogs and two drain records

Here `T` is the existing tenant/domain digest, `S` the existing scope digest, `O` an operation ULID, `E` a lifecycle epoch and `I` a fresh per-process-incarnation ULID. An instance ID is never reused after restart. `I` also occupies the existing capability/read/completion lease instance-ID field, within its original ASCII bound.

| Value | Exact key |
|---|---|
| `MemberHeadV1` | `eventstore:pp:v2:member-head:{T}` |
| `MemberPageV1` | `eventstore:pp:v2:members:{T}:{page:D10}` |
| `WorkHeadV1` | `eventstore:pp:v2:work-head:{T}` |
| `WorkPageV1` | `eventstore:pp:v2:work:{T}:{page:D10}` |
| `InvalidationDrainV1` | `eventstore:pp:v2:drain:{S}:{O}` |
| `CacheClearAckV1` | `eventstore:pp:v2:clear-ack:{S}:{E}:{I}` |

These are persistent records without TTL. The existing individual leases retain their specified TTLs. Exact ordered members follow `schemaVersion:1`:

| Value | Members |
|---|---|
| `MemberHeadV1` | `tenantDomainDigest:sha256`, `revision:u64`, `pageCount:u32`, `entryCount:u32`, `updatedAtUtc:timestamp` |
| `MemberPageV1` | `tenantDomainDigest:sha256`, `page:u32`, `entries:array` |
| Member entry | `ordinal:u32`, `generation:u64`, `instanceId:ulid`, `state:Serving\|Draining\|Cleared` |
| `WorkHeadV1` | `tenantDomainDigest:sha256`, `revision:u64`, `pageCount:u32`, `entryCount:u32`, `updatedAtUtc:timestamp` |
| `WorkPageV1` | `tenantDomainDigest:sha256`, `page:u32`, `entries:array` |
| Work entry | `operationId:ulid`, `scopeDigest:sha256` |
| `InvalidationDrainV1` | `tenantDomainDigest:sha256`, `scopeDigest:sha256`, `operationId:ulid`, `lifecycleEpoch:u64`, `memberCutoff:u32` |
| `CacheClearAckV1` | `scopeDigest:sha256`, `operationId:ulid`, `lifecycleEpoch:u64`, `instanceId:ulid`, `acknowledgedAtUtc:timestamp` |

Member ordinals are contiguous from 1; ordinal `n` belongs to page `(n-1)/256`, using integer division. Registrations are append-only and sorted by ordinal; identity/generation/ordinal never change. Only `Serving → Draining → Cleared` is allowed. Cleared entries remain permanent tombstones; catalog exhaustion is an explicit availability bound requiring separately designed maintenance, never silent eviction.

Work entries are unique across pages and sorted by operation ID within each page. Insert into the first page with capacity or append one page. Terminal removal retains empty pages and never renumbers them. Each catalog mutation changes its head revision and relevant page in the same ETag transaction. Full enumeration strong-reads the head, its declared pages, then the head again; changed revisions restart the enumeration within the enclosing operation budget. Exhaustion returns a bounded failure/pending result. Missing pages, duplicate entries, count/ordinal disagreement or wrong-root records block the dependent operation. Initialize empty heads with zero pages/entries using qualified conditional creation; never reinterpret a missing nonempty catalog as empty.

## 3. Registration, retirement and lifecycle barriers

Register a member and create its capability lease in one transaction comparing the current Open deployment generation, member head and target page. Capability renewals compare the unchanged Open generation and the member page showing `Serving`; they cannot revive a closed generation or a draining member. A transaction with an uncertain outcome is reconciled by its stable operation identity.

Read/completion lease admission and renewal also compare the unchanged allowed lifecycle epoch and the serving member page. Every returned key/plaintext result must still be within its operation deadline and admitted epoch; a suspended or cancelled operation discards and clears a late result. An elapsed lease cannot authorize resumed work.

For scope invalidation, one transaction moves lifecycle to `Invalidating` with a greater epoch, compares the current membership-head ETag, and creates the immutable drain record plus existing operation/reconciliation/work entries. `memberCutoff` is the confirmed member count in that transaction. New registrations have larger ordinals, start with empty caches and cannot acquire a lease for the invalidating scope.

After the barrier transaction is confirmed, wait a fresh full 60 seconds on a monotonic clock. This covers the base 45-second read and 60-second completion lease maxima. No renewal can cross the barrier. A worker restart or ambiguous commit starts a new full wait after strong confirmation; a pre-commit wall-clock timestamp cannot shorten it. Expiry removes lease authority but does not establish the outcome of an ambiguous persistence request. Unreconciled persistence can still block completion after the wait. Generation closure similarly waits 60 seconds after confirmed CAS, then checks exact indexed capability keys before permitting the next write-fence transition.

For every member at or below the drain cutoff, require either its exact scope/operation/epoch acknowledgement or a `Cleared` tombstone. An acknowledgement means that member has blocked new scope work, drained its local operations, and zeroed matching cached arrays and operation-owned key copies. Persist it conditionally against the unchanged drain/lifecycle/member records. An acknowledgement for another epoch or operation cannot satisfy this drain.

Graceful retirement first sets the member to `Draining`, preventing new/renewed leases. After a confirmed full 60-second drain, it clears all local operations and caches for that tenant/domain before transactionally recording `Cleared`. It can never serve again under that instance ID. A stopped, expired, suspended or unreachable member without clearing evidence remains uncertain and blocks terminal invalidation/deletion. Capability expiry proves loss of traffic authority; it does not prove memory zeroing. No automatic crash-retirement shortcut is introduced.

Member instances poll enrolled roots' pending work for invalidation requests; pub/sub may accelerate delivery but cannot replace durable requests/acknowledgements. Final indexed key mutation and irreversible workflow completion require confirmed drain/clearing evidence and current lifecycle/worker-claim ETags.

## 4. Restartable work and quarantine

For scope-wide Invalidate or Delete work, ReconciliationV1 uses keyRef="" and dekVersion=0, matching the corresponding scope-wide OperationV1. A scope-wide entry identifies the whole authenticated scope and its lifecycle operation, never a synthetic key. Create, Activate and Rewrap reconciliation require a canonical nonempty key-reference ULID and positive DEK version. Per-key Invalidate/Delete retain those same key requirements. Empty references with nonzero versions, nonempty references with zero versions, and empty references for other kinds are invalid. Scope-wide work must verify the complete scope index and applicable lifecycle/drain authority before recording any terminal result.

Generate the operation/key references before wrapping. Transactionally persist `OperationV1.phase=Started`, `ReconciliationV1.status=Pending` and the pending-work entry under current authority before invoking wrap. This refines base §10.3 ordering so a restart can discover a wrap whose outcome was lost. Creating this intent does not reserve a DEK or authorize payload output.

The later reservation transaction CAS-binds that intent and current lifecycle/fence/deployment/member authority, and creates the Reserved key plus both existing indexes before encryption returns. Every operation needing reconciliation has its work entry durably enlisted with the corresponding state mutation. Duplicate operation IDs must match the original root, scope, kind and key reference/version; mismatches fail closed.

Each worker pass handles at most 256 entries from one enrolled root, rotating roots/pages fairly. Keep its cursor in memory; after restart, enumerate from the durable catalog. Future-due entries remain indexed. Claim work using the existing reconciliation ETag and an increasing `attempt`; every subsequent durable mutation CAS-binds the current claim. Replacement claims fence a resumed old worker. Never blindly repeat a non-idempotent backend mutation; reconcile it by operation ID. Abandoned successful wrap/unwrap buffers are zeroed.

Remove a work entry only in the transaction recording a durable terminal disposition. Unknown persistence keeps a reservation pending. At the base 24-hour orphan boundary, a terminal/expired operation without a completion lease may receive `ReconciliationV1.status=Quarantined`; retain `WrappedDekV1.state=Reserved` and its indexes. Quarantine is bookkeeping, not evidence of zero references. It neither deletes the key nor blocks a legitimate persisted reference from being read under current lifecycle authority. Future reads of a reserved record may enlist reconciliation again. No automatic orphan deletion is authorized.

## 5. Exact key-record integrity encoding

`wrappedDekSha256` is lowercase SHA-256 hex of the decoded wrapped bytes. `canonicalFieldsSha256` is lowercase SHA-256 hex over header `48 58 4B 46 01 11 00 00` followed by the following 17 fields in ascending ID order. Use base §7 framing: field ID byte, type ID byte, big-endian u32 byte length, then value bytes. Type `01` is strict UTF-8, `02` is big-endian u32, `03` big-endian u64 and `04` exactly 32 decoded bytes.

| Field IDs | Exact fields | Type |
|---|---|---|
| 1, 2, 3 | `tenantId`, `domain`, `aggregateId` | `01` |
| 4 | `scopeDigest` | `04` |
| 5 | `keyRef` | `01` |
| 6 | `dekVersion` | `02` |
| 7, 8, 9, 10 | `state`, `backendId`, `providerKeyVersionId`, `wrapAlgorithmId` | `01` |
| 12, 13, 14 | `lifecycleEpoch`, `fenceApprovalEpoch`, `highestWrittenEpoch` | `03` |
| 15, 16, 17 | `creatingOperationId`, `createdAtUtc`, `updatedAtUtc` | `01` |
| 19 | `wrappedDekSha256` | `04` |

IDs follow the base record's member positions after `schemaVersion`. Exclude field 11 (`wrappedDek`, committed through field 19) and field 18 (the self hash). Header byte `01` binds schema version, `11` is the hexadecimal field count, and both reserved bytes are zero. Validate canonical encodings and the wrapped-byte hash before validating the field hash. Recompute hashes on record transitions. These are corruption checks, never authentication, authorization or deletion proof. No existing golden freezes either new field-hash preimage or digest; G-001/AAD/envelope bytes remain unchanged.

## 6. Closed reasons and audit encoding

Operation, reconciliation and audit reasons use exactly this union:

- Base §9 erasure tokens: `none`, `domain-pending`, `domain-invalidating`, `domain-invalidated`, `domain-deleted`, `state-unavailable`, `state-denied`, `state-unknown`, `epoch-invalid`, `epoch-regressed`.
- Existing readability tokens: `missing-key`, `key-invalidated`, `provider-unavailable`, `provider-denied`, `consistency-mismatch`, `malformed-metadata`, `unknown-metadata-version`, `provider-opaque-unsupported`, `bytes-metadata-mismatch`.
- Mechanics tokens: `cancelled`, `not-persisted`, `ambiguous-outcome`, `orphan-quarantined`, `fingerprint-collision-observed`.

Unknown tokens fail validation. Lease/ack uncertainty remains pending with `domain-invalidating` or `ambiguous-outcome`; it cannot become successful completion.

`AuditV1` emits the following ordered camelCase members, with absent optional members omitted rather than null:

| Members, in order | Type |
|---|---|
| `schemaVersion` | integer `1` |
| `auditId`, `operationId` | canonical ULIDs |
| `workflowId?`, `correlationId?` | canonical ULIDs |
| `tenantId`, `domain`, `aggregateId?` | validated identity strings, included only as allowed by existing audit scope policy |
| `operation` | `Create\|Activate\|Rewrap\|Invalidate\|Delete\|Reconcile` |
| `fromState?`, `toState?` | base wrapped-key/lifecycle state token |
| `lifecycleEpoch`, `fenceApprovalEpoch` | u64 |
| `backendId` | existing ASCII kebab ID |
| `formatVersion` | exact string `v2` |
| `keyFingerprint?`, `providerFingerprint?` | lowercase 16-hex display fingerprint, subject to existing key-reference policy |
| `attemptCount` | u32 |
| `durationMilliseconds` | u64 elapsed duration, rounded down |
| `result` | `Pending\|Succeeded\|Failed` |
| `reasonCode` | closed union above |
| `decisionActorId` | validated existing actor identity, 1..256 strict UTF-8 bytes |
| `decidedAtUtc` | canonical UTC timestamp with seven fractional digits |

For operations without a workflow, use its operation ID in the existing audit key's `workflowId` segment while omitting the JSON `workflowId` member. This names the storage group only; it invents no domain workflow. Audit scope permission must be validated before admission; missing permission fails the operation rather than leaking scope. `keyFingerprint` is the first 16 lowercase SHA-256 hex characters over the exact canonical key-reference ASCII; `providerFingerprint` is the corresponding prefix over exact provider-version UTF-8, as in the base. Neither replaces the existing key-alias fingerprint or becomes a lookup input. No arbitrary fields, provider details, payload, credentials or exception text are admitted. Existing metric names and tag allowlists remain unchanged.

## 7. Required implementation checks

Given concurrent registration/renewal and closure, when CAS races, then no old-generation or draining-member authority can be renewed. Given invalidation and new registration, when the barrier commits, then the durable ordinal cutoff reconstructs its exact member set after restart.

Given worker termination at each intent/reservation/claim boundary, when another worker resumes, then indexed work is found, stale claims cannot mutate, and uncertain reservations remain retained. Given expired or unreachable members, when invalidation resumes, then no completed deletion is claimed without clearing evidence.

Given a suspended operation or restarted drain worker, when it resumes, then late output is cleared and the full confirmed drain interval is enforced. Given hash-field mutation, reordered JSON, unknown reasons or malformed audit members, when decoded, then semantic corruption rejects deterministically while legal JSON member reordering preserves the field hash.

PF-02 must prove all required catalog/authority/lease/key/index operations are supported atomically on the exact selected Dapr component, including conditional creation and TTL. Incompatible partitions, weak reads or partial transactions block qualification. Tests inspect state through Dapr. Record commands/counts/source SHA/CI link in the story; no evidence packet or new sealing framework is introduced.

<!-- HX-PP-LIFECYCLE-AMENDMENT-END -->

## Verification and disposition

Python and Node independently reproduce both frontmatter digests. Base full-file identity, unique markers/canonical encoding, independent digest agreement, the 17-field table/header and recorded decision state pass five document checks. Both existing golden verifiers pass V001–V003; no runtime lifecycle behavior has been tested. `git diff --check` passes. Source baseline is the full canonical revision in frontmatter; no CI run exists for these local edits.

Recompute the normative and effective digests from the repository root:

```bash
python3 - <<'PY'
from pathlib import Path
from hashlib import sha256
p = Path('_bmad-output/implementation-artifacts/spec-8-5-lifecycle-protocol-amendment.md').read_bytes()
b = b'<!-- HX-PP-LIFECYCLE-AMENDMENT-BEGIN -->\n'
e = b'<!-- HX-PP-LIFECYCLE-AMENDMENT-END -->\n'
assert p.count(b) == p.count(e) == 1 and b'\r' not in p and not p.startswith(b'\xef\xbb\xbf')
a = sha256(p.split(b)[1].split(e)[0]).digest()
base = bytes.fromhex('de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e')
print(a.hex())
print(sha256(b'HXPA\x01' + base + a).hexdigest())
PY
```

Role approvals, source/profile qualification and later-story authorization are not inferred from the route-selection message.

## Approved correction: scope-wide reconciliation

Implementation exposed a closed-schema mismatch: the approved barrier creates scope-wide reconciliation, but the base reconciliation schema requires a key reference even for an empty scope. The interactive user approved the following exact paragraph on 2026-10-23 with “Approve scope-wide clarification (Recommended)”. It is now incorporated immediately after the section 4 heading. The previous digests above retain the original approval subject; no historical approval is transferred.

```text
For scope-wide Invalidate or Delete work, ReconciliationV1 uses keyRef="" and dekVersion=0, matching the corresponding scope-wide OperationV1. A scope-wide entry identifies the whole authenticated scope and its lifecycle operation, never a synthetic key. Create, Activate and Rewrap reconciliation require a canonical nonempty key-reference ULID and positive DEK version. Per-key Invalidate/Delete retain those same key requirements. Empty references with nonzero versions, nonempty references with zero versions, and empty references for other kinds are invalid. Scope-wide work must verify the complete scope index and applicable lifecycle/drain authority before recording any terminal result.
```
