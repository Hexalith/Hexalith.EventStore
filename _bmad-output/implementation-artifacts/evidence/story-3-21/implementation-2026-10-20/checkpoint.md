# Story 3.21 implementation checkpoint (2026-10-20)

Story remains unfinished. No owner ratification, production selection, canonical
profile, release/promotion, restore, Kubernetes or Story 4.16 proof is issued.

The retained attempt-11 packet has one observed PASS: RabbitMQ crash/redelivery
with the same CloudEvent/MessageId and one Dapr SQLite persisted application
effect after a process exit of 86. The other 23 matrix rows are explicit FAILs,
mostly blocked by local infrastructure. This is not a broker qualification pass.

Docker Desktop changed RabbitMQ's automatically allocated published port from
55849 to 55854 after restart. Components retained the original endpoint, so the
new sidecar exited with connection refused. Next work must retain a stable port
or rebind the new endpoint/secret generation deliberately, wait for broker health,
and recover the owned fixture between failed scenarios. Kafka's fixed published
port 49793 failed with Docker /forwards/expose HTTP 500 (docker exit 125); its
scenario matrix did not execute. Earlier attempts diagnosed/fixed OpenBao mount,
duplicate-listener and strict-CA issues, scheduler CLI flags, RabbitMQ cookie
ownership and exact queue grants, and Dapr's YAML resource filename requirement.

Kafka native retained-log expiry/replay after acknowledgement is not implemented;
the current TTL vector explicitly fails rather than infer it from Dapr expiry.
Further live runs must demonstrate durable quarantine backlog through broker
restart, actual denied diversion and credential-generation revocation, scopes,
TLS/missing-secret controls, and acknowledged-ID backpressure reconciliation.
Verifier hardening should independently bind every remaining security/fault
boolean to its native receipt (current counters, HTTP/state readback, diagnostics,
identity hashes and unrelated-container comparisons are bound). The current
explicit C# fixture drives this same Python lane; it compiled and was discovered
but its live test has not separately executed.

Verification at this checkpoint:

- `python3 -m unittest discover -s tools/tests -p test_dapr_broker_qualification.py`: 15 tests passed.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -m:1`: FAIL CS1704, duplicate Hexalith.Commons.UniqueIds source/package references; `/tmp/story-3-21-integration-build.log`.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj --configuration Debug --no-restore -m:1`: PASS, zero warnings/errors; `/tmp/story-3-21-integration-package-fallback.log`.
- Built assembly `-list tests -class '*DurableBrokerQualificationTests'`: discovers the explicit test.
- Attempt 11 producer: exit 1; packet verifier `--allow-failures`: integrity PASS, technical FAIL. `--require-production-authority`: expected exit 1.
- `actionlint .github/workflows/dapr-broker-qualification.yml`: PASS after replacing unavailable job-env runner context.
- `git diff --check`: PASS.

All attempt-11 owned Docker resources and native processes were removed. The
retained before/after container specification hashes agree, and cleanup reports
no owned resource remaining. Production Kubernetes was never contacted by this
runner. Existing authority bytes, sealed ci.yml/integration.yml, Builds pins and
Story 2.13 evidence were not edited. Unrelated concurrent workspace changes were
preserved; this packet makes no claim that their authoring stopped.

The Story 3.17 JSON is a proposed candidate inventory handoff, not an accepted
register entry. Story 5.9 guide drift remains the drift already retained in the
inspection packet (CI/Builds runtime 1.18.2/CLI 1.18.0; guide examples 1.18.0,
1.14.4 and latest). Story 5.7/3.19 binding and an exact production diff/restore
bundle remain blocked on owner records, protected previous-release inputs and
Kubernetes/restore proof. Refer to the unchanged inspection rollout-rollback.md.
