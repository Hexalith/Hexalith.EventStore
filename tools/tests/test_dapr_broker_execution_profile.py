"""Offline controls for the Dapr 1.19.2 Platform profile."""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

import test_dapr_platform_broker_configuration as configuration
from test_dapr_platform_broker_configuration import ROOT, producer
from dapr_broker_qualification import BROKERS, SCENARIOS, digest, validate_packet, write_json
from dapr_runtime_qualification import CAPTURE_BLOCKED, CONFIG_1192, INTAKE_1192, PLATFORM_1192_PROFILE


class ExecutionProfileTests(unittest.TestCase):
    def setUp(self):
        self.scratch = Path(self.enterContext(tempfile.TemporaryDirectory()))
        self.profile = ROOT / PLATFORM_1192_PROFILE
        self.config = ROOT / CONFIG_1192
        self.output = self.scratch / "packet"

    def cli(self, tool, *args):
        return subprocess.run([sys.executable, "-B", str(ROOT / "tools" / tool), *map(str, args)],
                              text=True, capture_output=True, check=False)

    def testExplicitOfflineEntryPointsAndBlockedLiveCapture(self):
        installer = self.cli("install-dapr-qualification-tools.py", "--profile", self.profile,
                             "--validate-only", "--destination", self.scratch / "install")
        self.assertEqual(installer.returncode, 0, installer.stderr)
        installed = json.loads(installer.stdout)
        self.assertEqual(installed["profileSha256"], digest(self.profile))
        self.assertIn("/v1.19.2/", installed["archives"]["daprd"]["url"])
        self.assertIn("/v1.19.1/", installed["archives"]["dapr"]["url"])
        self.assertFalse((self.scratch / "install").exists())

        args = ["--profile", self.profile, "--platform-config", self.config, "--output", self.output]
        plan = self.cli("qualify-dapr-brokers.py", *args, "--validate-only")
        self.assertEqual(plan.returncode, 0, plan.stdout + plan.stderr)
        receipt = json.loads(plan.stdout)
        self.assertEqual(receipt["profileSha256"], digest(self.profile))
        self.assertEqual(receipt["configuration"]["sha256"], digest(self.config))
        self.assertEqual(receipt["vectors"], 24)
        self.assertEqual(receipt["captureAdmission"], "blocked")
        self.assertEqual(receipt["componentSha256"], {
            "rabbitmq": digest(ROOT / "deploy/dapr/qualification/rabbitmq.yaml"),
            "kafka": "e8e494157c2f9eaa84ca3bc383872bf1354fde9a7d301901a3069e40466565c6"})
        refused = self.cli("qualify-dapr-brokers.py", *args, "--exclusive-worker", self.scratch / "worker")
        self.assertEqual(refused.returncode, 1)
        self.assertIn(CAPTURE_BLOCKED, refused.stdout)
        self.assertFalse(self.output.exists())

    def testInstallerUsesLockedReleaseURLAndRejectsWrongArchive(self):
        name = "install-dapr-qualification-tools.py"
        spec = importlib.util.spec_from_file_location("qualification_installer", ROOT / "tools" / name)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        destination = self.scratch / "install"
        argv = [name, "--profile", str(self.profile), "--destination", str(destination)]
        with mock.patch.object(sys, "argv", argv), mock.patch.object(
                module.urllib.request, "urlopen", return_value=io.BytesIO(b"wrong archive")) as download:
            with self.assertRaisesRegex(ValueError, "official archive checksum mismatch"):
                module.main()
        runtime = json.loads(self.profile.read_bytes())["runtime"]
        download.assert_called_once_with(runtime["archives"]["dapr"]["url"], timeout=120)
        self.assertFalse(destination.exists())

    def testInstallerVerifiesBothArchivesAndExtractedExecutables(self):
        name = "install-dapr-qualification-tools.py"
        spec = importlib.util.spec_from_file_location("qualification_installer", ROOT / "tools" / name)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        data, profile = module.load_profile(self.profile, ROOT)
        runtime = profile["runtime"]
        archives = {}
        for binary in ("dapr", "daprd"):
            content = ("offline archive fixture for " + binary).encode()
            package = io.BytesIO()
            with tarfile.open(fileobj=package, mode="w:gz") as writer:
                member = tarfile.TarInfo(binary)
                member.size = len(content)
                writer.addfile(member, io.BytesIO(content))
            identity = runtime["archives"][binary]
            identity["sha256"] = hashlib.sha256(package.getvalue()).hexdigest()
            archives[identity["url"]] = (package.getvalue(), content)
        runtime["cliSha256"] = hashlib.sha256(archives[runtime["archives"]["dapr"]["url"]][1]).hexdigest()

        destination = self.scratch / "install"
        args = [name, "--profile", str(self.profile), "--destination", str(destination)]
        def download(url, **kwargs):
            return io.BytesIO(archives[url][0])
        with mock.patch.object(sys, "argv", args), mock.patch.object(module, "load_profile", return_value=(data, profile)), \
                mock.patch.object(module.urllib.request, "urlopen", side_effect=download) as fetched, \
                mock.patch.dict("os.environ", {"GITHUB_PATH": "", "GITHUB_ENV": ""}):
            with self.assertRaisesRegex(ValueError, "daprd: immutable executable checksum mismatch"):
                module.main()
            self.assertFalse(destination.exists())
            runtime["binarySha256"] = hashlib.sha256(archives[runtime["archives"]["daprd"]["url"]][1]).hexdigest()
            self.assertEqual(module.main(), 0)
        urls = [call.args[0] for call in fetched.call_args_list]
        self.assertEqual(urls, [runtime["archives"][binary]["url"] for binary in ("dapr", "daprd") * 2])
        for binary, folder in (("dapr", "cli"), ("daprd", "runtime")):
            self.assertEqual((destination / folder / binary).read_bytes(), archives[runtime["archives"][binary]["url"]][1])

    def testUnversionedIntakeAlteredProfileAndConfigurationAreRefused(self):
        altered_config = self.scratch / "altered-config.json"
        altered_profile = self.scratch / "altered-profile.json"
        altered_config.write_bytes(self.config.read_bytes() + b"\n")
        altered_profile.write_bytes(self.profile.read_bytes() + b"\n")
        for profile, config, diagnostic in (
            (ROOT / INTAKE_1192, self.config, "changed candidates require requalification"),
            (altered_profile, self.config, "exact versioned lock bytes differ"),
            (self.profile, altered_config, "bytes differ from the versioned profile binding"),
            (self.profile, None, "required together"),
        ):
            with self.subTest(profile=profile, config=config):
                args = ["--profile", profile, "--validate-only"]
                if config:
                    args += ["--platform-config", config]
                attempt = self.cli("qualify-dapr-brokers.py", *args)
                self.assertEqual(attempt.returncode, 1)
                self.assertIn(diagnostic, attempt.stdout)

    def testSyntheticSchema3UsesActualRenderAndVerifierWithAllBlockedRows(self):
        args = SimpleNamespace(profile=self.profile, platform_config=self.config, output=self.output)
        runner = producer().Run
        run = runner.__new__(runner)
        run._initialize_packet(args, runner.input_selection(args))
        self.addCleanup(shutil.rmtree, run.private_parent, ignore_errors=True)
        with self.assertRaisesRegex(ValueError, "capture admission is blocked"):
            run.prepare()
        with mock.patch("signal.signal"), contextlib.redirect_stdout(io.StringIO()):
            run.bao_url = "https://127.0.0.1:1234"
            run.broker_port = 12345
            for broker in BROKERS:
                run.broker = broker
                run.components("qualification-publisher")
                for scenario in SCENARIOS:
                    run.record(broker, scenario, {"blockedBy": "offline test fixture"})
            run.finish()
        self.assertFalse(run.private_parent.exists())
        args = [self.output, "--platform-config", self.config]
        integrity = self.cli("validate-dapr-broker-qualification.py", *args, "--allow-failures")
        self.assertEqual(integrity.returncode, 0, integrity.stdout + integrity.stderr)
        packet = json.loads(integrity.stdout)
        self.assertEqual(packet["integrity"], "PASS")
        self.assertEqual(packet["technicalQualification"], "FAIL")
        self.assertEqual(len(packet["failedScenarios"]), 24)
        for flag, refusal in (([], "technical qualification FAIL"), (["--require-production-authority"], "production eligibility FAIL")):
            actual = self.cli("validate-dapr-broker-qualification.py", *args, *flag)
            self.assertEqual(actual.returncode, 1)
            self.assertIn(refusal, actual.stdout)
            self.assertEqual(actual.stderr, "")

        packet_file = self.output / "packet.json"
        packet = json.loads(packet_file.read_bytes())
        packet["schemaVersion"] = 2
        write_json(packet_file, packet)
        with self.assertRaisesRegex(ValueError, "requires its explicit profile and schema 3"):
            validate_packet(self.output, allow_failures=True, platform_config=self.config)
        packet["schemaVersion"] = 3
        packet["brokers"]["unexpected-broker"] = packet["brokers"]["kafka"]
        write_json(packet_file, packet)
        with self.assertRaisesRegex(ValueError, "unexpected or missing broker"):
            validate_packet(self.output, allow_failures=True, platform_config=self.config)
        del packet["brokers"]["unexpected-broker"]
        cleanup_file = self.output / "cleanup.json"
        cleanup = json.loads(cleanup_file.read_bytes())
        cleanup["privateMaterialRemoved"] = False
        write_json(cleanup_file, cleanup)
        packet["artifacts"]["cleanup.json"] = digest(cleanup_file)
        write_json(packet_file, packet)
        with self.assertRaisesRegex(ValueError, "private material removal is incomplete"):
            validate_packet(self.output, allow_failures=True, platform_config=self.config)
        cleanup.update(privateMaterialRemoved=True, errors=["owned cleanup failed"])
        write_json(cleanup_file, cleanup)
        packet["artifacts"]["cleanup.json"] = digest(cleanup_file)
        write_json(packet_file, packet)
        with self.assertRaisesRegex(ValueError, "empty error inventory"):
            validate_packet(self.output, allow_failures=True, platform_config=self.config)

    def testVersionedVerifierExercisesExecutedReceiptChecks(self):
        control = configuration.PlatformPacketTests()
        self.addCleanup(control.doCleanups)
        control.prepare_packet(PLATFORM_1192_PROFILE, self.config)
        self.assertEqual(control.verify()["technicalQualification"], "PASS")
        packet = control.f
        identities_path = packet.directory / "execution-identities.json"
        identities = json.loads(identities_path.read_bytes())
        identities["controlPlane"]["placement"]["binarySha256"] = "0" * 64
        write_json(identities_path, identities)
        packet.seal()
        with self.assertRaisesRegex(ValueError, "official executable identity mismatch"):
            control.verify()
