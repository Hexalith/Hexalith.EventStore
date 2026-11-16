"""Offline failure controls for owned product fixtures; no cloud or daemon access."""

from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import unittest
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


runtime = load("legacy_sidecar_runtime", "legacy_sidecar_runtime.py")
azure = load("qualify_payload_azure", "qualify-payload-azure.py")
with mock.patch.dict(sys.modules, {"legacy_sidecar_runtime": runtime}):
    product = load("ci_product_checks", "ci-product-checks.py")


class FakeDocker:
    def __init__(self):
        self.rows = {"a" * 64: {"Id": "a" * 64, "Name": "/dapr_placement", "Image": "shared-1.19.1",
                               "HostConfig": {"PortBindings": {}}, "Mounts": [],
                               "NetworkSettings": {"Networks": {}}, "Config": {"Labels": {}}}}
        self.removed = []
        self.calls = []
        self.daemon = "fixture-daemon"
        self.failure = None
        self.logs = []
        self.log_failure = False

    def __call__(self, arguments):
        self.calls.append(arguments)
        if arguments[0] != "docker":
            return "1.18.2"
        args = arguments[3:]
        if args[0] == "info":
            return self.daemon
        if args[0] == "ps":
            return "\n".join(self.rows)
        if args[:2] == ["image", "inspect"]:
            return json.dumps([{"Id": "runtime-content-id"}])
        if args[0] == "pull":
            return ""
        if args[0] == "logs":
            self.logs.append(args[-1])
            if self.log_failure:
                raise TimeoutError("native log collection failed")
            return "native fixture startup log"
        if args[0] == "inspect":
            return json.dumps([self.rows[key] for key in args[1:]])
        if args[0] == "cp":
            Path(args[-1]).write_bytes(b"fixture-binary")
            return ""
        if args[0] in ("run", "create"):
            identity = f"{len(self.rows) + 1:064x}"
            name = args[args.index("--name") + 1]
            label, value = args[args.index("--label") + 1].split("=", 1)
            ports = {}
            if "--publish" in args:
                binding = args[args.index("--publish") + 1].split(":")
                ports[binding[-1] + "/tcp"] = [{"HostIp": binding[0], "HostPort": binding[1] or "45123"}]
            self.rows[identity] = {"Id": identity, "Name": "/" + name, "Image": "private-1.18.2",
                                   "HostConfig": {"PortBindings": ports}, "Mounts": [], "Config": {"Labels": {label: value}},
                                   "NetworkSettings": {"Networks": {}, "Ports": ports}}
            Path(args[args.index("--cidfile") + 1]).write_text(identity)
            if self.failure and name.endswith(self.failure):
                raise RuntimeError("Fixture startup failed after resource creation")
            return identity
        if args[:2] == ["rm", "-f"]:
            self.removed.append(args[2])
            del self.rows[args[2]]
            return ""
        raise AssertionError(args)


class PrivateRuntimeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.docker = FakeDocker()
        self.fixture = runtime.LegacySidecarRuntime(Path(self.temporary.name) / "owned", execute=self.docker)
        self.environment = mock.patch.dict("os.environ", {"DOCKER_HOST": "unix:///fixture/docker.sock", "DOCKER_CONTEXT": ""})
        self.environment.start()
        self.addCleanup(self.environment.stop)

    def start(self):
        with mock.patch.object(runtime.socket, "create_connection"):
            return self.fixture.start()

    def test_private_1182_names_ports_and_cleanup_preserve_shared_1191(self):
        selected = self.start()
        self.assertTrue(selected["HEXALITH_OQ8_PLACEMENT_CONTAINER"].startswith("g6-oq8-"))
        self.assertNotEqual("dapr_placement", selected["HEXALITH_OQ8_PLACEMENT_CONTAINER"])
        self.assertNotEqual("6379", selected["EVENTSTORE_TEST_REDIS_PORT"])
        self.assertEqual(64, len(selected["EVENTSTORE_TEST_PLACEMENT_CONTAINER"]))
        self.fixture.close()
        self.assertEqual(["a" * 64], list(self.docker.rows))
        self.assertEqual("shared-1.19.1", self.docker.rows["a" * 64]["Image"])
        self.assertEqual(4, len(self.docker.removed))
        self.assertNotIn("a" * 64, self.docker.removed)

    def test_partial_startup_reconciles_created_ids_and_preserves_shared_resources(self):
        self.docker.failure = "placement"
        with self.assertRaisesRegex(RuntimeError, "after resource creation"):
            self.start()
        self.fixture.close()
        self.assertEqual(["a" * 64], list(self.docker.rows))
        self.assertEqual(3, len(self.docker.removed))

    def test_cleanup_refuses_replaced_ownership_but_continues_other_owned_ids(self):
        self.start()
        changed = self.fixture.ids[-1]
        self.docker.rows[changed]["Config"]["Labels"] = {}
        with self.assertRaisesRegex(ValueError, "Unrelated container identity"):
            self.fixture.close()
        self.assertNotIn(changed, self.docker.removed)
        self.assertEqual(3, len(self.docker.removed))
        self.assertIn("a" * 64, self.docker.rows)

    def test_daemon_replacement_refuses_cleanup_mutations(self):
        self.start()
        self.docker.daemon = "replacement-daemon"
        with self.assertRaisesRegex(ValueError, "daemon identity changed"):
            self.fixture.close()
        self.assertEqual([], self.docker.removed)

    def test_actual_context_normal_startup_and_body_exits_restore_handlers_and_owned_cleanup(self):
        for failure in (None, "startup", "body"):
            with self.subTest(failure=failure):
                docker = FakeDocker()
                docker.failure = "placement" if failure == "startup" else None
                docker.log_failure = failure == "body"
                fixture = runtime.LegacySidecarRuntime(Path(self.temporary.name) / str(failure), execute=docker)
                handlers = {kind: signal.getsignal(kind) for kind in (signal.SIGTERM, signal.SIGINT)}
                def enter():
                    with runtime.legacy_sidecar_runtime(fixture.directory) as selected:
                        self.assertEqual(64, len(selected["EVENTSTORE_TEST_REDIS_CONTAINER"]))
                        for kind, handler in handlers.items():
                            self.assertIsNot(handler, signal.getsignal(kind))
                        if failure == "body":
                            raise RuntimeError("body failure")
                with mock.patch.object(runtime, "LegacySidecarRuntime", return_value=fixture), \
                        mock.patch.object(runtime.socket, "create_connection"), \
                        mock.patch.object(runtime.sys, "stderr"):
                    if failure:
                        with self.assertRaises(RuntimeError):
                            enter()
                    else:
                        enter()
                self.assertEqual(handlers, {kind: signal.getsignal(kind) for kind in handlers})
                self.assertEqual(["a" * 64], list(docker.rows))
                self.assertEqual(3 if failure == "startup" else 4, len(docker.removed))
                self.assertNotIn("a" * 64, docker.removed)
                self.assertEqual(set(docker.removed) if failure else set(), set(docker.logs))
                self.assertNotIn("a" * 64, docker.logs)
                self.assertIn(runtime.RUNTIME_IMAGE, [args[-1] for args in docker.calls if args[3:4] == ["pull"]])
                self.assertEqual("daprio/dapr@sha256:9ec89d30076155d2376c06f98028b6920f31fac5df0eb40e0b8b94cc88dd5b59", runtime.RUNTIME_IMAGE)


