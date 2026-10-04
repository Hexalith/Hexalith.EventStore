#!/usr/bin/env python3
"""Retain a finite, fail-closed P1R investigation; never grant owner acceptance."""
from __future__ import annotations

import argparse
import base64
import datetime as dt
import hashlib
import functools
import contextlib
import io
import json
import os
import pathlib
import re
import shutil
import signal
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[4]
PROJECTS = ROOT.parents[1]
VERSIONS = ("3.110.0", "3.70.1")
SOURCES = {"selected": "27279fe6431925a6ea046c3f89af61487185c7de", "rollback_tag": "f13f9925fdca53efa2ab8c90d396ab106f91bb9c", "rollback_archive": "650faf053a98ed1c03c048b8e0d2e4d281b095cf", "current": "2c58ffda41759e895ace4b9625c9bd931a217672", "projects_baseline": "cbcf54fa4d7a8c17bbfc3f9fb555ac0a85c179c0", "current_builds": "688eec9a4333245cc0ff7772115c769094471863", "selected_builds": "21ce044ab465ccb2adab58b3d66e394ffbecf3c2", "rollback_builds": "7af20f8bafbfe561df6f7705913a0800603090b5"}
SOURCE_REPOSITORIES = {"Hexalith.EventStore": SOURCES["current"], "Hexalith.Builds": SOURCES["current_builds"], "Hexalith.Commons": "116d26815eb81e35b3c161e1799e5ee12805fc0a"}
POSTGRES = "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
RUNTIME = "daprio/dapr:1.18.2"
# Exact archive-derived maps are fixture trust anchors, independently checked against
# fresh signed NuGet archives before any packet-declared hashes are accepted.
PACKAGE_IDS = frozenset("Hexalith.EventStore." + suffix for suffix in ("Server", "Contracts", "Client", "DomainService", "ServiceDefaults"))
PROBE_ASSEMBLIES = frozenset(("Hexalith.EventStore.Server", "Hexalith.EventStore.Contracts"))
HOST_REQUIRED = PROBE_ASSEMBLIES | {"Hexalith.EventStore.Client"}
PUBLISHED_ARCHIVE_HASHES = {
    "3.110.0": {
        "Hexalith.EventStore.Client": "2085e60de82eefb00d0e76ea48438ecbcb14f610b9f65dc926feb1886af845b1",
        "Hexalith.EventStore.Contracts": "5c32eada89b3d6424f89cb01929e5e18d3240c463f1f1a6e32a5fb380bd4d6f2",
        "Hexalith.EventStore.DomainService": "ead5dfabb5d4cfc90739fa414b3c2f8806d388c8237f74737a864d3b7b518e93",
        "Hexalith.EventStore.Server": "ca676768798b2dd69035eaef9dce92a8a55db2c77d44ce3b0dda216fb7ed776a",
        "Hexalith.EventStore.ServiceDefaults": "7f23addbfca231eac9c2a6b3cc73d9db82c7674478e5f5c79f90a73d7abeb5c4",
    },
    "3.70.1": {
        "Hexalith.EventStore.Client": "2399a970ca7f6685e04e1d5bad06f7a7dec88de9e2f767f32df2371f2e599601",
        "Hexalith.EventStore.Contracts": "ee6cb34319615cf2612b75c6298aad3c1862ee07ce3cb78fa39deee1c62b7c1a",
        "Hexalith.EventStore.DomainService": "e9377c4cf418dbb73e30eefaab98f0ba4381e1ce8dd6108225f348ab56b7810f",
        "Hexalith.EventStore.Server": "a9b1f8e8b83b069421951f386572042f67ef31a58f024310b6ff9ad794567f74",
        "Hexalith.EventStore.ServiceDefaults": "64b1b32359e04c6b3523fd8135f373bfa6bbcc3346bcda6c744de053519397d6",
    },
}
PUBLISHED_DLL_HASHES = {
    "3.110.0": {
        "Hexalith.EventStore.Client": "2e4e148f44db53f0243d867f06a324bdc5e8143ea80725a04d40de7a9e09163a",
        "Hexalith.EventStore.Contracts": "f08388cf7eca84bb74426419758b4bbf1c4b152151721f97a0949e6bb2dc871b",
        "Hexalith.EventStore.DomainService": "598d55807f7f0bdf1092cfb8d5f5a3db1086f71325b4c200a6f54d8cea4a913b",
        "Hexalith.EventStore.Server": "ce5b825ad3251b3271e72675f6f58daba035b10f5a01563d2b802809acb447ca",
        "Hexalith.EventStore.ServiceDefaults": "fcb84dbbcf1e4e17b437972a3c520236b78d22ff1d2e2fb6109bd553b1a4a9fa",
    },
    "3.70.1": {
        "Hexalith.EventStore.Client": "8ab986541e1a5c71dd87bfecb8d78175507e36f31150b069ade429474c2a4f7c",
        "Hexalith.EventStore.Contracts": "bc5472a201f1a075ded0f945660f39a88e1763f8f7a7ba6fc8e944dba162a77d",
        "Hexalith.EventStore.DomainService": "c7979ba7cbda3852e920fa9406a5e0e4f29d85a172e68ce8ddde37f8939339e0",
        "Hexalith.EventStore.Server": "8f3284d47eeae4aa8d4886f81ce581dc2445b7e486bf6ef5cecfbdc30eb6b9da",
        "Hexalith.EventStore.ServiceDefaults": "3d0b3bdbcca1d210e6e501c5c3d8516c378af2f18c65b692ce921adbe4c55022",
    },
}
PUBLISHED_CONTENT_HASHES = {'3.110.0': {'Hexalith.EventStore.Client': 'OSjRL4VNdN7U40CQKxVWeboQV4Go4mnXm5a6zuKsyMOz/1/qmq/g6VRbGyEN1wN9R6nDElv2RHeV754BlQvjjg==', 'Hexalith.EventStore.Contracts': 'baYsmElIPNyTHly6B18qIldqyV3IQ10znHmYON+AKeuL3xyvacz2Jrn9xFMY8b2C/Ow874JYCkq0o6x/NbcUsQ==', 'Hexalith.EventStore.DomainService': 'HWvcNBAfLqxjZ2w7aOABVaZv2WXn7W3aox1GHjq5zFQe1UjXW3en/aiPazsa8ccXCUfXE+WqrA+Q1YNABKSmHw==', 'Hexalith.EventStore.Server': 'ag4Svmfv94X4DxxTRAiyJPQZM0IXxrH40JaOtTx5RTCfh2ASaKHf9aIwWJGSOL4pWHGqd5w3apvu4or1PYzoxg==', 'Hexalith.EventStore.ServiceDefaults': 'oz8knL5wa0XjWO1FcvTASXgpew08t+pEN21lqn7dC6rvIYls9CeQ72KeRgQndEhN4DSGbupWBkGAIhLa9yd7Mg=='}, '3.70.1': {'Hexalith.EventStore.Client': 'UjVLImHNmOZkgc0VkzeFyWFLnCrqYZ967RH4PF1caD/xgsrMsRwtLtd6+d6JQs8f5eLTJyAvYinnguRqg+zYiw==', 'Hexalith.EventStore.Contracts': 'D46Ujom2BaRA9dliNQEDkddSI+aETQX56bfK6RnHSGOTJnrK5MdUvvg4txZJLc/8OvhMEuRhxwHbS96O+gKo0A==', 'Hexalith.EventStore.Server': 'dDmVa7LxtzWyEsDYeyqfYaHAu7r0twGmagaCjVM+n9kFFMpAC5v7rWaIOi+CEiQUEkT/6kC5sXgHP0kYIy7tFg==', 'Hexalith.EventStore.DomainService': 'M0SprdUzSyPnfvFAIMHq2BT7HfxnR7G77DT26JnPxIFhBR3ysov7hkuaCUGVi+i5TfMLieEng+++UjdoaxNIjQ==', 'Hexalith.EventStore.ServiceDefaults': '8SwIg3ok4Tb+rQnVJziGGZV0OSDRrHEinn5TUXX8zu2KdyWFAmZkdWb9nwjQ+xTvn/G9qznYBKH+O0oMupQGVQ=='}}
ROLLBACK_HASHES = PUBLISHED_ARCHIVE_HASHES["3.70.1"]
ASSEMBLY_INVENTORY_SCRIPT = "import hashlib,json,pathlib,sys; print(json.dumps({p.stem:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(pathlib.Path(sys.argv[1]).glob('Hexalith.EventStore.*.dll'))},sort_keys=True))"
ARTIFACT_INVENTORY_SCRIPT = "import hashlib,json,pathlib,sys; print(json.dumps({str(pathlib.Path(p)):hashlib.sha256(json.dumps(json.loads(pathlib.Path(p).read_text()),sort_keys=True,separators=(',',':')).encode()).hexdigest() for p in sys.argv[1:-1]},sort_keys=True))"
FILE_HASH_SCRIPT = "import hashlib,pathlib,sys; print(hashlib.sha256(pathlib.Path(sys.argv[1]).read_bytes()).hexdigest())"
SOURCE_CLOSURE_SCRIPT = r"""import hashlib,json,pathlib,subprocess,sys
root=pathlib.Path(sys.argv[1]).resolve(); coordinate=sys.argv[2]
def git(*args): return subprocess.check_output(['git',*args],cwd=root)
assert git('rev-parse','HEAD').decode().strip()==coordinate, 'Source coordinate changed'
def relevant(name):
 p=pathlib.PurePosixPath(name)
 return (p.parts[0] in ('src','Props','Targets') or len(p.parts)==1) and (p.suffix.lower() in ('.cs','.csproj','.props','.targets','.json','.config','.resx','.ruleset') or p.name=='.editorconfig') and not any(x in ('bin','obj') for x in p.parts)
files={}
for item in git('ls-tree','-rz',coordinate).split(b'\0'):
 if not item: continue
 metadata,name=item.split(b'\t',1); name=name.decode(); mode,kind,blob=metadata.decode().split()
 if kind!='blob' or not relevant(name): continue
 path=root/name
 assert path.is_file() and not path.is_symlink(), 'Missing or substituted source input: '+name
 assert git('hash-object','--path',name,str(path)).decode().strip()==blob, 'Dirty source input: '+name
 files[name]={'git_blob':blob,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'committed_sha256':hashlib.sha256(git('show',coordinate+':'+name)).hexdigest()}
for base in (root,root/'src',root/'Props',root/'Targets'):
 if not base.exists(): continue
 paths=base.iterdir() if base==root else base.rglob('*')
 for path in paths:
  name=path.relative_to(root).as_posix()
  if path.is_file() and relevant(name): assert name in files, 'Untracked build input: '+name
assert files, 'Empty source closure'
print(json.dumps({'coordinate':coordinate,'repository':str(root),'files':files},sort_keys=True))
"""
INVENTORY_SQL = "select coalesce(json_agg(json_build_object('key',key,'sha256',encode(sha256(convert_to(value::text,'UTF8')),'hex'),'sequence',case when key like '%:metadata' then value->>'currentSequence' else null end,'floor',case when key like '%:metadata' then value->>'retainedFloor' else null end,'snapshotSequence',case when key like '%:snapshot' then value->>'sequenceNumber' else null end) order by key),'[]') from state;"
DOMAIN_REQUIRED = PACKAGE_IDS - {"Hexalith.EventStore.Server"}

RESOURCE_INSPECT_FORMAT = '{"id":{{json .Id}},"image":{{json .Image}},"running":{{json .State.Running}},"started":{{json .State.StartedAt}},"invocation":{{json (index .Config.Labels "hexalith.p1r.invocation")}}}'
OWNERSHIP_INSPECT_FORMAT = '{"id":{{json .Id}},"invocation":{{json (index .Config.Labels "hexalith.p1r.invocation")}}}'


def graph_packages(lane, project):
    if lane == "3.70.1" and project in {"Host", "Probe"}:
        return HOST_REQUIRED
    return PACKAGE_IDS - {"Hexalith.EventStore.Server"} if project == "Domain" else PACKAGE_IDS
