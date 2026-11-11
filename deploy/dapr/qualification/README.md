# Story 3.21 disposable candidate lane

These files are non-authorizing inputs. Neither broker is selected. The target
remains self-hosted Kubernetes; this runner implements only the local Docker
stage in the retained rollout plan. SQLite is an isolated Dapr application
readback component for this experiment, never the AD-26 production actor store.

Run from the repository root with Docker, Python 3.11+, OpenSSL, and the exact
Dapr CLI/runtime binaries whose SHA-256 values are in runtime-lock.json:

```bash
python3 -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py
python3 tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock.json --brokers rabbitmq,kafka --output /tmp/story-3-21-new-run
python3 tools/validate-dapr-broker-qualification.py /tmp/story-3-21-new-run --allow-failures
python3 tools/validate-dapr-broker-qualification.py /tmp/story-3-21-new-run --require-production-authority
```

Use a fresh output path. The runner requires both brokers and always records all
12 scenarios. Its nonzero exit means technical failure; an infrastructure blocker
is distinguished from an observed scenario failure in each observation. Integrity
verification with `--allow-failures` preserves that distinction and never changes
failed results into a pass. The authority command deliberately fails while the
owner/profile/Kubernetes/envelope/restore gates are absent. The independent manual
workflow neither publishes images nor promotes releases and retains this truthful
failure as its job result.

The explicit C# test in DurableBrokerQualificationTests drives the same real-host
matrix and independent verifier, then requires an observed bounded outcome for
every scenario. An observed rejected guarantee is retained as FAIL; a readiness
blocker fails the live test. A passing evidence-capture test never changes the
technical qualification or production eligibility results. Invoke the
built xUnit v3 assembly with `-class "*DurableBrokerQualificationTests" -explicit only`.
The fixture retains logs beside the packet directory so they cannot invalidate its
closed artifact inventory. Cancellation sends SIGTERM to let the runner clean its
own resources; do not forcibly kill the producer during cleanup.

An owned byte-forwarding process keeps each Dapr-facing loopback endpoint stable
when Docker Desktop changes a container's published port after restart. TLS stays
end to end; the forwarder has no broker protocol or credential handling.

Every container has a random ownership label. Only a local Unix-socket Docker
endpoint is accepted; Kubernetes contexts are never consulted. Placement and
scheduler binaries are extracted from retained immutable images and run as owned
processes on random ports. OpenBao is independently initialized with verified TLS
and a dedicated read-only token. Broker credentials and application-channel tokens
are ephemeral and redacted from receipts; application effects go only through
Dapr state APIs. Administration commands are isolated diagnostics/fault injection,
not application provider clients. Private material is outside the evidence packet
and removed after the owned containers stop.

Production rollout remains gated by the retained rollout-rollback.md. An exact
production change/restore bundle cannot be sealed before owner-selected broker,
protected prior release values, workload identity, Kubernetes rehearsal and restore
inputs exist. Do not use these candidate files as production-profile.yaml. Runtime
minor changes require broker requalification and Story 4.16 envelope re-proof.

## Explicit November runtime candidate

`runtime-lock.json` and its original hashes remain immutable. Existing callers
still select runtime 1.18.7 / CLI 1.18.2 / embedded components 1.18.6. Their exact
input bytes are preserved under `public-identities/1.18.7-original-inputs/` when a
current source file has evolved. These are historical inputs, never a claim that
the old capture code executed during a new run. Schema 1 packets retain their
original paths and interpretation; schema 2 captures separately retain the actual
current capture tools under `execution-tools/`.

Select `runtime-lock-1.19.1.json` explicitly for runtime/control plane/CLI 1.19.1
and embedded components 1.19.0. The SDK remains 1.19.0. Versioned identities include
official source tags/commits, release and latest-stable metadata, archive checksums,
verified executable checksums, the chart's version/appVersion, all six OCI indexes
and both Linux amd64/arm64 child manifests. Native placement/scheduler/Sentry bytes
are also checked against binaries read from the pinned amd64 OCI layers. Release
archives/executables stay in isolated temporary tools directories, outside the repo.
No installed Dapr, shared catalog, original PF-02/Azure subject or sealed workflow
input is repinned by this selection.

Before **any** Docker mutation, an infrastructure owner must allocate an exclusive
disposable worker and supply a worker JSON file containing `exclusive: true`, a
nonempty `workerId`, the canonical local Unix-socket `endpoint`, and its observed
`daemonId` (`docker --host ENDPOINT info --format '{{.ID}}'`). The producer holds an
exclusive lock on that allocation file. An arbitrary local file does not establish
that a shared Docker Desktop worker has become exclusive. Hosted jobs allocate
fresh GitHub-hosted workers; local/shared/remote workers without an allocation are
blocked. Never create an allocation document to bypass the ownership requirement.

