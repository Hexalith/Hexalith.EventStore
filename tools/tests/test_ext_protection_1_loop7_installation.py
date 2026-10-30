"""Offline actual shipped apphost closure and paired local-checkout preflight; never Live or role execution."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('ext_protection_loop7_installation', ROOT / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(v)


class PairedCheckoutPreflightTests(unittest.TestCase):
    def test_actual_paired_preflight_requires_clean_exact_reachable_revisions(self):
        with tempfile.TemporaryDirectory(prefix='protection-paired-checkout-') as temp:
            # The owning repository requires exact candidate validation even for disposable fixture history.
            messages = ('test(protection): seed paired checkout fixture', 'test(protection): advance paired checkout fixture')
            cli = Path(os.environ.get('EXT_PROTECTION_COMMITLINT_CLI', str(ROOT / 'node_modules/@commitlint/cli/cli.js'))).resolve()
            config = Path(os.environ.get('EXT_PROTECTION_COMMITLINT_CONFIG', str(ROOT / 'commitlint.config.mjs'))).resolve()
            if json.loads((cli.parent / 'package.json').read_bytes())['version'] != '21.1.0' or config.read_bytes() != (ROOT / 'commitlint.config.mjs').read_bytes():
                raise AssertionError('Owning pinned commitlint CLI and exact configuration required')
            message_files = []
            for index, message in enumerate(messages):
                candidate = Path(temp) / ('message-' + str(index) + '.txt'); candidate.write_text(message + '\n'); message_files.append(candidate)
                checked = subprocess.run(['node', str(cli), '--config', str(config), '--edit', str(candidate), '--verbose'], capture_output=True, text=True, check=False)
                self.assertEqual(checked.returncode, 0, checked.stdout + checked.stderr)
                print(json.dumps({'commitMessage': message, 'sha256': hashlib.sha256(candidate.read_bytes()).hexdigest(), 'commitlintVersion': '21.1.0', 'configurationSha256': hashlib.sha256(config.read_bytes()).hexdigest(), 'exitCode': checked.returncode, 'validation': checked.stdout.strip()}))
            roots, heads = [], []
            for name in ('eventstore', 'platform'):
                root = Path(temp) / name; root.mkdir(); roots.append(root)
                def git(*args):
                    return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.DEVNULL, text=True).strip()
                git('init', '-q'); git('config', 'user.name', 'Offline Test'); git('config', 'user.email', 'offline@example.invalid')
                (root / 'source').write_text('accepted'); git('add', 'source'); git('commit', '-q', '-F', str(message_files[0]))
                head = git('rev-parse', 'HEAD'); heads.append(head); git('update-ref', 'refs/remotes/origin/main', head)
            record = {'TargetVersionOrCommit': heads[0]}; profile = {'platformRoot': str(roots[1]), 'platformTargetCommit': heads[1]}
            v.paired_checkout_preflight(roots[0], record, profile)
            for selected in range(2):
                root = roots[selected]
                for state in ('wrong-head', 'dirty', 'untracked', 'unreachable'):
                    if state == 'dirty': (root / 'source').write_text('changed')
                    elif state == 'untracked': (root / 'untracked').write_text('unknown')
                    elif state == 'wrong-head':
                        (root / 'source').write_text('new'); subprocess.run(['git', '-C', str(root), 'add', 'source'], check=True); subprocess.run(['git', '-C', str(root), 'commit', '-q', '-F', str(message_files[1])], check=True)
                    else: subprocess.run(['git', '-C', str(root), 'update-ref', '-d', 'refs/remotes/origin/main'], check=True)
                    with self.subTest(owner=selected, state=state), self.assertRaises(v.Blocked):
                        v.paired_checkout_preflight(roots[0], record, profile)
                    subprocess.run(['git', '-C', str(root), 'reset', '--hard', heads[selected]], check=True, stdout=subprocess.DEVNULL)
                    (root / 'untracked').unlink(missing_ok=True)
                    subprocess.run(['git', '-C', str(root), 'update-ref', 'refs/remotes/origin/main', heads[selected]], check=True)
            with self.assertRaisesRegex(v.Blocked, 'DISTINCT_PLATFORM_CHECKOUT_REQUIRED'):
                v.paired_checkout_preflight(roots[0], record, {**profile, 'platformRoot': str(roots[0])})


class ShippedRoleClosureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix='protection-shipped-closure-'); cls.addClassCleanup(cls.temporary.cleanup)
        cls.directory = Path(cls.temporary.name); cls.platform = v.qualification_platform_root(ROOT)
        cls.host = Path(shutil.which('dotnet')).resolve(); cls.roles = {}
        cls.template = max((cls.host.parent / 'packs/Microsoft.NETCore.App.Host.linux-x64').glob('10.0.*/runtimes/linux-x64/native/apphost'), key=lambda path: tuple(map(int, path.parts[-5].split('.'))))
        for role in v.ROLE_NAMES:
            assembly = 'Hexalith.Platform.Protection.Qualification.' + role.capitalize()
            project = cls.platform / 'src' / assembly / (assembly + '.csproj')
            output = cls.platform / 'src' / assembly / 'bin/Debug/net10.0'
            if not (output / assembly).is_file():
                command = [str(cls.host), 'build', str(project), '-c', 'Debug', '-m:1', '-nodeReuse:false', '-p:UseHexalithProjectReferences=true', '-p:GeneratePackageOnBuild=false', '-p:HexalithEventStoreRoot=' + str(ROOT)]
                for name in ('HexalithCommonsRoot', 'HexalithTenantsBasePath'):
                    if os.environ.get(name): command.append('-p:' + name + '=' + os.environ[name])
                built = subprocess.run(command, capture_output=True, text=True, timeout=300)
                if built.returncode: raise AssertionError(built.stdout + built.stderr)
            installed = cls.directory / role; shutil.copytree(output, installed)
            key, public = installed / 'private-test.pem', installed / 'public.pem'
            subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(key)], check=True, capture_output=True)
            subprocess.run(['openssl', 'pkey', '-in', str(key), '-pubout', '-out', str(public)], check=True, capture_output=True)
            executable = installed / assembly
            config = {'executable': str(executable), 'executableSha256': v.digest(executable.read_bytes()), 'publicAnchorFile': str(public), 'publicAnchorSha256': v.digest(public.read_bytes()),
                      'credentialReference': 'env:EXT_PROTECTION_ROLE_LOOP7_' + role.upper(), 'identity': 'loop7-' + role, 'installationManifest': str(installed / 'installation.json')}
            client = installed / ('protection-role.' + role + '.json')
            client.write_bytes(v.canonical({'Schema': 'ext-protection-1-role-program/v1', 'Role': role, 'Identity': config['identity'], 'Endpoint': 'https://' + role + '.invalid/role',
                                           'CredentialReference': config['credentialReference'], 'PublicAnchorFile': str(public), 'PublicAnchorSha256': config['publicAnchorSha256']}))
            assets = set(v.resolved_application_assets(installed / (assembly + '.dll'), cls.host, cls.template)['files'])
            assets.update(map(str, (executable, installed / (assembly + '.deps.json'), installed / (assembly + '.runtimeconfig.json'), client, public, cls.host, cls.template)))
            manifest = {'schema': 'ext-protection-1-role-installation/v2', 'role': role, 'entryAssembly': str(installed / (assembly + '.dll')), 'sharedFrameworkBoundary': 'trusted-machine-dotnet10-sdk-framework-and-os',
                        'dotnetHost': str(cls.host), 'appHostTemplate': str(cls.template), 'files': [{'path': path, 'sha256': v.digest(Path(path).read_bytes())} for path in sorted(assets)]}
            Path(config['installationManifest']).write_bytes(v.canonical(manifest)); config['installationManifestSha256'] = v.digest(Path(config['installationManifest']).read_bytes())
            cls.roles[role] = config
        # Capability verifier remains independent of all three role signing keys.
        key, public = cls.directory / 'capability-private.pem', cls.directory / 'capability.pem'
        subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(key)], check=True, capture_output=True)
        subprocess.run(['openssl', 'pkey', '-in', str(key), '-pubout', '-out', str(public)], check=True, capture_output=True)
        cls.profile = {'schema': 'ext-protection-1-profile/v3', 'targetCommit': 'a' * 40, 'platformTargetCommit': 'b' * 40, 'platformRoot': str(cls.platform), 'roleProgramContract': 'ext-protection-1-role-program/v1',
                       'planSha256': 'c' * 64, 'tenantReferences': {'primary': 'tenant-a', 'other': 'tenant-b'}, 'roles': cls.roles,
                       'engineBinary': {'path': str(cls.directory / 'executor/Hexalith.EventStore.Server.dll'), 'sha256': v.digest((cls.directory / 'executor/Hexalith.EventStore.Server.dll').read_bytes())},
                       'capabilityTrust': [{'issuer': 'issuer', 'audience': 'audience', 'tenantId': 'tenant-a', 'keyFamily': 'DeletionBatchCapabilitySigningKey', 'keyVersion': 'key-1', 'publicAnchorId': 'independent',
                                            'publicAnchorVersion': '1', 'publicAnchorFile': str(public), 'publicAnchorSha256': v.digest(public.read_bytes()), 'trustProfileRevision': 1}]}

    def validate(self, profile=None):
        profile = profile or self.profile; path = self.directory / 'profile.json'; path.write_bytes(v.canonical(profile))
        return v.validate_profile(path, {'profileSha256': v.digest(path.read_bytes()), 'TargetVersionOrCommit': 'a' * 40, 'platformTargetCommit': 'b' * 40}, 'c' * 64)

    def environment(self):
        return patch.dict(os.environ, {'EXT_PROTECTION_ROLE_LOOP7_' + role.upper(): 'independent-' + role for role in v.ROLE_NAMES})

    def test_complete_actual_runtime_layout_and_disjoint_credentials_pass(self):
        with self.environment():
            accepted = self.validate(); self.assertIsInstance(accepted['roles'], v.ValidatedRoleConfigurations)
            for role, config in self.roles.items():
                pins = v.validate_role_installation(role, config)
                self.assertIn(str(Path(config['executable']).parent / 'Hexalith.Platform.Protection.dll'), pins)
                self.assertGreater(len(pins), 20)

    def test_every_resolved_dependency_and_configuration_mutation_or_removal_refuses_before_role(self):
        with self.environment():
            accepted = self.validate()
            for role, config in self.roles.items():
                manifest = v.read_json(config['installationManifest'])
                role_client = v.Role(role, config, 'binding', self.directory, self.profile['engineBinary'], self.profile['capabilityTrust'], accepted['roles'])
                for item in manifest['files']:
                    path = Path(item['path'])
                    if path in (self.host, self.template): continue  # Machine host pin is verified without mutating the installed SDK.
                    original = path.read_bytes(); mode = path.stat().st_mode
                    try:
                        for mutation in ('change', 'remove'):
                            if mutation == 'change': path.write_bytes(original + b'changed')
                            else: path.unlink()
                            with self.subTest(role=role, asset=path.name, mutation=mutation), patch.object(v.subprocess, 'run', side_effect=AssertionError('No process may run after pin drift')), self.assertRaises(v.Blocked):
                                role_client.request({'operationId': 'must-not-run'})
                            path.write_bytes(original); path.chmod(mode)
                    finally: path.write_bytes(original); path.chmod(mode)

    def test_each_same_value_credential_pair_and_all_three_aliases_refuse(self):
        for aliases in ((0, 1), (0, 2), (1, 2), (0, 1, 2)):
            values = {'EXT_PROTECTION_ROLE_LOOP7_' + role.upper(): 'different-' + role for role in v.ROLE_NAMES}
            for index in aliases: values['EXT_PROTECTION_ROLE_LOOP7_' + v.ROLE_NAMES[index].upper()] = 'same-private-test-value'
            with self.subTest(aliases=aliases), patch.dict(os.environ, values), self.assertRaisesRegex(v.Blocked, 'ROLE_SEPARATION_MISSING'): self.validate()

    def test_an_arbitrary_elf_repin_cannot_impersonate_the_shipped_apphost(self):
        config = self.roles['executor']; executable = Path(config['executable']); manifest_path = Path(config['installationManifest'])
        original, manifest_bytes, mode = executable.read_bytes(), manifest_path.read_bytes(), executable.stat().st_mode
        try:
            executable.write_bytes(Path('/usr/bin/true').read_bytes()); executable.chmod(mode)
            changed = {**config, 'executableSha256': v.digest(executable.read_bytes())}
            manifest = v.read_json(manifest_path)
            for item in manifest['files']:
                if item['path'] == str(executable): item['sha256'] = changed['executableSha256']
            manifest_path.write_bytes(v.canonical(manifest)); changed['installationManifestSha256'] = v.digest(manifest_path.read_bytes())
            with self.assertRaisesRegex(v.Blocked, 'APPLICATION_DEPENDENCY_RESOLUTION_FAILED'): v.validate_role_installation('executor', changed)
        finally:
            executable.write_bytes(original); executable.chmod(mode); manifest_path.write_bytes(manifest_bytes)

    def test_loader_control_credentials_refuse_before_profile_acceptance_or_child_launch(self):
        for variable in ('DOTNET_STARTUP_HOOKS', 'DOTNET_ADDITIONAL_DEPS', 'DOTNET_SHARED_STORE', 'DOTNET_ROOT', 'LD_PRELOAD', 'LD_LIBRARY_PATH', 'PATH', 'BASH_ENV', 'PYTHONPATH'):
            config = {**self.roles['executor'], 'credentialReference': 'env:' + variable}
            profile = copy.deepcopy(self.profile); profile['roles']['executor'] = config
            with self.environment(), patch.dict(os.environ, {variable: 'synthetic-test-value'}):
                with self.subTest(variable=variable), patch.object(v.subprocess, 'run', side_effect=AssertionError('No process may run for a reserved credential name')):
                    with self.assertRaisesRegex(v.Blocked, 'INERT_ROLE_CREDENTIAL_NAMESPACE_REQUIRED'): self.validate(profile)
                    with self.assertRaisesRegex(v.Blocked, 'INERT_ROLE_CREDENTIAL_NAMESPACE_REQUIRED'): v.isolated_environment(config['credentialReference'])
                    client = v.Role('executor', config, 'binding', self.directory, self.profile['engineBinary'], self.profile['capabilityTrust'], self.roles,
                                    trust_snapshot=object())
                    with self.assertRaisesRegex(v.Blocked, 'INERT_ROLE_CREDENTIAL_NAMESPACE_REQUIRED'): client.request({'operationId': 'must-not-run'})

    def test_actual_child_handoff_excludes_inherited_loader_controls_and_uses_only_pinned_runtime(self):
        config = self.roles['executor']
        client = v.Role('executor', config, 'binding', self.directory, self.profile['engineBinary'], self.profile['capabilityTrust'], self.roles)
        captured = []
        def capture(*args, **kwargs):
            captured.append(kwargs['env']); return subprocess.CompletedProcess(args[0], 1, b'', b'')
        with self.environment(), patch.dict(os.environ, {'DOTNET_STARTUP_HOOKS': 'unlisted-assembly', 'LD_PRELOAD': 'unlisted-native-library', 'DOTNET_ROOT': '/unaccepted/runtime'}), patch.object(v.subprocess, 'run', side_effect=capture):
            # Closure resolution is already independently covered; isolate only the last credential-bearing handoff.
            with patch.object(v, 'revalidate_installation'):
                with self.assertRaisesRegex(v.Blocked, 'ROLE_RESULT_UNCERTAIN'): client.request({'operationId': 'handoff'})
        self.assertEqual(len(captured), 1)
        child = captured[0]
        client = Path(config['executable']).parent / 'protection-role.executor.json'
        self.assertEqual(set(child), {'PATH', 'LANG', 'LC_ALL', 'DOTNET_ROOT', config['credentialReference'][4:], 'EXT_PROTECTION_ACCEPTED_ROLE_CONFIGURATION_SHA256'})
        self.assertEqual(child['EXT_PROTECTION_ACCEPTED_ROLE_CONFIGURATION_SHA256'], v.digest(client.read_bytes()))
        self.assertEqual(child['DOTNET_ROOT'], str(self.host.parent))
        self.assertNotIn('DOTNET_STARTUP_HOOKS', child); self.assertNotIn('LD_PRELOAD', child)

    def test_old_schema_executable_only_and_external_probing_fail_closed(self):
        with self.environment():
            for schema in ('ext-protection-1-profile/v1', 'ext-protection-1-profile/v2'):
                with self.assertRaisesRegex(v.Blocked, 'PROFILE_BINDING_MISMATCH'): self.validate({**self.profile, 'schema': schema})
            config = self.roles['executor']; path = Path(config['installationManifest']); original = path.read_bytes()
            try:
                path.write_bytes(v.canonical({'files': [{'path': config['executable'], 'sha256': config['executableSha256']}]}))
                changed = {**config, 'installationManifestSha256': v.digest(path.read_bytes())}
                with self.assertRaises(v.Blocked): v.validate_role_installation('executor', changed)
            finally: path.write_bytes(original)
