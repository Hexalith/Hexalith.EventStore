#!/usr/bin/env python3
"""Exercise individual logical callback fences in private timed compiling lanes.

Passing local controls grants no catalog, live Dapr topology or activation authority.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib
import shutil
import tempfile
import time


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("callback_base", ROOT / "scripts/verify-dapr-logical-model-guards.py")
BASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BASE)
CLIENT = pathlib.Path("src/Hexalith.EventStore.Client/Events")
SERVER = pathlib.Path("src/Hexalith.EventStore.Server/Events")
TESTS = pathlib.Path("tests/Hexalith.EventStore.Server.Tests")
CLASS = "Hexalith.EventStore.Server.Tests.Events.DaprLogicalCallbackFenceTests"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--timeout", default=60, type=int)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    parser.add_argument("--dependency-mode", choices=("source", "packages"), default="source")
    args = parser.parse_args()
    if not 1 <= args.timeout <= 60:
        parser.error("timeout must be between 1 and 60 seconds per lane")
    BASE.CONFIGURATION = args.configuration
    BASE.DEPENDENCY_MODE = args.dependency_mode
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    refusal = "RepresentativeCallbackAuthorityLossControls"
    leases = "YieldingAddressedFenceSeesExpiredAllCallbackFacadesAndExactFinalState"
    mutations = [
        ("addressed-callable-fence", CLIENT / "EventCallbackFence.cs",
         "await sourceFence(token).ConfigureAwait(false);", "await Task.CompletedTask.ConfigureAwait(false);", 1, refusal),
        ("source-independent-validators", CLIENT / "EventEvolutionService.cs",
         "RequireVersionBindings, ValidateVersionAsync);", "RequireVersionBindings);", 1, refusal),
        ("source-current-trust-forwarding", SERVER / "DaprLogicalReplaySource.cs",
         "sourceFence: RequirePageCurrentAsync", "sourceFence: null", 1, refusal),
        ("operation-state-binding-forwarding", SERVER / "DaprLogicalReplaySource.cs",
         "await operationFence(token).ConfigureAwait(false);", "await Task.CompletedTask.ConfigureAwait(false);", 2, refusal),
        ("post-getter-addressed-fence", CLIENT / "EventImplementationBinding.cs",
         "await sourceFence(token).ConfigureAwait(false);\n            token.ThrowIfCancellationRequested();\n            RequireDeclaredFields(descriptor, implementationField);\n            RequireOptionsHash(options, token);",
         "RequireOptionsHash(options, token);", 1, refusal),
        ("schema-borrow-lifetime", CLIENT / "RegisteredEventVersionValidation.cs",
         "using (var schemaLease = new InvocationPayloadLease(payload, token))\n        {\n            try { _schema(domain, canonicalType, version, format, schemaLease, token); }\n            finally { token.ThrowIfCancellationRequested(); }\n        }",
         "using var schemaLease = new InvocationPayloadLease(payload, token);\n        _schema(domain, canonicalType, version, format, schemaLease, token);", 1, leases),
        ("identity-borrow-lifetime", CLIENT / "RegisteredEventVersionValidation.cs",
         "using (var identityLease = new InvocationPayloadLease(payload, token))\n        {\n            try { _identity(domain, canonicalType, version, format, identityLease, token); }\n            finally { token.ThrowIfCancellationRequested(); }\n        }",
         "using var identityLease = new InvocationPayloadLease(payload, token);\n        _identity(domain, canonicalType, version, format, identityLease, token);", 1, leases),
        ("deserializer-borrow-lifetime", CLIENT / "RegisteredCurrentEventDeserializer.cs",
         "using (var lease = new InvocationPayloadLease(effectivePayload, token))\n        {\n            try { value = _deserialize(lease, token); }\n            finally { token.ThrowIfCancellationRequested(); }\n        }",
         "using var lease = new InvocationPayloadLease(effectivePayload, token);\n        value = _deserialize(lease, token);", 1, leases),
        ("current-deserializer-individual-fence", CLIENT / "EventEvolutionService.cs",
         "await _deserializers[canonicalType].DeserializeAsync(_registry, canonicalType, payload,\n            sourceFence, cancellationToken).ConfigureAwait(false)",
         "_deserializers[canonicalType].Deserialize(_registry, canonicalType, payload, cancellationToken)", 1, refusal),
        ("successor-version-validation", CLIENT / "EventUpcastChainExecutor.cs",
         "await ValidateAsync(next, canonicalType, version + 1, sourceFence, cancellationToken).ConfigureAwait(false);",
         "await Task.CompletedTask.ConfigureAwait(false);", 1, leases),
        ("private-payload-clearing", CLIENT / "ImmutablePayload.cs",
         "CryptographicOperations.ZeroMemory(owner);", "GC.KeepAlive(owner);", 2, leases),
    ]
    def root_hashes():
        inputs = [path for directory in (BASE.CLIENT, BASE.SERVER, BASE.DOMAIN, BASE.CONTRACTS, BASE.DEFAULTS, BASE.UNIQUE_IDS)
                  for path in (ROOT / directory).rglob("*") if path.is_file() and "bin" not in path.parts and "obj" not in path.parts]
        inputs += list((ROOT / TESTS / "Events").glob("Dapr*.cs"))
        inputs += [pathlib.Path(__file__), ROOT / "scripts/verify-dapr-logical-model-guards.py",
                   ROOT / ".github/workflows/event-evolution-local-guards.yml"]
        return {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in inputs}
    hashes = root_hashes()
    rows = []
    with tempfile.TemporaryDirectory(prefix="eventstore-callback-guards-") as temporary:
        work = pathlib.Path(temporary)
        BASE.prepare(work)
        for pattern in ("DaprLogicalReconstruction*.cs", "DaprLogicalCallbackFenceTests.cs"):
            for path in (ROOT / TESTS / "Events").glob(pattern):
                shutil.copyfile(path, work / TESTS / "Events" / path.name)

        def execute(label):
            started = time.monotonic()
            project = work / TESTS / f"{TESTS.name}.csproj"
            build = BASE.run(["dotnet", "build", str(project), "--configuration", args.configuration,
                              "-p:UseHexalithProjectReferences=" + ("true" if args.dependency_mode == "source" else "false"),
                              "-m:1", "--nologo"], work, output / f"{label}-build.log", args.timeout)
            if build.returncode:
                raise RuntimeError(f"Lane did not compile: {label}")
            remaining = int(args.timeout - (time.monotonic() - started))
            if remaining < 1:
                raise RuntimeError(f"Lane exhausted deadline before tests: {label}")
            assembly = work / TESTS / f"bin/{args.configuration}/net10.0/{TESTS.name}.dll"
            result = BASE.run(["dotnet", str(assembly), "-method", f"{CLASS}.{refusal}", "-method", f"{CLASS}.{leases}"],
                              work, output / f"{label}-tests.log", remaining)
            after = root_hashes()
            BASE.COMMANDS[-1].update({"allRootInputsSha256Before": hashes, "allRootInputsSha256After": after,
                                     "rootInputSetUnchanged": hashes == after})
            if hashes != after:
                raise RuntimeError("Root callback inputs changed during lane")
            return result, round(time.monotonic() - started, 3)

        control, _ = execute("control")
        if control.returncode or "Failed: 0" not in control.stdout or "Skipped: 0" not in control.stdout:
            raise RuntimeError("Unmutated callback control failed")
        print("control: passed", flush=True)
        for name, relative, before, after, count, killer in mutations:
            path = work / relative
            original = path.read_bytes()
            try:
                source = original.decode().replace("\r\n", "\n")
                if source.count(before) != count:
                    raise RuntimeError(f"Mutation anchor missing/ambiguous: {name}")
                path.write_text(source.replace(before, after), encoding="utf-8")
                result, elapsed = execute(name)
                lines = result.stdout.splitlines()
                failures = [index for index, line in enumerate(lines) if "[FAIL]" in line and killer in line]
                if not result.returncode or not failures:
                    raise RuntimeError(f"Mutation survived or missed named killing test: {name}")
                rows.append({"mutation": name, "result": "killed", "killingTest": killer,
                             "failureExcerpt": lines[failures[0]:failures[0] + 14], "elapsedSeconds": elapsed})
                print(f"{name}: killed", flush=True)
            finally:
                path.write_bytes(original)
    receipt = {"scope": "dormant individual logical callback fences", "configuration": args.configuration,
               "dependencyMode": args.dependency_mode, "control": "passed", "mutations": rows,
               "timeoutSecondsPerLane": args.timeout, "allRootInputsSha256": hashes, "commands": BASE.COMMANDS,
               "actualCrossActorInvocation": False, "productionQualification": False, "activationAuthority": False}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
