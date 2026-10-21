---
title: 'EXT-PROTECTION-1 Story 5.4 owner qualification packet'
type: 'feature'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: d01c6756c769dcbd8d4d5fff472d07339ebeb22a
context:
  - AGENTS.md
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
  - ../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - ../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-18.md
  - docs/implementation/ext-protection-1-owner-acceptance.md
  - docs/implementation/ext-protection-1-verifier-protocol.md
  - tools/ext_protection_1.py
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agents Story 5.4 cannot start OQ-5.4-1 phase 0 because EXT-PROTECTION-1 is `Uncommitted`: `TargetVersionOrCommit`, `TargetIntegrationDate`, and the compatibility command are `TBD`. EventStore already seals through the existing hooks, routes an explicit `(TenantId, AgentInteractionId, TargetProtectionKeyAlias)`, replays trusted destroyed content as typed `Erased`, and owns the consumption, hold, revocation, retry, and lookup state machine. Those paths still fail closed because the custodian, guard authority, runtime roles, and trust anchors are not installed. A substitute provider or a filled-in anchor would make the record look qualified when it is not.

**Approach:** Leave production hooks, the 41-case plan, goldens, one-attempt evidence, and the Agents register unchanged. Refresh the EventStore owner packet so it states the observed full `HEAD`, the already proposed integration date, the complete Live command, and the exact missing reference names the verifier and Platform runtime already read. Availability stays closed. Do not run Live.

</frozen-after-approval>

## Implementation Notes

2026-10-21: production hooks, explicit alias routing, trusted typed `Erased` replay, sealed distribution, and the consumption state machine were already present. No provider, anchor, register row, plan case, or Live attempt was added. The owner packet now records observed HEAD `d01c6756c769dcbd8d4d5fff472d07339ebeb22a`, proposed date `2026-11-02`, the existing Live command, and the exact null custody, guard, runtime, and trust-anchor references. Public HTTP replay stays unreadable. Unrelated dirty files were left untouched.

Review corrections distinguish register operation names from the actual lifecycle and manifest ports, state the profile schema `validate_profile` enforces, record that an early Live refusal still creates an attempt directory, and cite the retained mappings and `ObservedRevival` hashes. `python3 tools/verify-ext-protection-1.py --mode Source` exited 0 with 41 cases, plan SHA-256 `c1e7759a2687c122dd182aa48f1828e531166c0157a04b2ae0eba35e9ac4d10c`, and `liveExecuted:false`.

## Review Triage Log

- Custody operation names — medium. The six register names are not methods on the existing ports. The packet now names `ExecuteAsync`/`LookupAsync`, `ConsumeAsync`/`LookupAsync`, and the unimplemented `custody-lifecycle-v1` binding.
- Profile schema — medium. The packet now lists the exact closed profile, role, manifest, and capability-trust members, and says the secrets qualification field names are not profile fields.
- Live preconditions — medium. The packet now records the register parser, clean-tree and ancestry checks, the attempt directory created before the register read, and that `RECOVERED` is not `qualificationPassed`.
- Installed secrets bindings — medium. The mappings hash and `lifecycleOrAllCopyQualified:false` / `ordinarySnapshotSemantics:true` were rechecked, and tenant, HMAC, host, and endpoint values are excluded from the protection profile.
- Historical target table — medium. This section now supersedes the later proposed-record baseline and leaves G4, G6, and the host/secrets cohort prerequisites open.
- Guard methods — medium. The six methods that return false are named, and `VerifyDispatchAsync` is recorded as able to return true without being an installed guard.
- Missing spec sections — false. The oneshot route keeps only Intent and Implementation Notes. The frozen block was not edited. The protocol and verifier paths were added to context.
