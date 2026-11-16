#!/usr/bin/env python3
"""Exercise the genuine bridge and fixed Python child in disposable repository copies.

CI uses Release; local verification uses Debug. Neither bridge graph needs packages.
Synthetic profile files are created exclusively under the private temporary checkout.
"""
import argparse
import copy
import json
import os
import runpy
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]


def run(command, root, expected=0, environment=None):
    result = subprocess.run(command, cwd=root, env=environment, text=True, capture_output=True, timeout=180)
    if result.returncode != expected:
        raise AssertionError(f"command exited {result.returncode}, expected {expected}\n{result.stdout}\n{result.stderr}")
    return result


def copy_repository(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    run(['git', 'clone', '--shared', '--no-checkout', '--quiet', str(source), str(target)], source)
    paths = subprocess.check_output(['git', '-C', str(source), 'ls-files', '-z', '--cached', '--others', '--exclude-standard']).split(b'\0')
    for item in paths:
        if not item:
            continue
        relative = Path(os.fsdecode(item))
        original, destination = source / relative, target / relative
        if original.is_file() and not original.is_symlink():
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(original, destination)


def poison_build_ancestors(ancestors):
    """Fail any accidental import or response-file use from these ancestors."""
    poison = '<Project><Target Name="Poison" BeforeTargets="PrepareForBuild"><Error Text="unrelated-build-input-executed" /></Target></Project>'
    for ancestor in ancestors:
        for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
            (ancestor / name).write_text(poison)
        (ancestor / 'Directory.Build.rsp').write_text(
            '-p:CustomBeforeMicrosoftCommonTargets=' + str(ancestor / 'Directory.Build.targets'))


def check_evaluated_restore_sources(root, data):
    """Inspect SDK-evaluated sources; no package or restore target is exercised."""
    import publication_authority as P
    import phase_4_transition as T
    bounded = T.Control.bounded_process
    evaluated = RuntimeError('evaluation complete')
    names = ('RestoreSources', 'RestoreAdditionalProjectSources', 'RestoreFallbackFolders',
             'RestoreAdditionalProjectFallbackFolders')
    def observed(command, private, filename, **kwargs):
        assert filename == 'restore.log'
        library_packs = private / 'library-packs'
        library_packs.mkdir()
        flags = [flag for flag in command[command.index('--disable-build-servers'):]
                 if flag != '--disable-build-servers']
        control = '-p:DisableImplicitLibraryPacksFolder=true'
        assert control in flags
        values = []
        for disabled in (False, True):
            selected = flags if disabled else [flag for flag in flags if flag != control]
            query = [command[0], 'msbuild', command[2], *selected,
                     '-getProperty:' + ','.join(names), '-p:_WorkloadLibraryPacksFolder=' + str(library_packs)]
            output = bounded(query, private, 'sources-' + str(disabled) + '.json', **kwargs)
            values.append(json.loads(output)['Properties'])
        assert str(library_packs) in values[0]['RestoreAdditionalProjectSources'].split(';'), values[0]
        assert set(values[1]) == set(names) and all(value == '' for value in values[1].values()), values[1]
        raise evaluated
    with mock.patch.object(T.Control, 'bounded_process', side_effect=observed):
        try:
            P.B.execute(P.B.capture(root, data), P.digest(data[P.INVENTORY]), P.digest(data[P.PROFILE]), '-')
        except RuntimeError as error:
            assert error is evaluated
        else:
            raise AssertionError('evaluation unexpectedly reached restore/build execution')
    print('evaluated-sdk-library-packs-source-only (no package restore): PASS', flush=True)


def check_missing_targeting_pack(root, dotnet, scratch):
    """Relocate the selected installed SDK without its reference packs."""
    import publication_authority as P
    import phase_4_transition as T
    incomplete = scratch / 'sdk-without-packs'
    incomplete.mkdir()
    shutil.copy2(dotnet, incomplete / 'dotnet')
    for name in ('host', 'shared'):
        (incomplete / name).symlink_to(Path(dotnet).parent / name, target_is_directory=True)
    selected = run([dotnet, '--version'], root).stdout.strip()
    # Linking the SDK itself would resolve its original installation's packs.
    shutil.copytree(Path(dotnet).parent / 'sdk' / selected, incomplete / 'sdk' / selected)
    diagnostics = []
    bounded = T.Control.bounded_process
    def observed(command, private, filename, **kwargs):
        try:
            return bounded(command, private, filename, **kwargs)
        except T.Refusal:
            diagnostics.append((private / filename).read_bytes())
            raise
    with mock.patch.object(P.B.shutil, 'which', return_value=str(incomplete / 'dotnet')), \
         mock.patch.object(T.Control, 'bounded_process', side_effect=observed):
        try:
            P.profile_check(root, None)
        except P.Refusal as error:
            assert error.code == 'PROFILE_OWNER_VALIDATION_FAILED', error.code
        else:
            raise AssertionError('missing installed targeting pack accepted or fetched')
    # .NET 11 can first refuse missing prune data from the reference pack;
    # older SDKs can reach the explicit missing-targeting-pack diagnostic.
    content = b'\n'.join(diagnostics)
    code = next((value for value in (b'NETSDK1226', b'NETSDK1127') if value in content), None)
    assert code is not None, content.decode()
    print('missing-installed-targeting-pack-no-fallback: ' + code.decode() + ', PASS', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    args = parser.parse_args()
    original = args.root.resolve()
    originals = {path: (original / path).read_bytes() if (original / path).exists() else None
                 for path in ('deploy/dapr/production-profile.yaml', 'deploy/dapr/production-composing-inventory.json',
                              'deploy/dapr/publication-authority.json')}
    executable = shutil.which('dotnet')
    if not executable:
        raise AssertionError('dotnet SDK is required; this integration check cannot skip')
    dotnet = str(Path(executable).resolve())
    with tempfile.TemporaryDirectory(prefix='publication-bridge-integration-') as directory, \
         mock.patch.object(tempfile, 'tempdir', directory):
        scratch = Path(directory)
        workspace = scratch / 'workspace'
        root = workspace / 'references/Hexalith.EventStore'
        copy_repository(original, root)
        sys.path.insert(0, str(root / 'tools'))
        import publication_authority as P
        import phase_4_transition as T
        # These inputs must never be compiled/imported. No Builds/Commons/Tenants
        # checkout exists in this private workspace (and no nested checkout runs).
        (root / 'src/Hexalith.EventStore.Contracts/UnrelatedPoison.cs').write_text('#error unrelated Contracts must never compile')
        # Helper execution scratch is a direct child of this controlled tempdir,
        # so the poison also lies on the actual build/response-file ancestry.
        poison_build_ancestors((root, root.parent, workspace, scratch))
        (root / 'nuget.config').write_text('poisoned unrelated NuGet configuration')
        for name in ('Hexalith.Builds', 'Hexalith.Commons', 'Hexalith.Tenants'):
            assert not (workspace / 'references' / name).exists()
        for version in (1, 2):
            fixture = root / 'tests/Hexalith.EventStore.Contracts.Tests/Deployment/Fixtures'
            encode_profile = runpy.run_path(str(fixture / 'verify-golden.py'))['encode']
            golden = (fixture / f'SyntheticProfile.v{version}.json').read_bytes()
            digests = json.loads((fixture / f'SyntheticDigests.v{version}.json').read_bytes())
            applications = ['app:eventstore', 'app:tenants']
            components = ['component:actor-eventstore', 'component:actor-tenants', 'component:readmodels', 'component:broker',
                          'component:openbao', 'component:scheduler', 'component:placement', 'component:issuer', 'component:backup']
            if version == 2:
                components += ['component:distribution', 'component:containerd', 'component:os', 'component:crd',
                               'component:environment-service', 'component:management', 'component:workload', 'component:rancher']
            composition = root / 'deploy/dapr/synthetic-composition.json'
            composition.parent.mkdir(parents=True, exist_ok=True)
            composition.write_text(json.dumps({'applications': applications, 'components': components}, sort_keys=True))
            inventory = {'schema': f'hexalith.eventstore.production-composing-inventory.v{version}',
                         'source': {'path': composition.relative_to(root).as_posix(), 'sha256': P.digest(composition.read_bytes())},
                         'expectedApplicationIds': applications, 'expectedComponentIds': components}
            profile_path, inventory_path = root / P.PROFILE, root / P.INVENTORY
            sentinel = 'fixture-value-must-never-appear-in-diagnostics'

            def prepare(model=None, profile=golden):
                profile_path.write_bytes(profile)
                inventory_path.write_text(json.dumps(inventory if model is None else model, sort_keys=True))

            def bridge(label, expected, resolved=None, issue=None):
                data = {P.PROFILE: profile_path.read_bytes(), P.INVENTORY: inventory_path.read_bytes(),
                        composition.relative_to(root).as_posix(): composition.read_bytes()}
                outputs = []
                bounded = T.Control.bounded_process
                def observed(command, private, filename, **kwargs):
                    assert private.parent == scratch
                    constructor = subprocess.Popen
                    processes = []
                    def spawn(*args, **options):
                        process = constructor(*args, **options)
                        processes.append(process)
                        return process
                    try:
                        with mock.patch.object(subprocess, 'Popen', side_effect=spawn):
                            return bounded(command, private, filename, **kwargs)
                    finally:
                        if filename == 'profile-result.txt':
                            outputs.append(((private / filename).read_bytes(), processes[-1].returncode))
                try:
                    with mock.patch.object(T.Control, 'bounded_process', side_effect=observed):
                        output = P.B.execute(P.B.capture(root, data), P.digest(inventory_path.read_bytes()),
                                             resolved or digests['resolved'], digests['infrastructure'])
                except P.B.Refusal as error:
                    assert expected != 0 and error.code == 'PROFILE_OWNER_VALIDATION_FAILED', (label, error.code)
                    output = outputs[-1][0] if outputs else b''
                else:
                    assert expected == 0, label + ' unexpectedly succeeded'
                assert outputs and outputs[-1][1] == expected, (label, outputs)
                diagnostic = output.decode()
                assert sentinel not in diagnostic
                assert 'Unhandled exception' not in diagnostic and ' at Program.' not in diagnostic
                if issue is not None:
                    assert issue in {item['Code'] for item in json.loads(diagnostic)}
                if expected == 0:
                    document = json.loads(diagnostic)
                    assert document['isStructurallyValid'] and document['authorization'] == document['qualification'] == 'unevaluated'
                    assert document['resolvedDigest'] == digests['resolved']
                    assert document['infrastructureDigest'] == digests['infrastructure']
                print(f'v{version}-' + label + ': PASS', flush=True)

            prepare()
            if version == 1:
                check_evaluated_restore_sources(root, {P.PROFILE: golden, P.INVENTORY: inventory_path.read_bytes(),
                    composition.relative_to(root).as_posix(): composition.read_bytes()})
            bridge('complete-synthetic', 0)
            captured = P.B.capture(root, {P.PROFILE: golden, P.INVENTORY: inventory_path.read_bytes(),
                                         composition.relative_to(root).as_posix(): composition.read_bytes()})
            source_path = root / P.B.SOURCES[0]
            original_source = source_path.read_bytes()
            try:
                source_path.write_text('#error mutable checkout source must never compile')
                profile_path.write_bytes(b'mutable checkout profile must never decode')
                output = P.B.execute(captured, P.digest(inventory_path.read_bytes()), digests['resolved'], digests['infrastructure'])
                P.validate_owner_result(output, digests['resolved'], digests['infrastructure'], digests['infrastructure'])
            finally:
                source_path.write_bytes(original_source)
                prepare()
            print(f'v{version}-verified-snapshot-survives-checkout-mutation: PASS', flush=True)
            other = 2 if version == 1 else 1
            prepare({**inventory, 'schema': f'hexalith.eventstore.production-composing-inventory.v{other}'})
            bridge('explicit-version-mismatch-no-fallback', 1)
            if version == 2:
                prepare({**inventory, 'expectedComponentIds': [item for item in components if item != 'component:containerd']})
                bridge('missing-node-instance-coverage', 1)
                prepare({**inventory, 'expectedComponentIds': [*components, 'component:node-absent']})
                bridge('unexpected-expected-instance', 1)
                candidate = json.loads(golden)
                next(c for c in candidate['components'] if c['id'] == 'component:actor-eventstore')['dapr']['aclDigest'] = None
                invalid = encode_profile(candidate)
                prepare(profile=invalid); bridge('missing-applicable-dapr-config', 1, P.digest(invalid), 'missing-selection')
                candidate = json.loads(golden)
                next(c for c in candidate['components'] if c['id'] == 'component:containerd')['scope']['namespace'] = 'invented'
                invalid = encode_profile(candidate)
                prepare(profile=invalid); bridge('fabricated-node-namespace', 1, P.digest(invalid), 'invalid-scope-identifiers')
                candidate = json.loads(golden)
                next(c for c in candidate['components'] if c['id'] == 'component:containerd')['bundleParentId'] = 'component:absent'
                invalid = encode_profile(candidate)
                prepare(profile=invalid); bridge('dangling-bundle', 1, P.digest(invalid), 'dangling-reference')
            prepare()
            inventory['source']['sha256'] = '0'*64
            prepare(); bridge('composition-source-digest-mismatch', 2)
            inventory['source']['sha256'] = P.digest(composition.read_bytes())
            prepare(profile=golden + b'\n'); bridge('noncanonical', 1)
            prepare({**inventory, 'expectedComponentIds': components[1:]}); bridge('underdeclared-inventory', 1)
            prepare(); bridge('digest-domain-substitution', 1, digests['infrastructure'])
            profile_path.rename(profile_path.with_name('alternate-profile.yaml'))
            try:
                P.profile_check(root, None)
            except P.Refusal as error:
                assert error.code == 'PROFILE_MISSING', error.code
            else:
                raise AssertionError('alternate profile path accepted')
            print(f'v{version}-missing-sole-slot: PASS', flush=True)
            prepare({**inventory, 'source': {'path': P.INVENTORY, 'sha256': 'a'*64}}); bridge('self-inventory-source', 2)
            prepare({**inventory, 'schema': sentinel}); bridge('wrong-schema-no-echo', 2)
            inventory_path.write_text('{"schema":"' + sentinel + '","schema":"duplicate"}')
            bridge('duplicate-fields-no-echo', 2)
            prepare(); inventory_path.write_text(sentinel); bridge('malformed-json-no-echo', 2)
            # Exercise the real child with poisoned caller properties and imports.
            prepare()
            with mock.patch.dict(os.environ, {'MSBuildSDKsPath': str(root / 'untrusted-sdk'),
                    'DirectoryBuildPropsPath': str(root / 'Directory.Build.props'),
                    'CustomBeforeMicrosoftCommonTargets': str(root / 'Directory.Build.targets'),
                    'NUGET_PACKAGES': str(root / 'untrusted-packages'), 'GITHUB_TOKEN': sentinel}):
                assert P.profile_check(root, None) == composition.relative_to(root).as_posix()
            print('fixed-python-child-complete: PASS', flush=True)
            prepare(profile=golden + b'\n')
            try:
                P.profile_check(root, None)
            except P.Refusal as error:
                assert error.code == 'PROFILE_OWNER_VALIDATION_FAILED', error.code
            else:
                raise AssertionError('fixed Python child accepted noncanonical profile')
            print('fixed-python-child-noncanonical: PASS', flush=True)
            from test_publication_authority import chain
            owner_profile=json.loads(golden)
            rendered_path='deploy/dapr/synthetic-rendered.yaml'
            rendered=b'synthetic rendered deployment'
            promotion_bytes=golden.replace(('\"renderedDeploymentDigest\":\"'+owner_profile['environment']['renderedDeploymentDigest']+'\"').encode(),
                ('\"renderedDeploymentDigest\":\"'+P.digest(rendered)+'\"').encode())
            owner_profile=json.loads(promotion_bytes)
            release=owner_profile['release']
            record=chain()['records'][-1]
            packages=[{'id':a['id'],'version':a['version'],'sha256':a['digest']} for a in release['artifacts'] if a['kind']=='module-package']
            record['subject'].update(packages=packages,server_package_sha256=packages[0]['sha256'],
                oci_index_sha256=release['composedHostDigest'],extensions_sha256=release['extensionDigests'],
                catalog_sha256=release['catalogContentDigest'],runtime_sha256=release['runtimeImageDigest'])
            prepare(profile=promotion_bytes)
            (root/rendered_path).write_bytes(rendered)
            record['promotion'].update(environment=owner_profile['environment']['id'],
                profile={'path':P.PROFILE,'resolved_sha256':P.digest(promotion_bytes),'infrastructure_sha256':owner_profile['infrastructureDigest']},
                inventory={'path':P.INVENTORY,'sha256':P.digest(inventory_path.read_bytes())},
                rendered_deployment={'path':rendered_path,'sha256':P.digest(rendered)})
            def bind_deployment(candidate):
                promotion=candidate['promotion']
                promotion['deployment_sha256']=P.digest(P.canonical({'purpose':'publication-rendered-deployment','schemaVersion':1,
                    'oci_index_sha256':candidate['subject']['oci_index_sha256'],
                    'profile_sha256':promotion['profile']['resolved_sha256'],'rendered_sha256':promotion['rendered_deployment']['sha256']}))
                P.validate_record(candidate)
            bind_deployment(record)
            assert P.profile_check(root,record)==composition.relative_to(root).as_posix()
            print('genuine-promotion-associations: PASS',flush=True)
            cases=[('environment',lambda r:r['promotion'].update(environment='environment:other')),
                   ('rendered',lambda r:r['promotion']['rendered_deployment'].update(sha256=P.digest(b'alternate rendered deployment'))),
                   ('oci',lambda r:r['subject'].update(oci_index_sha256='0'*64)),
                   ('package-id',lambda r:r['subject']['packages'][0].update(id='artifact:other')),
                   ('package-version',lambda r:r['subject']['packages'][0].update(version='0.0.1')),
                   ('package-digest',lambda r:r['subject']['packages'][0].update(sha256='0'*64)),
                   ('server',lambda r:r['subject'].update(server_package_sha256='0'*64)),
                   ('extensions',lambda r:r['subject'].update(extensions_sha256=[])),
                   ('catalog',lambda r:r['subject'].update(catalog_sha256='0'*64)),
                   ('runtime',lambda r:r['subject'].update(runtime_sha256='0'*64))]
            # Reused release bindings are fully exercised by v1; v2 additionally
            # proves the exact environment association through the genuine child.
            for label,mutate in (cases if version == 1 else cases[:1]):
                changed=copy.deepcopy(record); mutate(changed); bind_deployment(changed)
                (root/rendered_path).write_bytes(b'alternate rendered deployment' if label=='rendered' else rendered)
                try:
                    P.profile_check(root,changed)
                except P.Refusal as error:
                    assert error.code=='PROFILE_SUBJECT_MISMATCH',(label,error.code)
                else:
                    raise AssertionError('promotion accepted mismatched '+label)
                print('promotion-'+label+'-mismatch: PASS',flush=True)
            prepare()
            sdk_path = root / 'global.json'
            owner_sdk = sdk_path.read_bytes()
            try:
                sdk_path.write_text(json.dumps({'sdk': {'version': '99.0.999', 'rollForward': 'disable'}}))
                try:
                    P.profile_check(root, None)
                except P.Refusal as error:
                    assert error.code == 'PROFILE_OWNER_VALIDATION_FAILED', error.code
                else:
                    raise AssertionError('fixed Python child ignored the owner SDK pin')
            finally:
                sdk_path.write_bytes(owner_sdk)
            print('fixed-python-child-owner-sdk-pin: PASS', flush=True)
            _, missing = P.missing_prerequisites(root, P.git(root, 'rev-parse', 'HEAD'))
            assert set(P.FIXED_MISSING) <= set(missing)
            assert 'RELEASE_EVIDENCE_CODEC_UNAVAILABLE' in missing
            print(f'v{version}-retained-fixed-authority-refusals: PASS', flush=True)
        # A relocated installed toolchain without its reference packs must refuse.
        check_missing_targeting_pack(root, dotnet, scratch)
        for path, content in originals.items():
            assert ((original / path).read_bytes() if (original / path).exists() else None) == content
        print('Bridge integration: both explicit profile versions passed; synthetic controls grant no authority.', flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
