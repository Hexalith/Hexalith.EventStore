"""Keep isolated package probes independent of their mapped producer environment."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "package_consumer_validation", ROOT / "scripts/validate-consumer-package-references.py"
)
CONSUMERS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CONSUMERS)


class PackageConsumerEnvironmentTests(unittest.TestCase):
    def test_real_child_drops_producer_mapping_and_preserves_parent_and_package_cache(self):
        with tempfile.TemporaryDirectory(prefix="consumer-environment-") as scratch:
            directory = Path(scratch)
            output = directory / "child.json"
            # These are the actual pinned Builds exports, plus the existing path handoff.
            inherited = {name: str(directory / "producer.targets") for name in (
                "AfterMicrosoftNETSdkTargets", "CustomAfterDirectoryBuildTargets",
                "CustomAfterMicrosoftCommonProps", "CustomBeforeDirectoryBuildProps",
                "CustomBeforeDirectoryBuildTargets",
            )}
            inherited.update(HEXALITH_SOURCE_MAPPING_HASH="producer-mapping-hash",
                             HEXALITH_SOURCE_MAPPING_PATH=str(directory / "mapping.json"),
                             Configuration="Release", CONSUMER_UNRELATED="preserved")
            with patch.dict(os.environ, inherited):
                before = dict(os.environ)
                retained_keys = [*inherited, "NUGET_PACKAGES"]
                CONSUMERS.run([
                    sys.executable, "-c",
                    "import json,os,sys;keys=json.loads(sys.argv[2]);"
                    "open(sys.argv[1],'w').write(json.dumps({k:os.environ[k] for k in keys if k in os.environ}))",
                    str(output), json.dumps(retained_keys),
                ], cwd=directory)
                self.assertEqual(dict(os.environ), before)
            child = json.loads(output.read_text())
            mapping_keys = set(inherited) - {"Configuration", "CONSUMER_UNRELATED"}
            self.assertTrue(mapping_keys.isdisjoint(child))
            self.assertEqual(child["Configuration"], "Release")
            self.assertEqual(child["CONSUMER_UNRELATED"], "preserved")
            if "NUGET_PACKAGES" in before:
                self.assertEqual(child["NUGET_PACKAGES"], before["NUGET_PACKAGES"])

    def test_tool_cache_override_is_child_only_and_cannot_reintroduce_mapping(self):
        with tempfile.TemporaryDirectory(prefix="tool-consumer-environment-") as scratch:
            cache = Path(scratch) / "packages"
            with patch.dict(os.environ, {"CustomBeforeDirectoryBuildProps": "producer.targets",
                                         "HEXALITH_SOURCE_MAPPING_HASH": "producer-hash"}):
                before = dict(os.environ)
                with patch.object(CONSUMERS.subprocess, "run") as child:
                    child.return_value = subprocess.CompletedProcess(["dotnet", "tool", "install"], 0)
                    CONSUMERS.run(["dotnet", "tool", "install"], packages_folder=cache)
                self.assertEqual(dict(os.environ), before)
            environment = child.call_args.kwargs["env"]
            self.assertEqual(environment["NUGET_PACKAGES"], str(cache))
            self.assertNotIn("CustomBeforeDirectoryBuildProps", environment)
            self.assertNotIn("HEXALITH_SOURCE_MAPPING_HASH", environment)

    def test_child_failure_keeps_original_exit_code(self):
        with patch.object(CONSUMERS.subprocess, "run") as child:
            child.return_value = subprocess.CompletedProcess(["dotnet", "restore"], 37)
            with self.assertRaises(subprocess.CalledProcessError) as failure:
                CONSUMERS.run(["dotnet", "restore"])
        self.assertEqual(failure.exception.returncode, 37)


if __name__ == "__main__":
    unittest.main()
