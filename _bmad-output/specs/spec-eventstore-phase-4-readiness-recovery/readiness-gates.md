# Readiness Gates

## Current Verdict

**BLOCKED — deployed-parity planning handoff is incomplete.** The current `epics.md` and active
Story 3.13 specification still assign positive `v3.94.1` closure to Story 3.13, while
`sprint-status.yaml` contains no Story 3.14 or 3.15 rows. The gate remains blocked until one
unchanged content-addressed planning set passes; an absent verdict is also blocked.

## Deployed-Parity Authority

| Slice | Owns | Prerequisite | Does not authorize |
| --- | --- | --- | --- |
| Story 1.20 | Completed source/package parity | None from Epic 3 | Deployed parity, release, deployment, or consumer removal |
| Story 3.12 | Completed historical multi-platform correction | Existing predecessors | Reopening Story 1.20 or supplying current positive parity |
| Story 3.13 | Content-bound rejected `v3.94.1` disposition | Retained immutable evidence and exact rejection receipts | Positive FR36 parity, release, deployment, or consumer removal |
| Story 3.14 | Provenance correction and a separately authorized later release | Authenticated, durable, unexpired, one-use release authority | Story 3.15 acceptance, deployment, or consumer removal |
| Story 3.15 | Independent positive deployed-runtime parity | Completed Stories 1.20 and 3.14 | Deployment or consumer removal |

## Blocking Gates

