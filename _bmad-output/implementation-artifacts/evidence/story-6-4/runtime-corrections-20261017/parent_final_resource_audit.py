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
    frames = []
    for path in sorted(HERE.glob("live-*-controls.json")):
        if "active-controls" in path.name:
            raise RuntimeError("A runtime frame is still active: " + path.name)
        packet = json.loads(path.read_text())
        if "full" not in packet.get("lane", ""):
            continue
        containers = {item["id"]: absent("container", item["id"]) for item in packet["containers"]}
        volumes = {mount["Name"]: absent("volume", mount["Name"]) for item in packet["containers"] for mount in item["mounts"] if mount["Type"] == "volume"}
        survivors = [item for item in packet.get("observedChildren", []) if same_process(item)]
        directories = {name: not pathlib.Path(name).exists() for name in packet.get("resourceDirectoriesRemoved", {})}
        directories[packet["work"]] = not pathlib.Path(packet["work"]).exists()
        postgres = None
        receipt_path = HERE / (packet["lane"] + "-oq8-cleanup.json")
        if receipt_path.exists():
            receipt = json.loads(receipt_path.read_text())
            identity = receipt.get("postgresContainerId")
            postgres = {"id": identity, "absent": absent("container", identity), "nativeReceipt": receipt_path.name} if identity else None
        ports = {}
        for port in packet.get("physicalPortsClosed", {}):
            with socket.socket() as probe:
                probe.settimeout(0.2)
                ports[port] = probe.connect_ex(("127.0.0.1", int(port))) != 0
        assert all(containers.values()) and all(volumes.values()) and all(directories.values())
        assert not survivors
        assert postgres is None or postgres["absent"]
        assert all(packet.get("physicalPortsClosed", {}).values())
        assert packet["controlExitCodes"] == [0, 0]
        frames.append({"lane": packet["lane"], "recordedNativeExitCode": packet.get("testExitCode"), "containersAbsent": containers, "knownVolumesAbsent": volumes, "recordedChildProcessIdentitiesStillAlive": survivors, "directoriesAbsent": directories, "postgres": postgres, "portsCurrentlyClosed": ports, "portsReboundAfterRecordedRelease": [port for port, closed in ports.items() if not closed], "boundary": "Port reuse by a later process does not grant authority to terminate it. PostgreSQL container absence is checked; this packet does not invent unrecorded volume identities."})
    entry = json.loads((HERE / "parent-source-entry.json").read_text())
    shared = {}
    for line in entry["sharedContainerIds"]:
        identity, name = line.split(" ", 1)
        result = subprocess.run(["docker", "inspect", identity, "--format", "{{.State.Running}}"], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
        shared[name] = {"recordedId": identity, "sameContainerPresentAndRunning": result.returncode == 0 and result.stdout.strip() == "true"}
    limits = {str(path): path.read_text().strip() for path in pathlib.Path("/proc/sys/fs/inotify").iterdir()}
    assert limits == entry["inotifyLimits"]
    report = {"checkedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "scriptSha256": hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(), "frames": frames, "sharedContainerPresence": shared, "inotifyLimitsUnchanged": True, "scope": "Independent physical checks of recorded full-run identities only. No process was stopped, no container/volume was removed, and no host setting or shared service was changed by this audit. Other intermediate frames retain their separate native cleanup receipts."}
    target = HERE / "parent-final-resource-audit.json"
    assert not target.exists()
    target.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"framesAudited": [item["lane"] for item in frames], "ownedRecordedResourcesAbsent": True, "sharedContainers": shared, "inotifyLimitsUnchanged": True}))


if __name__ == "__main__":
    main()
