"""Component fixtures preserve local defaults and the workflow's Release/package policy."""
import importlib.util
from contextlib import ExitStack
import os
from pathlib import Path
import subprocess
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
CASES = (
    ('source_p13', 'SourceContainmentEvidenceTests'),
    ('source_p34', 'SourcePostActivationEvidenceTests'),
    ('source_snapshot', 'SourceSnapshotEmissionTests'),
    ('producer_originals', 'ProducerOriginalTests'),
    ('loop7_recovery', 'ActualNativeRecoveryEvidenceTests'),
    ('loop7_installation', 'ShippedRoleClosureTests'),
)


class ComponentBuildPolicyTests(unittest.TestCase):
    def fixture(self, module_name, class_name):
        spec = importlib.util.spec_from_file_location('build_policy_' + module_name,
            ROOT / 'tools/tests' / ('test_ext_protection_1_' + module_name + '.py'))
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return getattr(module, class_name)

    def component_paths(self, fixture):
        stack = ExitStack()
        if fixture.__name__ == 'ShippedRoleClosureTests':
            verifier = fixture.setUpClass.__func__.__globals__['v']
            stack.enter_context(patch.object(verifier, 'qualification_platform_root', return_value=ROOT / 'references/Hexalith.Platform'))
            stack.enter_context(patch('shutil.which', return_value='/test/dotnet'))
            stack.enter_context(patch.object(Path, 'glob', return_value=[Path('/test/packs/Microsoft.NETCore.App.Host.linux-x64/11.0.100/runtimes/linux-x64/native/apphost')]))
        return stack

    def test_all_components_honor_release_package_policy_without_scrubbing_ci(self):
        for module_name, class_name in CASES:
            fixture = self.fixture(module_name, class_name)
            with self.subTest(component=module_name), self.component_paths(fixture), patch.dict(os.environ, {
                'CI': 'true', 'EXT_PROTECTION_INTEROP_CONFIGURATION': 'Release',
                'EXT_PROTECTION_INTEROP_PROJECT_REFERENCES': 'false'}, clear=True), \
                 patch('subprocess.run', return_value=subprocess.CompletedProcess([], 1, 'captured-build', '')) as run:
                try:
                    with self.assertRaisesRegex(AssertionError, 'captured-build'):
                        fixture.setUpClass()
                    args = run.call_args.args[0]
                    self.assertEqual(args[args.index('-c') + 1], 'Release')
                    self.assertIn('-p:UseHexalithProjectReferences=false', args)
                    self.assertIn('-p:CustomAfterMicrosoftCommonProps=' + str(ROOT / 'tools/tests/ext_protection_component_runtime.props'), args)
                    self.assertEqual(run.call_args.kwargs['env']['CI'], 'true')
                finally:
                    fixture.doClassCleanups()

    def test_local_source_debug_defaults_are_preserved(self):
        for module_name, class_name in CASES:
            fixture = self.fixture(module_name, class_name)
            with self.subTest(component=module_name), self.component_paths(fixture), patch.dict(os.environ, {'CI': 'true'}, clear=True), \
                 patch('subprocess.run', return_value=subprocess.CompletedProcess([], 1, 'captured-build', '')) as run:
                try:
                    with self.assertRaisesRegex(AssertionError, 'captured-build'):
                        fixture.setUpClass()
                    args = run.call_args.args[0]
                    self.assertEqual(args[args.index('-c') + 1], 'Debug')
                    self.assertIn('-p:UseHexalithProjectReferences=true', args)
                    self.assertFalse(any(argument.startswith('-p:CustomAfterMicrosoftCommonProps=') for argument in args))
                    self.assertNotIn('CI', run.call_args.kwargs['env'])
                finally:
                    fixture.doClassCleanups()

    def test_target_path_query_retains_configuration_and_every_effective_build_property(self):
        for module_name, class_name in (CASES[2], CASES[3]):
            fixture = self.fixture(module_name, class_name)
            with self.subTest(component=module_name), patch.dict(os.environ, {
                'CI': 'true', 'EXT_PROTECTION_INTEROP_CONFIGURATION': 'Release',
                'EXT_PROTECTION_INTEROP_PROJECT_REFERENCES': 'false',
                'HexalithCommonsRoot': '/owner path/commons', 'HexalithTenantsBasePath': '/owner path/tenants'}, clear=True), \
                 patch('subprocess.run', side_effect=[subprocess.CompletedProcess([], 0, '', ''),
                    subprocess.CompletedProcess([], 0, '/missing-component.dll', '')]) as run:
                try:
                    with self.assertRaisesRegex(AssertionError, 'Owning component project must report its built TargetPath'):
                        fixture.setUpClass()
                    build, query = (call.args[0] for call in run.call_args_list)
                    self.assertIn('-p:Configuration=Release', query)
                    self.assertIn('-getProperty:TargetPath', query)
                    for argument in build:
                        if argument.startswith('-p:'):
                            self.assertIn(argument, query)
                    self.assertEqual(run.call_args_list[0].kwargs['env'], run.call_args_list[1].kwargs['env'])
                finally:
                    fixture.doClassCleanups()

    def test_package_probe_runtime_matches_current_role_verifier(self):
        fixture = self.fixture(*CASES[-1])
        verifier = fixture.setUpClass.__func__.__globals__['v']
        framework = ET.parse(ROOT / 'tools/tests/ext_protection_component_runtime.props').findtext('PropertyGroup/TargetFramework')
        self.assertEqual(framework, f'net{verifier.ROLE_RUNTIME_MAJOR}.0')

    def test_invalid_component_policy_fails_before_building(self):
        for module_name, class_name in CASES:
            for configuration, references in (('Checked', 'false'), ('Release', 'yes')):
                fixture = self.fixture(module_name, class_name)
                with self.subTest(component=module_name, configuration=configuration, references=references), \
                     patch.dict(os.environ, {'EXT_PROTECTION_INTEROP_CONFIGURATION': configuration,
                         'EXT_PROTECTION_INTEROP_PROJECT_REFERENCES': references}, clear=True), patch('subprocess.run') as run:
                    try:
                        with self.assertRaisesRegex(ValueError, 'Invalid offline interop build configuration'):
                            fixture.setUpClass()
                        run.assert_not_called()
                    finally:
                        fixture.doClassCleanups()


if __name__ == '__main__':
    unittest.main()
