---
title: 'Next six EventStore deferred items'
type: 'bugfix'
created: '2026-11-25'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent">

## Intent

Complete six actionable deferred items in the established ledger order and commit each with its ledger closure.

- DW-199 assigns the live fixture and its replica a shared, run-private Dapr placement namespace.
- DW-201 exercises the request timeout through the real Dapr proxy against a stalled loopback listener.
- DW-202 preserves Dapr factory defaults when overriding only the ETag timeout and handles option construction inside the fail-open boundary.
- DW-207 validates module names and contains project paths in their mapped module across supported workspace layouts.
- DW-208 parses the Sample API access-control policy with the existing YAML parser and verifies the exact deny/allow rules.
- DW-209 makes the remaining top-level shell scripts directly executable and verifies their tracked modes.

Run focused tests per item, build the full solution, run all test projects, and push the six verified commits to main.

</frozen-after-approval>

## Implementation Notes

- DW-199 uses the randomized fixture application ID for the placement namespace. Both hosts share that namespace and can complete the same admission after failover.
- DW-201 reproduced the SDK's `TaskCanceledException` with a nested `TimeoutException`. The service returns a missing ETag when that timeout expires and continues propagating caller cancellation.
- DW-202 resolves the Dapr factory and copies its options inside the read call. A loopback test verifies the configured endpoint, API token and three-second timeout through the server's DI registration.
- DW-209 includes the release-source entrypoint. The ledger's original third script, `ci-local.sh`, already had the executable bit.

## Skipped Items

The preceding batch dispositions still apply. The items reconsidered around this batch have the following current dispositions.

| Items | Reason |
| --- | --- |
| DW-150–152, DW-154–156 | The OCI publication tools and fixtures are owned by Hexalith.Builds. |
| DW-159–160, DW-162, DW-165–168, DW-175–178, DW-183, DW-189 | The affected gateway, UI and source-mode work belongs to Hexalith.Tenants. |
| DW-180 | Package-mode Release builds run on this workstation without an interactive credential prompt. |
| DW-184–188 | The Story 2.12 evidence directory was retired. Successor CI enforcement belongs to the Tenants and Builds owners. |
| DW-191–192, DW-194, DW-197 | FrontComposer owns the release policy and the historical work described by these entries. |
| DW-193 | The Contracts CI lane already invokes the shell regression for the smoke-preflight exit code. |
| DW-204 | The source gitlink descends from the released Tenants package. An enforcement policy for independently selected source and package versions needs an owner decision. DW-202 verifies the separate ETag DI default. |

## Review Triage Log

- DW-199, patched. The namespace assertion runs inside the replica cleanup boundary. The test verifies both launch settings and persisted state across real replica failover. A second, foreign topology is outside this fixture's coverage.
- DW-201, patched. The real 500 ms override must finish before the three-second default could expire. The option-capture tests verify exact values. The typed Dapr API exposes no cancellation token, so the service honors caller cancellation at the next result or exception and reports the caller's token.
- DW-202, patched. A Dapr factory construction failure now occurs inside the fail-open read. The pinned SDK constructs options from the process environment and exposes defaults only on its concrete factory. A malformed SDK environment fails open, and the default preservation applies to the SDK factory registered by EventStore.
- DW-207, scoped. The finding describes filesystem-link traversal. The ledger item calls for lexical path validation and containment consistent with the sibling method. The mapped checkout remains the source trust boundary.
- DW-208, patched. The shared assertion verifies the policy's trust domain and namespace together with its operation allowlist.
- DW-209, patched. The mode assertion reads the committed Git tree, which is independent of the caller's index.

The Phase 4 spec completion writer reported `STORY_IDENTITY_MISSING` for the numbered batch. The ledger carries the item completions.
