# P1R verification invocation

This is an investigation, not owner acceptance. `qualified` and P1R usability remain false.

- provenance: passed / compatible (279 assertions)
- legacy-metadata: passed / compatible (4 assertions)
- metadata-read: passed / incompatible (8 assertions)
- metadata-write: unavailable / unverified (0 assertions)
- full-replay: unavailable / unverified (0 assertions)
- snapshot-tail: unavailable / unverified (0 assertions)
- retained-covered: unavailable / unverified (0 assertions)
- retained-uncovered: unavailable / unverified (0 assertions)
- missing-event: unavailable / unverified (0 assertions)
- invalid-evidence: unavailable / unverified (0 assertions)
- query-wire: passed / incompatible (52 assertions)
- projection-wire: passed / incompatible (10 assertions)
- mixed-api: unavailable / unverified (0 assertions)
- checkout: unavailable / unverified (0 assertions)
- post-upgrade-restore: unavailable / unverified (0 assertions)
- pre-upgrade-restore: unavailable / unverified (0 assertions)
- failure-cleanup: passed / compatible (6 assertions)

Pre-upgrade restore is containment mechanics only; later writes are absent from that backup. Incompatible or unavailable lanes require a later named owner decision. Commands, exact package graphs, assembly hashes, inventories and cleanup are hash-bound. Dump bytes and credentials were destroyed with the invocation scratch.
