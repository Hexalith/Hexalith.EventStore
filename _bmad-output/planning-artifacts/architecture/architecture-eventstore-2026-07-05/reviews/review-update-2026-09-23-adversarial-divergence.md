# Architecture Update — Adversarial Divergence Review

**Date:** 2026-09-23
**Subject:** `_bmad-output/planning-artifacts/architecture.md`; reviewed through SHA-256 `ba513f4d5e19e292b7b061a4e70235fde88316f8424588387f7952baea68aad8`
**Inputs:** approved `sprint-change-proposal-2026-09-23.md` §4.B and §5; `prd.md` §§1.2, 7.1, 11.3–11.4, 12; prior architecture reviews through 2026-09-09.
**Verdict:** **PASS for the focused design reconciliation; owner ratification and delivery gates remain blocked.** The requested AD-10 host names, all five publication states, and canonical profile boundary are present. Two high divergence cases and two medium authority-record gaps found during the update were closed in the saved spine before this report was finalized. AD-26 remains deliberately unratified and cannot supply production authority.

## High findings closed during this update

### H1 — A future externally reachable host can escape the AD-10 JWT inventory

**Earlier wording:** AD-10's JWT paragraph listed the known EventStore, Admin, Sample, Tenants, and generated REST fixtures, then extended the contract only to “every future JWT-binding host” (`architecture.md:154`).

**Required boundary:** PRD NFR3 says **every externally reachable or JWT-binding host** must consume the shared versioned JWT contract, expose its fingerprint, and pass the safety suite (`prd.md:349`). G-AUTH-HOSTS uses the same disjunctive universe (`:643`). The approved proposal §4.B.1 asks for a versioned host/fingerprint inventory and fail-closed gate.

**Divergence pair:** Unit A adds a new externally reachable host with a locally selected authentication mechanism and never registers the platform JWT capability. Under the current AD-10 inventory wording it is neither named nor JWT-binding, so its missing contract fingerprint does not fail the host gate. Unit B implements G-AUTH-HOSTS from the PRD and rejects the same release because the public host is absent. The base AD-10 rule requiring endpoint authentication does not close the NFR3 contract/fingerprint gap.

**Disposition — closed in current spine:** AD-10 now states every future **externally reachable or JWT-binding** host belongs to the contract and fingerprint inventory (`architecture.md:154`). Its missing-host gate fails closed. AD-28's DAPR app-channel token remains separate. This closes design wording, not Tenants or generated-host implementation proof.

### H2 — AD-22 can be read as granting consumer removal from parity and one owner receipt alone

**Earlier wording:** AD-22 said a consumer may remove infrastructure “only when supported by a content-bound parity packet,” then enumerated its packet and Consumer-owner receipt (`architecture.md:240-244`). AD-26 prohibited migration while record, profile, proof, or ratification was missing (`:287`), but AD-22 did not name the publication state, complete consumer universe, canonical profile digest, or consumer-manifest validator.

**Required boundary:** PRD FR36-C4/C5 requires deterministic enumeration before changes and a per-consumer receipt bound to the same production-promoted runtime SHA (`prd.md:414-416`). G-CONSUMER requires passing G-RUNTIME-PARITY **and** G-PUBLICATION-AUTH, plus the validated complete consumer-removal manifest, exact canonical profile digest, bounded repository/commit and removal subject, and `PASS`/`N/A` rules (`:644-646`). OR24 explicitly forbids substituting platform or promotion parity for per-consumer authority (`:694`).

**Divergence pair:** Unit A builds a content-bound parity packet and obtains the authenticated Consumer-owner receipt described by AD-22, then removes local projection/query infrastructure. It omits the complete consumer manifest and exact `production-promoted` runtime/profile binding because AD-22 does not require them. Unit B enforces PRD G-CONSUMER and rejects Unit A's removal. AD-26's general prohibition blocks current migration while the profile and records are absent, but does not specify these per-consumer prerequisites once those earlier proofs exist.

**Disposition — closed in current spine:** AD-22 now requires same-subject source/package and deployed-runtime proof, `release-available`, `production-promoted` for the canonical profile, a deterministic full consumer universe, profile and removal-subject binding, and a separate Consumer-owner receipt (`architecture.md:244`). It explicitly requires `tools/validate-consumer-removal-authority.py` to pass over that complete manifest and separately requires owner/validator attestation for `N/A`.

## Medium finding closed during this update

### M1 — AD-11 does not explicitly bind authentication proof bytes for later-state issuers

**Earlier wording:** AD-11 required authenticated release/deployment-owner records and listed issuer and role-registry identity, predecessor digest, evidence, state outcome, and validity/revocation (`architecture.md:170`). It did not say the issuer's **authentication-evidence digest** was a bound field.

**Required boundary:** The PRD's Publication Authority Record requires both authenticated issuer identity **and authentication-evidence digest**, with record bytes content-bound, and rejects unauthenticated issuers (`prd.md:176`).

