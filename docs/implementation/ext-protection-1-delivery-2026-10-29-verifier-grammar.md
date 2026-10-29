# EXT-PROTECTION-1 snapshot grammar checkpoint — 2026-10-29

**BlockedNotSubmitted; full delivery remains in progress.** The offline verifier now rejects additional mixed-root skeletons that the production engine already refuses. The [machine packet](ext-protection-1-delivery-2026-10-29-verifier-grammar.json) preserves the exact nine register fields, full v21/v23 contract, seven matrix mappings and remaining G1–G7 requirements. The [previous EC-3 checkpoint](ext-protection-1-delivery-2026-10-29-verifier-ec3.md) remains historical evidence.

## Correction

[The verifier](../../tools/ext_protection_1.py) accepted malformed or foreign standalone target declarations, case-varied ownership envelopes, exact or escaped reserved `$pdenc`/`$enc` markers, excess JSON depth/node counts and escaped lone-surrogate target strings beside unchanged valid sealed entries. The correction follows the existing `BoundedJsonDocument` and `SnapshotEntryLayout` rules. No production runtime, plan, codec, golden or registration changed.

[Seven new tests](../../tools/tests/test_ext_protection_1_mixed_snapshot.py) preserve ordinary `Target`/`Value` data, uppercase ordinary marker names, valid same-tenant standalone declarations larger than 2,048 characters/bytes and exact 64-container/65,536-node boundaries. Standalone declarations constrain geometry; they supply no enrollment authority. Per-entry binding requirements remain intact.

## Verification

| Final check | Result |
| --- | --- |
| Complete offline verifier/C# interoperability suite | 87 passed; zero failures/skips. Includes all 14 mixed-snapshot tests. |
| Fresh Debug/project-reference production parser comparison | All 26 distinct fixtures agree: 23 rejected, 3 accepted. Build has zero warnings/errors. |
| Source mode | 41 cases; unchanged plan SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`; `liveExecuted:false`. |
| Python syntax and diff checks | Pass. |

Final suite command, from EventStore root:

```bash
env CustomAfterDirectoryBuildProps=/home/administrator/hexalith-protection-evidence/ext-protection-1/grammar-20261029T180948Z-956fb743/source-build-input.props python3 -m unittest discover -s tools/tests -p 'test_ext_protection_1*.py' -v
```

The external props file redirects generated source-mode lock outputs outside the checkout; it changes no package pin or repository configuration. Exact commands, red/green logs, source/binary hashes, production parser inputs/outputs and incremental/current two-file diffs are retained in `/home/administrator/hexalith-protection-evidence/ext-protection-1/grammar-20261029T180948Z-956fb743/verification-manifest-final.json`. The initial 34 false accepts, 12 Unicode false accepts and an intermediate overly restrictive standalone-length regression remain retained. Final results exclude failed attempts. Parent inspected the complete correction, production rules and probe, and checked every recorded command-log hash and current source hash. No engine or Server suite was rerun for this Python-only change; earlier persisted-state results remain in prior packets.

Parent preservation evidence is `/home/administrator/hexalith-protection-evidence/ext-protection-1/resume-20261029T180858Z-8b48a566/`. Original frozen intent and baseline `9b062d0d4f506bf45b7fdcd8e54189599a36ff9b` are unchanged. The complete historical-baseline diff remains retained by the earlier attempt; no full historical review or formal workflow-completion claim is made here. Workflow remains at implementation step 3 because complete Tasks & Acceptance and the installed matrix remain unmet.

## Remaining delivery

Both earlier source candidates remain withdrawn. Concurrent Platform pass-path, recovery, per-copy correlation, holder-lifetime, bounded-history and preflight repairs remain separately owned in isolated worktrees. This correction imports none of that unfinished work and does not close the independent review.

G1–G7 stay open. Independent installed custody, current authority/registrar/trust/genesis, Executor/Observer/Enrollment identities and ACLs, accepted copy inventories, all-copy physical erasure/nonrollback/restore proof and the complete installed 41-case run remain required. Required native, host, authority and role fields are retained in the machine packet. The accepted profile is absent at `/home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json`. No missing input is replaced with a fixture identity. The retained `ObservedRevival` negative control remains unqualified and was not rerun.

Observed EventStore HEAD `8348bc491ace70dbd0d84c2320a6e4372479bbb9` is a source observation only. Complete accepted EventStore/Platform targets, accepted date and installed profile/engine/role/trust hashes remain null. Proposed date `2026-11-02` stays unaccepted. The register remains `Uncommitted`; availability and qualification remain false.

Candidate command, from the Agents root, retained without execution:

```bash
python3 ../eventstore/tools/verify-ext-protection-1.py --mode Live --profile /home/administrator/hexalith-protection-evidence/ext-protection-1/accepted-profile.json --evidence-root /home/administrator/hexalith-protection-evidence/ext-protection-1/attempts
```

No Live/cohort/Agents consumer, native custody/infrastructure/enrollment/spending action, AppHost restart, Platform/register/default-DI/dependency/plan/golden mutation or Git mutation occurred. Concurrent HTTP-security work and all prior owner records were preserved. No incomplete owner record is submitted for acceptance.
