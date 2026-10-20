---
title: 'Story 6.4 separate EventStore query authority prerequisite'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
qualification_status: 'not-accepted; broader governance/topology gates remain open'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '1eaabca4f8d3597a3a1f9fadf341be5cb9a3a8c0'
context:
  - '/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore/AGENTS.md'
  - '/home/administrator/projects/hexalith/projects/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore/docs/security/workload-binding-authority.md'
---

<frozen-after-approval reason="User explicitly authorized implementation of the selected D1/D4 EventStore prerequisite">

## Intent

Repair production query authority signing and expose verified originating delegation separately from the owner hop. Implement only EventStore's prerequisite for Story 6.4, following the selected D1–D6 decisions and contract annex in the enclosing Projects workspace.

## Boundaries & Constraints

Preserve actor/resource authorization, existing routes, principal/domain limits, assertion lifetimes and cryptography. No commits, staging, branch operations, package-pin changes, deployment, enrollment changes or Projects resolution handlers. Preserve all existing workspace changes and historical evidence. Never load skills under references or initialize nested dependencies. Run local Debug tests with source references (`-p:UseHexalithProjectReferences=true`).

## I/O & Edge-Case Matrix

| Scenario | Given / When | Then |
| --- | --- | --- |
| Production query | Enrolled gateway requests exact Tenant/domain/digest and approved audience/operation | Actual RSA authority, outbound issuer and receiving verification accept exact request |
| Policy mismatch | Unknown/extra/missing keys, wildcard values, wrong credentials/workload, audience, operation or Tenant/domain | No authority gained; refuse issuance/verification |
| Tampering | Actor, workload, delegation, scopes, audience, resource, payload or context changes after signing | Refuse before protected handler work |
| Lifetime/revocation | Expired assertion/proof or expired/revoked grant | Refuse expired evidence/new issuance; existing JWT validity remains bounded by existing lifetime |
| Aggregate boundary | Aggregate length 200/201/256/257 | First three sign/verify; 257 refuses without truncation |
| Limit isolation | Principal >200 or domain beyond existing bound | Refuse; aggregate amendment does not widen these limits |
| Origin separation | Nested verified originating query reaches owner SDK | Original actor/workload/delegation/scopes/audience survive in separate immutable verified context; owner hop retains its own workload/audience |
| Compatibility | Legacy request without origin context | Existing behavior, serialization and authorization remain intact |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.ServiceDefaults/Authentication/QueryCallerAuthorityBinding.cs`: Bind currently emits digest only; Decode requires Tenant/domain. Verify must match all three exact claims.
- Adjacent `WorkloadAuthorizationDetails.cs`, `WorkloadBindingAuthority.cs`, `WorkloadAuthorityGrant.cs`: retain v1 profile. Add default-off `AllowQueryAuthorityDigest` grant capability, allowing only a canonical 64-character uppercase SHA-256 digest for `domain-service:query`, mandatory exact Tenant/domain values and no arbitrary extra keys. Preserve ordinary exact-value families; do not interpret wildcards.
- `src/Hexalith.EventStore/Controllers/QueriesController.cs`: SubmitBoundOwnerAuthorityAsync verifies originating query/assertion but drops origin delegation/scopes/audience. Preserve allowlist/proof checks and forwarded permissions.
- `src/Hexalith.EventStore.Contracts/Queries/QueryCallerAuthority.cs`, `QueryEnvelope.cs`; `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`, `HandlerAwareQueryRouter.cs`: additive digest-covered immutable context must survive routing and JSON/DataContract round trips. Update manual equality/hash where necessary; omit new null wire values for legacy proof compatibility.
- `src/Hexalith.EventStore.DomainService/VerifiedQueryCaller.cs`: expose context only after SDK digest verification. Original sources are authenticated envelope fields populated by DualPrincipalClaimsHelper: sub, workload resolution, act.sub, scope/scp and aud. Preserve absent collections. Keep owner-hop identity separate; transport workload principals currently omit aud, so use validated receiver configuration or a narrow verified transport context.
- `src/Hexalith.EventStore.Client/Gateway/BoundOwnerQueryProof.cs`, `BoundStreamReadProof.cs`: widen aggregate only to256; keep principal200 and Tenant/domain200 proof limits, independent envelope64/domain rules.
- Existing tests: Server Authentication/WorkloadBindingAuthorityTests, JwtWorkloadAssertionIssuerTests, SyntheticBindingAuthorityHandler; Controllers/BoundOwnerQueryAuthorityTests and BoundOwnerQueryControllerTests; DomainService trust-boundary query tests; Contracts Queries/QueryCallerAuthorityTests. New focused same-named test files are allowed.

## Tasks & Acceptance

- [x] Repair binding producer/verifier and narrow issuance policy, preserving default denial.
- [x] Carry originating context through the gateway and expose it via the verified SDK feature, independently of transport identity.
- [x] Amend aggregate proof limits and test 200/201/256/257 plus principal/domain isolation.
- [x] Add production RSA signing/verification, tampering, wrong-scope/resource/audience, expiry/revocation and compatibility tests covering the matrix.
- [x] Run affected individual Server, Contracts, DomainService and Client test projects; document exact commands/counts/blockers and exact contracts/revisions in a concise EventStore report. Update authority documentation with capability and field mapping, distinguishing source tests from deployed acceptance.

Given authorized actor/resource state, when digest verification succeeds, then owners still independently authorize current access. Given a verified origin, when an owner receives it, then no payload/header can create that verified feature. Given existing consumers, when the optional context is absent, then existing contracts remain supported.

## Implementation Notes

The user requested implementation and continuation after selecting D1–D6; no further design approval is required. This is separate from the blocked Projects parent story and does not update its tracking. Investigation found no unresolved intent gap or irreversible action. Changes span authority, additive contracts, SDK and tests. Use repository evidence to settle routine implementation details and finish all authorized work.

Implementation and matrix audit complete: all 415 final focused cases passed without skips, and all four Debug/source builds passed. The [source verification report](6-4-eventstore-query-authority-prerequisite-report.md) records exact contracts, revisions, commands and native results. Root inspected the complete source/test diff and strengthened the actual RSA test to cover serialized originating context through the SDK and current-owner denial. Broader governance/topology failures and an interrupted diagnostic Server run remain explicit limitations, not accepted gates. Preexisting Projects edits and captured package/build configuration are unchanged.

After review patches, the final focused total is 422: root reran Server 140 and Contracts 183 with zero failures/skips; unchanged DomainService 89 and Client 10 evidence remains applicable. The [durable evidence packet](evidence/6-4-eventstore-query-authority-20261020/README.md) binds final commands, source hashes and native summaries. The source implementation is complete; broader qualification remains open.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1: raw receiver audience | medium | Confirmed: authentication uses `workload.Audience.Trim()` while the new producer copies raw options. A padded valid configuration yields incorrect verified hop metadata. Patch normalization and custom-audience dispatch coverage. |
| B2: missing receiver options admits null audience | false | The protected HTTP policy requires the registered workload scheme; its options monitor is registered by DI and blank audiences mark authentication unconfigured. Optional constructor omission is a direct test/manual invocation, not an admitted HTTP path. No guard added. |
| B3: inherited origin claim bounds undocumented | low | The existing helper caps scopes/audiences at 64 entries and truncates entries to 512 characters. New consumer documentation should state these inherited metadata limits and separate permission authority. Patch documentation only. |
| B4: temporary-only evidence | medium | Confirmed: the source hash manifest and native summaries currently exist only under `/tmp`, preventing later inspection of tested working bytes. Patch by retaining sanitized manifests, summaries and focused logs beside the report; preserve original evidence. |
| B5: mixed-operation capability grant untested | low | Positive tests cover ordinary grants and capability query grants separately; the new capability's coexistence with another exact-bound operation has no regression case. Patch that policy test. |
| B6: two changing digests untested | low | The production positive test uses one digest, leaving accidental per-query enrollment uncaught. Patch a second valid query under the unchanged grant and verify both exact assertions. |
| B7: duplicate raw query bindings untested | low | Dictionary-based new tests cannot represent duplicate JSON properties. The unchanged decoder rejects duplicates with `TryAdd`; patch raw query-profile cases to preserve that security boundary for the new family. |
| B8: nullable/nondelegated origin producer untested | low | Model round trips cover absent lists but the new gateway mapper has no corresponding legacy/nondelegated origin case. Patch producer/SDK coverage without changing authorization. |
| B9: correctly attested semantic inconsistency untested | medium | Existing forgeries change bytes after signing, so they cannot distinguish digest rejection from the new actor/origin semantic checks. Patch correctly attested inconsistent contexts and assert no handler disclosure. |
| B10: absent DataContract member omission untested | low | JSON omission and populated DataContract round trips pass, but the new `EmitDefaultValue=false` omission promise lacks a direct assertion. Patch the existing compatibility test. |
| V1: custom owner-hop audience unverified | medium | The verification layer established that all producer/consumer assertions use the same default literal, so a constant audience would pass. Same normalization defect as B1; patch a distinct padded receiver audience through gateway/router into the verified SDK handler. |

The edge-case layer returned no findings. All actionable findings above route to trivial implementation, test, documentation or evidence patches; no public contract or selected design changes are required.

Resolution: B1/V1 use identical audience trimming and a custom padded gateway audience through cryptographically verified SDK dispatch. B3 documents inherited claim truncation. B4 retains LF-stable manifests, sanitized native logs and comparisons beside the report. B5/B6 exercise mixed operations and two digests under one unchanged grant; B7 exercises all three duplicate raw JSON keys at the mapped endpoint. B8 preserves nondelegated/absent origin fields. B9 now uses correctly RSA-attested actor mismatch and a recognized authenticated-caller origin carrying disallowed context, proving the new semantic checks. B10 asserts the legacy DataContract member shape. All actionable findings are fixed and covered; B2 remains refuted. No intent/spec loopback or unresolved implementation finding remains.

## Verification

Build affected test projects individually in Debug/source mode, then run built xUnit v3 assemblies with single-dash filters when needed. Run full affected suites when practical; use narrow checks to isolate unrelated failures. `git diff --check` must pass. No claim of deployed acceptance, publication or consumer enrollment follows from synthetic RSA tests.
