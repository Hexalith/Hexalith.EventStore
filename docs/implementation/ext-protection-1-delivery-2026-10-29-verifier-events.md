# EXT-PROTECTION-1 persisted event verifier checkpoint — 2026-10-29

**BlockedNotSubmitted; full delivery remains in progress.** The offline verifier now rejects malformed persisted event observations that the production parser already refuses. The [machine packet](ext-protection-1-delivery-2026-10-29-verifier-events.json) retains the exact nine register fields, complete v21/v23 contract, clause and matrix mappings, current independently owned source findings, and missing installed bindings. The [snapshot grammar checkpoint](ext-protection-1-delivery-2026-10-29-verifier-grammar.md) and earlier packets remain historical evidence.

## Correction

[The verifier](../../tools/ext_protection_1.py) used permissive JSON decoding for persisted records and checked event ciphertext only for minimum length and magic bytes. Duplicate members, extra or nested reserved content, invalid UTF encoding, nonfinite numbers, excess depth/nodes, malformed decoded member names, escaped reserved ciphertext tokens and malformed V2 carriers could therefore pass an event observation.

Persisted records now use strict UTF-8 JSON with duplicate-member refusal. Event observations require the exact `ProtectedContent` field shape, complete target equality, the production 64-container/65,536-node bounds and canonical V2 header, lengths, key reference, single-field ordinal and nonce. [Nine new tests](../../tools/tests/test_ext_protection_1_event_evidence.py) exercise raw persisted bytes through `evaluate` for both source-outbox and target-event records. Ordinary domain fields, escaped outer member names, untouched scalar escapes and original golden bytes remain compatible. No production runtime, plan, golden, dependency or registration changed.

## Verification

| Final check | Result |
| --- | --- |
| Complete offline verifier/C# interoperability suite | 99 passed; zero failures/skips. Includes all nine new event methods. |
| Compiled production parser comparison | All 51 distinct raw fixtures agree: 8 accepted, 43 rejected, zero disagreements. |
| Debug/project-reference diagnostic and interoperability builds | Zero warnings/errors. |
| Source mode | 41 cases; unchanged plan SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`; `liveExecuted:false`. |
| Whitespace checks | Pass. |

Final suite command, from EventStore root:

```bash
env 'NuGetLockFilePath=/home/administrator/hexalith-protection-evidence/ext-protection-1/owner-audit-20261029T201418Z-41ecac07/interop-locks/$(MSBuildProjectName).packages.lock.json' python3 -m unittest discover -s tools/tests -p 'test_ext_protection_1*.py' -v
```

The environment property redirects generated source-mode locks outside the checkout. The exact probe build/run commands, all intermediate attempts, source/binary/fixture/log hashes and complete two-file patch are retained in `/home/administrator/hexalith-protection-evidence/ext-protection-1/owner-audit-20261029T201418Z-41ecac07/impl-final-manifest.json`. The complete final regression class against the original verifier reproduces 80 false-accept assertions across the two event kinds, with zero errors. The initial fixture correction and three production/Python Unicode disagreements are retained; final results exclude these failed attempts.

Parent inspected the complete correction, production rules and external C# probe and verified their recorded hashes. The probe checks syntax and envelope grammar; it supplies no installed authentication, physical custody or erasure evidence. No engine or Server test suite was rerun for this Python-only correction. Earlier engine/persisted-state/native results remain historical.

## Current source and acceptance gaps

Platform's current [round-2 handback](../../../platform/docs/implementation/ext-protection-1-erc-component-delivery-2026-10-29.md) supersedes the prior checkpoint's round-1 status. Both latest review candidates remain withdrawn: EventStore `6068b7771372eb0b05ed60bc91002a1c6b4486ba` and Platform `06751feca75a83c892d8cef53b35c7a2c5c58ddb`. The 17 individual findings and actual installation-field inventory are retained in the machine packet and external evidence. Their isolated re-derivation is independently owned; no isolated changes were imported and no independent review was closed here.

Remaining source work includes bounded custody capacity/current transitions, complete preflight before enrollment, genuine holder completion and idle cleanup, retained refused guard/conditional-owner originals, exact replay/backup call binding, independently retained admission discovery, authenticated permanent-loss ordering, trusted single-root erased snapshot replay, rejected-replacement historical binding and actual copy-policy/P34 coverage. This event correction does not resolve those findings.

G1–G7 remain open. Independent custody/authority/registrar/trust/genesis installation, distinct Executor/Observer/Enrollment binaries and ACLs, complete native/source/observation arguments, all-copy physical erasure/nonrollback/restore proof and the entire installed 41-case qualification remain required. The accepted profile is absent at `/home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json`. Installed engine/profile/role/trust hashes, accepted coordinated targets and accepted date remain null. The retained date proposal `2026-11-02` remains unaccepted. The prior `ObservedRevival` negative control remains unqualified and was not rerun.

Observed EventStore HEAD `6e0280bf489171326e2ce1ba3dde361712b7c977`, sibling Platform HEAD `2a18cdd33fcb609a563011e9bfd4153f56445c63` and root-declared Platform HEAD `c4d68ed739977c5e24e5c91e4bfd601e2a44dcd8` are source observations only. The original frozen intent and baseline `9b062d0d4f506bf45b7fdcd8e54189599a36ff9b` are unchanged. All full-delivery tasks remain unchecked; status remains `in-progress`, workflow step 3. The register remains `Uncommitted`; availability and qualification remain false. No incomplete owner record is submitted for acceptance.

Candidate command, from the Agents root, retained without execution:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

No Live/cohort/Agents consumer, native custody/infrastructure/enrollment/spending action, AppHost restart, Platform/register/default-DI/dependency/plan/golden mutation or Git mutation occurred. Concurrent Folders, HTTP-security and deferred-work edits and all previous owner records were preserved.
