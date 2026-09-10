# Platform / API-Contract Adversarial Review

## Verdict

**Reject as a current chain-top implementation or consumer-handoff baseline.** The PRD is admirably explicit that readiness is blocked, but that warning does not cure several concrete contract conflicts. The tracked REST generator violates the architecture's tenant and command-status identity rules while the corresponding stories are recorded `done`; the public compatibility policy omits most of the platform surface; the authentication contract does not cover every external API host; and consumer-removal authority exists only as a stronger architecture rule, not as a PRD requirement. The known append-loss and unreproducible OQ8 authority gaps remain genuine release blockers. Treat this document as a requirements draft with a conservative `Reject`, not as executable authority for release, deployment, API compatibility, or consumer infrastructure removal.

## Critical

### C1. The generated public API contradicts the canonical tenant boundary

- **Location:** PRD §5 tenant-isolation concern, NFR2 (§7), §8.2; architecture AD-10 and AD-27; `epics.md` Stories 2.2/2.5 and `sprint-status.yaml` lines 103/106.
- **Evidence / note:** NFR2 says tenant isolation includes generated REST APIs, and AD-27 requires exactly one explicit tenant, lowercase canonicalization under the `AggregateIdentity` grammar, rejection of invalid/conflicting tenant values, and rejection of `system` at every public boundary. The shipped generator instead returns literal `"system"` for `RestTenantSource.System`; route mode only converts whitespace to null and otherwise forwards the original spelling; claims mode returns the sole raw claim. It neither canonicalizes nor enforces the grammar/reserved-name rule (`RestApiControllerEmitter.cs` lines 407-445). Generator tests intentionally compile `RestTenantSource.System`, including an `api/tenants` surface. Stories 2.2 and 2.5 are nevertheless `done`, and their acceptance criteria never bind AD-27. This is not merely missing evidence: the reusable public adapter currently emits behavior forbidden by the architecture.
- **Fix:** Promote AD-27's exact public-boundary consequences into NFR2/FR12 (or stable sub-requirements), assign a primary story for the generator and every host, and add compiled-controller negative tests for uppercase, malformed, duplicate/conflicting, unauthorized, and reserved-`system` tenants. Do not call the external REST surface tenant-safe until the emitted code uses the shared canonicalizer and those tests pass.

### C2. A completed public `Location` contract can still select `CorrelationId` as command identity

- **Location:** PRD FR12, FR27, §8.2; architecture AD-17; `epics.md` Story 2.9; `sprint-status.yaml` line 110.
- **Evidence / note:** AD-17 states that `MessageId` is the sole status identity and `CorrelationId` never selects a command record. Story 2.9 says the tracking field must be read without assuming correlation equals message identity and is recorded `done`. The generator emits `string __hexalithStatusKey = __hexalithResponse.MessageId ?? __hexalithResponse.CorrelationId;` (`RestApiControllerEmitter.cs` line 281), and `RestApiControllerGenerationTests.cs` line 65 pins that fallback. When a response lacks `MessageId`, the generated external API may advertise a status URI for diagnostic correlation metadata, directly violating the adopted public contract.
- **Fix:** Make FR12 explicitly require `MessageId` as the only status key and fail-closed omission when it is absent or invalid. Remove the correlation fallback, add runtime tests for missing/invalid `MessageId`, and reopen/reconcile Story 2.9 rather than preserving `done` against contradictory shipped bytes.

### C3. Phase 4 still has a reproduced silent append-loss class with no delivered guard

- **Location:** PRD NFR7, §9 safety boundary, SM11, OR4; architecture AD-5 and production-gates table; `epics.md` Story 4.5.
- **Evidence / note:** Story 4.5 records `same-key-overwrite-raw-durable-write-lost`. The PRD correctly admits that the OQ8 fence is not provider-level append fencing, that class (c) is undelivered, and that fencing remains outside MVP implementation scope. This is therefore a truthful blocker rather than a hidden defect, but it is still Critical: a product whose defining promise is durable event storage cannot authorize MVP completion, release, or deployment while a supported writer race can silently overwrite durable state. The epic text also says the implementation gap is still unowned (`DW-326`).
- **Fix:** Create and approve an owned implementation/evidence slice for provider-portable write-once/append fencing, or define and mechanically enforce a supported operating envelope in which the second writer cannot exist. Retain the global block until production-path evidence proves loss prevention; risk-acceptance prose alone must never pass the gate.

### C4. Durable-admission semantics still depend on normative bytes EventStore cannot reproduce

- **Location:** PRD §1.1, FR27, NFR7, NFR16, OR11; architecture AD-25.
- **Evidence / note:** The PRD delegates descriptor fields, state-machine outcomes, timers, tombstones, failure handling, and evidence denominators to a Hexalith.Folders document, while explicitly admitting the bytes are absent from this repository. A digest without the governed bytes cannot let an EventStore developer, reviewer, CI job, or consumer reconstruct the contract. The local pre-review gate is also currently red: `python3 tools/validate-oq8-platform-evidence.py --pre-review` exits 1 with `Reviewed public document body drift: docs/guides/configuration-reference.md`. Thus neither the normative contract nor the current evidence closure is reproducible from the claimed baseline.
- **Fix:** Retain a permitted immutable copy, a complete approved normative projection, or a signed/content-addressed attestation whose bytes are available to validation. Bind that identity through PRD, architecture, epics, packet, and CI, repair/reseal the drifted OQ8 subject through its governed review path, and keep Story 4.15/release/consumer handoff closed until the gate passes.