| Gate | Required evidence |
| --- | --- |
| Planning baseline | `prd.md`, finalized `architecture.md`, canonical `ux.md`, `epics.md`, this SPEC package, the 2026-08-01 and 2026-08-16 correction proposals, and both adopted story-migration crosswalks exist. The PRD owns FR/NFR wording; the architecture memlog governs architecture decisions. |
| Architecture preservation | The adopted `architecture.md` SHA-256 is `9a20ba5c6860f124ca52a8801e531132a96dd0a761856fdc4684390d848f4101` and its memlog SHA-256 is `3b20c450f7c105b1cedb1d9862b5e6a10e3968e57dcb1698a47a52779d3abedb`. A reviewer parses exactly AD-1 through AD-25 with no gaps, duplicates, or renumbering. |
| Deployed-parity handoff | One manifest binds `_bmad-output/planning-artifacts/epics.md`, the active `_bmad-output/implementation-artifacts/3-13-deployed-runtime-parity-closure.md` or its atomically renamed replacement, new `3-14-corrective-oci-provenance-release.md` and `3-15-corrected-deployed-runtime-parity-closure.md`, and `_bmad-output/implementation-artifacts/sprint-status.yaml` by expected IDs/keys and SHA-256. It requires Epic 3 `in-progress`, Story 3.13 `in-progress`, Stories 3.14/3.15 `backlog`, and one unchanged verifier verdict. A story advances only after its own separately authorized acceptance evidence passes; Story 3.13 reaches `done` only on the bound negative disposition. Any missing, duplicate, partial, stale, prematurely advanced, or mixed-version set remains blocked. |
| Story 3.13 identity | The disposition binds source `80d12ef5eee71a9fe3ea7be51171da4a71b69a28`, review subject `6cee8dad34c1233c6184404b409fb65d1a4dd0bccdd0d0ee54e8869120970a97`, literal `https` in `source`/`url`/`documentation`, absent `revision`, null deployed identity, false deployment authorization, and matching authenticated rejection receipts from the EventStore owner, Release owner, and Test Architect. |
| Story 3.13 key | Rename the 3.13 file, key, and every reference only within the complete validated planning set. If the tracker treats keys as immutable, retain `3-13-deployed-runtime-parity-closure`, update display semantics everywhere, and reject duplicate 3.13 rows. |
| Dependency direction | Stories 3.13-3.15 do not gate or reopen completed Stories 1.20 or 3.12. Story 3.14 precedes Story 3.15. Story 2.6 uses deterministic presentation fixtures; Story 2.11 alone owns generated REST/Tenants production provenance proof. |
| Focused story slicing | Story 4.8 is an evidence ledger whose implementation is owned by Stories 4.9-4.15. Former umbrella Story 8.2 is replaced by Stories 8.2-8.11. Story 7.14 is limited to shell/routes while Stories 7.19 and 7.20 own client/evidence integration and conformance. No ledger or umbrella is treated as an executable story. |
| Migration audit | The 2026-07-15 and 2026-08-01 story-ID crosswalks preserve old-to-new identities, statuses, active-file supersession, implementation evidence, focused tests, review results, and external approval/SHA evidence. A child inherits `done` only where the crosswalk proves it; otherwise it is `review`, `ready-for-dev`, or `backlog` as recorded. |
| Tenants authority | Stories 1.9-1.10, 2.4-2.7, and 4.7 cannot become `done` without maintainer-approved PR/commit evidence, exact Tenants SHA, accepted scope, source/package-mode validation, and an explicit no-approval disposition. |
| Reserved tenant | Story 5.10 rejects the reserved `system` tenant name before state access or side effects and proves case/normalization behavior through the production provisioning boundary. |
| Release and parity authority | Story 3.14 authenticates the Release owner, reserves one authority to one run/attempt, binds every external write, and proves complete AD-11 packages/raw bytes/lengths/media types/OCI graph/provenance/two-platform smokes. Partial publication remains immutable non-authorizing evidence; retry requires a new version and authority. Story 3.15 requires explicit `deployed_runtime_parity: available` and independently derives every lineage edge from trusted facts and retained canonical bytes. |
| Parity receipts | Each Story 3.15 receipt records authenticated identity, exact EventStore-owner, Release-owner, or Test-Architect role, recomputed subject digest, explicit outcome, timestamp, and validity. The verifier validates its signature or immutable approval identity against the exact packet-bound owner-role registry; any subject, evidence, registry, receipt, or validity change fails closed. |
| Consumer parity | Consumer removal binds each capability catalog and owner-role registry by canonical owner/path/schema/version/content digest, the consumer repository/commit, trusted nonempty applicable-mode matrix, and exact removal subject. Empty, unknown, omitted, or failing active modes block. Only a valid authenticated Consumer-owner `consumer-removal-authorized` receipt binding every required digest, timestamp, and validity permits deletion. A consumer repository SHA is never compared with the EventStore SHA. |
| OQ8 platform | Stories 4.9-4.13 implement trusted admission, digest-directory rotation, state/fence behavior, expiry/tombstones, and legacy reconciliation. Story 4.14 produces `_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml` against the `oq8-postgresql-v1` multi-host DAPR profile. Story 4.15 cannot close until the packet proves leakage absence, restart/failover, and exactly one eligible execution and receives senior/security/test review. The Story 4.8 ledger itself carries no executable status. |
| Admin UI ownership | Stories 7.14, 7.19, and 7.20 evolve `src/Hexalith.EventStore.Admin.UI` in place under `eventstore-admin-ui`, using matching FrontComposer packages from the catalog-owned `HexalithFrontComposerVersion` (dated architecture value `4.1.1`) and Fluent UI V5. No second UI host, duplicate legacy page implementation, or unapproved performance budget is created. |
| Backlog story shape | Stories 7.15-7.18 independently govern GDPR-1, IAM-1, KIT-1, and REST generator hardening artifacts. |
| Typed gateway composition | `AddEventStoreGatewayClient(...)` registers the typed client only. Callers opting into DAPR explicitly chain `.AddEventStoreDaprServiceInvocation(appId, apiToken)` last so it is the innermost transport decorator; omission must not silently select DAPR. |
| Admin request-size safety | Story 5.2 acceptance uses concrete limits: `1_048_576` bytes for representative admin JSON write/sandbox bodies and `10 * 1024 * 1024` bytes for `AdminBackupsController.ImportStream`; "tested or documented" is insufficient. |
| Epic 6 accounting | Spec stories are architecture/readiness enablers, not runtime implementation progress. Stories 6.1, 6.3, and 6.5 authorize Stories 6.2, 6.4, and 6.6 only after approval. Story 6.2 proves `snapshot size <= folded-state payload size + MaxSnapshotEnvelopeOverheadBytes` using Story 6.1's numeric bound. |
| Story 7.6 AD-24 contract | Four BDD scenarios prove startup failure, runtime loss/recovery, acknowledged rotation, and real-OpenBao least privilege. The singleton DAPR `openbao` component uses `secretstores.hashicorp.vault` v1 and the value-free `deploy/dapr/openbao-secret-contract.yaml` drives shapes, consumers, lifecycle, component scopes, DAPR default-deny `allowedSecrets`, and matching OpenBao ACLs. |
| Secret profile conformance | Bootstrap inputs are acyclic; required-secret failures gate readiness; rotation is generation-aware publish-overlap-acknowledge-revoke; and release evidence uses real OpenBao. Local substitutes are not production proof, and Azure Container Apps managed DAPR cannot claim AD-24 compliance without a separately approved compatible profile. |
| Payload authorization | Story 8.1 approval authorizes Story 8.2 only. The sequence is `8.2 -> 8.3 -> (8.4 and 8.5) -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10 -> 8.11`; every story remains blocked until its predecessor and evidence gates pass. |
| Payload packages and closure | Story 8.8 alone creates the two payload-protection packages and atomically changes `tools/release-packages.json` from 14 to 16 entries; it does not modify `AGENTS.md`, `CLAUDE.md`, or `.github/copilot-instructions.md`. Story 8.11 alone records G5 `available` after exact source/package/backend identities, EventStore goldens, Parties dual-provider parity, rollback after `pdenc-v2` writes, and owner/security approval. |
| High-risk NFR traceability | NFR1-NFR4, NFR7-NFR11, NFR14-NFR17, and NFR19 map to concrete story coverage and persisted-evidence validation. |
| UX readiness | `ux.md` and the August UX handoff cover Sample accepted-submission behavior, Tenants projection-confirmed states, Admin unavailable-operation behavior, support-safe states, accessibility/localization evidence, dates/source traceability, and in-place FrontComposer/Fluent UI V5 governance. |
| Readiness rerun | Only after the atomic deployed-parity handoff can a fresh assessment validate FR1-FR37 and NFR1-NFR19. The assessment must report no stale positive `v3.94.1` authority, ungoverned forward dependency, or oversized active parent before broad remaining Phase 4 work resumes. Epic 8 remains separately gated post-MVP work. |

