
- source_spec: `_bmad-output/implementation-artifacts/spec-folders-12-2-remaining-gates.md`
  summary: Preserve and scan diff hunk-header text in the Server guard without changing the user-selected qualification snapshot.
  evidence: Selected SHA-bound SecretsProtectionTests.SourceContexts.cs:120-130 scans reconstructed views and :149-153 masks original tool output; SourceDiffView :186-190 opens a fence and drops the original header. Existing tests :192-204 inject only body material. This pre-existing false-negative gap remains open after the user's explicit current/latest source selection; restoring transient edits was not authorized by that selection. A narrow header-preservation repair needs generated header-only controls, a new build/test binding and a successor seal.

- source_spec: `_bmad-output/implementation-artifacts/spec-folders-12-2-remaining-gates.md`
  summary: Resolve the preserved Domain sweep-renewal observation race without relaxing deadlines or assertions.
  evidence: Sealed folders-12-2-gates-20261022T151209Z/domain-full.xml first execution failed SweepRenewalFaultPreservesCompletedSourceObservation at faulting.Fired=false after18ms. The RunContinuationsAsynchronously source-release continuation can race the runner cancellation and underlying held write; focused and repeat passes do not erase that failure.

- source_spec: `_bmad-output/implementation-artifacts/spec-folders-12-2-remaining-gates.md`
  summary: Determine why the unchanged retained-identity mutation test's initial baseline commit returned Indeterminate.
  evidence: The same sealed initial Domain execution failed DispatcherRejectsChangedRetainedEarlyMiddleAndFinalIdentitiesBeforePromotion for changed129/payload/false before constructing the mutation. The dispatcher catch-all concealed the underlying exception. Focused18-case and separate full passes are retained diagnostics, not proof of a cause or repair.

- source_spec: `_bmad-output/implementation-artifacts/spec-folders-12-2-remaining-gates.md`
  summary: Resolve Domain historical-candidate setup timing sensitivity under the existing100ms control-read deadline.
  evidence: Latest isolated-snapshot domain-full.xml failed RegistrationCostDoesNotGrowWithHistoricalPages during pre-registration of520 historical candidates at ProcessPublicationTests.cs:741, via ProcessCheckpointStore ReadPageAsync and ProcessReadDeadline.cs:58. The measured read-cost assertion was never reached; unchanged focused execution passed. Scheduling delay versus slow read execution remains unproven and no limit or assertion was changed.