The producer resolves Docker context/host precedence, rejects conflicting or remote
selections, captures the actual local socket identity and daemon ID, then pins and
rechecks them before every operation. Cleanup uses recorded container IDs and owner
labels. Startup/cancellation failures still compare the entire baseline of container,
network and volume specifications, including stopped unrelated containers. Missing
or changed baseline resources fail integrity. A changed daemon/socket refuses all
further mutations, including cleanup; unresolved custody remains an explicit failure.
Selected regression child processes use the same daemon-checking Docker adapter.

```bash
export QUALIFICATION_EXCLUSIVE_WORKER=/path/to/owner-issued-worker.json
python3 -B tools/install-dapr-qualification-tools.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --destination /tmp/dapr1191-tools
export QUALIFICATION_DAPRD=/tmp/dapr1191-tools/runtime/daprd
export PATH="/tmp/dapr1191-tools/cli:$PATH"
python3 -B tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock-1.19.1.json --exclusive-worker "$QUALIFICATION_EXCLUSIVE_WORKER" --output /tmp/dapr1191-brokers
python3 -B tools/validate-dapr-broker-qualification.py /tmp/dapr1191-brokers --allow-failures
python3 -B tools/validate-dapr-broker-qualification.py /tmp/dapr1191-brokers
python3 -B tools/validate-dapr-broker-qualification.py /tmp/dapr1191-brokers --require-production-authority
```

The last three commands have separate meanings: integrity of truthful results,
technical guarantees, and production authority. An unexecuted case is an infrastructure
blocker. An observed rejected guarantee remains technical FAIL. Neither capture
success nor `--allow-failures` changes a rejected guarantee to PASS. Production
eligibility remains FAIL and selected broker remains null.

The optional public `DevelopmentRouteCatalogRuntimeProfile.Dapr1191` is selected via
`DevelopmentRouteCatalogTransportOptions.RuntimeProfile`; the existing constructor,
deconstruction, constants and default remain original. Selected live fixtures use
`EVENTSTORE_DAPR_RUNTIME_PROFILE=1.19.1` plus explicitly verified binary paths.
The integration broker fixture selects a lock via
`EVENTSTORE_DAPR_QUALIFICATION_PROFILE`; the default stays original. Published
compatibility retains its original 1.18.4 runtime unless explicitly selected; its
security/restore classes receive owned candidate binaries through
`EVENTSTORE_DAPR_RUNTIME_BINARIES`. Candidate output never relabels the historical
matrix contract. `qualify-internal-actor-admission.py --profile ...
--exclusive-worker ...` separately supports the selected binaries/images.

`dapr-runtime-qualification.yml` builds affected projects in package/Release mode
and runs built xUnit assemblies with `-class`; the broker class uses `-explicit only`.
It covers public routing/token/ACL/native actor transport, concurrency/tenant isolation,
reminder recovery, snapshot reload and PublishedCompatibility security/restore.
The wrapper requires nonempty execution with zero skipped selected tests. Local
validation uses individual project Debug/source builds. The two qualification
workflows remain independent of sealed `ci.yml`, `integration.yml` and `docs/ci.md`.
Run output belongs in temporary directories or CI artifacts; the owning spec records
commands, counts, blockers and eventual CI links.

EventStore owns these contracts and qualification. Builds owns shared catalogs,
tools and runner adoption; Platform owns composed Kubernetes rehearsal, deployment
and restore. Their handoffs remain pending. Independent sharding/PF-02/Azure proof
subjects and Story 4.16 runtime-minor envelope re-proof remain separate gates.
The new candidate is unqualified until all selected live checks execute and pass;
this change grants no production, publication, broker-selection or cryptography
implementation authority.

The candidate requires schema 2. Its execution tools are compared with the
independently trusted owning checkout, and accepted worker/socket/daemon custody
is bound to observed commands. Both observed placement and scheduler identities
are required for executed candidate results; resealing a downgraded or substituted
packet cannot turn it into candidate proof. Original schema 1 packets retain their
original interpretation.

The selected xUnit wrapper owns the test process group and uses Linux child
subreaper custody to track, stop and reap descendants, including children that
create new sessions. Its pinned Docker adapter journals child-created container
and network IDs before returning them. Named volumes have no Docker UUID, so their
creation timestamp, driver/options and unique owner/resource labels are committed
and checked before removal. An unresolved resource identity, changed custody or
remaining descendant fails cleanup and retains private custody material. Independent
owned-ID cleanup continues after one removal failure while custody remains valid.

