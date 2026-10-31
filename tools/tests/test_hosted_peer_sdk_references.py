"""Evaluate actual SDK reference choices for the explicit hosted-test package profile."""
import json
import os
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]
PROJECTS = {
    'samples/Hexalith.EventStore.Sample.Api/Hexalith.EventStore.Sample.Api.csproj':
        {'Hexalith.EventStore.Client', 'Hexalith.EventStore.RestApi.Generators', 'Hexalith.EventStore.ServiceDefaults'},
    'samples/Hexalith.EventStore.Sample.Contracts/Hexalith.EventStore.Sample.Contracts.csproj':
        {'Hexalith.EventStore.Contracts'},
    'samples/Hexalith.EventStore.Sample.BlazorUI/Hexalith.EventStore.Sample.BlazorUI.csproj':
        {'Hexalith.EventStore.Client', 'Hexalith.EventStore.ServiceDefaults', 'Hexalith.EventStore.SignalR'},
    'src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj':
        {'Hexalith.EventStore.Admin.Server', 'Hexalith.EventStore.ServiceDefaults'},
    'src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj':
        {'Hexalith.EventStore.Admin.Abstractions', 'Hexalith.EventStore.Client',
         'Hexalith.EventStore.ServiceDefaults', 'Hexalith.EventStore.SignalR'},
}


class HostedPeerSdkReferenceTests(unittest.TestCase):
    def evaluate(self, project, mode=None, publishing=False):
        # The owning catalog path is resolved only from root-declared Builds checkouts; never initialize nesting.
        selected = os.environ.get('HEXALITH_BUILD_PACKAGE_PROPS')
        candidates = ([Path(selected)] if selected else []) + [
            ROOT / 'references/Hexalith.Builds/Props/Directory.Packages.props',
            ROOT.parent / 'Hexalith.Builds/Props/Directory.Packages.props',
            ROOT.parent.parent / 'references/Hexalith.Builds/Props/Directory.Packages.props']
        catalog = next((p for p in candidates if p.is_file()), None)
        self.assertIsNotNone(catalog, 'Supply an existing root-declared Builds catalog for MSBuild evaluation')
        args = ['dotnet', 'msbuild', str(ROOT / project), '-nologo',
                '-getItem:ProjectReference,PackageReference,PackageVersion',
                '-p:Hexalith1BuildPackageProps=' + str(catalog), '-p:HexalithEventStoreVersion=9.8.7',
                '-p:UseNuGetDeps=true', '-p:UseHexalithProjectReferences=false']
        if mode is not None:
            args.append('-p:HexalithUsePublishedSdkForHostedTests=' + mode)
        if publishing:
            args.append('-p:IsPublishing=true')
        output = subprocess.check_output(args, cwd=ROOT, text=True, timeout=60)
        return json.loads(output[output.index('{'):])['Items']

    def test_all_five_projects_preserve_native_edges_and_choose_only_explicit_packages(self):
        for project, expected in PROJECTS.items():
            with self.subTest(project=project):
                native = self.evaluate(project)
                disabled = self.evaluate(project, 'false')
                publishing = self.evaluate(project, publishing=True)
                package = self.evaluate(project, 'true')
                identities = lambda result, kind: {Path(item['Identity'].replace('\\', '/')).stem
                                                  for item in result[kind]}
                self.assertTrue(expected <= identities(native, 'ProjectReference'))
                self.assertEqual(identities(native, 'ProjectReference'), identities(disabled, 'ProjectReference'))
                self.assertEqual(identities(native, 'ProjectReference'), identities(publishing, 'ProjectReference'))
                self.assertFalse(expected & identities(package, 'ProjectReference'))
                self.assertEqual(identities(native, 'ProjectReference') - expected,
                                 identities(package, 'ProjectReference'))
                direct = {item['Identity']: item for item in package['PackageReference']}
                central = {item['Identity']: item for item in package['PackageVersion']}
                for identity in expected:
                    self.assertIn(identity, direct)
                    self.assertFalse(direct[identity].get('Version'), 'The owning central catalog must select versions')
                    self.assertEqual('9.8.7', central[identity]['Version'], 'The explicit build version override must reach central package evaluation')
                if 'Hexalith.EventStore.RestApi.Generators' in expected:
                    self.assertEqual('all', direct['Hexalith.EventStore.RestApi.Generators']['PrivateAssets'])


if __name__ == '__main__':
    unittest.main()