**Divergence pair:** A record producer stores issuer ID and role-registry ID after an ephemeral authentication check, then signs only the resulting identity field. A validator following the PRD demands a digest of the authentication proof and cannot validate that record. The spine's phrase “authenticated record” does not tell independent producer and validator units which proof bytes must agree.

**Disposition — closed in current spine:** AD-11 now calls for versioned, content-bound records that bind issuer identity **and authentication-evidence digest**, plus role-registry identity (`architecture.md:170`). The downstream schema and validator must still implement the PRD's full issuance, expiry, revocation, and invalidation contract; no record exists today.

## Medium finding closed during this update

### M2 — AD-11 left the two predecessor evidence identities generic

**Earlier wording:** AD-11 bound “source/package/OCI identities,” “evidence and receipt-set digest,” and predecessor-state digest (`architecture.md:170`). The surrounding prose named Story 3.14 authority and Story 3.15's validator/receipts, but the authority-record field list did not explicitly require the Story 3.14 packet identity or Story 3.15 validator identity and result.

**Required boundary:** The PRD's Publication Authority Record requires the Story 3.14 packet and OCI/package identities and the Story 3.15 validator, result, and receipt-set digest to be content-bound (`prd.md:176`). G-PUBLICATION-AUTH also invalidates authority when the Story 3.15 result or receipts change (`:645`).

**Divergence pair:** A release-record producer binds a digest of a generic parity report and three receipts but omits the exact Story 3.14 packet and validator identity/result. A PRD-conforming validator rejects the record because it cannot prove the candidate or validation lineage. Both units may claim to follow AD-11's generic “evidence” language.

**Disposition — closed in current spine:** AD-11 now binds schema/version, subject digest and source SHA, the Story 3.14 packet and package/OCI identities, Story 3.15 validator identity/result and receipt-set digest, predecessor, authenticated issuer and role registry, state-specific outcome, issuance, expiry, revocation, and invalidation (`architecture.md:170`). Separate authenticated release and deployment owner transitions remain required.

## Confirmed closures and retained blockers

| Item | Review result |
| --- | --- |
| Named AD-10 boundary | **Closed in design:** Both Tenants hosts, Admin UI, Sample Blazor UI, and runnable generated REST host fixtures now appear beside EventStore, Admin Server Host, and Sample API. Contract owner, safety rules, fingerprint inventory, negative tests, and AD-28 separation are stated (`architecture.md:154`). Host adoption remains unproven and PRD G-AUTH-HOSTS remains failed. |
| Five publication states | **Closed in design:** AD-11 uses the PRD's exact `built` → `evidence-candidate-published` → `evidence-validated` → `release-available` → `production-promoted` sequence. It separates Story 3.14 candidate authority, Story 3.15 evidence validation, release-owner approval, and deployment-owner promotion, with predecessor and immutable-subject binding (`:170`). The revised release-evidence paragraph makes a validated index digest an artifact identity prerequisite, not sufficient deployment permission (`:168`). |
| Canonical production profile | **Closed in design, pending decision and artifact:** AD-26 names one `deploy/dapr/production-profile.yaml` inventory slot, canonical-byte SHA-256, exclusions for Cosmos and Development/test, and fail-closed absence (`:281-287`). The file and publication-authority validator were absent on review; `[ASSUMPTION]` and `status: draft` remain correct. Architecture and Platform deployment owners must explicitly ratify or replace the Kubernetes/PostgreSQL v1 choice, record evidence and dissent, and identify the exact canonical profile before adoption. This review is not that ratification. |
| PRD current-subject path | **External reconciliation blocker:** PRD glossary and G-RUNTIME-PARITY still bind the obsolete `aafe9040…` subject and 0/3 receipts (`prd.md:176,613,644-645`), while the approved proposal records the later `7d64f87e…` technical pass and requires Release-owner definition of a current-subject authority record/validator (§1 and §4.A.2). AD-11 prudently gives no stale exact path and says the current-subject authority record/validator are absent (`architecture.md:170`). No later lifecycle state is authorized by the proposal or this review. |
| Readiness and history | **Blocked:** PRD frontmatter is `implementation_readiness_status: blocked` / `result: reject`; G-BASELINE, G-AUTH-HOSTS, G-PUBLICATION-AUTH, G-CONSUMER, and G-HIGH-RISK remain failed. Earlier review files and sealed packet observations remain historical evidence. Do not turn this architecture review or a bounded Story 3.15 validator pass into release, deployment, migration, or readiness authority. |

## Gate disposition

No open divergence was found in the requested AD-10, AD-11, AD-22, and AD-26 seams on the reviewed digest. The architecture lint and other configured reviewers still need their own recorded results. Keep `status: draft` and AD-26 `[ASSUMPTION]` pending independent Architecture and Platform deployment owner ratification or approved replacement. The Release owner must resolve the current-subject authority path and validator before PRD G-PUBLICATION-AUTH can be evaluated; the PRD's stale exact path must be corrected as a separate ordered step. Epics digest renewal, sprint tracker regeneration, and readiness claims remain blocked.