Actor admission/process replacement runs in its own `actor-admission-process`
job and claims that job's allocation directly; it never claims a second lock
inside the runtime wrapper. The C# caller forwards the explicit profile/worker
and asserts observed runtime and control-image identities, including replacement
sidecars. Unrelated container, network and volume inspection is hashed before
receipt persistence.

Compatibility jobs fetch the historical EventStore tags/source and the exact
Builds object needed by `ConsumerMaterializer`, using nonrecursive fetches.
Materialization stays in private scratch outside the uploaded artifact. Bounded
source, process, restore and cleanup witnesses are projected to
`compatibility-witnesses/` under the runtime output; arbitrary strings and command
diagnostics are committed by hash, and consumer/package trees, environment secrets,
provider dumps and private keys are excluded. The projection manifest records
omitted/oversized witnesses. xUnit results retain test identities/outcomes while
arbitrary diagnostic text is committed by hash.

Direct broker/admission xUnit lanes capture the complete initial container,
network and volume inventory in the parent before releasing the allocation lock
to their child owner. Child resource intents, absence checks, immutable IDs and
ownership labels are persisted atomically before/after creation. After the child
exits, times out or receives cancellation, the parent reclaims the same allocation
and verifies the original socket/daemon before independent reconciliation. SIGTERM
records a failed outcome and still runs that reconciliation. A short process
termination grace is never taken as proof that Docker cleanup completed. Missing
custody, unresolved creation, changed temporary-directory identity or an incomplete
baseline comparison keeps cleanup incomplete.

The child adapter supports `create`/`run` and `container create`/`container run`
with the same journal. Explicit bind mounts, tmpfs mounts and named volumes created
and measured by that journal are supported, including equals-form options.
Implicit named/anonymous volumes, inherited volumes and uncovered image-declared
volumes are rejected before container creation. Internal broker/admission owners
explicitly mount unused image scratch paths as tmpfs; durable paths keep their
explicit binds. Unsupported global options and alternate bulk creation commands
cannot bypass the creation parser.

Candidate input and execution-tool inventories are compared in full with the
trusted owning checkout/profile. Separate before/after command chains bind every
resource kind, including empty inventories; placement/scheduler identities include
extraction, measured-file and native-launch command references. Compatibility
witness limits use the exact sanitized bytes written, with separate original and
retained SHA-256/size commitments verified against the retained files. Partial
xUnit XML is replaced by a digest-only failed-report witness before upload.

New selected broker captures use versioned scenario diagnostics. RabbitMQ pressure
requires the exact `qualification-consumer-qualification.backpressure` queue's
measured effective reject-publish policy before publication. Missing-binding checks
retain physical binding/topic inventories before and after publication, beside an
existing positive control. Rotation retains administrative user/ACL state and a
typed broker authorization response; generic HTTP failures and timeouts cannot pass
as denial. Rotation and pressure reconcile every exact publication ID, including
rejected and timed-out requests, against one bounded persisted Dapr readback. Kafka
also retains offset/consumer-commit snapshots around pressure. These administrative
snapshots do not establish the authenticated principal/session, exact message-to-offset
mapping or a cold stale-credential restart. Missing readback effects remain bounded
non-observations, never proof of absence. Original packet interpretation is retained;
changed capture sources require fresh hosted qualification.

## Bounded Platform configuration input

`runtime-lock-1.19.1-platform-config-v1.json` is a separate, explicitly selected
profile. It keeps runtime/CLI 1.19.1, SDK/components 1.19.0, both immutable broker
identities and all 24 vectors. Both historical lock files stay unchanged. The
historical 1.19.1 source bytes are retained in
`public-identities/1.19.1-original-inputs/`; its archive manifest distinguishes
c94e6fffaf2d689c94bb288f151a65e10a2fea82 snapshots from two test files whose
existing lock already referenced earlier bytes. Historical schema-1/2 packet
interpretation and trusted source checks remain available. New historical-profile
captures retain their actual current execution tools separately; the verifier
accepts only a complete historical or complete current trusted tool inventory.

Platform supplies a UTF-8 JSON file with this exact schema. This is a tuning input
for the existing harness, not a Platform composition or runner descriptor:

