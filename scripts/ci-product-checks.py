#!/usr/bin/env python3
"""Retained product checks using the single shared package-mode Release build."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

from legacy_sidecar_runtime import legacy_sidecar_runtime, local_docker_environment


ROOT = Path(__file__).resolve().parents[1]
POSTGRES = "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
KIND_IMAGE = "kindest/node:v1.35.0@sha256:452d707d4862f52530247495d180205e029056831160e22870e37e3f6c1ac31f"


def run(*arguments: str, environment: dict | None = None) -> None:
    subprocess.run(arguments, cwd=ROOT, env=environment, check=True)


def test(project: str, output: Path, minimum: int, *options: str, environment: dict | None = None) -> None:
    run("dotnet", "test", "--project", f"tests/{project}/{project}.csproj", "--no-build", "--no-restore",
        "--configuration", "Release", "-p:UseHexalithProjectReferences=false", "--fail-skips", "on",
        "--minimum-expected-tests", str(minimum), "--results-directory", str(output),
        "--report-xunit-trx", *options, environment=environment)


def commitlint_fixture_environment(directory: Path) -> dict[str, str]:
    # Retained source-trust fixtures need the signed, locked CLI after root npm
    # tooling retires too. The selected direct Builds supplies tool policy.
    sys.path.insert(0, str(ROOT / "references/Hexalith.Builds/Github/lib"))
    from guard_tools import SOURCE, Tools

    directory.mkdir()
    tools = Tools(directory)
    policy = ROOT / "commitlint.config.mjs"
    if policy.is_file():
        configuration = directory / "eventstore.commitlint.config.mjs"
        configuration.write_bytes(policy.read_bytes())
    else:
        configuration = SOURCE / "commitlint.config.mjs"
    return {"EXT_PROTECTION_COMMITLINT_CLI": str(directory / "node_modules/@commitlint/cli/cli.js"),
            "EXT_PROTECTION_COMMITLINT_CONFIG": str(configuration),
            **{name: tools.environment[name] for name in ("HEXALITH_GUARD_TOOLS", "HEXALITH_GUARD_PYTHON")}}


def offline(output: Path) -> None:
    # The live project's offline schema class never receives Azure switches or secrets.
    test("Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests", output / "azure-schema", 15,
         "--filter-class", "Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests.AzureIdentityAuthoritySchemaTests")
    test("Hexalith.EventStore.PayloadProtection.Tests", output / "payload", 324)
    environment = os.environ.copy()
    environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1"
    test("Hexalith.EventStore.PayloadProtection.Tests", output / "payload-invariant", 1,
         "--filter-method", "Hexalith.EventStore.PayloadProtection.Tests.AadPathTests.V029_InertPlatformNormalizer_FailsClosedThroughCoreWriteSeams",
         environment=environment)
    with tempfile.TemporaryDirectory(prefix="eventstore-package-checks-") as temporary:
        scratch = Path(temporary)
        tools = scratch / "api-tool"
        run("dotnet", "tool", "install", "Microsoft.DotNet.ApiCompat.Tool", "--version", "11.0.100",
            "--tool-path", str(tools))
        environment = os.environ.copy()
        environment.update(commitlint_fixture_environment(scratch / "commitlint"))
        environment["RELEASE_API_COMPAT_TOOL_DLL"] = str(tools / ".store/microsoft.dotnet.apicompat.tool/11.0.100/microsoft.dotnet.apicompat.tool/11.0.100/tools/net10.0/any/Microsoft.DotNet.ApiCompat.Tool.dll")
        for pattern in ("test_release_package_contract.py", "test_package_consumer_environment.py",
                        "test_release_api_compatibility.py", "test_ext_protection_1*.py",
                        "test_dapr*qualification.py", "test_route_catalog_ci_tools.py",
                        "test_published_runtime_archives.py", "test_qualify_internal_actor_admission.py",
                        "test_managed_product_fixtures.py"):
            run(sys.executable, "-B", "-m", "unittest", "discover", "-s", "tools/tests", "-p", pattern,
                environment=environment)
        packages = scratch / "packages"
        run(sys.executable, "scripts/pack-release-packages.py", str(packages), "999.0.0-ci-test", "--no-build")
        run(sys.executable, "scripts/validate-nuget-packages.py", str(packages))
        # The DomainService consumer executes the hosted checkpointed-process proof.
        run(sys.executable, "scripts/validate-consumer-package-references.py", str(packages))
        run("bash", "tools/folded-snapshot-compatibility/run.sh",
            environment={**os.environ, "FOLDED_SNAPSHOT_REUSE_OUTPUTS": "true"})


def integration(output: Path) -> None:
    # This command is a dedicated stage process. Normalize its inherited Docker
    # selection once so every child, including fixture-owned tools, sees it.
    environment = local_docker_environment()
    for name in ("DOCKER_CONTEXT", "DOCKER_TLS_VERIFY", "DOCKER_CERT_PATH"):
        os.environ.pop(name, None)
    os.environ["DOCKER_HOST"] = environment["DOCKER_HOST"]
    run("docker", "pull", POSTGRES)
    run(sys.executable, "tools/prepare-route-catalog-tools.py")
    tools = Path(os.environ["TMPDIR"]) / "route-catalog-tools"
    environment = os.environ.copy()
    environment.update(EVENTSTORE_ROUTE_CATALOG_DAPRD=str(tools / "daprd"),
                       EVENTSTORE_ROUTE_CATALOG_DAPR_CLI=str(tools / "dapr"),
                       EVENTSTORE_PAYLOAD_DAPRD=str(tools / "daprd"))
    with legacy_sidecar_runtime(Path(os.environ["TMPDIR"]) / "legacy-live-sidecar") as selected:
        test("Hexalith.EventStore.Server.LiveSidecar.Tests", output / "live-sidecar", 1,
             environment={**environment, **selected})
    cache = Path(os.environ["TMPDIR"]) / "published-runtime"
    run(sys.executable, "tools/published_runtime_archives.py", "prepare", "--destination", str(cache))
    environment.update(P1R_DAPR_RUNTIME_ARCHIVE_CACHE=str(cache),
                       P1R_QUALIFICATION_OUTPUT_ROOT=str(output / "published"),
                       P1R_QUALIFICATION_SCRATCH_ROOT=str(Path(os.environ["TMPDIR"]) / "published"))
    run("docker", "pull", "redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf")
    test("Hexalith.EventStore.PublishedCompatibility.Tests", output / "published", 1, environment=environment)
    test("Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests", output / "payload-lifecycle", 31, environment=environment)
    profile = "deploy/dapr/qualification/runtime-lock-1.19.1.json"
    destination = Path(os.environ["TMPDIR"]) / "kubernetes-crypto-tools"
    run(sys.executable, "tools/install-dapr-qualification-tools.py", "--profile", profile, "--destination", str(destination))
    # Fetch the pinned kind executable only inside this owned fixture directory.
    run("curl", "--fail", "--location", "--silent", "--show-error", "--output", str(destination / "kind"),
        "https://github.com/kubernetes-sigs/kind/releases/download/v0.31.0/kind-linux-amd64")
    if hashlib.sha256((destination / "kind").read_bytes()).hexdigest() != "eb244cbafcc157dff60cf68693c14c9a75c4e6e6fedaf9cd71c58117cb93e3fa":
        raise ValueError("kind 0.31.0 checksum mismatch")
    (destination / "kind").chmod(0o755)
    run("docker", "pull", KIND_IMAGE)
    environment.update(QUALIFICATION_DAPRD=str(destination / "runtime/daprd"), HEXALITH_PP_KIND=str(destination / "kind"))
    try:
        test("Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests", output / "kubernetes-crypto", 14,
             "--filter-trait", "Lane=KubernetesCrypto", "--explicit", "only", environment=environment)
    finally:
        clusters = subprocess.check_output([str(destination / "kind"), "get", "clusters"], text=True, env=environment)
        if any(name.startswith("pp-k8s-") for name in clusters.splitlines()):
            raise ValueError("A product-fixture kind cluster survived cleanup")


def brokers(output: Path) -> None:
    if os.environ.get("RUNNER_ENVIRONMENT") != "github-hosted":
        raise ValueError("Broker qualification requires an exclusive disposable hosted worker")
    endpoint = "unix://" + str(Path("/var/run/docker.sock").resolve())
    daemon = subprocess.check_output(["docker", "--host", endpoint, "info", "--format", "{{.ID}}"], text=True).strip()
    lease = Path(os.environ["TMPDIR"]) / "exclusive-worker.json"
    lease.write_text(json.dumps({"exclusive": True, "endpoint": endpoint, "daemonId": daemon,
                                "workerId": "-".join(os.environ[key] for key in ("GITHUB_RUN_ID", "GITHUB_JOB", "GITHUB_RUN_ATTEMPT"))}))
    for name in ("runtime-lock.json", "runtime-lock-1.19.1.json"):
        profile = "deploy/dapr/qualification/" + name
        destination = Path(os.environ["TMPDIR"]) / name.removesuffix(".json")
        run(sys.executable, "tools/install-dapr-qualification-tools.py", "--profile", profile, "--destination", str(destination))
        environment = os.environ.copy()
        environment.update(QUALIFICATION_DAPRD=str(destination / "runtime/daprd"),
                           PATH=str(destination / "cli") + os.pathsep + os.environ["PATH"])
        packet = output / name.removesuffix(".json")
        run(sys.executable, "tools/qualify-dapr-brokers.py", "--profile", profile,
            "--exclusive-worker", str(lease), "--brokers", "rabbitmq,kafka", "--output", str(packet), environment=environment)
        run(sys.executable, "tools/validate-dapr-broker-qualification.py", str(packet), environment=environment)
        # A technical pass never manufactures production authority.
        result = subprocess.run([sys.executable, "tools/validate-dapr-broker-qualification.py", str(packet),
                                 "--require-production-authority"], cwd=ROOT, env=environment, check=False)
        if result.returncode != 1:
            raise ValueError("Broker packet must retain its non-authorizing production-ineligible posture")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=("offline", "integration", "brokers"))
    args = parser.parse_args()
    output = ROOT / "TestResults" / args.stage
    output.mkdir(parents=True, exist_ok=True)
    {"offline": offline, "integration": integration, "brokers": brokers}[args.stage](output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
