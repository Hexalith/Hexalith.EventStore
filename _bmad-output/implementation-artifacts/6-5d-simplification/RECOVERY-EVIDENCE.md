# Story 6.5d correction evidence

The owner approved recommended RD1–RD4 corrections on 2026-10-04. This run starts from canonical HEAD `5e32d07a6ac7a1bf70cc0ca554ea9928145b65f6`, after repository history and runtime files changed externally between planning and approval. Those changes are preserved, not attributed to this specification work. The original source baseline, frozen intent, protected candidates and archived contracts remain unchanged.

`correction-checkpoint.json.gz` records initial byte digests or gitlink revisions for 8,252 tracked paths outside the permitted artifacts. It contains hashes, not full initial file bytes. Its decompressed SHA-256 is pinned in `acceptance.py`; the approved correction frozen block has its own immutable pin. Current acceptance authenticates baseline ancestry, every committed path in the baseline-to-HEAD range, working-tree and untracked paths, outside-scope hashes, original protected/archive hashes, both frozen blocks, 54 dispositions, AD-13 UNAPPROVED and nine root submodule revisions. It preserves unrelated tracker entries and the original 24 wire records, 32 addressed keys and shared scope-key literal. A future in-scope commit can rerun the check; an unrelated commit or rewritten baseline cannot silently pass.

`pre-correction-evidence.tar.gz` retains the exact initial bytes of 38 story/evidence artifacts. `pre-correction-manifest.json` binds original paths, byte lengths and SHA-256 values; both manifest and archive are independently pinned by the acceptance script. This snapshot includes the original acceptance script, manifests, probe scripts, outputs and completion claims before correction. It is read without extracting or executing its files. The earlier `recovery-checkpoint.json.gz`, archived candidates and `prior-evidence/` remain byte-identical history. The old recovery checkpoint is authenticated as history, not used to deny pre-existing changes from before this correction run.

`pre-correction-checks.json` records the unchanged verifier passing and the historical acceptance script refusing the newer pre-existing `CommandStatusQueryResponse.cs` bytes. At the earlier planning HEAD it instead reached `AssertionError: Git history changed`. Neither failure is described as a passing rerun. Corrected command outputs and exact current artifact hashes are retained separately after implementation and parent review.

Run from the repository root:

```bash
python3 _bmad-output/implementation-artifacts/6-5d-simplification/verify.py
python3 _bmad-output/implementation-artifacts/6-5d-simplification/acceptance.py
python3 _bmad-output/implementation-artifacts/6-5d-simplification/mutations.py
python3 _bmad-output/implementation-artifacts/6-5d-simplification/reviews/focused-regressions.py
python3 _bmad-output/implementation-artifacts/6-5d-simplification/preservation-cases.py
git diff --check
```

[Historical evidence](prior-evidence/README.md) preserves the earlier proposal, acceptance, independent constructor and protected gates byte-for-byte. The current `independent-answers.mjs` separately constructs wire/control/public bytes, parses normative ceilings from obligations.md and checks every family; integers are transferred exactly rather than rounded through JavaScript numbers. The archived constructor also checks compatibility, and a separate constructor verifies the unframed shared scope key. Historical acceptance claims confer no current authority. The historical owner/cleanup/restore probes contain absolute paths, and their observed output lacks a retained, hash-bound pre-fix verifier. Their original bytes are retained, but reproducing their historical failures is not a current acceptance claim. Current scenario checks use repository-relative paths and corrected source hashes.

The old review manifests name an overwritten `/tmp` location; those fields are explicitly marked non-durable. Their retained compressed diffs and hashes identify historical review inputs. The current correction review records its own durable compressed diff and source hashes; no temporary path serves as lasting evidence.

`preservation-cases.py` supplies four bounded refusal checks using simulated Git replies: a non-ancestor baseline and forbidden committed, working-tree or untracked paths. Each pins its owning failure. It creates no commits, branches or workspace changes; the ordinary acceptance run separately checks the actual repository and current independent literals.

All authorization, provider receipts and transactions in the executable specification remain fixtures. Provider crash qualification, architecture amendment and exact integrated AD-13 human approval remain required handoff gates. Story status and local verification confer no runtime implementation authority.

Current correction completion: all four recommended decisions, all 26 RP patches and seven individually triaged review findings are closed. [Review closure](reviews/correction-closure.md) records five roots, targeted interleavings and three newly failing owning corruptions. The initial review launched two fresh reviewers; the verification lens reused the earlier read-only investigator after the third fresh launch twice failed at the tool thread limit. The retained record discloses that prior context. [Parent acceptance](parent-acceptance.md) maps the original criteria and measured sizes. Both execution records/sprint child state are done; D-SPLIT is resolved only for the child-specification prerequisite. Existing RW1 remains deferred, with no new deferrals.

The initial and final review manifests bind separate retained compressed diffs and artifact hashes. The final command manifest binds successful outputs to their script/input hashes. Historical outputs and current outputs are separate evidence, and temporary paths remain non-durable.
