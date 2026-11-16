"""Independent stdlib-only synthetic v2 canonical/digest vectors; no inventory acceptance.

Run from any directory. Existing v1 vectors are read without modification. No owner
codec or validator is used to construct these v2 expectations.
"""
import hashlib
import json
import runpy
from pathlib import Path

HERE = Path(__file__).resolve().parent
H = lambda label: hashlib.sha256(('synthetic-v2:' + label).encode()).hexdigest()


# Reuse the existing independent BCL-escaping oracle, including restore.order.
encode = runpy.run_path(str(HERE / "verify-golden.py"))["encode"]


def main():
    profile = json.loads((HERE / 'SyntheticProfile.v1.json').read_bytes())
    profile['schemaVersion'] = 2
    environment = profile['environment']['id']
    providers = {provider['id']: provider for provider in profile['infrastructure']['providers']}
    for component in profile['components']:
        component['scope'] = dict(kind='namespace', environmentId=environment, clusterId=component.pop('clusterId'),
                                  nodeId=None, namespace=component.pop('namespace'))
        component['kind'] = 'dapr-component'
        component['softwareVersion'] = providers[component['providerId']]['version']
        component['bundleParentId'] = None
        component['dapr'] = {key: component.pop(key) for key in
                             ('type', 'actorStateStore', 'appScopeIds', 'aclDigest', 'resiliencyDigest', 'dataMigrationDigest')}
        component['dapr']['apiVersion'] = component.pop('version')
    evidence = profile['evidence'][0]
    for name, kind, version in [('distribution', 'synthetic-distribution', '0.0.9-synthetic'),
                                ('containerd', 'containerd', '0.0.2-synthetic'),
                                ('os', 'synthetic-node-os', '0.0.3-synthetic'),
                                ('crd', 'synthetic-crd', '0.0.4-synthetic')]:
        provider = dict(id='provider:' + name, provider=kind, version=version, immutableDigest=H('pin:' + name),
                        topology='synthetic-unqualified', roles=[kind])
        profile['infrastructure']['providers'].append(provider)
        providers[provider['id']] = provider
        evidence['subjectDigests'].append(provider['immutableDigest'])
    rows = [('distribution', 'distribution', 'cluster', 'provider:distribution', 'cluster:workload', None, None),
            ('containerd', 'container-runtime', 'node', 'provider:containerd', 'cluster:workload', 'node:synthetic-1', 'component:distribution'),
            ('os', 'node-os', 'node', 'provider:os', 'cluster:workload', 'node:synthetic-1', 'component:distribution'),
            ('crd', 'crd', 'cluster', 'provider:crd', 'cluster:workload', None, 'component:distribution'),
            ('environment-service', 'service', 'environment', 'provider:issuer', None, None, None),
            ('management', 'kubernetes', 'cluster', 'provider:cluster', 'cluster:management', None, None),
            ('workload', 'kubernetes', 'cluster', 'provider:cluster', 'cluster:workload', None, 'component:distribution'),
            ('rancher', 'rancher', 'namespace', 'provider:rancher', 'cluster:management', None, None)]
    for name, kind, scope, provider, cluster, node, parent in rows:
        loaded = H('loaded:' + name)
        component = dict(id='component:' + name, name='synthetic-' + name, kind=kind,
                         scope=dict(kind=scope, environmentId=environment, clusterId=cluster, nodeId=node,
                                    namespace='synthetic-management' if scope == 'namespace' else None),
                         providerId=provider, softwareVersion=providers[provider]['version'], loadedIdentityDigest=loaded,
                         bundleParentId=parent, dapr=None, stateful=False, roles=[kind], physicalTargetIds=[],
                         credentialReferenceIds=[], supportSources=['https://support.invalid/' + name],
                         advisorySources=['https://advisory.invalid/' + name], currencyOwner='owner:synthetic',
                         renewalOwners=['owner:synthetic'], evidenceIds=[evidence['id']])
        profile['components'].append(component)
        evidence['componentIds'].append(component['id'])
        evidence['subjectDigests'].append(loaded)
    preimage = encode(dict(purpose='platform-infrastructure-profile', schemaVersion=2,
                           template=profile['template'], infrastructure=profile['infrastructure']))
    profile['infrastructureDigest'] = hashlib.sha256(preimage).hexdigest()
    document = encode(profile)
    (HERE / 'SyntheticInfrastructurePreimage.v2.json').write_bytes(preimage)
    (HERE / 'SyntheticProfile.v2.json').write_bytes(document)
    (HERE / 'SyntheticDigests.v2.json').write_bytes(encode(dict(infrastructure=profile['infrastructureDigest'],
                                                              resolved=hashlib.sha256(document).hexdigest())))


if __name__ == '__main__':
    main()