FIXTURES = frozenset((".editorconfig", "NuGet.Config", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "run_verification.py", "test_run_verification.py", "host/Host.csproj", "host/Program.cs", "domain/Domain.csproj", "domain/Program.cs", "domain/CounterAggregate.cs", "domain/CounterState.cs", "domain/IncrementCounter.cs", "domain/AssertCounter.cs", "domain/CounterIncremented.cs", "domain/CursorQueryHandler.cs", "probe/Probe.csproj", "probe/Program.cs"))
SCENARIOS = ("provenance", "legacy-metadata", "metadata-read", "metadata-write", "full-replay", "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "invalid-evidence", "query-wire", "projection-wire", "mixed-api", "checkout", "post-upgrade-restore", "pre-upgrade-restore", "failure-cleanup")
EXPECTED_CASES = {
    "provenance": set(VERSIONS),
    "legacy-metadata": {f"{c}-floor-None" for c in ("pascal", "web")},
    "metadata-read": {f"{c}-floor-{f}" for c in ("pascal", "web") for f in (1, 5)},
    "metadata-write": {"3.110.0-to-3.70.1"},
    "checkout": {"current-build", "legacy-metadata", "query-wire", "projection-wire", "full-replay", "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "metadata-write", "invalid-evidence"},
    "invalid-evidence": {f"{v}-{m}" for v in VERSIONS for m in ("invalid-floor", "unreadable", "protected", "unknown-type", "unknown-version")},
    "query-wire": {"-".join(d) + "-" + f + "-" + s for d in (VERSIONS, tuple(reversed(VERSIONS))) for f in ("json", "xml") for s in ("legacy", "dual")},
    "projection-wire": {"-".join(d) + "-json-positive" for d in (VERSIONS, tuple(reversed(VERSIONS)))},
    "mixed-api": {"3.110.0-client-3.70.1-host", "3.70.1-client-3.110.0-host", "status-downgrade", "cursor-downgrade", "cursor-upgrade", "old-cursor-scope", "selected-cursor-scope", "old-dispatcher-ProcessFencedCommandAsync", "old-dispatcher-ProcessTrustedEffectAsync", "old-dispatcher-GetRetainedFloorAsync", "selected-dispatcher-ProcessFencedCommandAsync", "selected-dispatcher-ProcessTrustedEffectAsync", "selected-dispatcher-GetRetainedFloorAsync"},
    "post-upgrade-restore": {"quiesced-full-backup"},
    "pre-upgrade-restore": {"quiesced-full-backup"},
    "failure-cleanup": {"startup-failure", "timeout", "cancellation"},
}
for _scenario in ("full-replay", "snapshot-tail", "retained-covered"):
    EXPECTED_CASES[_scenario] = {"3.110.0-to-3.70.1", "3.70.1-to-3.110.0"}
for _scenario, _variants in (("retained-uncovered", ("absent", "non-covering")), ("missing-event", ("interior", "tail"))):
    EXPECTED_CASES[_scenario] = {f"{a}-to-{b}-{v}" for a, b in (VERSIONS, tuple(reversed(VERSIONS))) for v in _variants}
SECRET = re.compile(r"(?i)(?<![a-z0-9_.])(?:POSTGRES_PASSWORD|password|authorization|bearer|api[_-]?key|access[_-]?token)[\"\']?\s*(?:=|:)\s*[\"\']?(?!\[redacted\])[^\s\"},]+")
AUTHORIZATION = re.compile(r"(?i)(?<![a-z0-9_.])authorization[\"\']?\s*[:=]\s*[\"\']?(?:bearer|basic)\s+[a-z0-9._~+/=-]+")
BEARER = re.compile(r"(?i)(?<![a-z0-9_.])bearer\s+[a-z0-9._~+/=-]+")


def expected_cases(name, source=False):
    if not source:
        return EXPECTED_CASES[name]
    if name == "metadata-write":
        return {"3.110.0-to-current", "current-to-3.110.0"}
    if name == "invalid-evidence":
        return {"current-"+m for m in ("invalid-floor", "unreadable", "protected", "unknown-type", "unknown-version")}
    return {c.replace("3.70.1", "current") for c in EXPECTED_CASES[name]}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode()


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")



@contextlib.contextmanager
def defer_cancellation():
    """Register a new child before delivering cancellation to its owner."""
    pending = []
    handlers = {s: signal.getsignal(s) for s in (signal.SIGINT, signal.SIGALRM) if callable(signal.getsignal(s))}
    try:
        for signum in handlers:
            signal.signal(signum, lambda s, frame: pending.append((s, frame)))
        yield
    finally:
        for signum, handler in handlers.items():
            signal.signal(signum, handler)
        if pending:
            signum, frame = pending[0]
            handlers[signum](signum, frame)


def safe(value):
    rendered = value if isinstance(value, str) else json.dumps(value)
    require(not SECRET.search(rendered) and not BEARER.search(rendered), "Secret-bearing receipt rejected")
    if isinstance(value, dict):
        for child in value.values():
            safe(child)
    elif isinstance(value, (list, tuple)):
        for child in value:
            safe(child)


def seal(directory):
    files = sorted(p for p in directory.rglob("*") if p.is_file() and p.name != "SHA256SUMS")
    (directory / "SHA256SUMS").write_text("".join(f"{sha(p.read_bytes())}  {p.relative_to(directory).as_posix()}\n" for p in files))


def build_input(name):
    path = pathlib.PurePosixPath(name)
    return (path.parts[0] in {"src", "Props", "Targets"} or len(path.parts) == 1) and (path.suffix.lower() in {".cs", ".csproj", ".props", ".targets", ".json", ".config", ".resx", ".ruleset"} or path.name == ".editorconfig") and not {"bin", "obj"}.intersection(path.parts)


@functools.lru_cache(maxsize=3)
def committed_source_inputs(name, coordinate):
    repository = PROJECTS / "references" / name
    tree = subprocess.check_output(["git", "ls-tree", "-rz", coordinate], cwd=repository)
    files = {}
    for entry in tree.split(b"\0"):
        if not entry:
            continue
        metadata, relative = entry.split(b"\t", 1)
        mode, kind, blob = metadata.decode().split()
        if kind == "blob" and build_input(relative.decode()):
            require(mode != "120000", "Symlink in committed build closure")
            files[relative.decode()] = blob
    output = subprocess.check_output(["git", "cat-file", "--batch"], input=("\n".join(files.values()) + "\n").encode(), cwd=repository)
    stream = io.BytesIO(output)
    result = {}
    for relative, expected in files.items():
        blob, kind, length = stream.readline().decode().split()
        data = stream.read(int(length)); stream.read(1)
        require(blob == expected and kind == "blob", "Committed source object unavailable")
        result[relative] = {"git_blob": blob, "committed_sha256": sha(data), "permitted_sha256": {sha(data), sha(data.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n"))}}
    return result


def receipt_json(command):
    require(command["exit_code"] == 0 or 200 <= command["exit_code"] < 300, "Operation did not succeed")
    diagnostic = command.get("diagnostic", "")
    require(isinstance(diagnostic, str) and sha(diagnostic.encode()) == command["output_sha256"], "Operation output differs from literal receipt")
    return json.loads(diagnostic.strip().splitlines()[-1])


def validate_graphs(directory, commands, lane):
    for project in ("Host", "Domain", "Probe"):
        graph = json.loads((directory / "artifacts" / lane / (project + "-assets.json")).read_text())
        lock = json.loads((directory / "artifacts" / lane / (project + "-lock.json")).read_text())
        require(lock.get("version") in {1, 2} and set(lock["dependencies"]) == {"net10.0"}, "Missing or substituted lock graph")
        libraries = graph["libraries"]
        locked = lock["dependencies"]["net10.0"]
        require(locked and {name.lower() for name in locked} == {name.split("/")[0].lower() for name in libraries}, "Lock/assets coverage differs")
        for key, library in libraries.items():
            name, version = key.split("/")
            value = next(v for k,v in locked.items() if k.lower() == name.lower())
            if library["type"] == "package":
                require(value["resolved"] == version and value["contentHash"] == library["sha512"] and value["type"] in {"Direct", "Transitive", "CentralTransitive"}, "Lock package resolution/content hash differs from assets")
                if name in PACKAGE_IDS and lane != "current":
                    require(value["contentHash"] == PUBLISHED_CONTENT_HASHES[lane][name], "Lock content hash differs from freshly verified exact archive")
            else:
                require(library["type"] == "project" and value["type"] == "Project", "Lock source dependency differs from assets")
        bound = []
        for command in commands:
            argv = command["argv"]
            if len(argv) == 6 and argv[1:3] == ["-c", ARTIFACT_INVENTORY_SCRIPT] and argv[-1] == "hash-restored-graphs" and pathlib.Path(argv[3]).parts[-4:] == (lane, project.lower(), "obj", "project.assets.json"):
                bound.append(command)
        require(any(receipt_json(c) == {c["argv"][3]: sha(canonical(graph)), c["argv"][4]: sha(canonical(lock))} for c in bound), "Restore graph lacks physical executed artifact inventory")


def wire_fixture(kind, shape):
    if kind == "projection":
        return {"eventTypeName": "P1R.Counter.CounterIncremented", "payload": "e30=", "serializationFormat": "json", "sequenceNumber": 3, "timestamp": "2026-01-01T00:00:00Z", "correlationId": "fixture-correlation", "messageId": "fixture-message", "userId": "fixture-user", "globalPosition": 987}
    value = {"tenantId": "tenant-a", "domain": "counter", "aggregateId": "fixture", "queryType": "fixture-query", "payload": "e30=", "correlationId": "fixture-correlation", "userId": "fixture-user", "entityId": None, "isGlobalAdmin": False, "paging": None}
    if shape == "dual":
        value.update(originalActorId="fixture-human", authenticatedWorkloadId="fixture-workload", isDelegated=True, scopes=["fixture-read", "fixture-append"], audience=["fixture-audience", "fixture-secondary"], delegationId="fixture-delegation")
    return value


def common_wire_fields(value):
    fields = {k[0].upper()+k[1:]: v for k, v in value.items() if k not in {"originalActorId", "authenticatedWorkloadId", "isDelegated", "scopes", "audience", "delegationId", "globalPosition"}}
    if "Timestamp" in fields:
        fields["Timestamp"] = fields["Timestamp"].replace("Z", "+00:00")
    return fields


def written_fixture_hash(value):
    return sha((json.dumps(value, indent=2, sort_keys=True) + "\n").encode())


def validate_support(directory, commands, rows):
    passed = {r["id"] for r in rows if r["execution"] == "passed"}
    by_id = {c["id"]: c for c in commands}
    closures = None
    if "provenance" in passed or "checkout" in passed or (directory / "artifacts/current-identity.json").exists():
        closures = json.loads((directory / "source-input-closure.json").read_text())
        require(set(closures) == set(SOURCE_REPOSITORIES), "Missing source/build/sibling closure")
        for name, coordinate in SOURCE_REPOSITORIES.items():
            closure = closures[name]
            require(closure["coordinate"] == coordinate and pathlib.Path(closure["repository"]).parts[-2:] == ("references", name), "Source closure coordinate/path mismatch")
            command = by_id[closure["command_id"]]
            require(command["argv"][1:] == ["-c", SOURCE_CLOSURE_SCRIPT, closure["repository"], coordinate, "source-input-closure"] and command["exit_code"] == 0 and command["output_sha256"] == sha(closure["output"].encode()), "Source closure lacks executed physical input check")
            require(json.loads(closure["output"]) == {k:v for k,v in closure.items() if k not in {"output", "command_id"}}, "Source closure differs from command output")
            committed = committed_source_inputs(name, coordinate)
            require(set(closure["files"]) == set(committed), "Source closure omitted committed inputs")
            for path, value in closure["files"].items():
                expected = committed[path]
                require(value["git_blob"] == expected["git_blob"] and value["committed_sha256"] == expected["committed_sha256"] and value["sha256"] in expected["permitted_sha256"], "Source input differs from approved committed bytes")
            require(any(c["argv"] == ["git", "ls-tree", SOURCES["projects_baseline"], "references/" + name] and c["exit_code"] == 0 and c["diagnostic"].split()[2] == coordinate for c in commands), "Sibling coordinate lacks approved gitlink receipt")
        comparison = json.loads((directory / "source-comparison.json").read_text())
        require(comparison["coordinates"] == SOURCES and comparison["current_inventory"] == {p:r["sha256"] for p,r in closures["Hexalith.EventStore"]["files"].items() if p.startswith("src/")}, "Source comparison inventory/coordinates differ from committed closure")
        require(any(c["argv"] == ["git", "diff", "--name-status", SOURCES["selected"], SOURCES["current"], "--", "src"] and c["exit_code"] == 0 and sha(c["diagnostic"].encode()) == c["output_sha256"] and c["diagnostic"].splitlines() == comparison["selected_to_current"] for c in commands), "Source comparison lacks executed diff")
        require(comparison["rollback_archive_to_tag_src_equal"] is True and any(c["argv"] == ["git", "diff", "--exit-code", SOURCES["rollback_archive"], SOURCES["rollback_tag"], "--", "src", "tools/release-packages.json"] and c["exit_code"] == 0 and not c["diagnostic"].strip() for c in commands), "Rollback source-equality claim lacks executed receipt")
    live = passed.intersection({"full-replay", "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "metadata-write", "invalid-evidence", "mixed-api", "checkout", "post-upgrade-restore", "pre-upgrade-restore"})
    if live:
        runtime = json.loads((directory / "runtime-identity.json").read_text())
        cleanup = json.loads((directory / "cleanup.json").read_text())
        require(runtime["dapr_version"] == "1.18.2" and runtime["private_discovery"] is True and runtime["daprd_sha256"] == "e730688f06b9b7cea0d616a7dde8fc0124cb6cc8d6dae149192422c7ea67f4e6", "Runtime identity differs from exact private runtime")
        require(set(runtime["images"]) == {"postgresql", "placement", "scheduler", "pubsub"} and {v["id"] for v in runtime["images"].values()} == set(cleanup["owned_containers"]), "Runtime/ownership resource coverage differs")
        binary = None
        for command in commands:
            argv = command["argv"]
            if argv[1:3] == ["-c", FILE_HASH_SCRIPT] and argv[-1] == "hash-private-runtime" and command["exit_code"] == 0 and command["diagnostic"].strip() == runtime["daprd_sha256"] and sha(command["diagnostic"].encode()) == command["output_sha256"]:
                binary = argv[3]
        require(binary is not None and any(c["argv"] == [binary, "--version"] and c["exit_code"] == 0 and "1.18.2" in c["diagnostic"] for c in commands), "Private runtime version/hash lacks executed receipts")
        require(any(c["argv"] == ["docker", "cp", runtime["images"]["placement"]["id"] + ":/daprd", binary] and c["exit_code"] == 0 for c in commands), "Runtime did not come from the owned placement container")
        for role, record in runtime["images"].items():
            image = POSTGRES if role == "postgresql" else RUNTIME if role in {"placement", "scheduler"} else "redis:7.4"
            require(any(c["argv"][:2] == ["docker", "run"] and image in c["argv"] and "p1r-" + cleanup["invocation"] + "-" + role in c["argv"] and "hexalith.p1r.invocation=" + cleanup["invocation"] in c["argv"] and c["exit_code"] == 0 for c in commands), "Runtime resource lacks invocation-owned launch receipt")
            require(any(c["argv"] == ["docker", "inspect", "--format", "{{.Image}}", record["id"]] and c["exit_code"] == 0 and c["diagnostic"].strip() == record["image_id"] and sha(c["diagnostic"].encode()) == c["output_sha256"] for c in commands), "Runtime image differs from observed container")
        require(runtime["images"]["postgresql"]["image_id"] == POSTGRES.split("@",1)[1] and runtime["images"]["placement"]["image_id"] == runtime["images"]["scheduler"]["image_id"], "Runtime container image coordinate mismatch")
    if {"checkout", "invalid-evidence"} <= passed:
        comparison = json.loads((directory / "shared-scope-comparison.json").read_text())
        checkout = next(r for r in rows if r["id"] == "checkout")
        invalid = next(r for r in rows if r["id"] == "invalid-evidence")
        shared = {c["id"]:c["compatibility"] for c in checkout["cases"] if c["id"] != "current-build"}
        selected = {c["id"].removeprefix(VERSIONS[0] + "-"):c["handling"] for c in invalid["cases"] if c["id"].startswith(VERSIONS[0] + "-")}
        current = {c["id"].removeprefix("current-"):c["handling"] for c in next(c for c in checkout["cases"] if c["id"] == "invalid-evidence")["cases"]}
        equivalent = all(v == "compatible" for k,v in shared.items() if k != "invalid-evidence") and selected == current
        require(comparison["selected"] == VERSIONS[0] and comparison["current_source"] == SOURCES["current"] and comparison["shared_case_dispositions"] == shared and comparison["selected_invalid_handling"] == selected and comparison["current_invalid_handling"] == current and comparison["shared_scope_equivalent"] == equivalent and comparison["invalid_evidence_remains_incompatible"] == (shared["invalid-evidence"] == "incompatible"), "Shared comparison contradicts executed case observations")
    return closures


def validate_operations(directory, commands, rows, inventories, current, closures):
    by_id = {c["id"]:c for c in commands}
    def scoped(case, parent_ids):
        ids = set(case["command_ids"])
        require(ids <= parent_ids, "Case receipts escape the enclosing scenario")
        prerequisites = set(case.get("prerequisite_command_ids", []))
        require(prerequisites <= parent_ids, "Unrelated prerequisite receipt")
        return [by_id[i] for i in sorted(ids | prerequisites)]
    def probes(receipts, lane, operation):
        configuration = "Debug" if lane == "current" else "Release"
        result = [c for c in receipts if c["argv"][:1] == ["dotnet"] and len(c["argv"]) >= 3 and pathlib.Path(c["argv"][1]).parts[-6:] == (lane, "probe", "bin", configuration, "net10.0", "Probe.dll") and c["argv"][2] == operation]
        for command in result:
            receipt_json(command)
        return result
    def alive(launch, request):
        return launch["exit_code"] == 0 and launch["started_utc"] <= request["started_utc"] <= request["finished_utc"] <= launch["finished_utc"]
    def host_proof(receipts, lane, kind="host"):
        configuration = "Debug" if lane == "current" else "Release"
        assembly = "Host.dll" if kind == "host" else "Domain.dll"
        require(any(c["argv"][:1] == ["dotnet"] and pathlib.Path(c["argv"][1]).parts[-6:] == (lane, kind, "bin", configuration, "net10.0", assembly) for c in receipts), "Missing executed lane application")
        hashes = current["built_assemblies"]["Host" if kind == "host" else "Domain"] if lane == "current" else PUBLISHED_DLL_HASHES[lane]
        suffix = "/identity" if kind == "host" else "/ready"
        valid = []
        for command in receipts:
            if command["argv"][:2] == ["HTTP", "GET"] and command["argv"][2].endswith(suffix) and command["exit_code"] == 200:
                endpoint = command["argv"][2].removesuffix(suffix)
                launches = [c for c in receipts if c["argv"][:1] == ["dotnet"] and pathlib.Path(c["argv"][1]).parts[-6:] == (lane, kind, "bin", configuration, "net10.0", assembly) and c.get("node_environment", {}).get("ASPNETCORE_URLS") == endpoint and alive(c, command)]
                if not launches:
                    continue
                observed = receipt_json(command)
                assemblies = observed if kind == "host" else observed.get("assemblies", []) if isinstance(observed, dict) else []
                if assemblies and all(a["sha256"] == hashes.get(a["name"]) for a in assemblies):
                    valid.append(command)
        require(valid, "Case lacks the actual running lane identity response")
        return valid
    def application_request(receipts, lane, command, suffix, kind="host"):
        identities = host_proof(receipts, lane, kind)
        endpoint = command["argv"][2].removesuffix(suffix)
        require(any(c["argv"][2].removesuffix("/identity" if kind == "host" else "/ready") == endpoint for c in identities), "Request endpoint differs from its actual lane identity")
        require(any(c["argv"][:1] == ["dotnet"] and str(c["argv"][1]).endswith("/"+kind.capitalize()+".dll") and c.get("node_environment", {}).get("ASPNETCORE_URLS") == endpoint and alive(c, command) for c in receipts), "Application lifetime does not cover its request")
    def sidecar_proof(receipts, lane, command, kind="host"):
        endpoint = command["argv"][3]
        configuration = "Debug" if lane == "current" else "Release"
        application = kind.capitalize() + ".dll"
        port = endpoint.rsplit(":", 1)[1]
        launches = [c for c in receipts if c["argv"][:1] == ["dotnet"] and pathlib.Path(c["argv"][1]).parts[-6:] == (lane, kind, "bin", configuration, "net10.0", application) and c.get("node_environment", {}).get("DAPR_HTTP_PORT") == port and alive(c, command)]
        require(launches, "Request endpoint lacks its actual lane application")
        require(any(alive(c, command) and "--dapr-http-port" in c["argv"] and c["argv"][c["argv"].index("--dapr-http-port")+1] == port and "--app-id" in c["argv"] and c["argv"][c["argv"].index("--app-id")+1] == ("eventstore" if kind == "host" else "counter") and "--app-port" in c["argv"] and any(c["argv"][c["argv"].index("--app-port")+1] == a["node_environment"]["ASPNETCORE_URLS"].rsplit(":",1)[1] for a in launches) for c in receipts), "Request lacks its actual owned sidecar/application port")
    def actor(receipts, lane, tenant, kind, count):
        calls = [c for c in probes(receipts, lane, "actor") if c["argv"][4:6] == [tenant, "fixture"] and c["argv"][7:] == [kind, str(count)]]
        require(calls, "Missing required typed actor invocation")
        for command in calls:
            endpoint = command["argv"][3]
            # A mixed client consumes the host lane selected by its node binding.
            hosts = [a for a in receipts if a["argv"][:1] == ["dotnet"] and str(a["argv"][1]).endswith("/Host.dll") and a.get("node_environment", {}).get("DAPR_HTTP_PORT") == endpoint.rsplit(":",1)[1] and alive(a, command)]
            require(hosts, "Actor request lacks its actual owned application")
            host_lane = pathlib.Path(hosts[-1]["argv"][1]).parts[-6]
            sidecar_proof(receipts, host_lane, command)
        return calls[-1], receipt_json(calls[-1])
    def seeds(receipts, lane):
        for tenant,count in (("tenant-a",12),("tenant-b",3)):
            calls = [c for c in probes(receipts,lane,"seed") if c["argv"][4:] == [tenant,"fixture",str(count)]]
            require(calls and all(receipt_json(c)["committedEvents"] == count and receipt_json(c)["hydratedCount"] == count and receipt_json(c)["assertions"] == 2*(count+1) for c in calls), "Missing or substituted real seed operations")
        host_proof(receipts,lane); host_proof(receipts,lane,"domain")
    def inventory(case,field):
        value = inventories[case["inventory_commands"][field]]
        require(value["sha256"] == case[field] and value["query_command_id"] in case["command_ids"], "Inventory is not from this case")
        return value
    def invariant(case, append=False):
        before,after = inventory(case,"before_sha256"),inventory(case,"after_sha256")
        require(before["database_identity_sha256"] == after["database_identity_sha256"], "Replay case changed databases")
        actor_ids = [c["id"] for c in scoped(case,set(case["command_ids"])) if c["argv"][:1] == ["dotnet"] and len(c["argv"]) > 2 and c["argv"][2] == "actor"]
        require(actor_ids and before["query_command_id"] < min(actor_ids) <= max(actor_ids) < after["query_command_id"], "Persisted observations must bracket distinct actual actor operations")
        if "database_identity_sha256" in case:
            require(case["database_identity_sha256"] == before["database_identity_sha256"], "Case database identity differs from queries")
        old,new = ({r["key"]:r for r in value["domain_rows"]} for value in (before,after))
        if not append:
            require(old == new, "Replay/rejection mutated committed domain state")
        else:
            prior_events = {k:v for k,v in old.items() if ":events:" in k}
            new_events = {k:v for k,v in new.items() if ":events:" in k}
            require(all(new_events.get(k) == v for k,v in prior_events.items()) and len(new_events) == len(prior_events)+1 and all(k in prior_events or k.endswith("tenant-a:counter:fixture:events:13") for k in new_events), "Append altered prior events or wrote an unrelated event")
            require(all(new.get(k) == v for k,v in old.items() if not k.endswith("tenant-a:counter:fixture:metadata")), "Append changed prior snapshot or other tenant state")
            metadata = next(v for k,v in new.items() if k.endswith("tenant-a:counter:fixture:metadata"))
            require(metadata["sequence"] == "13", "Append metadata sequence differs")
        return before,after
    def sequence(receipts,tenant,count,lane):
        endpoints={c["argv"][2].removesuffix("/identity") for c in host_proof(receipts,lane)}
        require(any(c["argv"][:2] == ["HTTP","GET"] and c["argv"][2] in {e+"/sequence/"+tenant+"/fixture" for e in endpoints} and c["exit_code"] == 200 and receipt_json(c) == count for c in receipts), "Missing actual sequence observation at the running host")
    def build(case,receipts,lane):
        identity = json.loads((directory/"artifacts"/(lane+"-identity.json")).read_text())
        outputs = probes(receipts,lane,"identity")
        require(outputs and receipt_json(outputs[-1]) == identity["loaded"] and case["actor_methods"] == identity["loaded"]["actorMethods"] and case["packages"] == len(identity["packages"]), "Provenance differs from physical probe invocation")
        for project in ("host","domain","probe"):
            for operation in ("restore","build"):
                calls = [c for c in receipts if c["argv"][:2] == ["dotnet",operation] and pathlib.Path(c["argv"][2]).parts[-3:] == (lane,project,project.capitalize()+".csproj")]
                require(calls and all(c["exit_code"] == 0 for c in calls), "Missing executed lane restore/build")
                if operation == "restore":
                    require(all("--configfile" in c["argv"] and "--no-http-cache" in c["argv"] and "--disable-parallel" in c["argv"] for c in calls), "Restore omitted isolated source/cache controls")
                else:
                    require(all("--no-restore" in c["argv"] and "-p:UseSharedCompilation=false" in c["argv"] and c["argv"][c["argv"].index("-c")+1] == ("Debug" if lane=="current" else "Release") for c in calls), "Build lane/configuration changed")
                if lane == "current":
                    require(all("-p:EventStoreSourceRoot="+closures["Hexalith.EventStore"]["repository"] in c["argv"] and "-p:HexalithCommonsRoot="+closures["Hexalith.Commons"]["repository"] in c["argv"] and "-p:UseCurrentSource=true" in c["argv"] for c in calls), "Current build did not consume the approved input closure")
            require(any(c["argv"][1:3] == ["-c",ARTIFACT_INVENTORY_SCRIPT] and c["argv"][-1] == "hash-restored-graphs" and pathlib.Path(c["argv"][3]).parts[-4:] == (lane,project,"obj","project.assets.json") for c in receipts), "Case lacks physical restored graph hashes")
            hashes=current["built_assemblies"][project.capitalize()] if lane=="current" else {name:PUBLISHED_DLL_HASHES[lane][name] for name in graph_packages(lane,project.capitalize())}
            require(any(c["argv"][1:3] == ["-c",ASSEMBLY_INVENTORY_SCRIPT] and pathlib.Path(c["argv"][3]).parts[-5:] == (lane,project,"bin","Debug" if lane=="current" else "Release","net10.0") and receipt_json(c)==hashes for c in receipts), "Physical output assembly hashes differ from verified lane DLLs")
        if lane != "current":
            for package in PACKAGE_IDS:
                require(any(c["argv"][:4] == ["dotnet","nuget","verify","--all"] and pathlib.Path(c["argv"][4]).parts[-3:] == (package.lower(),lane,package.lower()+"."+lane+".nupkg") and c["exit_code"] == 0 and "Signature type: Repository" in c["diagnostic"] for c in receipts), "Case lacks exact signed-archive verification")
        return "compatible"
    def check_group(name,cases,parent_ids,source=False):
        dispositions = []
        for case in cases:
            receipts = scoped(case,parent_ids)
            identity = case["id"]
            if name == "checkout":
                if identity == "current-build":
                    disposition = build(case,receipts,"current")
                else:
                    nested=expected_cases(identity,True)
                    require(len(case["cases"])==len(nested) and {c["id"] for c in case["cases"]}==nested and all(c["assertions"]>0 for c in case["cases"]), "Missing or duplicate nested comparison case")
                    disposition = check_group(identity,case["cases"],set(case["command_ids"]),True)
                    require(case["compatibility"] == disposition, "Current comparison label contradicts nested observations")
            elif name == "provenance":
                disposition = build(case,receipts,identity)
            elif name in {"legacy-metadata","metadata-read"}:
                convention,floor = identity.split("-floor-"); floor = None if floor=="None" else int(floor)
                lane = "current" if source else VERSIONS[0] if name=="legacy-metadata" else VERSIONS[1]
                calls = probes(receipts,lane,"metadata"); require(len(calls)==1 and calls[0]["argv"][-1]==convention,"Missing typed metadata reader/convention")
                observed = receipt_json(calls[0]); value={"CurrentSequence":12,"LastModified":"2026-01-01T00:00:00Z","ETag":"fixture-etag"}
                if floor is not None: value["RetainedFloor"]=floor
                if convention=="web": value={k[0].lower()+k[1:]:v for k,v in value.items()}
                require(case["fixture_input"]==value and observed["inputSha256"]==written_fixture_hash(value) and observed["currentSequence"]==12 and all(case[k]==observed[k] for k in ("currentSequence","floor","inputSha256","outputSha256")),"Metadata observations differ from actual fixture read")
                if name=="legacy-metadata": require(observed["floor"]==1,"Legacy floor default changed")
                disposition = "incompatible" if floor is not None and floor>1 and observed["floor"]!=floor else "compatible"
            elif name in {"query-wire","projection-wire"}:
                kind = "query" if name=="query-wire" else "projection"
                shape=case["shape"];direction=case["direction"];form=case["format"]
                allowed=(VERSIONS[0],"current") if source else VERSIONS
                require(tuple(direction) in (allowed,tuple(reversed(allowed))) and identity=="-".join(direction)+"-"+form+"-"+shape,"Wire direction/shape differs from case")
                calls=[]
                for lane,mode in ((direction[0],"to-xml" if form=="xml" else "json"),(direction[1],form),(direction[0],form)):
                    candidates=[c for c in probes(receipts,lane,"wire") if c["argv"][3:5]==[kind,mode] and c not in calls]
                    require(candidates,"Missing cross-version wire operation");calls.append(candidates[0])
                observed=[receipt_json(c) for c in calls];value=wire_fixture(kind,shape)
                require(case["fixture_input"]==value and observed[0]["inputSha256"]==written_fixture_hash(value) and observed[1]["inputSha256"]==observed[0]["outputSha256"] and observed[2]["inputSha256"]==observed[1]["outputSha256"] and case["wire_hashes"]==[r["outputSha256"] for r in observed],"Wire bytes do not form this fixture's actual round trip")
                require([case[k] for k in ("first_fields","middle_fields","final_fields")]==[r["fields"] for r in observed],"Wire fields differ from typed operations")
                expected={"OriginalActorId":"fixture-human","AuthenticatedWorkloadId":"fixture-workload","IsDelegated":True,"DelegationId":"fixture-delegation","Scopes":value["scopes"],"Audience":value["audience"]} if shape=="dual" else {"GlobalPosition":987} if kind=="projection" else {"UserId":"fixture-user"}
                common=common_wire_fields(value)
                require(all(k in observed[0]["fields"] and observed[0]["fields"][k] == v for k,v in common.items()), "Initial typed writer lost common wire fields")
                require(all(all(k in r["fields"] and r["fields"][k] == v for k,v in common.items()) for r in observed[1:]), "Common wire fields changed across versions")
                expected.update(common)
                preserved=all(k in r["fields"] and r["fields"][k]==v for r in observed[1:] for k,v in expected.items())
                if direction[0] in {VERSIONS[0],"current"}: require(all(observed[0]["fields"].get(k)==v for k,v in expected.items()),"Initial typed writer lost supplied fixture fields")
                require(case["preserved"]==preserved and all(r["fields"].get("UserId")=="fixture-user" for r in observed),"Wire preservation label contradicts observations")
                if shape=="legacy": require(all(all(r["fields"].get(k) is None for k in ("OriginalActorId","AuthenticatedWorkloadId","DelegationId","Scopes","Audience")) and r["fields"].get("IsDelegated",False) is False for r in observed),"Legacy delegated defaults changed")
                disposition="compatible" if preserved else "incompatible"
            elif name in {"full-replay","snapshot-tail","retained-covered","retained-uncovered","missing-event","metadata-write"}:
                writer,reader,variant=case["writer"],case["reader"],case["variant"]
                require(identity==writer+"-to-"+reader+("-"+variant if variant!="default" else ""),"Replay case/lane mismatch")
                seeds(receipts,writer);host_proof(receipts,reader);host_proof(receipts,reader,"domain")
                command,outcome=actor(receipts,reader,"tenant-a","AssertCounter",12)
                require(case["actor_outcome"]==outcome and outcome["accepted"]==(name not in {"retained-uncovered","missing-event"}) and outcome["eventCount"]==0,"Replay actor outcome differs")
                _,tenant=actor(receipts,reader,"tenant-b","AssertCounter",3);require(tenant["accepted"] and tenant["eventCount"]==0,"Tenant isolation control failed");sequence(receipts,"tenant-a",12,reader)
                before,after=invariant(case,name=="metadata-write")
                expected_events=set(range(5,13)) if name in {"retained-covered","retained-uncovered","metadata-write"} else set(range(1,13))-{7 if variant=="interior" else 12} if name=="missing-event" else set(range(1,13))
                require({int(r["key"].rsplit(":",1)[1]) for r in before["domain_rows"] if "tenant-a:counter:fixture:events:" in r["key"]}==expected_events and {int(r["key"].rsplit(":",1)[1]) for r in before["domain_rows"] if "tenant-b:counter:fixture:events:" in r["key"]}=={1,2,3},"Replay persisted fixture is incomplete/substituted")
                metadata=next(r for r in before["domain_rows"] if r["key"].endswith("tenant-a:counter:fixture:metadata"))
                require(metadata["sequence"]=="12" and metadata["floor"] in ({"5"} if name in {"retained-covered","retained-uncovered","metadata-write"} else {"1",None}),"Replay fixture metadata differs")
                snapshots=[r for r in before["domain_rows"] if r["key"].endswith("tenant-a:counter:fixture:snapshot")]
                if name in {"snapshot-tail","retained-covered","metadata-write"}: require(len(snapshots)==1 and snapshots[0]["snapshotSequence"]=="9","Missing actual covering snapshot fixture")
                if name=="retained-uncovered": require(not snapshots if variant=="absent" else len(snapshots)==1 and snapshots[0]["snapshotSequence"]=="2","Absent/non-covering snapshot observation differs")
                if name in {"retained-covered","retained-uncovered","metadata-write"}: require(any("'{retainedFloor}','5'" in c["argv"][-1] and "delete from state" in c["argv"][-1] for c in receipts if "psql" in c["argv"]),"Missing executed retained-prefix fixture mutation")
                if name=="missing-event": require(any(c["argv"][-1]=="delete from state where key like '%tenant-a:counter:fixture:events:"+str(7 if variant=="interior" else 12)+"';" for c in receipts if "psql" in c["argv"]),"Missing actual interior/tail mutation")
                if name=="retained-uncovered": require(any(("delete from state where key like '%tenant-a:counter:fixture:snapshot';" if variant=="absent" else "update state set value=jsonb_set(jsonb_set(value,'{sequenceNumber}','2'),'{state,count}','2') where key like '%tenant-a:counter:fixture:snapshot';")==c["argv"][-1] for c in receipts if "psql" in c["argv"]),"Missing absent/non-covering snapshot fixture mutation")
                if name in {"retained-uncovered","missing-event"}:
                    require(case["failure_category"]=="missing-event" and case["failure_command_ids"] and set(case["failure_command_ids"])<=set(case["command_ids"]),"Rejection lacks case-bound explicit category")
                    correlation=command["argv"][6]
                    require(any(any(f=={"correlation_id":correlation,"category":"missing-event"} for f in by_id[i].get("runtime_failures",[])) and re.search(r"CorrelationId="+re.escape(correlation)+r"[^\r\n]*ExceptionType=MissingEventException\b",by_id[i]["diagnostic"]) for i in case["failure_command_ids"]),"Unrelated rejection cannot satisfy missing-event control")
                disposition="compatible"
                if name=="metadata-write":
                    _,append=actor(receipts,reader,"tenant-a","IncrementCounter",0);require(append["accepted"] and append["eventCount"]==1,"Missing real append")
                    floor=next(r["floor"] for r in after["domain_rows"] if r["key"].endswith("tenant-a:counter:fixture:metadata"));disposition="compatible" if floor=="5" else "incompatible"
            elif name=="invalid-evidence":
                lane,mutation=case["lane"],case["mutation"];require(identity==lane+"-"+mutation,"Invalid-evidence case mismatch");seeds(receipts,lane)
                _,outcome=actor(receipts,lane,"tenant-a","AssertCounter",12)
                require(case["actor_outcome"]==outcome and outcome["eventCount"]==0 and case["handling"]==("accepted-unsafe" if outcome["accepted"] else "rejected"),"Invalid-evidence label contradicts actual actor result");invariant(case)
                markers={"invalid-floor":"'{retainedFloor}','0'","unreadable":"unreadable-fixture","protected":"json+pdenc-v1","unknown-type":"P1R.UnknownEvent","unknown-version":"'{metadataVersion}','987'"}
                require(any(markers[mutation] in c["argv"][-1] and c["argv"][-1].startswith("update state") for c in receipts if "psql" in c["argv"]),"Missing actual invalid-evidence mutation")
                disposition="incompatible" if outcome["accepted"] else "compatible"
            elif name.endswith("restore"):
                source_lane=VERSIONS[0] if name=="post-upgrade-restore" else VERSIONS[1];seeds(receipts,source_lane);host_proof(receipts,VERSIONS[1]);host_proof(receipts,VERSIONS[1],"domain")
                before=inventory(case,"source_inventory_sha256");restored=inventory(case,"restored_inventory_sha256");after=inventory(case,"restored_replay_sha256")
                require(before["rows"]==restored["rows"] and restored["database_identity_sha256"]!=before["database_identity_sha256"] and before["domain_rows"]==after["domain_rows"] and restored["database_identity_sha256"]==after["database_identity_sha256"],"Restoration inventory/invariants differ")
                dumps=[c for c in receipts if "pg_dump" in c["argv"] and c["exit_code"]==0 and c["output_sha256"]==case["backup_sha256"] and c.get("output_bytes")==case["backup_bytes"]]
                restores=[c for c in receipts if "pg_restore" in c["argv"] and c["exit_code"]==0 and c.get("input_sha256")==case["backup_sha256"] and c.get("input_bytes")==case["backup_bytes"]]
                require(len(dumps)==len(restores)==1 and dumps[0]["id"]<restores[0]["id"],"Dump/restore must consume this case's actual command receipts")
                for barrier in (dumps[0], restores[0]):
                    writers=[c for c in receipts if c["argv"][:1]==["dotnet"] and str(c["argv"][1]).endswith(("/Host.dll", "/Domain.dll")) and c["id"] < barrier["id"]]
                    require(writers and all(c["exit_code"] == 0 and c["finished_utc"] <= barrier["started_utc"] for c in writers), "Writers were not quiesced before backup/restore")
                replay_ids=[c["id"] for c in probes(receipts,VERSIONS[1],"actor") if c["id"]>restores[0]["id"]]
                require(replay_ids and before["query_command_id"]<dumps[0]["id"]<restores[0]["id"]<restored["query_command_id"]<min(replay_ids)<=max(replay_ids)<after["query_command_id"], "Restore observations do not bracket backup, restore and replay")
                require(sha(dumps[0]["argv"][-1].encode())==before["database_identity_sha256"] and sha(restores[0]["argv"][restores[0]["argv"].index("-d")+1].encode())==restored["database_identity_sha256"],"Backup command database differs from retained inventories")
                require(case["containment_only"]==(name=="pre-upgrade-restore"),"Restore containment scope changed")
                if name=="pre-upgrade-restore":
                    append,_=actor(receipts,VERSIONS[0],"tenant-a","IncrementCounter",0)
                    require(dumps[0]["id"]<append["id"]<restores[0]["id"] and any(c["argv"][:1]==["dotnet"] and str(c["argv"][1]).endswith("/3.110.0/host/bin/Release/net10.0/Host.dll") and c["finished_utc"]<=restores[0]["started_utc"] and c["id"]>append["id"] for c in receipts),"Pre-upgrade backup/write/stop/restore chronology differs")
                for tenant,count in (("tenant-a",12),("tenant-b",3)):
                    _,outcome=actor(receipts,VERSIONS[1],tenant,"AssertCounter",count);require(outcome["accepted"] and outcome["eventCount"]==0,"Restored actor control failed");sequence(receipts,tenant,count,VERSIONS[1])
                disposition="compatible"
            elif name=="mixed-api":
                if "client-" in identity:
                    client,host=case["client"],case["host"];seeds(receipts,host);host_proof(receipts,host)
                    for tenant,count in (("tenant-a",12),("tenant-b",3)):
                        _,outcome=actor(receipts,client,tenant,"AssertCounter",count);require(outcome["accepted"] and outcome["eventCount"]==0,"Common mixed client failed")
                    invariant(case);disposition="compatible"
                elif identity=="status-downgrade":
                    posts=[c for c in receipts if c["argv"][:2]==["HTTP","POST"] and c["argv"][2].endswith("/status/tenant-a/fixture-status")]
                    gets=[c for c in receipts if c["argv"][:2]==["HTTP","GET"] and c["argv"][2].endswith("/status/tenant-a/fixture-status")]
                    require(len(posts)==1 and len(gets)==2 and posts[0]["request_observation"]["retryable"] is True and posts[0]["request_observation"]["recoveryReasonCode"]=="fixture-recovery" and posts[0]["request_observation"]["drainAttemptCount"]==2,"Missing actual status write/read directions")
                    application_request(receipts,VERSIONS[0],posts[0],"/status/tenant-a/fixture-status")
                    for lane, request in zip(VERSIONS, gets):
                        application_request(receipts,lane,request,"/status/tenant-a/fixture-status")
                    selected,old=map(receipt_json,gets);require(selected["retryable"] is True and case["selected_keys"]==sorted(selected) and case["old_keys"]==sorted(old),"Status keys differ from typed store receipts")
                    lost=any(k not in old for k in ("retryable","recoveryReasonCode","drainAttemptCount"));require(case["handling"]==("lost-recovery-tristate" if lost else "preserved"),"Status handling claim differs");disposition="incompatible" if lost else "compatible"
                elif identity in {"cursor-downgrade","cursor-upgrade"}:
                    host_proof(receipts,case["mint"],"domain");host_proof(receipts,case["consume"],"domain")
                    minted=[c for c in receipts if c["argv"][:2]==["HTTP","GET"] and c["argv"][2].endswith("/cursor-mint")];queries=[c for c in receipts if c["argv"][:2]==["HTTP","POST"] and c["argv"][2].endswith("/query")]
                    require(len(minted)==1 and len(queries)==2 and minted[0]["confidential_observations"]["cursor_sha256"]==case["cursor_sha256"] and queries[0]["request_observation"]["paging"]["cursor"]["sha256"]==case["cursor_sha256"],"Cursor consumption does not bind the minted token")
                    application_request(receipts,case["mint"],minted[0],"/cursor-mint","domain")
                    for query in queries:
                        application_request(receipts,case["consume"],query,"/query","domain")
                        observed = query["request_observation"]
                        require({k:v for k,v in observed.items() if k != "paging"} == {"tenantId":"tenant-a","domain":"counter","aggregateId":"fixture","queryType":"fixture","payload":"e30=","correlationId":"fixture-correlation","userId":"fixture-user"}, "Cursor request fixture scope differs")
                        require(query["request_observation_sha256"] == sha(canonical(observed)), "Cursor request observation commitment differs")
                    require(case["scope"] == "tenant-a|watermark:987", "Cursor case scope differs")
                    values=[json.loads(base64.b64decode(receipt_json(c)["payloadBytes"])) for c in queries]
                    require(values[0]==case["decode_outcome"] and values[0]["decoded"] and values[0]["position"]=="position-3" and values[1]["decoded"] is False and case["tamper_rejected"] is True and case["key_material_retained"] is False,"Cursor success/tamper observations differ");disposition="compatible"
                elif identity.endswith("cursor-scope"):
                    lane=VERSIONS[1] if identity.startswith("old") else VERSIONS[0];calls=probes(receipts,lane,"cursor-scope");require(len(calls)==1,"Missing actual cursor scope helper call");outcome=receipt_json(calls[0])
                    require(all(case[k]==v for k,v in outcome.items()),"Cursor helper handling differs from actual invocation")
                    if outcome["handling"]=="executed": require(outcome["scope"]=="tenant:tenant-a|watermark:987" and outcome["invalid_watermark_rejected"] is True,"Watermark scope semantics differ")
                    disposition="incompatible" if outcome["handling"]=="unsupported-client-method" else "compatible"
                else:
                    host=VERSIONS[1] if identity.startswith("old") else VERSIONS[0];host_proof(receipts,host);method=case["method"];calls=[c for c in probes(receipts,VERSIONS[0],"capability") if c["argv"][-1]==method];require(len(calls)==1,"Missing real selected-only actor call");outcome=receipt_json(calls[0]);detail=outcome.get("detail","")
                    sidecar_proof(receipts,host,calls[0])
                    require(case["outcome"]==outcome["outcome"] and case["detail_sha256"]==sha(detail.encode()),"Actor capability result differs from actual dispatch")
                    unsupported=outcome["outcome"]=="rejected" and (method+"ReqBody" in detail and "deserializer has no knowledge" in detail or bool(re.search(r"(?i)(method.*(?:not found|not supported|does not exist|not implemented)|(?:missing|unknown).*method|MissingMethodException|KeyNotFoundException)",detail)))
                    if host==VERSIONS[1]: require(unsupported and case["handling"]=="unsupported-old-actor-contract","Malformed rejection cannot prove unsupported old dispatcher")
                    elif method=="GetRetainedFloorAsync": require(outcome["outcome"]=="returned","Selected floor dispatcher unavailable")
                    else: require(outcome["outcome"]=="rejected" and "ArgumentNullException" in detail,"Selected dispatcher null-input control differs")
                    disposition="incompatible" if unsupported else "compatible"
            elif name=="failure-cleanup":
                expected=7 if identity=="startup-failure" else 124 if identity=="timeout" else 130
                require(any(c["argv"][1:]==["-c","raise SystemExit(7)" if identity=="startup-failure" else "import time; time.sleep(10)"] and c["exit_code"]==expected for c in receipts),"Missing actual lifecycle failure operation");disposition="compatible"
            else:
                raise ValueError("Missing operation validator for "+name)
            dispositions.append(disposition)
        return "incompatible" if "incompatible" in dispositions else "compatible"
    for row in rows:
        if row["execution"]=="passed":
            derived=check_group(row["id"],row["cases"],set(row["command_ids"]))
            require(row["compatibility"]==derived,"Scenario compatibility contradicts actual case observations: "+row["id"])
        else:
            require(row["compatibility"]=="unverified","Unexecuted scenario has a compatibility disposition")


def validate(directory):
    directory = directory.resolve()
    require(directory.is_dir(), "Result directory missing")
    lines = (directory / "SHA256SUMS").read_text().splitlines()
    inventory = {}
    for line in lines:
        require(re.fullmatch(r"[0-9a-f]{64}  [^\r\n]+", line) is not None, "Malformed checksum")
        digest, relative = line.split("  ", 1)
        path = directory / relative
        require(not path.is_symlink() and path.resolve().is_relative_to(directory), "Escaping artifact path")
        require(relative not in inventory and relative != "SHA256SUMS", "Duplicate artifact")
        require(path.is_file() and sha(path.read_bytes()) == digest, "Mismatched artifact: " + relative)
        inventory[relative] = digest
    actual = {p.relative_to(directory).as_posix() for p in directory.rglob("*") if p.is_file() and p.name != "SHA256SUMS"}
    require(set(inventory) == actual, "Unbound or missing artifacts")
    for name in actual:
        require(pathlib.Path(name).suffix not in {".dump", ".sql", ".db", ".sqlite", ".env", ".nupkg"}, "Sensitive runtime artifact retained")
        contents = (directory / name).read_text(encoding="utf-8")
        safe(contents)
        if pathlib.Path(name).suffix == ".json":
            safe(json.loads(contents))
    manifest = json.loads((directory / "manifest.json").read_text())
    results = json.loads((directory / "scenario-results.json").read_text())
    require(manifest["schema"] == "hexalith.p1r.verification.v1", "Unknown manifest schema")
    require(manifest["coordinates"] == SOURCES and manifest["versions"] == list(VERSIONS), "Coordinate drift")
    require(manifest["runtime"]["dapr"] == "1.18.2" and manifest["runtime"]["postgresql"] == POSTGRES, "Runtime drift")
    require(manifest["usable_as_prerequisite"] is False and manifest["owner_acceptance_granted"] is False, "Forbidden acceptance claim")
    require(results["qualified"] is False, "Investigation cannot qualify P1R")
    rows = results["scenarios"]
    require(len(rows) == len(SCENARIOS) and {r["id"] for r in rows} == set(SCENARIOS), "Missing or duplicate scenario")
    commands = json.loads((directory / "commands.json").read_text())
    by_id = {c["id"]: c for c in commands}
    require(len({c["id"] for c in commands}) == len(commands), "Duplicate command receipt")
    command_ids = {c["id"] for c in commands}
    def validate_case_commands(cases):
        for case in cases:
            require(case.get("command_ids") and set(case["command_ids"]) <= command_ids, "Unbound case commands")
            if "cases" in case:
                validate_case_commands(case["cases"])
    for command in commands:
        require(isinstance(command["argv"], list) and command["argv"] and command["cwd"], "Missing literal command")
        require(command["started_utc"] <= command["finished_utc"], "Invalid command timestamps")
        require(type(command["exit_code"]) is int, "Missing command exit status")
        require(re.fullmatch(r"[0-9a-f]{64}", command["output_sha256"]) is not None, "Missing command output hash")
        if "request_observation" in command:
            observed = command["request_observation"]
            require(command["request_observation_sha256"] == sha(canonical(observed)), "Request observation commitment differs")
            opaque = isinstance(observed.get("paging"), dict) and isinstance(observed["paging"].get("cursor"), dict)
            if not opaque:
                require(command["argv"][-1] == "request-sha256="+sha(canonical(observed)), "Request observation differs from literal request hash")
    for row in rows:
        require(row["execution"] in {"passed", "failed", "unavailable"}, "Unknown execution result")
        require(row["compatibility"] in {"compatible", "incompatible", "unverified"}, "Unknown compatibility")
        require(row["command_ids"] and set(row["command_ids"]) <= command_ids, "Unbound scenario commands")
        require(type(row["assertions"]) is int and row["assertions"] >= 0, "Invalid assertion count")
        require(row["execution"] != "passed" or row["assertions"] > 0, "Zero-test success")
        require(row["execution"] == "passed" or row["compatibility"] != "compatible", "Nonpassing compatibility claim")
        require(isinstance(row["cases"], list) and row["cases"], "Missing scenario cases")
        require(len({c["id"] for c in row["cases"]}) == len(row["cases"]), "Duplicate case")
        require(type(row.get("setup_assertions", 0)) is int and row.get("setup_assertions", 0) >= 0, "Invalid setup assertion count")
        require(row["assertions"] == row.get("setup_assertions", 0) + sum(c["assertions"] for c in row["cases"]), "Assertion count mismatch")
        if row["execution"] == "passed":
            validate_case_commands(row["cases"])
            require({c["id"] for c in row["cases"]} == EXPECTED_CASES[row["id"]], "Missing required scenario case")
            require(all(c["assertions"] > 0 for c in row["cases"]), "Zero-assertion scenario case")
            if row["id"].endswith("restore"):
                require(all(re.fullmatch(r"[0-9a-f]{64}", c.get("backup_sha256", "")) and c.get("backup_bytes", 0) > 0 for c in row["cases"]), "Missing backup artifact binding")
                require(all("source_inventory_sha256" in c and "restored_inventory_sha256" in c for c in row["cases"]), "Missing restored inventory bindings")
    persisted = {}
    runtime_path = directory / "runtime-identity.json"
    pg = json.loads(runtime_path.read_text())["images"]["postgresql"]["id"] if runtime_path.exists() else None
    for command in commands:
        if command["argv"][:2] == ["docker", "exec"] and any(x in command["argv"] for x in ("psql", "createdb", "pg_dump", "pg_restore")):
            target = command["argv"][3] if command["argv"][2] == "-i" else command["argv"][2]
            require(pg is not None and target == pg, "PostgreSQL operation targets an unowned container")
    creations = {}
    for command in commands:
        if command["argv"][:2] == ["docker", "exec"] and "createdb" in command["argv"]:
            database=command["argv"][-1]
            require(command["exit_code"] == 0 and database not in creations and re.fullmatch(r"p1r_[a-z0-9_]+", database), "Database lacks unique successful fresh creation")
            creations[database] = command
    for command in commands:
        argv=command["argv"]
        if argv[:2] == ["docker", "exec"] and any(x in argv for x in ("psql", "pg_dump", "pg_restore")):
            database=argv[argv.index("-d")+1] if "-d" in argv else argv[-1]
            require(database in creations and creations[database]["id"] < command["id"], "Database used before successful fresh creation")
    for path in (directory / "inventories").glob("*.json") if (directory / "inventories").exists() else ():
        observed = json.loads(path.read_text())
        require(observed["sha256"] == sha(canonical(observed["rows"])), "Persisted inventory hash mismatch")
        require(observed["domain_rows"] == [r for r in observed["rows"] if ":events:" in r["key"] or r["key"].endswith(":snapshot") or r["key"].endswith(":metadata")], "Incorrect domain inventory projection")
        receipt = by_id[observed["query_command_id"]]
        require(receipt["exit_code"] == 0 and receipt["argv"][-2:] == ["-c", INVENTORY_SQL] and "psql" in receipt["argv"] and observed["query_output"] == receipt["diagnostic"] and json.loads(observed["query_output"]) == observed["rows"] and sha(observed["query_output"].encode()) == receipt["output_sha256"], "Inventory differs from its actual state-query receipt")
        database = receipt["argv"][receipt["argv"].index("-d") + 1]
        require(observed["database_identity_sha256"] == sha(database.encode()), "Inventory database binding differs")
        persisted[observed["query_command_id"]] = observed
    def validate_persisted(cases):
        for case in cases:
            for field in ("before_sha256", "after_sha256", "source_inventory_sha256", "restored_inventory_sha256", "restored_replay_sha256"):
                if field in case:
                    identity = case["inventory_commands"][field]
                    require(identity in persisted and persisted[identity]["sha256"] == case[field] and identity in case["command_ids"], "Missing or unrelated retained state-query inventory: " + field)
            if "cases" in case:
                validate_persisted(case["cases"])
    for row in rows:
        if row["execution"] == "passed":
            validate_persisted(row["cases"])
            if row["id"] in {"full-replay", "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "metadata-write", "invalid-evidence"}:
                require(all("before_sha256" in c and "after_sha256" in c for c in row["cases"]), "Missing persisted case bindings")
    provenance = next(r for r in rows if r["id"] == "provenance")
    def loaded_assemblies(assemblies, allowed, required, hashes, version):
        require(isinstance(assemblies, list) and len(assemblies) == len({a["name"] for a in assemblies}), "Duplicate or missing loaded identities")
        names = {a["name"] for a in assemblies}
        require(required <= names <= allowed, "Incomplete or unexpected loaded assembly coverage")
        for assembly in assemblies:
            require(assembly["version"] == version and assembly["sha256"] == hashes[assembly["name"]], "Loaded identity differs from verified DLL")
    if provenance["execution"] == "passed":
        require(any(len(c["argv"]) == 2 and c["argv"][1] == str(HERE.parent / "verify_public_packages.py") and c["exit_code"] == 0 for c in commands), "Missing successful public-package preflight")
        published = {p["id"]: p["nuget_sha256"] for p in json.loads((HERE.parent / "public-packages.json").read_text())["packages"]}
        for lane in VERSIONS:
            identity = json.loads((directory / "artifacts" / (lane + "-identity.json")).read_text())
            require(identity["lane"] == lane and len(identity["packages"]) == len(PACKAGE_IDS) and {p["id"] for p in identity["packages"]} == PACKAGE_IDS, "Incomplete or duplicate package identity coverage")
            for package in identity["packages"]:
                require(package["version"] == lane, "Artifact package version mismatch")
                require(package["repository"]["commit"] == SOURCES["selected" if lane == VERSIONS[0] else "rollback_archive"], "Artifact source mismatch")
                if lane == VERSIONS[0]:
                    require(package["archive_sha256"] == published[package["id"]], "Artifact archive mismatch")
                require(package["archive_sha256"] == PUBLISHED_ARCHIVE_HASHES[lane][package["id"]], "Artifact archive differs from verified exact archive")
                require(package["assemblies"] == {"lib/net10.0/" + package["id"] + ".dll": PUBLISHED_DLL_HASHES[lane][package["id"]]}, "Artifact DLL map differs from verified signed archive")
                require(package["content_hash"] == PUBLISHED_CONTENT_HASHES[lane][package["id"]], "Archive NuGet content hash differs")
            loaded_assemblies(identity["loaded"]["assemblies"], PROBE_ASSEMBLIES, PROBE_ASSEMBLIES, PUBLISHED_DLL_HASHES[lane], lane + ".0")
            for project in ("Host", "Domain", "Probe"):
                graph = json.loads((directory / "artifacts" / lane / (project + "-assets.json")).read_text())
                controlled = {n: v for n, v in graph["libraries"].items() if n.startswith("Hexalith.EventStore.")}
                require(set(controlled) == {name + "/" + lane for name in graph_packages(lane, project)} and all(v["type"] == "package" for v in controlled.values()), "Artifact restore graph mismatch")
            validate_graphs(directory, commands, lane)
    persisted_live = any(r["execution"] == "passed" and r["id"] in {"full-replay", "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "metadata-write", "invalid-evidence", "mixed-api", "post-upgrade-restore", "pre-upgrade-restore"} for r in rows)
    require(not persisted_live or provenance["execution"] == "passed", "Live execution lacks published provenance")
    for lane in VERSIONS:
        path = directory / "artifacts" / (lane + "-host-loaded.json")
        require(not persisted_live or path.is_file(), "Missing live host identity")
        if path.exists():
            loaded_assemblies(json.loads(path.read_text()), graph_packages(lane, "Host"), HOST_REQUIRED, PUBLISHED_DLL_HASHES[lane], lane + ".0")
        domain = directory / "artifacts" / (lane + "-domain-loaded.json")
        require(not persisted_live or domain.is_file(), "Missing live Domain identity")
        if domain.exists():
            loaded_assemblies(json.loads(domain.read_text()), DOMAIN_REQUIRED, DOMAIN_REQUIRED, PUBLISHED_DLL_HASHES[lane], lane + ".0")
    checkout_passed = any(r["id"] == "checkout" and r["execution"] == "passed" for r in rows)
    current_path = directory / "artifacts/current-identity.json"
    current = None
    if checkout_passed or current_path.exists():
        current = json.loads(current_path.read_text())
        require(current["lane"] == "current" and current["packages"] == [], "Current source identity claims package consumption")
        require(set(current["built_assemblies"]) == {"Host", "Domain", "Probe"}, "Missing current source outputs")
        for project, observed in current["built_assemblies"].items():
            require(set(observed) == graph_packages("current", project) and all(re.fullmatch(r"[0-9a-f]{64}", h) for h in observed.values()), "Incomplete current output assembly coverage")
            receipts = [c for c in commands if len(c["argv"]) >= 5 and c["argv"][1:3] == ["-c", ASSEMBLY_INVENTORY_SCRIPT] and c["argv"][-1] == "hash-built-assemblies" and pathlib.Path(c["argv"][-2]).parts[-5:] == ("current", project.lower(), "bin", "Debug", "net10.0")]
            require(any(c["exit_code"] == 0 and sha(c["diagnostic"].encode()) == c["output_sha256"] and json.loads(c["diagnostic"]) == observed for c in receipts), "Current output hashes lack executed inventory receipt")
            graph = json.loads((directory / "artifacts/current" / (project + "-assets.json")).read_text())
            controlled = {n: v for n,v in graph["libraries"].items() if n.startswith("Hexalith.EventStore.")}
            require(set(controlled) == {name + "/" + VERSIONS[0] for name in graph_packages("current", project)} and all(v["type"] == "project" for v in controlled.values()), "Current source restore graph mismatch")
        loaded_assemblies(current["loaded"]["assemblies"], PROBE_ASSEMBLIES, PROBE_ASSEMBLIES, current["built_assemblies"]["Probe"], VERSIONS[0] + ".0")
        host = directory / "artifacts/current-host-loaded.json"
        require(not checkout_passed or host.is_file(), "Missing current live host identity")
        if host.exists():
            loaded_assemblies(json.loads(host.read_text()), graph_packages("current", "Host"), HOST_REQUIRED, current["built_assemblies"]["Host"], VERSIONS[0] + ".0")
        domain = directory / "artifacts/current-domain-loaded.json"
        require(not checkout_passed or domain.is_file(), "Missing current live Domain identity")
        if domain.exists():
            loaded_assemblies(json.loads(domain.read_text()), DOMAIN_REQUIRED, DOMAIN_REQUIRED, current["built_assemblies"]["Domain"], VERSIONS[0] + ".0")
        validate_graphs(directory, commands, "current")
    for command in commands:
        if command["argv"][:2] == ["HTTP", "GET"] and command["argv"][2].endswith("/identity") and command["exit_code"] == 200:
            require(sha(command["diagnostic"].encode()) == command["output_sha256"], "Live identity response hash mismatch")
            assemblies = json.loads(command["diagnostic"])
            server = next((a for a in assemblies if a["name"] == "Hexalith.EventStore.Server"), None)
            require(server is not None, "Live identity lacks Server assembly")
            lane = next((lane for lane in VERSIONS if server["sha256"] == PUBLISHED_DLL_HASHES[lane]["Hexalith.EventStore.Server"]), None)
            if lane:
                loaded_assemblies(assemblies, graph_packages(lane, "Host"), HOST_REQUIRED, PUBLISHED_DLL_HASHES[lane], lane + ".0")
            else:
                require(current is not None and server["sha256"] == current["built_assemblies"]["Host"]["Hexalith.EventStore.Server"], "Live identity lacks package/source binding")
                loaded_assemblies(assemblies, graph_packages("current", "Host"), HOST_REQUIRED, current["built_assemblies"]["Host"], VERSIONS[0] + ".0")
        if command["argv"][:2] == ["HTTP", "GET"] and command["argv"][2].endswith("/ready") and command["exit_code"] == 200:
            response = receipt_json(command)
            if isinstance(response, dict) and "assemblies" in response:
                assemblies = response["assemblies"]
                sdk = next((a for a in assemblies if a["name"] == "Hexalith.EventStore.DomainService"), None)
                require(sdk is not None and response["ready"] is True, "Domain readiness lacks SDK identity")
                lane = next((v for v in VERSIONS if sdk["sha256"] == PUBLISHED_DLL_HASHES[v]["Hexalith.EventStore.DomainService"]), None)
                hashes = PUBLISHED_DLL_HASHES[lane] if lane else current["built_assemblies"]["Domain"] if current else {}
                loaded_assemblies(assemblies, DOMAIN_REQUIRED, DOMAIN_REQUIRED, hashes, (lane or VERSIONS[0]) + ".0")
    for lane in (*VERSIONS, "current"):
        for kind, suffix in (("host", "/identity"), ("domain", "/ready")):
            path = directory / "artifacts" / (lane + "-" + kind + "-loaded.json")
            if path.exists():
                observed = json.loads(path.read_text())
                responses = [receipt_json(c) for c in commands if c["argv"][:2] == ["HTTP", "GET"] and c["argv"][2].endswith(suffix) and c["exit_code"] == 200]
                require(any((r.get("assemblies") if kind == "domain" and isinstance(r, dict) else r) == observed for r in responses), "Retained live identity lacks matching response receipt")
    closures = validate_support(directory, commands, rows)
    if current is not None:
        for project in ("Host", "Domain", "Probe"):
            graph = json.loads((directory / "artifacts/current" / (project + "-assets.json")).read_text())
            for library in graph["libraries"].values():
                if library["type"] == "project":
                    path = (pathlib.Path(graph["project"]["restore"]["projectPath"]).parent / library["msbuildProject"]).resolve()
                    owners = [(name, closure) for name, closure in closures.items() if path.is_relative_to(pathlib.Path(closure["repository"]))]
                    require(len(owners) == 1 and path.relative_to(owners[0][1]["repository"]).as_posix() in owners[0][1]["files"], "Resolved source project escapes the approved committed closure")
    validate_operations(directory, commands, rows, persisted, current, closures)
    for row in rows:
        if row["execution"] == "passed" and row["id"].endswith("restore"):
            for case in row["cases"]:
                require(any(c["output_sha256"] == case["backup_sha256"] and c.get("output_bytes") == case["backup_bytes"] and "pg_dump" in c["argv"] for c in commands), "Dump hash is not bound to executed command")
                require(any(c.get("input_sha256") == case["backup_sha256"] and c.get("input_bytes") == case["backup_bytes"] and "pg_restore" in c["argv"] for c in commands), "Restore input differs from backup")
        if row["execution"] == "passed" and row["id"] == "checkout":
            for case in row["cases"]:
                if case["id"] in {"full-replay", "snapshot-tail", "retained-covered", "metadata-write"}:
                    expected = {"3.110.0-to-current", "current-to-3.110.0"}
                elif case["id"] == "retained-uncovered":
                    expected = {f"{a}-to-{b}-{v}" for a,b in ((VERSIONS[0], "current"), ("current", VERSIONS[0])) for v in ("absent", "non-covering")}
                elif case["id"] == "missing-event":
                    expected = {f"{a}-to-{b}-{v}" for a,b in ((VERSIONS[0], "current"), ("current", VERSIONS[0])) for v in ("interior", "tail")}
                elif case["id"] == "invalid-evidence":
                    expected = {"current-" + m for m in ("invalid-floor", "unreadable", "protected", "unknown-type", "unknown-version")}
                else:
                    continue
                require({c["id"] for c in case["cases"]} == expected, "Missing shared current-source case")
                require(all(c.get("assertions", 0) > 0 and "before_sha256" in c and "after_sha256" in c for c in case["cases"]), "Missing shared current-source persisted bindings")
    cleanup = json.loads((directory / "cleanup.json").read_text())
    require(cleanup["owned_processes_stopped"] and cleanup["owned_containers_removed"] and cleanup["scratch_removed"] and not cleanup.get("errors"), "Failed cleanup")
    owned=cleanup.get("owned_containers", [])
    if owned:
        discovery=[c for c in commands if c["argv"] == ["docker", "ps", "-aq", "--no-trunc", "--filter", "label=hexalith.p1r.invocation="+cleanup["invocation"]]]
        require(discovery and discovery[-1]["exit_code"] == 0 and not discovery[-1]["diagnostic"].strip() and discovery[-1]["output_sha256"] == sha(discovery[-1]["diagnostic"].encode()), "Cleanup lacks successful final owned-resource discovery")
        for identity in owned:
            inspections=[c for c in commands if c["argv"] == ["docker", "inspect", "--format", OWNERSHIP_INSPECT_FORMAT, identity]]
            require(inspections, "Cleanup lacks owned-resource inspection")
            inspection=inspections[-1]
            if inspection["exit_code"] != 0:
                require(re.search(r"(?i)no such (?:object|container)", inspection["diagnostic"]), "Cleanup inspection failed")
            else:
                require(receipt_json(inspection) == {"id": identity, "invocation":cleanup["invocation"]}, "Cleanup ownership differs")
                require(any(c["argv"] == ["docker", "rm", "-f", identity] and c["exit_code"] == 0 and inspection["id"] < c["id"] < discovery[-1]["id"] for c in commands), "Cleanup lacks successful owned-resource removal")
    require(cleanup.get("shared_discovery_complete") is True and isinstance(cleanup["shared_before"], dict) and isinstance(cleanup["shared_after"], dict), "Unknown shared-resource preservation")
    require(cleanup["shared_before"] == cleanup["shared_after"], "Shared resource drift")
    require(results["exit_code"] == (0 if all(r["execution"] == "passed" and r["compatibility"] == "compatible" for r in rows) and not cleanup.get("errors") else 1), "Invocation exit status contradicts executed dispositions")
    require(manifest["preserved_before"] == manifest["preserved_after"], "Acceptance or historical evidence changed")
    require(set(manifest["fixture_hashes"]) == FIXTURES, "Missing or unexpected fixture bindings")
    for relative, digest in manifest["fixture_hashes"].items():
        path = HERE / relative
        require(not any(p.is_symlink() for p in (path, *path.parents) if p.is_relative_to(HERE)) and path.resolve().is_relative_to(HERE), "Escaping fixture path")
        require(path.is_file() and sha(path.read_bytes()) == digest, "Fixture drift: " + relative)
    return results


class Runner:
    def __init__(self, output, scratch=None):
        self.output = output.resolve()
        self.scratch = scratch or pathlib.Path(tempfile.mkdtemp(prefix="hexalith-p1r-", dir="/var/tmp"))
        self.invocation = uuid.uuid4().hex
        self.commands = []
        self.processes = []
        self.containers = []
        self.databases = set()
        self.logs = []
        self.secret_values = []
        self.rows = []
        self.env = dict(os.environ, TMPDIR=str(self.scratch), DOTNET_CLI_HOME=str(self.scratch / "dotnet-home"), NUGET_SCRATCH=str(self.scratch / "nuget-scratch"), MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
        self.env.pop("DOTNET_ADDITIONAL_DEPS", None)
        self.env.pop("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", None)
        self.before = None
        self.discovery_errors = []
        self.container_launch_attempted = False
        self.node_stop_errors = []
        self.cleanup_errors = []
        self.current_assertions = 0
        self.source_paths = None

    def initialize(self):
        try:
            self.before = self.shared()
        except (Exception, KeyboardInterrupt) as error:
            self.discovery_errors.append({"phase": "before", "error": type(error).__name__})
            raise

    def redact(self, value):
        for secret in self.secret_values:
            value = value.replace(secret, "[redacted]")
        value = AUTHORIZATION.sub("authorization=[redacted]", value)
        value = BEARER.sub("bearer=[redacted]", value)
        return SECRET.sub("credential=[redacted]", value)

    def record(self, argv, started, code, output, cwd):
        diagnostic = self.redact(output.decode(errors="replace"))
        receipt = {"id": len(self.commands) + 1, "argv": [self.redact(str(a)) for a in argv], "cwd": str(cwd), "started_utc": started, "finished_utc": utc(), "exit_code": code, "output_sha256": sha(output), "diagnostic": diagnostic if str(argv[-1]) == INVENTORY_SQL else diagnostic[-10000:]}
        if any(str(a).endswith("/Host.dll") for a in argv):
            receipt["runtime_failures"] = [{"correlation_id": match[0], "category": "missing-event"} for match in re.findall(r"CorrelationId=([^,\s]+)[^\r\n]*ExceptionType=(MissingEventException)\b", output.decode(errors="replace"))]
        safe(receipt)
        self.commands.append(receipt)
        write(self.output / "commands.json", self.commands)
        return receipt

    def run(self, argv, cwd=HERE, env=None, timeout=180, check=True, binary=False, input_bytes=None):
        started = utc()
        process = None
        def record_output(code, stdout, stderr):
            receipt = self.record(argv, started, code, stderr if binary else stdout + stderr, cwd)
            if binary:
                receipt.update(output_sha256=sha(stdout), output_bytes=len(stdout), binary_output_retained=False)
            if input_bytes is not None:
                receipt.update(input_sha256=sha(input_bytes), input_bytes=len(input_bytes))
            write(self.output / "commands.json", self.commands)
            return receipt
        try:
            with defer_cancellation():
                process = subprocess.Popen([str(a) for a in argv], cwd=cwd, env=env or self.env, stdin=subprocess.PIPE if input_bytes is not None else subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
                self.processes.append(process)
        except (Exception, KeyboardInterrupt) as error:
            record_output(130 if isinstance(error, KeyboardInterrupt) else 127, b"", str(error).encode())
            if process is not None:
                try:
                    self.stop(process)
                except (Exception, KeyboardInterrupt) as failure:
                    self.cleanup_errors.append(type(failure).__name__)
                finally:
                    for stream in (process.stdin, process.stdout, process.stderr):
                        if stream is not None:
                            stream.close()
            raise
        try:
            stdout, stderr = process.communicate(input=input_bytes, timeout=timeout)
        except (subprocess.TimeoutExpired, KeyboardInterrupt) as error:
            code = 130 if isinstance(error, KeyboardInterrupt) else 124
            stdout, stderr = getattr(error, "output", None) or b"", getattr(error, "stderr", None) or b""
            receipt = record_output(code, stdout, stderr)
            interrupted = False
            try:
                self.stop(process)
                stdout, stderr = process.communicate(timeout=8)
                diagnostic = stderr if binary else stdout + stderr
                receipt.update(diagnostic=self.redact(diagnostic.decode(errors="replace"))[-10000:], output_sha256=sha(stdout if binary else diagnostic))
                if binary:
                    receipt.update(output_bytes=len(stdout), binary_output_retained=False)
            except (Exception, KeyboardInterrupt) as failure:
                receipt["termination_failure"] = type(failure).__name__
                interrupted = isinstance(failure, KeyboardInterrupt)
                if interrupted:
                    receipt["exit_code"] = 130
            safe(receipt)
            write(self.output / "commands.json", self.commands)
            if interrupted:
                raise KeyboardInterrupt("Owned command cleanup interrupted") from error
            if code == 124:
                raise TimeoutError("Owned command timed out") from error
            raise
        finally:
            for stream in (process.stdin, process.stdout, process.stderr):
                if stream is not None:
                    stream.close()
        record_output(process.returncode, stdout, stderr)
        if check:
            require(process.returncode == 0, f"Command {self.commands[-1]['id']} exited {process.returncode}")
        return stdout if binary else stdout.decode(errors="replace")

    def start(self, argv, env=None):
        started = utc()
        file = tempfile.TemporaryFile(dir=self.scratch)
        process = None
        try:
            with defer_cancellation():
                process = subprocess.Popen([str(a) for a in argv], cwd=self.scratch, env=env or self.env, stdin=subprocess.DEVNULL, stdout=file, stderr=file, start_new_session=True)
                self.processes.append(process)
                node_environment = {k: v for k, v in (env or {}).items() if k in {"ASPNETCORE_URLS", "DAPR_HTTP_PORT", "DAPR_GRPC_PORT"}}
                self.logs.append((process, file, list(argv), started, node_environment))
        except (Exception, KeyboardInterrupt) as error:
            self.record(argv, started, 130 if isinstance(error, KeyboardInterrupt) else 127, str(error).encode(), self.scratch)
            if process is None:
                file.close()
            else:
                try:
                    self.stop(process)
                except (Exception, KeyboardInterrupt) as failure:
                    self.cleanup_errors.append(type(failure).__name__)
            raise
        return process

    def stop(self, process):
        if process.poll() is None:
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            try:
                process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                process.wait(timeout=8)
        # Terminate any surviving descendants in this invocation's process group.
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass

    def flush_logs(self):
        for process, file, argv, started, node_environment in self.logs:
            file.seek(0)
            receipt = self.record(argv, started, process.returncode if process.returncode is not None else -1, file.read(), self.scratch)
            receipt["node_environment"] = node_environment
            file.close()
        self.logs.clear()

    def container(self, role, image, arguments=(), port=None, host_port=None, env_file=None, entrypoint=None):
        name = f"p1r-{self.invocation}-{role}"
        cidfile = self.scratch / (role + ".cid")
        argv = ["docker", "run", "--rm", "-d", "--cidfile", cidfile, "--name", name, "--label", "hexalith.p1r.invocation=" + self.invocation]
        if port:
            argv += ["-p", f"127.0.0.1:{host_port or ''}:{port}"]
        if env_file:
            argv += ["--env-file", env_file]
        if entrypoint:
            argv += ["--entrypoint", entrypoint]
        self.container_launch_attempted = True
        try:
            self.run([*argv, image, *arguments])
        finally:
            if cidfile.exists():
                self.containers.append(cidfile.read_text().strip())
        require(cidfile.exists(), "Missing owned container identity")
        identity = cidfile.read_text().strip()
        self.assertion(re.fullmatch(r"[0-9a-f]{64}", identity) is not None, "Invalid container identity")
        if not port:
            return identity, None
        endpoint = self.run(["docker", "port", identity, f"{port}/tcp"]).strip()
        self.assertion(endpoint.startswith("127.0.0.1:"), "Non-loopback endpoint")
        return identity, int(endpoint.rsplit(":", 1)[1])

    def shared(self):
        identities = self.run(["docker", "ps", "-aq", "--no-trunc"]).split()
        resources = {}
        if identities:
            inspection = self.run(["docker", "inspect", "--format", RESOURCE_INSPECT_FORMAT, *identities])
            for line in inspection.splitlines():
                row = json.loads(line)
                if row["invocation"] == self.invocation:
                    continue
                resources[row["id"]] = {key: row[key] for key in ("image", "running", "started")}
        for binary in (shutil.which("dapr"), str(pathlib.Path.home() / ".dapr/bin/daprd")):
            if binary and pathlib.Path(binary).is_file():
                resources[binary] = sha(pathlib.Path(binary).read_bytes())
        return resources

    def assertion(self, condition, message):
        require(condition, message)
        self.current_assertions += 1

    def scenario(self, name, action):
        start = len(self.commands)
        self.current_assertions = 0
        cases = []
        disposition = "unverified"
        cancelled = False
        try:
            cases, disposition = action()
            require(self.current_assertions > 0, "Zero assertions")
            execution = "passed"
            reason = None
        except (Exception, KeyboardInterrupt) as error:
            cancelled = isinstance(error, KeyboardInterrupt)
            execution = "unavailable" if isinstance(error, (FileNotFoundError, TimeoutError, urllib.error.URLError)) else "failed"
            if cancelled:
                execution = "unavailable"
            reason = type(error).__name__ + ": " + self.redact(str(error))
            cases = [{"id": "incomplete", "assertions": self.current_assertions, "reason": reason}]
        finally:
            stop_error = None
            try:
                self.stop_nodes()
            except (Exception, KeyboardInterrupt) as error:
                stop_error = error
                cancelled |= isinstance(error, KeyboardInterrupt)
                execution, disposition = "failed", "unverified"
                reason = "Owned node stop failed: " + type(error).__name__ + ": " + self.redact(str(error))
                cases = [{"id": "incomplete", "assertions": self.current_assertions, "reason": reason}]
        if len(self.commands) == start:
            self.run([sys.executable, "-c", "print('scenario receipt')"])
        row = {"id": name, "execution": execution, "compatibility": disposition, "assertions": self.current_assertions, "setup_assertions": self.current_assertions - sum(c["assertions"] for c in cases), "command_ids": [c["id"] for c in self.commands[start:]], "cases": cases, "reason": reason}
        self.rows.append(row)
        print(f"{name}: {execution}/{disposition} ({self.current_assertions} assertions)", flush=True)
        if cancelled:
            raise KeyboardInterrupt("Invocation cancelled; no further matrix scenarios run")
        if stop_error is not None:
            raise RuntimeError(reason) from stop_error
        return row

    def case(self, identity, action):
        before = self.current_assertions
        command_start = len(self.commands)
        result = action() or {}
        return {"id": identity, **result, "assertions": self.current_assertions - before, "command_ids": [c["id"] for c in self.commands[command_start:]]}

    def cleanup(self):
        if hasattr(self, "cleanup_receipt") and self.cleanup_receipt.get("complete"):
            return self.cleanup_receipt
        errors = list(self.node_stop_errors)
        for process in reversed(self.processes):
            try:
                self.stop(process)
            except (Exception, KeyboardInterrupt) as error:
                errors.append(type(error).__name__)
        try:
            self.flush_logs()
        except (Exception, KeyboardInterrupt) as error:
            errors.append(type(error).__name__)
        # Recover cidfile launch races by selecting only the invocation label.
        found = []
        if self.container_launch_attempted or self.containers:
            try:
                found = self.run(["docker", "ps", "-aq", "--no-trunc", "--filter", "label=hexalith.p1r.invocation=" + self.invocation]).split()
            except (Exception, KeyboardInterrupt) as error:
                errors.append(type(error).__name__)
        for identity in sorted(set(found + self.containers)):
            try:
                inspection = self.run(["docker", "inspect", "--format", OWNERSHIP_INSPECT_FORMAT, identity], check=False)
                if not inspection.strip().startswith("{"):
                    require(self.commands[-1]["exit_code"] != 0 and re.search(r"(?i)no such (?:object|container)", self.commands[-1]["diagnostic"]), "Owned container inspection failed")
                    continue
                row = json.loads(inspection)
                require(row["invocation"] == self.invocation and row["id"].startswith(identity), "Container ownership mismatch")
                self.run(["docker", "rm", "-f", identity])
            except (Exception, KeyboardInterrupt) as error:
                errors.append(identity + ":" + type(error).__name__)
        try:
            remaining = self.run(["docker", "ps", "-aq", "--no-trunc", "--filter", "label=hexalith.p1r.invocation=" + self.invocation]).strip() if self.container_launch_attempted or self.containers else ""
        except (Exception, KeyboardInterrupt) as error:
            errors.append(type(error).__name__)
            remaining = "unknown"
        after = None
        try:
            after = self.shared()
        except (Exception, KeyboardInterrupt) as error:
            self.discovery_errors.append({"phase": "after", "error": type(error).__name__})
        scratch = self.scratch
        try:
            if scratch.exists():
                shutil.rmtree(scratch, ignore_errors=False)
        except (OSError, KeyboardInterrupt) as error:
            errors.append(type(error).__name__)
        self.cleanup_errors.extend(e for e in errors if e not in self.cleanup_errors)
        receipt = {"invocation": self.invocation, "owned_processes": [p.pid for p in self.processes], "owned_containers": self.containers, "owned_processes_stopped": all(p.poll() is not None for p in self.processes) and not errors, "owned_containers_removed": not remaining and not errors, "scratch_removed": not scratch.exists(), "shared_before": self.before, "shared_after": after, "shared_discovery_complete": isinstance(self.before, dict) and isinstance(after, dict) and not self.discovery_errors, "discovery_errors": self.discovery_errors, "errors": self.cleanup_errors + ["shared_" + e["phase"] + "_unavailable" for e in self.discovery_errors], "complete": not remaining and not scratch.exists() and all(p.poll() is not None for p in self.processes) and not errors and isinstance(after, dict)}
        if hasattr(self, "cleanup_receipt"):
            previous = getattr(self, "cleanup_history", [])
            previous.append(self.cleanup_receipt)
            self.cleanup_history = previous
            write(self.output / "cleanup-attempts.json", previous)
        write(self.output / "cleanup.json", receipt)
        self.cleanup_receipt = receipt
        return receipt

    def probe(self, lane, *arguments):
        output = self.run(["dotnet", self.scratch / lane / "probe/bin" / ("Debug" if lane == "current" else "Release") / "net10.0/Probe.dll", *arguments], env=self.lane_env(lane))
        return json.loads(output.strip().splitlines()[-1])

    def lane_env(self, lane):
        return dict(self.env, NUGET_PACKAGES=str(self.scratch / lane / "packages"))

    def source_inputs(self):
        if self.source_paths is None:
            self.source_paths = {}
            for name, coordinate in SOURCE_REPOSITORIES.items():
                repository = self.scratch / "source/references" / name
                # Local object sharing does not modify the active checkout or initialize submodules.
                self.run(["git", "clone", "--shared", "--no-checkout", "--no-hardlinks", str(PROJECTS / "references" / name), str(repository)])
                self.run(["git", "-c", "core.autocrlf=false", "checkout", "--detach", coordinate], repository)
                self.source_paths[name] = repository
        closures = {}
        for name, coordinate in SOURCE_REPOSITORIES.items():
            repository = self.source_paths[name]
            bound = self.run(["git", "ls-tree", SOURCES["projects_baseline"], "references/" + name], PROJECTS).split()
            self.assertion(len(bound) == 4 and bound[2] == coordinate, "Source dependency differs from approved Projects gitlink")
            output = self.run([sys.executable, "-c", SOURCE_CLOSURE_SCRIPT, repository, coordinate, "source-input-closure"], timeout=180)
            closures[name] = {**json.loads(output), "command_id": self.commands[-1]["id"], "output": output}
        write(self.output / "source-input-closure.json", closures)
        return closures

    def prepare(self):
        cases = []
        records = []
        self.run([sys.executable, HERE.parent / "verify_public_packages.py"], ROOT, timeout=180)
        for lane in VERSIONS:
            def build(lane=lane):
                if lane == "current":
                    self.source_inputs()
                destination = self.scratch / lane
                for relative in sorted(FIXTURES):
                    target = destination / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(HERE / relative, target)
                props = [f"-p:EventStoreVersion={lane if lane != 'current' else VERSIONS[0]}", "-p:NuGetAudit=false"]
                if lane == "current":
                    props += ["-p:UseCurrentSource=true", "-p:UseHexalithProjectReferences=true", "-p:Configuration=Debug", f"-p:EventStoreSourceRoot={self.source_paths['Hexalith.EventStore']}", "-p:MinVerVersionOverride=3.110.0"]
                    props += [f"-p:HexalithCommonsRoot={self.source_paths['Hexalith.Commons']}"]
                    for name in ("HexalithBuildPackageVersions1", "HexalithBuildPackageVersions2", "HexalithBuildPackageVersions3", "Hexalith1BuildPackageProps", "Hexalith2BuildPackageProps", "Hexalith3BuildPackageProps", "Hexalith4BuildPackageProps"):
                        props.append(f"-p:{name}={self.source_paths['Hexalith.Builds'] / 'Props/Directory.Packages.props'}")
                package_rows = {}
                built_assemblies = {}
                for project in ("host/Host", "domain/Domain", "probe/Probe"):
                    csproj = destination / (project + ".csproj")
                    self.run(["dotnet", "restore", csproj, "--configfile", destination / "NuGet.Config", "--no-http-cache", "--disable-parallel", "-m:1", *props], env=self.lane_env(lane), timeout=300)
                    assets_path = csproj.parent / "obj/project.assets.json"
                    assets = json.loads(assets_path.read_text())
                    if lane == "current":
                        for row in assets["libraries"].values():
                            if row["type"] == "project":
                                resolved = (csproj.parent / row["msbuildProject"]).resolve()
                                self.assertion(any(resolved.is_relative_to(path) for path in self.source_paths.values()), "Unapproved resolved source dependency")
                        self.source_inputs()
                    write(self.output / "artifacts" / lane / (csproj.stem + "-assets.json"), assets)
                    write(self.output / "artifacts" / lane / (csproj.stem + "-lock.json"), json.loads((csproj.parent / "packages.lock.json").read_text()))
                    self.run([sys.executable, "-c", ARTIFACT_INVENTORY_SCRIPT, assets_path, csproj.parent / "packages.lock.json", "hash-restored-graphs"])
                    for name, row in assets["libraries"].items():
                        if name.startswith("Hexalith.EventStore."):
                            self.assertion(row["type"] == ("project" if lane == "current" else "package"), "Unexpected EventStore source dependency")
                            if lane != "current":
                                self.assertion(name.endswith("/" + lane), "EventStore package version drift")
                        if row["type"] == "package" and lane != "current":
                            package_rows[name] = row
                    self.run(["dotnet", "build", csproj, "--no-restore", "-c", "Debug" if lane == "current" else "Release", "-m:1", "-p:UseSharedCompilation=false", *props], env=self.lane_env(lane), timeout=300)
                    binary_directory = csproj.parent / "bin" / ("Debug" if lane == "current" else "Release") / "net10.0"
                    self.assertion((binary_directory / (csproj.stem + ".dll")).is_file(), "Missing fresh binary")
                    observed = json.loads(self.run([sys.executable, "-c", ASSEMBLY_INVENTORY_SCRIPT, binary_directory, "hash-built-assemblies"]))
                    self.assertion(set(observed) == graph_packages(lane, csproj.stem), "Missing exact output assembly coverage")
                    if lane != "current":
                        self.assertion(observed == {name: PUBLISHED_DLL_HASHES[lane][name] for name in graph_packages(lane, csproj.stem)}, "Built output differs from verified package DLL")
                    built_assemblies[csproj.stem] = observed
                    if lane == "current":
                        self.source_inputs()
                if lane != "current":
                    self.assertion({name.split('/')[0] for name in package_rows if name.startswith('Hexalith.EventStore.')} == PACKAGE_IDS, "Missing exact package coverage")
                archive_rows = []
                for name in sorted(package_rows):
                    package_id, version = name.split("/")
                    base = destination / "packages" / package_id.lower() / version
                    meta = json.loads((base / ".nupkg.metadata").read_text())
                    self.assertion(meta["source"] == "https://api.nuget.org/v3/index.json", "Unexpected restore source")
                    if package_id.startswith("Hexalith.EventStore."):
                        archive = base / f"{package_id.lower()}.{version}.nupkg"
                        with zipfile.ZipFile(archive) as z:
                            self.assertion(".signature.p7s" in z.namelist(), "Unsigned package")
                            xml = ET.fromstring(z.read(package_id + ".nuspec"))
                            repository = xml.find("{*}metadata/{*}repository").attrib
                            expected = SOURCES["selected" if lane == VERSIONS[0] else "rollback_archive"]
                            self.assertion(repository["commit"] == expected, "Archive source drift")
                            assemblies = {n: sha(z.read(n)) for n in z.namelist() if n.endswith(".dll")}
                            self.assertion(assemblies == {"lib/net10.0/" + package_id + ".dll": PUBLISHED_DLL_HASHES[lane][package_id]}, "Signed archive DLL map differs from verified exact bytes")
                        signature = self.run(["dotnet", "nuget", "verify", "--all", archive, "--verbosity", "minimal"], timeout=120)
                        self.assertion("Signature type: Repository" in signature, "NuGet repository signature not observed")
                        self.assertion(sha(archive.read_bytes()) == PUBLISHED_ARCHIVE_HASHES[lane][package_id], "Archive differs from exact verified bytes")
                        if lane == VERSIONS[0]:
                            published = json.loads((HERE.parent / "public-packages.json").read_text())
                            expected_hash = next(p["nuget_sha256"] for p in published["packages"] if p["id"] == package_id)
                            self.assertion(sha(archive.read_bytes()) == expected_hash, "Selected archive differs from fixed published record")
                        elif package_id in {"Hexalith.EventStore.Server", "Hexalith.EventStore.Contracts"}:
                            self.assertion(sha(archive.read_bytes()) == ROLLBACK_HASHES[package_id], "Rollback archive differs from historical provenance")
                        # NuGet's assets/lock contentHash is the restore identity of a signed
                        # package; it is distinct from its archive-byte SHA512 sidecar.
                        content_hash = package_rows[name]["sha512"]
                        self.assertion(content_hash == PUBLISHED_CONTENT_HASHES[lane][package_id], "Exact archive NuGet content hash changed")
                        archive_rows.append({"id": package_id, "version": version, "archive_sha256": sha(archive.read_bytes()), "content_hash": content_hash, "repository": repository, "assemblies": assemblies})
                identity = self.probe(lane, "identity")
                self.assertion(len(identity["assemblies"]) == 2, "Missing loaded assembly identities")
                if lane != "current":
                    known = {v for package in archive_rows for v in package["assemblies"].values()}
                    self.assertion(all(a["sha256"] in known for a in identity["assemblies"]), "Loaded assembly differs from signed package")
                record = {"lane": lane, "packages": archive_rows, "loaded": identity, "built_assemblies": built_assemblies}
                records.append(record)
                write(self.output / "artifacts" / (lane + "-identity.json"), record)
                return {"packages": len(archive_rows), "actor_methods": identity["actorMethods"]}
            cases.append(self.case(lane, build))
        self.build_current = lambda: build("current")
        comparison = self.run(["git", "diff", "--name-status", SOURCES["selected"], SOURCES["current"], "--", "src"], ROOT)
        self.run(["git", "diff", "--exit-code", SOURCES["rollback_archive"], SOURCES["rollback_tag"], "--", "src", "tools/release-packages.json"], ROOT)
        closures = self.source_inputs()
        write(self.output / "source-comparison.json", {"coordinates": SOURCES, "selected_to_current": comparison.splitlines(), "rollback_archive_to_tag_src_equal": True, "current_inventory": {p: row["sha256"] for p, row in closures["Hexalith.EventStore"]["files"].items() if p.startswith("src/")}, "reminder_boundary": "Current source adds Reminder contracts, actors, registration and dependencies. No tagged-package proof applies to these additions."})
        return cases, "compatible"

    def metadata(self, mode, selected=None):
        cases = []
        incompatible = False
        for convention in ("pascal", "web"):
            for floor in ((None,) if mode == "legacy-metadata" else (1, 5)):
                def execute(convention=convention, floor=floor):
                    nonlocal incompatible
                    value = {"CurrentSequence": 12, "LastModified": "2026-01-01T00:00:00Z", "ETag": "fixture-etag"}
                    if floor is not None:
                        value["RetainedFloor"] = floor
                    if convention == "web":
                        value = {k[0].lower() + k[1:]: v for k, v in value.items()}
                    input_path = self.scratch / "metadata-input.json"
                    output_path = self.scratch / "metadata-output.json"
                    write(input_path, value)
                    result = self.probe((selected or VERSIONS[0]) if mode == "legacy-metadata" else VERSIONS[1], "metadata", input_path, output_path, convention)
                    self.assertion(result["currentSequence"] == 12, "Sequence changed")
                    if mode == "legacy-metadata":
                        self.assertion(result["floor"] == 1, "Legacy floor did not default to one")
                    else:
                        incompatible |= floor > 1 and result["floor"] is None
                        self.assertion(result["inputSha256"] == sha(input_path.read_bytes()), "Raw read input changed")
                    return {**result, "fixture_input": value}
                cases.append(self.case(f"{convention}-floor-{floor}", execute))
        return cases, "incompatible" if incompatible else "compatible"

    def wire(self, kind, lanes=VERSIONS):
        cases = []
        incompatible = False
        for direction in (lanes, tuple(reversed(lanes))):
            for format in (("json", "xml") if kind == "query" else ("json",)):
                for shape in (("legacy", "dual") if kind == "query" else ("positive",)):
                    def execute(direction=direction, format=format, shape=shape):
                        nonlocal incompatible
                        value = {"tenantId": "tenant-a", "domain": "counter", "aggregateId": "fixture", "queryType": "fixture-query", "payload": "e30=", "correlationId": "fixture-correlation", "userId": "fixture-user", "entityId": None, "isGlobalAdmin": False, "paging": None}
                        if shape == "dual":
                            value.update(originalActorId="fixture-human", authenticatedWorkloadId="fixture-workload", isDelegated=True, scopes=["fixture-read", "fixture-append"], audience=["fixture-audience", "fixture-secondary"], delegationId="fixture-delegation")
                        if kind == "projection":
                            value = {"eventTypeName": "P1R.Counter.CounterIncremented", "payload": "e30=", "serializationFormat": "json", "sequenceNumber": 3, "timestamp": "2026-01-01T00:00:00Z", "correlationId": "fixture-correlation", "messageId": "fixture-message", "userId": "fixture-user", "globalPosition": 987}
                        path = self.scratch / "wire-input.json"
                        write(path, value)
                        first = self.scratch / "wire-first"
                        second = self.scratch / "wire-second"
                        third = self.scratch / "wire-third"
                        initial = self.probe(direction[0], "wire", kind, "to-xml" if format == "xml" else "json", path, first)
                        middle = self.probe(direction[1], "wire", kind, "xml" if format == "xml" else "json", first, second)
                        final = self.probe(direction[0], "wire", kind, "xml" if format == "xml" else "json", second, third)
                        self.assertion(first.is_file() and second.is_file() and third.is_file(), "Missing cross-version round trip")
                        expected = {"OriginalActorId": "fixture-human", "AuthenticatedWorkloadId": "fixture-workload", "IsDelegated": True, "DelegationId": "fixture-delegation", "Scopes": value["scopes"], "Audience": value["audience"]} if shape == "dual" else {"GlobalPosition": 987} if kind == "projection" else {"UserId": "fixture-user"}
                        common = common_wire_fields(value)
                        self.assertion(all(k in initial["fields"] and initial["fields"][k] == v for k,v in common.items()), "Initial typed writer lost common wire fields")
                        self.assertion(all(all(k in observed["fields"] and observed["fields"][k] == v for k,v in common.items()) for observed in (middle, final)), "Common wire fields changed across versions")
                        expected.update(common)
                        preserved = all(k in observed["fields"] and observed["fields"][k] == v for observed in (middle, final) for k,v in expected.items())
                        incompatible |= not preserved
                        self.assertion(final["fields"].get("UserId") == "fixture-user", "Common wire user changed")
                        if shape == "legacy":
                            for observed in (initial, middle, final):
                                self.assertion(all(observed["fields"].get(k) is None for k in ("OriginalActorId", "AuthenticatedWorkloadId", "DelegationId", "Scopes", "Audience")) and observed["fields"].get("IsDelegated", False) is False, "Legacy delegated defaults changed")
                        return {"direction": list(direction), "format": format, "shape": shape, "fixture_input": value, "preserved": preserved, "first_fields": initial["fields"], "middle_fields": middle["fields"], "final_fields": final["fields"], "wire_hashes": [sha(p.read_bytes()) for p in (first, second, third)]}
                    cases.append(self.case("-".join(direction) + "-" + format + "-" + shape, execute))
        return cases, "incompatible" if incompatible else "compatible"

    @staticmethod
    def port():
        return Runner.ports(1)[0]

    @staticmethod
    def ports(count):
        with contextlib.ExitStack() as sockets:
            ports = []
            for _ in range(count):
                connection = sockets.enter_context(socket.socket())
                connection.bind(("127.0.0.1", 0))
                ports.append(connection.getsockname()[1])
            return ports

    def topology(self):
        password = uuid.uuid4().hex
        self.secret_values.append(password)
        envfile = self.scratch / "postgres.env"
        envfile.write_text("POSTGRES_PASSWORD=" + password + "\nPOSTGRES_DB=eventstore\n")
        self.pg, self.pgport = self.container("postgresql", POSTGRES, port=5432, env_file=envfile)
        self.password = password
        self.placement, self.placement_port = self.container("placement", RUNTIME, ["./placement", "--port", "50005"], port=50005)
        scheduler_port = self.port()
        self.scheduler, self.scheduler_port = self.container("scheduler", RUNTIME, ["./scheduler", "--port", "50006", "--etcd-data-dir", "/tmp/p1r-scheduler", "--etcd-client-listen-address", "0.0.0.0", "--override-broadcast-host-port", f"127.0.0.1:{scheduler_port}"], port=50006, host_port=scheduler_port)
        self.redis, self.redis_port = self.container("pubsub", "redis:7.4", port=6379)
        # Extract only from the owned, versioned control-plane container.
        self.daprd = self.scratch / "daprd"
        self.run(["docker", "cp", self.placement + ":/daprd", self.daprd])
        self.daprd.chmod(0o700)
        self.assertion("1.18.2" in self.run([self.daprd, "--version"]), "Runtime mismatch")
        daprd_hash = self.run([sys.executable, "-c", FILE_HASH_SCRIPT, self.daprd, "hash-private-runtime"]).strip()
        write(self.output / "runtime-identity.json", {"daprd_sha256": daprd_hash, "dapr_version": "1.18.2", "private_discovery": True, "images": {role: {"id": identity, "image_id": self.run(["docker", "inspect", "--format", "{{.Image}}", identity]).strip()} for role, identity in (("postgresql", self.pg), ("placement", self.placement), ("scheduler", self.scheduler), ("pubsub", self.redis))}})
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            output = self.run(["docker", "exec", self.pg, "pg_isready", "-U", "postgres"], check=False)
            if "accepting connections" in output:
                break
            time.sleep(.5)
        else:
            raise TimeoutError("PostgreSQL startup timeout")

    def sql(self, database, statement, raw=False):
        require(database in self.databases, "Database is not invocation-owned")
        output = self.run(["docker", "exec", self.pg, "psql", "-U", "postgres", "-d", database, "-At", "-v", "ON_ERROR_STOP=1", "-c", statement])
        return output if raw else output.strip()

    def database(self):
        database = "p1r_" + uuid.uuid4().hex
        self.run(["docker", "exec", self.pg, "createdb", "-U", "postgres", "-T", "template0", database])
        self.databases.add(database)
        return database

    def inventory(self, database):
        output = self.sql(database, INVENTORY_SQL, raw=True)
        rows = json.loads(output)
        return {"rows": rows, "sha256": sha(canonical(rows)), "domain_rows": [r for r in rows if ":events:" in r["key"] or r["key"].endswith(":snapshot") or r["key"].endswith(":metadata")], "query_command_id": self.commands[-1]["id"], "query_output": output, "database_identity_sha256": sha(database.encode())}

    def persist_inventory(self, name, inventory):
        write(self.output / "inventories" / (name + ".json"), inventory)

    def start_nodes(self, lane, database, interval=1000):
        self.stop_nodes()
        resources = self.scratch / "resources"
        resources.mkdir(exist_ok=True)
        connection = f"host=127.0.0.1 port={self.pgport} user=postgres password={self.password} dbname={database} sslmode=disable"
        self.secret_values.append(connection)
        (resources / "statestore.yaml").write_text(f'apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.postgresql\n  version: v1\n  metadata:\n    - name: connectionString\n      value: "{connection}"\n    - name: actorStateStore\n      value: "true"\nscopes:\n  - eventstore\n')
        (resources / "pubsub.yaml").write_text(f'apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: pubsub\nspec:\n  type: pubsub.redis\n  version: v1\n  metadata:\n    - name: redisHost\n      value: "127.0.0.1:{self.redis_port}"\nscopes:\n  - eventstore\n')
        config = self.scratch / "discovery.yaml"
        config.write_text(f'apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: p1r-private\nspec:\n  nameResolution:\n    component: sqlite\n    version: v1\n    configuration:\n      connectionString: "{self.scratch / "discovery.sqlite"}"\n')
        self.active = []
        for kind, appid in (("domain", "counter"), ("host", "eventstore")):
            app, http, grpc, internal, metrics, profile = self.ports(6)
            environment = dict(self.env, ASPNETCORE_ENVIRONMENT="Development", ASPNETCORE_URLS=f"http://127.0.0.1:{app}", DAPR_HTTP_PORT=str(http), DAPR_GRPC_PORT=str(grpc), NAMESPACE="p1r-" + self.invocation,
                P1R_KEYS_PATH=str(self.scratch / "cursor-keys"), EventStore__Actors__AggregateActorTypeName="AggregateActor", EventStore__Snapshots__DefaultInterval=str(interval), EventStore__DomainServices__Registrations__counter__AppId="counter")
            registration = "EventStore__DomainServices__Registrations__*|counter|v1__"
            environment.update({registration + k: v for k, v in {"AppId": "counter", "MethodName": "process", "TenantId": "*", "Domain": "counter", "Version": "v1"}.items()})
            assembly = self.scratch / lane / kind / "bin" / ("Debug" if lane == "current" else "Release") / "net10.0" / ("Host.dll" if kind == "host" else "Domain.dll")
            application = self.start(["dotnet", assembly], environment)
            self.active.append(application)
            sidecar = self.start([self.daprd, "--app-id", appid, "--app-port", str(app), "--app-channel-address", "127.0.0.1", "--dapr-http-port", str(http), "--dapr-grpc-port", str(grpc), "--dapr-internal-grpc-port", str(internal), "--metrics-port", str(metrics), "--profile-port", str(profile), "--resources-path", resources, "--config", config, "--placement-host-address", f"127.0.0.1:{self.placement_port}", "--scheduler-host-address", f"127.0.0.1:{self.scheduler_port}", "--log-level", "warn"], environment)
            self.active.append(sidecar)
            readiness = self.wait_http(f"http://127.0.0.1:{app}/ready", application)
            if kind == "domain":
                write(self.output / "artifacts" / (lane + "-domain-loaded.json"), readiness["assemblies"])
            self.wait_http(f"http://127.0.0.1:{http}/v1.0/healthz/outbound", sidecar)
            if kind == "host":
                self.host_port, self.sidecar_port = app, http
            else:
                self.domain_port = app
        time.sleep(2)
        self.assertion((self.scratch / "discovery.sqlite").is_file(), "Private discovery not observed")
        identity = self.http("GET", f"http://127.0.0.1:{self.host_port}/identity")
        write(self.output / "artifacts" / (lane + "-host-loaded.json"), identity)

    def stop_nodes(self):
        errors = []
        cancelled = False
        for process in reversed(getattr(self, "active", [])):
            try:
                self.stop(process)
            except (Exception, KeyboardInterrupt) as error:
                cancelled |= isinstance(error, KeyboardInterrupt)
                message = type(error).__name__ + ": " + self.redact(str(error))
                self.record(["os.killpg", str(process.pid), "SIGTERM/SIGKILL"], utc(), 1, message.encode(), self.scratch)
                errors.append(type(error).__name__)
        self.active = [p for p in getattr(self, "active", []) if p.poll() is None]
        if errors:
            self.node_stop_errors.extend(errors)
            if cancelled:
                raise KeyboardInterrupt("Owned node cleanup interrupted")
            raise RuntimeError("Owned node cleanup failed: " + ",".join(errors))
        self.flush_logs()

    def wait_http(self, url, process):
        deadline = time.monotonic() + 45
        while time.monotonic() < deadline:
            if process.poll() is not None:
                self.flush_logs()
                raise RuntimeError("Owned application exited during startup")
            try:
                result = self.http("GET", url, timeout=2)
                return result
            except (OSError, urllib.error.URLError):
                time.sleep(.25)
        raise TimeoutError("Owned endpoint startup timed out")

    def http(self, method, url, value=None, confidential=False, timeout=45):
        data = canonical(value) if value is not None else None
        started = utc()
        try:
            request = urllib.request.Request(url, data=data, method=method, headers={"Content-Type": "application/json"})
            try:
                with urllib.request.urlopen(request, timeout=timeout) as response:
                    output, code = response.read(), response.status
            except urllib.error.HTTPError as error:
                with error:
                    output, code = error.read(), error.code
        except (Exception, KeyboardInterrupt) as error:
            code = 130 if isinstance(error, KeyboardInterrupt) else 124 if isinstance(error, (TimeoutError, socket.timeout)) else 0
            receipt = self.record(["HTTP", method, url, "request-sha256=" + sha(data or b"")], started, code, (type(error).__name__ + ": " + self.redact(str(error))).encode(), HERE)
            receipt["transport_failure"] = type(error).__name__
            write(self.output / "commands.json", self.commands)
            raise
        receipt = self.record(["HTTP", method, url, "request-sha256=" + sha(data or b"")], started, code, b"Confidential fixture response omitted" if confidential else output, HERE)
        if value is not None:
            observed = json.loads(canonical(value))
            if isinstance(observed, dict) and isinstance(observed.get("paging"), dict) and "cursor" in observed["paging"]:
                observed["paging"]["cursor"] = {"sha256": sha(observed["paging"]["cursor"].encode())}
            safe(observed)
            receipt["request_observation"] = observed
            receipt["request_observation_sha256"] = sha(canonical(observed))
        if confidential:
            receipt["output_sha256"] = sha(output)
            decoded = json.loads(output)
            receipt["confidential_observations"] = {"cursor_sha256": sha(decoded["cursor"].encode())} if isinstance(decoded, dict) and "cursor" in decoded else {}
        write(self.output / "commands.json", self.commands)
        require(code < 400, "HTTP request failed: " + str(code))
        return json.loads(output) if output else None

    def command(self, lane, tenant, aggregate, kind="AssertCounter", count=0, expected=True):
        self.last_actor_correlation = uuid.uuid4().hex
        result = self.probe(lane, "actor", f"http://127.0.0.1:{self.sidecar_port}", tenant, aggregate, self.last_actor_correlation, kind, str(count))
        self.assertion(result["accepted"] == expected, "Actor/domain outcome differs")
        self.assertion(result["eventCount"] == (1 if kind == "IncrementCounter" and expected else 0), "Unexpected domain-event count")
        return result

    def seed(self, lane, database, interval=1000):
        self.start_nodes(lane, database, interval)
        for tenant, count in (("tenant-a", 12), ("tenant-b", 3)):
            result = self.probe(lane, "seed", f"http://127.0.0.1:{self.sidecar_port}", tenant, "fixture", str(count))
            self.assertion(result["committedEvents"] == count and result["hydratedCount"] == count, "Persisted seed or actor hydration mismatch")
            self.assertion(result["assertions"] == 2 * (count + 1), "Missing actor command assertions")
            self.current_assertions += result["assertions"]
        self.stop_nodes()
        return self.inventory(database)

    def live(self, scenario, directions=None):
        cases = []
        incompatible = False
        supplied_directions = directions is not None
        directions = directions or (VERSIONS, tuple(reversed(VERSIONS)))
        if scenario == "metadata-write" and not supplied_directions:
            directions = (VERSIONS,)
        variants = ("absent", "non-covering") if scenario == "retained-uncovered" else ("interior", "tail") if scenario == "missing-event" else ("default",)
        for writer, reader, variant in ((w, r, v) for w, r in directions for v in variants):
            def execute(writer=writer, reader=reader, variant=variant):
                nonlocal incompatible
                database = self.database()
                interval = 10 if scenario in {"snapshot-tail", "retained-covered", "metadata-write", "retained-uncovered"} else 1000
                original = self.seed(writer, database, interval)
                self.assertion(len([r for r in original["domain_rows"] if ":events:" in r["key"]]) == 15, "Committed seed events missing")
                if scenario in {"retained-covered", "retained-uncovered", "metadata-write"}:
                    self.sql(database, "update state set value=jsonb_set(value,'{retainedFloor}','5') where key like '%tenant-a:counter:fixture:metadata'; delete from state where key like '%tenant-a:counter:fixture:events:%' and (split_part(key,':',7))::int < 5;")
                if scenario == "retained-uncovered":
                    self.sql(database, "delete from state where key like '%tenant-a:counter:fixture:snapshot';" if variant == "absent" else "update state set value=jsonb_set(jsonb_set(value,'{sequenceNumber}','2'),'{state,count}','2') where key like '%tenant-a:counter:fixture:snapshot';")
                if scenario == "missing-event":
                    self.sql(database, f"delete from state where key like '%tenant-a:counter:fixture:events:{7 if variant == 'interior' else 12}';")
                before = self.inventory(database)
                self.persist_inventory(scenario + "-" + writer + "-to-" + reader + "-" + variant + "-before", before)
                self.start_nodes(reader, database, interval)
                rejected = scenario in {"retained-uncovered", "missing-event"}
                outcome = self.command(reader, "tenant-a", "fixture", count=12, expected=not rejected)
                rejected_correlation = self.last_actor_correlation
                self.command(reader, "tenant-b", "fixture", count=3)
                self.assertion(self.http("GET", f"http://127.0.0.1:{self.host_port}/sequence/tenant-a/fixture") == 12, "Sequence changed on replay")
                if scenario == "metadata-write":
                    self.command(reader, "tenant-a", "fixture", "IncrementCounter")
                self.stop_nodes()
                failure_receipts = [c for c in self.commands if any(str(a).endswith("/" + reader + "/host/bin/" + ("Debug" if reader == "current" else "Release") + "/net10.0/Host.dll") for a in c["argv"]) and any(f["correlation_id"] == rejected_correlation and f["category"] == "missing-event" for f in c.get("runtime_failures", []))] if rejected else []
                if rejected:
                    self.assertion(bool(failure_receipts), "Rejection lacks explicit missing-event runtime category")
                after = self.inventory(database)
                self.persist_inventory(scenario + "-" + writer + "-to-" + reader + "-" + variant + "-after", after)
                old = {r["key"]: r["sha256"] for r in before["domain_rows"] if ":events:" in r["key"]}
                new = {r["key"]: r["sha256"] for r in after["domain_rows"] if ":events:" in r["key"]}
                self.assertion(all(new.get(k) == h for k, h in old.items()), "Prior event bytes rewritten")
                if scenario != "metadata-write":
                    self.assertion(before["domain_rows"] == after["domain_rows"], "Replay mutated domain state")
                else:
                    metadata = next(r for r in after["domain_rows"] if "tenant-a" in r["key"] and r["key"].endswith(":metadata"))
                    incompatible |= metadata["floor"] != "5"
                    self.assertion(metadata["sequence"] == "13", "Rollback append sequence wrong")
                return {"writer": writer, "reader": reader, "variant": variant, "database_identity_sha256": sha(database.encode()), "before_sha256": before["sha256"], "after_sha256": after["sha256"], "inventory_commands": {"before_sha256": before["query_command_id"], "after_sha256": after["query_command_id"]}, "actor_outcome": outcome, "failure_category": "missing-event" if rejected else None, "failure_command_ids": [c["id"] for c in failure_receipts], "bookkeeping_rows_before": len(before["rows"]) - len(before["domain_rows"]), "bookkeeping_rows_after": len(after["rows"]) - len(after["domain_rows"])}
            cases.append(self.case(writer + "-to-" + reader + ("-" + variant if variant != "default" else ""), execute))
        return cases, "incompatible" if incompatible else "compatible"

    def restore(self, scenario):
        command_start = len(self.commands)
        lane = VERSIONS[0] if scenario == "post-upgrade-restore" else VERSIONS[1]
        database = self.database()
        before = self.seed(lane, database, 10)
        dump = self.run(["docker", "exec", self.pg, "pg_dump", "-U", "postgres", "-Fc", database], binary=True)
        path = self.scratch / (scenario + ".dump")
        path.write_bytes(dump)
        self.assertion(len(dump) > 0, "Empty backup")
        if scenario == "pre-upgrade-restore":
            self.start_nodes(VERSIONS[0], database, 10)
            self.command(VERSIONS[0], "tenant-a", "fixture", "IncrementCounter")
            self.stop_nodes()
            self.assertion(self.inventory(database)["domain_rows"] != before["domain_rows"], "Selected writer did not advance source")
        restored = self.database()
        self.run(["docker", "exec", "-i", self.pg, "pg_restore", "-U", "postgres", "-d", restored, "--exit-on-error"], input_bytes=dump)
        copied = self.inventory(restored)
        self.assertion(before["rows"] == copied["rows"], "Committed backup inventory differs")
        self.start_nodes(VERSIONS[1], restored, 10)
        for tenant, count in (("tenant-a", 12), ("tenant-b", 3)):
            self.command(VERSIONS[1], tenant, "fixture", count=count)
            self.assertion(self.http("GET", f"http://127.0.0.1:{self.host_port}/sequence/{tenant}/fixture") == count, "Restored sequence differs")
        self.stop_nodes()
        after = self.inventory(restored)
        self.assertion(before["domain_rows"] == after["domain_rows"], "Restored replay mutated domain data")
        self.persist_inventory(scenario + "-before", before)
        self.persist_inventory(scenario + "-restored", copied)
        self.persist_inventory(scenario + "-after", after)
        return [{"id": "quiesced-full-backup", "assertions": self.current_assertions, "command_ids": [c["id"] for c in self.commands[command_start:]], "backup_sha256": sha(dump), "backup_bytes": len(dump), "source_inventory_sha256": before["sha256"], "restored_inventory_sha256": copied["sha256"], "restored_replay_sha256": after["sha256"], "inventory_commands": {"source_inventory_sha256": before["query_command_id"], "restored_inventory_sha256": copied["query_command_id"], "restored_replay_sha256": after["query_command_id"]}, "containment_only": scenario == "pre-upgrade-restore"}], "compatible"

    def invalid(self, lanes=VERSIONS):
        cases = []
        unsafe = False
        mutations = {
            "invalid-floor": "update state set value=jsonb_set(value,'{retainedFloor}','0') where key like '%tenant-a:counter:fixture:metadata'",
            "unreadable": "update state set value='\"unreadable-fixture\"'::jsonb where key like '%tenant-a:counter:fixture:events:7'",
            "protected": "update state set value=jsonb_set(value,'{serializationFormat}','\"json+pdenc-v1\"') where key like '%tenant-a:counter:fixture:events:7'",
            "unknown-type": "update state set value=jsonb_set(value,'{eventTypeName}','\"P1R.UnknownEvent\"') where key like '%tenant-a:counter:fixture:events:7'",
            "unknown-version": "update state set value=jsonb_set(value,'{metadataVersion}','987') where key like '%tenant-a:counter:fixture:events:7'",
        }
        for lane in lanes:
            for mutation, sql in mutations.items():
                def execute(lane=lane, mutation=mutation, sql=sql):
                    nonlocal unsafe
                    database = self.database()
                    self.seed(lane, database)
                    self.sql(database, sql)
                    before = self.inventory(database)
                    self.start_nodes(lane, database)
                    # Record actual acceptance as an incompatible negative control, without weakening rejection requirements.
                    outcome = self.probe(lane, "actor", f"http://127.0.0.1:{self.sidecar_port}", "tenant-a", "fixture", uuid.uuid4().hex, "AssertCounter", "12")
                    unsafe |= outcome["accepted"]
                    self.assertion(outcome["eventCount"] == 0, "Invalid evidence appended domain events")
                    self.stop_nodes()
                    after = self.inventory(database)
                    self.assertion(before["domain_rows"] == after["domain_rows"], "Invalid evidence mutated aggregate state")
                    self.persist_inventory("invalid-" + lane + "-" + mutation + "-before", before)
                    self.persist_inventory("invalid-" + lane + "-" + mutation + "-after", after)
                    return {"lane": lane, "mutation": mutation, "handling": "accepted-unsafe" if outcome["accepted"] else "rejected", "actor_outcome": outcome, "before_sha256": before["sha256"], "after_sha256": after["sha256"], "inventory_commands": {"before_sha256": before["query_command_id"], "after_sha256": after["query_command_id"]}}
                cases.append(self.case(lane + "-" + mutation, execute))
        return cases, "incompatible" if unsafe else "compatible"

    def checkout(self):
        cases = [self.case("current-build", self.build_current)]
        disposition = "compatible"
        operations = {
            "legacy-metadata": lambda: self.metadata("legacy-metadata", "current"),
            "query-wire": lambda: self.wire("query", (VERSIONS[0], "current")),
            "projection-wire": lambda: self.wire("projection", (VERSIONS[0], "current")),
            **{name: (lambda name=name: self.live(name, ((VERSIONS[0], "current"), ("current", VERSIONS[0])))) for name in ("full-replay", "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "metadata-write")},
            "invalid-evidence": lambda: self.invalid(("current",)),
        }
        for name, action in operations.items():
            def compare(action=action):
                nonlocal disposition
                nested, compatibility = action()
                if compatibility != "compatible":
                    disposition = "incompatible"
                return {"cases": nested, "compatibility": compatibility}
            cases.append(self.case(name, compare))
            self.stop_nodes()
        return cases, disposition

    def mixed(self):
        cases = []
        for lane, label in ((VERSIONS[1], "old"), (VERSIONS[0], "selected")):
            def cursor_scope_control(lane=lane):
                outcome = self.probe(lane, "cursor-scope")
                if lane == VERSIONS[0]:
                    self.assertion(outcome["handling"] == "executed" and outcome["scope"] == "tenant:tenant-a|watermark:987", "Selected cursor watermark scope failed")
                    self.assertion(outcome["invalid_watermark_rejected"] is True, "Non-authoritative cursor watermark accepted")
                else:
                    self.assertion(outcome["handling"] == "unsupported-client-method", "Old cursor watermark helper unexpectedly supported")
                return outcome
            cases.append(self.case(label + "-cursor-scope", cursor_scope_control))
        for client, host in (VERSIONS, tuple(reversed(VERSIONS))):
            def execute(client=client, host=host):
                database = self.database()
                self.seed(host, database)
                before = self.inventory(database)
                self.start_nodes(host, database)
                self.command(client, "tenant-a", "fixture", count=12)
                self.command(client, "tenant-b", "fixture", count=3)
                self.stop_nodes()
                after = self.inventory(database)
                self.assertion(before["domain_rows"] == after["domain_rows"], "Mixed calls mutated prior state")
                self.persist_inventory("mixed-" + client + "-" + host + "-before", before)
                self.persist_inventory("mixed-" + client + "-" + host + "-after", after)
                return {"client": client, "host": host, "calls": ["ProcessCommandAsync", "AssertCounter"], "before_sha256": before["sha256"], "after_sha256": after["sha256"], "inventory_commands": {"before_sha256": before["query_command_id"], "after_sha256": after["query_command_id"]}}
            cases.append(self.case(client + "-client-" + host + "-host", execute))
        status_command_start = len(self.commands)
        database = self.database()
        self.start_nodes(VERSIONS[0], database)
        status_value = {"status": 5, "timestamp": "2026-01-01T00:00:00Z", "aggregateId": "fixture", "eventCount": 0, "rejectionEventType": None, "failureReason": None, "timeoutDuration": None, "messageId": "fixture-status", "correlationId": "fixture-correlation", "retryable": True, "recoveryReasonCode": "fixture-recovery", "drainAttemptCount": 2}
        self.http("POST", f"http://127.0.0.1:{self.host_port}/status/tenant-a/fixture-status", status_value)
        status_write_id = self.commands[-1]["id"]
        selected_status = self.http("GET", f"http://127.0.0.1:{self.host_port}/status/tenant-a/fixture-status")
        selected_status_id = self.commands[-1]["id"]
        self.stop_nodes()
        self.start_nodes(VERSIONS[1], database)
        def status_control():
            old_status = self.http("GET", f"http://127.0.0.1:{self.host_port}/status/tenant-a/fixture-status")
            self.assertion(selected_status["retryable"] is True, "Selected status fixture not persisted")
            self.assertion("retryable" not in old_status, "Expected old status shape changed")
            return {"handling": "lost-recovery-tristate", "selected_keys": sorted(selected_status), "old_keys": sorted(old_status), "prerequisite_command_ids": [status_write_id, selected_status_id]}
        cases.append(self.case("status-downgrade", status_control))
        self.stop_nodes()
        cases[-1]["prerequisite_command_ids"] = [c["id"] for c in self.commands[status_command_start:]]
        for mint, consume, identity in ((VERSIONS[0], VERSIONS[1], "cursor-downgrade"), (VERSIONS[1], VERSIONS[0], "cursor-upgrade")):
            def cursor_control(mint=mint, consume=consume):
                self.start_nodes(mint, database)
                cursor = self.http("GET", f"http://127.0.0.1:{self.domain_port}/cursor-mint", confidential=True)["cursor"]
                self.stop_nodes()
                self.start_nodes(consume, database)
                query = {"tenantId": "tenant-a", "domain": "counter", "aggregateId": "fixture", "queryType": "fixture", "payload": "e30=", "correlationId": "fixture-correlation", "userId": "fixture-user", "paging": {"cursor": cursor}}
                response = self.http("POST", f"http://127.0.0.1:{self.domain_port}/query", query)
                decoded = json.loads(base64.b64decode(response["payloadBytes"]))
                self.assertion(response["success"] and decoded["decoded"] and decoded["position"] == "position-3", "Cross-package cursor decode failed")
                query["paging"]["cursor"] = cursor + "invalid"
                response = self.http("POST", f"http://127.0.0.1:{self.domain_port}/query", query)
                self.assertion(json.loads(base64.b64decode(response["payloadBytes"]))["decoded"] is False, "Tampered cursor accepted")
                self.stop_nodes()
                return {"mint": mint, "consume": consume, "cursor_sha256": sha(cursor.encode()), "scope": "tenant-a|watermark:987", "key_material_retained": False, "decode_outcome": decoded, "tamper_rejected": True}
            cases.append(self.case(identity, cursor_control))
        for host, label in ((VERSIONS[1], "old"), (VERSIONS[0], "selected")):
            host_start = len(self.commands)
            self.start_nodes(host, database)
            host_identity_ids = [c["id"] for c in self.commands[host_start:]]
            case_start = len(cases)
            for method in ("ProcessFencedCommandAsync", "ProcessTrustedEffectAsync", "GetRetainedFloorAsync"):
                def capability_control(method=method, host=host):
                    response = self.probe(VERSIONS[0], "capability", f"http://127.0.0.1:{self.sidecar_port}", method)
                    detail = response.get("detail", "")
                    if host == VERSIONS[1]:
                        old_methods = json.loads((self.output / "artifacts" / (host + "-identity.json")).read_text())["loaded"]["actorMethods"]
                        contract_missing = method + "ReqBody" in detail and "deserializer has no knowledge" in detail
                        method_missing = bool(re.search(r"(?i)(method.*(?:not found|not supported|does not exist|not implemented)|(?:missing|unknown).*method|MissingMethodException|KeyNotFoundException)", detail))
                        self.assertion(method not in old_methods, "Old actor interface unexpectedly declares selected API")
                        self.assertion(response["outcome"] == "rejected" and (contract_missing or method_missing), "Old dispatcher absence not proven")
                    elif method == "GetRetainedFloorAsync":
                        self.assertion(response["outcome"] == "returned", "Selected floor API unavailable")
                    else:
                        self.assertion(response["outcome"] == "rejected" and "ArgumentNullException" in detail, "Selected API did not validate its input")
                    return {"method": method, "outcome": response["outcome"], "detail_sha256": sha(detail.encode()), "exception": response.get("exception"), "handling": "unsupported-old-actor-contract" if host == VERSIONS[1] else "selected-dispatcher-executed", "input": "null negative validation control" if method != "GetRetainedFloorAsync" else "no arguments"}
                cases.append(self.case(label + "-dispatcher-" + method, capability_control))
            self.stop_nodes()
            host_launch_ids = [c["id"] for c in self.commands[host_start:] if c["argv"][:1] == ["dotnet"] and str(c["argv"][1]).endswith(("/Host.dll", "/Domain.dll")) or c["argv"][:1] == [str(self.daprd)]]
            for case in cases[case_start:]:
                case["prerequisite_command_ids"] = host_identity_ids + host_launch_ids
        return cases, "incompatible"


def preserved():
    paths = [PROJECTS / "_bmad-output/implementation-artifacts/6-1-p1r-acceptance.json", PROJECTS / "_bmad-output/implementation-artifacts/sprint-status.yaml"]
    paths += [p for p in HERE.parent.rglob("*") if p.is_file() and not p.is_relative_to(HERE)]
    return {str(p.relative_to(PROJECTS)): sha(p.read_bytes()) for p in paths}


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--out", type=pathlib.Path)
    group.add_argument("--validate", type=pathlib.Path)
    options = parser.parse_args(arguments)
    if options.validate:
        try:
            results = validate(options.validate)
        except (ValueError, KeyError, OSError, json.JSONDecodeError) as error:
            print("INVALID: " + str(error), file=sys.stderr)
            return 2
        print("VALID: complete hash-bound investigation; P1R remains unqualified")
        return 0
    output = options.out.resolve()
    require(not output.exists(), "Refusing to overwrite a retained result")
    require(not output.is_relative_to(HERE / "host") and not output.is_relative_to(HERE / "domain") and not output.is_relative_to(HERE / "probe"), "Output overlaps fixtures")
    output.mkdir(parents=True)
    original = preserved()
    run = Runner(output)
    manifest = {"schema": "hexalith.p1r.verification.v1", "started_utc": utc(), "coordinates": SOURCES, "versions": list(VERSIONS), "runtime": {"dapr": "1.18.2", "postgresql": POSTGRES, "discovery": "private SQLite v1 (Alpha; fixture only)"}, "usable_as_prerequisite": False, "owner_acceptance_granted": False, "preserved_before": original, "fixture_hashes": {}}
    try:
        for relative in sorted(FIXTURES):
            started = utc()
            try:
                manifest["fixture_hashes"][relative] = sha((HERE / relative).read_bytes())
            except OSError as error:
                run.record(["read-fixture", str(HERE / relative)], started, 127, str(error).encode(), HERE)
                raise
        run.initialize()
        run.scenario("provenance", run.prepare)
        run.scenario("legacy-metadata", lambda: run.metadata("legacy-metadata"))
        run.scenario("metadata-read", lambda: run.metadata("metadata-read"))
        run.scenario("query-wire", lambda: run.wire("query"))
        run.scenario("projection-wire", lambda: run.wire("projection"))
        run.topology()
        for name in ("full-replay", "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "metadata-write"):
            run.scenario(name, lambda name=name: run.live(name))
            run.stop_nodes()
        run.scenario("checkout", run.checkout)
        run.scenario("invalid-evidence", run.invalid)
        checkout_row = next(r for r in run.rows if r["id"] == "checkout")
        invalid_row = next(r for r in run.rows if r["id"] == "invalid-evidence")
        if checkout_row["execution"] == "passed" and invalid_row["execution"] == "passed":
            shared = {c["id"]: c.get("compatibility") for c in checkout_row["cases"] if c["id"] != "current-build"}
            selected_invalid = {c["id"].removeprefix(VERSIONS[0] + "-"): c["handling"] for c in invalid_row["cases"] if c["id"].startswith(VERSIONS[0] + "-")}
            source_invalid = {c["id"].removeprefix("current-"): c["handling"] for c in next(c for c in checkout_row["cases"] if c["id"] == "invalid-evidence")["cases"]}
            write(output / "shared-scope-comparison.json", {"selected": VERSIONS[0], "current_source": SOURCES["current"], "shared_case_dispositions": shared, "selected_invalid_handling": selected_invalid, "current_invalid_handling": source_invalid, "shared_scope_equivalent": all(v == "compatible" for k,v in shared.items() if k != "invalid-evidence") and selected_invalid == source_invalid, "invalid_evidence_remains_incompatible": shared["invalid-evidence"] == "incompatible", "outside_scope": "Reminder contracts/actors, conditional registration via AddEventStoreReminders and Dapr.Actors.AspNetCore dependency; no compatibility claim for these additions."})
        run.stop_nodes()
        run.scenario("mixed-api", run.mixed)
        for name in ("post-upgrade-restore", "pre-upgrade-restore"):
            run.scenario(name, lambda name=name: run.restore(name))
            run.stop_nodes()
    except (Exception, KeyboardInterrupt) as error:
        manifest["interruption"] = type(error).__name__ + ": " + run.redact(str(error))
    finally:
        try:
            run.stop_nodes()
        except (Exception, KeyboardInterrupt) as error:
            manifest["node_cleanup_interruption"] = type(error).__name__ + ": " + run.redact(str(error))
        # Execute startup-failure, timeout and repeated process cleanup controls using invocation-owned processes.
        def failure_controls():
            cases = []
            for name, command, timeout in (("startup-failure", [sys.executable, "-c", "raise SystemExit(7)"], 2), ("timeout", [sys.executable, "-c", "import time; time.sleep(10)"], .05), ("cancellation", [sys.executable, "-c", "import time; time.sleep(10)"], 2)):
                before = run.current_assertions
                command_start = len(run.commands)
                previous = signal.getsignal(signal.SIGALRM)
                try:
                    if name == "cancellation":
                        def cancel_owned_command(signum, frame):
                            raise KeyboardInterrupt()
                        signal.signal(signal.SIGALRM, cancel_owned_command)
                        signal.setitimer(signal.ITIMER_REAL, .05)
                    run.run(command, timeout=timeout, check=False)
                except (TimeoutError, KeyboardInterrupt):
                    pass
                finally:
                    signal.setitimer(signal.ITIMER_REAL, 0)
                    signal.signal(signal.SIGALRM, previous)
                run.assertion(run.commands[-1]["exit_code"] == (7 if name == "startup-failure" else 130 if name == "cancellation" else 124), "Failure control not exercised")
                process = run.processes[-1]
                run.stop(process)
                run.stop(process)
                run.assertion(process.poll() is not None, "Repeated cleanup failed")
                cases.append({"id": name, "assertions": run.current_assertions - before, "command_ids": [c["id"] for c in run.commands[command_start:]]})
            return cases, "compatible"
        try:
            run.scenario("failure-cleanup", failure_controls)
        except (Exception, KeyboardInterrupt) as error:
            manifest["cleanup_control_interruption"] = type(error).__name__ + ": " + run.redact(str(error))
        cleanup = run.cleanup()
        for name in SCENARIOS:
            if not any(r["id"] == name for r in run.rows):
                run.rows.append({"id": name, "execution": "unavailable", "compatibility": "unverified", "assertions": 0, "command_ids": [run.commands[-1]["id"]], "cases": [{"id": "blocked", "assertions": 0, "reason": manifest.get("interruption", "Prerequisite unavailable")}], "reason": manifest.get("interruption", "Prerequisite unavailable")})
        manifest.update(finished_utc=utc(), preserved_after=preserved())
        results = {"qualified": False, "scenarios": sorted(run.rows, key=lambda r: SCENARIOS.index(r["id"])), "exit_code": 0 if all(r["execution"] == "passed" and r["compatibility"] == "compatible" for r in run.rows) and not cleanup["errors"] else 1}
        write(output / "manifest.json", manifest)
        write(output / "scenario-results.json", results)
        write(output / "commands.json", run.commands)
        (output / "README.md").write_text("# P1R verification invocation\n\nThis is an investigation, not owner acceptance. `qualified` and P1R usability remain false.\n\n" + "\n".join(f"- {r['id']}: {r['execution']} / {r['compatibility']} ({r['assertions']} assertions)" for r in results["scenarios"]) + "\n\nPre-upgrade restore is containment mechanics only; later writes are absent from that backup. Incompatible or unavailable lanes require a later named owner decision. Commands, exact package graphs, assembly hashes, inventories and cleanup are hash-bound. Dump bytes and credentials were destroyed with the invocation scratch.\n")
        seal(output)
    try:
        validate(output)
    except (ValueError, KeyError, OSError, json.JSONDecodeError) as error:
        print("RETAINED NONPASSING: " + str(error), file=sys.stderr)
        return 2
    return results["exit_code"]


if __name__ == "__main__":
    raise SystemExit(main())
