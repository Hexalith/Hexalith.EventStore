# Story 3.21: self-hosted runtime/broker investigation

Status: **inspection and qualification design complete; runtime/broker execution
NOT RUN; production eligibility FAIL**. This is the Build planning checkpoint,
not a qualification pass, AD-26 ratification, or production change approval.
Azure and AWS are outside the requested MVP. No cloud service was provisioned.

## Evidence and observed target

The input is the existing, user-modified
[Story 2.13 packet](../../story-2-13/qualification-2026-10-20-network.md).
It retains its original bytes. Its Redis/local two-host results do not qualify
either durable broker or establish production notification distribution.

The [cluster inventory](cluster-before.json) contains exact GET/LIST argument
arrays, start/end UTC times, exit codes, filtered resource identities and the
collector hash. It targets context `jpiquot@local` and checks the API endpoint is
exactly `https://192.168.1.30:6443`. No Secret resources, pod execution,
port-forward, Helm operations, or cluster mutations were used. Resource metadata
and public configuration are retained; credential values and application
environment values are not. Inventory reads are sequential observations, not an
atomic cluster snapshot or cryptographic owner attestation.

| Observed infrastructure | Identity / posture | Reuse decision for planning |
| --- | --- | --- |
| Kubernetes | `v1.34.9`, one `linux/amd64` node, `node1`, `192.168.1.30`; containerd `2.3.3` | Reuse target; one physical failure domain, no node-failure HA claim |
| Dapr | Helm release `dapr` in `dapr-system`; operator, sentry, injector, placement and scheduler `1.18.1`, each 3 ready replicas | Shared infrastructure; below AD-26 floor. Upgrade only after reviewed maintenance approval |
| Dapr storage | Three scheduler `16Gi` claims and three placement `1Gi` claims, `openebs-hostpath-retain` | Retain claims; scheduler restore proof required before upgrade |
| Existing EventStore | `ext-secrets-1-isolated/eventstore`, one ready replica, isolated evidence image and Dapr `1.18.1` | Preserve; not an authorized production deployment identity |
| Broker | No Kafka/RabbitMQ in the 55 observed Deployment/StatefulSet/DaemonSet specifications, pod images or Service inventory; observed pub/sub components are Redis | No observed approved durable broker to reuse; external/unobserved brokers are not disproven |
| OpenBao | `openbao/hexalith-keys`, 3 ready replicas; `quay.io/openbao/openbao:2.6.2@sha256:11fd73a2102cda9c55d5d881a8c3210303146a7ec1e8ac76f526e175c6d24641` | Reuse service/TLS/custody infrastructure only after dedicated grants; existing tokens/policies confer no authority |
| PostgreSQL | CNPG operator `1.30.0`; Keycloak CNPG cluster, isolated registry database and Memories database already serve other workloads | Reuse operator expertise; do not reuse another app's database, tables or credentials |
| Storage | `openebs-hostpath-retain`, reclaim `Retain`, local-node placement, no expansion | Candidate broker gets dedicated retained storage; retention is not backup or HA |
| Backups | Observed Velero schedule covers Forgejo; OpenBao snapshot CronJob exists | Tooling reusable; no demonstrated Dapr scheduler/event-store restore point or restore drill |
| Client | kubectl `1.36.1`, server `1.34.9`; version command reports unsupported two-minor skew | Inspection succeeded; use a compatible `1.34` or `1.35` client for rollout |

All existing Dapr app IDs, component references, pod image IDs, claims, services,
network policies and disruption budgets are in the inventory. The three Dapr
replicas share one node; they do not create independent availability domains.
Node allocatable values do not establish spare disk or production capacity.
[CronJob metadata](cronjob-observation.json) establishes schedule existence only,
not successful backup integrity.

## Exact candidate identities

