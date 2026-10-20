# Story 3.21 rollout and rollback proposal

**Not approved or executed.** The production API is
`https://192.168.1.30:6443`, context `jpiquot@local`. This plan does not authorize
cluster changes. Build specification approval authorizes local implementation
and disposable qualification only. Production approval must name the exact
later change-bundle digest, namespaces, objects, maintenance window and stop
conditions. Neither approval substitutes for the required AD-26 owner records.

## 1. Qualify before asking for production changes

Use isolated disposable infrastructure on the development machine, owned
containers/volumes and random loopback ports. Do not reuse or restart shared
`dapr_placement`, `dapr_scheduler`, Redis, the unrelated Story 4.16 AppHost, or
any production resource. Bind the exact candidates from
`public-identities/identities.json`; hash every runtime binary and component.
Both brokers must run the matrix in `qualification.md`, with forbidden controls,
persisted readback, exact command receipts, log redaction and owned-ID cleanup.

Start with RabbitMQ 4.3.6 and Kafka 4.3.1 as comparison candidates. Neither is
selected. A lost event, false positive publish result, acknowledged poison event
without durable quarantine, invalid scope, or unresolved secret/TLS behavior
fails that candidate. Preserve failures. Resolve upstream/runtime risks or
return to the owner; do not add a provider client to application code.

The local lane first proves Dapr/component behavior. A disposable Kubernetes
rehearsal must then bind its server/node identity, chart rendering, admission,
sidecar authentication, DNS, storage restart and scheduler/placement configuration.
It must record any difference from the target's Kubernetes 1.34.9 and single-node
storage. Docker or a different Kubernetes version never counts as target-path
proof. Production-path evidence remains separately gated.

## 2. Assemble an exact change bundle

Generate `change-bundle/` beneath a new dated Story 3.21 evidence run, containing:

- The prior and proposed full Dapr release manifests, scrubbed reviewed values,
  release revision supplied through the owner's administrative workflow, exact
  original chart archive and new `dapr-1.18.7.tgz`, their hashes, and an object
  identity/specification diff. Helm values containing credentials stay in the
  owner's protected custody, outside the repository. This inspection did not
  read Helm release Secrets or obtain those private inputs.
- `upgrade-values.yaml` with immutable operator/injector/sentry/placement/scheduler
  image overrides and the retained live replica, security, service, API-level,
  trust, storage and scheduling settings. The pinned chart supports full image
  references in `image.name`; the injector separately exposes
  `dapr_sidecar_injector.injectorImage.name` and `.image.name` for its process and
  default daprd. Render and verify every resulting image, including hook jobs.
  Do not rely on `global.tag` alone or an unchecked `--reuse-values` merge.
- An explicit injector-default transition strategy. Keep unrelated workloads'
  existing sidecar versions during the approved canary stage, including their
  future rescheduling; set only the approved target's `dapr.io/sidecar-image` to
  the locked candidate. Test this coexistence posture in rehearsal. Do not change
  the cluster-wide injector default to the candidate or restart all Dapr apps as
  an incidental upgrade. A later default change needs its own enumerated approval.
  Existing below-floor applications remain ineligible for an AD-26 claim.
- The candidate broker's dedicated namespace `eventstore-broker`, StatefulSet,
  internal Service, retained PVC, network policies, TLS configuration and bounded
  resources; no public ingress. Add only the owner-selected broker. Reuse
  OpenBao, cert-manager and the retained storage class after dedicated grants
  and capacity checks; do not share other workloads' credentials, PVCs or tables.
- Application components/subscriptions scoped to a separately approved EventStore
  target namespace; the observed `ext-secrets-1-isolated` namespace is excluded.
  Component name `pubsub` matches application configuration. The `openbao`
  component, secret contract, broker identities and exact topic catalog must
  agree with the proposed profile and actual broker grants. Credentials never
  appear in the bundle; use logical references. Main/DLQ bindings precede traffic.
- The old workload specification and old component/catalog bytes, new immutable
  application image and sidecar identities, state/consumer compatibility proof,
  ingress switch, drain/offset ledger, and scripted reverse changes. Keep Direct
  notifications and at most one EventStore replica unless Story 2.13/AD-36
  separately qualify the fan-out profile. Do not turn on PubSub freshness merely
  because the event broker qualifies.
- A consistency point, durable off-node backup identities, restore order and
  successful restore drill for scheduler/placement, actor/admission/fence state,
  read models, checkpoints, command status and broker data. Prove a restored
  checkpoint never passes an event absent from its read model. Retained PVCs and
  existing Forgejo/OpenBao backup schedules do not establish this.

The bundle must include capacity/retention bounds, monitoring thresholds and
owner-approved RPO/RTO, plus acknowledgements from owners of **all** Dapr-enabled
apps enumerated by the live inventory. A shared control-plane upgrade can affect
them even when their specifications remain unchanged. Preserve the single-node
failure-domain limitation; three local replicas are not node-loss HA.

