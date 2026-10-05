# PostgreSQL v1 feasibility — Story 6.6

**Decision update (2026-10-05):** The owner directed Story 6.6 to stay within
the Dapr abstraction. The provider-extension recommendation below is historical;
the current implementation follows the [Dapr-only amendment](../../story-6-6-dapr-only-amendment.md).
The observations still describe why Dapr logical readback cannot be presented as
physical-envelope or historical-generation attestation.

The recommended stock-provider feasibility probe ran successfully. Its result is
**no-go for stock Dapr PostgreSQL v1 as the authenticated historical receipt
authority required by AD-13 §3**. PostgreSQL remains the proposed backend; this
does not qualify the missing provider adapter or authorize production activation.

## Reproduce and inspect

```sh
python3 _bmad-output/implementation-artifacts/evidence/story-6-6/postgresql-v1-spike.py --output /tmp/story-6-6-postgresql-v1-spike
```

The [probe](postgresql-v1-spike.py) requires Docker and uses digest-pinned Dapr
1.18.2 and PostgreSQL 18.4 images, matching the existing OQ8 PostgreSQL digest.
It copies `/daprd` from the pinned image and runs that binary locally to avoid
Docker Desktop container-to-host loopback differences. It owns a small actor
configuration/activation host, placement, sidecar and database; cleanup removes
its processes, containers and container volumes. Shared services are untouched.
Scheduler reminders are disabled because this probe exercises only state.

The [recorded result](postgresql-v1-spike-run/result.json) identifies the runtime,
image digests, repository revision, probe hash and observation-file hashes.
The [source audit](postgresql-v1-source-audit.json) identifies the exact
components-contrib 1.18.3 dependency selected by Dapr 1.18.2's
[go.mod](https://github.com/dapr/dapr/blob/v1.18.2/go.mod).

## Observations

| Check | Actual observation | Implication |
| --- | --- | --- |
| First actor state transaction | Event and head commit and read back successfully. | The stock provider handles the basic multi-key actor transaction. |
| Exact envelope | Submitted and retrieved SHA-256 differ; field order and whitespace change. | Readback cannot be called the original submitted envelope bytes. |
| Embedded payload | Decoding the stored Base64 gives the exact original bytes, including whitespace and Unicode escape spelling. | Payload preservation works; the envelope-byte failure must not be reported as payload corruption. |
| Lost acknowledgment | A proxy receives backend HTTP 204, discards it and closes the caller connection. Reading event 2 and head proves the current append is present. | Current-state reconciliation works for this controlled post-commit response loss. No historical receipt is returned. |
| Later append | Event 3 and head 3 commit. Events 1 and 2 remain; the current head contains only revision 3. | Earlier events alone do not reconstruct the complete earlier committed image, including its head and provider ETag. |
| Actor HTTP ETag | The state read supplies no ETag response header. Diagnostic SQL exposes `xmin`. | The ordinary actor read does not provide the full proposed raw-source contract. `xmin` is not a signed, retained generation receipt. |
| Historical lookup | Audited stock schema/API has current keyed state and metadata, with no retained generation journal or committed-generation receipt operation. | A stock adapter cannot obtain the required historical receipt through the component's supported lookup. |

The retained [generation-2 rows](postgresql-v1-spike-run/provider-after-generation-2.txt),
[generation-3 rows](postgresql-v1-spike-run/provider-after-generation-3.txt) and
[schema](postgresql-v1-spike-run/provider-schema.txt) support these observations.
The numbered `generation` fields are deliberately test-authored application
labels, **not** provider generations. Event and head rows share each append's
transaction `xmin`; that diagnostic fact is not a durable commit certificate.

The stock implementation creates a `jsonb` value column, upserts a single
current row per key, and reads current nonexpired rows by key. Its multi-operation
path uses a database transaction. These findings come from the exact pinned
[schema migrations](https://github.com/dapr/components-contrib/blob/23d03cb76e02e2c3f6b0963726f7a73f42023434/state/postgresql/v1/migrations.go),
[write queries](https://github.com/dapr/components-contrib/blob/23d03cb76e02e2c3f6b0963726f7a73f42023434/state/postgresql/v1/postgresql.go)
and [read/transaction implementation](https://github.com/dapr/components-contrib/blob/23d03cb76e02e2c3f6b0963726f7a73f42023434/common/component/postgresql/v1/postgresql.go).
PostgreSQL's physical MVCC/WAL history is not a supported, retained application
generation lookup and was not qualified here.

## Scope and decision

This is a provider feasibility diagnostic using the real actor-state HTTP
transaction protocol and a minimal activation host. It is **not** an
`AggregateActor`/`.NET IActorStateManager.SaveStateAsync` end-to-end qualification.
It does not prove crash atomicity, ambiguous in-flight outcomes, fences,
no-future-commit evidence, bounded production reads, provider signatures, or a
production trust anchor. Private-table SQL is confined to test diagnostics;
it must not be added to the application-owned metadata adapter.

The original recommendation was to try the stock provider before building a
larger evidence subsystem. That test now supplies a concrete negative result.
Stop expanding the stock-provider integration and review the provider boundary.
The smallest credible next design is one PostgreSQL provider extension that
captures exact envelope bytes and retained before/after images **inside the
same transaction as actor state**, then exposes bounded committed-generation
lookup to the trusted reader. It must establish retention, generation identity,
ETag/fencing and backend-bound signing. A separate post-save application journal
would introduce an unproven dual-write boundary and does not satisfy the contract.

That extension is a new provider capability and a maintenance commitment, not
a small signing gateway. Document and review its contract before implementation;
do not build a generic provider framework or switch to PostgreSQL v2 as a
shortcut. Production-profile approval and key provisioning remain separate.
Story 6.6 and its M1–M8 acceptance obligations remain in progress.

## Verification

- The reproduction command above passed against the pinned live provider; the
  retained run uses the repository evidence output directory instead of `/tmp`.
- Probe syntax and the recorded probe/artifact SHA-256 bindings passed independent
  checks against the files on disk.
- `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations`
  passed; the approved normative digest is unchanged, and all 20 obligations and
  47 implementation follow-ups remain open.
- `git diff --check` passed. No application runtime code or production
  configuration changed, so the earlier build/regression results are not
  replaced by this diagnostic run.
