# Corrective-work CLI controls

These fixtures are synthetic and confer no live authority. Each negative in
`rejections.json` runs the real isolated command in a fresh disposable Git
repository. Its exit status and reason must equal `observed-rejections.json`;
the same controls run with Python assertions disabled. Positive controls cover
preflight, staged and committed changes, deletion/rename/mode permissions,
postflight proposals, authenticated completion recording and replay,
member-associated pinned human identity, and absent output evidence plus an
absent validator with preapproved exact bytes.

`authority_cli.py` intercepts the standard-library HTTPS opener in the fixture
process. It accepts only the validator's fixed GET endpoints. The production
CLI has no alternate authority endpoint, fixture flag, or synthetic approval
bypass. Registry issuer and authorization owner numeric identities, timestamp,
role, exact payload and revocation checks still run. Production HTTPS identity
and GitHub availability are outside this offline fixture suite.

Evidence can defer its digest until the first successful postflight. Executable
validator bytes must be identified before handoff; only an explicitly scoped
addition with a preapproved SHA-256 may be absent at preflight. Tests never
authorize unknown future executable bytes.

Disposable fixture history uses the exact messages `test: seed corrective
authorization fixture` and `test: commit corrective authorization fixture`.
Both were validated before use with the repository-pinned commitlint 21.1.0:
`node node_modules/@commitlint/cli/cli.js --config commitlint.config.mjs`, exit 0
for each exact full candidate on 2026-10-17. No workspace commit is created.

Review-loop-1 controls also cover raw content hidden by assume-unchanged and
skip-worktree, mode changes with core.filemode=false, divergent allowed-source
HEAD/index shadows, unchanged safe baseline symlinks, external-target rejection,
Gitlink index hiding flags, canonical prohibited-directory spelling, page-two
authenticated revocation, post-evidence authority rechecks, unrelated copied
payloads, and cleared/committed/reset completion envelopes. Two precreated
record cores complete and pass registry evaluation without recursive hashes.
`malformed-records.json` retains structural negatives; registry evaluation reports
each one and continues to the valid record.

Completion proposals are non-authorizing FAILs carrying the exact canonical
owner payload and SHA-256. The fixture helper models a human read-only receipt
for that concrete output before the CLI may record postflight. No fixture posts
a GitHub comment, creates a live roster, or grants a gate transition. Clearing
local postflight cannot change the output accepted by a current authenticated
external commitment.

Authentication checks the current GitHub service records. Missing or withdrawn
registry/authorization approvals fail; a missing or withdrawn required completion
commitment fails postflight. A trusted issuer can deliberately withdraw and
reissue its own service records. This validator provides no independent,
append-only external history service and cannot make service deletions
irreversible.

The reviewed controls additionally cover fixed evidence checks before handoff,
raw mutations to another record's normalized envelope, option-like evidence
names, ignored validators, prohibited directories without trailing slashes,
deep JSON decoding failures, nonempty uninitialized Gitlinks, unsafe argument
characters, authorized output-mode drift, and argument-dependent validators.
Absent/empty uninitialized Gitlinks and verified initialized checkouts pass;
verification uses immediate directory names and superproject Git metadata.

Evidence argv preserves the validated positional path with a `./` prefix and
all approved arguments. Execution uses the current isolated interpreter; only
its executable token is normalized to `python3` in the retained subject command
so developer and CI installation paths do not alter output identity.

`subject-vectors.json` contains literal known answers for canonical JSON,
immutable authorization cores, input/output subjects, and multiple envelope
projections. These were computed independently with Node.js built-in crypto
and a separate recursive sorted-key JSON encoder, without production Python
helpers. Tests compare retained canonical strings/digests; they never regenerate
expected answers. The same fixture retains schema/runtime format controls and
supports an interpreter-location portability check against the known output
subject.