class ManagedStageBoundaryTests(unittest.TestCase):
    def test_invalid_docker_selection_prevents_the_first_postgres_pull(self):
        inspection = mock.Mock(stdout=json.dumps([{"Endpoints": {"docker": {"Host": "tcp://remote.invalid:2375"}}}]))
        for context, host in (("", "tcp://remote.invalid:2375"), ("remote", "unix:///local.sock")):
            with self.subTest(context=context), mock.patch.dict(os.environ, {"DOCKER_CONTEXT": context, "DOCKER_HOST": host}), \
                    mock.patch.object(runtime.subprocess, "run", return_value=inspection), \
                    mock.patch.object(product, "run") as mutate:
                with self.assertRaises(ValueError):
                    product.integration(Path("unused"))
                mutate.assert_not_called()

    def test_valid_selection_is_sanitized_before_the_first_child(self):
        class Done(Exception):
            pass
        def first(*arguments, **kwargs):
            self.assertEqual(("docker", "pull", product.POSTGRES), arguments)
            self.assertEqual("unix:///local.sock", os.environ["DOCKER_HOST"])
            self.assertFalse(any(name in os.environ for name in ("DOCKER_CONTEXT", "DOCKER_TLS_VERIFY", "DOCKER_CERT_PATH")))
            raise Done()
        with mock.patch.dict(os.environ, {"DOCKER_CONTEXT": "", "DOCKER_HOST": "unix:///local.sock",
                                          "DOCKER_TLS_VERIFY": "1", "DOCKER_CERT_PATH": "unused"}), \
                mock.patch.object(product, "run", side_effect=first):
            with self.assertRaises(Done):
                product.integration(Path("unused"))

    def test_standalone_process_proof_keeps_the_packer_build_default(self):
        standalone = load("verify_process", "verify-process-packages.py")
        with mock.patch.object(standalone.subprocess, "run") as invoke:
            self.assertEqual(0, standalone.main())
        command = invoke.call_args_list[0].args[0]
        self.assertEqual("scripts/pack-release-packages.py", command[1])
        self.assertNotIn("--no-build", command)

    def test_folded_standalone_builds_and_managed_caller_explicitly_reuses_outputs(self):
        with tempfile.TemporaryDirectory() as folder:
            scratch = Path(folder)
            tool = scratch / "dotnet"
            tool.write_text(f"#!{sys.executable}\nimport json,os,sys\n"
                            "with open(os.environ['CAPTURE_FOLDED_COMMANDS'],'a') as output: output.write(json.dumps(sys.argv[1:])+'\\n')\n"
                            "raise SystemExit(0 if sys.argv[1]=='pack' else 19)\n")
            tool.chmod(0o755)
            for reuse in (False, True):
                log = scratch / (str(reuse) + ".jsonl")
                environment = dict(os.environ, PATH=str(scratch) + os.pathsep + os.environ['PATH'],
                                   CAPTURE_FOLDED_COMMANDS=str(log))
                environment.pop('FOLDED_SNAPSHOT_REUSE_OUTPUTS', None)
                if reuse:
                    environment['FOLDED_SNAPSHOT_REUSE_OUTPUTS'] = 'true'
                result = subprocess.run(['bash', str(ROOT / 'tools/folded-snapshot-compatibility/run.sh')],
                                        env=environment, capture_output=True, text=True, timeout=30)
                self.assertEqual(19, result.returncode, result.stdout + result.stderr)
                commands = [json.loads(line) for line in log.read_text().splitlines()]
                packs = [arguments for arguments in commands if arguments[0] == 'pack']
                self.assertEqual(5, len(packs))
                self.assertTrue(all(('--no-build' in arguments) == reuse for arguments in packs))
                self.assertTrue(all(('--no-restore' in arguments) == reuse for arguments in packs))
        with mock.patch.object(product, 'test'), mock.patch.object(product, 'run') as invoke:
            product.offline(Path('unused'))
        folded = [call for call in invoke.call_args_list if call.args[:2] == ('bash', 'tools/folded-snapshot-compatibility/run.sh')]
        self.assertEqual(1, len(folded))
        self.assertEqual('true', folded[0].kwargs['environment']['FOLDED_SNAPSHOT_REUSE_OUTPUTS'])


