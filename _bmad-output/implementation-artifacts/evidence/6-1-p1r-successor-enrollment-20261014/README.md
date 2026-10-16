# P1R successor source enrollment — 2026-10-14

**Proposed stable version: 3.122.1.** EventStore is refreshed to observed `origin/main` `210b90da172cc7ca946fbb06cfd44390bffa707b` in a detached checkout; original local `main` and all repair/historical bytes are retained. Production snapshot enrollment matches refreshed upstream. Only the proposed successor is added to the bounded Counter, seed, case and strict witness guards.

[Publication-readiness handoff](publication-readiness-handoff.md) gives canonical version analysis, registry receipts, observed Builds identities, unset future landing/publication identities, required gates and exact guarded commands. The current docs-only main produces no release; a separately identified hypothetical future fix produces patch 3.122.1. No version was reserved or published.

[Reviewed source](reviewed-source.json): 23 paths; cumulative `publication-source-v3.diff` SHA-256 **`54263498531e2efe7b3c100d4244269a57dbf0afb8d23f3a21ed0708a91cde7d`**. Earlier source/diff/control evidence is retained under versioned names. The four unchanged historical helper scripts required by clean CI are explicitly bound in [required-ci-test-dependencies.json](required-ci-test-dependencies.json).

[Verification](verification.json) passed 228 Python controls, 178 Server controls and 24 real compiler evaluations. These cover actual strict genuine proof execution and missing/substituted proof refusal, provider byte order, physical serving/probe binaries, nine/manual/three timing, mutation refusal, real import forwarding, private admission, final source checks, exact cleanup absence, final isolation binding and timing/ownership substitutions. A [fresh real private runner](isolation-final-v3-live/isolation.json) passed a source-only command and owned teardown; it executed no successor qualification scenario.

[Independent review triage](review-triage.json) records every finding and disposition. Temporal and exact-execution cleanup findings passed independent final rechecks. The pre-existing remote TLS outer Docker controller limitation is deferred; the observed profile uses the local Unix endpoint.

[Preservation](preservation-result.json) rehashed all 191,058 files / 15,134,803,364 bytes in thirteen historical directories: zero missing, changed or extra files. Protected Story/acceptance/sprint/prior-spec bytes and production source are unchanged. Fixture correction, private isolation, complete shared equality, exact owned cleanup and unowned preservation, rollback=null, Dapr 1.18.2, the PostgreSQL digest/profile, comparisons 3.70.1/3.110.0, `p1r-executed-checks-v1`, Reminder recovery and logical-event evolution remain required.

No source was staged, committed, pushed or published. This is source/tooling verification only. Four-role acceptance, conformance and disposition remain pending; P1R is unusable and Story 6.1 is blocked. Successor package qualification is false.
