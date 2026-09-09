# Architecture Spine Update Review — Rubric Walker — 2026-09-09

**Gate verdict: CHANGES REQUIRED.** The update closes the previous validation report's two critical safety gaps and is mechanically sound, but five high-severity rules are still too ambiguous or mutually blocking to be dependable build substrate. No current finding is critical because AD-26 and the Deferred table explicitly prohibit production use while the profile remains incomplete.

## Review boundary

| Item | Result |
| --- | --- |
| Subject | `ARCHITECTURE-SPINE.md`, `status: draft`, updated 2026-09-09 |
| Lens | BMad architecture good-spine rubric walker |
| Deterministic lint | Pass; 0 findings |
| Critical | 0 |
| High | 5 |
| Medium | 4 |
| Low | 2 |

The review judged whether the spine fixes the real divergence points for its downstream epics, whether each rule is enforceable and prevents its stated divergence, whether deferrals fail closed, whether named technology is current, whether the update ratifies the brownfield codebase, whether the bound PRD/spec capabilities are covered, and whether the operational/environmental envelope is complete.

## Critical findings

None. The explicit production prohibition at `ARCHITECTURE-SPINE.md:219-223,375-390` contains the remaining provider, append-race, secrets, operations, and recovery gaps. That prohibition must remain intact while the high findings below are corrected.

## High findings

### H1 — AD-26 creates a release-before-proof cycle

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:123-129,219-223,375-386`
- **Evidence:** AD-11 defines an immutable released OCI index as the identity that deployment validation consumes. AD-26 then says both “Release or deployment is prohibited” until production-path proof is complete. The approved sequencing is the reverse: Story 3.14 produces the separately authorized corrective release candidate, and Story 3.15 validates that candidate as a deployed runtime (`sprint-change-proposal-2026-08-16.md:253-312`; `prd.md:149,299,513`).
- **Impact:** One team can interpret “release” as any candidate publication and be unable to create the immutable artifact required for deployment proof, while another can interpret it as production promotion and proceed. The rule therefore does not create one executable gate.
- **Fix:** Preserve a separately authorized, non-authorizing candidate publication path under AD-11. Prohibit production promotion, production traffic, consumer migration, and readiness claims until AD-26 proof passes. Name which evidence state changes a candidate into an approved production identity.

### H2 — The production profile is safe but still not an implementable identity

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:219-223,287-298,375-386`
- **Evidence:** AD-26 selects self-managed Kubernetes and PostgreSQL v1 but leaves the durable broker open, supplies no exact DAPR runtime pin, names no canonical overlay/manifest path, and gives no content-digest schema or validator owner for the phrase “full content-addressed overlay.” The Stack records three different runtime facts—CI sidecar `1.18.2`, deployment examples `1.18.0`, and available DAPR `1.18.3`—and says only that a future profile requires one tested pin. The Deferred table owns the broker but not the missing runtime/profile identity.
- **Impact:** Downstream deployment and test units cannot build the same profile or determine mechanically whether their manifests belong to it. The prohibition prevents unsafe production use, but it does not yet resolve the original runtime/provider divergence for implementation.
- **Fix:** Define a versioned production-profile artifact and owner. Its digest must bind the exact DAPR runtime image/CLI compatibility, Kubernetes/sidecar mode, PostgreSQL component version/configuration, broker/component, app IDs, scopes/ACLs, resiliency, OpenBao contract, route/idempotency catalog digests, and required evidence. Add the runtime/profile selection to Deferred with owner and trigger until that artifact exists.

