# Correction verification-gap review

This read-only investigator was reused after two fresh reviewer launches because the third fresh launch twice failed with `agent thread limit reached`. It did not implement the correction. It independently reread the final diff and lens instructions and reran checks/searches. This layer had prior investigation context; it was not context-free.

- **CV1 — continuation expiry regression gap.** Removing only `;expiry=d['expiry']` passes the complete owning verifier. Existing two-row cases return no continuation cursor. With three rows and advancing UTC, the mutation extends 900 seconds to 1,000 and accepts page three at 901. Add a three-page scenario preserving the first expiry and refusing at the original deadline.
- **CV2 — eight-shard cycle regression gap.** Changing only queue_save placement `%8` to `%7` passes the owning verifier; legal ticket 8 then refuses `ticket`. Existing fixtures admit at most three rows and the full-shard case uses limit zero. Add tickets 1–16, persisted shard/readback/header consistency and global grant order.
- **CV3 — readiness boundary regression gap.** Omitting only `256*CAPS['registry']` passes the owning verifier and accepts reserve one byte below the normative total. Existing cases are generous-fit or broadly insufficient. Add acceptance at the corrected bootstrap requirement and unchanged refusal one byte below, with recorded overhead.

Each claim is supported by symbol/consumer searches, inspection of active verification scripts and a one-change mutation against reviewed source `b33f0b1d8e789f9ae6cc1ede9a0db88300f2aa9f07a586784aee9237fb9a086a`. These are bounded feature/lifetime/boundary cases, not a per-guard sweep. The reviewer recommends patching each gap.