## Approved Story Migration

| Prior identity | Current identity or disposition |
| --- | --- |
| 1.3 | 1.3 persisted store/policy; 1.4 deterministic fake; 1.5 protected cursor codec |
| 1.6 | 1.8 Sample adoption; 1.9 Tenants query/read-model; 1.10 Tenants projection/consumer; 1.11 guardrails |
| 1.4-1.15 | 1.6-1.7 and 1.12-1.20 per the 2026-07-15 crosswalk |
| 1.20 deployed path | 1.20 remains completed source/package closure; 3.13 owns rejected `v3.94.1` disposition; 3.14 owns a separately authorized corrective release; 3.15 owns independent positive deployed-runtime closure |
| 2.4 | 2.4 contracts/routes; 2.5 external host; 2.6 deterministic UI/UX presentation; 2.7 compatibility/package mode |
| 2.5-2.8 | 2.8-2.11, with 2.11 exclusively owning production provenance |
| 3.7 | 3.7 caller migration; 3.8 reference/validation safety; 3.9 supply-chain backlog |
| 3.8 | 3.10 generated API smoke preflight |
| 4.8 executable story | 4.8 evidence ledger; 4.9 trusted admission; 4.10 digest directory; 4.11 state/fence; 4.12 expiry/tombstone; 4.13 legacy migration; 4.14 multi-host evidence; 4.15 closure/handoff |
| 5.6 | 5.6 AppHost loading; 5.7 production DAPR parity; 5.8 drift tests; 5.9 operator docs |
| New | 5.10 reserved-system-tenant provisioning guard |
| 7.2 | 7.2 claims; 7.3 audit; 7.4 deferred operations; 7.5 typed client |
| 7.3 | 7.6 secret store; 7.7 readiness/app health; 7.8 resiliency; 7.9 immutable images |
| 7.4 | 7.10 integration CI; 7.11 persisted evidence; 7.12 test reclassification; 7.13 advisory/performance workflow |
| 7.5 | 7.15 GDPR; 7.16 Admin OIDC; 7.17 aggregate test kit; 7.18 REST generator hardening |
| 7.14 umbrella | 7.14 shell/routes; 7.19 typed-client/evidence states; 7.20 accessibility/localization/responsive conformance |
| 8.2 umbrella | 8.2 contracts/goldens; 8.3 core crypto; 8.4 compatibility readers; 8.5 policy/key lifecycle; 8.6 Azure adapter; 8.7 server integration; 8.8 packages/release; 8.9 Parties parity; 8.10 rollback; 8.11 G5 closure |

## Minimum Readiness-Gate NFR Coverage

This table mirrors the PRD section 11.2 minimum gate set. The comprehensive registry in
`requirements-traceability.md` may name additional supporting stories.

| NFR | Primary story coverage |
| --- | --- |
| NFR1 | 5.2, 5.3, 5.5, 7.2, 7.3 |
| NFR2 | 2.5, 5.2, 5.5, 5.6, 5.10 |
| NFR3 | 5.3 |
| NFR4 | 5.3, 7.6 |
| NFR6 | 1.13, 7.1 |
| NFR7 | 4.1, 4.2, 4.4, 4.5, 4.9-4.15, 5.1 |
| NFR8 | 1.16, 1.19, 6.2-6.4 |
| NFR9 | 3.5, 3.8, 3.11-3.14 |
| NFR10 | 3.1, 3.11, 7.10 |
| NFR11 | 3.6, 3.12, 3.14, 8.8 |
| NFR14 | 2.3, 2.5, 2.6, 7.14, 7.19 |
| NFR15 | 7.3, 7.4, 7.19 |
| NFR16 | 1.9-1.15, 3.11-3.15, 4.9-4.15, 7.10, 8.2-8.11 |
| NFR17 | 3.12, 3.14, 5.6, 7.6-7.9 |
| NFR19 | 8.1-8.11 |

## Invalid Evidence

- Do not optimize for fewer stories if that preserves unreviewable multi-concern stories.
- Do not count API smoke responses as integration evidence where persisted state-store, read-model, or CloudEvent evidence is required.
- Do not satisfy UI readiness by documenting intent only; UI stories still need component/governance evidence in `ux.md` and tests.
- Do not grant `done` to a split child from parent status alone; require the dated evidence crosswalk.
- Do not compare the consuming repository commit to the approved EventStore runtime SHA.
- Do not treat Story 3.13 completion, a Story 3.14 release authority, a mutable tag, workflow success, smoke success, or free-form approval as positive parity, deployment, or consumer-removal authority.
- Do not accept a parity or removal receipt after any bound release, evidence, catalog, role-registry, consumer, mode-matrix, or removal-subject digest changes.
- Do not count a manifest scan, local substitute, or mocked secret store as real-OpenBao production evidence.
- Do not conflate AD-24 operational secrets with AD-23 or the payload-protection KEK backend.
- Do not count Epic 6 specification enablers as runtime implementation progress.
