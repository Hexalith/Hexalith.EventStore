#!/usr/bin/env python3
"""Prepare non-authorizing Story 6.6 candidates from repository and Release build facts.

These source-derived drafts intentionally leave undeclared normative catalog,
deployment, cryptographic, native/runtime and qualification inputs unresolved.
They never write the canonical production profile or routing catalog slots.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess


ROOT = pathlib.Path(__file__).resolve().parents[1]
SAMPLE = pathlib.Path("samples/Hexalith.EventStore.Sample")
BASELINE = "1329b35e52852952ecb2c94aabf100674e9691e3"


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def fact(relative: pathlib.Path | str) -> dict:
    path = ROOT / relative
    data = path.read_bytes()
    return {"path": str(relative), "sha256": digest(data), "bytes": len(data)}


def text(relative: pathlib.Path | str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8")


def constant(relative: pathlib.Path, member: str) -> str | None:
    match = re.search(rf'\b{re.escape(member)}\s*=>\s*"([^"\n]+)"', text(relative))
    return match.group(1) if match else None


def artifact_inventory(project: pathlib.Path) -> dict:
    directory = ROOT / project / "bin/Release/net10.0"
    if not directory.is_dir():
        raise RuntimeError(f"Required Release output is missing: {directory}")
    artifacts = [fact(path.relative_to(ROOT)) for path in sorted(directory.rglob("*"))
                 if path.is_file() and (path.suffix in {".dll", ".so", ".dylib"}
                                       or path.name.endswith((".deps.json", ".runtimeconfig.json")))]
    dependencies = []
    for deps_path in sorted(directory.glob("*.deps.json")):
        document = json.loads(deps_path.read_text())
        dependencies.append({"source": fact(deps_path.relative_to(ROOT)),
                             "runtimeTarget": document.get("runtimeTarget"),
                             "libraries": document.get("libraries", {}),
                             "declaredTargets": document.get("targets", {})})
    return {"project": str(project), "configuration": "Release", "artifacts": artifacts,
            "depsDeclarations": dependencies, "loadedImageIdentityProven": False,
            "completeNativeOrDynamicGraphProven": False,
            "assemblyMetadataVersions": "Not inferred from package version labels; unresolved until exact-image admission."}


def ratification_subject() -> dict:
    relative = pathlib.Path("_bmad-output/planning-artifacts/architecture.md")
    data = (ROOT / relative).read_bytes()
    if b"\r" in data:
        raise RuntimeError("Ratification-subject source must have exact LF bytes")
    lines = data.decode("utf-8").splitlines(keepends=True)
    append_start = next(i for i, line in enumerate(lines) if line.startswith("**Append race (NFR7 class (c)).**"))
    append_end = next(i for i in range(append_start + 1, len(lines)) if not lines[i].strip())
    profile_start = next(i for i, line in enumerate(lines) if line.startswith("### AD-26 "))
    profile_end = next(i for i in range(profile_start + 1, len(lines))
                       if lines[i].startswith(("### ", "## ")))
    while not lines[profile_end - 1].strip():
        profile_end -= 1
    section = lines[profile_start:profile_end]
    section[0] = section[0].replace(" [ASSUMPTION]", "").replace(" [ADOPTED]", "")
    subject = "".join(lines[append_start:append_end] + section).encode("utf-8")
    return {"source": fact(relative), "subjectSha256": digest(subject),
            "authenticatedArchitectureRecord": None, "authenticatedPlatformDeploymentRecord": None}


def write(output: pathlib.Path, name: str, document: dict) -> dict:
    encoded = (json.dumps(document, indent=2, sort_keys=True) + "\n").encode("utf-8")
    (output / name).write_bytes(encoded)
    return {"path": name, "candidateContentSha256": digest(encoded), "bytes": len(encoded)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()
    output = args.output.resolve()
    if output == ROOT / "deploy/dapr" or (ROOT / "deploy/dapr") in output.parents:
        parser.error("Candidates must remain outside the authoritative deploy/dapr inventory slots")
    output.mkdir(parents=True, exist_ok=True)
    common = {"draftSchema": "story-6-6-source-candidate/1", "status": "candidate-unreviewed-unqualified",
              "baselineCommit": BASELINE,
              "observedHead": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
              "approvalAuthority": False, "readinessAuthority": False, "productionAuthority": False,
              "activationAuthority": False}
    events = []
    for domain in ("Counter", "Greeting"):
        for relative in sorted((ROOT / SAMPLE / domain / "Events").glob("*.cs")):
            source = relative.read_text()
            name = relative.stem
            if not re.search(rf'\bsealed record {re.escape(name)}\s*:\s*I(?:EventPayload|RejectionEvent)\s*;', source):
                raise RuntimeError(f"Marker declaration changed; inspect it before proposing bounds: {relative}")
            full_name = f"Hexalith.EventStore.Sample.{domain}.Events.{name}"
            events.append({"domain": domain.lower(), "aggregateRoute": domain + "Aggregate",
                           "currentClrType": full_name, "source": fact(relative.relative_to(ROOT)),
                           "knownWriterAlias": full_name, "additionalFiniteAliasCandidate": name,
                           "legacyResolverBoundary": "Legacy suffix fallback is open-ended; retained history needs an actual alias inventory.",
                           "writerMode": "V1", "serializationFormat": "json", "maximumPayloadBytes": 2,
                           "exactLegacyPayloadHex": "7b7d", "declaredEventContractType": None,
                           "proposedEventContractType": re.sub(r"(?<!^)(?=[A-Z])", "-", name).lower(),
                           "declaredPayloadVersion": None, "proposedPayloadVersion": 1,
                           "proposedSchema": {"type": "object", "properties": {}, "additionalProperties": False},
                           "schemaValidatorBinding": None, "identityValidatorBinding": None,
                           "canonicalOptionsManifest": None, "canonicalDVAERows": None})
    events.append({"domain": "counter", "aggregateRoute": "CounterAggregate",
                   "currentClrType": "Hexalith.EventStore.Contracts.Events.AggregateTerminated",
                   "source": fact("src/Hexalith.EventStore.Contracts/Events/AggregateTerminated.cs"),
                   "writerDeclaration": fact(SAMPLE / "Counter/CounterEventSerialization.cs"),
                   "knownWriterAlias": "Hexalith.EventStore.Contracts.Events.AggregateTerminated",
                   "additionalFiniteAliasCandidate": "AggregateTerminated", "writerMode": "V1",
                   "serializationFormat": "json", "maximumPayloadBytes": 309,
                   "proposedEventContractType": "counter-aggregate-terminated", "proposedPayloadVersion": 1,
                   "adapterScope": "CounterAggregate only; ASCII AggregateIdentity aggregateId bound at 256 bytes.",
                   "schemaValidatorBinding": None, "identityValidatorBinding": None,
                   "canonicalOptionsManifest": None, "canonicalDVAERows": None})
    command_sources = sorted((ROOT / "samples/Hexalith.EventStore.Sample.Contracts/Counter/Commands").glob("*.cs"))
    command_sources += sorted((ROOT / SAMPLE / "Greeting/Commands").glob("*.cs"))
    commands = [{"domain": constant(path.relative_to(ROOT), "Domain") or path.parent.parent.name.lower(),
                 "messageType": constant(path.relative_to(ROOT), "CommandType") or path.stem,
                 "declaredContractDomain": constant(path.relative_to(ROOT), "Domain"),
                 "declaredContractMessageType": constant(path.relative_to(ROOT), "CommandType"),
                 "identitySource": "ICommandContract metadata" if constant(path.relative_to(ROOT), "CommandType")
                     else "Existing aggregate naming convention and reflection dispatch; no ICommandContract metadata",
                 "legacyClrName": path.stem, "source": fact(path.relative_to(ROOT)),
                 "proposedAppId": "sample", "method": "/process", "credentialKind": "workload",
                 "operation": "domain-service:process", "approvedContractVersion": None}
                for path in command_sources]
    sources = [fact(path) for path in (
        SAMPLE / "Program.cs", SAMPLE / "Counter/CounterEventSerialization.cs",
        SAMPLE / "Greeting/GreetingEventSerialization.cs", SAMPLE / "Counter/CounterAggregate.cs",
        SAMPLE / "Greeting/GreetingAggregate.cs", SAMPLE / "Counter/State/CounterState.cs",
        SAMPLE / "Greeting/State/GreetingState.cs", SAMPLE / "Counter/Projections/CounterProjection.cs",
        SAMPLE / "Counter/Projections/CounterProjectionHandler.cs", "src/Hexalith.EventStore.AppHost/Program.cs",
        "src/Hexalith.EventStore.DomainService/EventStoreDomainServicePolicies.cs",
        "src/Hexalith.EventStore.ServiceDefaults/Authentication/EventStoreWorkloadOperations.cs",
        "src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs",
        "src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs",
        "src/Hexalith.EventStore.Client/Handlers/DetachedStateCapture.cs",
        "src/Hexalith.EventStore.Client/Handlers/DomainProcessorBase.cs",
        "src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs",
        "src/Hexalith.EventStore.Client/Handlers/LegacyCommandReplayInput.cs",
        "src/Hexalith.EventStore.Client/Events/EventEvolutionProofFraming.cs",
        "src/Hexalith.EventStore.Client/Events/UnverifiedEventEvolutionProofFrame.cs",
        "src/Hexalith.EventStore.Server/Events/EventLogicalDigest.cs",
        "src/Hexalith.EventStore.Server/Events/DaprProductionLogicalEventReader.cs",
        "references/Hexalith.Platform/apphost.cs", "references/Hexalith.Platform/DaprComponents/statestore.yaml",
        "references/Hexalith.Platform/DaprComponents/pubsub.yaml",
        "references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md")]
    catalogs = {**common, "scope": "Root-owned Sample Counter and Greeting plus inspected shared implementation sources; other modules are not invented.",
                "sources": sources, "events": events, "commandRoutes": commands,
                "typedSnapshotCaptureDeclarations": [{"domain": domain,
                    "stateClrType": f"Hexalith.EventStore.Sample.{name}.State.{name}State",
                    "scalarMembers": fields, "maximumCombinedSourceAndCopyGraphChargeBytes": int(
                        re.search(r"_snapshotCapture\s*=\s*new\((\d+)", text(SAMPLE / name / f"{name}Aggregate.cs")).group(1)),
                    "scope": "Optional legacy command-state capture; owner-declared fixed scalar-only copy",
                    "canonicalStateSerializerDeclared": False, "genericGraphIsolationProven": False}
                    for domain, name, fields in (("counter", "Counter", ["Count:Int32", "IsTerminated:Boolean"]),
                                                 ("greeting", "Greeting", ["MessageCount:Int32"]))],
                "proofPreparation": {"outerFraming": "HX-EV-PROOF-1 codec 01; opaque claims",
                    "semanticClaimVerification": False, "signatureVerification": False, "runtimeRegistration": False,
                    "historicalStoredDigestEqualsApplicationLogicalDigest": False,
                    "requiredDesignInput": "Distinct Dapr logical claim model/carrier, source binding and signing-purpose/trust decision"},
                "projectionRoutes": [{"domain": "counter", "projectionType": "counter", "proposedAppId": "sample",
                    "method": "/project", "credentialKind": "workload", "operation": "domain-service:project",
                    "handlerClrType": "Hexalith.EventStore.Sample.Counter.Projections.CounterProjection",
                    "handlerRouteId": None, "verifiedHandlerCapability": False,
                    "compatibilityWarning": "Existing suffix-only projection skips rejection/unknown events; no verified evolved route is inferred."}],
                "requiredUnresolved": ["Complete reviewed D/V/A/E/F/S canonical rows and schema/options/identity implementations",
                    "Retained event alias/version inventory, including legacy suffix names",
                    "All deployed module handlers, registration adapters, filters, receipt providers and codecs",
                    "Complete managed/native/dynamic graph, loader contexts and immutable execution binding",
                    "Authoritative serving-peer inventory and gateway pins"]}
    records = [write(output, "event-evolution-catalog.candidate.json", catalogs)]
    peers = {**common, "catalogCandidateContentSha256": records[0]["candidateContentSha256"],
             "registryFingerprint": None, "handlerCompatibilityHashes": None, "eventTransformHashes": None,
             "releaseReviewIdentity": None, "immutableReleaseArtifactIdentity": None,
             "sourceTopology": [{"appId": "sample", "domains": ["counter", "greeting"], "purpose": "Root development sample domain-service host"},
                 {"appId": "eventstore", "purpose": "Gateway/aggregate actor host; Platform composed image is a separate subject"},
                 {"appId": "works", "purpose": "Inspected Platform development route, owned by an external module"},
                 {"appId": "eventstore-admin", "purpose": "Admin diagnostics"},
                 {"appId": "eventstore-operations", "purpose": "Platform development subscriber dead-letter actor host"}],
             "localReleaseArtifacts": [artifact_inventory(SAMPLE), artifact_inventory(pathlib.Path("src/Hexalith.EventStore.DomainService")),
                                       artifact_inventory(pathlib.Path("src/Hexalith.EventStore.Client"))],
             "requiredUnresolved": ["Exact deployed peers, images and immutable artifact paths; source topology is not a serving inventory",
                 "Canonical complete registry/handler/transform identities after catalog review",
                 "Signed gateway/peer pin carrier, key IDs and trust intervals", "Loaded framework/native image origin and ABI qualification"]}
    records.append(write(output, "serving-peer-pins.candidate.json", peers))
    template_sources = [fact(path) for path in (
        "deploy/dapr/statestore-postgresql.yaml", "deploy/dapr/pubsub-rabbitmq.yaml", "deploy/dapr/pubsub-kafka.yaml",
        "deploy/dapr/pubsub-servicebus.yaml", "deploy/dapr/resiliency.yaml", "deploy/dapr/accesscontrol.yaml",
        "deploy/dapr/accesscontrol.sample.yaml", "deploy/dapr/accesscontrol.eventstore-admin.yaml")]
    profile = {**common, "encoding": "JSON is a YAML 1.2 subset; this draft does not claim the absent canonical profile schema.",
               "ratification": ratification_subject(), "targetSource": "AD-26 unratified target; no environment selected",
               "proposedMode": "Kubernetes/per-application-Dapr-sidecars", "requiredDaprRuntimeFloor": "1.18.3",
               "observedLocalTooling": {"command": ["dapr", "--version"],
                    "output": subprocess.check_output(["dapr", "--version"], cwd=ROOT, text=True).strip(),
                    "productionImageDigest": None, "productionCliCompatibility": None},
               "componentTemplates": template_sources,
               "actorStateCandidate": {"type": "state.postgresql", "version": "v1", "actorStateStore": True,
                    "requiredLayout": "One stable component per actor-hosting app ID, scoped to that app alone",
                    "templateConflict": "Tracked PostgreSQL template also scopes eventstore-admin; split/narrow before qualification",
                    "actorHostingAppIds": None, "componentNames": None, "namespaces": None, "physicalTargets": None},
               "brokerCandidateAlternatives": ["pubsub.rabbitmq/v1", "pubsub.kafka/v1", "pubsub.azure.servicebus.topics/v1"],
               "selectedBroker": None, "excludedProductionTargets": ["state.redis", "pubsub.redis", "state.azure.cosmosdb"],
               "apiTokenRequired": True, "appChannelTokenRequired": True,
               "freshnessReplicaPostureCandidate": "One eventstore replica, or no multi-replica freshness claim",
               "routeCatalogCandidateContentSha256": records[0]["candidateContentSha256"],
               "peerCandidateContentSha256": records[1]["candidateContentSha256"],
               "scheduler": None, "placement": None, "scopesAndAcls": None, "appendOperatingEnvelope": None,
               "openBaoContractDigest": None, "openBaoServerFloorCandidates": ["2.7.1", "2.6.4"],
               "openBaoServerFloorSource": "architecture.md specified dependencies table; branch floor alternatives, not a selected pin",
               "selectedOpenBaoServerPin": None, "workloadAssertionIssuer": None,
               "globalAdministratorBootstrapCredentialReference": None, "idempotencyCatalogDigest": None,
               "profileScopedAd34InventoryDigest": None, "restorePosture": None,
               "requiredEvidence": ["Authenticated Architecture and Platform deployment ratification records",
                    "Pinned production runtime/image and CLI compatibility", "No-second-writer append envelope and placement/failover proof",
                    "Exact per-app PostgreSQL component topology and Dapr ETag/transaction/lost-acknowledgment/restart probes",
                    "Broker membership/acceptance/disable-reject and effect idempotency qualification",
                    "OpenBao and workload/bootstrap identity bindings", "Activated canonical route/idempotency catalogs",
                    "Restore consistency/order for every state role and scheduler", "Two-host/shared-backend and second-supported-component evidence",
                    "Complete consumer/compatibility/fleet gates and sealed transition validators"]}
    records.append(write(output, "production-profile.candidate.yaml", profile))
    receipt = {**common, "generator": fact("scripts/prepare-event-evolution-candidates.py"), "candidateFiles": records,
               "canonicalProductionProfileWritten": False, "canonicalRoutingCatalogWritten": False}
    write(output, "candidate-generation.json", receipt)
    print(json.dumps({"result": "prepared", "candidates": records, "activationAuthority": False}, sort_keys=True))


if __name__ == "__main__":
    main()
