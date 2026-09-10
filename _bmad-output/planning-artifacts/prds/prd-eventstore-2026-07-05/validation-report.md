# Validation Report — eventstore Phase 4 Implementation Readiness Recovery

- **PRD:** `_bmad-output/planning-artifacts/prd.md`
- **Rubric:** `.agents/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Repository baseline:** `293c69c42d35dee26682d42f05c943c0b65786f4`
- **Run at:** 2026-09-10T08:31:03+02:00
- **Grade:** Poor

## Overall verdict

This is a technically substantive and unusually candid requirements catalogue that supports one immediate decision well: keep implementation readiness blocked. It is not safe as the authoritative chain-top baseline it claims to be because downstream usability is broken by an unreconciled artifact set and unavailable normative OQ8 source, while acceptance clarity and document shape remain thin. The explicit `Reject` posture must stay in force until those gaps close.

The platform-contract review materially strengthens that conclusion with current repository evidence: generated REST tenant handling and command-status `Location` behavior contradict adopted public-boundary rules, silent append loss remains unguarded, and the compatibility, authentication-conformance, and consumer-removal contracts are incomplete. The prior 2026-09-09 reject remains directionally correct, but its recorded baseline and reasons do not describe one coherent current repository snapshot.

## Dimension verdicts

- Decision-readiness — adequate
- Substance over theater — strong
- Strategic coherence — adequate
- Done-ness clarity — thin
- Scope honesty — strong
- Downstream usability — broken
- Shape fit — thin

## Findings by severity

### Critical (5)

**[Downstream usability] — Planning artifacts are not one approved baseline (§11.3; §12 OR14, OR15)**

The PRD delegates implementation slicing and acceptance to `epics.md` while recording stale-input and lifecycle contradictions. Current matching PRD/architecture digests in `epics.md` were refreshed without the reviewed approval sequence OR14 requires; the accepted architecture remains `draft`, and UX digests are already stale. The traceability tables cannot authorize downstream implementation or completion claims.

Fix: Reconcile architecture to the PRD, reconcile epics and UX inputs to both, resolve lifecycle contradictions, run the guarded drift/status validations, and renew approval as one atomic baseline before handoff.

**[Downstream usability / Platform contract] — Normative OQ8 requirements are unavailable inside EventStore (§1.1; FR27; NFR7; NFR16; OR11)**

An external Hexalith.Folders design overrides safety-sensitive state-machine behavior, timers, tombstones, public errors, and evidence denominators, but EventStore cannot reproduce the governed bytes. The current local pre-review validator also exits 1 because `docs/guides/configuration-reference.md` has drifted.

Fix: Retain a permitted immutable copy, complete approved normative projection, or signed/content-addressed attestation whose bytes are available to validation. Bind the identity through PRD, architecture, epics, evidence, and CI, and keep closure fail-closed until the validator passes.

**[Platform contract] — Generated public APIs contradict the canonical tenant boundary (NFR2; §8.2; architecture AD-27)**

The generator can emit the reserved tenant `system`, forwards route tenant spelling without canonicalization, and returns the raw sole tenant claim. It does not enforce the shared grammar or invalid/conflicting/reserved-name rules even though Stories 2.2 and 2.5 are recorded `done`.

Fix: Promote AD-27's public-boundary consequences into stable PRD requirements, route generated controllers through the shared canonicalizer, and add compiled-controller negative tests for malformed, conflicting, unauthorized, uppercase, and reserved tenants.

**[Platform contract] — Public command `Location` can use `CorrelationId` as status identity (FR12; FR27; architecture AD-17)**

The generator falls back from `MessageId` to `CorrelationId` when building the command-status location, although AD-17 makes `MessageId` the sole record selector and Story 2.9 is recorded `done`.

Fix: Require `MessageId` as the only status key, fail closed when it is absent or invalid, remove the correlation fallback, add runtime coverage, and reopen/reconcile Story 2.9.

**[Platform contract] — Silent concurrent append loss remains unguarded (NFR7; §9; SM11; OR4)**

Story 4.5 reproduced `same-key-overwrite-raw-durable-write-lost`; no provider-level append fence has been delivered, and the implementation gap remains unowned. A durable event store cannot authorize MVP completion, release, or deployment while a supported writer race can silently overwrite state.

Fix: Deliver provider-portable write-once/append fencing, or mechanically enforce a supported operating envelope in which the second writer cannot exist. Keep the global gate failed until production-path evidence proves loss prevention.

### High (10)

**[Decision-readiness] — The future `READY` decision has no executable exit contract (§12 OR17)**

Reviewers must reconstruct mandatory gates, evidence, evaluators, waiver rules, and current results across several sections.

Fix: Add one Phase 4 exit table whose rows bind each gate to evidence, evaluator, current result, waiver policy, approval, and invalidation trigger.

**[Done-ness clarity] — Omnibus requirements have no clause-level closure rule (FR26, FR33, FR34, NFR17; OR7)**

Each requirement combines independently fail-able outcomes while whole-ID story mappings do not show which clauses are delivered.

Fix: Assign stable sub-IDs to independent clauses or create a clause-to-story-to-evidence table with an all-clauses-required completion rule.

**[Done-ness clarity] — Acceptance ambiguity extends beyond OR7's named requirements (FR4, FR5, FR7, FR12)**

Feature-level evidence paragraphs do not round-trip every consequence in several multi-clause requirements.

Fix: Give each FR a verifiable consequence list or extend the clause-to-evidence mechanism to all multi-clause requirements.

**[Done-ness clarity] — NFR8's projection-cost outcome has no measurable bound (§7 NFR8; §12 OR16)**

The required projection specification is missing, leaving the projection half of NFR8 untestable and Story 6.4 unauthorized.

Fix: Approve the named specification with numeric bounds, workload, pass condition, and evidence path, then bind those acceptance facts from NFR8.

**[Downstream usability] — NFR traceability is incomplete (§0; §11.2)**

The PRD claims FR/NFR traceability ownership, but §11.2 omits NFR5, NFR12, and NFR13 although `epics.md` declares coverage.

Fix: Add every NFR to the table, distinguishing primary from supporting ownership and retaining the warning that declared coverage is not delivered evidence.

**[Shape fit] — Multi-stakeholder and UI-affecting workflows lack user journeys (§3; §8.3)**

Six roles and three UI surfaces are named, but no narrated journey captures domain adoption, generated API exposure, projection-confirmed UI success, operator recovery, or release authorization.

Fix: Add a small set of named-protagonist journeys for load-bearing cross-role success and failure/recovery flows.

**[Platform contract] — Backward compatibility excludes most public platform surfaces (NFR12)**

The protected set omits major SDK, REST-generator, HTTP error, event-envelope, serialization, DAPR-wire, and NuGet abstraction contracts.

Fix: Inventory source, binary, wire, and HTTP contracts; define SemVer, deprecation, and removal rules; and gate release on API/wire baselines plus package-only consumer tests.

**[Platform contract] — Authentication conformance does not cover every external API host (NFR3)**

NFR3 names a closed host list and omits Tenants and future generated hosts from the full shared JWT posture and traceability.

Fix: Define NFR3 by capability—every externally reachable or JWT-binding host—and add Tenants plus a generated-host conformance fixture to primary release evidence.

**[Platform contract] — Consumer-removal authority is weaker in the PRD than in architecture (FR36; architecture AD-22)**

Consumer-specific repository identity, subject digest, authenticated Consumer-owner receipt, validity, and invalidation rules exist only in the lower-level architecture; the PRD does not define the Consumer-owner role.

Fix: Promote the stable authorization outcomes and invalidation conditions into FR36, define Consumer owner, and separate platform availability, release/deployment authority, and per-consumer removal authorization.

**[Platform contract] — The bound reject result is not a coherent current baseline (frontmatter; §0; OR14)**

The report binds commit `1b6f08d4…`, while current `HEAD` is `293c69c4…`; substantial authentication, planning, validator, and documentation changes have landed. Some recorded blocker reasons are stale even though readiness remains blocked.

Fix: Re-run reconciliation against an explicit clean commit, bind current digests and validator outputs atomically, and retire or rewrite superseded blocker descriptions.

### Medium (4)

**[Strategic coherence] — MVP priority follows the epic inventory more than the thesis (§9.1)**

The PRD does not distinguish exit-critical outcomes that validate the reuse-plus-hardening bet from supporting readiness work.

Fix: Add a short thesis-derived scope rationale without duplicating story sequencing.

**[Shape fit] — Volatile readiness evidence is embedded in the stable PRD (§1.1; §6.8; §10–§12; OR9)**

Receipt counts, story statuses, historical verdicts, source identities, and retired refinements make the requirements baseline stale quickly.

Fix: Keep durable authority and fail-closed rules in the PRD; move replaceable run history and evidence state into a generated ledger or addendum linked by immutable identity.

**[Platform contract] — Ownership traceability is not delivery traceability (§10–§12)**

Tracker status, wrapper status, evidence, and actual public behavior can disagree, so downstream consumers cannot determine which contracts are safe from ownership tables alone.

Fix: Generate an exit ledger keyed by stable requirement/sub-requirement ID with result, evidence identity, evaluator, waiver policy, and invalidation trigger.

**[Platform contract] — Shared-workflow governance mixes mutable and immutable authority (FR25; NFR9; OR18)**

Some authorizing workflows are SHA-pinned while other CI and governance dependencies use mutable `@main`, and the PRD does not classify which results are authorizing.

Fix: Classify every shared workflow/action as authorizing or advisory, pin authorizing dependencies immutably, and bind those identities into retained evidence.

### Low (0)

None.

## Mechanical notes

- FR1–FR37 and NFR1–NFR19 are complete and unique in their requirement declarations. The thematic FR ordering is non-numeric but deliberate and readable.
- §11.1 contains one coverage row for every FR. §11.2 omits NFR5, NFR12, and NFR13; this is promoted to a substantive finding above.
- No duplicate IDs or obviously broken internal section references were found, and every frontmatter `source_artifacts` path exists.
- Glossary terminology is generally disciplined, especially the parity and provenance distinctions.
- The Assumptions Index round-trips cleanly: no inline `[ASSUMPTION]` tags exist and the index says none exist.
- No UJ IDs or named protagonists are present; this is treated as a shape issue rather than an ID defect.

## Reviewer files

- `review-rubric.md`
- `review-platform-contract.md`

## Supporting source extract

- `reconcile-validation-2026-09-10.md`

Historical reviewer files remain preserved in the workspace and were treated as prior-run context, not as current-baseline findings.
