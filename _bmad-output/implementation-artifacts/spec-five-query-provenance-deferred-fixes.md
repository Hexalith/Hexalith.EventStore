---
title: 'Close five deferred query provenance gaps'
type: 'bugfix'
created: '2026-11-23'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close DW-101, DW-102, DW-104, DW-105 and DW-106 from the Story 2.8 review. For a successful query, the gateway should promote a producer ETag only when the result names a projection and has projection-backed provenance. Its body should use the same selected ETag as the HTTP response. The client should accept ETags from the HTTP header only when body and header establish projection-backed provenance. Ignore weak ETags on other successful routes. Preserve strong ETag and provenance requirements on 304, and preserve existing projection lookup and valid producer-validator precedence.

Apply one Contracts policy to remove projection-only evidence from other routes while preserving operational metadata. Verify the gateway rejects meaningful freshness requests for non-projection results, and exercise the production router composition for both committed route targets. Reuse the existing direct regression for undefined provenance JSON output.

</frozen-after-approval>

## Implementation Notes

- Source baseline `6f5b4e05117beaab7bcea6d8712d3af155abc43e`.
- Update `QueryResponseProvenancePolicy`, `QueriesController`, and `EventStoreGatewayClient`. Extend the existing controller, client, and composed router tests and document the HTTP ETag authority rule in `docs/reference/query-api.md`.
- Committed route declarations select the production router target. The Dapr registry is registered in the production graph and its operational lookup is advisory. Keep the tests on that production composition and stub only infrastructure.
- The gateway now publishes the selected validator to both response surfaces. The client drops body-only validators and skips ETag interpretation when successful response provenance is untrusted. Both use the Contracts metadata policy.
- Baseline `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive` built successfully with zero warnings or errors, then exited 2 because its child could not create `~/.aspire` on the read-only filesystem. `aspire describe` reported no running AppHost.
- Debug source-reference builds used `dotnet build tests/<project>/<project>.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1 --nologo`. Built-assembly runs passed 110 gateway client tests, 166 server query tests, 164 Contracts query tests, 15 QueryRouting tests, and 320 REST generator tests. The earlier full Client run passed 2,457 tests and skipped two that require isolated Dapr endpoints. The final gateway client and server runs used the reviewed code.
- `git diff --check` and the pinned `node_modules/.bin/commitlint --edit /tmp/eventstore-query-provenance-commit-message.txt --verbose` both passed.
- The completion writer `python3 -B tools/validate-phase-4-story-completion.py --spec _bmad-output/implementation-artifacts/spec-five-query-provenance-deferred-fixes.md --target spec --write` refused this freeform batch with `STORY_IDENTITY_MISSING`. The spec retains its open status. The five ledger entries track the implemented and verified fixes independently of the current sprint tracker.

## Review Triage Log

| Finding | Verdict and disposition |
| --- | --- |
| The reference guide omitted the bodyless 304 rule. | Low. Documented the HTTP 200 and 304 requirements separately. |
| A weak ETag on a 304 surfaced as an exception with status 304. | Medium. Invalid 304 validators now consistently produce 502, with coverage for missing, wildcard, and weak input. |
| The gateway assumes projection producers supply normalized ETag syntax. | Low. The existing producer syntax gap is recorded in the deferred ledger. |
| The owner workload endpoint can carry a body ETag under `no-store`. | Low. Scoped the HTTP validator documentation to the two query submission routes. |
| The routing test did not verify the advisory Dapr lookup. | Low. Added an assertion on the exact operational-index key. |
| Freshness tests prove the response outcome without isolating the rejection branch. | Low. The observable 400 and reason code are the contract. Testing the private implementation path would couple the assertion to control flow. |
