#!/usr/bin/env python3
"""Exercise the genuine bridge and fixed Python child in disposable repository copies.

CI uses package references and Release; local verification uses Debug source references.
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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    args = parser.parse_args()
    original = args.root.resolve()
    executable = shutil.which('dotnet')
    if not executable:
        raise AssertionError('dotnet SDK is required; this integration check cannot skip')
    dotnet = str(Path(executable).resolve())
    configuration = 'Release' if os.environ.get('CI') else 'Debug'
    flags = ['-p:UseHexalithProjectReferences=' + ('false' if os.environ.get('CI') else 'true'),
             '-p:UseNuGetDeps=' + ('true' if os.environ.get('CI') else 'false'),
             '-p:BuildInParallel=false', '--disable-build-servers']
    with tempfile.TemporaryDirectory(prefix='publication-bridge-integration-') as directory:
        scratch = Path(directory)
        workspace = scratch / 'workspace'
        root = workspace / 'references/Hexalith.EventStore'
        # Preserve the root-declared source layout without initializing nested submodules.
        superproject = subprocess.check_output(['git', '-C', str(original), 'rev-parse', '--show-superproject-working-tree'], text=True).strip()
        if superproject:
            workspace.mkdir(parents=True)
            shutil.copy2(Path(superproject) / '.gitmodules', workspace / '.gitmodules')
        copy_repository(original, root)
        for name in ('Hexalith.Builds', 'Hexalith.Commons', 'Hexalith.Tenants'):
            candidates = (original / 'references' / name, original.parent / name)
            for candidate in candidates:
                if not candidate.is_dir():
                    continue
                repository_root = subprocess.run(['git', '-C', str(candidate), 'rev-parse', '--show-toplevel'], capture_output=True, text=True)
                if repository_root.returncode == 0 and Path(repository_root.stdout.strip()).resolve() == candidate.resolve():
                    copy_repository(candidate, workspace / 'references' / name)
                    break
            else:
                if name == 'Hexalith.Builds' or not os.environ.get('CI'):
                    raise AssertionError('required root-declared source is unavailable: ' + name)
        sys.path.insert(0, str(root / 'tools'))
        import publication_authority as P
        environment = {key: value for key, value in os.environ.items()
                       if key in ('PATH', 'HOME', 'DOTNET_ROOT', 'NUGET_PACKAGES', 'TMPDIR', 'TMP', 'TEMP', 'CI')}
        environment.update(MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0')
        artifacts = scratch / 'artifacts'
        run([dotnet, 'build', 'tools/production-profile.cs', '-c', configuration, *flags, '--artifacts-path', str(artifacts)], root, environment=environment)
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
                result = run([dotnet, 'run', '--file', 'tools/production-profile.cs', '--no-build', '-c', configuration,
                              *flags, '--artifacts-path', str(artifacts), '--', str(root), P.digest(inventory_path.read_bytes()),
                              resolved or digests['resolved'], digests['infrastructure']], root, expected, environment)
                assert sentinel not in result.stdout + result.stderr
                assert 'Unhandled exception' not in result.stderr and ' at Program.' not in result.stderr
                if issue is not None:
                    assert issue in {item['Code'] for item in json.loads(result.stderr)}
                if expected == 0:
                    document = json.loads(result.stdout)
                    assert document['isStructurallyValid'] and document['authorization'] == document['qualification'] == 'unevaluated'
                print(f'v{version}-' + label + ': PASS', flush=True)

            prepare(); bridge('complete-synthetic', 0)
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
            profile_path.rename(profile_path.with_name('alternate-profile.yaml')); bridge('missing-sole-slot', 2)
            prepare({**inventory, 'source': {'path': P.INVENTORY, 'sha256': 'a'*64}}); bridge('self-inventory-source', 2)
            prepare({**inventory, 'schema': sentinel}); bridge('wrong-schema-no-echo', 2)
            inventory_path.write_text('{"schema":"' + sentinel + '","schema":"duplicate"}')
            bridge('duplicate-fields-no-echo', 2)
            prepare(); inventory_path.write_text(sentinel); bridge('malformed-json-no-echo', 2)
            # Exercise the real production child path, including its filtered PATH/HOME,
            # copied owner SDK pin, package cache and forced build-server protections.
            prepare()
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
        print('Bridge integration: both explicit profile versions passed; synthetic controls grant no authority.', flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
