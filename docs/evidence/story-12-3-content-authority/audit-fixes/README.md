# Current authoritative-content audit corrections

The original [qualification](../qualification.json) is the historical pre-audit source snapshot.
This [qualification.json](qualification.json) records the current 34-path source/guide/helper hashes,
exact sequential commands and retained log digests. Protocol16/16, real Redis/Dapr1/1 and affected
helper consumers49/49 passed with zero failures/skips.

A unique revocable CAS claim fences each caller. Only its owner may commit irreversible
`DeletionAuthorized` after fresh source grant proof; stale/ineligible visitors and stale failed grants
cannot clear that phase. Replacement stores complete physical erasure from the durable decision.
Before commitment, interrupted grant recovery re-reads current lifecycle state and re-proves the grant;
a changed source generation requires a fresh P7D epoch. The source grant permanently disposes this
receipt against later references, while unrelated later tasks may proceed.

The deterministic tests pause source authorization and physical reads to cover changed-generation and
failed-grant revocation, stale failed claims after another caller commits deletion, post-authorization
crash recovery, and fresh source proof/epoch after a pre-commit interruption. The real lane replaced
sidecar/client/store identities (4133490→4133607), inspected 8192 persisted bytes, rejected corruption,
and verified hold/P7D physical deletion.

The RecordingDaprClient change is broader than the original ConfigureAwait-only fix: typed state reads
now use the same byte-read hooks. Existing DaprReadModelStoreTests, SharedProjectionExpiryAndScratchTests
and SharedProjectionReviewThreeMaintenanceTests passed49/49 on that helper. No package/pin change or
production rollout is claimed. The updated [guide](../../../guides/authoritative-content.md) describes the
source disposition and SDK deletion phases separately.