class AzureRequestTests(unittest.TestCase):
    def environment(self, **changes):
        # Exact PascalCase schema from AzureLiveAuthority/AzureCustodyReceipt.
        # These are offline test bytes; every service command is mocked below.
        now = datetime.now(timezone.utc)
        binding = {"SourceSha": "b" * 40, "ResourceIdSha256": "1" * 64,
                   "IdentityIdSha256": "2" * 64, "NetworkIdSha256": "3" * 64, "Region": "westeurope"}
        custody = json.dumps({**binding, "ResourceType": "Microsoft.KeyVault/vaults", "Sku": "Premium",
                              "ProviderGenerated": True, "NonExportable": True, "KeyType": "RSA-HSM", "KeySize": 3072,
                              "PurgeProtection": True, "RetentionDays": 90, "PrivateEndpoint": True, "PrivateDnsVerified": True,
                              "PublicNetworkAccess": "Disabled", "TrustedServiceBypass": False,
                              "RuntimeRole": "Key Vault Crypto Service Encryption User", "KeyScopedRuntimeRole": True,
                              "SeparateProvisioningIdentity": True, "DiagnosticSinkSha256": "4" * 64,
                              "ObservedAtUtc": now.isoformat()}).encode()
        vault = "https://offline-fixture.vault.azure.net"
        document = {**binding, "Owner": "offline-test", "ExpiresAtUtc": (now + timedelta(hours=1)).isoformat(),
                    "VaultUri": vault, "KeyName": "offline-fixture",
                    "ManagedIdentityClientId": "11111111-1111-1111-1111-111111111111",
                    "DeniedManagedIdentityClientId": "22222222-2222-2222-2222-222222222222",
                    **{name: f"{vault}/keys/offline-fixture/{index:032x}" for index, name in enumerate(
                        ("InitialVersion", "RotationVersion", "DisabledVersion", "ExpiredVersion", "MissingVersion"), 1)},
                    "ExceptionDigest": "fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394",
                    "CorrectionDigest": "360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e",
                    "DiagnosticPolicyDigest": "a0a746aeab4de17c1b1b95cfa37fbf42a119e29af0c7354fe174d09512b389d9",
                    "Operations": ["get-key", "wrapkey", "unwrapkey", "isolated-dapr-persistence", "engine-rewrap",
                                   "read-only-rollback", "cleanup-isolated-dapr", "retain-all-azure-keys"],
                    "Faults": ["401", "403", "404", "disabled", "expired", "429", "timeout", "dns", "5xx",
                               "cancellation", "breaker", "lost-wrap-response"],
                    "FaultWindows": [{"Fault": fault, "NotBeforeUtc": (now + timedelta(minutes=10 + index * 4)).isoformat(),
                                      "NotAfterUtc": (now + timedelta(minutes=12 + index * 4)).isoformat()}
                                     for index, fault in enumerate(("429", "5xx", "timeout", "dns"))],
                    "CustodyReceiptSha256": hashlib.sha256(custody).hexdigest(), **changes}
        authority = json.dumps(document)
        return {"PAYLOAD_AZURE_AUTHORITY_JSON": authority, "PAYLOAD_AZURE_CUSTODY_JSON": custody.decode(),
                "QUALIFICATION_SOURCE_SHA": "b" * 40, "QUALIFICATION_AUTHORITY_SHA256": hashlib.sha256(authority.encode()).hexdigest()}

    def test_valid_pascalcase_request_reaches_only_mocked_services_and_cleans_private_files(self):
        environment = self.environment()
        real_read, real_hash = Path.read_bytes, hashlib.sha256
        retained = []
        def read(path):
            return b"offline-runtime" if path.parts[-3:] == (".dapr", "bin", "daprd") else real_read(path)
        def digest(value):
            if value == b"offline-runtime":
                return mock.Mock(hexdigest=lambda: "e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488")
            return real_hash(value)
        def service(command, *, env, **_):
            policy, custody = (Path(env[name]) for name in ("HEXALITH_PAYLOAD_AZURE_AUTHORITY", "HEXALITH_PAYLOAD_AZURE_CUSTODY_RECEIPT"))
            self.assertEqual(environment["PAYLOAD_AZURE_AUTHORITY_JSON"].encode(), policy.read_bytes())
            self.assertEqual(environment["PAYLOAD_AZURE_CUSTODY_JSON"].encode(), custody.read_bytes())
            self.assertEqual(0o600, policy.stat().st_mode & 0o777)
            self.assertEqual(0o600, custody.stat().st_mode & 0o777)
            self.assertNotIn("PAYLOAD_AZURE_AUTHORITY_JSON", env)
            self.assertNotIn("PAYLOAD_AZURE_CUSTODY_JSON", env)
            retained.append((command, env.get("HEXALITH_PAYLOAD_AZURE_PHASE"), policy, custody))
        with mock.patch.dict(os.environ, environment), mock.patch.object(Path, "read_bytes", read), \
                mock.patch.object(azure.hashlib, "sha256", side_effect=digest), \
                mock.patch.object(azure.subprocess, "check_output", side_effect=["b" * 40, "1.18.7"]), \
                mock.patch.object(azure.subprocess, "run", side_effect=service):
            self.assertEqual(0, azure.main())
        self.assertEqual(3, len(retained))
        self.assertEqual(["healthy", "prepared-faults"], [row[1] for row in retained[1:]])
        self.assertTrue(all("--no-build" in row[0] for row in retained[1:]))
        self.assertTrue(all(not path.exists() for row in retained for path in row[2:]))

    def test_camelcase_aliases_are_incompatible_with_the_unchanged_closed_csharp_schema(self):
        environment = self.environment()
        document = json.loads(environment["PAYLOAD_AZURE_AUTHORITY_JSON"])
        document["sourceSha"] = document.pop("SourceSha")
        value = json.dumps(document)
        environment.update(PAYLOAD_AZURE_AUTHORITY_JSON=value, QUALIFICATION_AUTHORITY_SHA256=hashlib.sha256(value.encode()).hexdigest())
        with self.assertRaises(ValueError):
            azure.validate_request(environment, "b" * 40)

    def test_missing_authority_never_starts_service_commands(self):
        with mock.patch.dict("os.environ", {}, clear=True), mock.patch.object(azure.subprocess, "check_output", return_value="b" * 40), \
                mock.patch.object(azure.subprocess, "run") as service:
            with self.assertRaisesRegex(ValueError, "separately supplied"):
                azure.main()
            service.assert_not_called()

    def test_expiry_source_and_custody_fail_before_any_service_command(self):
        for changed in ({"SourceSha": "c" * 40}, {"ExpiresAtUtc": (datetime.now(timezone.utc) - timedelta(seconds=1)).isoformat()},
                        {"ExpiresAtUtc": (datetime.now(timezone.utc) + timedelta(days=2)).isoformat()},
                        {"CustodyReceiptSha256": "d" * 64}):
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                azure.validate_request(self.environment(**changed), "b" * 40)

    def test_wrong_digest_and_duplicate_fields_fail_closed(self):
        environment = self.environment()
        environment["QUALIFICATION_AUTHORITY_SHA256"] = "0" * 64
        with self.assertRaises(ValueError):
            azure.validate_request(environment, "b" * 40)
        environment = self.environment()
        value = '{"Owner":"duplicate",' + environment["PAYLOAD_AZURE_AUTHORITY_JSON"][1:]
        environment.update(PAYLOAD_AZURE_AUTHORITY_JSON=value, QUALIFICATION_AUTHORITY_SHA256=hashlib.sha256(value.encode()).hexdigest())
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            azure.validate_request(environment, "b" * 40)


if __name__ == "__main__":
    unittest.main()
