# Retained earlier attempts

These attempts remain separate from final current-byte qualification. Their
compressed exact commands, copied/helper/DLL seals, outputs and initial root
inventories are retained beside each attempt.

- control1 compiled the owned runtime and failed two CS0246 errors for missing
  IReadOnlyPayload/IBoundedPayloadWriter test imports. It did not run controls.
- control2 passed the initial hint/equivalence/callback cases, then failed one
  mixed-save test expectation. Committing only state leaves the exact prior
  pair because old/desired state bytes are identical. Runtime correctly
  returned NoCommit. The test was repaired to preserve this explicit case and
  inject a separate third witness for Indeterminate; runtime classification
  was unchanged.
- control3 was control-only: 415 cases passed with zero errors/failures/skips/
  not-run, all 82 private command input/helper/DLL seals stable and whole root
  input set unchanged. It predates the guard definitions and the narrowed
  target mismatch control, so it grants no mutation qualification.

The first mutation-enabled Debug lane and following Release/packages lane both qualified without a failed/surviving mutation attempt; their current evidence is retained separately above.

The preceding replacement/continuation packets and root addenda remain
unchanged execution-time snapshots. The shared paired-write owner and original
actor entry receive a new explicitly scoped policy-isolation revision here;
this does not retroactively upgrade any earlier receipt.