No exact production diff can be sealed yet: broker/runtime execution, live
release rollback inputs, the EventStore production target, and required owner
records are missing. This is a concrete preparation sequence, not a runnable
production bundle with invented values. Story 3.19 owns the canonical
`deploy/dapr/production-profile.yaml`; this investigation does not create it.

## 3. Review and approve the production scope

Before a production approval request, require passing candidate/rehearsal
evidence; Story 3.17 inventory; Story 9.1 gate authorization; AD-26 ratification
by Architecture and Platform deployment; Story 5.7 component parity; Story 7.22
restore posture; Story 4.16 append-envelope proof; Story 5.13 activated catalogs;
and Story 3.19 publication/profile authority. A runtime minor change requires
fresh envelope proof. No failed/missing gate becomes an accepted exception.

Offer the owner the complete object diff and rollback drill results. Approval
must explicitly cover installing the selected broker and upgrading the shared
`dapr-system` release. Traffic/consumer migration requires a separately named
phase of that approval. If owners permit only an isolated canary, keep production
traffic and shared control-plane resources unchanged.

## 4. Approved maintenance sequence

After exact-bundle approval, re-run the read-only inventory and compare UIDs,
generations, replicas, image IDs, claims and endpoint. Any unreviewed drift stops
the operation. Use kubectl within the supported server skew. Check actual free
disk and ephemeral/pod capacity; allocatable CPU/memory are insufficient evidence.

Provision only approved broker objects; verify TLS, secret resolution, durable
queues/topics, positive/negative scopes and restore inputs with synthetic traffic.
Do not point existing production consumers at it. Pause admitted writes under
the approved drain procedure, establish the recorded consistency point and verify
off-node restore artifacts. Keep the old broker/state path intact.

Upgrade the existing `dapr` Helm release using the retained archive and reviewed
values, preserving all six scheduler/placement claims and the trust domain. A
future reviewed command has this exact release/context form, from the sealed
bundle directory:

```bash
helm upgrade dapr ./dapr-1.18.7.tgz --kube-context jpiquot@local --namespace dapr-system --values ./upgrade-values.yaml --wait --timeout 10m
kubectl --context=jpiquot@local --namespace=dapr-system rollout status deployment/dapr-operator --timeout=120s
kubectl --context=jpiquot@local --namespace=dapr-system rollout status deployment/dapr-sentry --timeout=120s
kubectl --context=jpiquot@local --namespace=dapr-system rollout status deployment/dapr-sidecar-injector --timeout=120s
kubectl --context=jpiquot@local --namespace=dapr-system rollout status statefulset/dapr-placement-server --timeout=120s
kubectl --context=jpiquot@local --namespace=dapr-system rollout status statefulset/dapr-scheduler-server --timeout=120s
```

These commands are **not authorized now**, and the reviewed values/bundle do
not exist yet. Avoid automatic Helm rollback: application/etcd data compatibility
requires the explicit recovery decision below. Do not delete/recreate a
StatefulSet to bypass immutable storage fields. Inspect actual pod image IDs,
scheduler/placement quorum and every existing Dapr application's synthetic health
control before allowing the target canary to restart.

Deploy only the approved immutable canary and its scoped components. Re-run the
target-path delivery/security/restart vectors, authenticated command/query
readback and relevant two-host regression without changing unrelated workloads.
Hold traffic until every gate passes. Activate the catalog/broker and traffic
through the approved single-authority transition: no uncontrolled dual publish,
no consumer offset reset, no inferred success from notifications. Record UTC,
image/profile digests, persisted cutover watermark and owner acknowledgement.

## 5. Stop and rollback

Stop immediately on any gate failure, 120-second rollout failure, unexpected
object/image/scope, scheduler or placement quorum loss, authenticated health
regression in another app, missing/mismatched/duplicate persisted effect,
unexplained event gap, DLQ capture failure, TLS/secret denial, or disk pressure.
Do not retry a command with unknown outcome as a fresh effect.

Before any new production writes, rollback removes the canary from traffic,
restores the approved previous workload/component/catalog specifications and
leaves the new broker disabled with its PVC retained. Reverse the shared Dapr
release using the exact prior sealed chart/values/revision and image IDs only
after rehearsal proves scheduler/placement data compatibility. Never use an
unqualified mutable tag or broad `rollout restart -A`.

After any new writes, image or Helm rollback alone is insufficient. Freeze and
drain traffic, retain both old/new broker ledgers and stores, reconcile persisted
MessageIds through Dapr, and choose the rehearsed compatibility reversal or the
recorded consistency-point restore. Restore in the approved order, including
scheduler state and read-model/checkpoint alignment. Prove command/query results
and duplicate handling before resuming. If downgrade compatibility is unknown,
remain paused for owner-led recovery; do not run old software against unproven
new persistent data. Report the approved RPO/RTO and actual recovery delta.

Do not delete old queues, offsets, PVCs, credentials, Secret generations or
backups during rollout/rollback. Retirement needs explicit later approval after
the observation window, reference inventory, drain and restore gates pass.
Retain before/after object comparisons and exact owned-resource cleanup receipts.
