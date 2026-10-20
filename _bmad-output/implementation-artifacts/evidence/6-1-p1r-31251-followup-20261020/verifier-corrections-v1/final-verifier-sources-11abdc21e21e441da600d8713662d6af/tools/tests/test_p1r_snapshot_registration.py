"""Execute the real fixture entry points and inspect their actual DI registrations.

The startup hook stops at HostBuilt: no synthetic readiness, database, sidecar or
registration response is substituted, and this test grants no qualification.
"""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / 'tools/p1r-published-consumers'
sys.path.insert(0,str(ROOT/'tools'))
import p1r_qualification as preparation


class FixtureSnapshotRegistrationTests(unittest.TestCase):
    def testSeparateRootHelperDrivesRealSourceReferencesCatalogAndVersionWithoutRestore(self):
        with tempfile.TemporaryDirectory(prefix='p1r-source-evaluation-') as temporary:
            root = Path(temporary)
            consumers,source,builds = root/'consumers/source',root/'release-source',root/'release-builds'
            shutil.copytree(FIXTURES,consumers,ignore=shutil.ignore_patterns('bin','obj','packages'))
            (builds/'Props').mkdir(parents=True)
            catalog = builds/'Props/Directory.Packages.props'
            catalog.write_text('<Project><PropertyGroup><HexalithVersionsLoaded>true</HexalithVersionsLoaded>'
                               '<HexalithEventStoreVersion>3.124.0</HexalithEventStoreVersion></PropertyGroup></Project>')
            source.mkdir()
            (source/'Directory.Packages.props').write_text('<Project><Import Project="$(Hexalith1BuildPackageProps)" /></Project>')
            (source/'Directory.Build.props').write_text('<Project><Import Project="$(HexalithBuildPackageVersions1)" /></Project>')
            projects = {source/'src'/('Hexalith.EventStore.'+name)/('Hexalith.EventStore.'+name+'.csproj')
                        for name in ('Client','Contracts','DomainService','Server','ServiceDefaults')}
            for project in projects:
                project.parent.mkdir(parents=True)
                project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                                   '<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            binding = {'runtime_source':{'repository':str(source),'builds':{'path':str(builds)}}}
            props = ['-p:'+name+'='+value for name,value in preparation.runtime_source_properties(binding).items()]
            for role in ('Host','Domain','Probe'):
                project = consumers/role.lower()/(role+'.csproj')
                argv = ['dotnet','msbuild',str(project),'-nologo',*props]
                measured = subprocess.run([*argv,'-getProperty:EventStoreVersion,HexalithEventStoreVersion,Version,DefineConstants',
                    '-getItem:ProjectReference','-getItem:PackageReference'],cwd=root,capture_output=True,text=True,timeout=30)
                self.assertEqual(measured.returncode,0,measured.stdout+measured.stderr)
                value = json.loads(measured.stdout)
                expected = {Path(row.attrib['Include'].replace('$(EventStoreSourceRoot)',str(source)))
                            for row in ET.parse(project).iter('ProjectReference')}
                self.assertTrue(expected <= projects)
                self.assertTrue(expected)
                self.assertEqual({Path(row['FullPath']) for row in value['Items']['ProjectReference']},expected)
                self.assertFalse(value['Items']['PackageReference'])
                self.assertIn('P1R_SOURCE',value['Properties']['DefineConstants'].split(';'))
                self.assertTrue(all(value['Properties'][name] == '3.125.1' for name in
                                    ('Version','EventStoreVersion','HexalithEventStoreVersion')))
                imported = subprocess.run([*argv,'-preprocess'],cwd=root,capture_output=True,text=True,timeout=30)
                self.assertEqual(imported.returncode,0,imported.stdout+imported.stderr)
                catalogs = {Path(name) for name in re.findall(r'(?m)^(/[^\r\n]+Directory\.Packages\.props)\r?$',imported.stdout)}
                self.assertEqual(catalogs,{catalog,source/'Directory.Packages.props',consumers/'Directory.Packages.props'})
            self.assertFalse(any(root.rglob('project.assets.json')))

    def testProposedAndUnenrolledPackageProjectEvaluations(self):
        expected_packages = {
            'Hexalith.EventStore.Client', 'Hexalith.EventStore.Contracts',
            'Hexalith.EventStore.DomainService', 'Hexalith.EventStore.Server',
            'Hexalith.EventStore.ServiceDefaults',
        }
        expected_sources = {
            'host': {'../domain/FixtureEvolutionManifest.cs', '../FixtureSnapshotQualification.cs'},
            'domain': {'FixtureEvolutionManifest.cs', 'CounterSnapshotParity.cs',
                       '../FixtureSnapshotQualification.cs'},
            'probe': set(),
        }
        all_fixture_sources = set().union(*expected_sources.values())
        for version, enrolled in (('3.125.0', True), ('3.125.1', True),
                                  ('3.125.2', False), ('3.126.0', False)):
            for role in ('host', 'domain', 'probe'):
                with self.subTest(version=version, role=role):
                    project = FIXTURES / role / (role.title() + '.csproj')
                    command = ['dotnet', 'msbuild', str(project),
                               '-p:UseCurrentSource=false', '-p:EventStoreVersion=' + version,
                               '-getProperty:DefineConstants', '-getItem:Compile',
                               '-getItem:Content',
                               '-getItem:PackageReference', '-getItem:ProjectReference']
                    result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, timeout=30)
                    self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                    evaluated = json.loads(result.stdout)
                    flags = set(evaluated['Properties']['DefineConstants'].split(';'))
                    self.assertEqual(flags & {'P1R_CAPABILITIES', 'P1R_CANDIDATE',
                                              'P1R_FOLDED_SNAPSHOT', 'P1R_SOURCE'},
                                     {'P1R_CAPABILITIES', 'P1R_CANDIDATE', 'P1R_FOLDED_SNAPSHOT'}
                                     if enrolled else set())
                    compiled = {item['Identity'] for item in evaluated['Items']['Compile']}
                    self.assertEqual(compiled & all_fixture_sources,
                                     expected_sources[role] if enrolled else set())
                    manifest_content = {
                        item['Identity']: item.get('CopyToOutputDirectory')
                        for item in evaluated['Items']['Content']
                        if Path(item['Identity']).name == 'fixture-evolution-manifest.json'
                    }
                    expected_content = {
                        'host': {'../domain/fixture-evolution-manifest.json': 'PreserveNewest'}
                                if enrolled else {},
                        'domain': {'fixture-evolution-manifest.json': 'PreserveNewest'},
                        'probe': {},
                    }
                    self.assertEqual(manifest_content, expected_content[role])
                    self.assertEqual({item['Identity'] for item in evaluated['Items']['PackageReference']},
                                     expected_packages)
                    self.assertFalse(evaluated['Items']['ProjectReference'])

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
