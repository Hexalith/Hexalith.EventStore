# P1R verification invocation

This is an investigation, not owner acceptance. `qualified` and P1R usability remain false.

- provenance: passed / compatible (232 assertions)
- legacy-metadata: passed / compatible (4 assertions)
- metadata-read: passed / incompatible (8 assertions)
- metadata-write: passed / incompatible (50 assertions)
- full-replay: passed / compatible (96 assertions)
- snapshot-tail: passed / compatible (96 assertions)
- retained-covered: passed / compatible (96 assertions)
- retained-uncovered: passed / compatible (192 assertions)
- missing-event: passed / compatible (192 assertions)
- invalid-evidence: passed / incompatible (420 assertions)
- query-wire: passed / incompatible (16 assertions)
- projection-wire: passed / incompatible (4 assertions)
- mixed-api: passed / incompatible (116 assertions)
- checkout: passed / incompatible (1026 assertions)
- post-upgrade-restore: passed / compatible (49 assertions)
- pre-upgrade-restore: passed / compatible (53 assertions)
- failure-cleanup: passed / compatible (6 assertions)

Pre-upgrade restore is containment mechanics only; later writes are absent from that backup. Incompatible or unavailable lanes require a later named owner decision. Commands, exact package graphs, assembly hashes, inventories and cleanup are hash-bound. Dump bytes and credentials were destroyed with the invocation scratch.
