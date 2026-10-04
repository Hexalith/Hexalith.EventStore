# Story 6.5d recovery evidence

The recovery run starts from full HEAD `ab11c86a991a6aa8349041e73312a9655a0aa1bf`. It preserves the execution record's original source baseline and frozen intent. `recovery-checkpoint.json.gz` records the exact initial bytes or gitlink revisions of 8,251 tracked paths outside the allowed story artifacts. `acceptance.py` pins the decompressed checkpoint digest, checks those inputs, original protected candidate/archive hashes, the exact 54 routed findings, AD-13 UNAPPROVED, current task scope and root submodule revisions. It also reconstructs current wire answers using a separate Node implementation and exact 64-bit integers. This scope audit is separate from the unchanged historical baseline gate.

Run from the repository root:

```bash
python3 _bmad-output/implementation-artifacts/6-5d-simplification/verify.py
python3 _bmad-output/implementation-artifacts/6-5d-simplification/acceptance.py
python3 _bmad-output/implementation-artifacts/6-5d-simplification/mutations.py
python3 _bmad-output/implementation-artifacts/6-5d-simplification/reviews/focused-regressions.py
git diff --check
```

[Historical evidence](prior-evidence/README.md) preserves the earlier temporary proposal, acceptance, independent constructor and protected gates byte-for-byte. Its manifest binds original paths, byte lengths and SHA-256 values. Those older acceptance claims are superseded by the focused review; running the historical scripts is not a gate for the current candidate. Current verification and focused review outputs are retained alongside this document when completed.

All authorization, provider receipts and transactions in the executable specification remain fixtures. Provider crash qualification, architecture amendment and exact integrated AD-13 human approval remain required handoff gates. Story status and local verification confer no runtime implementation authority.
