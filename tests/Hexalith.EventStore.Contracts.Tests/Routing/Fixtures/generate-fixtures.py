"""Independent stdlib-only golden-vector producer; all policies and targets are synthetic.

Run from any directory with python3 generate-fixtures.py. The ASCII-only fixture uses
the same specified ordinal ordering without invoking the .NET implementation.
"""

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def declaration(route_id, kind, domain, message, projection, admission=None, fallback=()):
    return dict(
        routeId=route_id,
        kind=kind,
        domain=domain,
        messageType=message,
        projectionType=projection,
        contractVersion="v1",
        messageIdVersion=2 if kind == "command" else None,
        credentialKind="workload",
        operation="domain-service:" + {"command": "process", "query": "query", "projection": "project"}[kind],
        platformAdmission=admission,
        fallbackTypes=list(fallback),
    )


declarations = [
    declaration("counter-create", "command", "counter", "create-counter", None),
    declaration("counter-get", "query", "counter", "get-counter-status", "counter"),
    declaration("counter-project", "projection", "counter", None, "counter"),
    declaration("platform-tenant-create", "command", "tenants", "create-tenant", None,
                dict(namespace="platform:tenants", requiredHumanPermission="tenants:create")),
    declaration("query-fallback", "query", "counter", "*", "counter",
                fallback=("get-counter-history", "get-counter-status")),
]
bindings = [
    dict(routeId=d["routeId"], appId="fixture-tenants" if d["domain"] == "tenants" else "fixture-counter",
         method=d["operation"].split(":")[1], contractVersion="v1", isRuntimeOverride=False)
    for d in declarations
]
idempotency = []
for d in declarations:
    if d["kind"] != "command":
        continue
    schema = dict(schemaId="fixture:" + d["routeId"], version=1,
                  fields=[dict(name="aggregate-id", type="string"), dict(name="amount", type="int64")])
    entry = dict(
        routeId=d["routeId"], generation=7, domain=d["domain"], commandType=d["messageType"],
        adapterId="fixture:" + d["routeId"], operationId="fixture:" + d["messageType"], policyVersion="policy-v1",
        descriptorSchema=schema,
        descriptorDigest=digest(dict(purpose="idempotency-descriptor", schemaVersion=1, descriptor=schema)),
        retentionTier="mutation", activeDigestKeyGeneration="g2", readerDigestKeyGenerations=["g2", "g1"],
        secretStore="fixture-secrets", logicalSecretName="fixture-digest-ring", secretMapGeneration="map-v2",
        keyMap=[dict(generation="g1", mapKey="digest-g1"), dict(generation="g2", mapKey="digest-g2")],
        consumerAppId="fixture-eventstore",
    )
    entry["contentDigest"] = digest(dict(purpose="idempotency-entry", schemaVersion=1, entry=entry))
    idempotency.append(entry)

construction_input = dict(generation=7, allowRuntimeOverrides=False, declarations=declarations,
                          bindings=bindings, idempotency=idempotency)
(ROOT / "CompleteInput.json").write_text(json.dumps(construction_input, indent=2) + "\n")
routes = [
    dict(routeId=b["routeId"], generation=7,
         declarationDigest=digest(dict(purpose="route-declaration", schemaVersion=1, declaration=d)),
         appId=b["appId"], method=b["method"], contractVersion=b["contractVersion"],
         credentialKind=d["credentialKind"], operation=d["operation"], isRuntimeOverride=False)
    for d, b in zip(declarations, bindings)
]
envelope = dict(schemaVersion=1, generation=7, profile="Development", allowRuntimeOverrides=False,
                declarations=declarations, routes=routes, idempotency=idempotency)
envelope["routeFacetDigest"] = digest(dict(purpose="route-facet", schemaVersion=1, generation=7,
                                          declarations=declarations, routes=routes))
envelope["idempotencyFacetDigest"] = digest(dict(purpose="idempotency-facet", schemaVersion=1, generation=7,
                                                entries=idempotency))
(ROOT / "CanonicalCatalog.json").write_bytes(canonical(envelope))
digests = dict(root=hashlib.sha256(canonical(envelope)).hexdigest(), routeFacet=envelope["routeFacetDigest"],
               idempotencyFacet=envelope["idempotencyFacetDigest"], declaration=routes[0]["declarationDigest"],
               descriptor=idempotency[0]["descriptorDigest"], idempotencyEntry=idempotency[0]["contentDigest"])
(ROOT / "Digests.json").write_text(json.dumps(digests, indent=2) + "\n")