### H3 — AD-22's owner approval is not an enforceable authorization contract

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:191-195`
- **Evidence:** The rule requires that “its owner approves evidence,” but does not define the consumer subject, applicable source/package/deployed modes, immutable approval identity, expiry, or invalidation behavior. FR36 requires an owner-reviewed parity packet and exact runtime SHA (`prd.md:287-301`), while the pre-update adopted AD-22 additionally rejected free-form/boolean/self-declared approval and bound an authenticated Consumer-owner receipt to the consumer repository/commit and removal subject (`git show HEAD:_bmad-output/planning-artifacts/architecture.md`, AD-22).
- **Impact:** Two consumers can accept different evidence and both claim “owner approval”; a stale or unverifiable approval could authorize deletion after the consumer or EventStore identity changes. The shortened rule weakens an adopted cross-repository safety boundary.
- **Fix:** Restore the stable minimum authorization contract: content-bound parity packet; explicit applicable-mode matrix; authenticated Consumer-owner identity; consumer repository and commit; exact removal-subject digest; explicit `consumer-removal-authorized` outcome; validity; and automatic invalidation on any bound change. Keep story-specific history out of the spine.

### H4 — AD-24 no longer establishes one secret-contract authority

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:203-207,219-223,379-385`
- **Evidence:** AD-24 selects the provider and says access is default-deny, but it no longer names one owner/composer for the singleton component, per-app DAPR configurations, logical secret inventory, scopes, OpenBao policies, lifecycle classes, generation/cache bounds, or rotation unit. The pre-update adopted AD-24 assigned that authority to the platform deployment overlay and `deploy/dapr/openbao-secret-contract.yaml`; the current Deferred row defers environment values and HA details, not contract ownership.
- **Impact:** State-store, pub/sub, host, and deployment units can author incompatible logical names, map keys, grants, caching, and rotation behavior while each follows the provider-level rule. This is exactly the mixed-custody divergence AD-24 says it prevents.
- **Fix:** Restore one value-free secret-contract artifact and owner. Require all `Component/openbao`, DAPR `Configuration`, allowed-secret scopes, OpenBao policies, consumer/lifecycle declarations, generation/cache/rotation bounds, and deployment validation to derive from it. Environment-specific addresses and credentials may remain deferred.

### H5 — AD-25 makes the approved expiry and migration contracts non-testable

- **Severity:** High
- **Location:** `ARCHITECTURE-SPINE.md:209-217` and Design Paradigm `:42-46`
- **Evidence:** “A minimized expired tombstone” does not define its allowlist or atomic replacement behavior, and “the same single-authority rule” does not define the legacy source/target/redirect transition. Those exact choices were present in the previously adopted AD-25 and are load-bearing for FR27's requirement that expired-key reuse fail identically before intent comparison and that unsafe legacy evidence never become a miss (`prd.md:242-246`; `git show HEAD:_bmad-output/planning-artifacts/architecture.md`, AD-25). The external OQ8 source is identified, but the rule does not explicitly state that its exact tombstone and state-transition protocol is normative for implementations.
- **Impact:** Independent actor, migration, and test slices can retain different tombstone fields or flip authority at different phases, causing information leakage, key resurrection, or dual execution while still satisfying the prose summary.
- **Fix:** Either restore the exact stable tombstone allowlist, atomic expiry replacement, indistinguishable expired outcome, and prepare/copy/redirect/flip authority transitions, or explicitly make those exact clauses of the content-bound OQ8 design normative and require conformance vectors before implementation. Do not depend on the word “minimized.”

## Medium findings

### M1 — AD-28 authenticates the app channel but its title overclaims caller identity

- **Severity:** Medium
- **Location:** `ARCHITECTURE-SPINE.md:231-235`
- **Evidence:** `APP_API_TOKEN`/`dapr-api-token` proves possession of the application-channel token. The rule itself correctly says caller app ID, mTLS, and ACLs constrain and attribute calls; the token does not by itself cryptographically bind the specific remote application identity named by `dapr-caller-app-id`.
- **Impact:** An implementer could treat the shared app-channel token as sufficient authorization for a claimed caller app ID, reintroducing the trust problem named in `Prevents`.
- **Fix:** Rename the decision to “DAPR App Endpoints Authenticate The App Channel” and state that operation authorization requires the authenticated channel plus sidecar-established caller identity and catalog/ACL authorization; never describe the token alone as caller identity.

### M2 — AD-33 lacks a named catalog authority and override precedence

- **Severity:** Medium
- **Location:** `ARCHITECTURE-SPINE.md:261-265`
- **Evidence:** The rule requires “one versioned platform catalog” but does not name its owning package/artifact, load authority, lifecycle, or precedence when an explicit fallback or host override exists. It says unsupported overrides fail without defining supported ones.
- **Impact:** Gateway, domain, projection, AppHost, and deployment work can each generate a same-shaped catalog from different sources or assign different fallback precedence, then compare only internally consistent digests.
- **Fix:** Name the source-of-truth artifact and owner; define exact-key-over-fallback precedence, whether overrides are forbidden or catalog-declared, and require every consumer to compare against the same signed/content-bound digest.

### M3 — The context diagram sends query/read-only flows through command admission

