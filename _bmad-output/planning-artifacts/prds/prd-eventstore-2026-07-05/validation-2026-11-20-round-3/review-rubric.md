# PRD quality review · EventStore Phase 4

## Overall verdict

The PRD supports a decision to continue scoped corrective work and gives the product owner a defined order for assessing readiness. Its silent-loss metric still credits a class against a stronger proof standard than the cited story met. Several story handoffs can also produce a different acceptance decision when read on their own. The October 6 readiness rejection remains in effect.

## Decision-readiness — adequate

The opening, frontmatter, §11.4 and OR1 now specify how to obtain G-READINESS after the 15 business and coverage results and the assurance check. The release, deployment and consumer owners each decide on their own subject. The absence of a current approval or seal is a delivery constraint with an identified owner. It does not make the decision order unclear. See PRD lines 109, 721–725, 747 and 809.

## Substance over theater — strong

The technical cases describe what a user or operator can observe. Examples include the conditional query against a changed representation, the effect count after subscriber redelivery, and the retained deferred Admin route’s order of validation and response. The current JWT conformance gate credits the specific Tenants Production integration while still identifying what other host modes and the release owner must prove. See PRD lines 271, 323, 364 and 739.

## Strategic coherence — adequate

SM8 and SM9 measure platform adoption. SM10 and SM11 measure different aspects of operational safety. SM10 now separates the protected-surface numerator from the verification that makes an anonymous endpoint an allowed exception. SM11 has an observable target but its current value still lacks a consistent basis.

### Findings

- **[high] SM11 still credits staged-state recovery without the production-path proof its definition requires.** PRD §10 line 584 credits classes (a), (b) and (d). NFR7 at line 405 requires production-path evidence for *every* credited class, and the glossary at line 230 requires the real process, sidecar, durable state component, actor placement and host pipeline. The Story 5.1 specification explicitly chooses the deterministic `InMemoryStateManager` as its “completion-grade durable-state oracle” and says its gate does not name a live-sidecar lane (line 431). This establishes an implemented, tested guard. It does not establish that class (a) meets the stronger metric. There is also a dated four-of-five historical count in the November 1 approved change proposal (line 161). **Correction.** Keep separate counts for historically credited classes, implemented guards and classes qualified against NFR7. For each current credit, bind the exact case, runtime/profile, result and source for the qualifying test. Mark the current qualified count unverified if those inputs are absent. This reassesses the metric without undoing the historical story decision. Sources: `_bmad-output/implementation-artifacts/spec-5-1-infrastructure-failure-cache-clear.md` and `_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-01-story-4-16-atomic-conflicting-writes.md`.

## Done-ness clarity — adequate

FR12 now makes the configured gateway base and version-valid MessageId decisive, and FR34-C4 makes a typed deferred-operation response conditional on an authorized, valid request. Independent cross-checks found one unqualified tenant rule and one historical completion label that need narrower interpretations.

### Findings

- **[medium] The journey and guardrail omit the authorized tenant-free route.** UJ2 at PRD line 193 says the host “resolves exactly one authorized tenant” before routing. The second sentence of §8.2 line 494 also requires exactly one explicit tenant. NFR2 at line 400 reserves a distinct authenticated platform-operation scope. Story 2.14’s decision table explicitly accepts a cataloged, authenticated platform operation “without tenant” and rejects mixed scopes (spec line 40). Applying UJ2 literally rejects valid platform bootstrap operations. **Correction.** Limit the single-tenant rule to tenant-scoped requests in both passages. Give the platform-operation branch its own positive case and a mixed-scope refusal. Source: `_bmad-output/implementation-artifacts/spec-2-14-canonical-tenant-boundary.md`, lines 40 and 104.
- **[medium] FR26-C4’s current Admin correlation behavior is missing from the handoff.** PRD lines 333, 446 and 493 require ULID-safe Admin correlation and prohibit `Guid.TryParse` for correlation IDs. Its named story is recorded as done (PRD §11.3 line 699). Story 5.4 explicitly requires a canonical input ULID to be preserved and an invalid input to receive a ULID-safe replacement (epics lines 4549–4552). The active Admin host registers a middleware that calls `Guid.TryParse` and replaces a valid canonical ULID with `Guid.NewGuid()`. The epic ledger correctly leaves the overall clause evidence pending, but the handoff still lacks the specific corrective work needed from the owner. **Correction.** Record the failed subcase for FR26-C4, retain the story’s historical scope and identify who must supply the host-level regression and corrected behavior before clause closure. Sources: `src/Hexalith.EventStore.Admin.Server.Host/Program.cs` line 54 and `src/Hexalith.EventStore.Admin.Server.Host/Middleware/CorrelationIdMiddleware.cs` lines 15–24.

## Scope honesty — strong

Sections 6.4 and 9.2 distinguish the approved Story 4.16 storage protection from general activation fencing. The November 1 decision preserves the fully proven exclusion envelope as an alternative gate outcome. Epic 8 and G5 have a separate post-MVP boundary. The generated-link rule and the deferred gateway GET decision also have explicit owners and limits. See PRD lines 325, 495, 526, 545, 753 and 788.

## Downstream usability — thin

The PRD and epic ledgers share an ownership model. A reader extracting the recently approved audit acceptance work or following the instructions for the clause validator still encounters a missing dependency or a retired one.

### Findings

- **[medium] FR4 closure does not bind the November conditional-query evidence.** PRD §6.1 line 271 and Story 2.18 require the returned representation’s own strong validator and HTTP 200 when an independently observed later revision supplies the only tag. FR4-C3 at line 427 checks “ETag … propagate losslessly” and lists Stories 2.7 and 2.11 as contributors. Neither this row nor another FR4 clause names Story 2.18. The epic mirror also omits it, even though Story 2.18 declares supporting FR4 coverage (epics lines 329–339 and 2245–2255). A transport can preserve an ETag exactly while forwarding the wrong revision’s ETag. **Correction.** Bind Story 2.18’s stale-tag negative and matching-tag positive to the affected FR4 closure test or require a separate acceptance entry in the MVP manifest. Preserve the agreed primary owner.
- **[medium] The Story 9.4 dependency still calls for the retired Story 9.1 record.** PRD §0, §8.1 and OR28 specify that the owning spec and its CI run supply corrective authorization and evidence. The detailed Story 9.4 handoff still requires “Story 9.1 passed, plus a Story 9.1 authorization record” (epics line 8155). Stories 3.21 and 7.22 contain comparable dependency wording at epics lines 3352 and 7054. The evidence-policy preamble explains the disposition of deleted tooling, but a reader extracting the dependency line alone is still told to acquire the obsolete record. **Correction.** Replace current dependency language with the owning spec, scoped owner authorization and required CI evidence. Retain the historical mechanism only in dated reconciliation notes.

## Shape fit — adequate

The five named journeys, stable clause table and decision register fit a brownfield platform with separate developer, operator and consumer responsibilities. OR9 explicitly assigns the eventual restructuring of duplicated historical status and accounts for machine-read section anchors. The existing structure is usable for this recovery phase.

## Mechanical notes

The reviewed working PRD SHA-256 is `4644f2b282a3ee7ff860869fd49a620cd32316284cdb594b32d45a6fea663a4b` and the epic plan SHA-256 is `9d488d02e056d656b33c2316cde4c06a00dbc424ce9ea50172c8ad5ecbbcda76`. The source validation grade in frontmatter applies to the previous revision. The current ownership allocations remain subject to the Story 9.4 sign-off recorded in §7.1 and OR7. No formal readiness or release approval was produced by this review.
