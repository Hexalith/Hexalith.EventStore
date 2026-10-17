"""Read-only physical checks of recorded task resources after the finite runs."""
import datetime
import hashlib
import json
import pathlib
import socket
import subprocess

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[4]


def absent(kind, identity):
    command = ["docker", kind, "inspect", identity] if kind == "volume" else ["docker", "inspect", identity]
    return subprocess.run(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode != 0


def same_process(item):
    stat = pathlib.Path("/proc") / str(item["pid"]) / "stat"
    try:
        raw = stat.read_text()
        fields = raw[raw.rindex(")") + 2:].split()
        return fields[19] == item["startTicks"]
    except (OSError, ValueError, IndexError):
        return False


def main():
    assert not list(HERE.glob('*active-controls.json')), 'A native runtime frame is active'
    frames = []
    for lane in ('oq8-baseline', 'release-live-full'):
        path = HERE / (lane + '-controls.json')
        packet = json.loads(path.read_text())
        containers = {item['id']: absent('container', item['id']) for item in packet['containers']}
        volumes = {mount['Name']: absent('volume', mount['Name']) for item in packet['containers'] for mount in item['mounts'] if mount['Type'] == 'volume'}
        survivors = [item for item in packet.get('observedChildren', []) if same_process(item)]
        directories = {name: not pathlib.Path(name).exists() for name in packet.get('resourceDirectoriesRemoved', {})}
        directories[packet['work']] = not pathlib.Path(packet['work']).exists()
        receipt_path = HERE / (lane + '-oq8-cleanup.json')
        receipt = json.loads(receipt_path.read_text())
        assert receipt['runtimeScratchRemoved'] and receipt['processesStopped']
        postgres = {'id': receipt['postgresContainerId'], 'absent': absent('container', receipt['postgresContainerId']), 'nativeReceipt': receipt_path.name}
        ports = {}
        for port in packet.get('physicalPortsClosed', {}):
            with socket.socket() as probe:
                probe.settimeout(0.2)
                ports[port] = probe.connect_ex(('127.0.0.1', int(port))) != 0
        assert all(containers.values()) and all(volumes.values()) and all(directories.values())
        assert not survivors and postgres['absent']
        assert all(packet.get('physicalPortsClosed', {}).values())
        assert packet['controlExitCodes'] == [0, 0]
        frames.append({'lane': lane, 'nativeExitCode': packet['testExitCode'], 'containersAbsent': containers, 'knownVolumesAbsent': volumes, 'recordedChildIdentitiesAlive': survivors, 'directoriesAbsent': directories, 'postgres': postgres, 'portsCurrentlyClosed': ports, 'portsReusedAfterRecordedRelease': [port for port, closed in ports.items() if not closed]})
    shared = {}
    first_inventory = {'eb6cc1ff457e':'security-rbzszwcz', '32ff95f6f2dc':'tunnel-proxy-5c9e2b8d-8f46-40ee-b7a0-085d9aba7799', '22ab85451b3d':'story61-deps-jt9oasms-dapr', '74a851a40d8e':'story61-deps-jt9oasms-redis', '0719fe8c59bd':'dapr_placement', '8a7dea8bcfe1':'dapr_scheduler', '8d0788f8322f':'dapr_redis', 'cb7c12622c18':'dapr_zipkin'}
    for identity, name in first_inventory.items():
        result = subprocess.run(['docker','inspect',identity,'--format','{{.Id}} {{.State.Running}}'],stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True)
        shared[name] = {'initialReadOnlyInventoryIdPrefix': identity, 'currentIdentityAndRunning': result.stdout.strip(), 'present': result.returncode == 0}
    report = {'checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scriptSha256':hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),'frames':frames,'sharedContainerPresence':shared,'scope':'Read-only physical checks of task-owned recorded identities. No process/container/volume/host setting was mutated by this audit. Later port owners are never terminated. PostgreSQL container absence is verified without inventing unrecorded volume IDs. Shared identity prefixes come from this continuation first read-only Docker inventory; no formal acceptance or initial inotify snapshot is inferred.'}
    target = HERE/'parent-resource-audit.json';assert not target.exists()
    target.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({'framesAudited':[f['lane'] for f in frames],'recordedOwnedResourcesAbsent':True,'portsReused':{f['lane']:f['portsReusedAfterRecordedRelease'] for f in frames}}))


if __name__ == '__main__':
    main()
