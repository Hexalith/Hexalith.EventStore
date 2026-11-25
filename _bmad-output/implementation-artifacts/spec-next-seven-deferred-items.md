---
title: 'Next seven EventStore deferred items'
type: 'bugfix'
created: '2026-11-25'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete the next seven actionable numbered ledger items, taking severity first and ascending item ID within each severity. Use matching recorded decisions for DW-35, DW-59, and DW-61. Report why earlier items were skipped.

Apply DW-546 to ambiguous payload identity properties and DW-547 to projection activation test synchronization. Apply DW-35 to unchanged counter history responses, DW-59 to durable handler retry progress, DW-61 to handler telemetry discovery, DW-82 to blank Dapr routing tokens, and DW-89 to atomic in-memory read model writes.

Verify each change and record its ledger resolution in its own Conventional Commit. Run a full build and test pass and push to main after successful verification.

</frozen-after-approval>

## Implementation Notes

- DW-17 moved into the batch after verifying that the private Keycloak realm can issue both control and delegated test claims. The test validates the actual issuer's signatures with the production bearer configuration, then checks the claims used by query identity extraction.
- DW-59 remains open. The existing Dapr marker acquire is a read without an ownership token. The recorded per-handler approach needs stable handler identities and agreed store semantics for acquiring, fencing and recovering durable ownership.
- Scope decisions for DW-35, DW-59, and DW-61 are recorded in the [BMAD Loop decision journal](../../.bmad-loop/decisions.json). Their intent text matches the current ledger entries.
- A full-suite repair uses a longer marker and checks the original text and its encoded prefixes in the payload redaction test. Four hex digits can occur in unrelated trace identifiers.
- The Phase 4 completion writer refused this batch record with `STORY_IDENTITY_MISSING` because the changes belong to numbered deferred entries. The item closures are recorded in the deferred-work ledger.

## Skipped Items

| Items | Reason |
| --- | --- |
| DW-456 | The maintained checker now parses numbered records. The remaining historical migration requires reconciliation of two ledgers whose IDs conflict with recorded owner decisions. |
| DW-457, DW-496, DW-521, DW-524, DW-533 | The OQ8 evidence packets and their historical verification machinery were retired by `6a0b6441f`. |
| DW-514 | Both downstream pins already descend from the corrective revert. |
| DW-515 | Changing required checks needs a GitHub ruleset update. The old OQ8 gate rationale has been retired. |
| DW-219 | Epic 2 has unfinished stories, so its current status is accurate. |
| DW-367, DW-368, DW-370, DW-408, DW-410, DW-412, DW-428, DW-430, DW-431, DW-433, DW-435, DW-437, DW-451, DW-453, DW-498–504, DW-520, DW-528–532, DW-534, DW-536–538 | These medium entries describe historical proof material, verifiers or approval records retired by `6a0b6441f`. |
| DW-407 | Preserve the recorded decision to rely on the reusable-workflow ref check at dispatch. |
| DW-423 | The expected OCI labels already include the supplied creation instant. |
| DW-510 | The historical combination of failing required checks no longer applies. The current build-and-test failure has a different cause, addressed by the redaction assertion repair. |
| DW-516 | Story 8.5 added the typed key-resolution seam and covers denial and invalidation. |
| DW-539–542 | These changes belong to the separately installed BMAD Loop runtime. |
| DW-544 | Story 8.7 still gates compatibility-router wiring. Identity history remains on the retained reader. |
| DW-6, DW-369, DW-376–380, DW-383–386, DW-390, DW-392, DW-393, DW-411, DW-413–418, DW-425–427, DW-429, DW-432, DW-434, DW-436, DW-444, DW-446, DW-452, DW-454, DW-523 | These low entries refer to retired evidence, historical commits or removed checkers. |
| DW-391 | The portable-path and numbered governance checks landed. The remaining historical grouping and deduplication belong to the DW-456 reconciliation. |
| DW-397 | Duplicate of the release guard already dispositioned in DW-407. |
| DW-440 | The test now builds an isolated fixture project with fresh assets and both runtime IDs. |
| DW-512 | The historic release gate was scoped to the Builds pin. Governance for the other direct references needs an approved pin policy. |
| DW-517 | The documented Contracts support posture needs an approved native AOT and trimming design before it changes. |
| DW-9, DW-11, DW-51, DW-56 | Dependabot and CI workflow policy are owned by Hexalith.CiCd or the shared workflow repository. |
| DW-15 | Immutable tag creation requires registry-side policy and verification. |
| DW-18 | The owner retained this timing work as a platform-level change. |
| DW-28 | Persisted read-model age semantics remain a separate projection freshness design. |
| DW-34 | Historical D5 branch scope hygiene requires no current product edit. |
| DW-37 | The fail-closed referenced-contract discovery rule requires an explicit API scope. |
| DW-57 | Projection and position actor names are part of the route-catalog contract. |
| DW-59 | Durable handler acquisition depends on a qualified store and ownership contract. |
| DW-62 | The health endpoints already declare anonymous access. |
| DW-64 | The approved migration requires changes to the Tenants host. |
| DW-65, DW-77–79 | The recorded guardrail disposition consolidates these source-scanner gaps into a semantic analysis story. |
| DW-80 | Source and assembly availability are prerequisites for these structural checks. Skipping on missing inputs would remove the intended coverage. |

## Review Triage Log

- DW-59, confirmed and still deferred. The reviewed code showed the existing replay behavior and the batch uses DW-17 in that place.
- DW-89, patched. Arbitrary serializers run before the atomic compare and write, so they cannot re-enter and overwrite a checked ETag. Both batch profiles now commit during the deterministic serialization pause and the stale write is rejected.
- DW-61, patched. Registered handler implementation types and instances drive discovery. The guide covers explicit telemetry for handlers behind opaque factory registrations.
- DW-35, patched. Repeated missing projections preserve the original empty-state row. A new valid 200 refresh can still add history after a 304.
- DW-17, scoped to bearer claim transport. The test uses signed Keycloak access tokens, OIDC discovery and the production JwtBearer configuration, then calls the consumer of those transport claims. Full gateway route selection is covered in the existing gateway authentication cohort.
- Redaction assertion, patched. The longer sentinel guards partial disclosure of the encoded prefix without matching random traces.
- Batch-record completeness, patched. The recorded decisions, skip reasons and private Keycloak claim shape are included here.
