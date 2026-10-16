"""Execute the real fixture entry points and inspect their actual DI registrations.

The startup hook stops at HostBuilt: no synthetic readiness, database, sidecar or
registration response is substituted, and this test grants no qualification.
"""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / 'tools/p1r-published-consumers'


class FixtureSnapshotRegistrationTests(unittest.TestCase):
    def testRealHostAndDomainRegistrationAndMissingEnrollmentNegativeControls(self):
        with tempfile.TemporaryDirectory(prefix='p1r-registration-') as directory:
            artifacts = Path(directory)
            environment = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development',
                               DOTNET_ENVIRONMENT='Development', DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE='false')

            def build(project, output, definitions=None):
                command = ['dotnet', 'build', str(project), '--configuration', 'Debug', '-m:1',
                           '--artifacts-path', str(output), '-p:UseCurrentSource=true',
                           '-p:EventStoreSourceRoot=' + str(ROOT), '-p:NuGetAudit=false',
                           '-p:RestorePackagesWithLockFile=false']
                if definitions is not None:
                    command.append('-p:DefineConstants=' + definitions)
                result = subprocess.run(command, cwd=ROOT, env=environment, capture_output=True, text=True, timeout=600)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

            build(FIXTURES / 'registration-probe/RegistrationProbe.csproj', artifacts / 'hook')
            hook = artifacts / 'hook/bin/RegistrationProbe/debug/RegistrationProbe.dll'
            self.assertTrue(hook.is_file())
            for role, state in (('host', 'System.Object'), ('domain', 'P1R.Counter.CounterState')):
                for enrolled in (True, False):
                    with self.subTest(role=role, enrolled=enrolled):
                        output = artifacts / (role + ('-enrolled' if enrolled else '-unenrolled'))
                        project = role.title()
                        build(FIXTURES / role / (project + '.csproj'), output,
                              None if enrolled else 'P1R_SOURCE')
                        binary = output / 'bin' / project / 'debug' / (project + '.dll')
                        result = subprocess.run(['dotnet', str(binary)], cwd=FIXTURES / role,
                                                env=dict(environment, DOTNET_STARTUP_HOOKS=str(hook),
                                                         P1R_EXPECTED_STATE=state),
                                                capture_output=True, text=True, timeout=45)
                        if enrolled:
                            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                            record = next(line for line in result.stdout.splitlines() if line.startswith('P1R_REGISTRATION '))
                            observed = json.loads(record.removeprefix('P1R_REGISTRATION '))
                            self.assertEqual(observed['state_type'], state)
                            self.assertEqual(observed['profile_id'], 'p1r-counter-folded-v1')
                            self.assertEqual(observed['limits']['maximumEventCount'], 16)
                            self.assertEqual(observed['limits']['maximumCaptureWorkspaceBytes'], 512_000)
                        else:
                            self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
                            self.assertIn('Snapshot registry missing', result.stderr)


if __name__ == '__main__':
    unittest.main()
