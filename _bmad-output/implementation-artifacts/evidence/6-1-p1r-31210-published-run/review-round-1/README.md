# EventStore 3.121.0 qualification after review fixes

Fresh execution and independent validation are complete. Qualification remains blocked: 19 lanes and 163 cases executed 4,774 assertions, with 4,758 passed and 16 failed. Four role decisions, same-baseline conformance and Solution Architect disposition remain pending. P1R is unusable and Story 6.1 remains blocked.

The exact approved inputs are unchanged: EventStore 3.121.0 at source/tag `21b34c01ec919769cfba9cfe0d47e50886b57ded`, rollback null, Dapr 1.18.2, PostgreSQL `postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`, historical comparisons 3.70.1/3.110.0, the adopted `p1r-executed-checks-v1` mechanism, Reminder recovery and logical-event evolution. [Approval](../approval.json) binds the unchanged [owner inputs](../owner-inputs.json) and original Projects proposal. Package/tag Builds and publication-execution Builds retain their distinct approved identities. No acceptance decision was invented.

Three independent reviewers identified twelve private witness/admission gaps. All were repaired and [134 focused controls](root-regression-controls.txt) passed before execution. [Triage](triage.json) and the [pre-fix source diff](source-before-review-fixes.diff) are retained. Physical probe digests, exact SQL input digests, owned launch/readiness/configuration, ordered inventories, full stream/protection metadata and all 20 profile fields are now bound. The adjacent incomplete-checkout comparison access is guarded. No finding remains deferred.

[Fresh execution](../execution-review-fixed-347d58d411184960bcd6412a8c1950ba/execution.json) uses private consumers and serialized Debug/source restore. It records no executor errors. [Lineage](fresh-execution-lineage.json) binds the new source closure; only three qualification tooling/test files changed from the earlier run, with production source unchanged. [Plan](fresh-execution-plan.json), [driver](fresh-execution.py) and [console](fresh-execution-console.txt) retain the literal invocation. The separate source build succeeded, and all ten package/source semantic comparisons are equivalent, including measured incompatibilities.

The post-upgrade lane passed 61/61 assertions: actual published JSON manual creation after nine committed events persisted a version-one folded Counter snapshot with count nine, followed by three actual appends, backup, fresh persisted restore, append to thirteen and restart. Floor five, original event/snapshot hashes and the second tenant are preserved. Counter has no automatic snapshot processor; this is manual fixture capture. The earlier typed DataContract result serialization failure remains unqualified and retained. Fixture enrollment grants no production P2 acceptance.

Mixed API passed 641/641, Reminder recovery 131/131 and logical-event evolution 193/193. [Measurement summary](measurement-summary.json) retains all six nonpassing required cases:

| Lane | Case | Failed assertions |
| --- | --- | --- |
| metadata-write | 3.110.0 → 3.121.0 | Hydration, accepted append, head thirteen |
| metadata-write | 3.70.1 → 3.121.0 | Hydration, accepted append, head thirteen |
| retained-covered | 3.110.0 → 3.121.0 | Hydration |
| retained-covered | 3.70.1 → 3.121.0 | Hydration |
| checkout | retained-covered | Package and source hydration |
| checkout | metadata-write | Package and source hydration, append, head thirteen |

The observed refusal diagnostic remains `logical-event-read-rejected`; no unmeasured cause or successful capability refusal is assigned to these required supported obligations. The [complete successor boundary](successor-support-boundary.json) retains 123 supported obligations, 39 historical fixture refusals and one containment-only case. Status is `measured-pending-disposition`, with disposition null.

Owned cleanup removed every owned container/process/scratch resource with no remaining resources or errors. Three unrelated Dapr mTLS containers disappeared during execution, while four shared identities remained unchanged. [Shared-resource delta](cleanup-shared-delta.json) records this without excluding or modifying unowned resources. The strict cleanup receipt remains failed at 6/7; its dependent candidate restore receipt remains failed at 11/12, despite the passing 61/61 operational restore lane.

[Independent audit D](../independent-validation-20261014-d/independent-validation.json) reports valid evidence with no validation errors, exactly 21 receipts including required restore/cleanup, and 89/89 signature/graph/physical-load checks for each package role. It recomputes case witnesses and the complete boundary. Its [audit script](independent-audit-next.py) refuses missing recovery receipts for every contributing invocation, including supplements. Valid evidence does not grant qualification. Strict [prepare/validate](strict-attempt.json) both retain exit two and the missing measured owner disposition refusal.

All earlier smokes, the original complete run and source timeout, serialized checkout supplement, original and selected-set boundary tables, failed independent audits and historical compatibility failures remain preserved. Audit C remains byte-for-byte intact as an earlier audit with an incomplete supplement-cleanup scope; D supersedes it for the new complete run. The [prior-attempt preservation check](prior-attempts-preservation-verification.json) and [workspace/history preservation check](workspace-history-preservation-verification.json) record exact preservation. The original run README receives only an append-only pointer to this result.

Earlier source closures differ from this review-fixed closure. Their retained validation flags describe the earlier frozen verifier scope and do not grant current qualification.
