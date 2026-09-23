# Architecture Update Review — Rubric Walker — 2026-09-23

**Verdict: PASS for the requested architecture reconciliation, with AD-26 owner ratification and implementation readiness still blocked.** The current spine fixes the Tenants/generated-host JWT divergence and expresses all five PRD publication states without granting release or production authority from candidate or parity evidence. No critical, high, or medium defect was found in this slice.

## Review boundary and method

| Item | Observed result |
| --- | --- |
| Subject | `_bmad-output/planning-artifacts/architecture.md`, `status: draft`, `updated: 2026-09-23`; snapshot SHA-256 `ba513f4d5e19e292b7b061a4e70235fde88316f8424588387f7952baea68aad8` |
| Primary inputs | Approved `sprint-change-proposal-2026-09-23.md` §4.B; `prd.md` glossary, FR36-C2–C5, NFR3, G-AUTH-HOSTS, G-PUBLICATION-AUTH, OR14, OR23, OR29 |
| Prior reviews | `validate-report-2026-09-09.md`, `reconcile-update-2026-09-09.md`, September 9 rubric/adversarial reviews and their closure reviews |
| Deterministic check | `uv run .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-eventstore-2026-07-05` → `ok: true`, `total_findings: 0` |
| Scope | Good-spine checklist and current AD-10/AD-11/AD-22/AD-26 text; old findings disposition. This review does not approve AD-26, a publication state, or readiness. |

I compared the exact current spine rules with the proposal and PRD, checked the Tenants and generated-host seams in the working tree, and followed older findings into the current ADs, seed, stack, and production-gate table. The review is of the architecture contract, not a claim that the implementation passes its gates.

## Requested reconciliation

| Boundary | Result | Evidence and residual |
| --- | --- | --- |
| AD-10 shared JWT | **Resolved as architecture.** ServiceDefaults owns the versioned validation contract and host/config fingerprint inventory. EventStore, Admin Server Host, Admin UI, Sample API, Sample Blazor UI, both Tenants hosts, generated REST fixtures, and every future externally reachable or JWT-binding application host must register and pass positive/negative conformance. A runnable generated host is required for proof; a UI host without protected REST controllers still counts, and missing inbound auth fails. Missing hosts and local validators fail the gate. AD-28 remains the separate DAPR app-channel scheme. | `architecture.md` AD-10 and its Shared JWT host-conformance gate. `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs` still configures `AddJwtBearer` locally, so adoption/evidence remains open as PRD G-AUTH-HOSTS says. |
| AD-11 publication lifecycle | **Resolved as architecture.** The unchanged subject moves only `built` → `evidence-candidate-published` → `evidence-validated` → `release-available` → `production-promoted`; predecessor digest, subject, issuer/role, evidence/receipts, validity, and revocation are bound. Story 3.14 authorizes a candidate only; Story 3.15 can establish evidence validation only. The separate release and deployment owner records and validator fail closed. A validated OCI index is necessary artifact identity, not sufficient production authority. | `architecture.md` AD-11; `prd.md` Publication Authority Record and FR36-C3. The authority record and validator are absent, as both documents state. |
| AD-26 production profile | **Resolved as a proposed contract, pending owner decision.** The sole declared canonical profile path and its validator-computed SHA-256 prevent an authority record from inventing or omitting profiles. Candidate publication and evidence validation cannot grant promotion. | `architecture.md` AD-26 remains `[ASSUMPTION]` and `status: draft`; `deploy/dapr/production-profile.yaml` is absent. Architecture and Platform deployment owners must ratify or approve a replacement before this decision is adopted. |
| AD-22 consumer removal | **Aligned.** It now requires the same subject's source/package, runtime, release, and promotion results plus a deterministic complete consumer manifest that passes `tools/validate-consumer-removal-authority.py`, and a per-consumer authenticated receipt. | `architecture.md` AD-22; `prd.md` FR36-C4/C5 and G-CONSUMER. No consumer-removal authority is inferred from the architecture wording. |

## Findings

No new architecture defect was found in the requested slice. During review, AD-11 and AD-26 gained the missing FR36 `Binds` entries, and the Stack was refreshed against `global.json` and the root-declared Builds catalog. Its DAPR availability note now cites v1.18.4, matching the [official DAPR release](https://github.com/dapr/dapr/releases/tag/v1.18.4). Its .NET 10.0.12 / SDK 10.0.401 repository pins match the [official .NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). [DAPR's PostgreSQL component documentation](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/) still says v1 is available and not deprecated, and v2 cannot read/write the same tables or migrate from v1. These checks do not authorize another pin change or production deployment.

### External blocker — PRD's exact publication subject is stale

The PRD glossary and G-PUBLICATION-AUTH still name `aafe9040…` as the *current* Story 3.15 subject and prescribe an authority path under that digest. The approved proposal records current `7d64f87e…` with a technically passing 3/3 receipt packet and explicitly defers replacing the old exact-subject publication path until the Release owner defines and approves a new record/validator. The spine correctly uses subject-relative rules and claims no later authority. **Disposition: refer to PRD/Release owners in the ordered handoff.** Do not copy receipts across subjects or treat the 3/15 technical pass as release approval.

## Outstanding-review disposition

The September 9 validation report's two critical and eleven high findings have architecture dispositions in the current spine: C1 → AD-26 and its production prohibition; C2 → AD-27; H1 → AD-28; H2 → AD-16; H3 → AD-29; H4 → AD-24/25/33; H5 → AD-32; H6 → AD-7/30; H7 → AD-15; H8 → AD-8/31 and current/target topology; H9 → AD-19's shipped carrier names; H10 → the dated Stack and deferred tested refresh; H11 → AD-5/25's mandatory admission and fence. The remaining production broker, physical append fence, secrets, Operations wiring, restore, route catalog, and environment proof are explicitly blocked in the final production-gate table rather than silently decided. Those are implementation/proof gaps, not closure evidence.

The subsequent `reconcile-update-2026-09-09.md` R1–R8 are now reflected in AD-16, AD-17, AD-23/24, the Gateway seed row, the NFR18 deferral, AD-8/21, and corrected decision bindings. R9 remains deliberately open: AD-26 is `[ASSUMPTION]`. The September 9 rubric and adversarial closure reports record their focused fixes, but neither report is an owner ratification. Their historical verdicts and older line anchors should be retained as reviews of their stated snapshots.

## Good-spine checklist and handoff

The updated rules name the cross-unit choices that could otherwise diverge: one JWT contract/inventory, one immutable publication subject and ordered state chain, one canonical production-profile inventory slot, and one owner-bound consumer-removal authority. Fail-closed behavior is explicit where artifacts or owner decisions are absent. The brownfield split is honest: existing EventStore/Admin/Sample seams coexist with a Tenants hand-rolled JWT path and no all-host proof. The operational envelope is either selected provisionally in AD-26 or gated with named owners and triggers. The DAPR runtime pin, broker, restore posture, provider append fence, and deployment validation remain undecided without allowing production claims.

Keep `architecture.md` draft and AD-26 `[ASSUMPTION]`. The Architecture and Platform deployment owners must review the exact self-managed Kubernetes / `state.postgresql` v1 / `oq8-postgresql-v1` proposal, evidence, dissent, profile identity, and Security/Release/Test dispositions; then explicitly ratify it or approve a replacement. Even after ratification, the absent canonical profile, authority record/validator, Tenants/generated-host conformance, and PRD §11.4 gates still block readiness, release, deployment, migration, and consumer removal. Do not repin epics, regenerate the tracker, or reuse a historical `READY` verdict from this review.
