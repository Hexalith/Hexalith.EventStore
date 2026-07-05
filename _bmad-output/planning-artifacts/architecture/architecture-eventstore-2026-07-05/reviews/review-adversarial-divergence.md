# Reviewer Gate - Adversarial Divergence Lens

Verdict: pass after applied fixes.

Attack method: construct two one-level-down units that obey the ADs but could still build incompatibly.

Findings:

- RESOLVED HIGH: A domain-query story could emit freshness/projection metadata as payload fields while a UI story expected HTTP headers or gateway result metadata. Added `AD-14` so query evidence crosses the gateway through platform-owned result/header contracts.
- PASS: External API host and interactive UI host cannot both own generated controllers while obeying the spine. `AD-3` and `AD-4` force generated controllers into external API hosts and UI hosts into client-library consumption.
- PASS: Sample and Tenants domain authors cannot both reimplement platform read-model/cursor plumbing while obeying the spine. `AD-2` and `AD-7` bind platform ownership and domain-only behavior.
- PASS: Two topology stories cannot independently change AppHost and DAPR YAML while obeying the spine. `AD-9` forces app IDs, sidecars, scopes, ACLs, topics, and tests to change together.
- PASS: Append durability and global-position work cannot silently choose incompatible ordering semantics while obeying the spine. `AD-5`, `AD-6`, and `AD-13` bind actor-owned mutation, current event identity, and the sharding spec gate.

Residual risk:

- The spine intentionally defers the exact sharding choice, folded snapshot format, sequence guard algorithm, and upcaster ordering to named specs. That is safe because dependent implementation stories are blocked by `AD-13`.