The recommended **qualification inputs**, with neither broker selected, are
Dapr runtime/chart `1.18.7`, CLI `1.18.2`, embedded `components-contrib 1.18.6`,
RabbitMQ `4.3.6-management` / `pubsub.rabbitmq/v1`, and Kafka `4.3.1` /
`pubsub.kafka/v1`. The maintained RabbitMQ patch avoids adopting the new 4.4
line solely because it is newer. These are test candidates, not an approved
production pin. The [Dapr release](https://github.com/dapr/dapr/releases/tag/v1.18.7),
[RabbitMQ release](https://github.com/rabbitmq/rabbitmq-server/releases/tag/v4.3.6)
and [Kafka downloads](https://kafka.apache.org/community/downloads/) establish
the upstream releases, not this repository's qualification.

[Public identities](public-identities/identities.json) retain all eight raw OCI
index manifests and exact `linux/amd64` child digests, immutable source commits,
source bytes and the Dapr chart archive hash. Each index's retained-byte SHA-256
matched the registry's digest header. This checks byte consistency, not image
signatures, scanning, provenance attestations or execution.

| Candidate image | OCI index SHA-256 |
| --- | --- |
| `ghcr.io/dapr/daprd:1.18.7` | `5da0d379de20830846616d56d07af1594e6ba30cdca8e3ba9de25d18a7624ece` |
| `ghcr.io/dapr/operator:1.18.7` | `42192d91b0942afd694740336e4bd047d55235ebfb3b9cbd4ec0e05ae4026cae` |
| `ghcr.io/dapr/injector:1.18.7` | `4a7b491b2ac5d27c92f0c4192fedd54319452c0dd7d6ae30cda6a4266f751943` |
| `ghcr.io/dapr/sentry:1.18.7` | `b9fc166085289661f516150dc1c47ac3495b9e6d1e666079cb71cd2374ca7256` |
| `ghcr.io/dapr/placement:1.18.7` | `c9da106eddcbb625c94baea31af4ed576d32750741b442c99f51518c420e76cc` |
| `ghcr.io/dapr/scheduler:1.18.7` | `2cc2da2f7c4ece604041e6a420de04a297afb24cea8b3c9394bd0e126a334444` |
| `docker.io/library/rabbitmq:4.3.6-management` | `54cb013d76cd1fd6c0a0708f34062ad61c452bb6f016b434c6c33f1a765d2b58` |
| `docker.io/apache/kafka:4.3.1` | `77e3df9054047a88b520d0cc46e16696d3b22022e1d580aeccd2632df6532837` |

Use `repository@sha256:digest` from the identity file; the tags above are
discovery labels. Runtime `go.mod` and component sources are pinned to canonical
commits in that file. Local binary hashes and version outputs are in
[local-tools.json](local-tools.json); binary identity is separate from OCI identity.
The inventory records existing production `1.18.1` image IDs for rollback review.

## Repository drift and candidate design

[Repository inputs](repository-inputs.json) retain full EventStore/Builds
revisions, the Builds gitlink, exact input file hashes and relevant pin lines.
Sealed integration CI pins runtime `1.18.2` / CLI `1.18.0`; Builds has the same
defaults, with Dapr .NET packages `1.18.10`. AppHost uses Aspire `13.6.1` and
installed runtime discovery, without an immutable runtime pin. Deployment
examples still contain `1.18.0`, Kubernetes guide `1.14.4`, and Compose `latest`.
Report these to Story 5.9; CLI/catalog changes belong to Builds and sealed CI
changes require Story 4.15 reseal. The proposed qualification workflow is new.

Candidate components must use `auth.secretStore: openbao`, credentials via
`secretKeyRef`, TLS verification, explicit app/component/topic scopes, and an
enrolled exact-topic inventory. Do not ship `{env:...}` placeholders or assume
wildcards enroll concrete tenant topics. Existing templates are references,
not ready production manifests. Production actor stores additionally require
AD-5's separate per-app physical targets; the currently shared template does
not establish this. No new production component is installed by this packet.

RabbitMQ's lab candidate uses durable, non-auto-delete queues, persistent
messages (`deliveryMode: 2`), manual acknowledgements, publisher confirms,
bounded prefetch and pinned-source metadata names `concurrency` and
`reconnectWaitSeconds`. Main and dead-letter subscriptions explicitly request
quorum queues and are declared before any producer starts. Queue backlog
retention after disconnection/restart is distinct from Kafka's retained log and
offset replay after acknowledgement. Synthetic expiry tests do not choose a
production retention period. A single member has no node-loss availability.

Kafka's lab candidate uses durable KRaft storage and explicit topic retention,
consumer identity, acknowledgements, TLS/SASL secret references and broker ACLs.
Single-node replication/ISR are 1; a combined lab controller/broker supplies no
HA or critical-production recommendation. Candidate comparison must retain
these limits instead of equating all forms of retention.

## Required live vectors and pinned-source concerns

Every row below is **NOT RUN** on these broker candidates. Source inspection
identifies code paths to falsify; it is not a service-level observation.

| Required vector | Positive and failure evidence to retain |
| --- | --- |
| Runtime identity | Loaded sidecar/component metadata, CLI and control-plane identities equal lock; below-floor/mutable/wrong-component mutations fail |
| At least once | Crash receiver after recording receipt but before ack; restart receiver/sidecar and prove same `CloudEvent.id == MessageId`, persisted one-effect end-state and recovery |
| Durable delivery | Disconnect subscriber, publish controls, restart broker using the same owned volume, then read the backlog through Dapr; no success from a health endpoint alone |
| Quarantine | Poison event reaches a predeclared durable DLQ; failed DLQ publication/authorization must leave the source recoverable; restart and reconcile durable capture before source acknowledgement |
| Retention | Backlog survives an explicit bounded outage; short synthetic expiry is observed beside an unexpired control. Kafka replay retention is tested separately from RabbitMQ acknowledgement removal |
| Scope/security | Allowed publish/subscribe beside forbidden app/topic; missing/revoked secret, bad CA and rotation all fail safely; application receives no broker credentials |
| Publication failures | Queue-full rejection, negative confirms, absent queue bindings, broker blackhole, caller cancellation and bounded retries; no false durable-success claim |
| Notification catalog | Exact tenant/projection enrollment, removal/drain and new-topic rollout; RabbitMQ exchange routing is not cross-exchange wildcard discovery |

The retained component source is commit
`eac9e31dd14cd798c9996aae30818b572aa12c59`. RabbitMQ `publishSync` assigns an
error on a negative confirmation but returns nil; its retry waits also return
nil on cancellation, and publish uses `mandatory=false`. Therefore confirms
alone do not establish a bound queue received a message. These are significant
qualification risks, requiring negative controls before accepting the candidate.
[Pinned RabbitMQ source](https://github.com/dapr/components-contrib/blob/eac9e31dd14cd798c9996aae30818b572aa12c59/pubsub/rabbitmq/rabbitmq.go#L268)
is retained with its hash; a failing runtime vector remains FAIL and returns
to the owner/upstream rather than silently adding a direct broker client.

Kafka's source marks offsets consumed on `ErrRetriesExhausted`, so finite
retry exhaustion without durable diversion requires an explicit loss test.
[Pinned Kafka source](https://github.com/dapr/components-contrib/blob/eac9e31dd14cd798c9996aae30818b572aa12c59/common/component/kafka/consumer.go#L238)
supports that test requirement, not a claim about an executed fixture.

Pinned Vault-component source supports token/token-file bootstrap, not the
preview Kubernetes-auth configuration. Existing OpenBao integration may be
reused only with a dedicated least-privilege contract and tested custody;
production tokens are never copied into this packet.

## Reproduction and disposition

Run from the repository root, choosing a new output path each time:

```bash
python3 _bmad-output/implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/inspect-cluster.py --output /tmp/story-3-21-cluster-new.json
python3 _bmad-output/implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/capture-identities.py --output /tmp/story-3-21-identities-new
python3 _bmad-output/implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/verify-inspection.py
python3 _bmad-output/implementation-artifacts/evidence/story-3-21/qualification-2026-10-20/verify-inspection.py --require-qualified
```

The first two commands do read-only cluster/public-registry discovery. Future
observations can differ. The verifier checks this retained packet and workspace
bindings; its default success is byte consistency only. `--require-qualified`
must exit 1 because broker execution and required authority are missing.
The planned `tools/qualify-dapr-brokers.py` does not yet exist; its implementation
awaits Build specification approval. No new application tests were run here.

Story 3.21 remains backlog; the new Build spec is draft. No AD-26 ratification,
Story 9.1 current-subject authorization, Story 3.19 production profile, production
issuer binding, scheduler-inclusive restore posture, or G-APPEND proof is
created or inferred. Technical results will feed Story 3.17 inventory rows,
Story 5.7 components and Story 3.19 identities only through their owning gates.
Story 2.13, Redis exceptions and NFR5 status are unchanged. The
[rollout/rollback plan](rollout-rollback.md) names the approval boundaries.
