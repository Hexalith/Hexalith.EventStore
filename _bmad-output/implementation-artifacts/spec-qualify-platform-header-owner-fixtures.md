---
title: Qualify platform routing-header ownership and immutable shell recorder
status: verified-awaiting-protected-checks
---

PR376 head6bcd40e3158d5b99e8d94cd300575768a6e479ab CI41223264277 found two test-fixture failures. The Client source inventory omitted the SDK-owned RouteCatalogInvocationHandler after its actual authenticated owner-state transport was added. The exact allowed-source set now includes that platform handler; host-local ownership remains refused and both app-id/API-token setter negatives run. No production handler or authentication policy changes.

The Contracts forwarding fixture generated an executable recorder immediately before exec. Linux returned126 with `bad interpreter: Text file busy`. Both posture/count helpers now execute one immutable checked-in recorder with the same argument/environment recording behavior. Production preflight and all reservation, authority, source, package-count and forwarding assertions remain unchanged. The unchanged53-case class had also passed locally before this permanent fixture correction.

Concurrent PR updates d17a1e4ca/e682b7d93 were fast-forwarded intact, preserving additional large-body/ESRCH fixture controls. Verification: individual Client and Contracts Release builds with warnings-as-errors both pass with zero warnings/errors (Version3.130.0, external package mode, explicit root-declared Builds props). The exact DaprRoutingHeaderOwnershipGuardTests class passes4/4 including independent unnamed-host setters for both header names; ContainerPublishingGovernanceTests passes53/53 with the immutable recorder, zero failures/skips. Logs /tmp/es-header-owner-client-{build,tests}.log and /tmp/es-immutable-recorder-{build,tests}.log; XML siblings retain actual execution. Protected PR/main gates remain required before publication.

## File Scope

Allowed files for this story:

- `tests/Hexalith.EventStore.Client.Tests/Registration/DaprRoutingHeaderOwnershipGuardTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContainerPublishingGovernanceTests.cs`
- `tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/record-publication-preflight.sh`
- `_bmad-output/implementation-artifacts/spec-qualify-platform-header-owner-fixtures.md`
