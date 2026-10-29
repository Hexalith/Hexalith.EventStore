# EXT-PROTECTION-1 reserved snapshot verifier correction — 2026-10-29

**BlockedNotSubmitted; full delivery remains in progress.** The offline verifier now rejects the malformed reserved snapshot shapes identified by EC-3 in the concurrent Platform review. This is a local component correction, not closure of that independently owned review, installed qualification, or a replacement target proposal.

Observed EventStore HEAD is `8348bc491ace70dbd0d84c2320a6e4372479bbb9` on `main`, obtained directly from Git. The correction is in the working tree. Both prior source candidates remain withdrawn. Complete accepted EventStore/Platform targets, installed profile/engine/role/trust bindings and accepted integration date remain unresolved. The proposed date `2026-11-02` remains unaccepted. The original spec baseline and frozen intent are preserved.

The [separate machine packet](ext-protection-1-delivery-2026-10-29-verifier-ec3.json) retains the exact current register contract and references the [previous packet](ext-protection-1-delivery-2026-10-29-resumed.json) by SHA-256 for the complete clause/matrix mappings and prior evidence. Earlier test counts are historical; they are not current-run results.

## Correction and verification

`snapshot_record_envelopes` in [the verifier](../../tools/ext_protection_1.py) previously accepted an exact-cased `ProtectedContent` object with `Target: null` and plaintext `Value` beside otherwise valid sealed entries. Those entries and their complete manifest could remain unchanged. The production `SnapshotEntryLayout` already rejects this representation.

The verifier now requires every reserved `ProtectedContent` member to contain the exact `Target`/`Value` envelope, with a target object declaring `TargetProtectionKeyAlias` and a null sealed value. Existing traversal verifies the complete target and manifest. Nested and array occurrences are covered. Ordinary domain objects named `Target` and `Value` remain compatible. The plan, codecs, golden vectors and production registration are unchanged.

| Executed command, from EventStore root | Result |
| --- | --- |
| `python3 -m unittest tools.tests.test_ext_protection_1_mixed_snapshot -v` before correction | Exit 1; 24 false-accept subcases reproduced. Retained failure is not counted as a passing run. |
| Same focused command after correction | Exit 0; 7 tests pass, including 13 malformed shapes checked against both legitimate targets and ordinary-domain compatibility. |
| `python3 -m unittest discover -s tools/tests -p 'test_ext_protection_1*.py' -v` | Exit 0; all 80 offline verifier/C# interoperability tests pass. The focused 7 are included in 80. Temporary Debug/project-reference interoperability build: zero warnings/errors. |
| `python3 tools/verify-ext-protection-1.py --mode Source` | Exit 0; 41 planned cases, SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`, `liveExecuted:false`. |
| `git diff --check -- tools/ext_protection_1.py tools/tests/test_ext_protection_1_mixed_snapshot.py` | Exit 0. |

Exact command arguments, results, log hashes, before/after source bytes, source hashes and the current two-file diff are retained in `/home/administrator/hexalith-protection-evidence/ext-protection-1/verifier-ec3-20261029T180052Z-3d7abb7d/`. The parent inspected the complete correction and matching production layout rules, checked the saved log hashes and current source hashes, and confirmed that the register contract is unchanged. No engine or Server suite was rerun for this Python-only correction. Prior concrete snapshot/persistence results remain in the previous packet.

The full historical diff from the original baseline remains retained by the previous attempt and is not claimed reviewed here. It spans extensive unrelated history. No files were staged. Formal workflow review/completion remains pending in step 3 because full acceptance and the installed matrix remain unmet.

## Remaining owner acceptance

G1–G7 remain open under the full register contract:

- G1/G3: installed six-operation custody, atomic manifest provenance, complete historical/copy inventory, irreversible physical erasure, nonrollback and restore exclusion remain unqualified. The historical `ObservedRevival` negative control is preserved and was not rerun.
- G2: independent current authority, exact grants/enrollment/genesis, guard/registrar/trust and outside-restore installation remain unqualified.
- G4: concrete mixed-root engine/persistence/replay source exists; installed source/target/cache/backup/restore evidence and writer/anchor/scheduler histories remain required. Concurrent role per-copy replay and exact recovery corrections remain with the Platform owner.
- G5/G6: three role programs and the 41-case catalog exist. Source pass paths, pending-manifest recovery, exact copy/receipt correlation, holder lifetime, bounded history and missing-input preflight remain under concurrent correction. This continuation fixes local EC-3 only. Independent installed roles/ACLs, authenticated complete originals/currentness/physical evidence and the entire installed matrix have not passed.
- G7: complete reviewed coordinated clean immutable source/package targets, installed profile/plan/engine/role/trust bindings, accepted date and owner acceptance remain absent.

The accepted profile remains absent at `/home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json`. The previous packet retains the exact required host/native/authority fields and missing installed-reference inventory. Current source hashes do not substitute for installed binary or trust hashes. No remote installation was queried and no missing value was filled with a fixture identity.

The candidate command, from the Agents repository root, remains unexecuted:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

No Live/cohort/Agents consumer, native/infrastructure/enrollment/spending action, AppHost restart, Platform/register/default-DI/dependency/plan/golden mutation or Git mutation occurred. Concurrent source, tests, owner records and isolated re-derivation work were preserved. No incomplete owner record is submitted for acceptance.
