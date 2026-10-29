"""Execute the actual catalog CI Docker-selection prefix against owned fake tooling."""

import ast
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest import mock


class RouteCatalogCiToolsTests(unittest.TestCase):
    def run_preparation(self, context, host, endpoint):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/integration.yml").read_text()
        block = workflow.split("- name: Prepare pinned route catalog Development tools", 1)[1]
        source = block.split("python3 - <<'PY'\n", 1)[1].split("\n          PY", 1)[0]
        source = "\n".join(line[10:] for line in source.splitlines())
        parsed = ast.parse(source)
        prefix = []
        for statement in parsed.body:
            if isinstance(statement, ast.Assign) and any(isinstance(target, ast.Name) and target.id == "version" for target in statement.targets):
                break
            prefix.append(statement)
        program = compile(ast.Module(body=prefix, type_ignores=[]), str(root / ".github/workflows/integration.yml"), "exec")
        with tempfile.TemporaryDirectory(prefix="catalog-ci-docker-") as scratch:
            scratch_path = Path(scratch)
            tool = scratch_path / "docker"
            calls = scratch_path / "calls.jsonl"
            tool.write_text("#!/usr/bin/env python3\nimport json,os,pathlib,sys\n"
                + f"log=pathlib.Path({str(calls)!r})\n"
                + "with log.open('a') as output: output.write(json.dumps({'args':sys.argv[1:],'context':os.environ.get('DOCKER_CONTEXT'),'host':os.environ.get('DOCKER_HOST')})+'\\n')\n"
                + f"if sys.argv[1]=='context': print(json.dumps([{{'Endpoints':{{'docker':{{'Host':{endpoint!r}}}}}}}]))\n")
            tool.chmod(0o700)
            environment = {"PATH": str(scratch_path) + os.pathsep + os.environ["PATH"]}
            if context is not None:
                environment["DOCKER_CONTEXT"] = context
            if host is not None:
                environment["DOCKER_HOST"] = host
            failure = None
            # Environment changes are confined to this test process and restored immediately;
            # the fake executable never contacts a Docker daemon or changes global contexts.
            with mock.patch.dict(os.environ, environment):
                for name, selected in (("DOCKER_CONTEXT", context), ("DOCKER_HOST", host)):
                    if selected is None:
                        os.environ.pop(name, None)
                try:
                    exec(program, {})
                except SystemExit as exception:
                    failure = str(exception)
            recorded = [json.loads(line) for line in calls.read_text().splitlines()] if calls.exists() else []
            return failure, recorded

    def test_remote_context_overrides_local_host_and_refuses_before_pull(self):
        failure, calls = self.run_preparation("remote", "unix:///fixture/local.sock", "tcp://remote.invalid:2375")
        self.assertIn("Conflicting", failure)
        self.assertEqual([call["args"] for call in calls], [["context", "inspect", "remote"]])

    def test_conflicting_local_context_and_remote_host_refuse_before_pull(self):
        failure, calls = self.run_preparation("local", "tcp://remote.invalid:2375", "unix:///fixture/local.sock")
        self.assertIn("Conflicting", failure)
        self.assertEqual(len(calls), 1)

    def test_remote_host_without_context_refuses_without_docker(self):
        failure, calls = self.run_preparation(None, "tcp://remote.invalid:2375", "unix:///fixture/local.sock")
        self.assertIn("local Unix", failure)
        self.assertEqual(calls, [])

    def test_local_context_pins_both_owned_image_pulls(self):
        failure, calls = self.run_preparation("local", None, "unix:///fixture/local.sock")
        self.assertIsNone(failure)
        self.assertEqual(calls[0]["args"], ["context", "inspect", "local"])
        self.assertEqual(len(calls), 3)
        for call in calls[1:]:
            self.assertEqual(call["args"][:3], ["--host", "unix:///fixture/local.sock", "pull"])
            self.assertIsNone(call["context"])
            self.assertEqual(call["host"], "unix:///fixture/local.sock")


if __name__ == "__main__":
    unittest.main()
