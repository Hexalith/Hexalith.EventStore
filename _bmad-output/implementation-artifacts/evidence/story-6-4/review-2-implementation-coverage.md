# Story 6.4 review-2 corrective implementation

All seven pass-2 corrections are implemented. This is narrow local Debug/source evidence; Story 6.4 remains in progress and global/formal acceptance remains open.

| Group | Change and owning regression |
| --- | --- |
| G16 | `ProjectionDeliveryStateBody` keeps one bit set per known evidence object and skips unknown subtrees with the configured reader. `GuardedBody_DeepUnknownObjectsStayWithinProofWorkingBudgetThroughOwningStore` admits 50,000 nested unknown objects, reads both markers, preserves bytes and performs zero writes. The serial observation records 374,848 validator allocation bytes and 3,362,936 complete owning-read allocation bytes under 4S=3,789,824. Existing nested/case ambiguity and configured-depth vectors remain passing. |
| G17 | Integer header admission uses the actual serializer's integer semantics for schema and completed/reserved heads. Own serialized completed and reserved H=10000 rows over 16 KiB roundtrip with both markers. Overflow, fractional/exponent quoted integers remain rejected; a write-only quoted profile fails before persistence. |
| G18 | Every sibling completes frozen history/output verification before one shared 30-second finalization/cleanup deadline. The new two-route test advances its fake clock 31 seconds per history read and still converges; the existing shared-deadline exhaustion test remains passing. No history pass was added. |
| G19 | Retry occupancy metrics tolerate null Items while healthy shards scan. Maintenance still classifies the malformed shard as unresolved conflict, without mutation. |
| G20 | One canonical part stream can serve mapped-streamed and full-replay siblings. SDK planning combines every required route into one original coordinated batch and receipt. The mixed owning test loses either stage or commit response, resumes the original preparation once, verifies both detail/index pairs and one receipt fingerprint, and retains bounded parts. |
| G21 | StageAsync charges its coordinated batch attempt. Existing full-replacement vectors assert exactly J=1 and D=2; verification creates no additional batch execution. Retried staging is charged, while cleanup-only terminal recovery has J=0/D=0. |
| G22 | Every mutating/reconciling terminal batch branch treats uncertain original-manifest cleanup as pending. Single-event recovery cleans by the original terminal receipt; guarded replay reclamation also checks its separate SDK batch manifest before deleting its own private manifest. Tests cover CAS refusal, exceptions, a lost successful delete response, staged same-work retry and completed-work reclamation without new preparation, model writes or checkpoint advancement. Read-only Current and receipt inspection perform no cleanup. |

Eleven new owning test methods cover nineteen cases; the existing full-replacement method gained exact J/D assertions. The final affected-class commands additionally execute their existing preservation cases.

| Sequential focused gate | Passed / selected | Failures / errors / skips / not-run |
| --- | --- | --- |
| Server: ProjectionDeliveryStateContractTests, DaprProjectionDeliveryRetrySchedulerTests, NamedProjectionDispatchCoordinatorTests | 245 / 245 | 0 / 0 / 0 / 0 |
| Client: DaprReadModelBatchTests | 64 / 64 | 0 / 0 / 0 / 0 |
| DomainService: DomainProjectionDispatcherV2Tests, DomainProjectionHandlerCompatibilityTests, MixedProjectionCatalogTests | 51 / 51 | 0 / 0 / 0 / 0 |

Each owning project was freshly built in Debug/source mode with GeneratePackageOnBuild=false, zero warnings and zero errors. The [closing audit](review-2-implementation-final-audit.json), [source packet](review-2-implementation-source2-inputs.json), [effective dependencies](review-2-effective-dependency-audit.json), per-command JSON/native XML and raw logs retain exact command/runtime/asset identities. Source/runtime bytes remained unchanged during all three serial commands, and source2 restored assets match the closing observation. These three focused commands are not whole-assembly or whole-checkout qualification.

The initial affected-class commands overlapped. Their passing native results remain supplements. The final `*-serial` gates ran sequentially, with the native exit observed before starting the next. No rebuild or wider queue was added for that correction.

[Preliminary attempts](review-2-preliminary-attempts.json) retain the stale-assets CS1704/MSB3243 failure, focused mode-correcting restores, compilation corrections, the 15/18 first regression run and 19/21 later regression run. The two later failures used a fixture whose selected model exceeded its existing proof-body cap; reducing only the new fixture padding to 7000 bytes retained a separate private manifest while respecting that cap. No existing assertion or budget changed. Initial source/runtime snapshots were not captured for those early commands, so their successful cases are not used as final qualification.

The initial failed build inherited GeneratePackageOnBuild and emitted four ignored Debug UniqueIds package/symbol archives. Their exact observed paths/hashes and failed producer log are retained in the preliminary record. Existing/uncertain bytes were preserved; those outputs are not inventory3 archives or a qualified package lane. Every later build disabled automatic package generation. No explicit pack, Release queue, Live rerun, P1R alteration, activation, cutover, publication, commit or push occurred.

The normative digest remains `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`. Earlier R1 evidence remains historical. Global/latest whole-checkout validation, sealed CI, owner assurance and AD-12/NFR8 acceptance remain open for the parent workflow.
