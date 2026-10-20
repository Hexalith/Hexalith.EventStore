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
