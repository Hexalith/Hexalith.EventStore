# Current CI release review records

The byte-preserving alternative was implemented and independently reviewed in `/tmp/eventstore-ci-release-current` from baseline `4cc77f9554395e84539e173e94b8f0b4df14d643`. All required local checks pass. `source-changes.diff` captures that alternative and its records; it has not been staged, committed or applied over the other session's changes.

The other session published v3.117.0 from `b830d9829af70536d2a3fd21c5e2a23b2ca2f256`. Publication verification is recorded separately. Its historical-capture rewrite conflicts with the requested spec's frozen constraint; the user's approach/ownership choice remains pending.

Recorded source hashes identify the isolated tested files, not the current shared checkout. Retained output paths use the same repository-relative evidence directory. No credentials from captured runtime state are included in this directory.

Publication verification is complete: ordinary protected Release 37750172086 succeeded as v3.117.0; the tag, exact-source CI/Commitlint, all 14 packages in both channels, every nuspec commit and uncompressed content parity were verified. The published repair belongs to the other session; the reviewed preserving alternative remains uncommitted pending the user's capture-handling choice.