```json
{
  "schemaVersion": 1,
  "platformRevision": "0123456789abcdef0123456789abcdef01234567",
  "platformPath": "qualification/broker-experiment.json",
  "rabbitmq": {
    "reconnectWaitSeconds": 2,
    "outboundTimeoutMilliseconds": 500
  },
  "kafka": {
    "consumeRetryIntervalMilliseconds": 200,
    "group.initial.rebalance.delay.ms": 100
  }
}
```

`platformRevision` is the full lowercase 40-character Platform source revision
that Platform declares for the experiment; `platformPath` is its declared relative
JSON path. They are content-bound provenance declarations. The input may be a
separately delivered local copy: its declaration does not prove that the file was
committed at that revision, authenticate Platform's author, or establish accepted
owner scope. The same independently supplied original file is required during
verification. Do not take the retained packet's copy as that independent input.

Only these integer values are permitted:

| Field | Values | Default |
| --- | --- | --- |
| RabbitMQ `reconnectWaitSeconds` | 1, 2, 3 | 1 |
| RabbitMQ `outboundTimeoutMilliseconds` | 250, 500, 1000, 2000, 3000 | 3000 |
| Kafka `consumeRetryIntervalMilliseconds` | 100, 200, 500, 1000 | 1000 |
| Kafka `group.initial.rebalance.delay.ms` | 0, 100, 500, 1000 | 0 |

Broker objects and tuning fields are optional; omitted fields retain the original
defaults. Booleans, strings, floating-point numbers, unknown or duplicate keys,
non-JSON data, credentials, endpoints and commands are rejected. Input files must
be regular `.json` files without symlinks or `..` traversal. Declared paths must
also be relative JSON paths without traversal. Rejection occurs before output
creation or resource mutation and reports fields without their supplied values.
These bounds define experiments, not proven remedies. Kafka `initialOffset` stays
`oldest`; inbound timeout/retries, TLS, grants, scopes, durability, confirms,
pressure controls, publication counts and observation/readback bounds are fixed.

Under Platform's separately accepted execution scope and supported allocated
runner, the existing entry points take the explicit pairing:

```bash
python3 -B tools/qualify-dapr-brokers.py --profile deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json --platform-config /path/to/platform-experiment.json --exclusive-worker "$QUALIFICATION_EXCLUSIVE_WORKER" --output /tmp/platform-broker-experiment
python3 -B tools/validate-dapr-broker-qualification.py /tmp/platform-broker-experiment --platform-config /path/to/platform-experiment.json --allow-failures
```

The C# entry point uses `EVENTSTORE_DAPR_QUALIFICATION_PROFILE` with the new lock
and `EVENTSTORE_DAPR_PLATFORM_CONFIG` with that same independent input. Configuration
with either historical profile, a missing configuration with the new profile, and
schema downgrades or mixed profile identities fail; there is no automatic profile
selection. Existing invocations without configuration keep their defaults.

Schema 3 retains exact configuration bytes, declared provenance, the complete
current source inventory, every loaded component/subscription/configuration file,
and sanitized effective Kafka properties. Kafka PEM values alone are replaced by
`<disposable-pem>`; a measured-file SHA-256 receipt binds the private properties
file before render commitment and broker launch. The independent verifier checks
rendered settings against the requested tuning and the established loopback/port,
private-path, negative-secret/TLS and replay-group substitutions. Render receipts
bind each loaded directory to its sidecar arguments and Kafka properties to its
broker launch. Changes to configuration, sources, effective settings or execution
paths fail even after packet artifact hashes are recomputed. Worker custody,
complete resource inventories, unrelated-resource preservation and private-material
cleanup checks apply to schema 3 as they do to schema 2.

This implementation runs no infrastructure or CI and adds no Platform runner
capability or acceptance. Platform supplies configuration and owns subsequent
execution; EventStore retains application assertions and evaluates its results.
Capture integrity, technical qualification and production authority remain separate.
All five hosted rejected guarantees stay unresolved: RabbitMQ rotation, pressure
and missing binding, and Kafka rotation and pressure. Platform's
[2026-11-10 dispositions](../../../references/Hexalith.Platform/docs/implementation/story-4-17-local-broker-dispositions-2026-11-10.md)
find Kafka `connections.max.reauth.ms` and a RabbitMQ alternate exchange
inapplicable to their vectors. RabbitMQ pressure separately needs a released
runtime embedding the upstream negative-confirm fix; missing binding separately
needs upstream `mandatory` and return handling. Neither is solved by these knobs.
Timeouts and missing bounded effects remain uncertainty/FAIL. Story 3.21 stays
in progress, selected broker stays null and production eligibility stays FAIL.