- **Severity:** Medium
- **Location:** `ARCHITECTURE-SPINE.md:48-59`
- **Evidence:** The only client path is `Edge --> Catalog --> Admission --> Aggregate`; the diagram has no query route from the edge to read models/domain query handlers. Its client node includes Admin and UI, whose read flows are explicitly recognized elsewhere (`AD-3`, `AD-14`, Structural Seed).
- **Impact:** At feature altitude this diagram can seed an implementation that couples reads and support-safe Admin queries to command admission, or conclude that queries must traverse the aggregate actor.
- **Fix:** Split the diagram at the route catalog: command routes go through admission/aggregate; query routes go to projection-backed read models or domain query handlers with AD-14 metadata. Show Admin mutations separately if retained.

### M4 — Source provenance was pruned below the decisions it still carries

- **Severity:** Medium
- **Location:** frontmatter `ARCHITECTURE-SPINE.md:12-34`; AD-11, AD-22, and AD-24
- **Evidence:** The update removes the approved 2026-07-19 OpenBao proposal, the 2026-07-15/16/17 architecture/release proposals, and the 2026-08-14 proposal from `sources`, although the corresponding adopted decisions remain and the update actively compresses them. The added latest validation report is a critique, not the original decision authority.
- **Impact:** A future reviewer cannot distinguish which compressed clauses remain approved invariants from which are new summaries, making further updates prone to accidental weakening.
- **Fix:** Retain direct, load-bearing decision sources (or one durable index that resolves them) and remove only sources proven redundant. The source list need not preserve every historical report, but every adopted security/release/parity rule needs traceable authority.

## Low findings

### L1 — Current DAPR evidence is not directly cited

- **Severity:** Low
- **Location:** frontmatter `ARCHITECTURE-SPINE.md:25-33`; Stack `:287-298`
- **Evidence:** The Stack claims DAPR `1.18.3` exists, but the source list includes component/security documentation and not the cited runtime release/tag. The .NET currentness claim does have its release metadata source.
- **Impact:** The named-current check is not reproducible from the spine alone.
- **Fix:** Add the official DAPR `v1.18.3` release URL or avoid the uncited availability claim.

### L2 — AD-23's package ownership is less exact than the approved capability

- **Severity:** Low
- **Location:** `ARCHITECTURE-SPINE.md:197-201`; Structural Seed `:304-335`
- **Evidence:** The rule says EventStore owns the engine but no longer names the package/boundary, and the Structural Seed has no prospective payload-protection package. FR37 requires a reusable engine package, while the approved specification can place backend adapters separately.
- **Impact:** Implementers may place the engine into `Server`, a consuming domain, or multiple packages and still read the decision as satisfied.
- **Fix:** Name the stable engine contract/package boundary and show the prospective package in the seed, while leaving provider-specific adapter placement to the approved spec.

## Checklist disposition

| Good-spine criterion | Result |
| --- | --- |
| Real downstream divergence points fixed | Partial — tenant, correlation, projection carrier, Operations, and fail-closed production posture are materially improved; H2-H5 remain |
| Every rule enforceable and prevents its divergence | Fail — H1, H3-H5; M1-M2 |
| Deferred cannot enable divergent production units | Pass with qualification — production is prohibited, but H2 must make the profile buildable before the prohibition is lifted |
| Named technology verified-current | Pass with low provenance gap L1 |
| Brownfield ratification | Pass with qualification — current vs target topology and Operations status are now honest; many adopted rules remain implementation gates rather than delivered state |
| Bound PRD/spec capabilities covered | Pass with qualification — H3/H5 and L2 need exact contract preservation |
| Prior adopted decisions not weakened | Fail — AD-22, AD-24, and AD-25 lost load-bearing authorization/ownership/protocol detail |
| Altitude-owned dimensions decided/deferred/open | Pass — deployment, providers, operations, recovery, telemetry, scale, security, and release are all represented |

## What passed

- Deterministic lint passes with all 33 stable AD IDs and required `Binds`/`Prevents`/`Rule` fields.
- AD-26 and the Deferred table convert the prior undefined-production critical into a fail-closed implementation gate.
- AD-27 fixes the prior tenant canonicalization critical with one owner, grammar, normalization rule, and omission/conflict behavior.
- AD-16, AD-17, AD-19, AD-28 through AD-32, the corrected Stack, and current-versus-target topology directly address the major findings in the 2026-09-09 validation report.
- The architecture now explicitly covers operational/environmental dimensions instead of silently omitting them.

## Recommended gate action

Apply H1-H5 before setting `status: final`. M1-M3 are clear local corrections and should be applied in the same pass. M4, L1, and L2 can be fixed without widening scope. Re-run deterministic lint and the configured independent reviewers after the amendments.
