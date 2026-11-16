"""Execute the actual catalog CI Docker-selection prefix against owned fake tooling."""

import hashlib
import io
import json
import os
from pathlib import Path
import tempfile
import subprocess
import sys
import tarfile
import unittest
from unittest import mock


class RouteCatalogCiToolsTests(unittest.TestCase):
    def preparation_source(self):
        root = Path(__file__).resolve().parents[2]
        return root, (root / "tools/prepare-route-catalog-tools.py").read_text()

    def run_preparation(self, context, host, endpoint):
        root, source = self.preparation_source()
        namespace = {"__file__": str(root / "tools/prepare-route-catalog-tools.py"), "__name__": "catalog_fixture"}
        exec(compile(source, namespace["__file__"], "exec"), namespace)
        class DownloadsBegin(Exception):
            pass
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
                    with mock.patch('urllib.request.urlopen', side_effect=DownloadsBegin):
                        namespace['main']()
                except DownloadsBegin:
                    pass
                except ValueError as exception:
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

    def test_local_context_pins_all_locked_control_image_pulls(self):
        failure, calls = self.run_preparation("local", None, "unix:///fixture/local.sock")
        self.assertIsNone(failure)
        self.assertEqual(calls[0]["args"], ["context", "inspect", "local"])
        self.assertEqual(len(calls), 5)
        root, _ = self.preparation_source()
        profile = json.loads((root / 'deploy/dapr/qualification/runtime-lock.json').read_text())
        self.assertEqual([call['args'][-1] for call in calls[1:]],
                         [profile['images'][name]['reference'] for name in ('sentry', 'placement', 'scheduler')]
                         + ['redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf'])
        for call in calls[1:]:
            self.assertEqual(call["args"][:3], ["--host", "unix:///fixture/local.sock", "pull"])
            self.assertIsNone(call["context"])
            self.assertEqual(call["host"], "unix:///fixture/local.sock")

    def test_isolated_runtime_and_cli_are_checksum_verified_without_changing_ordinary_pins(self):
        self.run_tool_downloads(False)

    def test_corrupt_isolated_cli_is_not_selected(self):
        self.run_tool_downloads(True)

    def run_tool_downloads(self, corrupt_cli):
        root, source = self.preparation_source()
        lock_path = root / 'deploy/dapr/qualification/runtime-lock.json'
        profile = json.loads(lock_path.read_text())
        original_read = Path.read_text
        binaries = {'daprd': b'fixture-runtime', 'dapr': b'fixture-cli'}
        profile['runtime']['binarySha256'] = hashlib.sha256(binaries['daprd']).hexdigest()
        profile['runtime']['cliSha256'] = hashlib.sha256(binaries['dapr']).hexdigest()
        urls = []
        def download(url, timeout):
            urls.append(url)
            executable = 'daprd' if '/daprd_' in url else 'dapr'
            binary = b'corrupt-cli' if corrupt_cli and executable == 'dapr' else binaries[executable]
            archive = io.BytesIO()
            with tarfile.open(fileobj=archive, mode='w:gz') as tar:
                member = tarfile.TarInfo(executable)
                member.size = len(binary)
                tar.addfile(member, io.BytesIO(binary))
            return io.BytesIO(archive.getvalue())
        with tempfile.TemporaryDirectory(prefix='catalog-ci-tools-') as scratch:
            environment = {'TMPDIR': scratch, 'RUNNER_TEMP': str(Path(scratch) / 'long-lived'), 'GITHUB_ENV': str(Path(scratch) / 'selected.env'),
                           'DOCKER_CONTEXT': '', 'DOCKER_HOST': 'unix:///fixture/local.sock'}
            def read(path, *args, **kwargs):
                return json.dumps(profile) if str(path).endswith('deploy/dapr/qualification/runtime-lock.json') else original_read(path, *args, **kwargs)
            namespace = {'__file__': str(root / 'tools/prepare-route-catalog-tools.py'), '__name__': 'catalog_fixture'}
            exec(compile(source, namespace['__file__'], 'exec'), namespace)
            with mock.patch.dict(os.environ, environment), mock.patch.object(Path, 'read_text', read), \
                    mock.patch('urllib.request.urlopen', side_effect=download), mock.patch('subprocess.run'):
                if corrupt_cli:
                    with self.assertRaisesRegex(ValueError, 'checksum mismatch'):
                        namespace['main']()
                else:
                    namespace['main']()
            self.assertFalse((Path(scratch) / 'selected.env').exists())
            self.assertFalse((Path(scratch) / 'long-lived').exists())
            self.assertEqual((Path(scratch) / 'route-catalog-tools/dapr').exists(), not corrupt_cli)
            if not corrupt_cli:
                self.assertEqual((Path(scratch) / 'route-catalog-tools/dapr').read_bytes(), binaries['dapr'])
            self.assertEqual(urls, [
                'https://github.com/dapr/dapr/releases/download/v1.18.7/daprd_linux_amd64.tar.gz',
                'https://github.com/dapr/cli/releases/download/v1.18.2/dapr_linux_amd64.tar.gz'])

    def test_optimized_interpreter_rejects_corrupt_download_before_writing_binary(self):
        root, _ = self.preparation_source()
        profile = json.loads((root / 'deploy/dapr/qualification/runtime-lock.json').read_text())
        with tempfile.TemporaryDirectory(prefix='catalog-optimized-') as temporary:
            scratch = Path(temporary)
            lock = scratch / 'deploy/dapr/qualification/runtime-lock.json'
            lock.parent.mkdir(parents=True)
            lock.write_text(json.dumps(profile))
            archive = scratch / 'corrupt.tar.gz'
            with tarfile.open(archive, 'w:gz') as output:
                member = tarfile.TarInfo('daprd')
                member.size = len(b'corrupt')
                output.addfile(member, io.BytesIO(b'corrupt'))
            driver = scratch / 'optimized.py'
            driver.write_text("import pathlib,runpy,sys,urllib.request\nfrom unittest import mock\n"
                              "urllib.request.urlopen=lambda *args,**kwargs:pathlib.Path(sys.argv[2]).open('rb')\n"
                              "with mock.patch('subprocess.run'):\n runpy.run_path(sys.argv[1],run_name='__main__')\n")
            environment = dict(os.environ, TMPDIR=str(scratch), DOCKER_HOST='unix:///fixture/local.sock', DOCKER_CONTEXT='')
            result = subprocess.run([sys.executable, '-O', '-B', str(driver), str(root / 'tools/prepare-route-catalog-tools.py'), str(archive)],
                                    cwd=scratch, env=environment, capture_output=True, text=True, timeout=30)
            self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn('checksum mismatch', result.stderr)
            self.assertFalse((scratch / 'route-catalog-tools/daprd').exists())



if __name__ == "__main__":
    unittest.main()
