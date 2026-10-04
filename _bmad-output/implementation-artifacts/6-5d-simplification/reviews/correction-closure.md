# Targeted correction closure

All seven individual findings were adjudicated before grouping into five roots. No new work is deferred; the existing RW1 per-guard coverage deferral remains unchanged.

Final verifier SHA-256: `0369d6eb950b64f947490cb3bab6124456ef39e3bb070de280e39bf4d929628d`. Known-answer input SHA-256: `1772a776d4361038598fa9098e88b036612f079b71ce3f42e4391c953b80c419`. Obligations at closure SHA-256: `4e9daf7b760a07b9e1cfa2f97de709d40a3a72322fa10e280f18a59752d5146e`.

The original edge reviewer independently checked four interleavings. Stale capture refuses with `capture-predecessor`, preserves replacement snapshot/carrier/active charge and permits sending after restart. Legacy generation, fence and combined transfers refuse `cas`, retain the original effect and charges, finish to draining after byte restart and preserve original messages; repeated restoration changes nothing. CB1/CE1 and CB2/CE2 are closed.

The verification reviewer ran only the three new focused controls and its original one-change corruptions in memory. All controls passed, and each corruption failed its new owning check:

| Finding | Owning failure | Mutated source SHA-256 |
| --- | --- | --- |
| CV1 | AssertionError: continuation-preserves-original-expiry | fb3e0be0ac8fcdc8247e95f7cf5ffcfeaf94c557241012aed0d84c37274ef933 |
| CV2 | Refusal: ticket | 2d46753d35f72f51c7cf366a83f449a45bee8221ba68efbf8f9e75d6079dde01 |
| CV3 | AssertionError: readiness-exact-bootstrap-precharge-fit | ff88d2517b95cf88fdf56b25e224a06fae097844ac26f4972f33e87d172c03d5 |

These checks made no workspace changes. CV1–CV3 are closed. Full parent verifier, six-corruption suite, seven-regression suite, independent constructors, preservation gate, four Git refusal cases and whitespace check passed after correction.

The initial review used two fresh context-free reviewers plus the earlier read-only investigator for the verification lens after a third fresh launch twice failed at the tool thread limit. That investigator independently retraced current code and tests but retained prior investigation context. This limitation is not described as three fresh context-free sessions. Provider behavior and AD-13 approval remain outside this evidence.