## High

### H1. The backward-compatibility requirement excludes most of the public platform

- **Location:** PRD NFR12, FR2-FR7, FR11-FR16, §5 public-contract concern.
- **Evidence / note:** NFR12's closed protected set covers only the SignalR signal-only notification, existing generic gateway APIs, and named payload-protection read formats. It does not protect the domain-service SDK methods and mapped endpoints, REST attributes and generated routes, Problem Details/error codes, query metadata headers, event envelopes, serialization contracts, DAPR wire methods, or most public NuGet abstractions. The repository has scattered compatibility tests, but no PRD-level versioning/deprecation/breaking-change policy or release gate for the complete public surface. A developer platform can therefore satisfy this PRD while silently breaking its principal consumers.
- **Fix:** Inventory public source/binary/wire/HTTP contracts, classify each compatibility promise, define additive/deprecation/removal and SemVer rules, and require API/wire baselines plus representative package-only consumer tests before release.

### H2. The signing-key contract leaves the Tenants external API and future generated hosts outside NFR3

- **Location:** PRD NFR1-NFR3 and §3 target users; architecture AD-10 JWT contract; `epics.md` Story 2.5 and NFR3 coverage.
- **Evidence / note:** NFR3 names only the EventStore gateway, Admin Server Host, and Sample API. The architecture says the shared JWT contract also governs every future JWT-binding host, but that obligation is absent from the PRD. The dedicated Tenants API is an explicit product surface; Story 2.5 is `done` under older acceptance that checks issuer/audience/HTTPS/minimum symmetric-key length, not NFR3's production symmetric-mode ban, explicit algorithm allowlist, 60-second skew, role validation, and tenant validation. The NFR3 traceability row does not include Story 2.5. This permits authentication posture to vary by generated host.
- **Fix:** Define NFR3 by capability (`every externally reachable or JWT-binding host`) rather than a closed host list, require the shared contract/fingerprint, and add Tenants plus a generated-host conformance fixture to primary coverage and release evidence.

### H3. The PRD's consumer-removal gate is weaker than the architecture's actual safety rule

- **Location:** PRD FR36, §6.8, glossary Owner Roles; architecture AD-22; `epics.md` Story 3.15.
- **Evidence / note:** FR36 requires an EventStore owner-reviewed packet and an exact EventStore SHA match. AD-22 additionally requires a consumer-specific repository/commit, applicable-mode matrix, exact removal-subject digest, authenticated Consumer-owner receipt, validity, and invalidation on bound change. The PRD's Owner Roles does not even define Consumer owner. Because product requirements are supposed to own intent while architecture owns mechanism, this material authorization policy is stranded in the lower-level artifact and can disappear from a future architecture revision without violating FR36.
- **Fix:** Amend FR36 with stable consumer-specific authorization outcomes and invalidation conditions, define the Consumer-owner role, and separate EventStore capability availability, release/deployment authority, and each consumer's removal authorization as independently failing gates.

### H4. The dated reject result no longer describes one coherent current repository baseline

- **Location:** PRD frontmatter and §0, SM2, OR14; `epics.md` frontmatter; current tracked repository.
- **Evidence / note:** The PRD binds its readiness result to commit `1b6f08d4...`, while current `HEAD` is `293c69c4...` and includes substantial authentication, planning, evidence-validator, and documentation changes. Meanwhile the `epics.md` recorded PRD/architecture digests now exactly equal the current files (`b99eff...` and `7e3dbc...`), contradicting OR14/SM2's assertion that those input digests are still stale. The current OQ8 pre-review failure is public-document drift, not the lifecycle-only failure recorded in the prior review. The posture remains conservatively blocked, but the stated reasons and provenance are not a truthful single snapshot.
- **Fix:** Re-run source reconciliation against an explicit clean commit, record current digests and validation outputs as one atomic baseline, retire or rewrite blockers that repository truth has superseded, and distinguish stable product requirements from generated/current execution status.

## Medium

### M1. Ownership traceability is not delivery traceability

- **Location:** PRD §11.1-§11.2, §10 metrics, OR7/OR13/OR17.
- **Evidence / note:** The PRD correctly says its tables report declarations rather than delivery, but it still provides no authoritative clause-level result for most FRs/NFRs. Tracker `done`, story wrapper status, implementation evidence, and actual public behavior can disagree—as the tenant and `Location` conflicts demonstrate. A downstream developer cannot determine which contract is safe to consume from the chain-top artifact.
- **Fix:** Add one generated exit ledger keyed by stable requirement/sub-requirement ID, with current result, exact evidence identity, evaluator, waiver policy, and invalidation trigger. Keep volatile rows out of the durable narrative, but make the current ledger part of readiness input.

### M2. Shared-workflow governance mixes mutable and immutable authority

- **Location:** PRD FR25, NFR9, §8.1, OR18; architecture AD-11/AD-22; `.github/workflows`.
- **Evidence / note:** FR25 requires shared Hexalith.Builds gates through mutable `@main`, while §8.1 and the architecture rely on a SHA-pinned shared publisher/validator for reproducible release authority. The release caller is pinned, but CI, CodeQL, commitlint, dependency-review, and initialization still use `@main`. OR18 admits the exact enforcing workflow identity is not bound. The PRD does not state which mutable uses may affect an authorizing result and which are advisory.
- **Fix:** Classify every shared workflow/action as authorizing or advisory, pin every authorizing dependency by immutable identity, bind that identity into evidence, and explicitly constrain where `@main` is acceptable.

## Low

None.
