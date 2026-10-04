# P1R verification invocation

This is an investigation, not owner acceptance. `qualified` and P1R usability remain false.

- provenance: passed / compatible (232 assertions)
- legacy-metadata: passed / compatible (4 assertions)
- metadata-read: passed / incompatible (8 assertions)
- metadata-write: failed / unverified (35 assertions)
- full-replay: failed / unverified (35 assertions)
- snapshot-tail: failed / unverified (35 assertions)
- retained-covered: failed / unverified (35 assertions)
- retained-uncovered: failed / unverified (35 assertions)
- missing-event: failed / unverified (35 assertions)
- invalid-evidence: failed / unverified (35 assertions)
- query-wire: passed / incompatible (16 assertions)
- projection-wire: passed / incompatible (4 assertions)
- mixed-api: failed / unverified (35 assertions)
- checkout: failed / unverified (55 assertions)
- post-upgrade-restore: failed / unverified (35 assertions)
- pre-upgrade-restore: failed / unverified (35 assertions)
- failure-cleanup: passed / compatible (6 assertions)

Pre-upgrade restore is containment mechanics only; later writes are absent from that backup. Incompatible or unavailable lanes require a later named owner decision. Commands, exact package graphs, assembly hashes, inventories and cleanup are hash-bound. Dump bytes and credentials were destroyed with the invocation scratch.
