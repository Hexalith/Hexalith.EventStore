# P1R verification invocation

This is an investigation, not owner acceptance. `qualified` and P1R usability remain false.

- provenance: unavailable / unverified (217 assertions)
- legacy-metadata: passed / compatible (4 assertions)
- metadata-read: passed / incompatible (8 assertions)
- metadata-write: failed / unverified (1 assertions)
- full-replay: failed / unverified (1 assertions)
- snapshot-tail: failed / unverified (1 assertions)
- retained-covered: unavailable / unverified (0 assertions)
- retained-uncovered: failed / unverified (1 assertions)
- missing-event: failed / unverified (1 assertions)
- invalid-evidence: failed / unverified (1 assertions)
- query-wire: passed / incompatible (16 assertions)
- projection-wire: passed / incompatible (4 assertions)
- mixed-api: failed / unverified (1 assertions)
- checkout: failed / unverified (1 assertions)
- post-upgrade-restore: failed / unverified (1 assertions)
- pre-upgrade-restore: failed / unverified (1 assertions)
- failure-cleanup: passed / compatible (4 assertions)

Pre-upgrade restore is containment mechanics only; later writes are absent from that backup. Incompatible or unavailable lanes require a later named owner decision. Commands, exact package graphs, assembly hashes, inventories and cleanup are hash-bound. Dump bytes and credentials were destroyed with the invocation scratch.
